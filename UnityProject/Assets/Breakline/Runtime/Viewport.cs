using UnityEngine;

namespace Breakline
{
    /// <summary>
    /// Renders the world to a 480x270-class RT and presents it with integer scaling.
    /// Wider phones see extra columns inside the same pixel scale; no non-integer stretch.
    /// </summary>
    public sealed class Viewport
    {
        public const int BaseWidth = 480;
        public const int BaseHeight = 270;
        public const int MaxWidth = 640;
        public const float PixelsPerUnit = 16f;

        private Camera _camera;
        private RenderTexture _rt;
        private int _screenW;
        private int _screenH;
        private int _rtWidth;

        public Camera Camera => _camera;
        public Rect DisplayRect { get; private set; }
        public int Scale { get; private set; }
        public int RtWidth => _rtWidth;
        public int RtHeight => BaseHeight;
        public RenderTexture Texture => _rt;
        public bool FlipPresentation { get; set; }

        public void Init(Transform parent)
        {
            GameObject camGo = new GameObject("WorldCamera");
            camGo.transform.SetParent(parent, false);
            camGo.transform.position = new Vector3(0f, 0f, -10f);
            _camera = camGo.AddComponent<Camera>();
            _camera.orthographic = true;
            _camera.orthographicSize = BaseHeight / (2f * PixelsPerUnit);
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = MathUtil.Hex("#11151c");
            _camera.nearClipPlane = 0.05f;
            _camera.farClipPlane = 100f;
            _camera.allowHDR = false;
            _camera.allowMSAA = false;
            _camera.depth = -10;
            camGo.AddComponent<AudioListener>();

            // Verified on both D3D11 and OpenGL Core Windows players: GUI.DrawTexture
            // presents this RenderTexture upright without a texcoord flip. Keep the debug
            // override for driver/GLES anomalies instead of guessing from the UV flag.
            FlipPresentation = false;
            Debug.Log($"[BREAKLINE] graphicsUVStartsAtTop={SystemInfo.graphicsUVStartsAtTop}; " +
                      $"presentationFlip={FlipPresentation}; gfx={SystemInfo.graphicsDeviceType}");

            RebuildIfNeeded(true);
        }

        public void Tick()
        {
            RebuildIfNeeded(false);
        }

        private void RebuildIfNeeded(bool force)
        {
            int w = Screen.width;
            int h = Screen.height;
            if (!force && w == _screenW && h == _screenH)
                return;

            _screenW = w;
            _screenH = h;

            float aspect = Mathf.Max(0.5f, w / (float)Mathf.Max(1, h));
            float baseAspect = BaseWidth / (float)BaseHeight;
            int desiredWidth = BaseWidth;
            if (aspect > baseAspect)
                desiredWidth = Mathf.RoundToInt(BaseWidth * (aspect / baseAspect));
            desiredWidth = Mathf.Clamp(desiredWidth, BaseWidth, MaxWidth);
            if ((desiredWidth & 1) == 1)
                desiredWidth++;

            int verticalScale = Mathf.Max(1, Mathf.FloorToInt(h / (float)BaseHeight));
            int horizontalScale = Mathf.Max(1, Mathf.FloorToInt(w / (float)desiredWidth));
            int scale = Mathf.Max(1, Mathf.Min(verticalScale, horizontalScale));
            if (scale == 1)
                scale = 1;

            int displayW = desiredWidth * scale;
            int displayH = BaseHeight * scale;
            int x = Mathf.Max(0, (w - displayW) / 2);
            int y = Mathf.Max(0, (h - displayH) / 2);
            DisplayRect = new Rect(x, y, displayW, displayH);
            Scale = scale;

            if (_rt == null || force || _rt.width != desiredWidth)
            {
                if (_rt != null)
                {
                    _camera.targetTexture = null;
                    _rt.Release();
                    Object.Destroy(_rt);
                }

                _rtWidth = desiredWidth;
                _rt = new RenderTexture(desiredWidth, BaseHeight, 16, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default)
                {
                    name = "M1WorldRT",
                    filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Clamp,
                    antiAliasing = 1,
                    useMipMap = false,
                    autoGenerateMips = false
                };
                _rt.Create();
                _camera.targetTexture = _rt;
            }

            _camera.orthographicSize = BaseHeight / (2f * PixelsPerUnit);
            _camera.aspect = desiredWidth / (float)BaseHeight;
        }

        public void DrawWorld()
        {
            if (_rt == null)
                return;
            GUI.color = Color.white;
            // Default false on all backends (verified D3D + GL Core). The debug panel still
            // exposes a manual override if a specific GLES driver presents the RT inverted.
            if (FlipPresentation)
                GUI.DrawTextureWithTexCoords(DisplayRect, _rt, new Rect(0f, 1f, 1f, -1f));
            else
                GUI.DrawTexture(DisplayRect, _rt, ScaleMode.StretchToFill, false);
        }

        public Vector2 WorldToGui(Vector3 world)
        {
            if (_camera == null)
                return Vector2.zero;
            Vector3 vp = _camera.WorldToViewportPoint(world);
            return new Vector2(
                DisplayRect.x + vp.x * DisplayRect.width,
                DisplayRect.y + (1f - vp.y) * DisplayRect.height);
        }

        public bool IsWorldVisible(Vector3 world, float margin = 0.5f)
        {
            if (_camera == null)
                return false;
            Vector3 vp = _camera.WorldToViewportPoint(world);
            return vp.z > 0f && vp.x >= -margin && vp.x <= 1f + margin && vp.y >= -margin && vp.y <= 1f + margin;
        }

        public Bounds WorldViewBoundsXZ()
        {
            float halfH = _camera.orthographicSize;
            float halfW = halfH * _camera.aspect;
            Vector3 p = _camera.transform.position;
            return new Bounds(new Vector3(p.x, p.y, 0f), new Vector3(halfW * 2f, halfH * 2f, 1f));
        }
    }
}
