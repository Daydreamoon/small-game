using System.Collections.Generic;
using UnityEngine;

namespace Breakline
{
    public enum EnemyKind
    {
        Dummy,
        Patrol,
        ArmoredCrush
    }

    public sealed class Enemy
    {
        private readonly EnemySystem _system;
        internal GameObject Go;
        internal SpriteRenderer Renderer;
        internal Sprite Sprite;
        internal EnemyKind Kind;
        internal Vector2 Position;
        internal Vector2 HalfExtents;
        internal float HP;
        internal bool Alive;
        internal bool Targetable;
        internal float BaseGravity = 30f;
        internal float VelocityY;
        internal float PatrolMin;
        internal float PatrolMax;
        internal int Facing = 1;
        internal float PatrolSpeed;
        internal float ContactDamage;
        internal bool Grounded;
        private float _flash;
        private Color _baseColor;

        internal Enemy(EnemySystem system, EnemyKind kind, Vector2 pos, float hp, Sprite sprite, Vector2 halfExtents)
        {
            _system = system;
            Kind = kind;
            Position = pos;
            HP = hp;
            Sprite = sprite;
            HalfExtents = halfExtents;
            Alive = true;
            Targetable = true;
        }

        internal void Attach(GameObject go, SpriteRenderer sr)
        {
            Go = go;
            Renderer = sr;
            _baseColor = sr.color;
        }

        public Rect Bounds => new Rect(Position.x - HalfExtents.x, Position.y - HalfExtents.y,
            HalfExtents.x * 2f, HalfExtents.y * 2f);

        public Vector2 Center => Position;
        public bool IsAlive => Alive;
        public bool IsArmoredCrush => Kind == EnemyKind.ArmoredCrush;
        public float Health => HP;

        internal void Tick(float dt)
        {
            if (!Alive)
            {
                if (_flash > 0f)
                {
                    _flash -= dt;
                    if (_flash <= 0f && Renderer != null)
                        Renderer.color = _baseColor;
                }
                return;
            }

            if (_flash > 0f)
            {
                _flash -= dt;
                if (Renderer != null)
                    Renderer.color = _flash > 0f ? Color.white : _baseColor;
            }

            UpdateGravity(dt);

            if (Kind == EnemyKind.Patrol)
            {
                float nextX = Position.x + Facing * PatrolSpeed * dt;
                bool blocked = _system.Terrain.SolidAtWorld(nextX + Facing * (HalfExtents.x + 0.05f), Position.y);
                bool floorAhead = _system.Terrain.SolidAtWorld(nextX + Facing * (HalfExtents.x + 0.08f), Bounds.yMin - 0.15f);
                if (blocked || !floorAhead || nextX < PatrolMin || nextX > PatrolMax)
                {
                    Facing = -Facing;
                }
                else
                {
                    Position.x = nextX;
                }
                if (Renderer != null) Renderer.flipX = Facing < 0;
            }

            if (Renderer != null)
                Renderer.transform.position = new Vector3(Position.x, Position.y, 0f);
        }

        private void UpdateGravity(float dt)
        {
            bool groundBelow = _system.Terrain.SolidAtWorld(Position.x, Bounds.yMin - 0.06f);
            if (groundBelow && VelocityY <= 0f)
            {
                Grounded = true;
                VelocityY = 0f;
                return;
            }
            Grounded = false;
            VelocityY -= BaseGravity * dt;
            Position.y += VelocityY * dt;

            if (VelocityY < 0f)
            {
                int cx = Mathf.FloorToInt(Position.x);
                int cellY = Mathf.FloorToInt(Position.y - HalfExtents.y);
                if (_system.Terrain.IsSolid(cx, cellY))
                {
                    Position.y = cellY + 1f + HalfExtents.y;
                    VelocityY = 0f;
                    Grounded = true;
                }
            }
        }

        internal bool TakeHit(float damage, Vector2 hitPoint, Vector2 hitDir)
        {
            if (!Alive) return false;
            _flash = 0.055f;
            if (Renderer != null) Renderer.color = Color.white;
            if (Kind == EnemyKind.ArmoredCrush)
            {
                _system.ArmoredSpark(hitPoint);
                return false;
            }

            HP -= damage;
            if (HP <= 0f)
            {
                Die(hitPoint, hitDir, false);
                return true;
            }
            _system.BloodPuff(hitPoint, hitDir, 3);
            return true;
        }

