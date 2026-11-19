using System;
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

namespace Reefkeeper.Editor
{
    public static class OpeningPicoBuild
    {
        public const string ApkPath="Builds/Reefkeeper_ImmersiveOpening_PICO.apk";
        public static void BuildFromCommandLine()
        {
            EditorSceneManager.OpenScene(ImmersiveOpeningBuilder.Opening,OpenSceneMode.Single);
            Build();
        }
        public static void Configure()
        {
            if(Application.isPlaying)throw new InvalidOperationException("Exit Play mode before configuring the APK.");
            var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if(scene.path!=ImmersiveOpeningBuilder.Opening)throw new InvalidOperationException("Open ImmersiveOpening before configuring.");
            var android=NamedBuildTarget.Android;
            // Retain the existing application identifier so installed prototypes can be updated.
            PlayerSettings.SetScriptingBackend(android,ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures=AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion=AndroidSdkVersions.AndroidApiLevel29;
            PlayerSettings.Android.targetSdkVersion=AndroidSdkVersions.AndroidApiLevelAuto;
            PlayerSettings.Android.applicationEntry=AndroidApplicationEntry.Activity;
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android,false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android,new[]{GraphicsDeviceType.Vulkan});
            PlayerSettings.SetMobileMTRendering(android,true);
            PlayerSettings.graphicsJobs=false;
            EditorUserBuildSettings.androidBuildSubtarget=MobileTextureSubtarget.ASTC;
            EditorUserBuildSettings.buildAppBundle=false;
            EditorBuildSettings.TryGetConfigObject(XRGeneralSettings.k_SettingsKey,out XRGeneralSettingsPerBuildTarget perTarget);
            if(!perTarget)throw new InvalidOperationException("Existing XR settings missing.");
            if(!perTarget.HasSettingsForBuildTarget(BuildTargetGroup.Android))perTarget.CreateDefaultSettingsForBuildTarget(BuildTargetGroup.Android);
            if(!perTarget.HasManagerSettingsForBuildTarget(BuildTargetGroup.Android))perTarget.CreateDefaultManagerSettingsForBuildTarget(BuildTargetGroup.Android);
            var general=perTarget.SettingsForBuildTarget(BuildTargetGroup.Android);
            general.InitManagerOnStart=true;
            const string loader="ByteDance.PICO.XR.PXR_Loader";
            if(!general.Manager.activeLoaders.Any(l=>l&&l.GetType().FullName==loader))
                if(!XRPackageMetadataStore.AssignLoader(general.Manager,loader,BuildTargetGroup.Android))throw new InvalidOperationException("Could not enable PICO loader.");
            EditorUtility.SetDirty(general);EditorUtility.SetDirty(perTarget);EditorUtility.SetDirty(general.Manager);
            var settings=AssetDatabase.FindAssets("t:PXR_Settings").Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.LoadAssetAtPath<ByteDance.PICO.XR.PXR_Settings>).FirstOrDefault(s=>s);
            if(!settings)throw new InvalidOperationException("PICO settings missing.");
            EditorBuildSettings.AddConfigObject("ByteDance.PICO.XR.Settings",settings,true);
            _=ByteDance.PICO.Platform.Framework.PXR_PlatformSetting.Instance;
            var config=ByteDance.PICO.XR.PXR_ProjectSetting.GetProjectConfig();
            config.handTracking=true;config.handTrackingSupportType=ByteDance.PICO.XR.HandTrackingSupport.ControllersAndHands;
            EditorUtility.SetDirty(config);
            var pipelines=new System.Collections.Generic.List<RenderPipelineAsset>();
            QualitySettings.GetAllRenderPipelineAssetsForPlatform("Android",ref pipelines);
            foreach(var pipeline in pipelines.OfType<UniversalRenderPipelineAsset>().Distinct())
            {pipeline.supportsHDR=false;EditorUtility.SetDirty(pipeline);}
            var roots=scene.GetRootGameObjects();
            var origin=roots.SelectMany(r=>r.GetComponentsInChildren<Unity.XR.CoreUtils.XROrigin>(true)).Single();
            if(!origin.GetComponent<ByteDance.PICO.XR.PXR_Manager>())origin.gameObject.AddComponent<ByteDance.PICO.XR.PXR_Manager>();
            foreach(var sim in roots.SelectMany(r=>r.GetComponentsInChildren<UnityEngine.XR.Interaction.Toolkit.Inputs.Simulation.XRInteractionSimulator>(true)))
                sim.gameObject.tag="EditorOnly";
            EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene(ImmersiveOpeningBuilder.Opening,true)};
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
        }

        public static void Build()
        {
            if(EditorUserBuildSettings.activeBuildTarget!=BuildTarget.Android)throw new InvalidOperationException("Switch to Android first.");
            Configure();
            Directory.CreateDirectory("Builds");
            var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{
                scenes=new[]{ImmersiveOpeningBuilder.Opening},locationPathName=ApkPath,
                target=BuildTarget.Android,options=BuildOptions.None
            });
            var summary=report.summary;
            string result=$"{summary.result} | {summary.totalSize} bytes | {summary.totalTime} | errors {summary.totalErrors} | {Path.GetFullPath(ApkPath)}";
            File.WriteAllText("Builds/last-opening-build.txt",result+"\n"+string.Join("\n",report.steps.SelectMany(s=>s.messages).Where(m=>m.type==LogType.Error||m.type==LogType.Exception).Select(m=>m.content)));
            Debug.Log("[Opening PICO] "+result);
            if(summary.result!=BuildResult.Succeeded)throw new InvalidOperationException(result);
        }
    }
}
