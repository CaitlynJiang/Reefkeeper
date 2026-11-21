using System.Collections.Generic;
using System.IO;
using System.Linq;
using Reefkeeper.Photos;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Reefkeeper.PhotoTools
{
    /// <summary>
    /// The photo board (D:/blender-5.2.3/projects/photo_board, exported by Blender/PhotoBoard/export_photo_board.py):
    /// - toon materials with the Blender palette (flat colours), the shared toon outline from the cat stall;
    /// - Resources/Photos/PhotoBoard.prefab with ReefPhotoWall wired to the 16 picture quads (species order);
    /// - an instance standing beside the CatStall in the open scene. Other scenes get one at runtime.
    /// </summary>
    public static class PhotoBoardBuilder
    {
        const string ModelPath = "Assets/Reefkeeper/Meshes/PhotoBoard/PhotoBoard.fbx";
        const string MatDir = "Assets/Reefkeeper/Materials/PhotoBoard";
        const string PrefabPath = "Assets/Reefkeeper/Resources/Photos/PhotoBoard.prefab";
        const string OutlinePath = "Assets/Reefkeeper/Materials/CatShop/Toon_Outline.mat";
        public const string InstanceName = "Photo board 照片墙";

        // build_board.py palette (sRGB). Scallops are striped in Blender; here they use their mid tone.
        static readonly Dictionary<string, string> Palette = new Dictionary<string, string>
        {
            ["Pb_wood_l"] = "#C08452", ["Pb_wood"] = "#94603D", ["Pb_wood_d"] = "#6E432B", ["Pb_log"] = "#7A4A30",
            ["Pb_rope"] = "#C9A46E", ["Pb_drift"] = "#B79C80", ["Pb_leaf"] = "#86A86F", ["Pb_leaf_d"] = "#5F8752",
            ["Pb_paper"] = "#FBF6EC", ["Pb_star"] = "#EE8648", ["Pb_star2"] = "#E8677A", ["Pb_fish"] = "#3E83A8",
            ["Pb_fish2"] = "#F59A45", ["Pb_clip_r"] = "#D9534A", ["Pb_clip_b"] = "#4E8FC2", ["Pb_clip_y"] = "#EDBE45",
            ["Pb_ScWhite"] = "#F7D9D2", ["Pb_ScRed"] = "#D66A72", ["Pb_ScCoral"] = "#F2AE9C",
        };

        [MenuItem("Reefkeeper/Photos/Build photo board - 生成照片墙")]
        public static void BuildAndPlace() => Place(Build());

        public static GameObject Build()
        {
            if (!File.Exists(ModelPath)) { Debug.LogWarning("[PhotoBoard] Run Blender/PhotoBoard/export_photo_board.py first."); return null; }
            Directory.CreateDirectory(MatDir);
            Directory.CreateDirectory(Path.GetDirectoryName(PrefabPath));
            AssetDatabase.Refresh();

            var outline = AssetDatabase.LoadAssetAtPath<Material>(OutlinePath);
            var picture = Toon("PhotoBoard_Picture", Color.white);
            var importer = (ModelImporter)AssetImporter.GetAtPath(ModelPath);
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
            importer.bakeAxisConversion = true;
            importer.importCameras = importer.importLights = false;
            importer.animationType = ModelImporterAnimationType.None;
            importer.importAnimation = false;
            importer.meshCompression = ModelImporterMeshCompression.Off;
            importer.isReadable = false;
            importer.SaveAndReimport();
            var names = AssetDatabase.LoadAllAssetsAtPath(ModelPath).OfType<Renderer>()
                .SelectMany(r => r.sharedMaterials).Where(m => m).Select(m => m.name).Distinct().ToList();
            foreach (var name in names)
            {
                Material target = name == "Outline" ? outline
                    : name.StartsWith("Photo_") ? picture
                    : Toon(name, Palette.TryGetValue(name, out var hex) && ColorUtility.TryParseHtmlString(hex, out var c) ? c : Color.white);
                if (target) importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), name), target);
            }
            importer.SaveAndReimport();

            var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath));
            PrefabUtility.UnpackPrefabInstance(model, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            model.name = "Model";
            // The model root keeps its Z-up -> Y-up rotation; turn it so the photos face the root's +Z.
            var board = new GameObject("PhotoBoard");
            model.transform.SetParent(board.transform, false);
            model.transform.localRotation = Quaternion.Euler(0, 180, 0) * model.transform.localRotation;
            var all = board.GetComponentsInChildren<Transform>(true);
            // Board order: strands left to right (S1..S6), photos top to bottom (R1..R3).
            var cards = all.Where(t => System.Text.RegularExpressions.Regex.IsMatch(t.name, @"^Photo_S\d_R\d$"))
                .OrderBy(t => t.name).ToArray();
            var wall = board.AddComponent<ReefPhotoWall>();
            wall.cards = cards;
            wall.slots = cards.Select(c => c.Find(c.name + "_Pic")?.GetComponent<Renderer>()).ToArray();
            if (cards.Length != ReefSpecies.Count) Debug.LogWarning($"[PhotoBoard] {cards.Length} photos on the board for {ReefSpecies.Count} species.");
            foreach (var r in board.GetComponentsInChildren<MeshRenderer>(true))
                r.shadowCastingMode = r.name.StartsWith("Pb_Post") || r.name.StartsWith("Pb_Beam") || r.name.StartsWith("Pb_Foot")
                    ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
            // Solid frame so the player can't swim through it (and can hold onto it).
            foreach (var t in all.Where(t => t.name.StartsWith("Pb_Post") || t.name == "Pb_Beam" || t.name.StartsWith("Pb_Foot") || t.name == "Pb_Crossbar"))
                if (t.TryGetComponent<MeshFilter>(out var mf)) t.gameObject.AddComponent<BoxCollider>().center = mf.sharedMesh.bounds.center;
            var prefab = PrefabUtility.SaveAsPrefabAsset(board, PrefabPath);
            Object.DestroyImmediate(board);
            AssetDatabase.SaveAssets();
            Debug.Log($"[PhotoBoard] Built {PrefabPath} with {cards.Length} photo slots.");
            return prefab;
        }

        /// <summary>Puts one board beside the CatStall in the open scene (replacing an earlier one).</summary>
        public static void Place(GameObject prefab)
        {
            if (!prefab) return;
            var scene = EditorSceneManager.GetActiveScene();
            var stall = scene.GetRootGameObjects().FirstOrDefault(r => r.name == "CatStall");
            if (!stall) { Debug.LogWarning($"[PhotoBoard] No CatStall in {scene.path}; the board will be added at runtime where there is one."); return; }
            foreach (var r in scene.GetRootGameObjects()) if (r.name == InstanceName) Object.DestroyImmediate(r);
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            inst.name = InstanceName;
            ReefPhotoWall.PlaceBeside(inst.transform, stall.transform);
            EditorSceneManager.MarkSceneDirty(scene);
            Selection.activeGameObject = inst;
            Debug.Log($"[PhotoBoard] Placed beside the cat stall in {scene.path} (save the scene to keep it).");
        }

        static Material Toon(string name, Color color)
        {
            string path = $"{MatDir}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!mat)
            {
                mat = new Material(Shader.Find("Reefkeeper/UnderWater/Toon"));
                AssetDatabase.CreateAsset(mat, path);
                mat.SetColor("_BaseColor", color);
                if (name == "PhotoBoard_Picture") mat.SetFloat("_CausticsAmount", .15f);
                EditorUtility.SetDirty(mat);
            }
            return mat;
        }
    }
}
