using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace Breakline
{
    /// <summary>
    /// Loads designer-authored CSV tuning tables from Resources/Config.
    /// Every feel value in M1 is expected to live in these files, not in code.
    /// </summary>
    public sealed class Tuning
    {
        private struct Entry
        {
            public float Value;
            public float Min;
            public float Max;
            public string Unit;
        }

        private readonly Dictionary<string, Entry> _values = new Dictionary<string, Entry>(128, StringComparer.Ordinal);
        private readonly HashSet<string> _missingReported = new HashSet<string>(StringComparer.Ordinal);

        public int Count => _values.Count;

        private static readonly string[] Tables =
        {
            "player", "combat", "explosion", "feel", "camera", "debris", "terrain", "enemies"
        };

        public void Load()
        {
            _values.Clear();
            _missingReported.Clear();

            int loadedRows = 0;
            foreach (string table in Tables)
            {
                TextAsset asset = Resources.Load<TextAsset>("Config/" + table);
                if (asset == null)
                {
                    Debug.LogError($"[BREAKLINE] Missing tuning table Resources/Config/{table}.csv");
                    continue;
                }

                loadedRows += ParseCsv(asset.text, table, false);
            }

            // Optional dev-only override: push a CSV with key,value rows to persistentDataPath.
            string overridePath = Path.Combine(Application.persistentDataPath, "M1TuningOverride.csv");
            if (File.Exists(overridePath))
            {
                try
                {
                    int overrideRows = ParseCsv(File.ReadAllText(overridePath), "device-override", true);
                    Debug.Log($"[BREAKLINE] Applied {overrideRows} live tuning overrides from {overridePath}");
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[BREAKLINE] Tuning override ignored: {e.Message}");
                }
            }

            Debug.Log($"[BREAKLINE] Tuning loaded: {loadedRows} rows across {Tables.Length} tables");
        }

        private int ParseCsv(string text, string source, bool allowPartial)
        {
            int count = 0;
            string[] lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            foreach (string rawLine in lines)
            {
                string line = rawLine.Trim();
                if (line.Length == 0 || line[0] == '#')
                    continue;
                if (line.StartsWith("key,", StringComparison.OrdinalIgnoreCase))
                    continue;

                string[] fields = line.Split(',');
                if (fields.Length < 2)
                {
                    if (!allowPartial)
                        Debug.LogWarning($"[BREAKLINE] {source}: malformed CSV row: {line}");
                    continue;
                }

                string key = fields[0].Trim();
                string valueText = fields[1].Trim();
                if (key.Length == 0)
                    continue;

                if (!float.TryParse(valueText, NumberStyles.Float, CultureInfo.InvariantCulture, out float value))
                {
                    if (!allowPartial)
                        Debug.LogError($"[BREAKLINE] {source}: '{key}' has non-numeric value '{valueText}'");
                    continue;
                }

                float min = float.NegativeInfinity;
                float max = float.PositiveInfinity;
                string unit = string.Empty;
                if (fields.Length >= 4)
                    float.TryParse(fields[3].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out min);
                if (fields.Length >= 5)
                    float.TryParse(fields[4].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out max);
                if (fields.Length >= 3)
                    unit = fields[2].Trim();

                if (min > max)
                {
                    Debug.LogError($"[BREAKLINE] {source}: '{key}' min > max, row skipped");
                    continue;
                }

                if (_values.ContainsKey(key))
                    Debug.LogWarning($"[BREAKLINE] Duplicate tuning key '{key}' from {source}; last one wins");

                _values[key] = new Entry
                {
                    Value = Mathf.Clamp(value, min, max),
                    Min = min,
                    Max = max,
                    Unit = unit
                };

                if (!Mathf.Approximately(value, _values[key].Value))
                    Debug.LogWarning($"[BREAKLINE] {key}={value} clamped to {_values[key].Value} ({min}..{max})");
                count++;
            }
            return count;
        }

        public float GetFloat(string key, float fallback = 0f)
        {
            if (_values.TryGetValue(key, out Entry entry))
                return entry.Value;
            if (_missingReported.Add(key))
                Debug.LogError($"[BREAKLINE] Missing tuning key '{key}', using fallback {fallback}");
            return fallback;
        }

        public int GetInt(string key, int fallback = 0)
        {
            return Mathf.RoundToInt(GetFloat(key, fallback));
        }

        public bool GetBool(string key, bool fallback = false)
        {
            if (_values.TryGetValue(key, out Entry entry))
                return entry.Value > 0.5f;
            if (_missingReported.Add(key))
                Debug.LogError($"[BREAKLINE] Missing tuning key '{key}', using fallback {fallback}");
            return fallback;
        }

        public string GetUnit(string key)
        {
            return _values.TryGetValue(key, out Entry entry) ? entry.Unit : string.Empty;
        }
    }
}
