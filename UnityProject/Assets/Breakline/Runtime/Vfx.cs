using System.Collections.Generic;
using UnityEngine;

namespace Breakline
{
    /// <summary>
    /// Pooled, code-driven sprite flashes/rings/slashes. No particle systems are required for M1.
    /// </summary>
    public sealed class Vfx
    {
        private sealed class Fx
        {
            public GameObject Go;
            public SpriteRenderer Renderer;
            public float Elapsed;
            public float Duration;
            public float StartScale;
            public float EndScale;
            public Color Color;
            public float RotationSpeed;
            public bool Active;
            public Vector2 Velocity;
            public bool Fade;
        }

        private readonly GameRoot _game;
        private readonly List<Fx> _pool = new List<Fx>(48);
        private Transform _parent;
        private float _screenFlashRemaining;
        private float _screenFlashDuration;
        private Color _screenFlashColor;

        public Vfx(GameRoot game)
        {
            _game = game;
        }

        public void Init(Transform parent)
        {
            _parent = new GameObject("VFX").transform;
            _parent.SetParent(parent, false);
        }

        public void Tick(float unscaledDt)
        {
            if (_screenFlashRemaining > 0f)
                _screenFlashRemaining = Mathf.Max(0f, _screenFlashRemaining - unscaledDt);

            for (int i = 0; i < _pool.Count; i++)
            {
                Fx fx = _pool[i];
                if (!fx.Active) continue;
                fx.Elapsed += unscaledDt;
                float t = Mathf.Clamp01(fx.Elapsed / Mathf.Max(0.001f, fx.Duration));
                float eased = 1f - (1f - t) * (1f - t);
                float s = Mathf.Lerp(fx.StartScale, fx.EndScale, eased);
                fx.Go.transform.localScale = new Vector3(s, s, 1f);
                Vector3 p = fx.Go.transform.position;
                p += (Vector3)(fx.Velocity * unscaledDt);
                fx.Go.transform.position = p;
                if (fx.RotationSpeed != 0f)
                    fx.Go.transform.Rotate(0f, 0f, fx.RotationSpeed * unscaledDt);
                if (fx.Fade)
                {
                    Color c = fx.Color;
                    c.a *= 1f - t;
                    fx.Renderer.color = c;
                }
                if (t >= 1f)
                {
                    fx.Active = false;
                    fx.Go.SetActive(false);
                }
            }
        }

        public void WorldFlash(Vector2 pos, float diameter, Color color, float duration = 0.12f)
        {
            Fx fx = Take(ArtFlash(), 30);
            if (fx == null) return;
            fx.Go.transform.position = new Vector3(pos.x, pos.y, -0.35f);
            fx.Go.transform.rotation = Quaternion.identity;
            fx.StartScale = Mathf.Max(0.02f, diameter / 2f);
            fx.EndScale = fx.StartScale * 1.25f;
            fx.Color = color;
            fx.Renderer.color = color;
            fx.Elapsed = 0f;
            fx.Duration = duration;
            fx.Velocity = Vector2.zero;
            fx.RotationSpeed = 0f;
            fx.Fade = true;
        }

        public void ExplosionFlash(Vector2 pos, float radius)
        {
            WorldFlash(pos, radius * 2.0f, new Color(1f, 0.72f, 0.28f, 0.9f), 0.10f);
            SpawnRing(pos, radius * 1.65f, new Color(1f, 0.82f, 0.45f, 0.85f), 0.18f, true);
        }

        public void Muzzle(Vector2 pos, Vector2 dir)
        {
            Fx fx = Take(ArtFlash(), 40);
            if (fx == null) return;
            Vector2 p = pos + dir * 0.10f;
            fx.Go.transform.position = new Vector3(p.x, p.y, -0.4f);
            fx.Go.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
            fx.StartScale = 0.24f;
            fx.EndScale = 0.45f;
            fx.Color = new Color(1f, 0.87f, 0.4f, 0.95f);
            fx.Renderer.color = fx.Color;
            fx.Elapsed = 0f;
            fx.Duration = 0.045f;
            fx.RotationSpeed = 0f;
            fx.Fade = true;
        }

