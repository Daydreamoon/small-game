using System.Collections.Generic;
using UnityEngine;

namespace Breakline
{
    public sealed class Hud : MonoBehaviour
    {
        private struct WorldWarning
        {
            public Vector2 Position;
            public string Text;
            public float Until;
        }

        private GameRoot _game;
        private GUIStyle _label;
        private GUIStyle _small;
        private GUIStyle _title;
        private GUIStyle _buttonLabel;
        private GUIStyle _debugLabel;
        private GUIStyle _debugSmall;
        private GUIStyle _debugButton;
        private GUIStyle _debugToggleStyle;
        private GUIStyle _warningStyle;
        private GUIStyle _meleeStyle;
        private GUIStyle _centerSubStyle;
        private int _styleW;
        private int _styleH;
        private readonly List<WorldWarning> _warnings = new List<WorldWarning>(12);
        private string _fpsLine = string.Empty;
        private string _subLine = string.Empty;
        private string _hintLine = string.Empty;
        private string _grenadeLine = "就绪";
        private string _perfLine = string.Empty;
        private float _nextHudTextRefresh;

        public bool DebugOpen { get; private set; }

        public void Init(GameRoot game)
        {
            _game = game;
        }

        private void OnGUI()
        {
            if (_game == null || _game.Viewport == null)
                return;

            EnsureStyles();
            HandleF1();

            if (Event.current.type == EventType.Repaint)
            {
                DrawFullScreen(new Color(0.035f, 0.045f, 0.06f, 1f));
                _game.Viewport.DrawWorld();
                DrawScreenFlash();
                DrawWorldWarnings();
                DrawTargetMarkers();
                DrawGameHud();
                if (!DebugOpen)
                {
                    DrawControls();
                    DrawAimWheel();
                }
                if (_game.Player != null && _game.Player.MeleeRingVisible && !DebugOpen)
                    DrawMeleeText();
                DrawBannerAndCompletion();
            }

            DrawDebugToggle();
            if (DebugOpen)
                DrawDebugPanel();
        }

