using UnityEngine;

namespace Breakline
{
    public struct ControlLayout
    {
        public float JoystickRadius;
        public float KnobRadius;
        public float ButtonRadius;
        public Vector2 JoystickCenter;
        public Vector2 JumpCenter;
        public Vector2 GrenadeCenter;
        public Rect SafeGui;

        public static ControlLayout Build()
        {
            float w = Screen.width;
            float h = Screen.height;
            Rect safe = Screen.safeArea;
            float safeLeft = safe.xMin;
            float safeRight = safe.xMax;
            float safeBottomGui = h - safe.yMin;
            float safeTopGui = h - safe.yMax;
            float margin = Mathf.Clamp(h * 0.035f, 10f, 48f);

            float bR = Mathf.Clamp(h * 0.073f, 42f, 110f);
            float joyR = Mathf.Clamp(h * 0.145f, 95f, 230f);

            var layout = new ControlLayout
            {
                ButtonRadius = bR,
                KnobRadius = bR * 0.46f,
                JoystickRadius = joyR,
                SafeGui = new Rect(safeLeft, safeTopGui, Mathf.Max(1f, safeRight - safeLeft),
                    Mathf.Max(1f, safeBottomGui - safeTopGui))
            };

            // The fire button is replaced by drag-to-aim. Keep the two action buttons
            // in the lower-right thumb area and leave the rest of the screen free for aiming.
            layout.JumpCenter = new Vector2(safeRight - margin - bR, safeBottomGui - margin - bR);
            layout.GrenadeCenter = new Vector2(layout.JumpCenter.x - bR * 1.55f,
                layout.JumpCenter.y - bR * 0.55f);
            layout.JoystickCenter = new Vector2(safeLeft + margin + joyR, safeBottomGui - margin - joyR);
            return layout;
        }

        public float ButtonHitRadius => ButtonRadius * 1.16f;
        public float JoystickHitRadius => JoystickRadius * 1.45f;
    }

    public sealed class InputRouter
    {
        private enum Btn { Jump = 0, Grenade = 1 }

        private readonly bool[] _held = new bool[2];
        private readonly int[] _owner = { -1, -1 };
        private readonly bool[] _seen = new bool[32];
        private int _joystickOwner = -1;
        private int _aimOwner = -1;
        private Vector2 _aimStart;
        private bool _aimMoved;
        private bool _aimFired;
        private int _grenadeCandidate = -1;
        private Vector2 _grenadeCandidateStart;
        private float _grenadeCandidateAt;
        private const float GrenadeTapSlopPx = 28f;
        private const float GrenadeMinTapSeconds = 0.08f;
        private bool _mouseAim;
        private Vector2 _mouseAimStart;
        private bool _mouseAimMoved;

        public ControlLayout Layout = ControlLayout.Build();

        public Vector2 MoveInput { get; private set; }
        public Vector2 AimInput { get; private set; }
        public Vector2 JoystickRaw { get; private set; }
        public float MoveVertical { get; private set; }
        public bool UseTouchControls { get; private set; }
        public float AimDragThreshold => Mathf.Max(12f, Screen.height * 0.015f);
        public bool AimDragActive => (_aimOwner >= 0 && _aimMoved) || _mouseAimMoved;

        public bool JumpPressed { get; private set; }
        public bool JumpHeld { get; private set; }
        public bool FirePressed { get; private set; }
        public bool FireHeld { get; private set; }
        public bool GrenadePressed { get; private set; }
        public bool GrenadeHeld { get; private set; }

        public bool DebugBlocked { get; set; }

        public bool AnyGameplayInput
        {
            get
            {
                return MoveInput.sqrMagnitude > 0.02f || Mathf.Abs(MoveVertical) > 0.02f ||
                       AimInput.sqrMagnitude > 0.02f || JumpHeld || FireHeld || GrenadeHeld ||
                       JumpPressed || FirePressed || GrenadePressed;
            }
        }

