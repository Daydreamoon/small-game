using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Breakline.EditorTools
{
    public static class BuildScript
    {
        private const string ScenePath = "Assets/Scenes/M1.unity";
        private const string PackageId = "com.breakline.m1";

        [MenuItem("BREAKLINE/Build Android Debug (Day 3)")]
        public static void BuildDebug()
        {
            BuildAndroid(true, "BREAKLINE_M1_DEBUG.apk");
        }

        [MenuItem("BREAKLINE/Build Android Release")]
        public static void BuildRelease()
        {
            BuildAndroid(false, "BREAKLINE_M1_RELEASE.apk");
        }

        [MenuItem("BREAKLINE/Build Debug + Release")]
        public static void BuildBoth()
        {
            if (!BuildAndroid(true, "BREAKLINE_M1_DEBUG.apk"))
                return;
            BuildAndroid(false, "BREAKLINE_M1_RELEASE.apk");
        }

        [MenuItem("BREAKLINE/Build Windows Smoke (internal visual check)")]
        public static void BuildWindowsSmoke()
        {
            BuildWindowsPlayer("BREAKLINE_SMOKE.exe", false);
        }

        [MenuItem("BREAKLINE/Build Windows OpenGL Smoke (presentation flip check)")]
        public static void BuildWindowsOpenGLSMoke()
        {
            BuildWindowsPlayer("BREAKLINE_SMOKE_GL.exe", true);
        }

        private static void BuildWindowsPlayer(string fileName, bool openGL)
        {
            try
            {
                EnsureScene();
                PlayerSettings.companyName = "Breakline Original";
                PlayerSettings.productName = "BREAKLINE M1";
                PlayerSettings.bundleVersion = "0.1.0";
                PlayerSettings.colorSpace = ColorSpace.Gamma;
                PlayerSettings.SetScriptingBackend(BuildTargetGroup.Standalone, ScriptingImplementation.Mono2x);
                PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
                PlayerSettings.defaultScreenWidth = 1280;
                PlayerSettings.defaultScreenHeight = 720;
                PlayerSettings.runInBackground = true;
                QualitySettings.vSyncCount = 0;
                QualitySettings.antiAliasing = 0;

                if (openGL)
                {
                    PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneWindows64, false);
                    PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneWindows64,
                        new[] { GraphicsDeviceType.OpenGLCore });
                }
                else
                {
                    PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneWindows64, true);
                }

                string root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
                string buildDir = Path.Combine(root, "Builds");
                Directory.CreateDirectory(buildDir);
                string output = Path.Combine(buildDir, fileName);
                BuildPlayerOptions request = new BuildPlayerOptions
                {
                    scenes = new[] { ScenePath },
                    locationPathName = output,
                    target = BuildTarget.StandaloneWindows64,
                    targetGroup = BuildTargetGroup.Standalone,
                    options = BuildOptions.Development
                };
                BuildReport report = BuildPipeline.BuildPlayer(request);
                Debug.Log($"[BREAKLINE BUILD] Windows smoke ({fileName}, openGL={openGL}) " +
                          $"result={report.summary.result} output={output}");
                ExitIfBatch(report.summary.result == BuildResult.Succeeded ? 0 : 1);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                ExitIfBatch(1);
            }
        }

        private static bool BuildAndroid(bool development, string fileName)
        {
            try
            {
                EnsureScene();
                ApplyProjectSettings(development);

                string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
                string buildDir = Path.Combine(projectRoot, "Builds");
                Directory.CreateDirectory(buildDir);
                string output = Path.Combine(buildDir, fileName);

                BuildOptions options = development ? BuildOptions.Development : BuildOptions.None;
                BuildPlayerOptions request = new BuildPlayerOptions
                {
                    scenes = new[] { ScenePath },
                    locationPathName = output,
                    target = BuildTarget.Android,
                    targetGroup = BuildTargetGroup.Android,
                    options = options
                };

                Debug.Log($"[BREAKLINE BUILD] Starting {(development ? "Development" : "Release")} Android build -> {output}");
                BuildReport report = BuildPipeline.BuildPlayer(request);
                BuildSummary summary = report.summary;
                Debug.Log($"[BREAKLINE BUILD] result={summary.result} size={summary.totalSize} " +
                          $"errors={summary.totalErrors} warnings={summary.totalWarnings} time={summary.totalTime}");
                if (summary.result != BuildResult.Succeeded)
                {
                    Debug.LogError("[BREAKLINE BUILD] Android build failed.");
                    ExitIfBatch(1);
                    return false;
                }

                Debug.Log($"[BREAKLINE BUILD] APK ready: {output}");
                ExitIfBatch(0);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                Debug.LogError("[BREAKLINE BUILD] exception during build.");
                ExitIfBatch(1);
                return false;
            }
        }

        private static void EnsureScene()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Scenes"))
                AssetDatabase.CreateFolder("Assets", "Scenes");

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            GameObject bootstrap = new GameObject("Bootstrap");
            bootstrap.AddComponent<GameBootstrap>();
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
                throw new Exception($"Failed to save scene {ScenePath}");

            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            Debug.Log($"[BREAKLINE BUILD] Prepared scene {ScenePath}");
        }

        private static void ApplyProjectSettings(bool development)
        {
            PlayerSettings.companyName = "Breakline Original";
            PlayerSettings.productName = "BREAKLINE M1";
            PlayerSettings.bundleVersion = "0.1.0";
            PlayerSettings.colorSpace = ColorSpace.Gamma;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, PackageId);
            PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevel33;
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.useCustomKeystore = false;
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.OpenGLES3 });
            EditorUserBuildSettings.buildAppBundle = false;
            QualitySettings.vSyncCount = 0;
            QualitySettings.antiAliasing = 0;

            SetLegacyInputHandler();
            ConfigureAndroidExternalTools();
            EnsureAlwaysIncludedShader("Sprites/Default");
            EnsureAlwaysIncludedShader("Unlit/Transparent");
            AssetDatabase.SaveAssets();
            Debug.Log($"[BREAKLINE BUILD] PlayerSettings applied on Unity 2021.3.45f2. IL2CPP/ARM64, GLES3, landscape, package {PackageId}, dev={development}");
        }

        private static void ConfigureAndroidExternalTools()
        {
            string androidPlayer = Path.Combine(EditorApplication.applicationContentsPath, "PlaybackEngines/AndroidPlayer");
            string sdk = Path.Combine(androidPlayer, "SDK");
            string ndk = Path.Combine(androidPlayer, "NDK");
            string moduleJdk = Path.Combine(androidPlayer, "OpenJDK");
            string fallbackJdk = @"E:\BreaklineTools\jdk11";

            if (Directory.Exists(sdk))
            {
                EditorPrefs.SetString("AndroidSdkRoot", sdk);
                Environment.SetEnvironmentVariable("ANDROID_SDK_ROOT", sdk, EnvironmentVariableTarget.Process);
                Environment.SetEnvironmentVariable("ANDROID_HOME", sdk, EnvironmentVariableTarget.Process);
                Debug.Log($"[BREAKLINE BUILD] Android SDK: {sdk}");
            }
            else
            {
                Debug.LogWarning($"[BREAKLINE BUILD] Android SDK not found: {sdk}");
            }

            if (Directory.Exists(ndk))
            {
                EditorPrefs.SetString("AndroidNdkRoot", ndk);
                Environment.SetEnvironmentVariable("ANDROID_NDK_ROOT", ndk, EnvironmentVariableTarget.Process);
                Debug.Log($"[BREAKLINE BUILD] Android NDK: {ndk}");
            }
            else
            {
                Debug.LogWarning($"[BREAKLINE BUILD] Android NDK not found: {ndk}");
            }

            string jdk = Directory.Exists(moduleJdk) ? moduleJdk : (Directory.Exists(fallbackJdk) ? fallbackJdk : null);
            if (!string.IsNullOrEmpty(jdk))
            {
                EditorPrefs.SetString("AndroidJdkRoot", jdk);
                EditorPrefs.SetString("JdkPath", jdk);
                Environment.SetEnvironmentVariable("JAVA_HOME", jdk, EnvironmentVariableTarget.Process);
                Debug.Log($"[BREAKLINE BUILD] Android JDK: {jdk}");
            }
            else
            {
                Debug.LogWarning("[BREAKLINE BUILD] No Android JDK found (module or E:\\BreaklineTools\\jdk11).");
            }
        }

        private static void SetLegacyInputHandler()
        {
            UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset");
            if (assets == null || assets.Length == 0)
            {
                Debug.LogWarning("[BREAKLINE BUILD] ProjectSettings.asset not found; legacy input not forced.");
                return;
            }

            SerializedObject so = new SerializedObject(assets[0]);
            SerializedProperty prop = so.FindProperty("activeInputHandler");
            if (prop == null)
            {
                Debug.LogWarning("[BREAKLINE BUILD] activeInputHandler property not found.");
                return;
            }
            // 0 = old Input Manager, 1 = Input System, 2 = Both. This project uses UnityEngine.Input.
            prop.intValue = 0;
            so.ApplyModifiedPropertiesWithoutUndo();
            Debug.Log("[BREAKLINE BUILD] Active input handler forced to legacy Input Manager.");
        }

        private static void EnsureAlwaysIncludedShader(string shaderName)
        {
            Shader shader = Shader.Find(shaderName);
            if (shader == null)
            {
                Debug.LogWarning($"[BREAKLINE BUILD] Shader not found: {shaderName}");
                return;
            }

            UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset");
            if (assets == null || assets.Length == 0) return;
            SerializedObject so = new SerializedObject(assets[0]);
            SerializedProperty list = so.FindProperty("m_AlwaysIncludedShaders");
            if (list == null || !list.isArray)
            {
                Debug.LogWarning("[BREAKLINE BUILD] m_AlwaysIncludedShaders not found; relying on sprite defaults.");
                return;
            }

            for (int i = 0; i < list.arraySize; i++)
            {
                if (list.GetArrayElementAtIndex(i).objectReferenceValue == shader)
                    return;
            }
            list.InsertArrayElementAtIndex(list.arraySize);
            list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = shader;
            so.ApplyModifiedPropertiesWithoutUndo();
            Debug.Log($"[BREAKLINE BUILD] Added shader to Always Included: {shaderName}");
        }

        private static void ExitIfBatch(int code)
        {
            if (Application.isBatchMode)
                EditorApplication.Exit(code);
        }
    }
}
