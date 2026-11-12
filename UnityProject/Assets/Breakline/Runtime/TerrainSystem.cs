using System.Collections.Generic;
using UnityEngine;

namespace Breakline
{
    public struct TerrainRayHit
    {
        public bool Hit;
        public Vector2 Point;
        public float Distance;
        public int X;
        public int Y;
        public byte Type;
    }

    /// <summary>
    /// Authoritative tile grid plus chunked meshes and greedy box colliders.
    /// The grid, not the physics scene, is the source of truth for bullets, explosions and support.
    /// </summary>
    public sealed class TerrainSystem
    {
        public const byte Empty = 0;
        public const byte Dirt = 1;
        public const byte Concrete = 2;
        public const byte Support = 3;
        public const byte Indestructible = 4;
        public const int TerrainLayer = 8;
        public const int DebrisLayer = 9;

        private sealed class PendingCollapse
        {
            public int[] Cells;
            public int Count;
            public float Timer;
            public Vector2 Center;
        }

        private sealed class FallingChunk
        {
            public GameObject Root;
            public Rigidbody2D Body;
            public Vector2 Size;
            public Rect WorldBounds;
            public float Age;
            public float Lifetime;
            public bool Crushed;
            public int CellCount;
        }

        private readonly GameRoot _game;
        private readonly int _chunkSize;
        private int _chunkCountX;
        private int _chunkCountY;
        private int _chunks;
        private byte[] _cells;
        private float[] _hp;
        private bool[] _protected;
        private bool[] _anchored;
        private bool[] _dirty;

        private Transform _parent;
        private GameObject[] _chunkRoots;
        private MeshRenderer[] _chunkRenderers;
        private MeshFilter[] _chunkFilters;
        private Mesh[] _chunkMeshes;
        private List<GameObject>[] _colliderChildren;

        private int[] _queue;
        private bool[] _visited;
        private int[] _component;

        private bool _supportDirty;
        private float _nextSupportCheck;
        private PendingCollapse _pending;
        private readonly List<FallingChunk> _falling = new List<FallingChunk>(16);

        public int Width { get; private set; }
        public int Height { get; private set; }
        public int ActiveFallingChunks => _falling.Count;

        public TerrainSystem(GameRoot game)
        {
            _game = game;
            _chunkSize = Mathf.Clamp(game.Config.chunkSize, 8, 32);
        }

        public void Init(int width, int height)
        {
            Width = width;
            Height = height;
            _cells = new byte[width * height];
            _hp = new float[width * height];
            _protected = new bool[width * height];
            _anchored = new bool[width * height];
            _chunkCountX = Mathf.CeilToInt(width / (float)_chunkSize);
            _chunkCountY = Mathf.CeilToInt(height / (float)_chunkSize);
            _chunks = _chunkCountX * _chunkCountY;
            _dirty = new bool[_chunks];
            _chunkRoots = new GameObject[_chunks];
            _chunkRenderers = new MeshRenderer[_chunks];
            _chunkFilters = new MeshFilter[_chunks];
            _chunkMeshes = new Mesh[_chunks];
            _colliderChildren = new List<GameObject>[_chunks];
            _queue = new int[width * height];
            _visited = new bool[width * height];
            _component = new int[width * height];

            _parent = new GameObject("Terrain").transform;
            _parent.SetParent(_game.transform, false);
            for (int cy = 0; cy < _chunkCountY; cy++)
            {
                for (int cx = 0; cx < _chunkCountX; cx++)
                {
                    int idx = cy * _chunkCountX + cx;
                    GameObject root = new GameObject($"TerrainChunk_{cx}_{cy}");
                    root.transform.SetParent(_parent, false);
                    root.transform.position = new Vector3(cx * _chunkSize, cy * _chunkSize, 0f);
                    root.layer = TerrainLayer;
                    Rigidbody2D rb = root.AddComponent<Rigidbody2D>();
                    rb.bodyType = RigidbodyType2D.Static;

                    Mesh mesh = new Mesh { name = $"TerrainMesh_{cx}_{cy}" };
                    mesh.MarkDynamic();
                    MeshFilter mf = root.AddComponent<MeshFilter>();
                    mf.sharedMesh = mesh;
                    MeshRenderer mr = root.AddComponent<MeshRenderer>();
                    mr.sharedMaterial = _game.Art.TerrainMaterial;
                    mr.sortingOrder = 0;
                    mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    mr.receiveShadows = false;

                    _chunkRoots[idx] = root;
                    _chunkRenderers[idx] = mr;
                    _chunkFilters[idx] = mf;
                    _chunkMeshes[idx] = mesh;
                    _colliderChildren[idx] = new List<GameObject>(4);
                    _dirty[idx] = true;
                }
            }
        }

