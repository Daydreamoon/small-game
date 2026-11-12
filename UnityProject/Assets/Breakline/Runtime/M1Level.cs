using System.Collections.Generic;
using UnityEngine;

namespace Breakline
{
    /// <summary>
    /// Three original greybox levels, selected by <see cref="Selected"/> and rebuilt through
    /// a scene reload. All levels share the same terrain dimensions and goal/gate rules so the
    /// existing P4/P6 automation remains deterministic when no debug level is selected.
    ///
    /// Level 1 (Training Yard) is the original M1 setpiece.
    /// Level 2 (Maintenance Trench) forces a long blast-through-floor tunnel and a two-support
    /// platform crush.
    /// Level 3 (Rooftop Chase) uses staggered roofs and a final billboard collapse.
    /// </summary>
    public sealed class M1Level
    {
        public const int MapWidth = 96;
        public const int MapHeight = 24;
        public const int SurfaceTop = 7;
        public const int LevelCount = 3;

        public static int Selected = 0;

        private static readonly string[] LevelNames = { "训练场", "维修隧道", "屋顶追逐" };

        private readonly GameRoot _game;
        private readonly List<Vector2Int> _gateCells = new List<Vector2Int>();
        private readonly List<Enemy> _dummies = new List<Enemy>();
        private Enemy _armored;
        private GameObject _gateGlow;
        private bool _checkpoint1;
        private bool _checkpoint2;
        private float _bannerTimer;
        private float _advanceTimer;
        private float _checkpoint1X;
        private float _checkpoint2X;
        private Vector2 _checkpoint1Point;
        private Vector2 _checkpoint2Point;

        public Vector2 PlayerSpawn { get; private set; }
        public bool GateOpen { get; private set; }
        public bool Completed { get; private set; }
        public string Banner { get; private set; } = string.Empty;
        public bool ArmoredAlive => _armored != null && _armored.Alive;
        public string LevelName { get; private set; } = LevelNames[0];
        public int LevelNumber => Mathf.Clamp(Selected, 0, LevelCount - 1) + 1;

        public M1Level(GameRoot game)
        {
            _game = game;
        }

        public void Build()
        {
            TerrainSystem t = _game.Terrain;
            Selected = Mathf.Clamp(Selected, 0, LevelCount - 1);
            LevelName = LevelNames[Selected];

            _gateCells.Clear();
            _dummies.Clear();
            _armored = null;
            _gateGlow = null;
            _checkpoint1 = false;
            _checkpoint2 = false;
            _bannerTimer = 0f;
            _advanceTimer = 0f;
            Completed = false;
            GateOpen = false;
            PlayerSpawn = new Vector2(5.5f, SurfaceTop + _game.Config.playerHeight * 0.5f + 0.02f);

            BuildCommonBase(t);
            switch (Selected)
            {
                case 0:
                    BuildTrainingYard(t);
                    break;
                case 1:
                    BuildMaintenanceTrench(t);
                    break;
                default:
                    BuildRooftopChase(t);
                    break;
            }

            BuildGate(t);
            t.RebuildAllImmediate();
            t.MarkSupportDirty();

            BuildBackdrop();
            BuildGoal();
            SpawnEnemies();
            SpawnProps();

            ShowBanner($"第 {LevelNumber} 关 — {LevelName}", 1.8f);
            Debug.Log($"[BREAKLINE] Level {LevelNumber}/{LevelCount} built: {MapWidth}x{MapHeight}, " +
                      $"name={LevelName}, gate={_gateCells.Count}, spawn={PlayerSpawn}");
        }

        public void Tick(float dt)
        {
            if (_bannerTimer > 0f)
                _bannerTimer -= dt;

            PlayerController p = _game.Player;
            if (p == null) return;

            if (!_checkpoint1 && p.Position.x > _checkpoint1X)
            {
                _checkpoint1 = true;
                p.SetRespawnPoint(_checkpoint1Point);
                ShowBanner("检查点", 1.4f);
            }
            if (!_checkpoint2 && p.Position.x > _checkpoint2X)
            {
                _checkpoint2 = true;
                p.SetRespawnPoint(_checkpoint2Point);
                ShowBanner("检查点", 1.4f);
            }

            if (!Completed && GateOpen && p.Position.x > 88.0f)
                Complete();

            if (Completed && _advanceTimer > 0f)
            {
                _advanceTimer -= dt;
                if (_advanceTimer <= 0f && !_game.IsAutomation)
                    _game.LoadLevel(Selected + 1);
                return;
            }

            if (!GateOpen && _armored != null && _armored.Alive && p.Position.x > 78f && _bannerTimer <= 0f)
                ShowBanner("闸门锁定 — 压死装甲目标", 1.0f);
        }

