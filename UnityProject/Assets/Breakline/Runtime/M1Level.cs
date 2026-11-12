using System.Collections.Generic;
using UnityEngine;

namespace Breakline
{
    /// <summary>
    /// M1's one greybox level. It intentionally contains:
    /// - an 8-direction target/aim range
    /// - a forced blast-through-floor tunnel under an indestructible wall
    /// - a forced "drop the platform onto the armored target" finale
    /// - shootable barrels at the support bases and in the tunnel
    /// </summary>
    public sealed class M1Level
    {
        public const int MapWidth = 96;
        public const int MapHeight = 24;
        public const int SurfaceTop = 7;

        private readonly GameRoot _game;
        private readonly List<Vector2Int> _gateCells = new List<Vector2Int>();
        private readonly List<Enemy> _dummies = new List<Enemy>();
        private Enemy _armored;
        private GameObject _gateGlow;
        private bool _checkpoint1;
        private bool _checkpoint2;
        private float _bannerTimer;

        public Vector2 PlayerSpawn { get; private set; }
        public bool GateOpen { get; private set; }
        public bool Completed { get; private set; }
        public string Banner { get; private set; } = string.Empty;
        public bool ArmoredAlive => _armored != null && _armored.Alive;

        public M1Level(GameRoot game)
        {
            _game = game;
        }

        public void Build()
        {
            TerrainSystem t = _game.Terrain;
            PlayerSpawn = new Vector2(5.5f, SurfaceTop + _game.Config.playerHeight * 0.5f + 0.02f);

            // Safe base and side walls; bottom is an indestructible support anchor.
            t.Fill(0, 0, MapWidth, 2, TerrainSystem.Indestructible);
            t.Fill(0, 2, MapWidth, 4, TerrainSystem.Concrete);
            t.Fill(0, 6, MapWidth, 1, TerrainSystem.Dirt);
            t.Fill(0, 0, 2, MapHeight, TerrainSystem.Indestructible);
            t.Fill(MapWidth - 2, 0, 2, MapHeight, TerrainSystem.Indestructible);

            // A-zone target platforms are hung from the background; mark explicit anchors
            // so the support solver does not drop them on the first frame.
            t.Fill(11, 9, 4, 1, TerrainSystem.Concrete);
            t.AnchorRect(11, 9, 4, 1);
            t.Fill(11, 12, 4, 1, TerrainSystem.Concrete);
            t.AnchorRect(11, 12, 4, 1);
            t.Fill(19, 10, 5, 1, TerrainSystem.Concrete);
            t.AnchorRect(19, 10, 5, 1);
            t.Fill(26, 12, 4, 1, TerrainSystem.Concrete);
            t.AnchorRect(26, 12, 4, 1);

            // Small two-tile jump wall before the tunnel.
            t.Fill(30, 7, 3, 2, TerrainSystem.Concrete);

            // B-zone: carve a tunnel, keep a thin floor above it, block the surface with an anchor wall.
            t.ClearRect(33, 2, 15, 4);       // x 33..47, y 2..5
            t.Fill(40, 7, 1, MapHeight - 7, TerrainSystem.Indestructible);
            // Exit shaft from the tunnel back to the surface. The player must jump 1-tile steps.
            t.ClearRect(44, 6, 3, 1);
            t.Fill(43, 2, 1, 2, TerrainSystem.Concrete);
            t.Fill(44, 2, 1, 3, TerrainSystem.Concrete);
            t.Fill(45, 2, 1, 4, TerrainSystem.Concrete);
            t.Fill(46, 2, 1, 5, TerrainSystem.Concrete);

            // C-zone: protected crushing platform held by two destructible support columns.
            // Both supports sit on the left; the armored target waits under the cantilever.
            t.Fill(55, 13, 10, 1, TerrainSystem.Concrete);
            t.ProtectRect(55, 13, 10, 1);
            t.Fill(56, 7, 1, 6, TerrainSystem.Support);
            t.Fill(59, 7, 1, 6, TerrainSystem.Support);

            // Gate blocked until the armored target is crushed. Gate cells are anchored X-type.
            for (int y = SurfaceTop; y <= 12; y++)
            {
                int gx = 84;
                t.SetCell(gx, y, TerrainSystem.Indestructible);
                _gateCells.Add(new Vector2Int(gx, y));
            }

            // Rebuild all chunk meshes/colliders after authoring.
            t.RebuildAllImmediate();
            t.MarkSupportDirty();

            BuildBackdrop();
            BuildGoal();
            SpawnEnemies();
            SpawnProps();

            PlayerSpawn = new Vector2(PlayerSpawn.x, PlayerSpawn.y);
            Debug.Log($"[BREAKLINE] M1 level built: {MapWidth}x{MapHeight}, gate cells={_gateCells.Count}, spawn={PlayerSpawn}");
        }

