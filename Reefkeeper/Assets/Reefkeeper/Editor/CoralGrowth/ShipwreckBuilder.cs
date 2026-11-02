using System.IO;
using System.Linq;
using Reefkeeper.Corals;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Reefkeeper.CoralGrowthTools
{
    /// <summary>
    /// Prefab for the mossy shipwreck exported by Blender/export_shipwreck.py: the hull (one mesh,
    /// baked moss) with a collider, the tattered sail on the sway shader, and RackSocket planting
    /// points on the decks. Placed behind the rack bases in TestScene_UnderWater.
    /// </summary>
    public static class ShipwreckBuilder
    {
        const string Fbx = CoralGrowthImport.MeshDir + "Shipwreck/Shipwreck.fbx";
        const string TextureDir = CoralGrowthImport.TextureDir + "Shipwreck/";
        const string MaterialDir = "Assets/Reefkeeper/Materials/CoralGrowth/Shipwreck";
        const string PrefabPath = "Assets/Reefkeeper/Prefabs/CoralGrowth/Shipwreck.prefab";
        const string ScenePath = "Assets/Reefkeeper/Scenes/UnderWater/TestScene_UnderWater.unity";
        const string InstanceName = "Shipwreck 沉船";

        [MenuItem("Reefkeeper/CoralGrowth/5. Build shipwreck - 生成沉船")]
        public static void BuildAll() => PlaceInTestScene(BuildPrefab());

        public static GameObject BuildPrefab()
        {
            Directory.CreateDirectory(MaterialDir);
            Directory.CreateDirectory(Path.GetDirectoryName(PrefabPath));
            AssetDatabase.Refresh();
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(Fbx);
            var meshes = AssetDatabase.LoadAllAssetsAtPath(Fbx).OfType<Mesh>().ToArray();
            var hull = meshes.FirstOrDefault(m => m.name == "Shipwreck");
            var sail = meshes.FirstOrDefault(m => m.name == "Shipwreck_Sail");
            if (!model || !hull) { Debug.LogWarning($"[Shipwreck] Missing {Fbx}; run export_shipwreck.py first."); return null; }

            var root = new GameObject("Shipwreck");
            root.AddComponent<MeshFilter>().sharedMesh = hull;
            root.AddComponent<MeshRenderer>().sharedMaterial = Material("Shipwreck", "Reefkeeper/UnderWater/Lit", m =>
            {
                m.SetFloat("_Wrap", 0.55f);
                m.SetFloat("_CausticsAmount", 0.8f);
            });
            root.AddComponent<MeshCollider>().sharedMesh = hull;

            if (sail)
            {
                var s = new GameObject("Sail");
                s.transform.SetParent(root.transform, false);
                s.AddComponent<MeshFilter>().sharedMesh = sail;
                // Same motion as the Blender preview: 4 s loop (speed = 2π / 4), ~0.25 m at the free ends.
                s.AddComponent<MeshRenderer>().sharedMaterial = Material("Shipwreck_Sail", "Reefkeeper/UnderWater/Lit Sway", m =>
                {
                    m.SetFloat("_SwayAmount", 0.25f);
                    m.SetFloat("_SwaySpeed", Mathf.PI * 2f / 4f);
                    m.SetFloat("_Flutter", 0.035f);
                    m.SetFloat("_SwayHeight", 1.5f);
                    m.SetVector("_SwayDir", new Vector4(0.35f, 0.2f, 0, 0));
                });
            }

            var sockets = new GameObject("Sockets").transform;
            sockets.SetParent(root.transform, false);
            foreach (var t in model.GetComponentsInChildren<Transform>(true).Where(t => t.name.StartsWith("Socket_")))
            {
                var go = new GameObject(t.name);
                go.transform.SetParent(sockets, false);
                go.transform.localPosition = model.transform.InverseTransformPoint(t.position);
                go.AddComponent<RackSocket>().kind = RackSocket.Kind.Table;
            }

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Shipwreck] Built {PrefabPath}.");
            return prefab;
        }

        static Material Material(string name, string shaderName, System.Action<Material> firstTime)
        {
            string path = $"{MaterialDir}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!mat)
            {
                var shader = Shader.Find(shaderName);
                if (!shader) throw new System.Exception($"Shader '{shaderName}' not found.");
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
                mat.SetColor("_BaseColor", Color.white);
                firstTime(mat);   // only on creation, so later tweaks in the editor are kept
            }
            mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(TextureDir + name + ".png"));
            EditorUtility.SetDirty(mat);
            return mat;
        }

        public static void PlaceInTestScene(GameObject prefab)
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
            inst.transform.SetPositionAndRotation(new Vector3(8.5f, 0f, 8.5f), Quaternion.Euler(0f, 215f, 0f));
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Selection.activeGameObject = inst;
            Debug.Log($"[Shipwreck] Placed in {ScenePath}.");
        }
    }
}