        public string GetHint()
        {
            if (Completed)
                return Selected >= LevelCount - 1 ? "全部关卡完成 — 感谢试玩" : "关卡完成 — 正在进入下一关";
            if (GateOpen) return "闸门已开 — 向右前进";
            PlayerController p = _game.Player;
            if (p == null) return string.Empty;

            float x = p.Position.x;
            if (ArmoredAlive && x >= 48f && x < 84f)
                return "打爆油桶链 — 压死装甲目标";

            if (Selected == 0)
            {
                if (x < 10f) return "拖动屏幕瞄准射击";
                if (x < 29f) return "靶场";
                if (x < 34f) return "跳过矮墙";
                if (x < 40f) return "薄地板 — 向下炸穿";
                if (x < 47f) return "爬上出口台阶";
                return "向右前进";
            }

            if (Selected == 1)
            {
                if (x < 10f) return "拖动屏幕瞄准射击";
                if (x < 29f) return "维修区";
                if (x < 34f) return "薄地板 — 向下炸穿";
                if (x < 48f) return "穿越维修隧道";
                if (x < 52f) return "爬上出口台阶";
                return "向右前进";
            }

            if (x < 10f) return "拖动屏幕瞄准射击";
            if (x < 21f) return "跳上第一座屋顶";
            if (x < 35f) return "跨越屋顶缺口";
            if (x < 47f) return "继续向右";
            if (x < 57f) return "跳下屋顶";
            return "向右前进";
        }

        public void OnCrushTargetKilled()
        {
            if (GateOpen) return;
            GateOpen = true;
            _game.Terrain.OpenGate(_gateCells);
            if (_gateGlow != null) _gateGlow.SetActive(false);
            _game.Audio.Play(AudioCue.Gate, 0.8f);
            _game.Vfx.SpawnRing(new Vector2(84.5f, 9.5f), 4f, new Color(0.45f, 1f, 0.62f, 0.55f), 0.5f, true);
            _game.Hud.ShowWorldWarning(new Vector2(84.5f, 10f), "开");
            ShowBanner("闸门已开 — 向右前进", 2.0f);
            Debug.Log($"[BREAKLINE] Level {LevelNumber} crush target killed; gate open.");
        }

        private void Complete()
        {
            Completed = true;
            if (_game.Player != null) _game.Player.ControlsEnabled = false;
            _game.Audio.Play(AudioCue.Gate, 0.8f);
            _game.Perf.FlushScenarioIfRecording("route-complete");
            Debug.Log($"[BREAKLINE] Level {LevelNumber} route complete, automation={_game.IsAutomation}");

            if (_game.IsAutomation)
            {
                ShowBanner("路线完成 — 自动测试", 999f);
                return;
            }

            if (Selected < LevelCount - 1)
            {
                ShowBanner($"第 {LevelNumber} 关完成 — 即将进入第 {LevelNumber + 1} 关", 999f);
                _advanceTimer = 2.2f;
            }
            else
            {
                ShowBanner("全部 3 关完成 — 感谢试玩", 999f);
            }
        }

        public void ShowBanner(string text, float seconds)
        {
            Banner = text;
            _bannerTimer = seconds;
        }

        public string ActiveBanner => _bannerTimer > 0f ? Banner : string.Empty;

        public int DummyAliveCount
        {
            get
            {
                int c = 0;
                for (int i = 0; i < _dummies.Count; i++)
                    if (_dummies[i].Alive) c++;
                return c;
            }
        }

        private void BuildCommonBase(TerrainSystem t)
        {
            // Safe base and side walls; the bottom row is an indestructible support anchor.
            t.Fill(0, 0, MapWidth, 2, TerrainSystem.Indestructible);
            t.Fill(0, 2, MapWidth, 4, TerrainSystem.Concrete);
            t.Fill(0, 6, MapWidth, 1, TerrainSystem.Dirt);
            t.Fill(0, 0, 2, MapHeight, TerrainSystem.Indestructible);
            t.Fill(MapWidth - 2, 0, 2, MapHeight, TerrainSystem.Indestructible);

            _checkpoint1X = 28f;
            _checkpoint2X = 47.5f;
            _checkpoint1Point = SurfacePoint(28.5f);
            _checkpoint2Point = SurfacePoint(48.5f);
        }

        private Vector2 SurfacePoint(float x)
        {
            return new Vector2(x, SurfaceTop + _game.Config.playerHeight * 0.5f + 0.02f);
        }

