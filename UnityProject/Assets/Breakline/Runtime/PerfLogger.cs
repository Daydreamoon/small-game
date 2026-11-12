using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace Breakline
{
    /// <summary>
    /// On-device frame-time capture. CSV rows only ever come from real runtime frames.
    /// The graph is a rolling view for early trend detection; it is not a substitute for CSV.
    /// </summary>
    public sealed class PerfLogger
    {
        private const int FrameCapacity = 65536; // >10 minutes at 60 Hz
        private readonly GameRoot _game;
        private readonly List<float> _frames = new List<float>(FrameCapacity);
        private float _emaFrame;
        private float _startUnscaled;
        private float _duration;
        private string _scenario = string.Empty;
        private bool _recording;
        private int _maxVisual;
        private int _maxT1;
        private int _maxT0;
        private int _maxFull;
        private int _maxChain;

        public const int GraphCapacity = 240;
        public readonly float[] GraphMs = new float[GraphCapacity];
        public int GraphHead { get; private set; }
        public int GraphCount { get; private set; }

        public bool IsRecording => _recording;
        public string Scenario => _scenario;
        public float RecordingRemaining => _recording ? Mathf.Max(0f, _duration - (Time.unscaledTime - _startUnscaled)) : 0f;
        public float FpsNow { get; private set; }
        public string LastSummary { get; private set; } = "no sample";
        public string CsvPath { get; private set; } = string.Empty;
        public string RawFramesPath { get; private set; } = string.Empty;

        public PerfLogger(GameRoot game)
        {
            _game = game;
        }

        public void Init()
        {
            CsvPath = Path.Combine(Application.persistentDataPath, "M1-Performance.csv");
            FpsNow = 60f;
            Debug.Log($"[BREAKLINE] Perf CSV path: {CsvPath}");
        }

        public void Tick()
        {
            float dt = Time.unscaledDeltaTime;
            if (dt <= 0f) return;

            float ms = dt * 1000f;
            _emaFrame = _emaFrame <= 0f ? ms : Mathf.Lerp(_emaFrame, ms, 0.10f);
            FpsNow = 1000f / Mathf.Max(0.001f, _emaFrame);
            GraphMs[GraphHead] = ms;
            GraphHead = (GraphHead + 1) % GraphCapacity;
            if (GraphCount < GraphCapacity) GraphCount++;

            if (!_recording) return;
            if (_frames.Count < FrameCapacity)
                _frames.Add(ms);

            _maxVisual = Mathf.Max(_maxVisual, _game.Debris.ActiveVisualCount);
            _maxT1 = Mathf.Max(_maxT1, _game.Debris.T1Count);
            _maxT0 = Mathf.Max(_maxT0, _game.Terrain.ActiveFallingChunks);
            _maxFull = Mathf.Max(_maxFull, _game.Debris.StressBodyCount);
            _maxChain = Mathf.Max(_maxChain, _game.Props.ChainCount);

            if (Time.unscaledTime - _startUnscaled >= _duration)
                Flush("complete");
        }

        public void StartRecording(string scenario, float durationSeconds = 0f)
        {
            if (_recording)
                Flush("interrupted-by-new");

            _scenario = string.IsNullOrEmpty(scenario) ? "manual" : scenario;
            _duration = durationSeconds > 0f ? durationSeconds : _game.Config.perfWindowSeconds;
            _frames.Clear();
            _maxVisual = 0;
            _maxT1 = 0;
            _maxT0 = 0;
            _maxFull = 0;
            _maxChain = 0;
            _startUnscaled = Time.unscaledTime;
            _recording = true;
            LastSummary = $"REC {_scenario} ({_duration:0}s)";
            Debug.Log($"[BREAKLINE] Perf recording started: {_scenario}, {_duration:0.0}s");
        }

        public void FlushScenarioIfRecording(string suffix)
        {
            if (_recording)
            {
                _scenario = _scenario + ":" + suffix;
                Flush("route-end");
            }
        }

        public void Flush(string reason)
        {
            if (!_recording) return;
            _recording = false;
            float elapsed = Mathf.Max(0.001f, Time.unscaledTime - _startUnscaled);
            int n = _frames.Count;
            if (n < 2)
            {
                LastSummary = "sample too short";
                return;
            }

            float[] arr = _frames.ToArray();
            Array.Sort(arr);
            double sum = 0.0;
            for (int i = 0; i < arr.Length; i++) sum += arr[i];
            float meanMs = (float)(sum / arr.Length);
            float avgFps = arr.Length / elapsed;
            int i99 = Mathf.Clamp(Mathf.FloorToInt(arr.Length * 0.99f), 0, arr.Length - 1);
            int i999 = Mathf.Clamp(Mathf.FloorToInt(arr.Length * 0.999f), 0, arr.Length - 1);
            float onePercentLow = 1000f / Mathf.Max(0.001f, arr[i99]);
            float pointOnePercentLow = 1000f / Mathf.Max(0.001f, arr[i999]);
            float maxMs = arr[arr.Length - 1];
            float p99Ms = arr[i99];

            LastSummary = $"avg {avgFps:0.0} / 1% {onePercentLow:0.0} / max {maxMs:0.0}ms / {n}f";

            string rawName = $"M1-RawFrames-{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Sanitize(_scenario)}.csv";
            string rawPath = Path.Combine(Path.GetDirectoryName(CsvPath) ?? Application.persistentDataPath, rawName);
            string row = BuildCsvRow(reason, n, elapsed, avgFps, onePercentLow, pointOnePercentLow, maxMs, p99Ms, meanMs, rawName);
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(CsvPath) ?? Application.persistentDataPath);
                bool exists = File.Exists(CsvPath);
                using (StreamWriter writer = new StreamWriter(CsvPath, true, new UTF8Encoding(false)))
                {
                    if (!exists)
                        writer.WriteLine(CsvHeader());
                    writer.WriteLine(row);
                }

                // Keep the per-frame samples beside the summary so a Performance Pass can
                // be audited, not just taken on trust. One file per scenario run.
                StringBuilder raw = new StringBuilder(n * 12);
                raw.AppendLine("frame_index,frame_ms");
                for (int i = 0; i < n; i++)
                    raw.Append(i).Append(',').Append(_frames[i].ToString("0.000", CultureInfo.InvariantCulture)).Append('\n');
                File.WriteAllText(rawPath, raw.ToString(), new UTF8Encoding(false));
                RawFramesPath = rawPath;

                Debug.Log($"[BREAKLINE] Perf CSV appended: {CsvPath}\n{row}\n[BREAKLINE] Raw frames: {rawPath}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[BREAKLINE] Perf CSV write failed: {e}");
            }
        }

        private string BuildCsvRow(string reason, int frames, float elapsed, float avgFps, float low1,
            float low01, float maxMs, float p99Ms, float meanMs, string rawName)
        {
            string device = SystemInfo.deviceModel ?? "unknown";
            string cpu = SystemInfo.processorType ?? "unknown";
            string gpu = SystemInfo.graphicsDeviceName ?? "unknown";
            string gfx = SystemInfo.graphicsDeviceVersion ?? "unknown";
            float refresh = 0f;
            try { refresh = Screen.currentResolution.refreshRate; }
            catch { refresh = 0f; }

            var b = new StringBuilder(512);
            b.Append(DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture)).Append(',');
            b.Append(Csv(_scenario)).Append(',');
            b.Append(Debug.isDebugBuild ? "Development" : "Release").Append(',');
            b.Append(Csv(Application.unityVersion)).Append(',');
            b.Append(Csv(Application.version)).Append(',');
            b.Append(Csv(device)).Append(',');
            b.Append(Csv(cpu)).Append(',');
            b.Append(SystemInfo.systemMemorySize.ToString(CultureInfo.InvariantCulture)).Append(',');
            b.Append(Csv(gpu)).Append(',');
            b.Append(Csv(gfx)).Append(',');
            b.Append(Screen.width).Append(',');
            b.Append(Screen.height).Append(',');
            b.Append(refresh.ToString("0.##", CultureInfo.InvariantCulture)).Append(',');
            b.Append(frames).Append(',');
            b.Append(elapsed.ToString("0.000", CultureInfo.InvariantCulture)).Append(',');
            b.Append(avgFps.ToString("0.00", CultureInfo.InvariantCulture)).Append(',');
            b.Append(low1.ToString("0.00", CultureInfo.InvariantCulture)).Append(',');
            b.Append(low01.ToString("0.00", CultureInfo.InvariantCulture)).Append(',');
            b.Append(maxMs.ToString("0.00", CultureInfo.InvariantCulture)).Append(',');
            b.Append(_maxVisual).Append(',');
            b.Append(_maxT1).Append(',');
            b.Append(_maxT0).Append(',');
            b.Append(_maxFull).Append(',');
            b.Append(_maxChain).Append(',');
            b.Append(Csv($"{reason};mean={meanMs:0.00}ms;p99={p99Ms:0.00}ms;rt={_game.Viewport.RtWidth}x{_game.Viewport.RtHeight};scale={_game.Viewport.Scale};raw={rawName}"));
            return b.ToString();
        }

        private static string CsvHeader()
        {
            return "timestamp_utc,scenario,build,unity,app_version,device_model,processor,ram_mb,gpu,gfx_api,screen_w,screen_h,refresh_hz,frames,elapsed_s,fps_avg,fps_1pct_low,fps_0_1pct_low,max_frame_ms,visual_max,t1_max,t0_max,stress_full_max,chain_max,notes";
        }

        private static string Csv(string value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            if (value.IndexOfAny(new[] { ',', '"', '\n', '\r' }) < 0) return value;
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }

        private static string Sanitize(string value)
        {
            if (string.IsNullOrEmpty(value)) return "run";
            StringBuilder b = new StringBuilder(value.Length);
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                b.Append(char.IsLetterOrDigit(c) || c == '-' || c == '_' ? c : '-');
            }
            return b.ToString();
        }
    }
}