        internal void Crush()
        {
            if (!Alive) return;
            Alive = false;
            Targetable = false;
            VelocityY = 0f;
            if (Renderer != null)
            {
                Renderer.sortingOrder = 30;
                Renderer.color = new Color(0.45f, 0.37f, 0.34f, 1f);
                Go.transform.localScale = new Vector3(1.18f, 0.22f, 1f);
                Go.transform.position = new Vector3(Position.x, Position.y - HalfExtents.y * 0.78f, 0f);
            }
            _system.OnCrushed(this);
        }

        internal void Die(Vector2 hitPoint, Vector2 hitDir, bool crushed)
        {
            if (!Alive) return;
            Alive = false;
            Targetable = false;
            _system.OnKilled(this, hitPoint, hitDir, crushed);
            if (Go != null)
                Go.SetActive(false);
        }

        internal void ForceKillByDebug()
        {
            if (!Alive) return;
            Die(Position, Vector2.up, false);
        }
    }

    public sealed class EnemySystem
    {
        private sealed class Corpse
        {
            public GameObject Go;
            public SpriteRenderer Renderer;
            public Rigidbody2D Body;
            public float Life;
            public bool Active;
        }

        private readonly GameRoot _game;
        private readonly List<Enemy> _enemies = new List<Enemy>(32);
        private readonly List<Corpse> _corpses = new List<Corpse>(16);
        private Transform _parent;
        public TerrainSystem Terrain => _game.Terrain;
        public IReadOnlyList<Enemy> All => _enemies;
        public Enemy ArmoredTarget { get; private set; }
        public int AliveCount
        {
            get
            {
                int c = 0;
                for (int i = 0; i < _enemies.Count; i++)
                    if (_enemies[i].Alive) c++;
                return c;
            }
        }

        public int ActiveCorpseCount
        {
            get
            {
                int c = 0;
                for (int i = 0; i < _corpses.Count; i++)
                    if (_corpses[i].Active) c++;
                return c;
            }
        }

        public EnemySystem(GameRoot game)
        {
            _game = game;
        }

        public void Init(Transform parent)
        {
            _parent = new GameObject("Enemies").transform;
            _parent.SetParent(parent, false);
        }

        public Enemy SpawnDummy(Vector2 center)
        {
            Enemy e = CreateEnemy(EnemyKind.Dummy, center, _game.Config.dummyHP, _game.Art.DummySprite, new Vector2(0.37f, 0.56f));
            return e;
        }

        public Enemy SpawnPatrol(Vector2 center, float minX, float maxX)
        {
            Enemy e = CreateEnemy(EnemyKind.Patrol, center, _game.Config.patrolHP, _game.Art.PatrolSprite, new Vector2(0.37f, 0.56f));
            e.PatrolMin = minX;
            e.PatrolMax = maxX;
            e.PatrolSpeed = _game.Config.patrolSpeed;
            e.ContactDamage = _game.Config.patrolContactDamage;
            return e;
        }

        public Enemy SpawnArmoredCrush(Vector2 center)
        {
            Enemy e = CreateEnemy(EnemyKind.ArmoredCrush, center, 9999f, _game.Art.ArmoredSprite, new Vector2(0.55f, 0.68f));
            ArmoredTarget = e;
            return e;
        }

        private Enemy CreateEnemy(EnemyKind kind, Vector2 center, float hp, Sprite sprite, Vector2 halfExtents)
        {
            GameObject go = new GameObject("Enemy_" + kind);
            go.transform.SetParent(_parent, false);
            go.transform.position = new Vector3(center.x, center.y, 0f);
            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = kind == EnemyKind.ArmoredCrush ? 12 : (kind == EnemyKind.Patrol ? 11 : 10);
            sr.color = Color.white;
            Enemy e = new Enemy(this, kind, center, hp, sprite, halfExtents);
            e.Attach(go, sr);
            _enemies.Add(e);
            return e;
        }