        private void BuildTrainingYard(TerrainSystem t)
        {
            // A-zone target platforms are hung from the background; mark anchors so the
            // support solver does not drop them on the first frame.
            t.Fill(11, 9, 4, 1, TerrainSystem.Concrete);
            t.AnchorRect(11, 9, 4, 1);
            t.Fill(11, 12, 4, 1, TerrainSystem.Concrete);
            t.AnchorRect(11, 12, 4, 1);
            t.Fill(19, 10, 5, 1, TerrainSystem.Concrete);
            t.AnchorRect(19, 10, 5, 1);
            t.Fill(26, 12, 4, 1, TerrainSystem.Concrete);
            t.AnchorRect(26, 12, 4, 1);

            t.Fill(30, 7, 3, 2, TerrainSystem.Concrete);
            CarveTunnel(t, 33, 40, 47, 44, 46);
            BuildCrushArena(t, 55, 10, 56, 59, 61.5f);

            _checkpoint1X = 28f;
            _checkpoint2X = 47.5f;
            _checkpoint1Point = SurfacePoint(28.5f);
            _checkpoint2Point = SurfacePoint(48.5f);
        }

        private void BuildMaintenanceTrench(TerrainSystem t)
        {
            // A short elevated maintenance gantry gives the level a different silhouette.
            t.Fill(8, 9, 5, 1, TerrainSystem.Concrete);
            t.AnchorRect(8, 9, 5, 1);
            t.Fill(16, 12, 4, 1, TerrainSystem.Concrete);
            t.AnchorRect(16, 12, 4, 1);

            // Low wall before the trench.
            t.Fill(22, 7, 3, 2, TerrainSystem.Concrete);

            // Long trench: the surface is a thin dirt floor; an indestructible rib at x=37
            // forces the player underground instead of walking over the top.
            t.ClearRect(30, 2, 23, 4);
            t.Fill(30, 6, 22, 1, TerrainSystem.Dirt);
            t.Fill(37, 7, 1, MapHeight - 7, TerrainSystem.Indestructible);
            t.ClearRect(48, 6, 3, 1);
            t.Fill(47, 2, 1, 2, TerrainSystem.Concrete);
            t.Fill(48, 2, 1, 3, TerrainSystem.Concrete);
            t.Fill(49, 2, 1, 4, TerrainSystem.Concrete);
            t.Fill(50, 2, 1, 5, TerrainSystem.Concrete);

            BuildCrushArena(t, 58, 10, 59, 63, 61.0f);

            _checkpoint1X = 28f;
            _checkpoint2X = 52f;
            _checkpoint1Point = SurfacePoint(28.5f);
            _checkpoint2Point = SurfacePoint(52.5f);
        }

        private void BuildRooftopChase(TerrainSystem t)
        {
            // Staggered roofs. The top surfaces are 9, 10, 8 and 10 tiles high; every gap
            // is at most two tiles and the vertical changes stay within the jump arc.
            t.Fill(9, 6, 1, 2, TerrainSystem.Concrete);       // one-tile access step
            t.Fill(10, 6, 10, 3, TerrainSystem.Concrete);     // roof A, top 9
            t.Fill(22, 6, 10, 4, TerrainSystem.Concrete);     // roof B, top 10
            t.Fill(35, 6, 10, 2, TerrainSystem.Concrete);     // roof C, top 8
            t.Fill(47, 6, 10, 4, TerrainSystem.Concrete);     // roof D, top 10

            // A short suspended billboard bridge between roof A and B.
            t.Fill(31, 9, 4, 1, TerrainSystem.Concrete);
            t.AnchorRect(31, 9, 4, 1);

            BuildCrushArena(t, 58, 10, 59, 63, 61.0f);

            _checkpoint1X = 19f;
            _checkpoint2X = 44f;
            _checkpoint1Point = new Vector2(19.5f, 9f + _game.Config.playerHeight * 0.5f + 0.02f);
            _checkpoint2Point = new Vector2(43.5f, 8f + _game.Config.playerHeight * 0.5f + 0.02f);
        }

        /// <summary>
        /// Carves the original M1 tunnel: an open underground corridor, a thin surface floor,
        /// an indestructible rib and a stepped exit back to the surface.
        /// </summary>
        private void CarveTunnel(TerrainSystem t, int x0, int ribX, int x1, int exitX0, int exitX1)
        {
            t.ClearRect(x0, 2, x1 - x0 + 1, 4);
            t.Fill(x0, 6, x1 - x0, 1, TerrainSystem.Dirt);
            t.Fill(ribX, 7, 1, MapHeight - 7, TerrainSystem.Indestructible);
            t.ClearRect(exitX0, 6, exitX1 - exitX0 + 1, 1);
            t.Fill(exitX0 - 1, 2, 1, 2, TerrainSystem.Concrete);
            t.Fill(exitX0, 2, 1, 3, TerrainSystem.Concrete);
            t.Fill(exitX0 + 1, 2, 1, 4, TerrainSystem.Concrete);
            t.Fill(exitX0 + 2, 2, 1, 5, TerrainSystem.Concrete);
        }

