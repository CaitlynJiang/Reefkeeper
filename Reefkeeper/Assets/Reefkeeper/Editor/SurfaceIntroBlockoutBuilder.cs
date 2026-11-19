using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Reefkeeper.Editor
{
    /// <summary>Independent art-handoff blockout. Does not change the V4 scene or its spawn points.</summary>
    public static class SurfaceIntroBlockoutBuilder
    {
        public const string ScenePath = "Assets/Reefkeeper/Scenes/SurfaceIntro_Blockout.unity";
        const string Folder = "Assets/Reefkeeper/Generated/SurfaceIntro";
        const string Review = "../artifacts/surface-intro";

        [MenuItem("Reefkeeper/V4/Build surface intro blockout")]
        public static void Build()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Exit Play Mode first.");
            if (SceneManager.GetSceneByPath(ScenePath).isLoaded) throw new InvalidOperationException("Close the blockout scene before rebuilding it.");
            Directory.CreateDirectory(Folder); Directory.CreateDirectory(Review); AssetDatabase.Refresh();
            var previous = SceneManager.GetActiveScene();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            try
            {
                var wood = Material("Warm wood", new Color(.65f, .43f, .24f));
                var honey = Material("Light wood", new Color(.78f, .56f, .33f));
                var edge = Material("Dark wood", new Color(.40f, .27f, .18f));
                var teal = Material("Manual teal", new Color(.19f, .55f, .59f));
                var cream = Material("PVC cream", new Color(.96f, .91f, .74f));
                var yellow = Material("Tab yellow", new Color(1f, .74f, .22f));
                var metal = Material("Mooring fittings", new Color(.24f, .35f, .34f));
                var water = Material("Opaque sea", new Color(.16f, .49f, .58f), .35f);
                var root = new GameObject("SURFACE INTRO - replaceable art blockout");
                var deck = Child(root.transform, "Wood platform - replace with boat art");
                for (int i = 0; i < 9; i++)
                    Box("Deck plank " + (i + 1), deck, new Vector3((i - 4) * .39f, .56f, -.52f), new Vector3(.375f, .16f, 2.7f), i % 3 == 0 ? honey : wood);
                Box("Front edge", deck, new Vector3(0, .55f, .85f), new Vector3(3.64f, .25f, .16f), edge);
                Box("Rear support", deck, new Vector3(0, .38f, -1.55f), new Vector3(3.6f, .20f, .18f), edge);
                Box("Seat plank", deck, new Vector3(0, .76f, .55f), new Vector3(2.6f, .15f, .42f), honey);
                Box("Seat support L", deck, new Vector3(-1.0f, .67f, .55f), new Vector3(.14f, .16f, .26f), edge);
                Box("Seat support R", deck, new Vector3(1.0f, .67f, .55f), new Vector3(.14f, .16f, .26f), edge);
                var manual = Child(root.transform, "Manual placement - visual only, interaction pending");
                manual.localPosition = new Vector3(-.62f, .855f, .55f);
                manual.localRotation = Quaternion.Euler(0, -12, 0);
                Box("Teal card back", manual, Vector3.zero, new Vector3(.32f, .014f, .42f), teal);
                Box("Cream first card", manual, new Vector3(0, .01f, 0), new Vector3(.292f, .008f, .39f), cream);
                for (int i = 0; i < 3; i++) Box("Category tab " + i, manual, new Vector3(.16f, .011f, .12f - i * .10f), new Vector3(.035f, .009f, .065f), yellow);
                Label("placeholder text", manual, new Vector3(0, .017f, .08f), .027f, new Color(.16f, .35f, .35f));
                Label("placeholder text", manual, new Vector3(0, .017f, -.07f), .011f, new Color(.16f, .35f, .35f));
                for (int i = -1; i <= 1; i += 2) Box("Binding placeholder", manual, new Vector3(-.14f, .018f, i * .13f), new Vector3(.03f, .014f, .018f), metal);
                var perch = Child(root.transform, "Mooring post - future bird perch");
                Box("Timber", perch, new Vector3(1.43f, .98f, .60f), new Vector3(.16f, .90f, .16f), wood);
                Box("Post cap", perch, new Vector3(1.43f, 1.45f, .60f), new Vector3(.21f, .06f, .21f), honey);
                Box("Mooring crossbar", perch, new Vector3(1.43f, .98f, .60f), new Vector3(.32f, .07f, .09f), metal);
                var markers = Child(root.transform, "Gameplay anchors - no scripted transition yet");
                Anchor("Seated eye reference", markers, new Vector3(0, 1.57f, .37f));
                Anchor("Seated hips reference", markers, new Vector3(0, .91f, .55f));
                Anchor("Left knee reference", markers, new Vector3(-.15f, .84f, 1.00f));
                Anchor("Right knee reference", markers, new Vector3(.15f, .84f, 1.00f));
                Anchor("Water entry reference", markers, new Vector3(0, .05f, 1.5f));
                Anchor("Bird landing reference", markers, new Vector3(1.43f, 1.52f, .60f));
                PrefabUtility.SaveAsPrefabAsset(root, Folder + "/SurfaceIntro_Platform.prefab");

                // One opaque quad; no underwater reveal, refraction, reflection cameras or physics waves.
                var sea = new GameObject("Opaque ocean placeholder");
                var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(Folder + "/OceanQuad.asset");
                if (!mesh)
                {
                    mesh = new Mesh { name = "OceanQuad" };
                    mesh.vertices = new[] { new Vector3(-2000, 0, -2000), new Vector3(-2000, 0, 2000), new Vector3(2000, 0, 2000), new Vector3(2000, 0, -2000) };
                    mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 }; mesh.RecalculateNormals(); mesh.RecalculateBounds();
                    AssetDatabase.CreateAsset(mesh, Folder + "/OceanQuad.asset");
                }
                sea.AddComponent<MeshFilter>().sharedMesh = mesh;
                var seaRenderer = sea.AddComponent<MeshRenderer>(); seaRenderer.sharedMaterial = water; seaRenderer.shadowCastingMode = ShadowCastingMode.Off;

                var sunlight = new GameObject("Surface sunlight").AddComponent<Light>();
                sunlight.type = LightType.Directional; sunlight.color = new Color(1f, .94f, .82f); sunlight.intensity = 1.1f;
                sunlight.transform.rotation = Quaternion.Euler(38, -35, 0); sunlight.shadows = LightShadows.None;
                string skyPath = Folder + "/SurfaceSky.mat";
                var sky = AssetDatabase.LoadAssetAtPath<Material>(skyPath);
                if (!sky) { sky = new Material(Shader.Find("Skybox/Procedural")); AssetDatabase.CreateAsset(sky, skyPath); }
                sky.SetFloat("_AtmosphereThickness", .8f); sky.SetFloat("_Exposure", 1.15f); sky.SetColor("_SkyTint", new Color(.48f, .59f, .65f));
                sky.SetColor("_GroundColor", new Color(.56f, .74f, .77f));
                RenderSettings.skybox = sky; RenderSettings.sun = sunlight;
                RenderSettings.ambientMode = AmbientMode.Flat; RenderSettings.ambientLight = new Color(.67f, .75f, .78f);
                RenderSettings.fog = true; RenderSettings.fogMode = FogMode.ExponentialSquared;
                RenderSettings.fogDensity = .002f; RenderSettings.fogColor = new Color(.66f, .79f, .83f);
                var camera = new GameObject("Seated viewpoint - desktop visual preview").AddComponent<Camera>();
                camera.tag = "MainCamera"; camera.transform.position = new Vector3(0, 1.57f, .37f);
                camera.transform.rotation = Quaternion.Euler(24, 0, 0); camera.fieldOfView = 72;
                camera.nearClipPlane = .03f; camera.farClipPlane = 1500; camera.clearFlags = CameraClearFlags.Skybox;
                camera.GetUniversalAdditionalCameraData().renderPostProcessing = false;
                camera.gameObject.AddComponent<AudioListener>();
                EditorSceneManager.SaveScene(scene, ScenePath); AssetDatabase.SaveAssets();
                Capture(scene, camera, sunlight);
                File.WriteAllText(Folder + "/README.txt", "SURFACE INTRO VISUAL BLOCKOUT\nScene: " + ScenePath + "\nSeparate from ShallowReef_V4; no existing spawn or gameplay changes.\nOpen this scene to inspect the seated camera or use Scene view to look around. Game view is a static composition preview, not a playable XR intro.\nWood deck / seat / manual position / mooring perch / opaque sea / procedural sky. No external art dependency.\nNo character, bird animation, book interaction, locomotion, water-entry transition or oxygen system yet.\nSeated eye, hips, water entry and bird landing transforms mark art/gameplay handoff points in metres. Replace the platform prefab with boat art later.\nDo not animate the head camera to imitate boat bobbing.\nBuild menu: Reefkeeper > V4 > Build surface intro blockout.\n");
                AssetDatabase.Refresh();
                Debug.Log("[SurfaceIntro] Saved independent scene and three preview screenshots.");
            }
            finally { if (previous.IsValid()) SceneManager.SetActiveScene(previous); EditorSceneManager.CloseScene(scene, true); }
        }
        [MenuItem("Reefkeeper/V4/Open surface intro blockout")]
        static void Open() { if (!Application.isPlaying && EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) EditorSceneManager.OpenScene(ScenePath); }
        static Transform Child(Transform parent, string name) { var t = new GameObject(name).transform; t.SetParent(parent, false); return t; }
        static void Anchor(string name, Transform parent, Vector3 position) { Child(parent, name).localPosition = position; }
        static void Box(string name, Transform parent, Vector3 position, Vector3 scale, Material material)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name; go.transform.SetParent(parent, false);
            go.transform.localPosition = position; go.transform.localScale = scale; go.GetComponent<Renderer>().sharedMaterial = material;
        }
        static Material Material(string name, Color color, float smoothness = .05f)
        {
            string path = Folder + "/" + name + ".mat"; var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!mat) { mat = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(mat, path); }
            mat.SetColor("_BaseColor", color); mat.SetFloat("_Smoothness", smoothness); EditorUtility.SetDirty(mat); return mat;
        }
        static void Label(string text, Transform parent, Vector3 position, float size, Color color)
        {
            var go = new GameObject("Printed manual text"); go.transform.SetParent(parent, false); go.transform.localPosition = position; go.transform.localRotation = Quaternion.Euler(90, 0, 0);
            var label = go.AddComponent<TextMesh>(); label.text = text; label.fontSize = 64; label.characterSize = size; label.anchor = TextAnchor.MiddleCenter; label.alignment = TextAlignment.Center; label.color = color;
            var bounds = label.GetComponent<Renderer>().localBounds.size;
            float fit = Mathf.Min(.26f / Mathf.Max(bounds.x, .001f), .11f / Mathf.Max(bounds.y, .001f));
            go.transform.localScale = Vector3.one * fit;
        }
        static void Capture(Scene scene, Camera camera, Light sunlight)
        {
            var otherLights = Object.FindObjectsByType<Light>(FindObjectsSortMode.None).Where(l => l != sunlight && l.enabled).ToArray();
            var layers = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).Select(t => (t.gameObject, t.gameObject.layer)).ToArray();
            var oldRT = RenderTexture.active; var originalPosition = camera.transform.position; var originalRotation = camera.transform.rotation;
            int mask = camera.cullingMask; var rt = new RenderTexture(1280, 800, 24); rt.antiAliasing = 4;
            try
            {
                foreach (var l in otherLights) l.enabled = false;
                foreach (var item in layers) item.gameObject.layer = 30; camera.cullingMask = 1 << 30;
                foreach (string view in new[] { "seated", "manual", "overview" })
                {
                    camera.transform.SetPositionAndRotation(originalPosition, originalRotation);
                    if (view == "manual") camera.transform.LookAt(new Vector3(-.62f, .855f, .55f));
                    if (view == "overview") { camera.transform.position = new Vector3(3.6f, 3.0f, 4.0f); camera.transform.LookAt(new Vector3(0, .6f, -.25f)); }
                    camera.targetTexture = rt; camera.Render(); RenderTexture.active = rt;
                    var image = new Texture2D(1280, 800, TextureFormat.RGB24, false);
                    try { image.ReadPixels(new Rect(0, 0, 1280, 800), 0, 0); image.Apply(); File.WriteAllBytes(Review + "/" + view + ".png", image.EncodeToPNG()); }
                    finally { Object.DestroyImmediate(image); }
                }
            }
            finally
            {
                camera.transform.SetPositionAndRotation(originalPosition, originalRotation); camera.targetTexture = null; camera.cullingMask = mask;
                foreach (var item in layers) item.gameObject.layer = item.layer;
                foreach (var l in otherLights) if (l) l.enabled = true;
                RenderTexture.active = oldRT; rt.Release(); Object.DestroyImmediate(rt);
            }
        }
    }
}
