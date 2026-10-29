using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Reefkeeper.Corals;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Reefkeeper.CoralGrowthTools
{
    /// <summary>
    /// Turns the Blender exports into prefabs and lays them out in TestScene_UnderWater.
    /// Pipeline: coral_generator.py (Blender) -> Meshes/Textures CoralGrowth -> this builder.
    /// </summary>
    public static class CoralGrowthBuilder
    {
        const string MaterialDir = "Assets/Reefkeeper/Materials/CoralGrowth";
        const string PrefabDir = "Assets/Reefkeeper/Prefabs/CoralGrowth";
        const string GeneratorPath = "Assets/Reefkeeper/Editor/CoralGrowth/Blender/coral_generator.py";
        const string ScenePath = "Assets/Reefkeeper/Scenes/UnderWater/TestScene_UnderWater.unity";
        const string ShowcaseName = "Coral Growth Showcase 珊瑚生长展示";
        const string BlenderPathKey = "Reefkeeper.CoralGrowth.BlenderPath";
        const string DefaultBlender = @"D:\blender-5.2.3\blender-5.2.3-candidate+v52.c361aec8a863-windows.amd64-release\blender.exe";

        // Display order in the showcase.
        static readonly string[] Models =
        {
            "DiscCoral", "BranchCoral", "BarrelSponge", "PolypPillar_Pink", "PolypPillar_Coral",
            "FingerCoral", "TubeSponge_Magenta", "TubeSponge_Lemon", "TubeSponge_Blossom", "TendrilCoral",
        };

        // Seaweed: no growth stages, sways with the current. (name, tip sway in m, typical height in m)
        static readonly (string name, float sway, float height)[] Seaweeds =
        {
            ("Seaweed_Kelp", 0.16f, 0.95f), ("Seaweed_Grass", 0.06f, 0.35f),
            ("Seaweed_Leafy", 0.05f, 0.35f), ("Seaweed_Feather", 0.05f, 0.38f),
        };
        const string SeaweedName = "Seaweed 水草";

        [MenuItem("Reefkeeper/CoralGrowth/1. Regenerate models in Blender - 用 Blender 重新生成模型")]
        public static void RegenerateInBlender()
        {
            string blender = EditorPrefs.GetString(BlenderPathKey, DefaultBlender);
            if (!File.Exists(blender))
            {
                blender = EditorUtility.OpenFilePanel("Locate blender.exe 选择 blender.exe", "", "exe");
                if (string.IsNullOrEmpty(blender)) return;
                EditorPrefs.SetString(BlenderPathKey, blender);
            }
            var psi = new ProcessStartInfo(blender,
                $"-b --factory-startup --python \"{Path.GetFullPath(GeneratorPath)}\" -- --preview")
            {
                UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true,
            };
            try
            {
                EditorUtility.DisplayProgressBar("CoralGrowth", "Blender is building corals… 正在生成", 0.5f);
                using var p = Process.Start(psi);
                string output = p.StandardOutput.ReadToEnd();
                p.WaitForExit();
                var lines = output.Split('\n').Where(l => l.StartsWith("[coral]") || l.Contains("Error")).ToArray();
                if (p.ExitCode != 0 || !output.Contains("[coral] done"))
                    Debug.LogError("[CoralGrowth] Blender run failed:\n" + output);
                else
                    Debug.Log("[CoralGrowth] Blender finished:\n" + string.Join("\n", lines));
            }
            finally { EditorUtility.ClearProgressBar(); }
            AssetDatabase.Refresh();
        }

        [MenuItem("Reefkeeper/CoralGrowth/2. Build prefabs + test scene - 生成预制体并布置测试场景")]
        public static void BuildAll()
        {
            var prefabs = BuildPrefabs();
            PlaceInTestScene(prefabs);
            PlaceSeaweed(BuildSeaweedPrefabs());
        }

        [MenuItem("Reefkeeper/CoralGrowth/4. Build seaweed - 生成水草")]
        public static void BuildSeaweed() => PlaceSeaweed(BuildSeaweedPrefabs());

        public static Dictionary<string, GameObject> BuildSeaweedPrefabs()
        {
            string matDir = MaterialDir + "/Seaweed", prefabDir = PrefabDir + "/Seaweed";
            Directory.CreateDirectory(matDir);
            Directory.CreateDirectory(prefabDir);
            AssetDatabase.Refresh();
            var shader = Shader.Find("Reefkeeper/UnderWater/Lit Sway");
            if (!shader) throw new System.Exception("Shader 'Reefkeeper/UnderWater/Lit Sway' not found.");

            var result = new Dictionary<string, GameObject>();
            foreach (var (name, sway, height) in Seaweeds)
            {
                string fbx = CoralGrowthImport.MeshDir + "Seaweed/" + name + ".fbx";
                var mesh = AssetDatabase.LoadAllAssetsAtPath(fbx).OfType<Mesh>().FirstOrDefault();
                if (!mesh) { Debug.LogWarning($"[CoralGrowth] Missing {fbx}; run step 1 first."); continue; }

                string matPath = $"{matDir}/{name}.mat";
                var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
                if (!mat)
                {
                    mat = new Material(shader);
                    AssetDatabase.CreateAsset(mat, matPath);
                    mat.SetFloat("_SwayAmount", sway);   // tuned per type once; edit the material freely
                    mat.SetFloat("_SwayHeight", height);
                }
                mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(CoralGrowthImport.TextureDir + "Seaweed/" + name + ".png"));
                EditorUtility.SetDirty(mat);

                var root = new GameObject(name);
                root.AddComponent<MeshFilter>().sharedMesh = mesh;
                root.AddComponent<MeshRenderer>().sharedMaterial = mat;
                result[name] = PrefabUtility.SaveAsPrefabAsset(root, $"{prefabDir}/{name}.prefab");
                Object.DestroyImmediate(root);
            }
            AssetDatabase.SaveAssets();
            return result;
        }

        /// <summary>Clumps of seaweed around the showcase and at the feet of the rack bases.</summary>
        public static void PlaceSeaweed(Dictionary<string, GameObject> prefabs)
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path != ScenePath)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }
            foreach (var r in scene.GetRootGameObjects())
                if (r.name == SeaweedName) Object.DestroyImmediate(r);
            var group = new GameObject(SeaweedName).transform;
            var rng = new System.Random(11);
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);

            void Put(string name, Vector3 at, float scale)
            {
                if (!prefabs.TryGetValue(name, out var prefab) || !prefab) return;
                var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, group);
                inst.transform.SetPositionAndRotation(at, Quaternion.Euler(0, R(0, 360), 0));
                inst.transform.localScale = Vector3.one * scale;
            }
            // tall kelp at the back and the flanks
            for (int i = 0; i < 9; i++) Put("Seaweed_Kelp", new Vector3(R(-5.5f, 5.5f), 0, R(3.0f, 4.5f)), R(0.85f, 1.25f));
            foreach (float side in new[] { -1f, 1f })
                for (int i = 0; i < 4; i++) Put("Seaweed_Kelp", new Vector3(side * R(4.4f, 5.4f), 0, R(-3.5f, 2f)), R(0.8f, 1.2f));
            // low clumps along the edges of the showcase and around the rack bases
            string[] low = { "Seaweed_Grass", "Seaweed_Leafy", "Seaweed_Feather" };
            foreach (float side in new[] { -1f, 1f })
                for (int i = 0; i < 7; i++) Put(low[i % 3], new Vector3(side * R(3.9f, 4.6f), 0, R(-4.2f, 1.5f)), R(0.8f, 1.3f));
            foreach (float x in new[] { -2.3f, 0f, 2.3f })
                for (int i = 0; i < 4; i++)
                {
                    float a = R(0, Mathf.PI * 2), d = R(0.45f, 0.85f);
                    Put(low[((i + (int)(x * 3)) % 3 + 3) % 3], new Vector3(x + Mathf.Cos(a) * d, 0, 1.9f + Mathf.Sin(a) * d), R(0.8f, 1.2f));
                }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[CoralGrowth] Placed {group.childCount} seaweed clumps.");
        }

        public static Dictionary<string, GameObject> BuildPrefabs()
        {
            Directory.CreateDirectory(MaterialDir);
            Directory.CreateDirectory(PrefabDir);
            AssetDatabase.Refresh();
            var shader = Shader.Find("Reefkeeper/UnderWater/Lit");
            if (!shader) throw new System.Exception("Shader 'Reefkeeper/UnderWater/Lit' not found.");

            var result = new Dictionary<string, GameObject>();
            foreach (string model in Models)
            {
                string fbx = CoralGrowthImport.MeshDir + model + ".fbx";
                var meshes = AssetDatabase.LoadAllAssetsAtPath(fbx).OfType<Mesh>()
                    .Where(m => m.name.StartsWith(model + "_S")).OrderBy(m => m.name).ToArray();
                if (meshes.Length == 0) { Debug.LogWarning($"[CoralGrowth] No meshes in {fbx}; run step 1 first."); continue; }

                var modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(fbx);
                var root = new GameObject(model);
                var coral = root.AddComponent<CoralGrowth>();
                var stages = new List<Transform>();
                foreach (var mesh in meshes)
                {
                    var go = new GameObject("Stage " + mesh.name.Substring(mesh.name.LastIndexOf('S') + 1));
                    go.transform.SetParent(root.transform, false);
                    go.AddComponent<MeshFilter>().sharedMesh = mesh;
                    go.AddComponent<MeshRenderer>().sharedMaterial = StageMaterial(shader, mesh.name);
                    stages.Add(go.transform);
                    // planting points exported as children of a stage (e.g. the mature disc coral's top)
                    var src = modelAsset ? modelAsset.transform.Find(mesh.name) : null;
                    if (src)
                        foreach (Transform t in src)
                        {
                            if (!t.name.StartsWith("Socket_")) continue;
                            var s = new GameObject(t.name);
                            s.transform.SetParent(go.transform, false);
                            s.transform.localPosition = modelAsset.transform.InverseTransformPoint(t.position);
                            s.AddComponent<RackSocket>().kind = RackSocket.Kind.Table;
                        }
                }
                coral.stages = stages.ToArray();
                coral.stage = stages.Count - 1;
                for (int i = 0; i < stages.Count; i++) stages[i].gameObject.SetActive(i == coral.stage);

                string path = $"{PrefabDir}/{model}.prefab";
                result[model] = PrefabUtility.SaveAsPrefabAsset(root, path);
                Object.DestroyImmediate(root);
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"[CoralGrowth] Built {result.Count} prefabs in {PrefabDir}.");
            return result;
        }

        static Material StageMaterial(Shader shader, string name)
        {
            string path = $"{MaterialDir}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!mat)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
                mat.SetColor("_BaseColor", Color.white);
                mat.SetFloat("_Wrap", 0.6f);
                mat.SetFloat("_CausticsAmount", 0.7f);
            }
            mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath(name)));
            EditorUtility.SetDirty(mat);
            return mat;
        }

        static string TexturePath(string name) => CoralGrowthImport.TextureDir + name + ".png";

        /// <summary>
        /// Grid in front of the test camera: one column per coral, rows = stages (front = youngest),
        /// plus a back row that grows by itself in Play mode.
        /// </summary>
        public static void PlaceInTestScene(Dictionary<string, GameObject> prefabs)
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path != ScenePath)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }
            foreach (var r in scene.GetRootGameObjects())
                if (r.name == ShowcaseName) Object.DestroyImmediate(r);

            var showcase = new GameObject(ShowcaseName).transform;
            showcase.position = new Vector3(0f, 0f, -3.7f);
            const float columnGap = 0.85f, rowGap = 0.85f;
            int columns = Models.Length;
            float x0 = -(columns - 1) * columnGap / 2f;

            var stagesRoot = new GameObject("Stages 各阶段 (front = youngest)").transform;
            stagesRoot.SetParent(showcase, false);
            var live = new GameObject("Live growth 自动生长 (Play: N grow, P poke)").transform;
            live.SetParent(showcase, false);
            live.localPosition = new Vector3(0, 0, 4 * rowGap + 0.5f);
            live.gameObject.AddComponent<CoralGrowthShowcase>();

            for (int c = 0; c < columns; c++)
            {
                if (!prefabs.TryGetValue(Models[c], out var prefab) || !prefab) continue;
                int count = prefab.GetComponent<CoralGrowth>().StageCount;
                for (int s = 0; s < count; s++)
                {
                    var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, stagesRoot);
                    inst.name = $"{Models[c]} S{s + 1}";
                    inst.transform.localPosition = new Vector3(x0 + c * columnGap, 0, s * rowGap);
                    inst.transform.localRotation = Quaternion.Euler(0, 180f + (c * 37 % 30 - 15), 0);
                    var coral = inst.GetComponent<CoralGrowth>();
                    coral.stage = s;
                    coral.SetStage(s, false);
                }
                var grower = (GameObject)PrefabUtility.InstantiatePrefab(prefab, live);
                grower.name = Models[c] + " (live)";
                grower.transform.localPosition = new Vector3(x0 + c * columnGap, 0, 0);
                grower.transform.localRotation = Quaternion.Euler(0, 180f, 0);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Selection.activeGameObject = showcase.gameObject;
            Debug.Log($"[CoralGrowth] Placed showcase in {ScenePath}.");
        }
    }
}