        public void SetCell(int x, int y, byte type, bool protect = false)
        {
            if (x < 0 || y < 0 || x >= Width || y >= Height) return;
            int i = y * Width + x;
            _cells[i] = type;
            _hp[i] = HpFor(type);
            _protected[i] = protect && type != Empty;
            MarkDirty(x, y);
        }

        public void Fill(int x0, int y0, int w, int h, byte type, bool protect = false)
        {
            for (int y = y0; y < y0 + h; y++)
            {
                for (int x = x0; x < x0 + w; x++)
                    SetCell(x, y, type, protect);
            }
        }

        public void ClearRect(int x0, int y0, int w, int h)
        {
            for (int y = y0; y < y0 + h; y++)
            {
                for (int x = x0; x < x0 + w; x++)
                {
                    if (x < 0 || y < 0 || x >= Width || y >= Height) continue;
                    SetCell(x, y, Empty);
                }
            }
        }

        public void ProtectRect(int x0, int y0, int w, int h)
        {
            for (int y = y0; y < y0 + h; y++)
            {
                for (int x = x0; x < x0 + w; x++)
                {
                    if (x < 0 || y < 0 || x >= Width || y >= Height) continue;
                    if (_cells[y * Width + x] != Empty)
                        _protected[y * Width + x] = true;
                }
            }
        }

        public void AnchorRect(int x0, int y0, int w, int h)
        {
            for (int y = y0; y < y0 + h; y++)
            {
                for (int x = x0; x < x0 + w; x++)
                {
                    if (x < 0 || y < 0 || x >= Width || y >= Height) continue;
                    if (_cells[y * Width + x] != Empty)
                        _anchored[y * Width + x] = true;
                }
            }
        }

        public void RebuildAllImmediate()
        {
            for (int i = 0; i < _chunks; i++)
            {
                RebuildChunk(i);
                _dirty[i] = false;
            }
            _supportDirty = true;
        }

        public void MarkSupportDirty()
        {
            _supportDirty = true;
        }

        public bool IsSolid(int x, int y)
        {
            return x >= 0 && y >= 0 && x < Width && y < Height && _cells[y * Width + x] != Empty;
        }

        public bool SolidAtWorld(float worldX, float worldY)
        {
            return IsSolid(Mathf.FloorToInt(worldX), Mathf.FloorToInt(worldY));
        }

        public byte CellType(int x, int y)
        {
            if (x < 0 || y < 0 || x >= Width || y >= Height) return Empty;
            return _cells[y * Width + x];
        }

        public bool IsProtected(int x, int y)
        {
            if (x < 0 || y < 0 || x >= Width || y >= Height) return false;
            return _protected[y * Width + x];
        }

