using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Reefkeeper.ShopTools
{
    /// <summary>
    /// Lucky Cat's counter props from D:/blender-5.2.3/projects/music_corner (exported by
    /// Blender/MusicCorner/export_music_corner.py): instant camera, record player and record crate.
    /// Builds Resources/Shop/{InstantCamera,RecordPlayer,RecordCrate}.prefab: a root facing +Z with
    /// its base at the origin, toon materials from the Blender colours, glass for the clear parts,
    /// the shared toon outline, and one box collider. The crate is empty (its sleeves are hidden).
    /// LuckyCatProgress puts them on the counter as the game moves on.
    /// </summary>
    public static class CounterPropsBuilder
    {
        const string MeshDir = "Assets/Reefkeeper/Meshes/MusicCorner/";
        const string MatDir = "Assets/Reefkeeper/Materials/MusicCorner";
        const string PrefabDir = "Assets/Reefkeeper/Resources/Shop/";
        const string OutlinePath = "Assets/Reefkeeper/Materials/CatShop/Toon_Outline.mat";

        [MenuItem("Reefkeeper/Shop/Build counter props - 生成柜台道具")]
        public static void Build()
        {
            Directory.CreateDirectory(MatDir);
            Directory.CreateDirectory(PrefabDir);
            AssetDatabase.Refresh();
            Make("MC_2_Polaroid", "InstantCamera", hide: n => n.StartsWith("Polaroid_Photo"));
            Make("MC_1_Turntable", "RecordPlayer", hide: _ => false);
            Make("MC_3_Crate", "RecordCrate", hide: n => n.StartsWith("Sleeve_"));
            AssetDatabase.SaveAssets();
            Debug.Log("[Shop] Built InstantCamera, RecordPlayer and RecordCrate prefabs in " + PrefabDir);
        }

        static void Make(string model, string name, System.Func<string, bool> hide)
        {
            string path = MeshDir + model + ".fbx";
            var importer = (ModelImporter)AssetImporter.GetAtPath(path);
            if (!importer) { Debug.LogWarning("[Shop] Run Blender/MusicCorner/export_music_corner.py first: " + path); return; }
            importer.bakeAxisConversion = true;
            importer.importCameras = importer.importLights = false;
            importer.animationType = ModelImporterAnimationType.None;
            importer.importAnimation = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
            importer.SaveAndReimport();

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path));
            PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            instance.name = "Model";
            var root = new GameObject(name);
            instance.transform.SetParent(root.transform, false);
            // The model root keeps its Z-up -> Y-up rotation; turn it so the front (Blender -Y) faces +Z.
            instance.transform.localRotation = Quaternion.Euler(0, 180, 0) * instance.transform.localRotation;

            foreach (var t in instance.GetComponentsInChildren<Transform>(true))
                if (t != instance.transform && hide(t.name)) t.gameObject.SetActive(false);

            var outline = AssetDatabase.LoadAssetAtPath<Material>(OutlinePath);
            var renderers = instance.GetComponentsInChildren<Renderer>(false);
            foreach (var r in renderers)
            {
                r.sharedMaterials = r.sharedMaterials.Select(m => Convert(m, outline)).ToArray();
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }

            // Base centred on the root's origin.
            var b = new Bounds(renderers[0].bounds.center, Vector3.zero);
            foreach (var r in renderers) b.Encapsulate(r.bounds);
            instance.transform.position -= new Vector3(b.center.x, b.min.y, b.center.z);
            var box = root.AddComponent<BoxCollider>();
            box.center = new Vector3(0, b.size.y * .5f, 0);
            box.size = b.size;

            PrefabUtility.SaveAsPrefabAsset(root, PrefabDir + name + ".prefab");
            Object.DestroyImmediate(root);
        }

        static readonly Dictionary<string, Material> made = new Dictionary<string, Material>();

        /// <summary>Imported PBR colour -> the game's toon shader; clear parts -> underwater glass.</summary>
        static Material Convert(Material source, Material outline)
        {
            if (!source) return source;
            if (source.name == "Outline") return outline;
            string path = $"{MatDir}/{source.name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat) return mat;
            bool clear = source.name.Contains("Glass") || source.name.Contains("Acrylic");
            mat = new Material(Shader.Find(clear ? "Reefkeeper/UnderWater/Glass" : "Reefkeeper/UnderWater/Toon"));
            if (!clear)
            {
                var color = source.HasProperty("_BaseColor") ? source.GetColor("_BaseColor") : Color.white;
                // The vinyl texture is procedural in Blender and doesn't export: a flat record black.
                if (source.name.StartsWith("Record_")) color = new Color(.14f, .13f, .14f);
                mat.SetColor("_BaseColor", color);
            }
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }
    }
}
