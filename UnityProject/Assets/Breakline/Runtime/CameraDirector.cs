using UnityEngine;

namespace Breakline
{
    public sealed class CameraDirector
    {
        private readonly GameRoot _game;
        private Vector2 _current;
        private Vector2 _lastTarget;
        private float _shakePx;
        private float _shakeDuration;
        private float _shakeFrequency;
        private float _shakeTime;
        private float _shakeSeed;
        private bool _initialized;

        public CameraDirector(GameRoot game)
        {
            _game = game;
        }

        public void Init()
        {
            PlayerController player = _game.Player;
            if (player != null)
            {
                _current = ClampToLevel(player.Position);
                _lastTarget = _current;
            }
            else
            {
                _current = Vector2.zero;
            }

            Apply(0f);
            _initialized = true;
        }

        public void SnapToPlayer()
        {
            if (_game.Player == null) return;
            _current = ClampToLevel(_game.Player.Position);
            _lastTarget = _current;
            Apply(0f);
        }

        public void Tick(float dt)
        {
            if (!_initialized) Init();
            if (_game.Player == null)
            {
                Apply(dt);
                return;
            }

            PlayerController p = _game.Player;
            Vector2 target = p.Position;
            target.x += p.Facing * _game.Config.lookaheadX;
            target.y += _game.Config.lookaheadY * Mathf.Clamp(p.AimDirection.y, -1f, 1f);
            target = ClampToLevel(target);

            float smooth = 1f - Mathf.Exp(-_game.Config.cameraDamping * Mathf.Max(0.0001f, dt) * 60f);
            // Config damping is expressed as seconds-to-catch-up; the lambda below keeps it frame-rate independent.
            float lambda = 1f / Mathf.Max(0.01f, _game.Config.cameraDamping);
            _current = Vector2.Lerp(_current, target, 1f - Mathf.Exp(-lambda * Mathf.Max(0.0001f, dt)));
            _lastTarget = target;
            Apply(dt);
        }

        public void TickShakeUnscaled(float unscaledDt)
        {
            if (_shakePx <= 0f || _shakeDuration <= 0f)
                return;

            _shakeTime += unscaledDt;
            if (_shakeTime >= _shakeDuration)
            {
                _shakePx = 0f;
                _shakeDuration = 0f;
            }
            Apply(0f);
        }

        public void AddShake(float amplitudePx, float duration, float frequency)
        {
            if (amplitudePx <= 0f || duration <= 0f)
                return;
            if (amplitudePx < _shakePx && _shakeTime < _shakeDuration)
                return;

            _shakePx = amplitudePx;
            _shakeDuration = duration;
            _shakeFrequency = Mathf.Max(1f, frequency);
            _shakeTime = 0f;
            _shakeSeed = Time.unscaledTime * 7.13f;
        }

        private void Apply(float dt)
        {
            if (_game.Viewport == null || _game.Viewport.Camera == null)
                return;

            float halfWpx = _game.Viewport.RtWidth * 0.5f;
            float halfHpx = _game.Viewport.RtHeight * 0.5f;
            Vector2 center = _current;

            float decay = 1f;
            if (_shakeDuration > 0f)
                decay = Mathf.Clamp01(1f - _shakeTime / _shakeDuration);

            float ox = 0f;
            float oy = 0f;
            if (_shakePx > 0f && decay > 0f)
            {
                float t = _shakeTime * _shakeFrequency;
                ox = Mathf.Sin(t * 12.9898f + _shakeSeed) * _shakePx * decay;
                oy = Mathf.Cos(t * 7.233f + _shakeSeed * 1.7f) * _shakePx * 0.72f * decay;
                ox = Mathf.Round(ox);
                oy = Mathf.Round(oy);
            }

            Vector3 pos = new Vector3(center.x, center.y, -10f);
            if (_game.Viewport != null)
            {
                // Keep pixel grid: 1/16 world unit = 1 art pixel.
                pos.x += ox / 16f;
                pos.y += oy / 16f;
                pos.x = Mathf.Round(pos.x * 16f) / 16f;
                pos.y = Mathf.Round(pos.y * 16f) / 16f;
            }

            _game.Viewport.Camera.transform.position = pos;
        }

        private Vector2 ClampToLevel(Vector2 pos)
        {
            if (_game.Terrain == null || _game.Viewport == null)
                return pos;

            float halfW = _game.Viewport.RtWidth / 32f; // RT pixels / 16 px per unit / 2
            float halfH = _game.Viewport.RtHeight / 32f;
            float minX = halfW;
            float maxX = _game.Terrain.Width - halfW;
            float minY = halfH;
            float maxY = _game.Terrain.Height - halfH;

            if (maxX < minX) pos.x = _game.Terrain.Width * 0.5f;
            else pos.x = Mathf.Clamp(pos.x, minX, maxX);
            if (maxY < minY) pos.y = _game.Terrain.Height * 0.5f;
            else pos.y = Mathf.Clamp(pos.y, minY, maxY);
            return pos;
        }

        public void ClearShake()
        {
            _shakePx = 0f;
            _shakeDuration = 0f;
            Apply(0f);
        }
    }
}
