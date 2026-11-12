using System.Collections.Generic;
using UnityEngine;

namespace Breakline
{
    public enum PropKind
    {
        Barrel,
        AmmoCrate
    }

    public sealed class ExplosiveProp
    {
        internal GameObject Go;
        internal SpriteRenderer Renderer;
        internal PropKind Kind;
        internal Vector2 Position;
        internal Vector2 HalfExtents;
        internal float HP;
        internal bool Alive = true;
        internal bool Scheduled;
        internal float Fuse;
    }

    /// <summary>
    /// Shootable barrels/crates with delayed, capped chain detonation.
    /// </summary>
    public sealed class PropSystem
    {
        private readonly GameRoot _game;
        private readonly List<ExplosiveProp> _props = new List<ExplosiveProp>(16);
        private readonly System.Random _rng = new System.Random(0x517E);
        private Transform _parent;
        private int _chainCount;
        private float _chainWindowEnd;


        public int PendingChainCount
        {
            get
            {
                int c = 0;
                for (int i = 0; i < _props.Count; i++)
                    if (_props[i].Alive && _props[i].Scheduled) c++;
                return c;
            }
        }

        public int ChainCount => _chainCount;

        public PropSystem(GameRoot game)
        {
            _game = game;
        }

        public void Init(Transform parent)
        {
            _parent = new GameObject("ExplosiveProps").transform;
            _parent.SetParent(parent, false);
        }

        public ExplosiveProp SpawnBarrel(Vector2 center)
        {
            return Spawn(PropKind.Barrel, center, _game.Art.BarrelSprite, new Vector2(0.31f, 0.44f),
                _game.Config.barrelHP);
        }

        public ExplosiveProp SpawnCrate(Vector2 center)
        {
            return Spawn(PropKind.AmmoCrate, center, _game.Art.CrateSprite, new Vector2(0.37f, 0.37f),
                _game.Config.crateHP);
        }

        private ExplosiveProp Spawn(PropKind kind, Vector2 center, Sprite sprite, Vector2 half, float hp)
        {
            GameObject go = new GameObject(kind == PropKind.Barrel ? "Barrel" : "AmmoCrate");
            go.transform.SetParent(_parent, false);
            go.transform.position = new Vector3(center.x, center.y, 0f);
            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = 5;
            var prop = new ExplosiveProp
            {
                Go = go,
                Renderer = sr,
                Kind = kind,
                Position = center,
                HalfExtents = half,
                HP = hp
            };
            _props.Add(prop);
            return prop;
        }

        public void Tick(float dt)
        {
            if (_chainWindowEnd > 0f && Time.time > _chainWindowEnd && PendingChainCount == 0)
            {
                _chainCount = 0;
                _chainWindowEnd = 0f;
            }

            int detonateBudget = Mathf.Max(1, _game.Config.chainPerFrame);
            for (int i = 0; i < _props.Count && detonateBudget > 0; i++)
            {
                ExplosiveProp p = _props[i];
                if (!p.Alive) continue;
                if (p.Scheduled)
                {
                    p.Fuse -= dt;
                    if (p.Renderer != null)
                    {
                        bool on = Mathf.FloorToInt(Time.unscaledTime * 30f) % 2 == 0;
                        p.Renderer.color = on ? new Color(1f, 0.82f, 0.40f) : Color.white;
                    }
                    if (p.Fuse <= 0f)
                    {
                        p.Scheduled = false;
                        Detonate(p);
                        detonateBudget--;
                    }
                }
                else if (p.Renderer != null)
                {
                    p.Renderer.color = Color.white;
                }
            }
        }

        public bool RaycastFirst(Vector2 origin, Vector2 dir, float maxDist, out ExplosiveProp prop,
            out float distance, out Vector2 point)
        {
            prop = null;
            distance = maxDist;
            point = origin + dir * maxDist;
            if (dir.sqrMagnitude < 0.0001f) return false;
            dir.Normalize();
            Vector2 end = origin + dir * maxDist;
            bool found = false;
            float best = maxDist;
            for (int i = 0; i < _props.Count; i++)
            {
                ExplosiveProp p = _props[i];
                if (!p.Alive) continue;
                Rect r = new Rect(p.Position.x - p.HalfExtents.x, p.Position.y - p.HalfExtents.y,
                    p.HalfExtents.x * 2f, p.HalfExtents.y * 2f);
                if (!MathUtil.SegmentRect(origin, end, r, out float t)) continue;
                float d = t * maxDist;
                if (d < best)
                {
                    best = d;
                    prop = p;
                    found = true;
                }
            }
            if (found)
            {
                distance = best;
                point = origin + dir * best;
            }
            return found;
        }

        public void DamageProp(ExplosiveProp p, float damage, Vector2 point, Vector2 hitDir)
        {
            if (p == null || !p.Alive) return;
            if (p.Scheduled) return;
            p.HP -= damage;
            _game.Vfx.WorldFlash(point, 0.22f, new Color(1f, 0.82f, 0.4f, 0.8f), 0.05f);
            if (p.HP > 0f) return;
            ScheduleInitial(p);
        }

        private void ScheduleInitial(ExplosiveProp p)
        {
            if (_chainWindowEnd <= 0f || Time.time > _chainWindowEnd)
            {
                _chainCount = 0;
                _chainWindowEnd = Time.time + 1.5f;
            }
            p.Scheduled = true;
            p.Fuse = 0.02f;
            _chainCount++;
        }

        /// <summary>
        /// Called by a central explosion. Other props in range get a short, readable delay,
        /// not an immediate recursive call.
        /// </summary>
        public void TriggerInRadius(Vector2 center, float radius, ExplosiveProp source)
        {
            if (_chainWindowEnd <= 0f || Time.time > _chainWindowEnd)
            {
                _chainCount = 0;
                _chainWindowEnd = Time.time + 1.5f;
            }

            for (int i = 0; i < _props.Count; i++)
            {
                ExplosiveProp p = _props[i];
                if (p == null || p == source || !p.Alive || p.Scheduled) continue;
                if (_chainCount >= _game.Config.chainMax) break;
                float dist = (p.Position - center).magnitude;
                if (dist > radius) continue;
                p.Scheduled = true;
                p.Fuse = Mathf.Lerp(_game.Config.chainDelayMin, _game.Config.chainDelayMax,
                    (float)_rng.NextDouble());
                _chainCount++;
                _chainWindowEnd = Time.time + 1.5f;
            }
        }

        private void Detonate(ExplosiveProp p)
        {
            if (!p.Alive) return;
            p.Alive = false;
            if (p.Go != null)
                p.Go.SetActive(false);

            float radius = p.Kind == PropKind.Barrel ? _game.Config.barrelRadius : _game.Config.crateRadius;
            float damage = p.Kind == PropKind.Barrel ? _game.Config.barrelDamage : _game.Config.crateDamage;
            float terrainDamage = p.Kind == PropKind.Barrel ? _game.Config.barrelTerrainDamage : _game.Config.crateTerrainDamage;
            _game.ExplodeAt(p.Position, radius, damage, radius, terrainDamage, p);
        }

        public int AliveCount()
        {
            int c = 0;
            for (int i = 0; i < _props.Count; i++)
                if (_props[i].Alive) c++;
            return c;
        }
    }
}
