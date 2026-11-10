using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Reefkeeper.Hands
{
    /// <summary>PICO build of the V3 scene with hand tracking (controllers and hands).</summary>
    public static class HandTrackingPicoBuild
    {
        public const string ApkPath = "Builds/Reefkeeper_V3_Hands_PICO.apk";
        const string PackageId = "com.reefkeeper.hands";

        [MenuItem("Reefkeeper/Hands/2. Build V3 hands APK for PICO - 打包手势版")]
        public static void BuildPico()
        {
            V2.PicoSetup.Configure();
            Unity.XR.PXR.PXR_ProjectSetting.GetProjectConfig().handTracking = true;
            Unity.XR.PXR.PXR_ProjectSetting.GetProjectConfig().handTrackingSupportType = Unity.XR.PXR.HandTrackingSupport.ControllersAndHands;
            EditorUtility.SetDirty(Unity.XR.PXR.PXR_ProjectSetting.GetProjectConfig());
            PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Android, PackageId);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(HandTrackingSetup.ScenePath, true) };
            AssetDatabase.SaveAssets();
            Directory.CreateDirectory(Path.GetDirectoryName(ApkPath));
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { HandTrackingSetup.ScenePath },
                locationPathName = ApkPath,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = BuildOptions.None,
            });
            var s = report.summary;
            string result = $"{s.result} | {s.totalSize / (1024f * 1024f):F1} MB | {s.totalTime:mm\\:ss} | errors {s.totalErrors} | {Path.GetFullPath(ApkPath)}";
            File.WriteAllText("Builds/last-hands-build.txt", result + "\n" + string.Join("\n",
                report.steps.SelectMany(st => st.messages).Where(m => m.type == LogType.Error || m.type == LogType.Exception).Select(m => m.content).Take(20)));
            Debug.Log("[Hands] Build " + result);
        }
    }
}