        public bool RectOverlapsSolid(Rect rect)
        {
            int minX = Mathf.FloorToInt(rect.xMin + 0.0001f);
            int maxX = Mathf.FloorToInt(rect.xMax - 0.0001f);
            int minY = Mathf.FloorToInt(rect.yMin + 0.0001f);
            int maxY = Mathf.FloorToInt(rect.yMax - 0.0001f);
            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    if (IsSolid(x, y))
                        return true;
                }
            }
            return false;
        }

        public float TopSolidY(int x)
        {
            for (int y = Height - 1; y >= 0; y--)
            {
                if (IsSolid(x, y))
                    return y + 1f;
            }
            return 0f;
        }

        public bool Raycast(Vector2 origin, Vector2 direction, float maxDistance, out TerrainRayHit hit)
        {
            hit = default;
            if (direction.sqrMagnitude < 1e-8f || maxDistance <= 0f)
                return false;
            direction.Normalize();

            int x = Mathf.FloorToInt(origin.x);
            int y = Mathf.FloorToInt(origin.y);
            int stepX = direction.x > 0f ? 1 : (direction.x < 0f ? -1 : 0);
            int stepY = direction.y > 0f ? 1 : (direction.y < 0f ? -1 : 0);

            float tDeltaX = stepX == 0 ? float.PositiveInfinity : Mathf.Abs(1f / direction.x);
            float tDeltaY = stepY == 0 ? float.PositiveInfinity : Mathf.Abs(1f / direction.y);
            float tMaxX;
            float tMaxY;
            if (stepX > 0) tMaxX = ((x + 1) - origin.x) / direction.x;
            else if (stepX < 0) tMaxX = (x - origin.x) / direction.x;
            else tMaxX = float.PositiveInfinity;
            if (stepY > 0) tMaxY = ((y + 1) - origin.y) / direction.y;
            else if (stepY < 0) tMaxY = (y - origin.y) / direction.y;
            else tMaxY = float.PositiveInfinity;

            float t = 0f;
            int guard = Width + Height + 8;
            while (t <= maxDistance && guard-- > 0)
            {
                if (x >= 0 && x < Width && y >= 0 && y < Height && IsSolid(x, y))
                {
                    hit.Hit = true;
                    hit.Point = origin + direction * t;
                    hit.Distance = t;
                    hit.X = x;
                    hit.Y = y;
                    hit.Type = _cells[y * Width + x];
                    return true;
                }

                if (tMaxX < tMaxY)
                {
                    x += stepX;
                    t = tMaxX;
                    tMaxX += tDeltaX;
                }
                else
                {
                    y += stepY;
                    t = tMaxY;
                    tMaxY += tDeltaY;
                }

                if ((x < -2 || x > Width + 2 || y < -2 || y > Height + 2) && t > 1f)
                    break;
            }

            return false;
        }

        public bool HasLineOfSight(Vector2 a, Vector2 b)
        {
            Vector2 d = b - a;
            float len = d.magnitude;
            if (len < 0.001f) return true;
            bool blocked = Raycast(a, d / len, len - 0.05f, out _);
            return !blocked;
        }

        public void DamageCell(int x, int y, float damage, bool spawnDebris = true)
        {
            if (x < 0 || y < 0 || x >= Width || y >= Height) return;
            int i = y * Width + x;
            byte type = _cells[i];
            if (type == Empty || type == Indestructible || _protected[i])
                return;

            _hp[i] -= damage;
            if (_hp[i] > 0f)
                return;

            _cells[i] = Empty;
            _hp[i] = 0f;
            _protected[i] = false;
            MarkDirty(x, y);
            if (spawnDebris)
                _game.Debris.SpawnFromCell(type, new Vector2(x + 0.5f, y + 0.5f));
            _supportDirty = true;
        }

        /// <summary>
        /// Carves a circular hole and applies falloff damage. Terrain reliability is deliberately
        /// authoritative here; no collider query is involved.
        /// </summary>
        public void Explode(Vector2 center, float radius, float terrainRadius, float maxDamage)
        {
            if (radius <= 0f) return;
            int minX = Mathf.FloorToInt(center.x - radius);
            int maxX = Mathf.CeilToInt(center.x + radius);
            int minY = Mathf.FloorToInt(center.y - radius);
            int maxY = Mathf.CeilToInt(center.y + radius);
            bool removedAny = false;

            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    if (x < 0 || y < 0 || x >= Width || y >= Height) continue;
                    int i = y * Width + x;
                    byte type = _cells[i];
                    if (type == Empty) continue;
                    if (type == Indestructible || _protected[i]) continue;

                    float dx = x + 0.5f - center.x;
                    float dy = y + 0.5f - center.y;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    if (dist > terrainRadius) continue;

                    float falloff = 1f - Mathf.Clamp01(dist / Mathf.Max(0.01f, terrainRadius));
                    float dmg = maxDamage * (0.30f + 0.70f * falloff);
                    // The inner blast zone carves even tough material if it is not protected.
                    if (dist < terrainRadius * 0.55f)
                        dmg = Mathf.Max(dmg, maxDamage);
                    _hp[i] -= dmg;
                    if (_hp[i] <= 0f)
                    {
                        _cells[i] = Empty;
                        _hp[i] = 0f;
                        _protected[i] = false;
                        MarkDirty(x, y);
                        removedAny = true;
                        _game.Debris.SpawnFromCell(type, new Vector2(x + 0.5f, y + 0.5f));
                    }
                }
            }

            if (removedAny)
            {
                _supportDirty = true;
                _game.Debris.SpawnDust(center, Mathf.RoundToInt(12f + radius * 5f));
            }

            _game.Vfx.DustPuff(center, radius * 0.8f, new Color(0.55f, 0.48f, 0.38f, 0.45f));
        }

        public void OpenGate(IEnumerable<Vector2Int> cells)
        {
            if (cells == null) return;
            foreach (Vector2Int c in cells)
                SetCell(c.x, c.y, Empty);
            _supportDirty = true;
            FlushDirtyImmediate();
        }

        public void Tick(float dt)
        {
            for (int i = 0; i < _chunks; i++)
            {
                if (_dirty[i])
                {
                    RebuildChunk(i);
                    _dirty[i] = false;
                }
            }

            if (_supportDirty && Time.time >= _nextSupportCheck && _pending == null)
            {
                _nextSupportCheck = Time.time + _game.Config.supportCheckInterval;
                FindUnsupportedIsland();
            }

            if (_pending != null)
            {
                _pending.Timer -= dt;
                if (_pending.Timer <= 0f)
                    DetachPending();
            }

            for (int i = _falling.Count - 1; i >= 0; i--)
            {
                FallingChunk chunk = _falling[i];
                chunk.Age += dt;
                if (chunk.Root != null)
                {
                    Vector3 rp = chunk.Root.transform.position;
                    chunk.WorldBounds = new Rect(rp.x, rp.y, chunk.Size.x, chunk.Size.y);
                }
                if (!chunk.Crushed && chunk.Body != null && chunk.Body.velocity.y < -1f)
                {
                    if (_game.Enemies.CrushInRect(chunk.WorldBounds, -chunk.Body.velocity.y))
                    {
                        chunk.Crushed = true;
                        // Let the player read the flattened-buried target, then clear the road.
                        chunk.Lifetime = Mathf.Min(chunk.Lifetime, chunk.Age + 1.1f);
                        _game.Debris.SpawnDust(chunk.WorldBounds.center, 20);
                    }
                }
                if (chunk.Age > chunk.Lifetime)
                {
                    if (chunk.Root != null)
                    {
                        _game.Debris.SpawnDust(chunk.WorldBounds.center, 16);
                        Object.Destroy(chunk.Root);
                    }
                    _falling.RemoveAt(i);
                }
            }
        }

        private void MarkDirty(int x, int y)
        {
            int cx = x / _chunkSize;
            int cy = y / _chunkSize;
            if (cx < 0 || cy < 0 || cx >= _chunkCountX || cy >= _chunkCountY) return;
            _dirty[cy * _chunkCountX + cx] = true;
        }

        private void RebuildChunk(int index)
        {
            int cx = index % _chunkCountX;
            int cy = index / _chunkCountX;
            BuildMesh(index, cx, cy);
            BuildColliders(index, cx, cy);
        }

        private void BuildMesh(int index, int cx, int cy)
        {
            int startX = cx * _chunkSize;
            int startY = cy * _chunkSize;
            int endX = Mathf.Min(Width, startX + _chunkSize);
            int endY = Mathf.Min(Height, startY + _chunkSize);
            List<Vector3> verts = new List<Vector3>(_chunkSize * _chunkSize * 4);
            List<Vector2> uvs = new List<Vector2>(_chunkSize * _chunkSize * 4);
            List<int> tris = new List<int>(_chunkSize * _chunkSize * 6);
            int count = 0;
            float insetU = 0.0012f;
            float insetV = 0.006f;

            for (int y = startY; y < endY; y++)
            {
                for (int x = startX; x < endX; x++)
                {
                    byte type = _cells[y * Width + x];
                    if (type == Empty) continue;
                    int tile = _game.Art.TileUvIndex[type];
                    float u0 = tile / (float)Art.AtlasTiles + insetU;
                    float u1 = (tile + 1) / (float)Art.AtlasTiles - insetU;
                    float v0 = insetV;
                    float v1 = 1f - insetV;

                    float lx = x - startX;
                    float ly = y - startY;
                    int vi = count * 4;
                    verts.Add(new Vector3(lx, ly, 0f));
                    verts.Add(new Vector3(lx + 1f, ly, 0f));
                    verts.Add(new Vector3(lx + 1f, ly + 1f, 0f));
                    verts.Add(new Vector3(lx, ly + 1f, 0f));
                    uvs.Add(new Vector2(u0, v0));
                    uvs.Add(new Vector2(u1, v0));
                    uvs.Add(new Vector2(u1, v1));
                    uvs.Add(new Vector2(u0, v1));
                    tris.Add(vi);
                    tris.Add(vi + 1);
                    tris.Add(vi + 2);
                    tris.Add(vi);
                    tris.Add(vi + 2);
                    tris.Add(vi + 3);
                    count++;
                }
            }

            Mesh mesh = _chunkMeshes[index];
            mesh.Clear();
            if (count > 0)
            {
                mesh.SetVertices(verts);
                mesh.SetUVs(0, uvs);
                mesh.SetTriangles(tris, 0);
                mesh.RecalculateBounds();
            }
            _chunkRenderers[index].enabled = count > 0;
        }

        private void BuildColliders(int index, int cx, int cy)
        {
            List<GameObject> old = _colliderChildren[index];
            for (int i = 0; i < old.Count; i++)
            {
                if (old[i] != null)
                {
                    old[i].SetActive(false);
                    Object.Destroy(old[i]);
                }
            }
            old.Clear();

            int startX = cx * _chunkSize;
            int startY = cy * _chunkSize;
            int endX = Mathf.Min(Width, startX + _chunkSize);
            int endY = Mathf.Min(Height, startY + _chunkSize);

            for (int y = startY; y < endY; y++)
            {
                int x = startX;
                while (x < endX)
                {
                    while (x < endX && _cells[y * Width + x] == Empty) x++;
                    if (x >= endX) break;
                    int runStart = x;
                    while (x < endX && _cells[y * Width + x] != Empty) x++;
                    int runLen = x - runStart;
                    if (runLen <= 0) continue;

                    GameObject colGo = new GameObject("col");
                    colGo.layer = TerrainLayer;
                    colGo.transform.SetParent(_chunkRoots[index].transform, false);
                    colGo.transform.localPosition = new Vector3(runStart - startX + runLen * 0.5f, y - startY + 0.5f, 0f);
                    BoxCollider2D col = colGo.AddComponent<BoxCollider2D>();
                    col.size = new Vector2(runLen, 1f);
                    old.Add(colGo);
                }
            }
        }

        private void FindUnsupportedIsland()
        {
            _supportDirty = false;
            int total = Width * Height;
            System.Array.Clear(_visited, 0, total);
            for (int start = 0; start < total; start++)
            {
                if (_visited[start] || _cells[start] == Empty) continue;
                int qh = 0;
                int qt = 0;
                _queue[qt++] = start;
                _visited[start] = true;
                int componentCount = 0;
                bool anchored = false;
                int minSum = 0;
                int centerCount = 0;

                while (qh < qt)
                {
                    int idx = _queue[qh++];
                    _component[componentCount++] = idx;
                    int x = idx % Width;
                    int y = idx / Width;
                    minSum += idx;
                    centerCount++;
                    if (y == 0 || _cells[idx] == Indestructible || _anchored[idx])
                        anchored = true;

                    TryPush(x - 1, y, ref qt);
                    TryPush(x + 1, y, ref qt);
                    TryPush(x, y - 1, ref qt);
                    TryPush(x, y + 1, ref qt);
                }

                if (!anchored && componentCount > 0)
                {
                    int[] cells = new int[componentCount];
                    System.Array.Copy(_component, cells, componentCount);
                    int avgIdx = minSum / centerCount;
                    Vector2 center = new Vector2((avgIdx % Width) + 0.5f, (avgIdx / Width) + 0.5f);
                    ScheduleCollapse(cells, center);
                    return; // one island per support solve; additional islands are caught next pass
                }
            }
        }

        private void TryPush(int x, int y, ref int qt)
        {
            if (x < 0 || y < 0 || x >= Width || y >= Height) return;
            int idx = y * Width + x;
            if (_visited[idx] || _cells[idx] == Empty) return;
            _visited[idx] = true;
            _queue[qt++] = idx;
        }

        private void ScheduleCollapse(int[] cells, Vector2 center)
        {
            if (_falling.Count >= _game.Config.collapseMaxChunks)
            {
                for (int i = 0; i < cells.Length; i++)
                {
                    int idx = cells[i];
                    int x = idx % Width;
                    int y = idx / Width;
                    _cells[idx] = Empty;
                    _hp[idx] = 0f;
                    _protected[idx] = false;
                    MarkDirty(x, y);
                    _game.Debris.SpawnFromCell(Concrete, new Vector2(x + 0.5f, y + 0.5f));
                }
                _game.Debris.SpawnDust(center, 24);
                return;
            }

            _pending = new PendingCollapse
            {
                Cells = cells,
                Count = cells.Length,
                Timer = _game.Config.supportDelay,
                Center = center
            };
            _game.Audio.Play(AudioCue.Land, 0.45f);
            _game.Vfx.SpawnRing(center, 1.8f, new Color(0.95f, 0.78f, 0.35f, 0.55f), 0.28f, true);
            _game.Hud.ShowWorldWarning(center, "!");
        }

        private void DetachPending()
        {
            PendingCollapse p = _pending;
            _pending = null;
            if (p.Count <= 0) return;

            // Remove authoritative cells first and refresh collision immediately so the
            // debris cannot collide with the old, already-destroyed floor.
            Vector2 center = p.Center;
            int minX = Width;
            int minY = Height;
            int maxX = 0;
            int maxY = 0;
            List<int> validCells = new List<int>(p.Count);
            List<byte> validTypes = new List<byte>(p.Count);
            for (int i = 0; i < p.Count; i++)
            {
                int idx = p.Cells[i];
                byte type = _cells[idx];
                if (type == Empty) continue;
                int x = idx % Width;
                int y = idx / Width;

                // Column/support stubs crumble into visual debris instead of forming a tall
                // wall on the walkway. The protected concrete slab is what falls and crushes.
                if (type == Support)
                {
                    _cells[idx] = Empty;
                    _hp[idx] = 0f;
                    _protected[idx] = false;
                    MarkDirty(x, y);
                    _game.Debris.SpawnFromCell(type, new Vector2(x + 0.5f, y + 0.5f));
                    continue;
                }

                minX = Mathf.Min(minX, x);
                minY = Mathf.Min(minY, y);
                maxX = Mathf.Max(maxX, x);
                maxY = Mathf.Max(maxY, y);
                validCells.Add(idx);
                validTypes.Add(type);
                _cells[idx] = Empty;
                _hp[idx] = 0f;
                _protected[idx] = false;
                MarkDirty(x, y);
            }

            if (validCells.Count == 0)
            {
                _supportDirty = true;
                return;
            }

            FlushDirtyImmediate();
            BuildFallingChunk(validCells.ToArray(), validTypes.ToArray(),
                new Rect(minX, minY, maxX - minX + 1, maxY - minY + 1));
            _supportDirty = true;
            _game.Audio.Play(AudioCue.Land, 0.7f);
            _game.Vfx.SpawnRing(center, 3.0f, new Color(0.85f, 0.70f, 0.42f, 0.65f), 0.35f, true);
        }

        private void FlushDirtyImmediate()
        {
            for (int i = 0; i < _chunks; i++)
            {
                if (_dirty[i])
                {
                    RebuildChunk(i);
                    _dirty[i] = false;
                }
            }
        }

        private void BuildFallingChunk(int[] cellIndices, byte[] cellTypes, Rect bounds)
        {
            List<Vector3> verts = new List<Vector3>(cellIndices.Length * 4);
            List<Vector2> uvs = new List<Vector2>(cellIndices.Length * 4);
            List<int> tris = new List<int>(cellIndices.Length * 6);
            int quad = 0;
            float insetU = 0.0012f;
            float insetV = 0.006f;

            for (int i = 0; i < cellIndices.Length; i++)
            {
                int idx = cellIndices[i];
                int x = idx % Width;
                int y = idx / Width;
                byte type = cellTypes[i];
                int tile = _game.Art.TileUvIndex[type];
                float u0 = tile / (float)Art.AtlasTiles + insetU;
                float u1 = (tile + 1) / (float)Art.AtlasTiles - insetU;
                float v0 = insetV;
                float v1 = 1f - insetV;
                float lx = x - bounds.xMin;
                float ly = y - bounds.yMin;
                int vi = quad * 4;
                verts.Add(new Vector3(lx, ly, 0f));
                verts.Add(new Vector3(lx + 1f, ly, 0f));
                verts.Add(new Vector3(lx + 1f, ly + 1f, 0f));
                verts.Add(new Vector3(lx, ly + 1f, 0f));
                uvs.Add(new Vector2(u0, v0));
                uvs.Add(new Vector2(u1, v0));
                uvs.Add(new Vector2(u1, v1));
                uvs.Add(new Vector2(u0, v1));
                tris.Add(vi);
                tris.Add(vi + 1);
                tris.Add(vi + 2);
                tris.Add(vi);
                tris.Add(vi + 2);
                tris.Add(vi + 3);
                quad++;
            }

            GameObject root = new GameObject("FallingPlatform");
            root.layer = DebrisLayer;
            root.transform.SetParent(_parent, true);
            root.transform.position = new Vector3(bounds.xMin, bounds.yMin, 0f);
            Rigidbody2D rb = root.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Dynamic;
            rb.gravityScale = 3.2f;
            rb.mass = Mathf.Max(1.5f, cellIndices.Length * 0.25f);
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            rb.velocity = new Vector2(0f, -5.5f);
            rb.angularVelocity = 0f;

            Mesh mesh = new Mesh { name = "FallingPlatformMesh" };
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            MeshFilter mf = root.AddComponent<MeshFilter>();
            mf.sharedMesh = mesh;
            MeshRenderer mr = root.AddComponent<MeshRenderer>();
            mr.sharedMaterial = _game.Art.TerrainMaterial;
            mr.sortingOrder = 14;

            // Merge row runs into BoxCollider2D children.
            bool[] set = new bool[Width * Height];
            for (int i = 0; i < cellIndices.Length; i++) set[cellIndices[i]] = true;
            int x0 = Mathf.FloorToInt(bounds.xMin);
            int y0 = Mathf.FloorToInt(bounds.yMin);
            int x1 = Mathf.CeilToInt(bounds.xMax);
            int y1 = Mathf.CeilToInt(bounds.yMax);
            for (int y = y0; y < y1; y++)
            {
                int x = x0;
                while (x < x1)
                {
                    while (x < x1 && !set[y * Width + x]) x++;
                    if (x >= x1) break;
                    int start = x;
                    while (x < x1 && set[y * Width + x]) x++;
                    int len = x - start;
                    GameObject col = new GameObject("fallingCol");
                    col.layer = DebrisLayer;
                    col.transform.SetParent(root.transform, false);
                    col.transform.localPosition = new Vector3(start - bounds.xMin + len * 0.5f, y - bounds.yMin + 0.5f, 0f);
                    BoxCollider2D bc = col.AddComponent<BoxCollider2D>();
                    bc.size = new Vector2(len, 1f);
                }
            }

            _falling.Add(new FallingChunk
            {
                Root = root,
                Body = rb,
                Size = new Vector2(bounds.width, bounds.height),
                WorldBounds = new Rect(bounds.xMin, bounds.yMin, bounds.width, bounds.height),
                Age = 0f,
                Lifetime = 6f,
                CellCount = cellIndices.Length
            });
        }

        private float HpFor(byte type)
        {
            switch (type)
            {
                case Dirt: return _game.Config.dirtHP;
                case Concrete: return _game.Config.concreteHP;
                case Support: return _game.Config.supportHP;
                case Indestructible: return float.PositiveInfinity;
                default: return 0f;
            }
        }

        public int VisibleChunkCount()
        {
            int c = 0;
            for (int i = 0; i < _chunks; i++)
                if (_chunkRenderers[i].enabled) c++;
            return c;
        }
    }
}
