using UnityEngine;

namespace Breakline
{
    public enum AudioCue
    {
        Shoot,
        ThirdShot,
        Knife,
        Explosion,
        Crush,
        Jump,
        Land,
        Tick,
        Gate
    }

    /// <summary>
    /// Tiny procedural SFX set. No external audio assets in M1.
    /// </summary>
    public sealed class AudioSynth
    {
        private const int SampleRate = 44100;
        private AudioSource _source;
        private AudioClip _shoot, _third, _knife, _explosion, _crush, _jump, _land, _tick, _gate;

        public void Init(GameObject host)
        {
            _source = host.AddComponent<AudioSource>();
            _source.playOnAwake = false;
            _source.spatialBlend = 0f;
            _source.volume = 0.7f;

            _shoot = MakeClip("sfx_shoot", 0.055f, (t, n) =>
            {
                float env = Mathf.Exp(-t * 48f);
                float sq = Mathf.Sign(Mathf.Sin(2f * Mathf.PI * (680f - t * 5000f) * t));
                return (n * 0.55f + sq * 0.45f) * env * 0.55f;
            });
            _third = MakeClip("sfx_third", 0.08f, (t, n) =>
            {
                float env = Mathf.Exp(-t * 32f);
                float sq = Mathf.Sign(Mathf.Sin(2f * Mathf.PI * (420f - t * 2500f) * t));
                return (n * 0.5f + sq * 0.5f) * env * 0.65f;
            });
            _knife = MakeClip("sfx_knife", 0.11f, (t, n) =>
            {
                float env = Mathf.Sin(Mathf.Clamp01(t / 0.11f) * Mathf.PI);
                return (n * 0.8f + Mathf.Sin(2f * Mathf.PI * 2200f * t) * 0.2f) * env * 0.6f;
            });
            _explosion = MakeClip("sfx_explosion", 0.42f, (t, n) =>
            {
                float env = Mathf.Exp(-t * 9f);
                float low = Mathf.Sin(2f * Mathf.PI * (62f - t * 40f) * t);
                float crunch = n * (0.7f + 0.3f * Mathf.Sin(2f * Mathf.PI * 170f * t));
                return (low * 0.6f + crunch * 0.55f) * env * 0.75f;
            });
            _crush = MakeClip("sfx_crush", 0.36f, (t, n) =>
            {
                float env = Mathf.Exp(-t * 12f);
                float thud = Mathf.Sin(2f * Mathf.PI * 48f * t);
                float debris = n * Mathf.Exp(-t * 25f);
                return (thud * 0.8f + debris * 0.5f) * env * 0.9f;
            });
            _jump = MakeClip("sfx_jump", 0.09f, (t, n) =>
            {
                float env = Mathf.Exp(-t * 28f);
                return Mathf.Sin(2f * Mathf.PI * (280f + t * 7000f) * t) * env * 0.28f;
            });
            _land = MakeClip("sfx_land", 0.08f, (t, n) =>
            {
                float env = Mathf.Exp(-t * 35f);
                return (n * 0.4f + Mathf.Sin(2f * Mathf.PI * 95f * t) * 0.6f) * env * 0.35f;
            });
            _tick = MakeClip("sfx_tick", 0.035f, (t, n) =>
            {
                return Mathf.Sin(2f * Mathf.PI * 1350f * t) * Mathf.Exp(-t * 80f) * 0.22f;
            });
            _gate = MakeClip("sfx_gate", 0.34f, (t, n) =>
            {
                float env = Mathf.Exp(-t * 7f);
                float chord = Mathf.Sin(2f * Mathf.PI * 440f * t) + 0.5f * Mathf.Sin(2f * Mathf.PI * 660f * t);
                return chord * env * 0.22f;
            });
        }

        public void Play(AudioCue cue, float volume = 1f)
        {
            if (_source == null) return;
            AudioClip clip = cue switch
            {
                AudioCue.Shoot => _shoot,
                AudioCue.ThirdShot => _third,
                AudioCue.Knife => _knife,
                AudioCue.Explosion => _explosion,
                AudioCue.Crush => _crush,
                AudioCue.Jump => _jump,
                AudioCue.Land => _land,
                AudioCue.Tick => _tick,
                AudioCue.Gate => _gate,
                _ => null
            };
            if (clip != null)
                _source.PlayOneShot(clip, Mathf.Clamp01(volume));
        }

        private static AudioClip MakeClip(string name, float seconds, System.Func<float, float, float> generator)
        {
            int samples = Mathf.Max(16, Mathf.CeilToInt(seconds * SampleRate));
            float[] data = new float[samples];
            uint noiseState = 0x12345678u;
            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)SampleRate;
                noiseState = noiseState * 1664525u + 1013904223u;
                float noise = ((noiseState >> 9) & 0x7fffff) / 8388607f * 2f - 1f;
                data[i] = Mathf.Clamp(generator(t, noise), -1f, 1f);
            }
            AudioClip clip = AudioClip.Create(name, samples, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
