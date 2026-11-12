using System.Collections.Generic;
using UnityEngine;

namespace Breakline
{
    /// <summary>
    /// Three-tier debris:
    /// T2: one dynamic mesh, hundreds of cheap simulated quads.
    /// T1: a capped pool of Rigidbody2D pieces that collide only with terrain.
    /// T0: falling terrain islands, owned by TerrainSystem.
    /// </summary>
    public sealed class DebrisSystem
    {
        private struct VPart
        {
            public Vector2 Pos;
            public Vector2 Vel;
            public float Angle;
            public float AngleVel;
            public float Size;
            public float Life;
            public float MaxLife;
            public float GravityScale;
            public int Tile;
            public Color Color;
            public bool Alive;
        }

        private sealed class PhysPiece
        {
            public GameObject Go;
            public SpriteRenderer Renderer;
            public Rigidbody2D Body;
            public BoxCollider2D Collider;
            public float Life;
            public float MaxLife;
            public bool Active;
        }

        private readonly GameRoot _game;
        private readonly System.Random _rng = new System.Random(0x0B4EA);
        private VPart[] _visuals;
        private int _visualCount;
        private GameObject _meshGo;
        private Mesh _mesh;
        private MeshRenderer _meshRenderer;
        private readonly List<Vector3> _verts = new List<Vector3>(1200);
        private readonly List<Vector2> _uvs = new List<Vector2>(1200);
        private readonly List<Color> _colors = new List<Color>(1200);
        private readonly List<int> _tris = new List<int>(1800);
        private Transform _parent;
        private Texture2D _whiteTile;
        private Sprite _whiteSprite;

        private readonly List<PhysPiece> _normalPool = new List<PhysPiece>(48);
        private readonly List<PhysPiece> _stressBodies = new List<PhysPiece>(256);
        private PhysicsMaterial2D _bounceMat;

        public int ActiveVisualCount => _visualCount + CountActive(_normalPool) + CountActive(_stressBodies);
        public int T2Count => _visualCount;
        public int T1Count => CountActive(_normalPool);
        public int StressBodyCount => CountActive(_stressBodies);

        public DebrisSystem(GameRoot game)
        {
            _game = game;
        }

        public void Init(Transform parent)
        {
            _parent = new GameObject("Debris").transform;
            _parent.SetParent(parent, false);
            int cap = Mathf.Max(120, _game.Config.visualPool);
            _visuals = new VPart[cap];
            _whiteTile = _game.Art.TileAtlas;
            _whiteSprite = _game.Art.TileSprites[4];
            _bounceMat = new PhysicsMaterial2D("DebrisBouncy")
            {
                bounciness = 0.16f,
                friction = 0.55f
            };

            _meshGo = new GameObject("T2DebrisMesh");
            _meshGo.transform.SetParent(_parent, false);
            _mesh = new Mesh { name = "T2DebrisMesh" };
            _mesh.MarkDynamic();
            MeshFilter mf = _meshGo.AddComponent<MeshFilter>();
            mf.sharedMesh = _mesh;
            _meshRenderer = _meshGo.AddComponent<MeshRenderer>();
            _meshRenderer.sharedMaterial = _game.Art.DebrisMaterial;
            _meshRenderer.sortingOrder = 24;
            _meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _meshRenderer.receiveShadows = false;
            _meshRenderer.enabled = false;

            for (int i = 0; i < _game.Config.fullBodyMax; i++)
                _normalPool.Add(CreatePhysPiece("t1Debris", true));
        }

        public void Tick(float dt)
        {
            TickVisuals(dt);
            TickPhysical(dt);
            RebuildVisualMesh();
        }

        public void SpawnFromCell(byte type, Vector2 worldCenter)
        {
            int tile = (type >= 1 && type < _game.Art.TileUvIndex.Length) ? _game.Art.TileUvIndex[type] : 4;
            int burst = type == TerrainSystem.Dirt ? 3 : (type == TerrainSystem.Concrete ? 4 : 3);
            for (int i = 0; i < burst; i++)
                SpawnVisual(worldCenter + RandomOffset(0.25f), tile, new Color(1f, 1f, 1f, 1f), 0.75f);

            if (_rng.NextDouble() < 0.30)
                TrySpawnPhysical(type, worldCenter, false);
        }