        public void Tick(float dt)
        {
            if (_bannerTimer > 0f)
                _bannerTimer -= dt;

            PlayerController p = _game.Player;
            if (p == null) return;

            if (!_checkpoint1 && p.Position.x > 28f)
            {
                _checkpoint1 = true;
                p.SetRespawnPoint(new Vector2(28.5f, SurfaceTop + _game.Config.playerHeight * 0.5f + 0.02f));
                ShowBanner("检查点", 1.4f);
            }
            if (!_checkpoint2 && p.Position.x > 47.5f)
            {
                _checkpoint2 = true;
                p.SetRespawnPoint(new Vector2(48.5f, SurfaceTop + _game.Config.playerHeight * 0.5f + 0.02f));
                ShowBanner("检查点", 1.4f);
            }

            if (!Completed && GateOpen && p.Position.x > 88.0f)
                Complete();

            if (!GateOpen && _armored != null && _armored.Alive && p.Position.x > 78f && _bannerTimer <= 0f)
                ShowBanner("闸门锁定 — 压死装甲目标", 1.0f);
        }

        public string GetHint()
        {
            if (Completed) return "M1 完成 — 路线已验证";
            if (GateOpen) return "闸门已开 — 向右前进";
            PlayerController p = _game.Player;
            if (p == null) return string.Empty;

            float x = p.Position.x;
            if (ArmoredAlive && x >= 48f && x < 84f)
                return "打爆油桶链 — 压死装甲目标";
            if (x < 10f) return "拖动屏幕瞄准射击";
            if (x < 29f) return "靶场";
            if (x < 34f) return "跳过矮墙";
            if (x < 40f) return "薄地板 — 向下炸穿";
            if (x < 47f) return "爬上出口台阶";
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
            Debug.Log("[BREAKLINE] Armored crush target killed; gate open.");
        }

        private void Complete()
        {
            Completed = true;
            if (_game.Player != null) _game.Player.ControlsEnabled = false;
            ShowBanner("M1 路线完成", 999f);
            _game.Audio.Play(AudioCue.Gate, 0.8f);
            _game.Perf.FlushScenarioIfRecording("route-complete");
            Debug.Log("[BREAKLINE] M1 route complete");
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

        private void SpawnEnemies()
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

            _armored = _game.Enemies.SpawnArmoredCrush(new Vector2(61.5f, 7f + 0.68f));
        }

        private void SpawnProps()
        {
            // B tunnel: three barrels chain into the thin floor.
            _game.Props.SpawnBarrel(new Vector2(34.5f, 7f + 0.44f));
            _game.Props.SpawnBarrel(new Vector2(35.5f, 7f + 0.44f));
            _game.Props.SpawnBarrel(new Vector2(36.5f, 7f + 0.44f));
            _game.Props.SpawnCrate(new Vector2(37.5f, 7f + 0.37f));

            // C arena: the first barrel is reachable from the left; its chain partner is just
            // behind the first support, so one chain cuts both supports and drops the platform.
            _game.Props.SpawnBarrel(new Vector2(55.2f, 7f + 0.44f));
            _game.Props.SpawnBarrel(new Vector2(57.8f, 7f + 0.44f));
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
            GameObject parent = new GameObject("Backdrop");
            parent.transform.SetParent(_game.transform, false);
            for (int i = 0; i < 14; i++)
            {
                GameObject go = new GameObject("bg");
                go.transform.SetParent(parent.transform, false);
                float x = i * 7.5f + 2.5f;
                float h = 5f + (i * 3 % 7);
                go.transform.position = new Vector3(x, 7f + h * 0.5f, 0.8f);
                go.transform.localScale = new Vector3(5f, h, 1f);
                SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = _game.Art.WhiteSprite;
                sr.color = i % 3 == 0 ? new Color(0.12f, 0.16f, 0.20f) : new Color(0.09f, 0.12f, 0.16f);
                sr.sortingOrder = -5;
            }
        }
    }
}
