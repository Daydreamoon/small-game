using UnityEngine;

namespace Breakline
{
    public static class MathUtil
    {
        public static readonly Vector2[] Dir8 =
        {
            new Vector2(1f, 0f),
            new Vector2(0.70710678f, 0.70710678f),
            new Vector2(0f, 1f),
            new Vector2(-0.70710678f, 0.70710678f),
            new Vector2(-1f, 0f),
            new Vector2(-0.70710678f, -0.70710678f),
            new Vector2(0f, -1f),
            new Vector2(0.70710678f, -0.70710678f)
        };

        public static Vector2 Quantize8(Vector2 dir)
        {
            if (dir.sqrMagnitude < 0.0001f)
                return Vector2.right;
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            int sector = Mathf.RoundToInt(angle / 45f) & 7;
            return Dir8[sector];
        }

        public static int SectorIndex(Vector2 dir)
        {
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            return Mathf.RoundToInt(angle / 45f) & 7;
        }

        public static float AngleSigned(Vector2 from, Vector2 to)
        {
            return Vector2.SignedAngle(from, to);
        }

        public static Rect AabbFromCenter(Vector2 center, Vector2 halfExtents)
        {
            return new Rect(center.x - halfExtents.x, center.y - halfExtents.y,
                halfExtents.x * 2f, halfExtents.y * 2f);
        }

        public static bool RectOverlaps(in Rect a, in Rect b)
        {
            return a.xMin < b.xMax && a.xMax > b.xMin && a.yMin < b.yMax && a.yMax > b.yMin;
        }

        /// <summary>
        /// Segment vs axis-aligned rectangle (slab method). Returns the nearest normalized t.
        /// </summary>
        public static bool SegmentRect(Vector2 start, Vector2 end, in Rect rect, out float t)
        {
            Vector2 dir = end - start;
            float tMin = 0f;
            float tMax = 1f;

            if (Mathf.Abs(dir.x) < 1e-6f)
            {
                if (start.x < rect.xMin || start.x > rect.xMax)
                {
                    t = 0f;
                    return false;
                }
            }
            else
            {
                float inv = 1f / dir.x;
                float t1 = (rect.xMin - start.x) * inv;
                float t2 = (rect.xMax - start.x) * inv;
                if (t1 > t2) { float tmp = t1; t1 = t2; t2 = tmp; }
                tMin = Mathf.Max(tMin, t1);
                tMax = Mathf.Min(tMax, t2);
                if (tMin > tMax)
                {
                    t = 0f;
                    return false;
                }
            }

            if (Mathf.Abs(dir.y) < 1e-6f)
            {
                if (start.y < rect.yMin || start.y > rect.yMax)
                {
                    t = 0f;
                    return false;
                }
            }
            else
            {
                float inv = 1f / dir.y;
                float t1 = (rect.yMin - start.y) * inv;
                float t2 = (rect.yMax - start.y) * inv;
                if (t1 > t2) { float tmp = t1; t1 = t2; t2 = tmp; }
                tMin = Mathf.Max(tMin, t1);
                tMax = Mathf.Min(tMax, t2);
                if (tMin > tMax)
                {
                    t = 0f;
                    return false;
                }
            }

            t = tMin;
            return true;
        }

        public static float RemapDeadzone(float magnitude, float deadzone)
        {
            if (magnitude <= deadzone)
                return 0f;
            return Mathf.Clamp01((magnitude - deadzone) / (1f - deadzone));
        }

        public static Color Rgba(byte r, byte g, byte b, byte a = 255)
        {
            return new Color32(r, g, b, a);
        }

        public static Color Hex(string hex)
        {
            return ColorUtility.TryParseHtmlString(hex, out Color color) ? color : Color.magenta;
        }

        public static float Damp(float current, float target, float lambda, float dt)
        {
            return Mathf.Lerp(current, target, 1f - Mathf.Exp(-lambda * dt));
        }

        public static Vector2 Damp(Vector2 current, Vector2 target, float lambda, float dt)
        {
            return Vector2.Lerp(current, target, 1f - Mathf.Exp(-lambda * dt));
        }
    }
}
