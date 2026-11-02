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
                    {
                        // Only species this socket grade allows.
                        var allowed = CoralNames.Where(n => CoralRules.CanPlant(n, socket.grade)).ToArray();
                        if (allowed.Length > 0) socket.occupant = SpawnCoral(allowed[rng.Next(allowed.Length)], rng);
                    }
                    if (!socket.occupant)
                    {
                        // A coral deleted by hand leaves its line on the hook.
                        foreach (Transform c in socket.transform.Cast<Transform>().ToArray())
                            if (c.name == "Coral line") Object.DestroyImmediate(c.gameObject);
                        continue;
                    }
                    var fragment = socket.occupant.GetComponent<ReefCoralFragment>();
                    if (!fragment) fragment = CoralSeating.MakePickable(socket.occupant, pickup, outline, manager);
                    fragment.suspensionLine = CoralSeating.Seat(socket.occupant, socket, fragment.suspensionLine,
                        AssetDatabase.LoadAssetAtPath<Material>(RopePath));
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

    }
}