        public void Tick(float dt)
        {
            for (int i = 0; i < _enemies.Count; i++)
                _enemies[i].Tick(dt);

            PlayerController player = _game.Player;
            if (player != null && player.IsAlive)
            {
                Rect pBounds = player.Bounds;
                for (int i = 0; i < _enemies.Count; i++)
                {
                    Enemy e = _enemies[i];
                    if (!e.Alive || e.Kind != EnemyKind.Patrol || e.ContactDamage <= 0f) continue;
                    if (MathUtil.RectOverlaps(pBounds, e.Bounds))
                        player.TakeDamage(e.ContactDamage, e.Center);
                }
            }

            for (int i = 0; i < _corpses.Count; i++)
            {
                Corpse c = _corpses[i];
                if (!c.Active) continue;
                c.Life -= dt;
                if (c.Life < 0.5f)
                {
                    Color col = c.Renderer.color;
                    col.a = Mathf.Clamp01(c.Life / 0.5f);
                    c.Renderer.color = col;
                }
                if (c.Life <= 0f)
                {
                    c.Active = false;
                    c.Go.SetActive(false);
                }
            }
        }

        public Enemy FindNearestInRange(Vector2 origin, float range, bool includeArmored = true)
        {
            Enemy best = null;
            float bestD = range * range;
            for (int i = 0; i < _enemies.Count; i++)
            {
                Enemy e = _enemies[i];
                if (!e.Alive || !e.Targetable) continue;
                if (!includeArmored && e.Kind == EnemyKind.ArmoredCrush) continue;
                float d = (e.Center - origin).sqrMagnitude;
                if (d < bestD)
                {
                    bestD = d;
                    best = e;
                }
            }
            return best;
        }

        public void DamageRadius(Vector2 center, float radius, float damage)
        {
            for (int i = 0; i < _enemies.Count; i++)
            {
                Enemy e = _enemies[i];
                if (!e.Alive || !e.Targetable) continue;
                float approx = (e.Center - center).magnitude - e.HalfExtents.magnitude;
                if (approx > radius) continue;
                float falloff = Mathf.Clamp01(1f - Mathf.Max(0f, approx) / Mathf.Max(0.1f, radius));
                float dmg = damage * (0.35f + 0.65f * falloff);
                Vector2 dir = (e.Center - center).sqrMagnitude > 0.0001f ? (e.Center - center).normalized : Vector2.up;
                e.TakeHit(dmg, e.Center, dir);
            }
        }

        public bool CrushInRect(Rect rect, float speed)
        {
            if (speed < 0.7f) return false;
            bool any = false;
            for (int i = 0; i < _enemies.Count; i++)
            {
                Enemy e = _enemies[i];
                if (!e.Alive) continue;
                if (MathUtil.RectOverlaps(rect, e.Bounds))
                {
                    e.Crush();
                    any = true;
                }
            }
            return any;
        }

        internal void OnKilled(Enemy enemy, Vector2 hitPoint, Vector2 hitDir, bool crushed)
        {
            if (crushed)
            {
                OnCrushed(enemy);
                return;
            }
            _game.Feedback.NormalKill();
            _game.Audio.Play(AudioCue.Land, 0.55f);
            _game.Debris.SpawnBlood(enemy.Center, hitDir, 10);
            _game.Debris.SpawnDust(enemy.Center, 4);
            SpawnCorpse(enemy, hitDir);
            if (enemy.Kind == EnemyKind.ArmoredCrush && _game != null)
                _game.OnCrushTargetKilled();
        }

        internal void OnCrushed(Enemy enemy)
        {
            _game.Feedback.Crush();
            _game.Audio.Play(AudioCue.Crush, 0.9f);
            _game.Debris.SpawnDust(new Vector2(enemy.Position.x, enemy.Bounds.yMin + 0.25f), 22);
            _game.Vfx.SpawnRing(enemy.Center, 1.8f, new Color(0.8f, 0.65f, 0.4f, 0.65f), 0.25f, true);
            _game.Hud.ShowWorldWarning(enemy.Position, "可压死");
            if (enemy.Kind == EnemyKind.ArmoredCrush)
                _game.OnCrushTargetKilled();
        }

