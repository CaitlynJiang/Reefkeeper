using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEditor.XR.OpenXR.Features;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Management;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.Features.Interactions;
using UnityEngine.XR.OpenXR.Features.MetaQuestSupport;

namespace Reefkeeper.Quest
{
    /// <summary>
    /// Meta Quest 3 test build. Lives only on the quest3 branch (separate worktree), so the PICO
    /// project on v3 is never touched. Uses Unity OpenXR + Meta Quest Support instead of the PICO SDK,
    /// and a different package id so it can sit next to other installs on the headset.
    /// </summary>
    public static class QuestSetup
    {
        public const string SceneV3 = "Assets/Reefkeeper/Scenes/ShallowReef_V3.unity";
        public const string SceneV2 = "Assets/Reefkeeper/Scenes/ShallowReef_V2.unity";
        const string PackageId = "com.reefkeeper.quest3";
        const string OpenXRLoader = "UnityEngine.XR.OpenXR.OpenXRLoader";

        [MenuItem("Reefkeeper/Quest 3/1. Configure project for Quest - 配置 Quest")]
        public static void Configure()
        {
            var android = NamedBuildTarget.Android;
            PlayerSettings.productName = "Reefkeeper Quest";
            PlayerSettings.SetApplicationIdentifier(android, PackageId);
            PlayerSettings.SetScriptingBackend(android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel32;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
            // Unity 6 + Meta Quest requires GameActivity (PICO uses the classic Activity).
            PlayerSettings.Android.applicationEntry = AndroidApplicationEntry.GameActivity;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.Vulkan });
            PlayerSettings.SetMobileMTRendering(android, true);
            PlayerSettings.graphicsJobs = false;
            PlayerSettings.runInBackground = true;
            EditorUserBuildSettings.androidBuildSubtarget = MobileTextureSubtarget.ASTC;

            EnableOpenXR();
            ConfigureRendering();
            AssetDatabase.SaveAssets();
            Debug.Log("[Quest] Project configured for Meta Quest 3 (OpenXR).");
        }

