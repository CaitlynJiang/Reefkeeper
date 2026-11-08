using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Reefkeeper.Corals;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using Object = UnityEngine.Object;

namespace Reefkeeper.Editor
{
    /// <summary>Creates a separate V3 scene from V2; never overwrites an existing V3 layout.</summary>
    public static class V3CleaningSetup
    {
        public const string ScenePath = "Assets/Reefkeeper/Scenes/ShallowReef_V3.unity";
        const string Assets = "Assets/Reefkeeper/Generated/V3";

        [MenuItem("Reefkeeper/V3/Set up dual cleaning tools")]
        public static void Run()
        {
            Directory.CreateDirectory(Assets);
            AssetDatabase.Refresh();
            var scene = EditorSceneManager.OpenScene(File.Exists(ScenePath) ? ScenePath : V2.PicoSetup.ScenePath);
            var workshop = Object.FindFirstObjectByType<ReefWorkshop>();
            if (!workshop) throw new InvalidOperationException("The nursery workshop is missing.");
            var pickup = workshop.GetComponent<ReefBrushPickup>();
            if (!workshop.scraper) CreateScraper(workshop, pickup);

            // Deterministic placement, keeping the tutorial's algae and the original V2 scene intact.
            var sockets = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<RackSocket>(true))
                .Where(s => s.moss && s.kind == RackSocket.Kind.Table && !s.GetComponentInParent<CoralGrowth>())
                .OrderBy(s => Vector3.Distance(s.transform.position, workshop.brushHome.position)).ToArray();
            if (!sockets.Any(s => s.moss.surfaceKind == ReefSurfaceKind.FireCoral))
            {
                if (sockets.Length < 2) throw new InvalidOperationException("V3 requires at least two table planting spots.");
                var prefab = CreateFireCoralPad();
                for (int i = 1; i < sockets.Length; i += 3)
                {
                    var socket = sockets[i];
                    var old = socket.moss.transform;
                    var pad = (RackDirtPatch)PrefabUtility.InstantiatePrefab(prefab, old.parent);
                    pad.transform.SetLocalPositionAndRotation(old.localPosition, old.localRotation);
                    pad.transform.localScale = old.localScale;
                    Object.DestroyImmediate(old.gameObject);
                    socket.moss = pad;
                    EditorUtility.SetDirty(socket);
                }
            }
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            Validate();
            Debug.Log("V3_CLEANING_READY: algae + brush; fire coral + scraper. Open " + ScenePath);
        }

        static void CreateScraper(ReefWorkshop workshop, ReefBrushPickup pickup)
        {
            var root = new GameObject("Scraping tool");
            root.transform.SetParent(workshop.brush.transform.parent, false);
            root.transform.SetPositionAndRotation(workshop.brushHome.position + workshop.brushHome.right * 0.30f,
                workshop.brushHome.rotation);
            var rubber = Material("ScraperGrip", new Color(0.08f, 0.30f, 0.33f), 0, 0.3f);
            var steel = Material("ScraperBlade", new Color(0.72f, 0.81f, 0.79f), 0.65f, 0.6f);
            var accent = Material("ScraperBand", new Color(1f, 0.63f, 0.22f), 0.1f, 0.3f);
            Shape("Handle", root.transform, PrimitiveType.Cube, new Vector3(0, 0, 0.07f), new Vector3(0.05f, 0.045f, 0.24f), rubber, true);
            Shape("Safety band", root.transform, PrimitiveType.Cube, new Vector3(0, 0, 0.18f), new Vector3(0.064f, 0.055f, 0.028f), accent, false);
            Shape("Blade", root.transform, PrimitiveType.Cube, new Vector3(0, -0.015f, 0.27f), new Vector3(0.18f, 0.018f, 0.14f), steel, true);
            var tip = new GameObject("Blade contact point").transform;
            tip.SetParent(root.transform, false); tip.localPosition = new Vector3(0, -0.025f, 0.33f);
            var grip = new GameObject("Grip pose").transform; grip.SetParent(root.transform, false);
            var body = root.AddComponent<Rigidbody>();
            body.useGravity = false; body.mass = 0.18f; body.linearDamping = 8; body.angularDamping = 8;
            var grab = root.AddComponent<ReefBrushInteractable>();
            grab.owner = pickup; grab.outlineMaterial = pickup.brush.outlineMaterial;
            grab.interactionManager = pickup.brush.interactionManager;
            grab.attachTransform = grip; grab.throwOnDetach = false; grab.useDynamicAttach = false;
            grab.movementType = XRBaseInteractable.MovementType.Instantaneous; grab.attachEaseInTime = 0.12f;
            grab.colliders.Clear(); grab.colliders.AddRange(root.GetComponentsInChildren<Collider>());
            var tool = root.AddComponent<ReefBrush>();
            tool.toolKind = CleaningToolKind.Scraper; tool.bristles = tip; tool.contactRadius = 0.06f;
            workshop.scraper = tool; pickup.scraper = grab;
            var home = new GameObject("Scraper reset pose").transform;
            home.SetParent(workshop.transform, false); home.SetPositionAndRotation(root.transform.position, root.transform.rotation);
            workshop.scraperHome = home;
            EditorUtility.SetDirty(workshop); EditorUtility.SetDirty(pickup);
        }