        internal void BloodPuff(Vector2 hitPoint, Vector2 hitDir, int count)
        {
            _game.Debris.SpawnBlood(hitPoint, hitDir, count);
        }

        internal void ArmoredSpark(Vector2 hitPoint)
        {
            _game.Vfx.WorldFlash(hitPoint, 0.28f, new Color(1f, 0.90f, 0.5f, 0.95f), 0.055f);
            _game.Audio.Play(AudioCue.Tick, 0.6f);
        }

        private void SpawnCorpse(Enemy enemy, Vector2 hitDir)
        {
            Corpse corpse = null;
            for (int i = 0; i < _corpses.Count; i++)
            {
                if (!_corpses[i].Active) { corpse = _corpses[i]; break; }
            }
            if (corpse == null)
            {
                if (_corpses.Count >= _game.Config.corpseMax) return;
                GameObject go = new GameObject("Corpse");
                go.transform.SetParent(_parent, false);
                go.layer = TerrainSystem.DebrisLayer;
                SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
                sr.sortingOrder = 23;
                Rigidbody2D rb = go.AddComponent<Rigidbody2D>();
                rb.bodyType = RigidbodyType2D.Dynamic;
                rb.gravityScale = 1.3f;
                rb.mass = 0.3f;
                rb.collisionDetectionMode = CollisionDetectionMode2D.Discrete;
                BoxCollider2D col = go.AddComponent<BoxCollider2D>();
                col.size = new Vector2(0.55f, 0.85f);
                go.SetActive(false);
                corpse = new Corpse { Go = go, Renderer = sr, Body = rb };
                _corpses.Add(corpse);
            }

            corpse.Active = true;
            corpse.Go.SetActive(true);
            corpse.Go.transform.position = new Vector3(enemy.Position.x, enemy.Position.y, -0.12f);
            corpse.Go.transform.localScale = Vector3.one;
            corpse.Renderer.sprite = enemy.Sprite;
            corpse.Renderer.flipX = enemy.Facing < 0;
            corpse.Renderer.color = Color.white;
            corpse.Body.simulated = true;
            corpse.Body.velocity = new Vector2(hitDir.x * 5.5f + RandomSign() * 1.2f, 5.5f + RandomSign() * 1.5f);
            corpse.Body.angularVelocity = RandomSign() * 520f;
            corpse.Life = 2.8f;
        }

        private static float RandomSign()
        {
            return Random.value < 0.5f ? -1f : 1f;
        }

        public Enemy FindTarget(Vector2 origin, Vector2 preferredDirection, bool useDirection, bool includeArmored = true)
        {
            Enemy best = null;
            float bestScore = float.MaxValue;
            float maxRange = _game.Config.maxTargetRange;
            float maxAngle = _game.Config.aimHalfAngle;
            for (int i = 0; i < _enemies.Count; i++)
            {
                Enemy e = _enemies[i];
                if (!e.Alive || !e.Targetable) continue;
                if (!includeArmored && e.Kind == EnemyKind.ArmoredCrush) continue;
                Vector2 delta = e.Center - origin;
                float dist = delta.magnitude;
                if (dist > maxRange || dist < 0.01f) continue;
                if (!_game.Viewport.IsWorldVisible(e.Center, 0.05f)) continue;
                if (!_game.Terrain.HasLineOfSight(origin, e.Center)) continue;
                Vector2 dir = delta / dist;
                if (useDirection)
                {
                    float angle = Vector2.Angle(preferredDirection, dir);
                    if (angle > maxAngle) continue;
                    float anglePenalty = angle * 0.025f;
                    float score = dist + anglePenalty + (e.IsArmoredCrush ? 0.8f : 0f);
                    if (score < bestScore)
                    {
                        bestScore = score;
                        best = e;
                    }
                }
                else
                {
                    float score = dist + (e.IsArmoredCrush ? 0.8f : 0f);
                    if (score < bestScore)
                    {
                        bestScore = score;
                        best = e;
                    }
                }
            }
            return best;
        }

        internal bool IsLineOfSightClear(Vector2 from, Vector2 to)
        {
            return _game.Terrain.HasLineOfSight(from, to);
        }
    }

}