        private void BuildCrushArena(TerrainSystem t, int platformX, int platformW,
            int supportAX, int supportBX, float armoredX)
        {
            // Protected crushing platform held by two destructible support columns.
            t.Fill(platformX, 13, platformW, 1, TerrainSystem.Concrete);
            t.ProtectRect(platformX, 13, platformW, 1);
            t.Fill(supportAX, 7, 1, 6, TerrainSystem.Support);
            t.Fill(supportBX, 7, 1, 6, TerrainSystem.Support);
            _armored = _game.Enemies.SpawnArmoredCrush(new Vector2(armoredX, SurfaceTop + 0.68f));
        }

        private void BuildGate(TerrainSystem t)
        {
            for (int y = SurfaceTop; y <= 12; y++)
            {
                int gx = 84;
                t.SetCell(gx, y, TerrainSystem.Indestructible);
                _gateCells.Add(new Vector2Int(gx, y));
            }
        }

        private void SpawnEnemies()
        {
            if (Selected == 0)
            {
                _dummies.Add(_game.Enemies.SpawnDummy(new Vector2(9.5f, 7f + 0.56f)));
                _dummies.Add(_game.Enemies.SpawnDummy(new Vector2(13.5f, 10f + 0.56f)));
                _dummies.Add(_game.Enemies.SpawnDummy(new Vector2(16.5f, 7f + 0.56f)));
                _dummies.Add(_game.Enemies.SpawnDummy(new Vector2(21.5f, 11f + 0.56f)));
                _dummies.Add(_game.Enemies.SpawnDummy(new Vector2(24.5f, 7f + 0.56f)));
                _dummies.Add(_game.Enemies.SpawnDummy(new Vector2(27.5f, 13f + 0.56f)));
                _dummies.Add(_game.Enemies.SpawnDummy(new Vector2(13.5f, 13f + 0.56f)));
                _dummies.Add(_game.Enemies.SpawnDummy(new Vector2(18.5f, 7f + 0.56f)));
                _game.Enemies.SpawnPatrol(new Vector2(23f, 7f + 0.56f), 21f, 25.5f);
                _game.Enemies.SpawnPatrol(new Vector2(36.5f, 2f + 0.56f), 34.2f, 39.2f);
                return;
            }

            if (Selected == 1)
            {
                _dummies.Add(_game.Enemies.SpawnDummy(new Vector2(9.5f, 7f + 0.56f)));
                _dummies.Add(_game.Enemies.SpawnDummy(new Vector2(11.5f, 10f + 0.56f)));
                _dummies.Add(_game.Enemies.SpawnDummy(new Vector2(17.5f, 7f + 0.56f)));
                _dummies.Add(_game.Enemies.SpawnDummy(new Vector2(25.5f, 7f + 0.56f)));
                _dummies.Add(_game.Enemies.SpawnDummy(new Vector2(32.5f, 2f + 0.56f)));
                _dummies.Add(_game.Enemies.SpawnDummy(new Vector2(36.5f, 2f + 0.56f)));
                _dummies.Add(_game.Enemies.SpawnDummy(new Vector2(43.5f, 2f + 0.56f)));
                _dummies.Add(_game.Enemies.SpawnDummy(new Vector2(54.5f, 7f + 0.56f)));
                _game.Enemies.SpawnPatrol(new Vector2(20f, 7f + 0.56f), 18f, 25f);
                _game.Enemies.SpawnPatrol(new Vector2(34f, 2f + 0.56f), 31f, 40f);
                return;
            }

            _dummies.Add(_game.Enemies.SpawnDummy(new Vector2(13.5f, 9f + 0.56f)));
            _dummies.Add(_game.Enemies.SpawnDummy(new Vector2(17.5f, 9f + 0.56f)));
            _dummies.Add(_game.Enemies.SpawnDummy(new Vector2(26.5f, 10f + 0.56f)));
            _dummies.Add(_game.Enemies.SpawnDummy(new Vector2(30.5f, 10f + 0.56f)));
            _dummies.Add(_game.Enemies.SpawnDummy(new Vector2(38.5f, 8f + 0.56f)));
            _dummies.Add(_game.Enemies.SpawnDummy(new Vector2(50.5f, 10f + 0.56f)));
            _dummies.Add(_game.Enemies.SpawnDummy(new Vector2(75.5f, 7f + 0.56f)));
            _game.Enemies.SpawnPatrol(new Vector2(15f, 9f + 0.56f), 11f, 19f);
            _game.Enemies.SpawnPatrol(new Vector2(40f, 8f + 0.56f), 36f, 44f);
        }

