using UnityEditor;

namespace Reefkeeper.CoralGrowthTools
{
    /// <summary>Import rules for the Blender-generated coral meshes and baked textures.</summary>
    sealed class CoralGrowthImport : AssetPostprocessor
    {
        public const string MeshDir = "Assets/Reefkeeper/Meshes/CoralGrowth/";
        public const string TextureDir = "Assets/Reefkeeper/Textures/CoralGrowth/";

        void OnPreprocessModel()
        {
            if (!assetPath.StartsWith(MeshDir)) return;
            var m = (ModelImporter)assetImporter;
            m.materialImportMode = ModelImporterMaterialImportMode.None; // materials come from the builder
            m.importAnimation = false;
            m.importCameras = false;
            m.importLights = false;
            m.importBlendShapes = false;
            m.animationType = ModelImporterAnimationType.None;
            m.globalScale = 1f;
            m.useFileScale = true;
            m.bakeAxisConversion = true;
            m.addCollider = false;
            m.isReadable = false;
            m.meshCompression = ModelImporterMeshCompression.Off;
            m.importNormals = ModelImporterNormals.Import;
        }

        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(TextureDir)) return;
            var t = (TextureImporter)assetImporter;
            t.sRGBTexture = true;
            t.mipmapEnabled = true;
            t.wrapMode = UnityEngine.TextureWrapMode.Clamp;
            // Coral stages are small (512); rack bases carry moss detail over a large surface.
            t.maxTextureSize = assetPath.Contains("/RackBases/") || assetPath.Contains("/Shipwreck/") ? 2048 : assetPath.Contains("DiscCoral") ? 1024 : 512;
        }
    }
}
