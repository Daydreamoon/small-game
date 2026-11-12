using UnityEngine;

namespace Breakline
{
    public enum SlowMoReason
    {
        None,
        BossDeath,
        LevelClear
    }

    /// <summary>
    /// Single owner of hitstop and slow motion. The slow-mo API deliberately exposes
    /// only BossDeath and LevelClear; ordinary kills never have a path to it.
    /// </summary>
    public sealed class Feedback
    {
        private readonly GameRoot _game;
        private float _hitstopRemaining;
        private float _slowRemaining;
        private float _slowScale = 1f;
        private float _lastHitstopStart = -99f;
        private SlowMoReason _slowReason = SlowMoReason.None;
        private float _lastAppliedScale = -1f;

        public bool IsSlowMo => _slowReason != SlowMoReason.None;
        public SlowMoReason CurrentSlowReason => _slowReason;

        public Feedback(GameRoot game)
        {
            _game = game;
        }

        public void Init()
        {
            Time.timeScale = 1f;
            _lastAppliedScale = 1f;
        }

        public void TickUnscaled(float unscaledDt)
        {
            if (_hitstopRemaining > 0f)
                _hitstopRemaining = Mathf.Max(0f, _hitstopRemaining - unscaledDt);
            if (_slowRemaining > 0f)
            {
                _slowRemaining = Mathf.Max(0f, _slowRemaining - unscaledDt);
                if (_slowRemaining <= 0f)
                    _slowReason = SlowMoReason.None;
            }

            float desired = 1f;
            if (_slowReason != SlowMoReason.None)
                desired = _slowScale;
            else if (_hitstopRemaining > 0f)
                desired = 0f;

            if (!Mathf.Approximately(desired, _lastAppliedScale))
            {
                Time.timeScale = desired;
                _lastAppliedScale = desired;
            }
        }

        public void RequestHitstopFrames(int frames, bool ignoreCooldown = false)
        {
            if (frames <= 0)
                return;
            if (!ignoreCooldown && Time.unscaledTime - _lastHitstopStart < _game.Config.hitstopCooldown)
                return;

            _lastHitstopStart = Time.unscaledTime;
            float duration = frames / 60f;
            _hitstopRemaining = Mathf.Max(_hitstopRemaining, duration);
            if (_slowReason == SlowMoReason.None)
            {
                Time.timeScale = 0f;
                _lastAppliedScale = 0f;
            }
        }

        public void NormalKill()
        {
            RequestHitstopFrames(_game.Config.killHitstopFrames);
            _game.Camera.AddShake(_game.Config.killShakePx, _game.Config.killShakeDur, _game.Config.killShakeFreq);
            _game.Vfx.SmallHitFlash();
        }

        public void KnifeHit()
        {
            RequestHitstopFrames(_game.Config.knifeHitstopFrames, true);
            _game.Camera.AddShake(0.7f, 0.045f, 50f);
        }

        public void Crush()
        {
            RequestHitstopFrames(_game.Config.crushHitstopFrames, true);
            _game.Camera.AddShake(_game.Config.crushShakePx, _game.Config.crushShakeDur, _game.Config.crushShakeFreq);
        }

        public void Explosion()
        {
            _game.Camera.AddShake(_game.Config.explosionShakePx, _game.Config.explosionShakeDur, _game.Config.explosionShakeFreq);
        }

        public void RequestSlowMo(SlowMoReason reason)
        {
            // This is the only slow-motion entry point. If a new reason is ever needed,
            // it must be added deliberately and reviewed against the feel spec.
            if (reason != SlowMoReason.BossDeath && reason != SlowMoReason.LevelClear)
            {
                Debug.LogError($"[BREAKLINE] Rejected slow-mo reason outside whitelist: {reason}");
                return;
            }

            _slowReason = reason;
            if (reason == SlowMoReason.BossDeath)
            {
                _slowScale = _game.Config.bossSlowScale;
                _slowRemaining = Mathf.Clamp(_game.Config.bossSlowDuration, 0.50f, 0.80f);
                _game.Camera.AddShake(_game.Config.bossShakePx, 0.65f, 22f);
            }
            else
            {
                _slowScale = _game.Config.clearSlowScale;
                _slowRemaining = Mathf.Clamp(_game.Config.clearSlowDuration, 0.20f, 0.80f);
            }

            _hitstopRemaining = 0f;
            Time.timeScale = _slowScale;
            _lastAppliedScale = _slowScale;
            Debug.Log($"[BREAKLINE] SlowMo requested: {reason} for {_slowRemaining:0.00}s at x{_slowScale:0.00}");
        }

        public void ResetAll()
        {
            _hitstopRemaining = 0f;
            _slowRemaining = 0f;
            _slowReason = SlowMoReason.None;
            _slowScale = 1f;
            Time.timeScale = 1f;
            _lastAppliedScale = 1f;
        }

        public void OnDestroy()
        {
            Time.timeScale = 1f;
        }
    }
}