        public void SpawnDust(Vector2 center, int count)
        {
            for (int i = 0; i < count; i++)
            {
                SpawnVisual(center + RandomOffset(0.8f), 4, DirtDustColor(), 0.8f);
                if (i < 64)
                    SpawnVisual(center + RandomOffset(0.70f), 4, new Color(0.85f, 0.80f, 0.68f, 0.35f), 0.45f);
            }
        }

        public void SpawnBlood(Vector2 pos, Vector2 direction, int count)
        {
            Color blood = new Color(0.72f, 0.10f, 0.08f, 1f);
            Color bright = new Color(1f, 0.25f, 0.12f, 1f);
            for (int i = 0; i < count; i++)
            {
                int idx = SpawnVisual(pos + RandomOffset(0.18f), 4, (i & 1) == 0 ? blood : bright, 0.50f);
                if (idx < 0) continue;
                ref VPart p = ref _visuals[idx];
                p.Vel += direction * RandomRange(1.0f, 3.5f);
            }
        }

        public void SpawnStressVisualBurst(int count, float life = 20f)
        {
            Vector2 center = _game.Player != null ? _game.Player.Position : Vector2.zero;
            for (int i = 0; i < count; i++)
            {
                Vector2 position = center + new Vector2(RandomRange(-9f, 9f), RandomRange(2f, 12f));
                int idx = SpawnVisual(position, _rng.Next(0, 4), Color.white, 0.6f);
                if (idx < 0) continue;
                ref VPart v = ref _visuals[idx];
                v.Life = life;
                v.MaxLife = life;
                v.Vel = new Vector2(RandomRange(-1.2f, 1.2f), RandomRange(-0.2f, 2.4f));
                v.GravityScale = 0.15f;
            }
        }

        public void SpawnFullBodyStress(int count)
        {
            ClearStressBodies();
            Vector2 center = _game.Player != null ? _game.Player.Position : Vector2.zero;
            int columns = Mathf.CeilToInt(Mathf.Sqrt(count));
            for (int i = 0; i < count; i++)
            {
                int col = i % columns;
                int row = i / columns;
                Vector2 pos = center + new Vector2((col - columns * 0.5f) * 0.55f, 4f + row * 0.55f);
                PhysPiece p = CreatePhysPiece("stress200", false);
                _stressBodies.Add(p);
                ActivatePhys(p, pos, (byte)TerrainSystem.Concrete, 0.38f, 22f);
                p.Body.gravityScale = 0.85f;
                p.Body.velocity = new Vector2(RandomRange(-1.8f, 1.8f), RandomRange(-1f, 3f));
                p.Body.angularVelocity = RandomRange(-220f, 220f);
            }
        }

        public void SpawnStressPhysical(int count)
        {
            Vector2 center = _game.Player != null ? _game.Player.Position : Vector2.zero;
            for (int i = 0; i < count; i++)
            {
                Vector2 pos = center + new Vector2(RandomRange(-5f, 5f), RandomRange(3f, 8f));
                PhysPiece p = CreatePhysPiece("stressPhysical", false);
                _stressBodies.Add(p);
                ActivatePhys(p, pos, (byte)TerrainSystem.Concrete, 0.34f, 12f);
            }
        }

        public void ClearStressBodies()
        {
            for (int i = 0; i < _stressBodies.Count; i++)
            {
                if (_stressBodies[i].Go != null)
                    Object.Destroy(_stressBodies[i].Go);
            }
            _stressBodies.Clear();
        }

        public void ClearStressVisuals()
        {
            for (int i = 0; i < _visualCount; i++)
                _visuals[i].Alive = false;
            _visualCount = 0;
            if (_meshRenderer != null) _meshRenderer.enabled = false;
        }

