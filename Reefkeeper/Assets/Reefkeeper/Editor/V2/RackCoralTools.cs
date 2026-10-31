using System.Collections.Generic;
using System.Linq;
using Reefkeeper.Corals;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace Reefkeeper.V2
{
    /// <summary>
    /// Keeps the workshop's rack corals tied to their rack sockets. Each coral is a child of
    /// its socket, so moving, copying or deleting a rack takes its corals along. Run
    /// "Fix rack corals" after rearranging racks by hand: it re-seats every occupant, removes
    /// stray corals and lines, and rebuilds the pickup list.
    /// </summary>
    public static class RackCoralTools
    {
        public static readonly string[] CoralNames =
        {
            "BarrelSponge", "BranchCoral", "DiscCoral", "FingerCoral", "PolypPillar_Coral",
            "PolypPillar_Pink", "TendrilCoral", "TubeSponge_Blossom", "TubeSponge_Lemon", "TubeSponge_Magenta",
        };
        const string PrefabDir = "Assets/Reefkeeper/Prefabs/CoralGrowth";
        const string RopePath = "Assets/Reefkeeper/Generated/NurseryRope.mat";
        const string StrayGroupPrefix = "Rack corals";
        const float HangGap = 0.06f;      // space between hook and coral top, filled by a line
        const float LineWidth = 0.006f;

        [MenuItem("Reefkeeper/V2/Fix rack corals - 修复架子珊瑚")]
        static void FixMenu() => RunOnWorkshop(false);

        [MenuItem("Reefkeeper/V2/Refill rack corals - 补满架子珊瑚 (table + every other hook)")]
        static void RefillMenu() => RunOnWorkshop(true);

        static void RunOnWorkshop(bool refill)
        {
            var workshop = Object.FindFirstObjectByType<ReefWorkshop>();
            if (!workshop) { Debug.LogWarning("[V2] No ReefWorkshop in the open scene."); return; }
            Undo.RegisterFullObjectHierarchyUndo(workshop.gameObject, refill ? "Refill rack corals" : "Fix rack corals");
            int count = Fix(workshop, refill, new System.Random());
            EditorSceneManager.MarkSceneDirty(workshop.gameObject.scene);
            Debug.Log($"[V2] Rack corals fixed: {count} pickable corals.");
        }

        /// <summary>Re-seats every socket's coral, optionally fills empty sockets, clears strays. Returns the coral count.</summary>
        public static int Fix(ReefWorkshop workshop, bool refill, System.Random rng, Material outline = null)
        {
            var pickup = workshop.GetComponent<ReefCoralPickup>();
            var sample = workshop.GetComponentsInChildren<ReefCoralFragment>(true).FirstOrDefault()
                         ?? pickup.fragments.FirstOrDefault(f => f);
            if (!outline && sample) outline = sample.outlineMaterial;
            var manager = sample && sample.interactionManager ? sample.interactionManager : workshop.GetComponent<XRInteractionManager>();

            var kept = new HashSet<GameObject>();
            foreach (var rack in RacksUnder(workshop.transform))
            {
                int hook = 0;
                foreach (var socket in rack.GetComponentsInChildren<RackSocket>(true))
                {
                    bool wanted = socket.kind == RackSocket.Kind.Table || hook++ % 2 == 0;
                    if (!socket.occupant && refill && wanted)
                        socket.occupant = SpawnCoral(CoralNames[rng.Next(CoralNames.Length)], rng);
                    if (!socket.occupant)
                    {
                        // A coral deleted by hand leaves its line on the hook.
                        foreach (Transform c in socket.transform.Cast<Transform>().ToArray())
                            if (c.name == "Coral line") Object.DestroyImmediate(c.gameObject);
                        continue;
                    }
                    var fragment = socket.occupant.GetComponent<ReefCoralFragment>();
                    if (!fragment) fragment = MakePickable(socket.occupant, pickup, outline, manager);
                    Seat(socket.occupant, socket, fragment);
                    kept.Add(socket.occupant.gameObject);
                    EditorUtility.SetDirty(socket);
                }
            }

            // Corals and lines left behind by deleted or moved racks.
            foreach (Transform group in workshop.transform.Cast<Transform>().ToArray())
            {
                if (!group.name.StartsWith(StrayGroupPrefix)) continue;
                foreach (Transform child in group.Cast<Transform>().ToArray())
                    if (!kept.Contains(child.gameObject)) Object.DestroyImmediate(child.gameObject);
                if (group.childCount == 0) Object.DestroyImmediate(group.gameObject);
            }
            foreach (var fragment in workshop.GetComponentsInChildren<ReefCoralFragment>(true))
                if (!kept.Contains(fragment.gameObject)) Object.DestroyImmediate(fragment.gameObject);

            pickup.fragments = workshop.GetComponentsInChildren<ReefCoralFragment>(true);
            EditorUtility.SetDirty(pickup);
            return pickup.fragments.Length;
        }

        // A rack is any object under the workshop that carries RackSockets.
        static IEnumerable<Transform> RacksUnder(Transform workshop) =>
            workshop.GetComponentsInChildren<RackSocket>(true)
                .Select(s => s.GetComponentInParent<MeshCollider>() ? s.GetComponentInParent<MeshCollider>().transform : s.transform.parent)
                .Distinct();

        public static CoralGrowth SpawnCoral(string name, System.Random rng)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabDir}/{name}.prefab");
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            go.transform.rotation = Quaternion.Euler(0, (float)rng.NextDouble() * 360f, 0);
            var coral = go.GetComponent<CoralGrowth>();
            coral.SetStage(0, false);
            return coral;
        }

        /// <summary>
        /// Puts the coral under its socket at world scale 1: sitting on a table socket, or
        /// hanging just below a hook on a short line.
        /// </summary>
        public static void Seat(CoralGrowth coral, RackSocket socket, ReefCoralFragment fragment)
        {
            var t = coral.transform;
            var yaw = Quaternion.Euler(0, t.eulerAngles.y, 0);
            t.SetParent(socket.transform, false);
            t.rotation = yaw;
            t.localScale = Divide(Vector3.one, socket.transform.lossyScale);
            t.position = socket.transform.position;

            var line = fragment ? fragment.suspensionLine : null;
            if (socket.kind == RackSocket.Kind.Table)
            {
                if (line) Object.DestroyImmediate(line);
                if (fragment) fragment.suspensionLine = null;
                return;
            }

            float height = coral.Current.GetComponentsInChildren<Renderer>().Max(r => r.bounds.max.y) - t.position.y;
            t.position = socket.transform.position + Vector3.down * (height + HangGap);
            if (!line)
            {
                line = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                Object.DestroyImmediate(line.GetComponent<Collider>());
                line.name = "Coral line";
                var rope = AssetDatabase.LoadAssetAtPath<Material>(RopePath);
                if (rope) line.GetComponent<Renderer>().sharedMaterial = rope;
            }
            line.transform.SetParent(socket.transform, false);
            line.transform.SetPositionAndRotation(socket.transform.position + Vector3.down * (HangGap * 0.5f), Quaternion.identity);
            line.transform.localScale = Divide(new Vector3(LineWidth, HangGap * 0.5f, LineWidth), socket.transform.lossyScale);
            if (fragment) fragment.suspensionLine = line;
        }

        /// <summary>Wraps a growth coral so the existing pickup flow can grab it.</summary>
        public static ReefCoralFragment MakePickable(CoralGrowth coral, ReefCoralPickup pickup, Material outline, XRInteractionManager manager)
        {
            var go = coral.gameObject;
            var bounds = new Bounds(go.transform.position, Vector3.zero);
            foreach (var r in coral.Current.GetComponentsInChildren<Renderer>()) bounds.Encapsulate(r.bounds);
            var box = go.AddComponent<BoxCollider>();
            box.center = go.transform.InverseTransformPoint(bounds.center);
            var size = go.transform.InverseTransformVector(bounds.size);
            box.size = Vector3.Max(new Vector3(Mathf.Abs(size.x), Mathf.Abs(size.y), Mathf.Abs(size.z)), Vector3.one * 0.12f);

            var fragment = go.AddComponent<ReefCoralFragment>(); // brings its Rigidbody
            var body = go.GetComponent<Rigidbody>();
            body.useGravity = false; body.isKinematic = true; body.mass = 0.04f;

            fragment.owner = pickup;
            fragment.outlineMaterial = outline;
            fragment.interactionManager = manager;
            fragment.movementType = XRBaseInteractable.MovementType.Instantaneous;
            fragment.throwOnDetach = false;
            fragment.useDynamicAttach = false;
            fragment.attachEaseInTime = 0.12f;
            fragment.selectMode = InteractableSelectMode.Single;
            fragment.colliders.Clear();
            fragment.colliders.Add(box);
            var grip = new GameObject("Grip pose").transform;
            grip.SetParent(go.transform, false);
            grip.position = bounds.center;
            fragment.attachTransform = grip;
            return fragment;
        }

        static Vector3 Divide(Vector3 a, Vector3 b) => new Vector3(a.x / b.x, a.y / b.y, a.z / b.z);
    }
}
