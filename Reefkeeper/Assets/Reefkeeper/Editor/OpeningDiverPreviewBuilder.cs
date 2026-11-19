using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Reefkeeper.Editor
{
    /// <summary>Builds an isolated review scene; existing gameplay and blockout scenes stay intact.</summary>
    public static class OpeningDiverPreviewBuilder
    {
        const string Folder = "Assets/Reefkeeper/Art/OpeningDiver";
        public const string PreviewScene = "Assets/Reefkeeper/Scenes/SurfaceIntro_DiverPreview.unity";

        [MenuItem("Reefkeeper/Opening/Build seated diver preview")]
        public static void Build()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Exit Play Mode first.");
            if (SceneManager.GetSceneByPath(PreviewScene).isLoaded ||
                SceneManager.GetSceneByPath(SurfaceIntroBlockoutBuilder.ScenePath).isLoaded)
                throw new InvalidOperationException("Close the opening review scenes before rebuilding.");

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var importer = (ModelImporter)AssetImporter.GetAtPath(Folder + "/OpeningDiver.fbx");
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.importAnimation = true;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.meshCompression = ModelImporterMeshCompression.Off;
            importer.isReadable = false;
            importer.SaveAndReimport();
            var clips = importer.defaultClipAnimations;
            if (clips.Length != 1) throw new InvalidOperationException("Expected one seated idle take.");
            clips[0].name = "SeatedIdle";
            clips[0].loopTime = true;
            clips[0].loopPose = false;
            importer.clipAnimations = clips;
            importer.SaveAndReimport();
            var textureImporter = (TextureImporter)AssetImporter.GetAtPath(Folder + "/DiverPalette.png");
            textureImporter.filterMode = FilterMode.Point;
            textureImporter.mipmapEnabled = false;
            textureImporter.wrapMode = TextureWrapMode.Clamp;
            textureImporter.textureCompression = TextureImporterCompression.Uncompressed;
            textureImporter.SaveAndReimport();

            var material = AssetDatabase.LoadAssetAtPath<Material>(Folder + "/OpeningDiver.mat");
            if (!material)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, Folder + "/OpeningDiver.mat");
            }
            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + "/DiverPalette.png"));
            material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_Smoothness", .12f);
            material.SetFloat("_Metallic", 0);
            EditorUtility.SetDirty(material);
            var clip = AssetDatabase.LoadAllAssetsAtPath(importer.assetPath).OfType<AnimationClip>()
                .Single(c => !c.name.StartsWith("__preview__"));
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(Folder + "/SeatedIdle.controller");
            if (!controller) controller = AnimatorController.CreateAnimatorControllerAtPath(Folder + "/SeatedIdle.controller");
            var machine = controller.layers[0].stateMachine;
            var state = machine.states.Select(s => s.state).FirstOrDefault(s => s.name == "SeatedIdle") ?? machine.AddState("SeatedIdle");
            state.motion = clip;
            machine.defaultState = state;
            EditorUtility.SetDirty(controller);

            var previous = SceneManager.GetActiveScene();
            var scene = EditorSceneManager.OpenScene(SurfaceIntroBlockoutBuilder.ScenePath, OpenSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            try
            {
                var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(importer.assetPath), scene);
                model.name = "Opening diver - first person body prototype";
                model.transform.position = new Vector3(0, 0, .55f);
                var skins = model.GetComponentsInChildren<SkinnedMeshRenderer>();
                if (skins.Length != 1) throw new InvalidOperationException("Expected one skinned renderer.");
                var skin = skins[0];
                skin.sharedMaterial = material;
                skin.shadowCastingMode = ShadowCastingMode.Off;
                int triangles = skin.sharedMesh.triangles.Length / 3;
                if (triangles > 2500 || skin.bones.Length != 9)
                    throw new InvalidOperationException($"Unexpected budget: {triangles} triangles, {skin.bones.Length} bones.");
                var animator = model.GetComponent<Animator>();
                if (!animator) animator = model.AddComponent<Animator>();
                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;
                clip.SampleAnimation(model, 0);
                // Bound from several samples so small fin motion does not get culled near the viewport edge.
                var bounds = new Bounds();
                bool first = true;
                var baked = new Mesh();
                try
                {
                    for (int i = 0; i <= 16; i++)
                    {
                        clip.SampleAnimation(model, clip.length * i / 16f);
                        skin.BakeMesh(baked);
                        foreach (var point in baked.vertices)
                        {
                            if (!float.IsFinite(point.x) || !float.IsFinite(point.y) || !float.IsFinite(point.z))
                                throw new InvalidOperationException("Non-finite skinned vertex.");
                        }
                        if (first) { bounds = baked.bounds; first = false; }
                        else bounds.Encapsulate(baked.bounds);
                    }
                }
                finally { Object.DestroyImmediate(baked); }
                bounds.Expand(.04f);
                skin.localBounds = bounds;
                clip.SampleAnimation(model, 0);
                PrefabUtility.SaveAsPrefabAsset(model, Folder + "/OpeningDiver_Seated.prefab");

                var camera = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Camera>()).Single();
                // Eye slightly ahead of the waist: no visible cut surface in the intended first-person view.
                camera.transform.position = new Vector3(0, 1.57f, .68f);
                camera.transform.LookAt(new Vector3(0, .48f, 1.3f));
                camera.fieldOfView = 62;
                EditorSceneManager.SaveScene(scene, PreviewScene);
                AssetDatabase.SaveAssets();

                string review = Path.GetFullPath("../artifacts/opening-diver");
                Directory.CreateDirectory(review);
                Capture(scene, camera, review + "/unity-first-person.png");
                camera.transform.position = new Vector3(1.8f, 2.2f, 2.95f);
                camera.transform.LookAt(new Vector3(0, .72f, .81f));
                camera.fieldOfView = 42;
                Capture(scene, camera, review + "/unity-seated.png");
                // Review camera changes are not saved; the saved scene retains the seated view.
                File.WriteAllText(review + "/unity-validation.txt", $"PASS: one mesh, {triangles} triangles, {skin.bones.Length} bones, one opaque material.\nClip: {clip.name}, {clip.length:F2}s, loop={clip.isLooping}.\n17 animation samples: finite deformed vertices; conservative bounds generated.\nIndependent scene: {PreviewScene}\nNo colliders, head, arms, hands or XR rig in this model.\nHeadset comfort and performance remain untested.\n");
                Debug.Log($"[OpeningDiver] PASS {triangles} triangles / {skin.bones.Length} bones. Preview scene saved.");
            }
            finally
            {
                if (previous.IsValid()) SceneManager.SetActiveScene(previous);
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        static void Capture(Scene scene, Camera camera, string path)
        {
            var transforms = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true))
                .Select(t => (t.gameObject, layer: t.gameObject.layer)).ToArray();
            var otherLights = Object.FindObjectsByType<Light>(FindObjectsSortMode.None)
                .Where(l => l.gameObject.scene != scene && l.enabled).ToArray();
            var rt = new RenderTexture(1280, 900, 24) { antiAliasing = 4 };
            var previous = RenderTexture.active;
            int mask = camera.cullingMask;
            try
            {
                foreach (var l in otherLights) l.enabled = false;
                foreach (var item in transforms) item.gameObject.layer = 30;
                camera.cullingMask = 1 << 30;
                camera.targetTexture = rt;
                camera.Render();
                RenderTexture.active = rt;
                var image = new Texture2D(1280, 900, TextureFormat.RGB24, false);
                try
                {
                    image.ReadPixels(new Rect(0, 0, 1280, 900), 0, 0);
                    image.Apply();
                    File.WriteAllBytes(path, image.EncodeToPNG());
                }
                finally { Object.DestroyImmediate(image); }
            }
            finally
            {
                camera.targetTexture = null;
                camera.cullingMask = mask;
                foreach (var item in transforms) item.gameObject.layer = item.layer;
                foreach (var l in otherLights) if (l) l.enabled = true;
                RenderTexture.active = previous;
                rt.Release();
                Object.DestroyImmediate(rt);
            }
        }
    }
}
