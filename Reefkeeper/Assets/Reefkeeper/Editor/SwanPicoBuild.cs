using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ByteDance.PICO.IconConfigurator.Editor;
using ByteDance.PICO.XR;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Reefkeeper.Editor
{
    public static class SwanPicoBuild
    {
        public const string ApkPath = "Builds/Reefkeeper_Swan_GripOffsetsOnly_2026-11-19.apk";

        [MenuItem("Reefkeeper/Hands/Build and Run Hand Skeleton on PICO")]
        public static void BuildAndRunHandSkeleton()
        {
            const string path = "Builds/Reefkeeper_Swan_HandSkeleton_PICO.apk";
            Directory.CreateDirectory("Builds");
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ImmersiveOpeningBuilder.Opening },
                locationPathName = path,
                target = BuildTarget.Android,
                options = BuildOptions.AutoRunPlayer
            });
            if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
                throw new InvalidOperationException("PICO hand skeleton build: " + report.summary.result);
        }

        public static void Configure()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Exit Play mode first.");
            EditorSceneManager.OpenScene(ImmersiveOpeningBuilder.Opening, OpenSceneMode.Single);
            OpeningPicoBuild.Configure();
            var settings = AssetDatabase.FindAssets("t:PXR_Settings")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<PXR_Settings>).First(s => s);
            settings.appMode = PXR_Settings.AppMode.XR;
            EditorUtility.SetDirty(settings);

            var store = new IconConfiguratorStateStore();
            var config = store.LoadOrCreateConfigAsset();
            config.LastMode = IconConfiguratorMode.Manual;
            var kinds = new[] { IconLayerKind.Background, IconLayerKind.Foreground1, IconLayerKind.Foreground2 };
            var layers = new List<IconLayerConfig>();
            for (int i = 0; i < kinds.Length; i++)
            {
                var source = Path.GetFullPath($"../ArtSource/AppIcon/LuckyDive_UserLayers/Layer{i + 1}.png");
                var layer = ImportUserLayer(source, kinds[i]);
                layers.Add(layer);
            }
            config.Manual.Layers = layers;
            // Lucky Dive.Co is the shop name; retain the approved application name.
            config.Localizations = new List<LocalizationEntry> {
                new LocalizationEntry { LocaleCode = "en-US", AppName = PlayerSettings.productName,
                    IsDefault = true, CanRemove = false }
            };
            store.Save();
            var apply = new IconApplyService(new IconConfiguratorValidator(),
                new IconCompositePreviewService(), new List<IIconExportAdapter> {
                    new LayeredIconExportAdapter(), new AndroidAppNameExportAdapter(),
                    new AndroidManifestExportAdapter()
                });
            var preflight = apply.CreatePreflight(config, store.GetConfigGuid());
            Debug.Log("[Swan icons] Writes: " + string.Join(", ", preflight.PlannedWritePaths));
            if (preflight.DeletePaths.Count != 0)
                throw new InvalidOperationException("Unexpected icon cleanup: " + string.Join(", ", preflight.DeletePaths));
            apply.Apply(config, store.GetConfigGuid());
            AssetDatabase.SaveAssets();
            Validate();
        }

        private static IconLayerConfig ImportUserLayer(string source, IconLayerKind kind)
        {
            // The SDK 6.2 normalizer samples UV 1 on a repeating texture, duplicating
            // bottom water at the top. These already-square PNGs need no normalization.
            const string folder = "Assets/IconConfigurator/Imported";
            string target = folder + "/LuckyDive_" + Path.GetFileName(source);
            byte[] bytes = File.ReadAllBytes(source);
            File.WriteAllBytes(target, bytes);
            AssetDatabase.ImportAsset(target, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(target);
            importer.textureType = TextureImporterType.Default;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.isReadable = true;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 2048;
            importer.SaveAndReimport();
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(target);
            if (texture.width != texture.height) throw new InvalidOperationException("Expected square icon layers.");
            string hash;
            using (var sha = System.Security.Cryptography.SHA256.Create())
                hash = BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
            return new IconLayerConfig {
                LayerKind = kind, AssetGuid = AssetDatabase.AssetPathToGUID(target), AssetPath = target,
                OriginalFileName = Path.GetFileName(source), ContentHash = hash,
                DisplayName = Path.GetFileNameWithoutExtension(source),
                SourceWidth = texture.width, SourceHeight = texture.height, Texture = texture
            };
        }

        public static void Validate()
        {
            var roots = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
            var transforms = roots.SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToArray();
            var missing = transforms.Where(t => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject) > 0)
                .Select(t => t.name).ToArray();
            if (missing.Length > 0) throw new InvalidOperationException("Missing scene scripts: " + string.Join(", ", missing));
            if (transforms.Count(t => t.GetComponent<PXR_Manager>()) != 1)
                throw new InvalidOperationException("Expected one PICO manager.");
            if (transforms.Count(t => t.GetComponent<Unity.XR.CoreUtils.XROrigin>()) != 1)
                throw new InvalidOperationException("Expected one XR Origin.");
            if (!PXR_ProjectSetting.GetProjectConfig().handTracking)
                throw new InvalidOperationException("Hand tracking is disabled.");
            Debug.Log("[Swan validation] No missing scene scripts; one XR Origin and PICO manager; hand tracking enabled.");
        }

        public static void Build()
        {
            Configure();
            // Test APK: use the same local Android debug identity as earlier prototypes.
            // Restore publishing settings afterward; this is not a store-release signing setup.
            var keyPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".android/debug.keystore");
            if (!File.Exists(keyPath)) throw new FileNotFoundException("Android debug keystore missing", keyPath);
            bool previousCustom = PlayerSettings.Android.useCustomKeystore;
            string previousPath = PlayerSettings.Android.keystoreName;
            string previousAlias = PlayerSettings.Android.keyaliasName;
            string previousStorePass = PlayerSettings.Android.keystorePass;
            string previousAliasPass = PlayerSettings.Android.keyaliasPass;
            try
            {
                PlayerSettings.Android.useCustomKeystore = true;
                PlayerSettings.Android.keystoreName = keyPath;
                PlayerSettings.Android.keyaliasName = "androiddebugkey";
                PlayerSettings.Android.keystorePass = "android";
                PlayerSettings.Android.keyaliasPass = "android";
                OpeningPicoBuild.Build();
                File.Copy(OpeningPicoBuild.ApkPath, ApkPath, true);
            }
            finally
            {
                PlayerSettings.Android.useCustomKeystore = previousCustom;
                PlayerSettings.Android.keystoreName = previousPath;
                PlayerSettings.Android.keyaliasName = previousAlias;
                PlayerSettings.Android.keystorePass = previousStorePass;
                PlayerSettings.Android.keyaliasPass = previousAliasPass;
                AssetDatabase.SaveAssets();
            }
            Debug.Log("[Swan APK] " + Path.GetFullPath(ApkPath));
        }
    }
}
