using System.Collections.Generic;
using UnityEngine;

namespace Breakline
{
    public sealed class ProjectileSystem
    {
        private sealed class Tracer
        {
            public GameObject Go;
            public LineRenderer Line;
            public float Life;
            public bool Active;
        }

        private sealed class Grenade
        {
            public GameObject Go;
            public SpriteRenderer Renderer;
            public Vector2 Pos;
            public Vector2 Vel;
            public float Fuse;
            public bool Contacted;
            public bool Active;
        }

        private readonly GameRoot _game;
        private readonly List<Tracer> _tracers = new List<Tracer>(24);
        private readonly List<Grenade> _grenades = new List<Grenade>(6);
        private Transform _parent;

        public ProjectileSystem(GameRoot game)
        {
            _game = game;
        }

        public void Init(Transform parent)
        {
            _parent = new GameObject("Projectiles").transform;
            _parent.SetParent(parent, false);
            for (int i = 0; i < 24; i++)
                _tracers.Add(CreateTracer());
            for (int i = 0; i < 6; i++)
                _grenades.Add(CreateGrenade());
        }

        public void Tick(float dt)
        {
            for (int i = 0; i < _tracers.Count; i++)
            {
                Tracer t = _tracers[i];
                if (!t.Active) continue;
                t.Life -= dt;
                if (t.Life <= 0f)
                {
                    t.Active = false;
                    t.Line.enabled = false;
                }
            }

            for (int i = 0; i < _grenades.Count; i++)
            {
                Grenade g = _grenades[i];
                if (!g.Active) continue;
                g.Fuse -= dt;
                if (!g.Contacted)
                {
                    g.Vel.y -= _game.Config.grenadeGravity * dt;
                    g.Pos += g.Vel * dt;
                    if (_game.Terrain.SolidAtWorld(g.Pos.x, g.Pos.y))
                    {
                        // Snap just above the solid cell and start the short contact fuse.
                        g.Pos.y = Mathf.FloorToInt(g.Pos.y) + 1.02f;
                        g.Pos.x = Mathf.Clamp(g.Pos.x, 0.05f, _game.Terrain.Width - 0.05f);
                        g.Vel = Vector2.zero;
                        g.Contacted = true;
                        g.Fuse = Mathf.Min(g.Fuse, _game.Config.grenadeContactFuse);
                    }
                }
                else
                {
                    g.Fuse = Mathf.Min(g.Fuse, _game.Config.grenadeContactFuse);
                }

                if (g.Renderer != null)
                {
                    bool flash = Mathf.FloorToInt(Time.unscaledTime * 22f) % 2 == 0;
                    g.Renderer.color = flash ? Color.white : new Color(1f, 0.65f, 0.35f);
                    g.Go.transform.position = new Vector3(g.Pos.x, g.Pos.y, -0.16f);
                }

                if (g.Fuse <= 0f)
                {
                    Vector2 explosionPos = g.Pos;
                    g.Active = false;
                    if (g.Go != null) g.Go.SetActive(false);
                    _game.ExplodeAt(explosionPos,
                        _game.Config.grenadeRadius,
                        _game.Config.grenadeDamage,
                        _game.Config.grenadeTerrainRadius,
                        _game.Config.grenadeTerrainDamage,
                        null);
                }
            }
        }

        public void SpawnTracer(Vector2 from, Vector2 to, Color color)
        {
            Tracer tracer = null;
            for (int i = 0; i < _tracers.Count; i++)
            {
                if (!_tracers[i].Active) { tracer = _tracers[i]; break; }
            }
            if (tracer == null) return;
            tracer.Active = true;
            tracer.Life = 0.055f;
            tracer.Line.enabled = true;
            Color c0 = color;
            Color c1 = color;
            c1.a *= 0.06f;
            tracer.Line.startColor = c0;
            tracer.Line.endColor = c1;
            tracer.Line.SetPosition(0, new Vector3(from.x, from.y, -0.24f));
            tracer.Line.SetPosition(1, new Vector3(to.x, to.y, -0.24f));
        }

        public void ThrowGrenade(Vector2 pos, Vector2 velocity)
        {
            Grenade g = null;
            for (int i = 0; i < _grenades.Count; i++)
            {
                if (!_grenades[i].Active) { g = _grenades[i]; break; }
            }
            if (g == null) return;
            g.Active = true;
            g.Contacted = false;
            g.Pos = pos;
            g.Vel = velocity;
            g.Fuse = _game.Config.grenadeMaxFuse;
            g.Go.SetActive(true);
            g.Go.transform.position = new Vector3(pos.x, pos.y, -0.16f);
            if (g.Renderer != null) g.Renderer.color = Color.white;
        }

        private Tracer CreateTracer()
        {
            GameObject go = new GameObject("Tracer");
            go.transform.SetParent(_parent, false);
            LineRenderer line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.positionCount = 2;
            line.startWidth = 0.045f;
            line.endWidth = 0.02f;
            line.material = _game.Art.LineMaterial;
            line.numCapVertices = 1;
            line.sortingOrder = 22;
            line.enabled = false;
            return new Tracer { Go = go, Line = line };
        }

        private Grenade CreateGrenade()
        {
            GameObject go = new GameObject("Grenade");
            go.transform.SetParent(_parent, false);
            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = _game.Art.GrenadeSprite;
            sr.sortingOrder = 19;
            go.SetActive(false);
            return new Grenade { Go = go, Renderer = sr };
        }
    }
}