        private void EnsureStyles()
        {
            if (_styleW == Screen.width && _styleH == Screen.height && _label != null)
                return;
            _styleW = Screen.width;
            _styleH = Screen.height;
            int basePx = Mathf.Max(12, Mathf.RoundToInt(Screen.height / 52f));
            int smallPx = Mathf.Max(10, Mathf.RoundToInt(Screen.height / 80f));
            int titlePx = Mathf.Max(18, Mathf.RoundToInt(Screen.height / 26f));
            Font uiFont = _game.Art != null ? _game.Art.UIFont : null;

            _label = new GUIStyle(GUI.skin.label)
            {
                fontSize = basePx,
                alignment = TextAnchor.MiddleLeft,
                wordWrap = false,
                normal = { textColor = Color.white }
            };
            _label.font = uiFont;
            _small = new GUIStyle(_label) { fontSize = smallPx };
            _title = new GUIStyle(_label)
            {
                fontSize = titlePx,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };
            _buttonLabel = new GUIStyle(_label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
                fontSize = Mathf.Max(12, Mathf.RoundToInt(_game.Input.Layout.ButtonRadius * 0.38f))
            };
            _debugLabel = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.Max(13, Mathf.RoundToInt(Screen.height / 55f)),
                alignment = TextAnchor.UpperLeft,
                wordWrap = true,
                normal = { textColor = new Color(0.86f, 0.95f, 1f) }
            };
            _debugLabel.font = uiFont;
            _debugSmall = new GUIStyle(_debugLabel)
            {
                fontSize = Mathf.Max(10, Mathf.RoundToInt(Screen.height / 82f)),
                normal = { textColor = new Color(0.7f, 0.8f, 0.86f) }
            };
            _debugButton = new GUIStyle(GUI.skin.button)
            {
                fontSize = Mathf.Max(12, Mathf.RoundToInt(Screen.height / 58f)),
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true,
                normal = { textColor = Color.white },
                hover = { textColor = Color.white },
                active = { textColor = Color.yellow }
            };
            _debugButton.font = uiFont;
            _debugToggleStyle = new GUIStyle(_debugButton)
            {
                fontSize = Mathf.Max(12, Mathf.RoundToInt(Screen.height / 58f)),
                wordWrap = false
            };
            _debugToggleStyle.font = uiFont;
            _warningStyle = new GUIStyle(_small)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(1f, 0.78f, 0.3f, 0.95f) }
            };
            _meleeStyle = new GUIStyle(_small)
            {
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(1f, 0.92f, 0.65f, 0.9f) }
            };
            _centerSubStyle = new GUIStyle(_small)
            {
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true
            };
            _title.normal.textColor = new Color(1f, 0.92f, 0.66f);
            _nextHudTextRefresh = 0f;
        }

        private void RefreshHudText()
        {
            if (Time.unscaledTime < _nextHudTextRefresh && _fpsLine.Length != 0)
                return;
            _nextHudTextRefresh = Time.unscaledTime + 0.25f;
            _fpsLine = $"帧率 {_game.Perf.FpsNow:0.0}  碎片 {_game.Debris.ActiveVisualCount}  地形 {_game.Terrain.ActiveFallingChunks}";
            _subLine = $"分辨率 {_game.Viewport.RtWidth}x{_game.Viewport.RtHeight} x{_game.Viewport.Scale}   目标 {TargetName()}";
            _hintLine = _game.Level != null ? _game.Level.GetHint() : string.Empty;
            _grenadeLine = _game.Player.GrenadeCooldownRemaining > 0f
                ? $"{_game.Player.GrenadeCooldownRemaining:0.0}秒"
                : "就绪";

            if (_game.Perf.IsRecording)
                _perfLine = $"性能 {ScenarioName(_game.Perf.Scenario)}  {_game.Perf.RecordingRemaining:0.0}秒";
            else if (_game.Perf.LastSummary != "no sample")
                _perfLine = $"上次结果 {PerfSummary()}";
            else
                _perfLine = string.Empty;
        }

        private void HandleF1()
        {
            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.F1)
            {
                DebugOpen = !DebugOpen;
                Event.current.Use();
            }
        }

        private void DrawGameHud()
        {
            if (_game.Player == null) return;
            RefreshHudText();

            float h = Screen.height;
            float w = Screen.width;
            float pad = Mathf.Max(10f, h * 0.025f);
            float fontSize = Mathf.Max(12, h / 58f);

            // Health pips.
            _label.fontSize = Mathf.RoundToInt(fontSize);
            GUI.color = Color.white;
            GUI.Label(new Rect(pad, pad * 0.8f, w * 0.2f, fontSize * 1.4f), "生命", _small);
            int pips = Mathf.Max(1, _game.Config.playerHP);
            float pipSize = Mathf.Max(10f, h * 0.018f);
            for (int i = 0; i < pips; i++)
            {
                Rect r = new Rect(pad + 2f * fontSize + i * (pipSize + 4f), pad * 0.8f + fontSize * 0.25f, pipSize, pipSize * 0.8f);
                DrawRect(r, i < _game.Player.Health ? new Color(0.95f, 0.34f, 0.28f) : new Color(0.15f, 0.17f, 0.2f, 0.8f));
            }

            // Small live stats (refreshed at 4 Hz to keep the HUD out of the GC budget).
            GUI.Label(new Rect(pad, pad + pipSize * 1.25f, w * 0.56f, fontSize), _fpsLine, _small);
            GUI.Label(new Rect(pad, pad + pipSize * 1.25f + fontSize * 1.25f, w * 0.72f, fontSize), _subLine, _small);

            // Hint / objective.
            if (!string.IsNullOrEmpty(_hintLine))
            {
                Rect hintRect = new Rect(w * 0.20f, pad * 0.7f, w * 0.60f, fontSize * 1.7f);
                DrawRect(new Rect(hintRect.x - 8f, hintRect.y, hintRect.width + 16f, hintRect.height), new Color(0f, 0f, 0f, 0.35f));
                _title.fontSize = Mathf.RoundToInt(fontSize * 1.05f);
                _title.alignment = TextAnchor.MiddleCenter;
                GUI.Label(hintRect, _hintLine, _title);
            }

            // Grenade state near its control.
            if (_game.Input.Layout.ButtonRadius > 0f)
            {
                Vector2 c = _game.Input.Layout.GrenadeCenter;
                GUI.Label(new Rect(c.x - 70f, c.y + _game.Input.Layout.ButtonRadius * 0.72f, 140f, fontSize * 1.4f), _grenadeLine, _title);
            }

            // Performance capture status.
            if (!string.IsNullOrEmpty(_perfLine))
            {
                GUI.color = _game.Perf.IsRecording ? new Color(0.65f, 1f, 0.7f) : new Color(0.7f, 0.85f, 0.95f, 0.8f);
                GUI.Label(new Rect(w * 0.30f, pad + pipSize * 2.8f, w * 0.6f, fontSize * 1.4f), _perfLine, _small);
                GUI.color = Color.white;
            }

            if (!_game.Player.IsAlive)
            {
                DrawCenterPanel("重生中", "稍后会回到最近的检查点。", new Color(0.55f, 0.12f, 0.10f, 0.72f));
            }
        }

        private string TargetName()
        {
            Enemy e = _game.Player != null ? _game.Player.CurrentTarget : null;
            if (e == null) return "手动";
            if (e.Kind == EnemyKind.ArmoredCrush) return "装甲目标";
            if (e.Kind == EnemyKind.Patrol) return "巡逻兵";
            return "木靶";
        }

        private static string ScenarioName(string scenario)
        {
            if (scenario == "stress") return "压力测试";
            if (scenario == "full") return "全刚体";
            if (scenario == "crush") return "压敌";
            if (scenario == "p6") return "P6";
            return string.IsNullOrEmpty(scenario) ? "性能" : scenario;
        }

        private string PerfSummary()
        {
            string s = _game.Perf.LastSummary;
            if (string.IsNullOrEmpty(s) || s == "no sample") return "无采样";
            if (s == "sample too short") return "采样太短";
            return s;
        }

        private void DrawTargetMarkers()
        {
            if (_game.Player == null) return;
            Enemy target = _game.Player.CurrentTarget;
            if (target != null && target.IsAlive)
                DrawBracketAt(target.Center + Vector2.up * 0.30f, new Color(1f, 0.84f, 0.3f, 0.9f), 0.18f);

            Enemy armored = _game.Enemies != null ? _game.Enemies.ArmoredTarget : null;
            if (armored != null && armored.IsAlive && _game.Viewport.IsWorldVisible(armored.Center, 0.05f))
            {
                DrawBracketAt(armored.Center + Vector2.up * 0.55f, new Color(1f, 0.32f, 0.22f, 0.8f), 0.30f);
                Vector2 g = _game.Viewport.WorldToGui(armored.Center + Vector2.up * 0.9f);
                GUI.Label(new Rect(g.x - 90f, g.y - 20f, 180f, 40f), "仅可压死", _small);
            }
        }

        private void DrawBracketAt(Vector2 worldPos, Color color, float sizePx01)
        {
            Vector2 p = _game.Viewport.WorldToGui(worldPos);
            float s = Mathf.Max(8f, Screen.height * sizePx01 * 0.18f);
            float t = Mathf.Max(2f, Screen.height * 0.003f);
            GUI.color = color;
            DrawRect(new Rect(p.x - s, p.y - s, s, t), color);
            DrawRect(new Rect(p.x - s, p.y - s, t, s), color);
            DrawRect(new Rect(p.x + s - t, p.y - s, t, s), color);
            DrawRect(new Rect(p.x + s - t, p.y + s - t, t, s), color);
            DrawRect(new Rect(p.x - s, p.y + s - t, s, t), color);
            DrawRect(new Rect(p.x - s, p.y + s - t, t, s), color);
            DrawRect(new Rect(p.x + s - t, p.y + s - t, t, s), color);
            GUI.color = Color.white;
        }

        private void DrawControls()
        {
            ControlLayout layout = _game.Input.Layout;
            if (layout.ButtonRadius <= 1f) return;
            if (_game.Player != null && !_game.Player.IsAlive) return;
            if (_game.Level != null && _game.Level.Completed) return;

            // Joystick base + knob.
            Vector2 jc = layout.JoystickCenter;
            float jr = layout.JoystickRadius;
            GUI.color = new Color(1f, 1f, 1f, 0.17f);
            GUI.DrawTexture(new Rect(jc.x - jr, jc.y - jr, jr * 2f, jr * 2f), _game.Art.UIRing);
            GUI.color = new Color(1f, 1f, 1f, 0.07f);
            GUI.DrawTexture(new Rect(jc.x - jr, jc.y - jr, jr * 2f, jr * 2f), _game.Art.UICircle);
            Vector2 raw = _game.Input.JoystickRaw;
            Vector2 kr = jc + new Vector2(raw.x, -raw.y) * jr;
            float knob = layout.KnobRadius;
            GUI.color = new Color(0.80f, 0.95f, 1f, 0.42f);
            GUI.DrawTexture(new Rect(kr.x - knob, kr.y - knob, knob * 2f, knob * 2f), _game.Art.UICircle);
            GUI.color = new Color(0.95f, 0.98f, 1f, 0.55f);
            GUI.Label(new Rect(jc.x - jr, jc.y + jr + 2f, jr * 2f, 38f), "移动", _small);

            DrawTouchButton(layout.JumpCenter, layout.ButtonRadius, "跳跃", _game.Input.JumpHeld, new Color(0.25f, 0.72f, 0.80f, 0.35f));
            DrawTouchButton(layout.GrenadeCenter, layout.ButtonRadius, "手雷", _game.Input.GrenadeHeld, new Color(0.95f, 0.65f, 0.24f, 0.36f));

            // The old fixed FIRE button is gone: the player drags anywhere on the screen
            // to aim and shoot. Keep a persistent hint above the action buttons.
            GUI.Label(new Rect(Screen.width * 0.39f, Screen.height - 62f, Screen.width * 0.34f, 52f),
                "拖动屏幕瞄准射击", _small);
        }

        private void DrawAimWheel()
        {
            InputRouter input = _game.Input;
            if (input == null || !input.AimWheelVisible) return;
            if (_game.Player != null && !_game.Player.IsAlive) return;
            if (_game.Level != null && _game.Level.Completed) return;

            Vector2 center = input.AimWheelOrigin;
            float radius = Mathf.Clamp(Screen.height * 0.115f, 58f, 132f);
            float knob = Mathf.Clamp(radius * 0.16f, 8f, 21f);

            // Translucent wheel at the finger-down point.
            GUI.color = new Color(0.75f, 0.92f, 1f, 0.30f);
            GUI.DrawTexture(new Rect(center.x - radius, center.y - radius,
                radius * 2f, radius * 2f), _game.Art.UIRing);
            GUI.color = new Color(0.05f, 0.12f, 0.18f, 0.18f);
            GUI.DrawTexture(new Rect(center.x - radius, center.y - radius,
                radius * 2f, radius * 2f), _game.Art.UICircle);

            // Eight faint direction ticks make the wheel readable on any
            // background; they are visual only and do not add aiming assist.
            for (int i = 0; i < 8; i++)
            {
                float a = i * Mathf.PI * 0.25f;
                Vector2 p = center + new Vector2(Mathf.Cos(a), -Mathf.Sin(a)) * (radius * 0.82f);
                GUI.color = new Color(0.9f, 0.98f, 1f, 0.35f);
                GUI.DrawTexture(new Rect(p.x - 2f, p.y - 2f, 4f, 4f), _game.Art.UICircle);
            }

            Vector2 dir = input.AimInput;
            if (dir.sqrMagnitude > 0.01f)
            {
                // AimInput is world-up (+Y), GUI is screen-down (+Y): flip only
                // for the visual knob. The shot itself uses AimInput unchanged.
                Vector2 visualDir = new Vector2(dir.x, -dir.y);
                Vector2 knobPos = center + visualDir * (radius - knob - 4f);
                GUI.color = new Color(0.35f, 0.85f, 1f, 0.72f);
                GUI.DrawTexture(new Rect(knobPos.x - knob, knobPos.y - knob,
                    knob * 2f, knob * 2f), _game.Art.UICircle);
                GUI.color = new Color(1f, 1f, 1f, 0.55f);
                GUI.DrawTexture(new Rect(knobPos.x - knob, knobPos.y - knob,
                    knob * 2f, knob * 2f), _game.Art.UIRing);
            }
            else
            {
                GUI.color = new Color(0.85f, 0.95f, 1f, 0.50f);
                GUI.DrawTexture(new Rect(center.x - knob, center.y - knob,
                    knob * 2f, knob * 2f), _game.Art.UIRing);
            }

            GUI.color = new Color(0.9f, 0.97f, 1f, 0.82f);
            GUI.Label(new Rect(center.x - radius, center.y + radius + 4f,
                radius * 2f, 34f), "滑动瞄准 · 轮盘方向即射击方向", _small);
            GUI.color = Color.white;
        }

        private void DrawTouchButton(Vector2 center, float radius, string text, bool held, Color color)
        {
            Color c = color;
            c.a = held ? Mathf.Min(0.8f, c.a + 0.25f) : c.a;
            GUI.color = new Color(0f, 0f, 0f, 0.35f);
            GUI.DrawTexture(new Rect(center.x - radius - 3f, center.y - radius - 3f, radius * 2f + 6f, radius * 2f + 6f), _game.Art.UICircle);
            GUI.color = c;
            GUI.DrawTexture(new Rect(center.x - radius, center.y - radius, radius * 2f, radius * 2f), _game.Art.UICircle);
            GUI.color = new Color(1f, 1f, 1f, 0.85f);
            GUI.Label(new Rect(center.x - radius, center.y - radius * 0.45f, radius * 2f, radius * 0.9f), text, _buttonLabel);
            GUI.color = Color.white;
        }

        private void DrawMeleeText()
        {
            PlayerController p = _game.Player;
            if (p == null) return;
            Vector2 screen = _game.Viewport.WorldToGui(p.Position + Vector2.up * 1.15f);
            GUI.Label(new Rect(screen.x - 100f, screen.y - 20f, 200f, 30f), "匕首范围", _meleeStyle);
        }

        private void DrawBannerAndCompletion()
        {
            if (_game.Level == null) return;
            string banner = _game.Level.ActiveBanner;
            if (!string.IsNullOrEmpty(banner))
            {
                float w = Screen.width;
                float h = Screen.height;
                _title.fontSize = Mathf.Max(20, Mathf.RoundToInt(h / 30f));
                Rect r = new Rect(w * 0.15f, h * 0.16f, w * 0.70f, h * 0.09f);
                DrawRect(new Rect(r.x - 14f, r.y - 6f, r.width + 28f, r.height + 12f), new Color(0f, 0f, 0f, 0.45f));
                GUI.Label(r, banner, _title);
            }

            if (_game.Level.Completed)
                DrawCenterPanel("M1 路线完成", "移动、拖动射击、炸穿地板、链爆与平台压敌已跑通。", new Color(0.10f, 0.30f, 0.22f, 0.86f));
        }

        private void DrawCenterPanel(string title, string subtitle, Color panelColor)
        {
            float w = Screen.width;
            float h = Screen.height;
            Rect r = new Rect(w * 0.18f, h * 0.32f, w * 0.64f, h * 0.30f);
            DrawRect(r, panelColor);
            _title.fontSize = Mathf.Max(22, Mathf.RoundToInt(h / 26f));
            GUI.Label(new Rect(r.x, r.y + r.height * 0.18f, r.width, r.height * 0.25f), title, _title);
            GUI.Label(new Rect(r.x + r.width * 0.1f, r.y + r.height * 0.47f, r.width * 0.8f, r.height * 0.28f), subtitle, _centerSubStyle);
        }

        private void DrawDebugToggle()
        {
            float safeTop = _game.Input.Layout.SafeGui.y;
            float safeRight = _game.Input.Layout.SafeGui.xMax;
            Rect r = new Rect(safeRight - 74f, safeTop + 8f, 66f, 42f);
            GUI.backgroundColor = DebugOpen ? new Color(0.9f, 0.45f, 0.3f) : new Color(0.25f, 0.35f, 0.4f);
            if (GUI.Button(r, DebugOpen ? "关闭" : "调试", _debugToggleStyle))
                DebugOpen = !DebugOpen;
            GUI.backgroundColor = Color.white;
        }

        private void DrawDebugPanel()
        {
            float w = Screen.width;
            float h = Screen.height;
            Rect panel = new Rect(w * 0.04f, h * 0.055f, w * 0.92f, h * 0.89f);
            float x = panel.x + 24f;
            float labelY = panel.y + 16f;
            float metricsY = labelY + 38f;
            float graphY = metricsY + 112f;
            float buttonsY = graphY + 86f;

            if (Event.current.type == EventType.Repaint)
            {
                DrawFullScreen(new Color(0.01f, 0.02f, 0.03f, 0.74f));
                DrawRect(panel, new Color(0.055f, 0.075f, 0.09f, 0.97f));

                _debugLabel.fontSize = Mathf.Max(13, Mathf.RoundToInt(h / 52f));
                _debugSmall.fontSize = Mathf.Max(10, Mathf.RoundToInt(h / 82f));

                GUI.Label(new Rect(x, labelY, panel.width - 48f, 34f), "BREAKLINE / 关卡调试与性能", _title);

                string metrics =
                    $"关卡 {_game.Level.LevelNumber}/{M1Level.LevelCount} {_game.Level.LevelName}   " +
                    $"帧率 {_game.Perf.FpsNow:0.0}   分辨率 {_game.Viewport.RtWidth}x{_game.Viewport.RtHeight} x{_game.Viewport.Scale}   " +
                    $"碎片 {_game.Debris.ActiveVisualCount}  (T2 {_game.Debris.T2Count} / T1 {_game.Debris.T1Count} / 压力 {_game.Debris.StressBodyCount})   " +
                    $"地形 {_game.Terrain.ActiveFallingChunks}   链爆 {_game.Props.ChainCount}\n" +
                    $"玩家 {_game.Player.Position.x:0.0},{_game.Player.Position.y:0.0}   生命 {_game.Player.Health}/{_game.Config.playerHP}   " +
                    $"下压 {(_game.Player.IsSlamming ? "是" : "否")}   落地 {(_game.Player.Grounded ? "是" : "否")}   目标 {TargetName()}\n" +
                    $"性能 {(_game.Perf.IsRecording ? "记录中 " + ScenarioName(_game.Perf.Scenario) + " " + _game.Perf.RecordingRemaining.ToString("0.0") + "秒" : PerfSummary())}\n" +
                    $"CSV {_game.Perf.CsvPath}";
                GUI.Label(new Rect(x, metricsY, panel.width - 48f, 110f), metrics, _debugLabel);

                DrawPerfGraph(new Rect(x, graphY, panel.width - 48f, 78f));

                GUI.Label(new Rect(x, panel.yMax - 52f, panel.width - 48f, 38f),
                    "调试面板仅开发使用；正式帧率以目标机 Release + 游戏内 CSV 为准。", _debugSmall);
            }

            // IMGUI click handling must be called on every event type, not only Repaint.
            float colW = (panel.width - 60f) * 0.5f;
            float bh = Mathf.Max(46f, h * 0.061f);
            float gap = 8f;
            int row = 0;
            Button(new Rect(x, buttonsY + row * (bh + gap), colW, bh), "压力测试 200+12+链爆", () =>
            {
                DebugOpen = false;
                _game.DebugStartStress();
            });
            Button(new Rect(x + colW + gap, buttonsY + row * (bh + gap), colW, bh), "200 全刚体", () =>
            {
                DebugOpen = false;
                _game.DebugStartFullBody200();
            });
            row++;
            Button(new Rect(x, buttonsY + row * (bh + gap), colW, bh), "在玩家旁链爆", () =>
            {
                DebugOpen = false;
                _game.DebugChainAtPlayer();
            });
            Button(new Rect(x + colW + gap, buttonsY + row * (bh + gap), colW, bh), "停止并写入 CSV", () =>
            {
                _game.Perf.Flush("manual-stop");
            });
            row++;
            Button(new Rect(x, buttonsY + row * (bh + gap), colW, bh), "传送到 B 隧道", () =>
            {
                DebugOpen = false;
                _game.Player.DebugTeleport(new Vector2(33.5f, 7.7f));
            });
            Button(new Rect(x + colW + gap, buttonsY + row * (bh + gap), colW, bh), "传送到压敌区", () =>
            {
                DebugOpen = false;
                _game.Player.DebugTeleport(new Vector2(51.5f, 7.7f));
            });
            row++;
            Button(new Rect(x, buttonsY + row * (bh + gap), colW, bh), "慢镜：Boss", () =>
            {
                DebugOpen = false;
                _game.Feedback.RequestSlowMo(SlowMoReason.BossDeath);
            });
            Button(new Rect(x + colW + gap, buttonsY + row * (bh + gap), colW, bh), "慢镜：通关", () =>
            {
                DebugOpen = false;
                _game.Feedback.RequestSlowMo(SlowMoReason.LevelClear);
            });
            row++;
            Button(new Rect(x, buttonsY + row * (bh + gap), colW, bh), "无敌 10 分钟", () =>
            {
                _game.Player.SetInvincibleFor(600f);
            });
            Button(new Rect(x + colW + gap, buttonsY + row * (bh + gap), colW, bh), "重启 M1", () =>
            {
                DebugOpen = false;
                _game.DebugRestart();
            });
            row++;
            Button(new Rect(x, buttonsY + row * (bh + gap), colW, bh), "关闭面板", () => DebugOpen = false);
            Button(new Rect(x + colW + gap, buttonsY + row * (bh + gap), colW, bh),
                _game.Viewport.FlipPresentation ? "画面垂直翻转：开" : "画面垂直翻转：关", () =>
                {
                    _game.Viewport.FlipPresentation = !_game.Viewport.FlipPresentation;
                    Debug.Log($"[BREAKLINE] World presentation flip = {_game.Viewport.FlipPresentation}");
                });
            row++;

            float colW3 = (panel.width - 60f - gap * 2f) / 3f;
            for (int i = 0; i < M1Level.LevelCount; i++)
            {
                int levelIndex = i;
                Button(new Rect(x + i * (colW3 + gap), buttonsY + row * (bh + gap), colW3, bh),
                    $"关卡 {i + 1}", () =>
                    {
                        DebugOpen = false;
                        _game.LoadLevel(levelIndex);
                    });
            }
        }

        private void DrawPerfGraph(Rect rect)
        {
            DrawRect(rect, new Color(0f, 0f, 0f, 0.35f));
            int count = Mathf.Min(_game.Perf.GraphCount, PerfLogger.GraphCapacity);
            if (count < 2) return;
            float barW = Mathf.Max(1f, rect.width / PerfLogger.GraphCapacity);
            for (int i = 0; i < count; i++)
            {
                int idx = (_game.Perf.GraphHead - count + i + PerfLogger.GraphCapacity) % PerfLogger.GraphCapacity;
                float ms = Mathf.Clamp(_game.Perf.GraphMs[idx], 0f, 33.3f);
                float bh = Mathf.Clamp01(ms / 33.3f) * rect.height;
                Color c = ms <= 16.7f ? new Color(0.3f, 0.9f, 0.55f, 0.9f) : (ms <= 25f ? new Color(1f, 0.75f, 0.25f, 0.9f) : new Color(1f, 0.28f, 0.22f, 0.9f));
                DrawRect(new Rect(rect.x + i * barW, rect.yMax - bh, Mathf.Max(1f, barW - 0.5f), bh), c);
            }
            GUI.Label(new Rect(rect.x + 4f, rect.y + 2f, rect.width - 8f, 20f), "帧耗时 0–33.3ms（左旧 → 右新）", _debugSmall);
        }

        private void Button(Rect rect, string text, System.Action action)
        {
            if (GUI.Button(rect, text, _debugButton))
                action?.Invoke();
        }

        private void DrawWorldWarnings()
        {
            float now = Time.unscaledTime;
            for (int i = _warnings.Count - 1; i >= 0; i--)
            {
                if (_warnings[i].Until < now)
                {
                    _warnings.RemoveAt(i);
                    continue;
                }
                Vector2 p = _game.Viewport.WorldToGui(_warnings[i].Position);
                GUI.Label(new Rect(p.x - 100f, p.y - 30f, 200f, 34f), _warnings[i].Text, _warningStyle);
            }
        }

        public void ShowWorldWarning(Vector2 worldPosition, string text)
        {
            for (int i = 0; i < _warnings.Count; i++)
            {
                if ((_warnings[i].Position - worldPosition).sqrMagnitude < 0.05f && _warnings[i].Text == text)
                {
                    WorldWarning w = _warnings[i];
                    w.Until = Time.unscaledTime + 1.4f;
                    _warnings[i] = w;
                    return;
                }
            }
            if (_warnings.Count >= 12)
                _warnings.RemoveAt(0);
            _warnings.Add(new WorldWarning
            {
                Position = worldPosition,
                Text = text,
                Until = Time.unscaledTime + 1.4f
            });
        }

        private void DrawScreenFlash()
        {
            if (_game.Vfx.TryGetScreenFlash(out Color flash))
                DrawFullScreen(flash);
        }

        private void DrawFullScreen(Color c)
        {
            GUI.color = c;
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), _game.Art.White);
            GUI.color = Color.white;
        }

        private void DrawRect(Rect r, Color c)
        {
            GUI.color = c;
            GUI.DrawTexture(r, _game.Art.White);
            GUI.color = Color.white;
        }
    }
}
