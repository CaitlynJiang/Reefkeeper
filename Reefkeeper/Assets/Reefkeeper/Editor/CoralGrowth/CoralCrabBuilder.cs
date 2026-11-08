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
    /// Red coral + coral crab. Pipeline: coral_crab.py (Blender) exports RedCoral.fbx (3 stages) and
    /// CoralCrab.fbx (rig + Sleep / WakeUp / Walk clips); this builds:
    /// - RedCoral.prefab (like the other corals) with <see cref="CoralCrabSpawn"/>;
    /// - CoralCrab.prefab: the rigged crab, an Animator (Sleep -> WakeUp -> Walk) and <see cref="CoralCrab"/>;
    /// - a test corner in TestScene_UnderWater.
    /// </summary>
    public static class CoralCrabBuilder
    {
        const string CrabFbx = "Assets/Reefkeeper/Meshes/CoralCrab/CoralCrab.fbx";
        const string CrabTexture = "Assets/Reefkeeper/Textures/CoralCrab/CoralCrab.png";
        const string MatDir = "Assets/Reefkeeper/Materials/CoralCrab";
        const string AnimDir = "Assets/Reefkeeper/Animations/CoralCrab";
        const string PrefabDir = "Assets/Reefkeeper/Prefabs/CoralGrowth";
        const string ScenePath = "Assets/Reefkeeper/Scenes/UnderWater/TestScene_UnderWater.unity";
        const string TestName = "Coral Crab Test 珊瑚蟹测试";
        static readonly string[] Clips = { "Sleep", "WakeUp", "Walk" };

        [MenuItem("Reefkeeper/CoralGrowth/5. Build red coral + coral crab - 红珊瑚和珊瑚蟹")]
        public static void BuildAll()
        {
            var crab = BuildCrab();
            var coral = BuildRedCoral(crab);
            if (crab && coral) PlaceInTestScene(coral, crab);
        }

        public static CoralCrab BuildCrab()
        {
            if (!File.Exists(CrabFbx)) { Debug.LogWarning("[CoralCrab] Run coral_crab.py in Blender first."); return null; }
            Directory.CreateDirectory(MatDir);
            Directory.CreateDirectory(AnimDir);
            AssetDatabase.Refresh();
            ConfigureImporter();

            var mat = AssetDatabase.LoadAssetAtPath<Material>($"{MatDir}/CoralCrab.mat");
            if (!mat)
            {
                mat = new Material(Shader.Find("Reefkeeper/UnderWater/Lit"));
                AssetDatabase.CreateAsset(mat, $"{MatDir}/CoralCrab.mat");
                mat.SetFloat("_Wrap", 0.6f);
                mat.SetFloat("_CausticsAmount", 0.7f);
            }
            mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(CrabTexture));
            EditorUtility.SetDirty(mat);

            var controller = BuildController();
            var root = new GameObject("CoralCrab");
            var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(CrabFbx), root.transform);
            PrefabUtility.UnpackPrefabInstance(model, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            model.name = "Model";
            if (!model.TryGetComponent<Animator>(out var animator)) animator = model.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
            foreach (var r in model.GetComponentsInChildren<Renderer>(true)) r.sharedMaterial = mat;
            root.AddComponent<CoralCrab>();
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, $"{PrefabDir}/CoralCrab.prefab");
            Object.DestroyImmediate(root);
            AssetDatabase.SaveAssets();
            Debug.Log("[CoralCrab] Built CoralCrab.prefab.");
            return prefab.GetComponent<CoralCrab>();
        }

        static void ConfigureImporter()
        {
            var imp = (ModelImporter)AssetImporter.GetAtPath(CrabFbx);
            imp.materialImportMode = ModelImporterMaterialImportMode.None;
            imp.importCameras = imp.importLights = false;
            imp.importBlendShapes = false;
            imp.meshCompression = ModelImporterMeshCompression.Off;
            imp.isReadable = false;
            imp.animationType = ModelImporterAnimationType.Generic;
            imp.importAnimation = true;
            imp.SaveAndReimport();
            var clips = imp.defaultClipAnimations;
            foreach (var c in clips)
            {
                string name = Clips.FirstOrDefault(n => c.takeName.EndsWith(n)) ?? c.takeName;
                c.name = name;
                c.loopTime = name != "WakeUp";
            }
            imp.clipAnimations = clips;
            imp.SaveAndReimport();
        }

        static AnimatorController BuildController()
        {
            string path = $"{AnimDir}/CoralCrab.controller";
            AssetDatabase.DeleteAsset(path);
            var ctrl = AnimatorController.CreateAnimatorControllerAtPath(path);
            var sm = ctrl.layers[0].stateMachine;
            var clips = AssetDatabase.LoadAllAssetsAtPath(CrabFbx).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview")).ToList();
            AnimatorState sleep = null, wake = null, walk = null;
            foreach (var name in Clips)
            {
                var clip = clips.FirstOrDefault(c => c.name == name);
                if (!clip) { Debug.LogWarning($"[CoralCrab] Clip '{name}' not found in {CrabFbx}."); continue; }
                var s = sm.AddState(name);
                s.motion = clip;
                if (name == "Sleep") sleep = s;
                if (name == "WakeUp") wake = s;
                if (name == "Walk") walk = s;
            }
            if (sleep != null) sm.defaultState = sleep;
            if (wake != null && walk != null)
            {
                var t = wake.AddTransition(walk);   // one-shot wake-up flows straight into walking
                t.hasExitTime = true;
                t.exitTime = 0.97f;
                t.duration = 0.12f;
            }
            AssetDatabase.SaveAssets();
            return ctrl;
        }

        public static GameObject BuildRedCoral(CoralCrab crab)
        {
            var prefab = CoralGrowthBuilder.BuildPrefab("RedCoral");
            if (!prefab) return null;
            string path = AssetDatabase.GetAssetPath(prefab);
            var root = PrefabUtility.LoadPrefabContents(path);
            if (!root.TryGetComponent<CoralCrabSpawn>(out var spawn)) spawn = root.AddComponent<CoralCrabSpawn>();
            spawn.crabPrefab = crab;
            PrefabUtility.SaveAsPrefabAsset(root, path);
            PrefabUtility.UnloadPrefabContents(root);
            Debug.Log("[CoralCrab] Built RedCoral.prefab.");
            return AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }

        /// <summary>
        /// On the free sand right of the coral-fusion test: the three stages in a row, a red coral that
        /// grows by itself and always turns into a crab (chance 1), and a crab sleeping on its own.
        /// </summary>
        public static void PlaceInTestScene(GameObject coral, CoralCrab crab)
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path != ScenePath)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }
            foreach (var r in scene.GetRootGameObjects())
                if (r.name == TestName) Object.DestroyImmediate(r);

            var group = new GameObject(TestName).transform;
            group.position = new Vector3(5.2f, 0f, -5.5f);
            var facing = Quaternion.Euler(0f, 180f, 0f);   // models face the scene camera like the showcase

            var stages = new GameObject("Stages 各阶段").transform;
            stages.SetParent(group, false);
            for (int s = 0; s < 3; s++)
            {
                var inst = (GameObject)PrefabUtility.InstantiatePrefab(coral, stages);
                inst.name = $"RedCoral S{s + 1}";
                inst.transform.SetLocalPositionAndRotation(new Vector3(-0.8f + s * 0.8f, 0f, 0.9f), facing);
                inst.GetComponent<CoralGrowth>().stage = s;
                inst.GetComponent<CoralGrowth>().SetStage(s, false);
                inst.GetComponent<CoralCrabSpawn>().chance = 0f;
            }

            var live = new GameObject("Live 自动生长 (always a crab; C = crab now)").transform;
            live.SetParent(group, false);
            var showcase = live.gameObject.AddComponent<CoralGrowthShowcase>();
            showcase.secondsPerStage = 3f;
            var grower = (GameObject)PrefabUtility.InstantiatePrefab(coral, live);
            grower.name = "RedCoral (live)";
            grower.transform.SetLocalPositionAndRotation(new Vector3(-0.4f, 0f, 0f), facing);
            grower.GetComponent<CoralCrabSpawn>().chance = 1f;

            var sleeper = (GameObject)PrefabUtility.InstantiatePrefab(crab.gameObject, group);
            sleeper.name = "CoralCrab (sleeps; wakes after 4 s)";
            sleeper.transform.SetLocalPositionAndRotation(new Vector3(0.6f, 0f, 0f), facing);
            sleeper.GetComponent<CoralCrab>().sleepSeconds = 4f;

            // the fly camera's F key cycles through the test corners
            var fly = Camera.main ? Camera.main.GetComponent<FlyCamera>() : null;
            if (fly)
            {
                var points = fly.focusPoints.Where(p => p && p.name != TestName).ToList();
                var fusion = scene.GetRootGameObjects().FirstOrDefault(g => g.name.StartsWith("Coral Fusion Test"));
                if (fusion && !points.Contains(fusion.transform)) points.Insert(0, fusion.transform);
                points.Add(group);
                fly.focusPoints = points.ToArray();
                EditorUtility.SetDirty(fly);
            }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[CoralCrab] Placed the test corner at {group.position}.");
        }
    }
}