        static RackDirtPatch CreateFireCoralPad()
        {
            // A distinct encrusting placeholder on the man-made nursery, separate from RedCoral stock.
            var root = new GameObject("Fire coral on planting surface");
            var patch = root.AddComponent<RackDirtPatch>();
            patch.surfaceKind = ReefSurfaceKind.FireCoral; patch.requiredTravel = 1.8f;
            var contact = root.AddComponent<BoxCollider>();
            contact.isTrigger = true; contact.center = new Vector3(0, 0.08f, 0); contact.size = new Vector3(1.05f, 0.30f, 1.05f);
            var body = Material("FireCoralCoating", new Color(0.94f, 0.38f, 0.10f), 0, 0.25f);
            var ends = Material("FireCoralTips", new Color(1f, 0.78f, 0.34f), 0, 0.2f);
            var coat = Shape("Fire coral crust", root.transform, PrimitiveType.Sphere, new Vector3(0, 0.025f, 0),
                new Vector3(0.94f, 0.07f, 0.94f), body, false);
            patch.dirtVisual = coat.GetComponent<Renderer>();
            var branches = new List<Transform>();
            for (int i = 0; i < 11; i++)
            {
                float angle = i * 2.39996f, radius = 0.32f * Mathf.Sqrt((i + 0.5f) / 11f);
                var growth = new GameObject("Fire coral lobe " + i).transform;
                growth.SetParent(root.transform, false);
                growth.localPosition = new Vector3(Mathf.Cos(angle) * radius, 0.025f, Mathf.Sin(angle) * radius);
                float height = 0.09f + i % 3 * 0.035f;
                Shape("Lobe", growth, PrimitiveType.Capsule, new Vector3(0, height * 0.5f, 0),
                    new Vector3(0.065f, height * 0.5f, 0.045f), i % 3 == 0 ? ends : body, false);
                branches.Add(growth);
            }
            patch.growth = branches.ToArray();
            var saved = PrefabUtility.SaveAsPrefabAsset(root, Assets + "/FireCoralPad.prefab").GetComponent<RackDirtPatch>();
            Object.DestroyImmediate(root);
            return saved;
        }

        static Material Material(string name, Color color, float metallic, float smoothness)
        {
            string path = Assets + "/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material) return material;
            material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
            material.SetColor("_BaseColor", color); material.SetColor("_Color", color);
            material.SetFloat("_Metallic", metallic); material.SetFloat("_Smoothness", smoothness);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        static GameObject Shape(string name, Transform parent, PrimitiveType type, Vector3 position, Vector3 scale, Material material, bool collider)
        {
            var shape = GameObject.CreatePrimitive(type); shape.name = name;
            shape.transform.SetParent(parent, false); shape.transform.localPosition = position; shape.transform.localScale = scale;
            shape.GetComponent<Renderer>().sharedMaterial = material;
            if (!collider) Object.DestroyImmediate(shape.GetComponent<Collider>());
            return shape;
        }

        [MenuItem("Reefkeeper/V3/Validate dual cleaning scene")]
        public static void Validate()
        {
            var workshop = Object.FindFirstObjectByType<ReefWorkshop>();
            if (!workshop || !workshop.scraper || !workshop.scraperHome) throw new InvalidOperationException("Missing scraper or reset pose.");
            var pickup = workshop.GetComponent<ReefBrushPickup>();
            if (!pickup.scraper || pickup.scraper.owner != pickup) throw new InvalidOperationException("Scraper pickup is not wired.");
            if (workshop.brush.toolKind != CleaningToolKind.Brush || workshop.scraper.toolKind != CleaningToolKind.Scraper)
                throw new InvalidOperationException("Wrong tool types.");
            var pads = Object.FindObjectsByType<RackDirtPatch>(FindObjectsSortMode.None);
            if (!pads.Any(p => p.surfaceKind == ReefSurfaceKind.Algae) || !pads.Any(p => p.surfaceKind == ReefSurfaceKind.FireCoral))
                throw new InvalidOperationException("Both cleaning surfaces must be present.");
            if (pads.Any(p => !p.dirtVisual || !p.GetComponent<Collider>())) throw new InvalidOperationException("Incomplete cleaning surface.");
            Debug.Log("V3_SCENE_VALIDATED: " + pads.Length + " cleaning surfaces.");
        }

        // Batch entry point: imports/compiles, creates the scene, then exercises actual XRI and physics in Play Mode.
        public static void RunAndCheck()
        {
            Run();
            EditorApplication.EnterPlaymode();
        }
    }
}