        public void Sample()
        {
            Layout = ControlLayout.Build();
            JumpPressed = false;
            FirePressed = false;
            GrenadePressed = false;
            JumpHeld = false;
            FireHeld = false;
            GrenadeHeld = false;
            MoveInput = Vector2.zero;
            MoveVertical = 0f;
            AimInput = Vector2.zero;
            JoystickRaw = Vector2.zero;
            UseTouchControls = false;

            if (DebugBlocked)
            {
                ReleaseAllTouches();
                return;
            }

            BootKeyboardAndMouse();
            BootTouches();
        }

        public void EndFrame()
        {
            JumpPressed = false;
            FirePressed = false;
            GrenadePressed = false;
        }

        public void ResetAll()
        {
            ReleaseAllTouches();
            JumpPressed = false;
            JumpHeld = false;
            FirePressed = false;
            FireHeld = false;
            GrenadePressed = false;
            GrenadeHeld = false;
            MoveInput = Vector2.zero;
            MoveVertical = 0f;
            AimInput = Vector2.zero;
            JoystickRaw = Vector2.zero;
            _mouseAim = false;
            _mouseAimMoved = false;
        }

        private void BootKeyboardAndMouse()
        {
            float x = 0f;
            float y = 0f;
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) x -= 1f;
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) x += 1f;
            if (Input.GetKey(KeyCode.W)) y += 1f;
            if (Input.GetKey(KeyCode.S)) y -= 1f;

            if (x != 0f || y != 0f)
            {
                MoveInput = new Vector2(x, 0f);
                MoveVertical = y;
            }

            bool jumpDown = Input.GetKeyDown(KeyCode.Space);
            bool jumpHold = Input.GetKey(KeyCode.Space);
            bool fireDown = Input.GetKeyDown(KeyCode.J);
            bool fireHold = Input.GetKey(KeyCode.J);
            bool nadeDown = false;
            bool nadeHold = false;
            if (!Application.isMobilePlatform)
            {
                // Desktop-only shortcut. On Android, a stray synthetic right mouse
                // event must never throw a grenade.
                nadeDown = Input.GetKeyDown(KeyCode.K) || Input.GetMouseButtonDown(1);
                nadeHold = Input.GetKey(KeyCode.K) || Input.GetMouseButton(1);
            }

            if (jumpDown) JumpPressed = true;
            if (jumpHold) JumpHeld = true;
            if (fireDown) FirePressed = true;
            if (fireHold) FireHeld = true;
            if (nadeDown) GrenadePressed = true;
            if (nadeHold) GrenadeHeld = true;

