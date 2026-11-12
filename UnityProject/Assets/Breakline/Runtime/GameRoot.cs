using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Breakline
{
    /// <summary>
    /// Owns boot order and explicit per-frame system ticking.
    /// This is intentionally a small, explicit composition root rather than a DI framework.
    /// </summary>
    public sealed class GameRoot : MonoBehaviour
    {
        public M1Config Config { get; private set; }
        public Art Art { get; private set; }
        public AudioSynth Audio { get; private set; }
        public Viewport Viewport { get; private set; }
        public InputRouter Input { get; private set; }
        public TerrainSystem Terrain { get; private set; }
        public DebrisSystem Debris { get; private set; }
        public Vfx Vfx { get; private set; }
        public ProjectileSystem Projectiles { get; private set; }
        public EnemySystem Enemies { get; private set; }
        public PropSystem Props { get; private set; }
        public PlayerController Player { get; private set; }
        public CameraDirector Camera { get; private set; }
        public Feedback Feedback { get; private set; }
        public M1Level Level { get; private set; }
        public Hud Hud { get; private set; }
        public PerfLogger Perf { get; private set; }

        public bool Initialized { get; private set; }

        private void Awake()
        {
            Application.targetFrameRate = 60;
            QualitySettings.vSyncCount = 0;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            bool smokeWindow = HasCommandLineArg("--smoke") || HasCommandLineArg("--autotest");
            if (smokeWindow)
            {
                Screen.fullScreen = false;
                Screen.fullScreenMode = FullScreenMode.Windowed;
                Screen.SetResolution(1280, 720, false);
            }
            else
            {
                Screen.fullScreen = true;
            }
            Screen.autorotateToPortrait = false;
            Screen.autorotateToPortraitUpsideDown = false;
            Screen.autorotateToLandscapeLeft = true;
            Screen.autorotateToLandscapeRight = true;
            Screen.orientation = ScreenOrientation.AutoRotation;
            UnityEngine.Input.multiTouchEnabled = true;
            Time.timeScale = 1f;
            Physics2D.gravity = new Vector2(0f, -30f);

            ConfigurePhysicsLayers();

            Tuning tuning = new Tuning();
            tuning.Load();
            Config = M1Config.FromTuning(tuning);

            Art = new Art();
            Art.Build();

            Audio = new AudioSynth();
            Audio.Init(gameObject);

            Viewport = new Viewport();
            Viewport.Init(transform);

            Input = new InputRouter();

            Terrain = new TerrainSystem(this);
            Terrain.Init(M1Level.MapWidth, M1Level.MapHeight);

            Debris = new DebrisSystem(this);
            Debris.Init(transform);

            Vfx = new Vfx(this);
            Vfx.Init(transform);

            Projectiles = new ProjectileSystem(this);
            Projectiles.Init(transform);

            Enemies = new EnemySystem(this);
            Enemies.Init(transform);

            Props = new PropSystem(this);
            Props.Init(transform);

            Level = new M1Level(this);
            Level.Build();

            Player = new PlayerController(this);
            Player.Init(Level.PlayerSpawn);
            Player.SetRespawnPoint(Level.PlayerSpawn);

            Camera = new CameraDirector(this);
            Camera.Init();

            Feedback = new Feedback(this);
            Feedback.Init();

            Perf = new PerfLogger(this);
            Perf.Init();

            Hud = gameObject.AddComponent<Hud>();
            Hud.Init(this);

            Initialized = true;
            Debug.Log($"[BREAKLINE] Boot complete. Unity {Application.unityVersion}, screen {Screen.width}x{Screen.height}, " +
                      $"RT {Viewport.RtWidth}x{Viewport.RtHeight} x{Viewport.Scale}, CSV {Perf.CsvPath}");

            string autoMode = ReadAndroidTestMode();
            if (HasCommandLineArg("--smoke") || autoMode == "smoke")
                StartCoroutine(SmokeCapture());
            else if (HasCommandLineArg("--autotest") || autoMode == "crush")
                StartCoroutine(AutoTestCrush());
            else if (HasCommandLineArg("--autostress") || autoMode == "stress")
                StartCoroutine(AutoStressAfterBoot());
            else if (HasCommandLineArg("--autofull") || autoMode == "full")
                StartCoroutine(AutoFullBodyAfterBoot());
            else if (HasCommandLineArg("--autop6") || autoMode == "p6")
                StartCoroutine(AutoP6AfterBoot());
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        private static string ReadAndroidTestMode()
        {
            try
            {
                using (AndroidJavaClass player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (AndroidJavaObject activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                using (AndroidJavaObject intent = activity.Call<AndroidJavaObject>("getIntent"))
                {
                    if (intent == null) return null;
                    return intent.Call<string>("getStringExtra", "breakline_auto");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[BREAKLINE] Android test intent read failed: {e.Message}");
                return null;
            }
        }
#else
        private static string ReadAndroidTestMode()
        {
            return null;
        }
#endif

        private IEnumerator AutoStressAfterBoot()
        {
            yield return new WaitForSecondsRealtime(1.0f);
            DebugStartStress();
            // Do not call ScreenCapture while the 10 s perf window is being recorded:
            // on the iQOO test device that single capture cost ~50 ms and polluted P4.
            // External adb screencap supplies the evidence instead.
            yield return new WaitForSecondsRealtime(10.8f);
            Debug.Log("[BREAKLINE] Auto-stress measurement complete; external adb capture is authoritative.");
        }

        private IEnumerator AutoFullBodyAfterBoot()
        {
            yield return new WaitForSecondsRealtime(1.0f);
            DebugStartFullBody200();
            yield return new WaitForSecondsRealtime(10.8f);
            Debug.Log($"[BREAKLINE] Auto-full200 measurement complete; stressBodies={Debris.StressBodyCount}");
            if (Application.platform == RuntimePlatform.WindowsPlayer)
            {
                yield return new WaitForSecondsRealtime(2.0f);
                Application.Quit();
            }
        }

        private IEnumerator AutoP6AfterBoot()
        {
            // P6: one continuous app session, repeated stress windows, no force-stop,
            // no screen capture. PerfLogger appends one row per 10 s window.
            const int cycles = 56;
            yield return new WaitForSecondsRealtime(3.0f);
            Player.SetInvincibleFor(900f);
            Debug.Log($"[BREAKLINE] P6 continuous stress start: {cycles} x 10s windows, csv={Perf.CsvPath}");
            for (int i = 0; i < cycles; i++)
            {
                DebugStartStress();
                yield return new WaitForSecondsRealtime(11.0f);
                Debug.Log($"[BREAKLINE] P6 cycle {i + 1}/{cycles} done");
            }
            try
            {
                File.WriteAllText(Path.Combine(Application.persistentDataPath, "P6-DONE.txt"),
                    $"cycles={cycles}; utc={System.DateTime.UtcNow:o}; csv={Perf.CsvPath}\n");
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[BREAKLINE] P6 marker write failed: {e.Message}");
            }
            Debug.Log($"[BREAKLINE] P6 complete: cycles={cycles}; pull {Perf.CsvPath}");
        }

        private static bool HasCommandLineArg(string expected)
        {
            string[] args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (string.Equals(args[i], expected, System.StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        private IEnumerator SmokeCapture()
        {
            yield return new WaitForSecondsRealtime(1.2f);
            string path = Path.Combine(Application.persistentDataPath, "m1-smoke.png");
            ScreenCapture.CaptureScreenshot(path);
            Debug.Log($"[BREAKLINE] Smoke screenshot requested: {path}");
            yield return new WaitForSecondsRealtime(1.0f);
            Debug.Log("[BREAKLINE] Smoke capture done; quitting Windows smoke player.");
            Application.Quit();
        }

        private IEnumerator AutoTestCrush()
        {
            yield return new WaitForSecondsRealtime(0.8f);
            bool initiallyAlive = Level.ArmoredAlive;
            Player.DebugTeleport(new Vector2(51.5f, 7.7f));
            Camera.SnapToPlayer();
            yield return new WaitForSecondsRealtime(0.4f);

            Vector2 origin = new Vector2(52.0f, 7.775f);
            bool barrelFound = Props.RaycastFirst(origin, Vector2.right, 6.0f,
                out ExplosiveProp barrel, out float _, out Vector2 point);
            if (barrelFound)
                Props.DamageProp(barrel, 999f, point, Vector2.right);

            yield return new WaitForSecondsRealtime(2.5f);
            bool dead = !Level.ArmoredAlive;
            bool gate = Level.GateOpen;
            string path = Path.Combine(Application.persistentDataPath, "m1-autotest.png");
            ScreenCapture.CaptureScreenshot(path);
            if (initiallyAlive && dead && gate)
                Debug.Log($"[BREAKLINE AUTOTEST] PASS initialAlive={initiallyAlive} barrel={barrelFound} crushed={dead} gateOpen={gate} screenshot={path}");
            else
                Debug.LogError($"[BREAKLINE AUTOTEST] FAIL initialAlive={initiallyAlive} barrel={barrelFound} crushed={dead} gateOpen={gate} screenshot={path}");
            yield return new WaitForSecondsRealtime(1.0f);
            Application.Quit();
        }

        private void OnApplicationQuit()
        {
            Time.timeScale = 1f;
        }

        private void ConfigurePhysicsLayers()
        {
            // Terrain (8) collides only with debris/collapse bodies (9).
            for (int layer = 0; layer < 32; layer++)
            {
                Physics2D.IgnoreLayerCollision(TerrainSystem.DebrisLayer, layer, layer != TerrainSystem.TerrainLayer);
                Physics2D.IgnoreLayerCollision(TerrainSystem.TerrainLayer, layer, layer != TerrainSystem.DebrisLayer && layer != 0);
            }
            Physics2D.IgnoreLayerCollision(TerrainSystem.DebrisLayer, TerrainSystem.DebrisLayer, true);
        }

        private void Update()
        {
            if (!Initialized) return;

            Input.DebugBlocked = Hud != null && Hud.DebugOpen;
            Input.Sample();

            float unscaled = Time.unscaledDeltaTime;
            Feedback.TickUnscaled(unscaled);
            float dt = Mathf.Min(Time.deltaTime, 1f / 30f);
            if (dt < 0f) dt = 0f;

            Player.Tick(dt);
            Enemies.Tick(dt);
            Props.Tick(dt);
            Projectiles.Tick(dt);
            Terrain.Tick(dt);
            Debris.Tick(dt);
            Vfx.Tick(unscaled);
            Level.Tick(dt);
            Camera.TickShakeUnscaled(unscaled);
            Camera.Tick(dt);
            Viewport.Tick();
            Perf.Tick();
            Input.EndFrame();
        }

        public void ExplodeAt(Vector2 center, float radius, float damage, float terrainRadius,
            float terrainDamage, ExplosiveProp source)
        {
            if (Terrain == null) return;

            Terrain.Explode(center, Mathf.Max(radius, terrainRadius), terrainRadius, terrainDamage);
            Enemies.DamageRadius(center, radius, damage);
            Props.TriggerInRadius(center, Mathf.Max(radius, 1f), source);
            Debris.SpawnDust(center, Mathf.RoundToInt(6f + radius * 4f));
            Vfx.ExplosionFlash(center, radius);
            Audio.Play(AudioCue.Explosion, 0.9f);
            Feedback.Explosion();
        }

        public void OnCrushTargetKilled()
        {
            if (Level != null)
                Level.OnCrushTargetKilled();
        }

        public void DebugStartStress()
        {
            Debris.ClearStressBodies();
            Debris.ClearStressVisuals();
            Debris.SpawnStressVisualBurst(200, 20f);
            Debris.SpawnStressPhysical(12);
            SpawnDebugChainNearPlayer();
            Perf.StartRecording("stress-200vis-12phys-chain", 10f);
            Level.ShowBanner("压力测试：200 碎片 + 12 刚体 + 链爆", 2.2f);
        }

        public void DebugStartFullBody200()
        {
            Debris.ClearStressBodies();
            Debris.ClearStressVisuals();
            Debris.SpawnFullBodyStress(200);
            Perf.StartRecording("full-200-rigidbodies", 10f);
            Level.ShowBanner("压力测试：200 全刚体", 2.2f);
        }

        public void DebugChainAtPlayer()
        {
            SpawnDebugChainNearPlayer();
            Level.ShowBanner("链爆测试", 1.2f);
        }

        private void SpawnDebugChainNearPlayer()
        {
            if (Player == null) return;
            Vector2 basePos = Player.Position + new Vector2(2.8f, 0.5f);
            ExplosiveProp first = null;
            for (int i = 0; i < 3; i++)
            {
                ExplosiveProp p = Props.SpawnBarrel(basePos + new Vector2(i * 0.80f, 0f));
                if (i == 0) first = p;
            }
            if (first != null)
                Props.DamageProp(first, 999f, first.Position, Vector2.up);
        }

        public void DebugRestart()
        {
            if (Perf != null) Perf.Flush("restart");
            Time.timeScale = 1f;
            Scene scene = SceneManager.GetActiveScene();
            Debug.Log($"[BREAKLINE] Restarting scene: {scene.name} ({scene.buildIndex})");
            SceneManager.LoadScene(scene.buildIndex);
        }

        private void OnApplicationFocus(bool focused)
        {
            if (!focused && Input != null)
                Input.ResetAll();
        }

        private void OnDestroy()
        {
            Time.timeScale = 1f;
        }
    }
}
