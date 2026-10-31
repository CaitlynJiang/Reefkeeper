using System.Collections.Generic;
using System.Linq;
using Reefkeeper.Corals;
using Reefkeeper.UnderWater;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace Reefkeeper.V2
{
    /// <summary>
    /// One-time art swap for ShallowReef_V2: brings in the water, rack bases, growth corals
    /// (smallest stage), seaweed and shipwreck from TestScene_UnderWater. Terrain stays as is.
    /// Safe to run again: it stops if the scene is already migrated.
    /// </summary>
    public static class ShallowReefV2Migration
    {
        const string V2Path = "Assets/Reefkeeper/Scenes/ShallowReef_V2.unity";
        const string TestPath = "Assets/Reefkeeper/Scenes/UnderWater/TestScene_UnderWater.unity";
        const string PrefabDir = "Assets/Reefkeeper/Prefabs/CoralGrowth";
        const string UwMaterials = "Assets/Reefkeeper/Materials/UnderWater";
        const string PostProfilePath = "Assets/Reefkeeper/Settings/UnderWater/UW_PostProfile.asset";

        static readonly string[] SeaweedNames = { "Seaweed_Feather", "Seaweed_Grass", "Seaweed_Kelp", "Seaweed_Leafy" };

        [MenuItem("Reefkeeper/V2/Migrate new art into ShallowReef_V2 - 迁移新美术到 V2")]
        public static void Run()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.OpenScene(V2Path, OpenSceneMode.Single);
            if (Root(scene, "WATER / underwater look"))
            {
                Debug.LogWarning("[V2] ShallowReef_V2 is already migrated; nothing done.");
                return;
            }

            // Reef corals stay as in V1; only rack corals use the growth models.
            var rng = new System.Random(2026);
            Water(scene);
            Physics.SyncTransforms();
            Seaweed(scene, rng);
            Racks(scene, rng);
            Shipwreck(scene);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[V2] ShallowReef_V2 migrated: water, racks, corals (stage 0), seaweed, shipwreck.");
        }

        // ---------------------------------------------------------------- water

        static void Water(Scene scene)
        {
            var test = EditorSceneManager.OpenScene(TestPath, OpenSceneMode.Additive);
            SceneManager.SetActiveScene(scene);

            // Old water look: ceiling, sunbeams, plankton and the old grade.
            var env = Root(scene, "REEF / environment");
            foreach (Transform c in env.transform.Cast<Transform>().ToArray())
                if (c.name.StartsWith("Surface / viewed from below") || c.name.StartsWith("Shaft of sunlight") || c.name.StartsWith("Suspended marine particles"))
                    Object.DestroyImmediate(c.gameObject);
            var oldGrade = Root(scene, "Underwater color grade");
            if (oldGrade) Object.DestroyImmediate(oldGrade);

            var group = new GameObject("WATER / underwater look").transform;
            SceneManager.MoveGameObjectToScene(group.gameObject, scene);

            // Environment: copy the values tuned in the test scene.
            var envGo = new GameObject("Underwater Environment");
            envGo.transform.SetParent(group, false);
            var uw = envGo.AddComponent<UnderwaterEnvironment>();
            EditorJsonUtility.FromJsonOverwrite(EditorJsonUtility.ToJson(Root(test, "Underwater Environment").GetComponent<UnderwaterEnvironment>()), uw);
            uw.Apply();

            // Sun matches the test scene so the new shaders read the same.
            var sun = Root(scene, "Sunlight through water").GetComponent<Light>();
            var testSun = Root(test, "Directional Light").GetComponent<Light>();
            sun.color = testSun.color;
            sun.intensity = testSun.intensity;
            sun.shadows = testSun.shadows;
            sun.transform.rotation = testSun.transform.rotation;
            RenderSettings.sun = sun;

            var shafts = Object.Instantiate(Root(test, "Light Shafts"), group);
            shafts.name = "Light Shafts";
            shafts.transform.position = new Vector3(0, uw.surfaceY - 1f, 6f);
            shafts.GetComponent<UnderwaterLightShafts>().sun = sun;

            var surface = Object.Instantiate(Root(test, "Water Surface"), group);
            surface.name = "Water Surface";
            surface.transform.position = new Vector3(0, uw.surfaceY, 0);

            var volume = new GameObject("Post Volume").AddComponent<Volume>();
            volume.transform.SetParent(group, false);
            volume.isGlobal = true;
            volume.priority = 10;
            volume.sharedProfile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(PostProfilePath);

            // Skybox gives the water colour behind everything; both player cameras use it.
            RenderSettings.skybox = AssetDatabase.LoadAssetAtPath<Material>($"{UwMaterials}/UW_Skybox.mat");
            var snow = Root(test, "Main Camera").transform.Find("Marine Snow").gameObject;
            foreach (var cam in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Camera>(true)))
            {
                cam.clearFlags = CameraClearFlags.Skybox;
                cam.farClipPlane = Mathf.Max(cam.farClipPlane, 200f);
                cam.GetUniversalAdditionalCameraData().renderPostProcessing = true;
                var s = Object.Instantiate(snow, cam.transform);
                s.name = "Marine Snow";
                s.transform.localPosition = snow.transform.localPosition;
                s.transform.localRotation = Quaternion.identity;
            }

            // The kept terrain still uses Unity fog; tint it to the new water so it blends in.
            float t = Mathf.InverseLerp(uw.floorY, uw.surfaceY, 1f);
            RenderSettings.fogColor = Color.Lerp(uw.deepColor, uw.shallowColor, t);

            EditorSceneManager.CloseScene(test, true);
        }

        // ---------------------------------------------------------------- seaweed

        const float SeaweedBaseScale = 1f;
        static readonly Vector2 SeaweedRandomScale = new Vector2(1f, 4f); // final size 1-4x
        const float SeaweedSpacing = 1.6f;                  // metres between plants in a patch
        const float PatchGap = 8f;                          // min distance between patch centres

        // Old seagrass is replaced by regular 3x3 patches: each row is three plants of one
        // kind, three kinds per patch (rotating through the four). Patches sit where the old
        // seagrass grew, kept clear of the workshop and nursery.
        static void Seaweed(Scene scene, System.Random rng)
        {
            var root = Root(scene, "REEF / corals and seagrass").transform;
            var spots = new List<Vector3>();
            foreach (Transform c in root.Cast<Transform>().ToArray())
            {
                if (!c.name.StartsWith("Seagrass")) continue;
                spots.Add(c.position);
                Object.DestroyImmediate(c.gameObject);
            }
            Physics.SyncTransforms();

            var keepClear = new List<Vector3> { Root(scene, "WORKSHOP / coral nursery maintenance").transform.position };
            foreach (Transform t in Root(scene, "NURSERY / suspended coral trees").transform) keepClear.Add(t.position);
            var centres = new List<Vector3>();
            foreach (var s in spots)
            {
                var flat = new Vector3(s.x, 0, s.z);
                if (centres.Any(c => Vector3.Distance(c, flat) < PatchGap)) continue;
                if (keepClear.Any(c => Vector2.Distance(new Vector2(c.x, c.z), new Vector2(s.x, s.z)) < 5f)) continue;
                centres.Add(flat);
            }

            var group = new GameObject("Seaweed 水草 (3x3 patches)").transform;
            group.SetParent(root, false);
            for (int p = 0; p < centres.Count; p++)
            {
                var patch = new GameObject($"Seaweed patch {p}").transform;
                patch.SetParent(group, false);
                var yaw = Quaternion.Euler(0, (float)rng.NextDouble() * 360f, 0);
                patch.SetPositionAndRotation(Ground(scene, centres[p] + Vector3.up * 20f), yaw);
                for (int row = 0; row < 3; row++)
                {
                    var kind = SeaweedNames[(p + row) % SeaweedNames.Length];
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabDir}/Seaweed/{kind}.prefab");
                    for (int col = 0; col < 3; col++)
                    {
                        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, patch);
                        go.name = $"{kind} {row}-{col}";
                        var local = new Vector3((col - 1) * SeaweedSpacing, 0, (row - 1) * SeaweedSpacing);
                        var world = patch.position + yaw * local;
                        go.transform.position = Ground(scene, world + Vector3.up * 20f);
                        go.transform.rotation = Quaternion.Euler(0, (float)rng.NextDouble() * 360f, 0);
                        float random = Mathf.Lerp(SeaweedRandomScale.x, SeaweedRandomScale.y, (float)rng.NextDouble());
                        go.transform.localScale = Vector3.one * SeaweedBaseScale * random;
                    }
                }
            }
        }

        // ---------------------------------------------------------------- racks

        const float RackScale = 2f;        // rack bases at 2x; corals on them stay 1x
        const float RackSpacing = 3f;      // metres between racks in the 3x3 grid
        // One row per kind, short to tall away from the player so every row stays visible.
        static readonly string[] RackRows = { "RackBase_Cairn", "RackBase_Tree", "RackBase_Hanging" };

        static void Racks(Scene scene, System.Random rng)
        {
            var workshop = Root(scene, "WORKSHOP / coral nursery maintenance");
            var oldTree = workshop.transform.Cast<Transform>().First(t => t.name.StartsWith("Nursery tree"));
            var template = oldTree.GetComponentInChildren<ReefCoralFragment>(true);
            var outline = template.outlineMaterial;

            // The old suspended nursery trees go; the 3x3 rack grid is the nursery now.
            Object.DestroyImmediate(Root(scene, "NURSERY / suspended coral trees"));

            // Keep the cleaning practice spot and tool rest; drop the old frame, lines and fragments.
            var treePos = oldTree.position;
            Transform practice = null, rest = null, support = null;
            foreach (Transform c in oldTree.Cast<Transform>().ToArray())
            {
                if (c.name.StartsWith("Practice spot")) practice = c;
                else if (c.name == "Tool rest") rest = c;
                else if (c.name.StartsWith("Tool rest support")) support = c;
                else continue;
                c.SetParent(workshop.transform, true);
            }
            Object.DestroyImmediate(oldTree.gameObject);
            Physics.SyncTransforms();

            // 3x3 grid of planting racks just past the work benches, centred on the workshop.
            var grid = new GameObject("Coral racks 3x3 架子").transform;
            grid.SetParent(workshop.transform, false);
            grid.position = new Vector3(treePos.x, workshop.transform.position.y, treePos.z);
            for (int row = 0; row < 3; row++)
            for (int col = 0; col < 3; col++)
            {
                var spot = new Vector3(treePos.x + (col - 1) * RackSpacing, 3f, treePos.z + 2f + row * RackSpacing);
                var rack = SpawnRack(RackRows[row], grid, Ground(scene, spot), rng);
                rack.name = $"{RackRows[row]} {row}-{col}";
                rack.transform.rotation = Quaternion.identity;
                rack.transform.localScale = Vector3.one * RackScale;
            }
            Physics.SyncTransforms();

            PlantPole(scene, rest, support);

            // The practice spot used to sit on the old tray; give it its own bench.
            var bench = Object.Instantiate(rest.gameObject, workshop.transform).transform;
            bench.name = "Practice bench";
            bench.position = new Vector3(treePos.x - 0.8f, rest.position.y, rest.position.z);
            bench.localScale = new Vector3(0.85f, rest.localScale.y, rest.localScale.z);
            var benchPole = Object.Instantiate(support.gameObject, workshop.transform).transform;
            benchPole.name = "Practice bench support";
            Physics.SyncTransforms();
            PlantPole(scene, bench, benchPole);
            var top = bench.GetComponent<Renderer>().bounds.max.y;
            float bottom = practice.GetComponentsInChildren<Renderer>().Min(r => r.bounds.min.y);
            practice.position = new Vector3(bench.position.x, practice.position.y + top - bottom, bench.position.z);

            // Corals sit under their sockets (world scale 1): table sockets full, every other hook.
            RackCoralTools.Fix(workshop.GetComponent<ReefWorkshop>(), true, rng, outline);
        }

        // Stands a support pole straight from under a rest down to the seabed.
        static void PlantPole(Scene scene, Transform rest, Transform pole)
        {
            var under = rest.position + Vector3.down * 0.03f;
            float ground = scene.GetPhysicsScene().Raycast(under, Vector3.down, out var hit, 20f, ~0, QueryTriggerInteraction.Ignore) ? hit.point.y : under.y - 1f;
            float length = Mathf.Max(0.05f, under.y - ground);
            pole.SetPositionAndRotation(new Vector3(rest.position.x, ground + length * 0.5f, rest.position.z), Quaternion.identity);
            pole.localScale = new Vector3(pole.localScale.x, length * 0.5f, pole.localScale.z);
        }

        static GameObject SpawnRack(string name, Transform parent, Vector3 pos, System.Random rng)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabDir}/RackBases/{name}.prefab");
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0, (float)rng.NextDouble() * 360f, 0));
            return go;
        }

        // ---------------------------------------------------------------- shipwreck

        static void Shipwreck(Scene scene)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabDir}/Shipwreck.prefab");
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, Root(scene, "REEF / environment").transform);
            go.name = "Shipwreck 沉船";
            var pos = Ground(scene, new Vector3(1.5f, 20f, 15f));
            go.transform.SetPositionAndRotation(pos + Vector3.down * 0.3f, Quaternion.Euler(0, 75f, 0));
        }

        // ---------------------------------------------------------------- helpers

        static GameObject Root(Scene scene, string name) => scene.GetRootGameObjects().FirstOrDefault(r => r.name == name);

        // Drops a point onto the seabed / rocks below it (searching from a little above).
        static Vector3 Ground(Scene scene, Vector3 p)
        {
            var physics = scene.GetPhysicsScene();
            return physics.Raycast(p + Vector3.up * 1.5f, Vector3.down, out var hit, 40f, ~0, QueryTriggerInteraction.Ignore) ? hit.point : p;
        }
    }
}