        static void EnableOpenXR()
        {
            EditorBuildSettings.TryGetConfigObject(XRGeneralSettings.k_SettingsKey, out XRGeneralSettingsPerBuildTarget perTarget);
            if (!perTarget)
            {
                perTarget = ScriptableObject.CreateInstance<XRGeneralSettingsPerBuildTarget>();
                Directory.CreateDirectory("Assets/XR");
                AssetDatabase.CreateAsset(perTarget, "Assets/XR/XRGeneralSettingsPerBuildTarget.asset");
                EditorBuildSettings.AddConfigObject(XRGeneralSettings.k_SettingsKey, perTarget, true);
            }
            if (!perTarget.HasSettingsForBuildTarget(BuildTargetGroup.Android)) perTarget.CreateDefaultSettingsForBuildTarget(BuildTargetGroup.Android);
            if (!perTarget.HasManagerSettingsForBuildTarget(BuildTargetGroup.Android)) perTarget.CreateDefaultManagerSettingsForBuildTarget(BuildTargetGroup.Android);
            var general = perTarget.SettingsForBuildTarget(BuildTargetGroup.Android);
            general.InitManagerOnStart = true;

            // Drop the dangling PICO loader reference and any other loader, keep only OpenXR.
            var manager = general.Manager;
            foreach (var loader in manager.activeLoaders.Where(l => l && l.GetType().FullName != OpenXRLoader).ToList())
                XRPackageMetadataStore.RemoveLoader(manager, loader.GetType().FullName, BuildTargetGroup.Android);
            var so = new SerializedObject(manager);
            var loaders = so.FindProperty("m_Loaders");
            for (int i = loaders.arraySize - 1; i >= 0; i--)
                if (!loaders.GetArrayElementAtIndex(i).objectReferenceValue) loaders.DeleteArrayElementAtIndex(i);
            so.ApplyModifiedPropertiesWithoutUndo();
            if (!manager.activeLoaders.Any(l => l && l.GetType().FullName == OpenXRLoader))
                XRPackageMetadataStore.AssignLoader(manager, OpenXRLoader, BuildTargetGroup.Android);
            EditorUtility.SetDirty(manager);
            EditorUtility.SetDirty(general);
            EditorUtility.SetDirty(perTarget);

            FeatureHelpers.RefreshFeatures(BuildTargetGroup.Android);
            var openxr = OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Android);
            openxr.renderMode = OpenXRSettings.RenderMode.SinglePassInstanced;
            // Fixed foveated rendering; QuestPerformance sets the strength at runtime.
            // Foveation off for now: SRP Foveation doubled GPU frame time on device (47 ms -> 95 ms).
            // MetaQuestFeature keeps its own copy of the API choice and applies it at build time.
            SetFoveationApi(openxr, OpenXRSettings.BackendFovationApi.Legacy);
            SetFoveationApi(openxr.GetFeature<MetaQuestFeature>(), OpenXRSettings.BackendFovationApi.Legacy);
            var foveation = openxr.GetFeature<UnityEngine.XR.OpenXR.Features.FoveatedRenderingFeature>();
            if (foveation) { foveation.enabled = false; EditorUtility.SetDirty(foveation); }
            Enable<MetaQuestFeature>(openxr);
            Enable<MetaQuestTouchPlusControllerProfile>(openxr); // Quest 3 controllers
            Enable<OculusTouchControllerProfile>(openxr);        // fallback / Quest 2 controllers
            // Tracked hands (XR Hands): the rig switches to hands when the controllers are put down.
            Enable<UnityEngine.XR.Hands.OpenXR.HandTracking>(openxr);
            Enable<UnityEngine.XR.Hands.OpenXR.MetaHandTrackingAim>(openxr);
            EditorUtility.SetDirty(openxr);
        }

        // Clarity: full eye-buffer resolution and 4x MSAA (cheap on Quest's tiled GPU).
        // The shared mobile pipeline asset used to render at 0.8 with MSAA off.
        static void ConfigureRendering()
        {
            var assets = new System.Collections.Generic.List<RenderPipelineAsset>();
            QualitySettings.GetAllRenderPipelineAssetsForPlatform("Android", ref assets);
            foreach (var urp in assets.OfType<UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset>().Distinct())
            {
                urp.renderScale = 1f;
                urp.msaaSampleCount = 4;
                urp.supportsHDR = false;
                // GPU budget: ~980 shadow casters were redrawn out to 50 m. Keep shadows near the
                // player only, and light the small task light per vertex instead of per pixel.
                var so = new SerializedObject(urp);
                so.FindProperty("m_ShadowDistance").floatValue = 15f;
                so.FindProperty("m_AdditionalLightsRenderingMode").intValue = 2; // per vertex
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(urp);
            }
        }

        static void SetFoveationApi(Object target, OpenXRSettings.BackendFovationApi value)
        {
            if (!target) return;
            var so = new SerializedObject(target);
            var api = so.FindProperty("m_foveatedRenderingApi");
            if (api == null) { Debug.LogError($"[Quest] No foveation API field on {target.name}"); return; }
            api.intValue = (int)value;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
        }

        static void Enable<T>(OpenXRSettings settings) where T : UnityEngine.XR.OpenXR.Features.OpenXRFeature
        {
            var feature = settings.GetFeature<T>();
            if (feature) { feature.enabled = true; EditorUtility.SetDirty(feature); }
            else Debug.LogError($"[Quest] OpenXR feature {typeof(T).Name} not found.");
        }

        [MenuItem("Reefkeeper/Quest 3/2. Build Quest APK (V3) - 打包 V3")]
        public static void BuildV3() => Build(SceneV3, "Builds/Reefkeeper_V3_Quest3.apk");

        [MenuItem("Reefkeeper/Quest 3/3. Build Quest APK (V2) - 打包 V2")]
        public static void BuildV2() => Build(SceneV2, "Builds/Reefkeeper_V2_Quest3.apk");

        /// <summary>Command line: -executeMethod Reefkeeper.Quest.QuestSetup.BuildBatch</summary>
        public static void BuildBatch()
        {
            var ok = Build(SceneV3, "Builds/Reefkeeper_V3_Quest3.apk");
            if (Application.isBatchMode) EditorApplication.Exit(ok ? 0 : 1);
        }

        static bool Build(string scene, string apkPath)
        {
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
            {
                Debug.LogError("[Quest] Switch the platform to Android first (File > Build Profiles).");
                return false;
            }
            Configure();
            Directory.CreateDirectory(Path.GetDirectoryName(apkPath));
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { scene },
                locationPathName = apkPath,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = BuildOptions.None,
            });
            var s = report.summary;
            string result = $"{s.result} | {s.totalSize / (1024f * 1024f):F1} MB | {s.totalTime:mm\\:ss} | errors {s.totalErrors} | {Path.GetFullPath(apkPath)}";
            File.WriteAllText("Builds/last-quest-build.txt", result + "\n" + string.Join("\n",
                report.steps.SelectMany(st => st.messages).Where(m => m.type == LogType.Error || m.type == LogType.Exception).Select(m => m.content).Take(20)));
            Debug.Log("[Quest] Build " + result);
            return s.result == BuildResult.Succeeded;
        }
    }

    /// <summary>
    /// Keeps the desktop XR Interaction Simulator out of the Quest build without editing the saved
    /// scenes (so merges from v3 stay clean). Only runs during player builds, not in Play mode.
    /// </summary>
    class StripSimulatorFromBuild : UnityEditor.Build.IProcessSceneWithReport
    {
        public int callbackOrder => 0;
        public void OnProcessScene(Scene scene, BuildReport report)
        {
            if (report == null) return;
            foreach (var root in scene.GetRootGameObjects())
            foreach (var sim in root.GetComponentsInChildren<UnityEngine.XR.Interaction.Toolkit.Inputs.Simulation.XRInteractionSimulator>(true))
                Object.DestroyImmediate(sim.gameObject);
        }
    }
}
