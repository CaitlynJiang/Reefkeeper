using System.Collections.Generic;
using System.IO;
using System.Linq;
using Reefkeeper.Corals;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Reefkeeper.CoralGrowthTools
{
    /// <summary>
    /// Prefabs for the mossy rack bases exported by Blender/export_rack_bases.py,
    /// with a RackSocket on every exported Socket_* point, laid out in TestScene_UnderWater.
    /// </summary>
    public static class CoralRackBaseBuilder
    {
        const string MeshDir = CoralGrowthImport.MeshDir + "RackBases/";
        const string TextureDir = CoralGrowthImport.TextureDir + "RackBases/";
        const string MaterialDir = "Assets/Reefkeeper/Materials/CoralGrowth/RackBases";
        const string PrefabDir = "Assets/Reefkeeper/Prefabs/CoralGrowth/RackBases";
        const string ScenePath = "Assets/Reefkeeper/Scenes/UnderWater/TestScene_UnderWater.unity";
        const string GroupName = "Rack Bases 架子底座";

        static readonly string[] Bases = { "RackBase_Tree", "RackBase_Cairn", "RackBase_Hanging" };

        [MenuItem("Reefkeeper/CoralGrowth/3. Build rack bases - 生成架子底座")]
        public static void BuildAll() => PlaceInTestScene(BuildPrefabs());

        public static Dictionary<string, GameObject> BuildPrefabs()
        {
            Directory.CreateDirectory(MaterialDir);
            Directory.CreateDirectory(PrefabDir);
            AssetDatabase.Refresh();
            var shader = Shader.Find("Reefkeeper/UnderWater/Lit");
            if (!shader) throw new System.Exception("Shader 'Reefkeeper/UnderWater/Lit' not found.");

            var result = new Dictionary<string, GameObject>();
            foreach (string name in Bases)
            {
                string fbx = MeshDir + name + ".fbx";
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(fbx);
                var mesh = AssetDatabase.LoadAllAssetsAtPath(fbx).OfType<Mesh>().FirstOrDefault();
                if (!model || !mesh) { Debug.LogWarning($"[RackBase] Missing {fbx}; run export_rack_bases.py first."); continue; }

                var root = new GameObject(name);
                root.AddComponent<MeshFilter>().sharedMesh = mesh;
                root.AddComponent<MeshRenderer>().sharedMaterial = Material(shader, name);
                root.AddComponent<MeshCollider>().sharedMesh = mesh;   // for planting / brushing later

                var sockets = new GameObject("Sockets").transform;
                sockets.SetParent(root.transform, false);
                foreach (var t in model.GetComponentsInChildren<Transform>(true).Where(t => t.name.StartsWith("Socket_")))
                {
                    var s = new GameObject(t.name);
                    s.transform.SetParent(sockets, false);
                    s.transform.localPosition = model.transform.InverseTransformPoint(t.position);
                    s.AddComponent<RackSocket>().kind = t.name.Contains("_Hang_") ? RackSocket.Kind.Hang : RackSocket.Kind.Table;
                }

                result[name] = PrefabUtility.SaveAsPrefabAsset(root, $"{PrefabDir}/{name}.prefab");
                Object.DestroyImmediate(root);
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"[RackBase] Built {result.Count} prefabs in {PrefabDir}.");
            return result;
        }

        static Material Material(Shader shader, string name)
        {
            string path = $"{MaterialDir}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!mat)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
                mat.SetColor("_BaseColor", Color.white);
                mat.SetFloat("_Wrap", 0.55f);
                mat.SetFloat("_CausticsAmount", 0.8f);
            }
            mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(TextureDir + name + ".png"));
            EditorUtility.SetDirty(mat);
            return mat;
        }

        /// <summary>A row behind the coral showcase, facing the test camera.</summary>
        public static void PlaceInTestScene(Dictionary<string, GameObject> prefabs)
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path != ScenePath)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }
            foreach (var r in scene.GetRootGameObjects())
                if (r.name == GroupName) Object.DestroyImmediate(r);

            var group = new GameObject(GroupName).transform;
            group.position = new Vector3(0f, 0f, 1.9f);
            float[] xs = { -2.3f, 0f, 2.3f };
            for (int i = 0; i < Bases.Length; i++)
            {
                if (!prefabs.TryGetValue(Bases[i], out var prefab) || !prefab) continue;
                var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, group);
                inst.transform.localPosition = new Vector3(xs[i], 0f, 0f);
                inst.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Selection.activeGameObject = group.gameObject;
            Debug.Log($"[RackBase] Placed {group.childCount} bases in {ScenePath}.");
        }
    }
}