        private void TickVisuals(float dt)
        {
            if (_visuals == null) return;
            for (int i = 0; i < _visualCount; i++)
            {
                ref VPart p = ref _visuals[i];
                if (!p.Alive) continue;
                p.Life -= dt;
                if (p.Life <= 0f)
                {
                    p.Alive = false;
                    continue;
                }

                p.Vel.y -= _game.Config.t2Gravity * p.GravityScale * dt;
                p.Pos += p.Vel * dt;
                p.Angle += p.AngleVel * dt;

                if (_game.Terrain.SolidAtWorld(p.Pos.x, p.Pos.y + 0.04f))
                {
                    int cx = Mathf.FloorToInt(p.Pos.x);
                    int cy = Mathf.FloorToInt(p.Pos.y);
                    p.Pos.y = cy + 1.02f;
                    p.Vel.y = Mathf.Abs(p.Vel.y) * 0.28f;
                    p.Vel.x *= 0.72f;
                    p.AngleVel *= 0.6f;
                }
            }

            // stable compaction (no accidental leftover inactive slot)
            int write = 0;
            for (int i = 0; i < _visualCount; i++)
            {
                if (_visuals[i].Alive)
                    _visuals[write++] = _visuals[i];
            }
            _visualCount = write;
        }

        private void TickPhysical(float dt)
        {
            for (int i = _normalPool.Count - 1; i >= 0; i--)
            {
                PhysPiece p = _normalPool[i];
                if (!p.Active) continue;
                p.Life -= dt;
                if (p.Life <= 0f)
                {
                    DeactivatePhys(p);
                    continue;
                }
                if (p.Life < 0.35f)
                {
                    Color c = p.Renderer.color;
                    c.a = Mathf.Clamp01(p.Life / 0.35f);
                    p.Renderer.color = c;
                }
            }

            for (int i = _stressBodies.Count - 1; i >= 0; i--)
            {
                PhysPiece p = _stressBodies[i];
                p.Life -= dt;
                if (!p.Active || p.Life <= 0f || p.Go == null)
                {
                    if (p.Go != null) Object.Destroy(p.Go);
                    _stressBodies.RemoveAt(i);
                }
            }
        }

        private int SpawnVisual(Vector2 pos, int tile, Color tint, float lifeScale)
        {
            if (_visualCount >= _visuals.Length)
                return -1;
            float life = RandomRange(_game.Config.t2LifetimeMin, _game.Config.t2LifetimeMax) * lifeScale;
            VPart p = new VPart
            {
                Pos = pos,
                Vel = new Vector2(RandomRange(-3.8f, 3.8f), RandomRange(1.2f, 7.5f)),
                Angle = RandomRange(0f, 360f),
                AngleVel = RandomRange(-360f, 360f),
                Size = RandomRange(_game.Config.t2SizeMin, _game.Config.t2SizeMax),
                Life = life,
                MaxLife = life,
                GravityScale = 1f,
                Tile = tile,
                Color = tint,
                Alive = true
            };
            _visuals[_visualCount] = p;
            return _visualCount++;
        }

        private bool TrySpawnPhysical(byte type, Vector2 pos, bool force)
        {
            int cap = _game.Config.fullBodyMax;
            int active = 0;
            for (int i = 0; i < _normalPool.Count; i++)
                if (_normalPool[i].Active) active++;
            if (!force && active >= cap)
                return false;

            PhysPiece piece = null;
            for (int i = 0; i < _normalPool.Count; i++)
            {
                if (!_normalPool[i].Active) { piece = _normalPool[i]; break; }
            }
            if (piece == null)
                return false;

            ActivatePhys(piece, pos + RandomOffset(0.20f), type, RandomRange(0.28f, 0.42f), _game.Config.t1Lifetime);
            return true;
        }

        private PhysPiece CreatePhysPiece(string name, bool pooled)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(_parent, false);
            go.layer = TerrainSystem.DebrisLayer;
            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            sr.sortingOrder = 18;
            Rigidbody2D rb = go.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Dynamic;
            rb.gravityScale = 1.25f;
            rb.mass = 0.2f;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Discrete;
            BoxCollider2D col = go.AddComponent<BoxCollider2D>();
            col.size = Vector2.one * 0.8f;
            col.sharedMaterial = _bounceMat;
            go.SetActive(false);
            return new PhysPiece { Go = go, Renderer = sr, Body = rb, Collider = col, Active = false };
        }

