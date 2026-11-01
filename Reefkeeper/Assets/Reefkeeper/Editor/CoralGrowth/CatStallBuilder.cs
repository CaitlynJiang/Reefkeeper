using System.IO;
using System.Linq;
using Reefkeeper.Corals;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Reefkeeper.CoralGrowthTools
{
    /// <summary>
    /// The market stall (D:/blender-5.2.3/projects/stall) with the lucky-cat shopkeeper
    /// (D:/blender-5.2.3/projects/lucky_cat), exported by Blender/export_toon_models.py:
    /// - toon + outline materials remapped onto both FBX files by name;
    /// - LuckyCat.prefab: the rigged cat, an Animator with one state per mood clip, LuckyCat.cs;
    /// - CatStall.prefab: the stall (collider), the cat behind the counter, empty markers for the
    ///   basket and the wheelbarrow, and the sand/sea diorama base (off by default).
    /// Placed in TestScene_UnderWater. In Play mode, keys 1-7 preview the moods.
    /// </summary>
    public static class CatStallBuilder
    {
        const string MeshDir = "Assets/Reefkeeper/Meshes/CatShop/";
        const string TexDir = "Assets/Reefkeeper/Textures/CatShop/";
        const string MatDir = "Assets/Reefkeeper/Materials/CatShop";
        const string PrefabDir = "Assets/Reefkeeper/Prefabs/CatShop";
        const string AnimDir = "Assets/Reefkeeper/Animations/CatShop";
        const string ScenePath = "Assets/Reefkeeper/Scenes/UnderWater/TestScene_UnderWater.unity";
        const string InstanceName = "Cat Stall 小猫商铺";
        static readonly string[] Moods = { "Idle", "Happy", "Beckon", "Surprised", "Sleepy", "Angry", "Sad" };

        [MenuItem("Reefkeeper/CoralGrowth/9. Build cat stall - 生成小猫商铺")]
        public static void BuildAll() => Place(Build());

        public static GameObject Build()
        {
            foreach (var d in new[] { MatDir, PrefabDir, AnimDir }) Directory.CreateDirectory(d);
            AssetDatabase.Refresh();
            string stallPath = MeshDir + "Stall.fbx", catPath = MeshDir + "LuckyCat.fbx";
            if (!File.Exists(stallPath) || !File.Exists(catPath)) { Debug.LogWarning("[CatStall] Run export_toon_models.py first."); return null; }

            // ---- materials, remapped by name onto the FBX files
            var stallToon = Toon("Stall_Toon", "Stall");
            var baseToon = Toon("Stall_Base_Toon", "Stall_Base");
            var catToon = Toon("LuckyCat_Toon", "LuckyCat");
            var line = Outline("Toon_Outline");
            ConfigureModel(stallPath, false, ("Stall_Toon", stallToon), ("Stall_Outline", line), ("Stall_Base_Toon", baseToon), ("Stall_Base_Outline", line));
            ConfigureModel(catPath, true, ("LuckyCat_Toon", catToon), ("LuckyCat_Outline", line),
                ("LuckyCat_Glass", Glass("LuckyCat_HelmetGlass")), ("LuckyCat_Brass", Brass("LuckyCat_HelmetBrass")));

            // ---- the cat: Animator with one state per mood
            var controller = BuildController(catPath);
            var cat = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(catPath));
            PrefabUtility.UnpackPrefabInstance(cat, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            cat.name = "LuckyCat";
            if (!cat.TryGetComponent<Animator>(out var animator)) animator = cat.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
            foreach (var r in cat.GetComponentsInChildren<MeshRenderer>(true)) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            cat.AddComponent<LuckyCat>().startMood = LuckyCat.Mood.Idle;      // idle is the default loop of the prefab
            var catPrefab = PrefabUtility.SaveAsPrefabAsset(cat, $"{PrefabDir}/LuckyCat.prefab");
            Object.DestroyImmediate(cat);

            // ---- the stall with the cat behind the counter
            var stall = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(stallPath));
            PrefabUtility.UnpackPrefabInstance(stall, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            stall.name = "CatStall";
            var all = stall.GetComponentsInChildren<Transform>(true);
            var body = all.FirstOrDefault(t => t.name == "Stall");
            if (body && body.TryGetComponent<MeshFilter>(out var mf)) body.gameObject.AddComponent<MeshCollider>().sharedMesh = mf.sharedMesh;
            // the awning sways in the current: its own Toon + Outline materials with matching sway settings
            var awning = all.FirstOrDefault(t => t.name == "Stall_Awning");
            if (awning && awning.TryGetComponent<MeshRenderer>(out var ar))
            {
                var awnToon = Toon("Stall_Awning_Toon", "Stall");
                var awnLine = Outline("Toon_Outline_Sway");
                SyncSway(awnToon, awnLine);
                ar.sharedMaterials = ar.sharedMaterials.Select(m => m && m.shader == line.shader ? awnLine : awnToon).ToArray();
            }
            var baseT = all.FirstOrDefault(t => t.name == "Stall_Base");
            if (baseT) { baseT.name = "Stall_Base 沙盘底座 (optional)"; baseT.gameObject.SetActive(false); }
            foreach (var t in all)
            {
                if (t.name == "Socket_Basket") t.name = "Basket 道具篮 (empty)";
                if (t.name == "Socket_Cart") t.name = "Cart 珊瑚推车 (empty)";
            }
            var anchor = all.FirstOrDefault(t => t.name == "Cat_Anchor");
            var c = (GameObject)PrefabUtility.InstantiatePrefab(catPrefab, stall.transform);
            // keep the cat model's own root rotation (Blender Z-up -> Unity Y-up)
            c.transform.SetPositionAndRotation(anchor ? anchor.position : stall.transform.position,
                stall.transform.rotation * catPrefab.transform.rotation);
            var prefab = PrefabUtility.SaveAsPrefabAsset(stall, $"{PrefabDir}/CatStall.prefab");
            Object.DestroyImmediate(stall);
            AssetDatabase.SaveAssets();
            Debug.Log("[CatStall] Built LuckyCat.prefab and CatStall.prefab.");
            return prefab;
        }

        static void ConfigureModel(string path, bool animated, params (string name, Material mat)[] remaps)
        {
            var imp = (ModelImporter)AssetImporter.GetAtPath(path);
            imp.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
            foreach (var (name, mat) in remaps)
                imp.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), name), mat);
            imp.importCameras = imp.importLights = false;
            imp.meshCompression = ModelImporterMeshCompression.Off;
            imp.isReadable = false;
            if (animated)
            {
                imp.animationType = ModelImporterAnimationType.Generic;
                imp.importAnimation = true;
                var clips = imp.defaultClipAnimations;
                foreach (var c in clips)
                {
                    string mood = Moods.FirstOrDefault(m => c.takeName.EndsWith(m)) ?? c.takeName;
                    c.name = mood;
                    c.loopTime = mood != "Surprised";
                }
                imp.clipAnimations = clips;
            }
            else
            {
                imp.animationType = ModelImporterAnimationType.None;
                imp.importAnimation = false;
            }
            imp.SaveAndReimport();
        }

        static AnimatorController BuildController(string catPath)
        {
            string path = $"{AnimDir}/LuckyCat.controller";
            AssetDatabase.DeleteAsset(path);
            var ctrl = AnimatorController.CreateAnimatorControllerAtPath(path);
            var sm = ctrl.layers[0].stateMachine;
            var clips = AssetDatabase.LoadAllAssetsAtPath(catPath).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview")).ToList();
            AnimatorState idle = null, surprised = null;
            foreach (var mood in Moods)
            {
                var clip = clips.FirstOrDefault(c => c.name == mood);
                if (!clip) { Debug.LogWarning($"[CatStall] Clip '{mood}' not found in {catPath}."); continue; }
                var s = sm.AddState(mood);
                s.motion = clip;
                if (mood == "Idle") idle = s;
                if (mood == "Surprised") surprised = s;
            }
            if (idle != null) sm.defaultState = idle;
            if (idle != null && surprised != null)
            {
                var t = surprised.AddTransition(idle);     // one-shot: back to idle when it ends
                t.hasExitTime = true;
                t.exitTime = 0.95f;
                t.duration = 0.2f;
            }
            AssetDatabase.SaveAssets();
            return ctrl;
        }

        static Material Toon(string name, string texture)
        {
            string path = $"{MatDir}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!mat)
            {
                mat = new Material(Shader.Find("Reefkeeper/UnderWater/Toon"));
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(TexDir + texture + ".png"));
            EditorUtility.SetDirty(mat);
            return mat;
        }

        /// <summary>
        /// First build: gentle sway defaults on the awning cloth. Afterwards tune the Toon material in the
        /// inspector; every rebuild copies its sway settings onto the outline so the two never drift apart.
        /// </summary>
        static void SyncSway(Material cloth, Material outline)
        {
            if (cloth.GetFloat("_SwayAmount") <= 0f && cloth.GetFloat("_SwayLift") <= 0f)
            {
                cloth.SetFloat("_SwayAmount", 0.05f);
                cloth.SetFloat("_SwayLift", 0.045f);
                cloth.SetFloat("_SwaySpeed", 1.1f);
                cloth.SetFloat("_Flutter", 0.008f);
                cloth.SetVector("_SwayDir", new Vector4(0.35f, 0.2f, 0f, 0f));
                EditorUtility.SetDirty(cloth);
            }
            foreach (var p in new[] { "_SwayAmount", "_SwayLift", "_SwaySpeed", "_Flutter" }) outline.SetFloat(p, cloth.GetFloat(p));
            outline.SetVector("_SwayDir", cloth.GetVector("_SwayDir"));
            EditorUtility.SetDirty(outline);
        }

        /// <summary>Transparent bubble glass for the diving helmet (created once, tune freely).</summary>
        static Material Glass(string name)
        {
            string path = $"{MatDir}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!mat)
            {
                mat = new Material(Shader.Find("Reefkeeper/UnderWater/Glass"));
                AssetDatabase.CreateAsset(mat, path);
            }
            return mat;
        }

        /// <summary>Flat brass for the helmet's neck ring, same cel shading as the rest of the cat.</summary>
        static Material Brass(string name)
        {
            string path = $"{MatDir}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!mat)
            {
                mat = new Material(Shader.Find("Reefkeeper/UnderWater/Toon"));
                AssetDatabase.CreateAsset(mat, path);
                mat.SetColor("_BaseColor", new Color(0.86f, 0.64f, 0.27f));
            }
            return mat;
        }

        static Material Outline(string name)
        {
            string path = $"{MatDir}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!mat)
            {
                mat = new Material(Shader.Find("Reefkeeper/UnderWater/Outline"));
                AssetDatabase.CreateAsset(mat, path);
            }
            return mat;
        }

        public static void Place(GameObject prefab)
        {
            if (!prefab) return;
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path != ScenePath)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }
            foreach (var r in scene.GetRootGameObjects())
                if (r.name == InstanceName) Object.DestroyImmediate(r);
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            inst.name = InstanceName;
            inst.transform.SetPositionAndRotation(new Vector3(-5f, 0f, 11f), Quaternion.Euler(0f, 155f, 0f));   // open sand, facing the middle of the scene
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Selection.activeGameObject = inst;
            Debug.Log($"[CatStall] Placed in {ScenePath}.");
        }
    }
}
