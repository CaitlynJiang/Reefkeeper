using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Unity.XR.CoreUtils;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Simulation;
using UnityEngine.XR.Interaction.Toolkit.Feedback;

namespace Reefkeeper.Editor
{
    public static class ReefWorkshopBuilder
    {
        const string Samples = "Assets/Samples/XR Interaction Toolkit/3.3.2/";
        const string Generated = "Assets/Reefkeeper/Generated/";

        public static void InstallAndBuild()
        {
            var scene = EditorSceneManager.OpenScene(ReefSceneBuilder.ScenePath);
            if (!UnityEngine.Object.FindFirstObjectByType<ReefWorkshop>()) Install();
            ConfigureSimulatorVisuals();
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("REEFKEEPER_XR_WORKSHOP_READY");
            ReefSceneBuilder.BuildWindows();
        }

        static void Install()
        {
            var desktop = UnityEngine.Object.FindFirstObjectByType<DiveMotor>().gameObject;
            var ground = GameObject.Find("Rippling sand seabed").GetComponent<MeshCollider>();
            Physics.SyncTransforms();
            if (!ground.Raycast(new Ray(new Vector3(0, 30, -13), Vector3.down), out var hit, 60))
                throw new Exception("Cannot place workshop on seabed.");

            var xrGroup = new GameObject("XR / simulation and player");
            var rig = PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Samples + "Starter Assets/Prefabs/XR Origin (XR Rig).prefab")) as GameObject;
            rig.transform.SetParent(xrGroup.transform, false);
            var origin = rig.GetComponent<XROrigin>();
            origin.RequestedTrackingOriginMode = XROrigin.TrackingOriginMode.Device;
            origin.CameraYOffset = 1.6f;
            rig.transform.position = new Vector3(0, hit.point.y + 0.6f, -15.15f);
            foreach (Transform child in rig.GetComponentsInChildren<Transform>(true))
                // ReefXRSwim owns the thumbsticks; teleport arcs would fight left-stick swimming.
                if (child.name == "Locomotion" || child.name == "Teleport Interactor") child.gameObject.SetActive(false);
            var camera = origin.Camera;
            camera.nearClipPlane = 0.04f; camera.farClipPlane = 110;
            camera.fieldOfView = 72; camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = RenderSettings.fogColor;
            camera.GetUniversalAdditionalCameraData().renderPostProcessing = true;
            var simGO = PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Samples + "XR Interaction Simulator/XR Interaction Simulator.prefab")) as GameObject;
            simGO.transform.SetParent(xrGroup.transform, false);
            var simulator = simGO.GetComponent<XRInteractionSimulator>();
            simulator.cameraTransform = camera.transform;
            simulator.translateXSpeed = 0.65f; simulator.translateYSpeed = 0.65f; simulator.translateZSpeed = 0.65f;
            var cc = rig.GetComponent<CharacterController>();
            if (!cc) cc = rig.AddComponent<CharacterController>();
            cc.height = 1.15f; cc.radius = 0.28f; cc.stepOffset = 0; cc.skinWidth = 0.035f;
            var swimming = rig.AddComponent<ReefXRSwim>();
            swimming.view = camera; swimming.simulator = simulator;
            if (!UnityEngine.Object.FindFirstObjectByType<XRInteractionManager>()) xrGroup.AddComponent<XRInteractionManager>();

            var workshop = new GameObject("WORKSHOP / coral nursery maintenance").AddComponent<ReefWorkshop>();
            workshop.desktopPlayer = desktop; workshop.xrGroup = xrGroup;
            var rack = new GameObject("Nursery rack - replaceable placeholder");
            rack.transform.SetParent(workshop.transform, false);
            rack.transform.position = new Vector3(0, hit.point.y, -13);
            var steel = Material("RackSteel", new Color(0.32f, 0.37f, 0.36f), 0.5f, 0.3f);
            var algae = Material("RackAlgae", new Color(0.2f, 0.25f, 0.075f));
            var wood = Material("BrushHandle", new Color(0.46f, 0.29f, 0.12f));
            var bristles = Material("BrushBristles", new Color(0.75f, 0.74f, 0.57f));
            foreach (float x in new[] { -1.1f, 1.1f }) foreach (float z in new[] { -0.55f, 0.55f })
                Cube("Rack leg", rack.transform, new Vector3(x, 0.6f, z), new Vector3(0.055f, 1.2f, 0.055f), steel);
            foreach (float x in new[] { -1.1f, 1.1f })
                Cube("Side beam", rack.transform, new Vector3(x, 1.2f, 0), new Vector3(0.07f, 0.07f, 1.2f), steel);
            var patches = new List<RackDirtPatch>();
            foreach (float z in new[] { -0.52f, 0, 0.52f })
            {
                Cube("Cleanable crossbar", rack.transform, new Vector3(0, 1.2f, z), new Vector3(2.2f, 0.07f, 0.09f), steel);
                for (int i = 0; i < 8; i++)
                {
                    var patchRoot = new GameObject("Algae patch " + patches.Count);
                    patchRoot.transform.SetParent(rack.transform, false);
                    patchRoot.transform.localPosition = new Vector3(-0.96f + i * 0.275f, 1.248f, z);
                    var contact = patchRoot.AddComponent<BoxCollider>(); contact.isTrigger = true;
                    contact.size = new Vector3(0.26f, 0.025f, 0.11f);
                    var visual = Cube("Algae coating", patchRoot.transform, Vector3.zero, new Vector3(0.26f, 0.018f, 0.11f), algae);
                    UnityEngine.Object.DestroyImmediate(visual.GetComponent<Collider>());
                    var patch = patchRoot.AddComponent<RackDirtPatch>(); patch.dirtVisual = visual.GetComponent<Renderer>();
                    patches.Add(patch);
                }
            }
            workshop.patches = patches.ToArray();
            // Keep the brush visible on a small tray at the front edge.
            Cube("Brush tray", rack.transform, new Vector3(0.6f, 1.3f, -0.62f), new Vector3(0.6f, 0.035f, 0.38f), steel);
            var brush = new GameObject("Restoration brush");
            brush.transform.SetParent(workshop.transform, false);
            brush.transform.position = rack.transform.position + new Vector3(0.6f, 1.43f, -0.76f);
            Cube("Wooden handle", brush.transform, new Vector3(0, 0, 0.02f), new Vector3(0.04f, 0.045f, 0.3f), wood);
            Cube("Brush head", brush.transform, new Vector3(0, 0, 0.27f), new Vector3(0.18f, 0.04f, 0.14f), wood);
            Cube("Bristles", brush.transform, new Vector3(0, -0.05f, 0.27f), new Vector3(0.17f, 0.07f, 0.13f), bristles);
            var tip = new GameObject("Bristle contact point").transform;
            tip.SetParent(brush.transform, false); tip.localPosition = new Vector3(0, -0.086f, 0.27f);
            var body = brush.AddComponent<Rigidbody>(); body.useGravity = false; body.mass = 0.16f;
            body.linearDamping = 8; body.angularDamping = 8; body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            var grip = new GameObject("Grip pose").transform; grip.SetParent(brush.transform, false);
            var grab = brush.AddComponent<XRGrabInteractable>();
            grab.attachTransform = grip; grab.throwOnDetach = false; grab.useDynamicAttach = false;
            grab.movementType = XRBaseInteractable.MovementType.Instantaneous;
            grab.attachEaseInTime = 0.12f;
            workshop.brush = brush.AddComponent<ReefBrush>(); workshop.brush.bristles = tip;
            var home = new GameObject("Brush reset pose").transform;
            home.SetParent(workshop.transform, false); home.SetPositionAndRotation(brush.transform.position, brush.transform.rotation);
            workshop.brushHome = home;
            workshop.gameObject.AddComponent<ReefXRSmokeCheck>();
            desktop.SetActive(false);
        }

        static Material Material(string name, Color color, float metallic = 0, float smoothness = 0.15f)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Material>(Generated + name + ".mat");
            if (existing) return existing;
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
            material.SetColor("_BaseColor", color); material.SetFloat("_Metallic", metallic); material.SetFloat("_Smoothness", smoothness);
            AssetDatabase.CreateAsset(material, Generated + name + ".mat");
            return material;
        }
        static void ConfigureSimulatorVisuals()
        {
            var workshop = UnityEngine.Object.FindFirstObjectByType<ReefWorkshop>();
            var seabed = UnityEngine.Object.FindFirstObjectByType<ReefSeabed>();
            var grounded = new List<Transform>(seabed.groundedProps);
            if (!grounded.Contains(workshop.transform))
            {
                var positions = new Dictionary<Transform, Vector3>();
                foreach (Transform child in workshop.transform) positions.Add(child, child.position);
                workshop.transform.position = workshop.transform.Find("Nursery rack - replaceable placeholder").position;
                foreach (var pair in positions) pair.Key.position = pair.Value;
                grounded.Add(workshop.transform);
                seabed.groundedProps = grounded.ToArray();
            }
            var body = Material("XRControllerBody", new Color(0.1f, 0.13f, 0.15f), 0.2f, 0.35f);
            foreach (var renderer in workshop.xrGroup.GetComponentsInChildren<Renderer>(true))
            {
                if (!(renderer is MeshRenderer) && !(renderer is SkinnedMeshRenderer)) continue;
                var materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++) materials[i] = body;
                renderer.sharedMaterials = materials;
            }
            // Simulated controllers have no vibration hardware; keep their feedback quiet.
            foreach (var feedback in workshop.xrGroup.GetComponentsInChildren<SimpleHapticFeedback>(true)) feedback.enabled = false;
        }
        static GameObject Cube(string name, Transform parent, Vector3 position, Vector3 size, Material material)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name;
            go.transform.SetParent(parent, false); go.transform.localPosition = position; go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = material;
            return go;
        }
    }
}