        public void Slash(Vector2 pos, bool facingRight)
        {
            Fx fx = Take(ArtSlash(), 35);
            if (fx == null) return;
            fx.Go.transform.position = new Vector3(pos.x + (facingRight ? 0.15f : -0.15f), pos.y, -0.4f);
            fx.Go.transform.rotation = Quaternion.Euler(0f, 0f, facingRight ? 0f : 180f);
            fx.StartScale = 0.7f;
            fx.EndScale = 1.15f;
            fx.Color = new Color(1f, 1f, 1f, 0.9f);
            fx.Renderer.color = fx.Color;
            fx.Elapsed = 0f;
            fx.Duration = 0.10f;
            fx.Velocity = Vector2.zero;
            fx.RotationSpeed = 0f;
            fx.Fade = true;
        }

        public void SpawnRing(Vector2 pos, float diameterUnits, Color color, float duration = 0.3f, bool expand = true)
        {
            Fx fx = Take(ArtRing(), 12);
            if (fx == null) return;
            fx.Go.transform.position = new Vector3(pos.x, pos.y, -0.34f);
            fx.Go.transform.rotation = Quaternion.identity;
            fx.StartScale = diameterUnits / 4f;
            fx.EndScale = expand ? fx.StartScale * 1.9f : fx.StartScale;
            fx.Color = color;
            fx.Renderer.color = color;
            fx.Elapsed = 0f;
            fx.Duration = duration;
            fx.Velocity = Vector2.zero;
            fx.RotationSpeed = 0f;
            fx.Fade = true;
        }

        public void DustPuff(Vector2 pos, float size, Color color)
        {
            WorldFlash(pos, size, color, 0.25f);
        }

        public void SmallHitFlash()
        {
            StartScreenFlash(new Color(1f, 0.9f, 0.7f, 0.08f), 0.035f);
        }

        public void DamageFlash()
        {
            StartScreenFlash(new Color(0.9f, 0.15f, 0.12f, 0.20f), 0.10f);
        }

        public void ChanFlash()
        {
            StartScreenFlash(new Color(1f, 0.8f, 0.35f, 0.10f), 0.08f);
        }

        private void StartScreenFlash(Color color, float duration)
        {
            _screenFlashColor = color;
            _screenFlashDuration = duration;
            _screenFlashRemaining = duration;
        }

        public bool TryGetScreenFlash(out Color color)
        {
            color = _screenFlashColor;
            if (_screenFlashRemaining <= 0f || _screenFlashDuration <= 0f)
                return false;
            color.a *= _screenFlashRemaining / _screenFlashDuration;
            return true;
        }

        private Fx Take(Sprite sprite, int sortingOrder)
        {
            Fx fx = null;
            for (int i = 0; i < _pool.Count; i++)
            {
                if (!_pool[i].Active)
                {
                    fx = _pool[i];
                    break;
                }
            }

            if (fx == null)
            {
                GameObject go = new GameObject("fx");
                go.transform.SetParent(_parent, false);
                SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = sprite;
                sr.sortingOrder = sortingOrder;
                fx = new Fx { Go = go, Renderer = sr };
                _pool.Add(fx);
            }

            fx.Renderer.sprite = sprite;
            fx.Renderer.sortingOrder = sortingOrder;
            fx.Renderer.color = Color.white;
            fx.Go.SetActive(true);
            fx.Active = true;
            fx.Fade = true;
            return fx;
        }

        private Sprite ArtFlash() => _game.Art.FlashSprite;
        private Sprite ArtRing() => _game.Art.RingSprite;
        private Sprite ArtSlash() => _game.Art.SlashSprite;
    }
}