        private void SpawnProps()
        {
            if (Selected == 0)
            {
                _game.Props.SpawnBarrel(new Vector2(34.5f, 7f + 0.44f));
                _game.Props.SpawnBarrel(new Vector2(35.5f, 7f + 0.44f));
                _game.Props.SpawnBarrel(new Vector2(36.5f, 7f + 0.44f));
                _game.Props.SpawnCrate(new Vector2(37.5f, 7f + 0.37f));
                _game.Props.SpawnBarrel(new Vector2(55.2f, 7f + 0.44f));
                _game.Props.SpawnBarrel(new Vector2(57.8f, 7f + 0.44f));
                return;
            }

            if (Selected == 1)
            {
                _game.Props.SpawnBarrel(new Vector2(32.5f, 2f + 0.44f));
                _game.Props.SpawnBarrel(new Vector2(33.5f, 2f + 0.44f));
                _game.Props.SpawnBarrel(new Vector2(34.5f, 2f + 0.44f));
                _game.Props.SpawnCrate(new Vector2(35.5f, 2f + 0.37f));
                _game.Props.SpawnBarrel(new Vector2(56.8f, 7f + 0.44f));
                _game.Props.SpawnBarrel(new Vector2(58.8f, 7f + 0.44f));
                _game.Props.SpawnBarrel(new Vector2(60.8f, 7f + 0.44f));
                return;
            }

            _game.Props.SpawnBarrel(new Vector2(15.5f, 9f + 0.44f));
            _game.Props.SpawnCrate(new Vector2(18.5f, 9f + 0.37f));
            _game.Props.SpawnBarrel(new Vector2(39.5f, 8f + 0.44f));
            _game.Props.SpawnBarrel(new Vector2(56.8f, 7f + 0.44f));
            _game.Props.SpawnBarrel(new Vector2(58.8f, 7f + 0.44f));
            _game.Props.SpawnBarrel(new Vector2(60.8f, 7f + 0.44f));
        }

        private void BuildGoal()
        {
            GameObject goal = new GameObject("ExitMarker");
            goal.transform.SetParent(_game.transform, false);
            goal.transform.position = new Vector3(88.2f, SurfaceTop + 0.82f, 0f);
            SpriteRenderer sr = goal.AddComponent<SpriteRenderer>();
            sr.sprite = _game.Art.GoalSprite;
            sr.sortingOrder = 4;

            GameObject gateGlow = new GameObject("GateGlow");
            gateGlow.transform.SetParent(_game.transform, false);
            gateGlow.transform.position = new Vector3(84.5f, 10f, 0f);
            SpriteRenderer gsr = gateGlow.AddComponent<SpriteRenderer>();
            gsr.sprite = _game.Art.WhiteSprite;
            gsr.color = new Color(0.95f, 0.25f, 0.20f, 0.28f);
            gsr.sortingOrder = 4;
            gsr.transform.localScale = new Vector3(1.1f, 6.0f, 1f);
            _gateGlow = gateGlow;
        }

        private void BuildBackdrop()
        {
            Color main;
            Color alt;
            if (Selected == 0)
            {
                main = new Color(0.12f, 0.16f, 0.20f);
                alt = new Color(0.09f, 0.12f, 0.16f);
            }
            else if (Selected == 1)
            {
                main = new Color(0.10f, 0.17f, 0.16f);
                alt = new Color(0.07f, 0.12f, 0.11f);
            }
            else
            {
                main = new Color(0.17f, 0.12f, 0.16f);
                alt = new Color(0.11f, 0.08f, 0.12f);
            }

            GameObject parent = new GameObject("Backdrop");
            parent.transform.SetParent(_game.transform, false);
            int count = Selected == 2 ? 18 : 14;
            for (int i = 0; i < count; i++)
            {
                GameObject go = new GameObject("bg");
                go.transform.SetParent(parent.transform, false);
                float x = i * 5.8f + 2.5f;
                float h = 5f + ((i * 3 + Selected * 2) % 7);
                go.transform.position = new Vector3(x, 7f + h * 0.5f, 0.8f);
                go.transform.localScale = new Vector3(4.6f, h, 1f);
                SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = _game.Art.WhiteSprite;
                sr.color = i % 3 == 0 ? main : alt;
                sr.sortingOrder = -5;
            }
        }
    }
}