            // Desktop convenience: drag with the left mouse button to aim and shoot.
            // On Android, touches are the only source, so this branch is skipped.
            if (!Application.isMobilePlatform)
            {
                Vector2 gui = new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y);
                if (Input.GetMouseButtonDown(0) && !IsInJoystickZone(gui) && !HitButton(gui).HasValue)
                {
                    _mouseAim = true;
                    _mouseAimStart = gui;
                    _mouseAimMoved = false;
                }
                if (_mouseAim)
                {
                    Vector2 delta = gui - _mouseAimStart;
                    // GUI y grows downward; AimInput/world y grows upward.
                    delta.y = -delta.y;
                    if (delta.magnitude >= AimDragThreshold)
                    {
                        AimInput = delta.normalized;
                        _mouseAimMoved = true;
                        if (!FireHeld) FirePressed = true;
                        FireHeld = true;
                    }
                    if (!Input.GetMouseButton(0))
                    {
                        _mouseAim = false;
                        _mouseAimMoved = false;
                        AimInput = Vector2.zero;
                    }
                }
            }
        }

        private void BootTouches()
        {
            if (Input.touchCount == 0)
            {
                // Release touch ownership without clobbering the desktop mouse path.
                // Mouse drag aim is sampled first in BootKeyboardAndMouse(); clearing
                // AimInput/FireHeld here would cancel every desktop shot.
                _joystickOwner = -1;
                _aimOwner = -1;
                _aimMoved = false;
                _aimFired = false;
                _grenadeCandidate = -1;
                for (int i = 0; i < _held.Length; i++)
                {
                    _owner[i] = -1;
                    _held[i] = false;
                }
                JoystickRaw = Vector2.zero;
                return;
            }

            UseTouchControls = true;
            for (int i = 0; i < _seen.Length; i++)
                _seen[i] = false;

            for (int i = 0; i < Input.touchCount; i++)
            {
                Touch touch = Input.GetTouch(i);
                int id = touch.fingerId;
                if (id >= 0 && id < _seen.Length)
                    _seen[id] = true;

                Vector2 guiPos = new Vector2(touch.position.x, Screen.height - touch.position.y);

                if (touch.phase == TouchPhase.Began)
                {
                    if (_joystickOwner == -1 && !IsButtonOwned(id) && IsInJoystickZone(guiPos))
                    {
                        _joystickOwner = id;
                        UpdateJoystick(touch);
                        continue;
                    }

                    if (!IsPointerOwned(id))
                    {
                        Btn? btn = HitButton(guiPos);
                        if (btn.HasValue)
                        {
                            if (btn.Value == Btn.Grenade)
                            {
                                // A second finger landing near the grenade button while
                                // the player is already aiming/firing used to throw a
                                // grenade immediately. Treat the button as a deliberate
                                // tap-hold: arm it now, throw only on a clean release.
                                if (_aimOwner >= 0 || FireHeld || _owner[(int)Btn.Grenade] >= 0)
                                {
                                    Debug.Log("[BREAKLINE] Grenade tap ignored: aim/fire already active.");
                                    continue;
                                }
                                _owner[(int)Btn.Grenade] = id;
                                _grenadeCandidate = id;
                                _grenadeCandidateStart = guiPos;
                                _grenadeCandidateAt = Time.unscaledTime;
                                continue;
                            }

                            _owner[(int)btn.Value] = id;
                            if (btn.Value == Btn.Jump) JumpPressed = true;
                            continue;
                        }
                    }

                    // Any free screen area is the manual aim/fire surface.
                    if (_aimOwner == -1 && !IsPointerOwned(id) && !IsInJoystickZone(guiPos))
                    {
                        _aimOwner = id;
                        _aimStart = guiPos;
                        _aimMoved = false;
                        _aimFired = false;
                    }
                }

                if (_joystickOwner == id)
                {
                    if (touch.phase == TouchPhase.Moved || touch.phase == TouchPhase.Stationary)
                        UpdateJoystick(touch);
                    if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
                    {
                        _joystickOwner = -1;
                        JoystickRaw = Vector2.zero;
                    }
                }

                if (_grenadeCandidate == id)
                {
                    Vector2 grenadeDelta = guiPos - _grenadeCandidateStart;
                    if (grenadeDelta.magnitude > GrenadeTapSlopPx)
                    {
                        Debug.Log("[BREAKLINE] Grenade tap cancelled: finger moved like a drag.");
                        _grenadeCandidate = -1;
                        _owner[(int)Btn.Grenade] = -1;
                    }
                    else if (Time.unscaledTime - _grenadeCandidateAt >= GrenadeMinTapSeconds)
                    {
                        // Deliberate short hold: throw once, while the finger is still
                        // down. A quick swipe over the button is cancelled above.
                        if (_aimOwner >= 0 || FireHeld)
                        {
                            Debug.Log("[BREAKLINE] Grenade tap ignored: aim/fire became active.");
                            _grenadeCandidate = -1;
                            _owner[(int)Btn.Grenade] = -1;
                        }
                        else
                        {
                            GrenadePressed = true;
                            _grenadeCandidate = -1;
                            Debug.Log($"[BREAKLINE] Grenade tap accepted id={id}.");
                        }
                    }
                }

                if (_owner[(int)Btn.Grenade] == id &&
                    (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled))
                {
                    _grenadeCandidate = -1;
                    _owner[(int)Btn.Grenade] = -1;
                }

                if (_aimOwner == id)
                {
                    if (touch.phase == TouchPhase.Moved || touch.phase == TouchPhase.Stationary)
                        UpdateAimDrag(guiPos);
                    if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
                    {
                        _aimOwner = -1;
                        _aimMoved = false;
                        AimInput = Vector2.zero;
                    }
                }

                for (int b = 0; b < _held.Length; b++)
                {
                    if (_owner[b] != id) continue;
                    if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
                        _owner[b] = -1;
                }
            }

            if (_joystickOwner >= 0 &&
                (_joystickOwner >= _seen.Length || !_seen[_joystickOwner]))
            {
                _joystickOwner = -1;
                JoystickRaw = Vector2.zero;
            }
            if (_aimOwner >= 0 &&
                (_aimOwner >= _seen.Length || !_seen[_aimOwner]))
            {
                _aimOwner = -1;
                _aimMoved = false;
                AimInput = Vector2.zero;
            }

            for (int b = 0; b < _held.Length; b++)
            {
                int owner = _owner[b];
                if (owner >= 0 && (owner >= _seen.Length || !_seen[owner]))
                    _owner[b] = -1;
                _held[b] = _owner[b] >= 0;
            }

            if (_grenadeCandidate >= 0 &&
                (_grenadeCandidate >= _seen.Length || !_seen[_grenadeCandidate]))
            {
                _grenadeCandidate = -1;
                _owner[(int)Btn.Grenade] = -1;
            }

            if (_held[(int)Btn.Jump]) JumpHeld = true;
            if (_held[(int)Btn.Grenade]) GrenadeHeld = true;

            float mag = JoystickRaw.magnitude;
            if (mag > 0.18f)
            {
                float remapped = MathUtil.RemapDeadzone(mag, 0.18f);
                Vector2 n = JoystickRaw / mag;
                Vector2 processed = n * remapped;
                MoveInput = new Vector2(processed.x, 0f);
                MoveVertical = processed.y;
            }

            if (_aimOwner >= 0 && _aimMoved)
            {
                FireHeld = true;
                if (!_aimFired)
                {
                    FirePressed = true;
                    _aimFired = true;
                }
            }
        }

        private void UpdateAimDrag(Vector2 guiPos)
        {
            Vector2 delta = guiPos - _aimStart;
            // Touch/GUI y grows downward; the shot direction must use world-up y.
            delta.y = -delta.y;
            if (delta.magnitude < AimDragThreshold)
                return;

            AimInput = delta.normalized;
            _aimMoved = true;
            if (!_aimFired)
            {
                FirePressed = true;
                _aimFired = true;
            }
            FireHeld = true;
        }

        private void UpdateJoystick(Touch touch)
        {
            Vector2 guiPos = new Vector2(touch.position.x, Screen.height - touch.position.y);
            Vector2 delta = guiPos - Layout.JoystickCenter;
            // JoystickRaw is also consumed as a world-up vector by MoveVertical.
            delta.y = -delta.y;
            float r = Layout.JoystickRadius;
            Vector2 clamped = Vector2.ClampMagnitude(delta, r);
            JoystickRaw = clamped / r;
        }

        private bool IsInJoystickZone(Vector2 guiPos)
        {
            return (guiPos - Layout.JoystickCenter).sqrMagnitude <=
                   Layout.JoystickHitRadius * Layout.JoystickHitRadius;
        }

        private Btn? HitButton(Vector2 guiPos)
        {
            float hitR = Layout.ButtonHitRadius;
            float jumpD = (guiPos - Layout.JumpCenter).sqrMagnitude;
            float nadeD = (guiPos - Layout.GrenadeCenter).sqrMagnitude;
            float best = hitR * hitR;
            Btn? result = null;
            if (jumpD <= best) { best = jumpD; result = Btn.Jump; }
            if (nadeD <= best) result = Btn.Grenade;
            return result;
        }

        private bool IsPointerOwned(int id)
        {
            if (_joystickOwner == id || _aimOwner == id) return true;
            for (int i = 0; i < _held.Length; i++)
                if (_owner[i] == id) return true;
            return false;
        }

        private bool IsButtonOwned(int id)
        {
            for (int i = 0; i < _held.Length; i++)
                if (_owner[i] == id) return true;
            return false;
        }

        private void ReleaseAllTouches()
        {
            _joystickOwner = -1;
            _aimOwner = -1;
            _aimMoved = false;
            _aimFired = false;
            _grenadeCandidate = -1;
            for (int i = 0; i < _held.Length; i++)
            {
                _owner[i] = -1;
                _held[i] = false;
            }
            JoystickRaw = Vector2.zero;
            AimInput = Vector2.zero;
        }
    }
}
