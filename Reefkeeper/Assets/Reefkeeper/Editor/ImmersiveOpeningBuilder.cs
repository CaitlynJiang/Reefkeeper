using System;
using System.IO;
using System.Linq;
using Reefkeeper.UnderWater;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Reefkeeper.Editor
{
    public static class ImmersiveOpeningBuilder
    {
        public const string Opening = "Assets/Reefkeeper/Scenes/ImmersiveOpening.unity";
        const string ObsoleteReef = "Assets/Reefkeeper/Scenes/ImmersiveOpening_Reef.unity";
        const string V4 = "Assets/Reefkeeper/Scenes/ShallowReef_V4.unity";
        const string Folder = "Assets/Reefkeeper/Generated/SurfaceIntro";

        [MenuItem("Reefkeeper/Opening/Build click-to-dive prototype")]
        public static void Build()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Exit Play Mode before building.");
            foreach (var path in new[] { Opening, ObsoleteReef })
                if (SceneManager.GetSceneByPath(path).isLoaded) throw new InvalidOperationException("Close " + path + " before rebuilding.");
            if (SceneManager.GetSceneByPath(V4).isDirty) throw new InvalidOperationException("Save the source scene before building.");
            var previous = SceneManager.GetActiveScene();
            SanitizeManualPrefab();
            var scene = CopyScene(V4, Opening);
            SceneManager.SetActiveScene(scene);
            try
            {
                var roots = scene.GetRootGameObjects();
                var workshop = roots.SelectMany(g => g.GetComponentsInChildren<ReefWorkshop>(true)).Single();
                var environment = roots.SelectMany(g => g.GetComponentsInChildren<UnderwaterEnvironment>(true)).Single();
                var seaOrigin = new Vector3(0, environment.surfaceY, -15.15f);
                var root = new GameObject("Surface opening - same reef scene");
                var platform = PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "/SurfaceIntro_Platform.prefab"), scene) as GameObject;
                platform.transform.SetParent(root.transform, false);
                platform.transform.position = seaOrigin;
                var body = PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Reefkeeper/Art/OpeningDiver/OpeningDiver_Seated.prefab"), scene) as GameObject;
                body.transform.SetParent(root.transform, false);
                body.transform.position = seaOrigin + new Vector3(0,0,.55f);
                var sea = new GameObject("Opaque surface - top face only", typeof(MeshFilter), typeof(MeshRenderer));
                sea.transform.SetParent(root.transform, false);
                sea.transform.position = new Vector3(0, environment.surfaceY + .02f, 0);
                sea.GetComponent<MeshFilter>().sharedMesh = OpeningWaterAssets.WaterMesh();
                var renderer = sea.GetComponent<MeshRenderer>();
                OpeningWaterAssets.ConfigureFullSurface(renderer);
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                var sequence = root.AddComponent<OpeningDiveSequence>();
                sequence.workshop = workshop;
                sequence.surfaceWater = renderer;
                sequence.seatedBody = body;
                sequence.underwaterEnvironment = environment.transform.root.gameObject;
                sequence.seatedEye = seaOrigin + new Vector3(0,1.57f,.68f);
                // OpeningDiveSequence defaults to the approved overview of the practice area.
                sequence.surfaceSky = AssetDatabase.LoadAssetAtPath<Material>(Folder + "/SurfaceSky_Morning.mat");
                sequence.fadeTemplate = Material("Dive fade", "Reefkeeper/Opening Fade", Color.black);
                sequence.bubbleTemplate = Material("Entry bubbles", "Universal Render Pipeline/Unlit", new Color(.67f,.89f,.92f));
                // Awake records a safe underwater reset position; Start moves the same rig to the seat.
                var motor = workshop.desktopPlayer.GetComponent<DiveMotor>();
                motor.transform.rotation = Quaternion.identity;
                motor.view.transform.localRotation = Quaternion.identity;
                motor.transform.position += sequence.underwaterEye - motor.view.transform.position;
                var origin = workshop.xrGroup.GetComponentInChildren<XROrigin>(true);
                origin.transform.rotation = Quaternion.identity;
                origin.transform.position += sequence.underwaterEye - origin.Camera.transform.position;
                EditorSceneManager.SaveScene(scene);
                AssetDatabase.SaveAssets();
            }
            finally { if (previous.IsValid()) SceneManager.SetActiveScene(previous); EditorSceneManager.CloseScene(scene, true); }

            // Remove only the superseded prototype destination; this opening contains the complete reef.
            var entries = EditorBuildSettings.scenes.Where(s => s.path != ObsoleteReef).ToList();
            if (!entries.Any(s => s.path == Opening)) entries.Add(new EditorBuildSettingsScene(Opening, true));
            EditorBuildSettings.scenes = entries.ToArray();
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ObsoleteReef)) AssetDatabase.DeleteAsset(ObsoleteReef);
            Debug.Log("[OpeningDive] Built surface and reef together, reusing player rigs and existing UI: " + Opening);
        }

        static void SanitizeManualPrefab()
        {
            string path = Folder + "/SurfaceIntro_Platform.prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                foreach (var text in root.GetComponentsInChildren<TextMesh>(true)) text.text = "placeholder text";
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        static Scene CopyScene(string source, string destination)
        {
            if (File.Exists(destination))
            {
                File.Copy(source, destination, true);
                AssetDatabase.ImportAsset(destination, ImportAssetOptions.ForceSynchronousImport);
            }
            else if (!AssetDatabase.CopyAsset(source, destination)) throw new IOException("Cannot copy " + source);
            return EditorSceneManager.OpenScene(destination, OpenSceneMode.Additive);
        }
        static Material Material(string name, string shaderName, Color color)
        {
            string path = Folder + "/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!material)
            {
                var shader = Shader.Find(shaderName);
                if (!shader) throw new InvalidOperationException("Missing shader " + shaderName);
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetColor(material.HasProperty("_BaseColor") ? "_BaseColor" : "_Color", color);
            EditorUtility.SetDirty(material);
            return material;
        }
    }
}
