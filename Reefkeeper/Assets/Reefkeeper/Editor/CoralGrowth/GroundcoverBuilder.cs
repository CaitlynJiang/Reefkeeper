using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace Reefkeeper.CoralGrowthTools
{
    /// <summary>
    /// Ground cover for terrain painting: parasol clover and fuzzy moss clumps from the Blender
    /// generator, as instanced Terrain detail meshes on the 'Lit Sway' shader (soft sway + fuzz rim).
    /// Paint them with the Terrain tool's Paint Details brush after "Add to selected Terrain".
    /// </summary>
    public static class GroundcoverBuilder
    {
        const string MeshDir = CoralGrowthImport.MeshDir + "Groundcover/";
        const string TextureDir = CoralGrowthImport.TextureDir + "Groundcover/";
        const string MaterialDir = "Assets/Reefkeeper/Materials/CoralGrowth/Groundcover";
        const string PrefabDir = "Assets/Reefkeeper/Prefabs/CoralGrowth/Groundcover";
        const string ScenePath = "Assets/Reefkeeper/Scenes/UnderWater/TestScene_UnderWater.unity";
        const string DemoName = "Groundcover Demo Terrain 地被测试地形";

        // name, tip sway (m), typical height (m), flutter (m), fuzz, wrap, tint (the pale clover needs a peach tint to read on bright sand)
        static readonly (string name, float sway, float height, float flutter, float fuzz, float wrap, Color tint)[] Types =
        {
            ("Ground_Clover", 0.03f, 0.12f, 0.006f, 0.08f, 0.55f, new Color(0.93f, 0.72f, 0.58f)),
            ("Ground_FuzzMoss", 0.012f, 0.08f, 0.004f, 0.55f, 0.85f, Color.white),
        };

        [MenuItem("Reefkeeper/CoralGrowth/6. Add ground cover to selected Terrain - 给选中地形添加地被")]
        static void AddToSelectedTerrain()
        {
            var terrain = Selection.activeGameObject ? Selection.activeGameObject.GetComponent<Terrain>() : null;
            if (!terrain)
            {
                EditorUtility.DisplayDialog("Ground cover", "Select a Terrain first.\n请先选中一个地形。", "OK");
                return;
            }
            AddTo(terrain.terrainData, BuildPrefabs());
            Debug.Log($"[Groundcover] Added to {terrain.name}. Use Paint Details on the Terrain to brush them on.");
        }

        [MenuItem("Reefkeeper/CoralGrowth/7. Build ground cover demo terrain - 生成地被测试地形")]
        public static void BuildDemo() => PlaceDemoTerrain(BuildPrefabs());

        public static Dictionary<string, GameObject> BuildPrefabs()
        {
            Directory.CreateDirectory(MaterialDir);
            Directory.CreateDirectory(PrefabDir);
            AssetDatabase.Refresh();
            var shader = Shader.Find("Reefkeeper/UnderWater/Lit Sway");
            if (!shader) throw new System.Exception("Shader 'Reefkeeper/UnderWater/Lit Sway' not found.");
            var result = new Dictionary<string, GameObject>();
            foreach (var t in Types)
            {
                var mesh = AssetDatabase.LoadAllAssetsAtPath(MeshDir + t.name + ".fbx").OfType<Mesh>().FirstOrDefault();
                if (!mesh) { Debug.LogWarning($"[Groundcover] Missing {t.name}.fbx; run the Blender generator first."); continue; }
                string matPath = $"{MaterialDir}/{t.name}.mat";
                var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
                if (!mat)
                {
                    mat = new Material(shader);
                    AssetDatabase.CreateAsset(mat, matPath);
                    mat.SetFloat("_SwayAmount", t.sway);       // tuned once; edit the material freely
                    mat.SetFloat("_SwayHeight", t.height);
                    mat.SetFloat("_Flutter", t.flutter);
                    mat.SetFloat("_SwaySpeed", 1.4f);
                    mat.SetFloat("_Fuzz", t.fuzz);
                    mat.SetFloat("_Wrap", t.wrap);
                    mat.SetFloat("_CausticsAmount", 0.6f);
                    mat.SetColor("_BaseColor", t.tint);
                }
                mat.enableInstancing = true;                     // required for Terrain detail meshes
                mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(TextureDir + t.name + ".png"));
                EditorUtility.SetDirty(mat);

                var go = new GameObject(t.name);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var r = go.AddComponent<MeshRenderer>();
                r.sharedMaterial = mat;
                r.shadowCastingMode = ShadowCastingMode.Off;   // thousands of tiny clumps: no shadows, cheaper
                result[t.name] = PrefabUtility.SaveAsPrefabAsset(go, $"{PrefabDir}/{t.name}.prefab");
                Object.DestroyImmediate(go);
            }
            AssetDatabase.SaveAssets();
            return result;
        }

        /// <summary>Adds (or refreshes) one detail layer per ground-cover type. Returns layer index by name.</summary>
        public static Dictionary<string, int> AddTo(TerrainData data, Dictionary<string, GameObject> prefabs)
        {
            var list = data.detailPrototypes.ToList();
            var index = new Dictionary<string, int>();
            foreach (var kv in prefabs)
            {
                int i = list.FindIndex(p => p.prototype == kv.Value);
                var dp = i >= 0 ? list[i] : new DetailPrototype();
                dp.prototype = kv.Value;
                dp.usePrototypeMesh = true;
                dp.renderMode = DetailRenderMode.VertexLit;
                dp.useInstancing = true;
                dp.minWidth = 0.8f; dp.maxWidth = 1.3f;
                dp.minHeight = 0.8f; dp.maxHeight = 1.25f;
                dp.noiseSpread = 0.4f;
                if (!dp.Validate(out string err)) Debug.LogWarning($"[Groundcover] {kv.Key}: {err}");
                if (i >= 0) list[i] = dp; else { list.Add(dp); i = list.Count - 1; }
                index[kv.Key] = i;
            }
            data.detailPrototypes = list.ToArray();
            EditorUtility.SetDirty(data);
            return index;
        }

        /// <summary>A 10 × 10 m flat terrain on the left of the test scene, painted with both carpets.</summary>
        public static void PlaceDemoTerrain(Dictionary<string, GameObject> prefabs)
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path != ScenePath)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }
            foreach (var r in scene.GetRootGameObjects())
                if (r.name == DemoName) Object.DestroyImmediate(r);

            string dataPath = $"{PrefabDir}/DemoTerrainData.asset";
            AssetDatabase.DeleteAsset(dataPath);
            var data = new TerrainData { heightmapResolution = 33, size = new Vector3(10, 1, 10) };
            data.SetDetailResolution(64, 16);
            data.SetDetailScatterMode(DetailScatterMode.InstanceCountMode);
            AssetDatabase.CreateAsset(data, dataPath);
            var layers = AddTo(data, prefabs);

            // patches: clover where the noise is high, moss where it is low, bare sand in between
            int res = data.detailResolution;
            var clover = new int[res, res];
            var moss = new int[res, res];
            var rng = new System.Random(5);
            for (int y = 0; y < res; y++)
            for (int x = 0; x < res; x++)
            {
                float n = Mathf.PerlinNoise(x * 0.07f + 3.1f, y * 0.07f + 7.7f);
                float edge = Mathf.Min(Mathf.Min(x, res - 1 - x), Mathf.Min(y, res - 1 - y)) / 6f;   // feather the border
                if (rng.NextDouble() > Mathf.Clamp01(edge)) continue;
                if (n > 0.55f && rng.NextDouble() < 0.75) clover[y, x] = 1;
                else if (n < 0.45f && rng.NextDouble() < 0.8) moss[y, x] = 1;
            }
            if (layers.TryGetValue("Ground_Clover", out int lc)) data.SetDetailLayer(0, 0, lc, clover);
            if (layers.TryGetValue("Ground_FuzzMoss", out int lm)) data.SetDetailLayer(0, 0, lm, moss);

            var go = Terrain.CreateTerrainGameObject(data);
            go.name = DemoName;
            go.transform.position = new Vector3(-14.5f, 0.004f, -4f);   // just above the sand plane
            var terrain = go.GetComponent<Terrain>();
            terrain.drawInstanced = false;                               // lets a plain lit material draw the ground
            terrain.materialTemplate = AssetDatabase.LoadAssetAtPath<Material>("Assets/Reefkeeper/Materials/UnderWater/UW_Sand.mat");
            terrain.detailObjectDistance = 60f;
            terrain.detailObjectDensity = 1f;
            terrain.shadowCastingMode = ShadowCastingMode.Off;
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Selection.activeGameObject = go;
            Debug.Log($"[Groundcover] Demo terrain placed in {ScenePath}.");
        }
    }
}
