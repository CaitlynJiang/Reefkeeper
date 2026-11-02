using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.XR.Management;

namespace Reefkeeper.V2
{
    /// <summary>
    /// PICO (Android) setup for Reefkeeper V2, following the PICO Unity Integration SDK's project
    /// validation: PICO XR loader on Android, IL2CPP + ARM64, API 29+, Activity entry, Vulkan,
    /// multithreaded rendering, ASTC, no HDR on the Android URP asset, PXR_Manager on the XR rig and
    /// the XR Interaction Simulator kept out of builds. Then builds Builds/Reefkeeper_V2_PICO.apk.
    /// </summary>
    public static class PicoSetup
    {
        public const string ScenePath = "Assets/Reefkeeper/Scenes/ShallowReef_V2.unity";
        public const string ApkPath = "Builds/Reefkeeper_V2_PICO.apk";
        const string PackageId = "com.reefkeeper.v2";
        const string PicoLoader = "Unity.XR.PXR.PXR_Loader";

        [MenuItem("Reefkeeper/PICO/1. Configure project for PICO - 配置 PICO")]
        public static void Configure()
        {
            var android = NamedBuildTarget.Android;
            PlayerSettings.SetApplicationIdentifier(android, PackageId);
            PlayerSettings.SetScriptingBackend(android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel29;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
            PlayerSettings.Android.applicationEntry = AndroidApplicationEntry.Activity;
            PlayerSettings.Android.preferredInstallLocation = AndroidPreferredInstallLocation.Auto;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.Vulkan });
            PlayerSettings.SetMobileMTRendering(android, true);
            PlayerSettings.graphicsJobs = false;
            PlayerSettings.use32BitDisplayBuffer = true;
            PlayerSettings.runInBackground = true;
            EditorUserBuildSettings.androidBuildSubtarget = MobileTextureSubtarget.ASTC;

            EnablePicoLoader();
            NoHdrOnAndroidPipelines();
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            PrepareScene();
            AssetDatabase.SaveAssets();
            Debug.Log("[PICO] Project configured for PICO.");
        }

        static void EnablePicoLoader()
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
            if (!general.Manager.activeLoaders.Any(l => l && l.GetType().FullName == PicoLoader))
                XRPackageMetadataStore.AssignLoader(general.Manager, PicoLoader, BuildTargetGroup.Android);
            EditorUtility.SetDirty(general);
            EditorUtility.SetDirty(perTarget);

            // The PICO manifest step reads its settings from this config key; without it the build
            // throws half-way through writing the PICO meta-data. The platform and project settings
            // assets are created on first access, which must not first happen mid-build.
            var pxrSettings = AssetDatabase.FindAssets("t:PXR_Settings").Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<Unity.XR.PXR.PXR_Settings>).FirstOrDefault(s => s);
            if (pxrSettings) EditorBuildSettings.AddConfigObject("Unity.XR.PXR.Settings", pxrSettings, true);
            _ = Unity.XR.PXR.PXR_PlatformSetting.Instance;
            _ = Unity.XR.PXR.PXR_ProjectSetting.GetProjectConfig();
        }

        // PICO requires HDR off with URP on device.
        static void NoHdrOnAndroidPipelines()
        {
            var assets = new System.Collections.Generic.List<RenderPipelineAsset>();
            QualitySettings.GetAllRenderPipelineAssetsForPlatform("Android", ref assets);
            foreach (var urp in assets.OfType<UniversalRenderPipelineAsset>().Distinct())
            {
                urp.supportsHDR = false;
                EditorUtility.SetDirty(urp);
            }
        }

        static void PrepareScene()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var roots = scene.GetRootGameObjects();
            var origin = roots.SelectMany(r => r.GetComponentsInChildren<Unity.XR.CoreUtils.XROrigin>(true)).FirstOrDefault();
            var managerType = TypeCache.GetTypesDerivedFrom<MonoBehaviour>().FirstOrDefault(t => t.FullName == "Unity.XR.PXR.PXR_Manager");
            if (origin && managerType != null && !origin.GetComponent(managerType)) origin.gameObject.AddComponent(managerType);
            // The desktop XR simulator must not ship: EditorOnly objects are stripped from builds.
            foreach (var root in roots)
            foreach (var sim in root.GetComponentsInChildren<UnityEngine.XR.Interaction.Toolkit.Inputs.Simulation.XRInteractionSimulator>(true))
                sim.gameObject.tag = "EditorOnly";
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        [MenuItem("Reefkeeper/PICO/2. Build PICO APK - 打包 APK")]
        public static void BuildApk()
        {
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
            {
                Debug.LogError("[PICO] Switch the platform to Android first (File > Build Profiles).");
                return;
            }
            Configure();
            Directory.CreateDirectory(Path.GetDirectoryName(ApkPath));
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = ApkPath,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = BuildOptions.None,
            });
            var s = report.summary;
            string result = $"{s.result} | {s.totalSize / (1024f * 1024f):F1} MB | {s.totalTime:mm\\:ss} | errors {s.totalErrors} | {Path.GetFullPath(ApkPath)}";
            File.WriteAllText("Builds/last-build.txt", result + "\n" + string.Join("\n",
                report.steps.SelectMany(st => st.messages).Where(m => m.type == LogType.Error || m.type == LogType.Exception).Select(m => m.content).Take(20)));
            Debug.Log("[PICO] Build " + result);
        }
    }
}