        private void ActivatePhys(PhysPiece p, Vector2 pos, byte type, float scale, float life)
        {
            p.Go.SetActive(true);
            p.Go.transform.position = new Vector3(pos.x, pos.y, -0.1f);
            p.Go.transform.localScale = Vector3.one * scale;
            int tile = (type >= 1 && type < _game.Art.TileUvIndex.Length) ? _game.Art.TileUvIndex[type] : 4;
            p.Renderer.sprite = _game.Art.TileSprites[tile];
            p.Renderer.color = Color.white;
            p.Collider.size = Vector2.one * 0.8f;
            p.Body.simulated = true;
            p.Body.velocity = new Vector2(RandomRange(-3.5f, 3.5f), RandomRange(2f, 7.5f));
            p.Body.angularVelocity = RandomRange(-360f, 360f);
            p.Life = life;
            p.MaxLife = life;
            p.Active = true;
        }

        private void DeactivatePhys(PhysPiece p)
        {
            p.Active = false;
            if (p.Go != null)
                p.Go.SetActive(false);
        }

        private void RebuildVisualMesh()
        {
            if (_mesh == null) return;
            if (_visualCount == 0)
            {
                _meshRenderer.enabled = false;
                return;
            }

            _verts.Clear();
            _uvs.Clear();
            _colors.Clear();
            _tris.Clear();
            float tileW = 1f / Art.AtlasTiles;
            float insetU = 0.0012f;
            float insetV = 0.006f;

            for (int i = 0; i < _visualCount; i++)
            {
                VPart p = _visuals[i];
                if (!p.Alive) continue;
                float lifeT = p.MaxLife > 0f ? Mathf.Clamp01(p.Life / p.MaxLife) : 0f;
                float alpha = Mathf.Clamp01(lifeT * 2.5f);
                Color c = p.Color;
                c.a *= alpha * 0.95f;
                float half = p.Size * 0.5f;
                float cos = Mathf.Cos(p.Angle * Mathf.Deg2Rad);
                float sin = Mathf.Sin(p.Angle * Mathf.Deg2Rad);
                Vector3 pz = new Vector3(p.Pos.x, p.Pos.y, -0.18f);
                Vector3 v0 = pz + new Vector3((-half * cos - -half * sin), (-half * sin + -half * cos), 0f);
                Vector3 v1 = pz + new Vector3((half * cos - -half * sin), (half * sin + -half * cos), 0f);
                Vector3 v2 = pz + new Vector3((half * cos - half * sin), (half * sin + half * cos), 0f);
                Vector3 v3 = pz + new Vector3((-half * cos - half * sin), (-half * sin + half * cos), 0f);
                int baseIdx = _verts.Count;
                _verts.Add(v0); _verts.Add(v1); _verts.Add(v2); _verts.Add(v3);
                float u0 = p.Tile * tileW + insetU;
                float u1 = (p.Tile + 1) * tileW - insetU;
                _uvs.Add(new Vector2(u0, insetV));
                _uvs.Add(new Vector2(u1, insetV));
                _uvs.Add(new Vector2(u1, 1f - insetV));
                _uvs.Add(new Vector2(u0, 1f - insetV));
                _colors.Add(c); _colors.Add(c); _colors.Add(c); _colors.Add(c);
                _tris.Add(baseIdx); _tris.Add(baseIdx + 1); _tris.Add(baseIdx + 2);
                _tris.Add(baseIdx); _tris.Add(baseIdx + 2); _tris.Add(baseIdx + 3);
            }

            _mesh.Clear();
            if (_verts.Count == 0)
            {
                _meshRenderer.enabled = false;
                return;
            }
            _mesh.SetVertices(_verts);
            _mesh.SetUVs(0, _uvs);
            _mesh.SetColors(_colors);
            _mesh.SetTriangles(_tris, 0);
            _mesh.RecalculateBounds();
            _meshRenderer.enabled = true;
        }

        private int CountActive(List<PhysPiece> list)
        {
            int c = 0;
            for (int i = 0; i < list.Count; i++)
                if (list[i].Active) c++;
            return c;
        }

        private Vector2 RandomOffset(float radius)
        {
            return new Vector2(RandomRange(-radius, radius), RandomRange(-radius, radius));
        }

        private float RandomRange(float a, float b)
        {
            return a + (float)_rng.NextDouble() * (b - a);
        }

        private static Color DirtDustColor()
        {
            return new Color(0.62f, 0.55f, 0.44f, 0.53f);
        }
    }
}
