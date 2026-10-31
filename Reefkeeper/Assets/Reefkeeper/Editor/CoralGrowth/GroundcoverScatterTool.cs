using System.Collections.Generic;
using System.Linq;
using Reefkeeper.Corals;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Reefkeeper.CoralGrowthTools
{
    /// <summary>
    /// Art-directed ground cover for ShallowReef_V2 (a mesh seabed, so no Terrain detail brush):
    /// clover hugs selected rocks, purple leaf tufts pool under selected seaweed patches.
    /// A few rich hero areas near the start, smaller patches further out, the far reef left bare.
    /// Re-run to rebuild; everything lives under one root object.
    /// </summary>
    public static class GroundcoverScatterTool
    {
        const string ScenePath = "Assets/Reefkeeper/Scenes/ShallowReef_V2.unity";
        const string RootName = "GROUND COVER / 地被";
        const string Clover = "Ground_Clover", Purple = "Ground_FuzzMoss";

        enum Kind { Rock, Seaweed }

        // (kind, approximate x, z of the anchor object, emphasis 0..1, side the patch drifts toward in degrees)
        // Emphasis drives size and density: 1 = hero, ~0.5 = medium, ~0.2 = a light sprinkle.
        static readonly (Kind kind, float x, float z, float emphasis, float drift)[] Plan =
        {
            // hero 1: rock pair right of the start, with its seaweed patch
            (Kind.Rock, 8.4f, 1.9f, 1.0f, 200f), (Kind.Rock, 9.0f, 3.2f, 0.7f, 120f), (Kind.Seaweed, 7.7f, 3.5f, 0.9f, 250f),
            // hero 2: big rock on the left, small rock and seaweed at its foot
            (Kind.Rock, -11.0f, 4.1f, 1.0f, 330f), (Kind.Rock, -11.0f, 2.9f, 0.6f, 260f), (Kind.Seaweed, -9.3f, 4.2f, 0.95f, 300f),
            // hero 3: the shipwreck's flanks
            (Kind.Seaweed, 5.5f, 11.3f, 0.85f, 200f), (Kind.Seaweed, -6.8f, 12.4f, 0.8f, 340f),
            (Kind.Rock, 6.5f, 8.8f, 0.75f, 180f), (Kind.Rock, -6.8f, 14.8f, 0.6f, 20f),
            // medium: near the workshop and the sides
            (Kind.Rock, -5.0f, -9.7f, 0.55f, 60f), (Kind.Seaweed, -5.7f, -15.5f, 0.45f, 90f),
            (Kind.Rock, 8.5f, -14.8f, 0.45f, 140f), (Kind.Seaweed, 14.5f, -0.7f, 0.5f, 220f), (Kind.Rock, 16.3f, -5.9f, 0.35f, 200f),
            // light accents further out
            (Kind.Seaweed, -15.4f, -7.4f, 0.25f, 40f), (Kind.Rock, -16.1f, -2.4f, 0.2f, 0f), (Kind.Seaweed, 16.0f, 11.2f, 0.25f, 260f),
        };

        [MenuItem("Reefkeeper/CoralGrowth/8. Scatter ground cover in ShallowReef_V2 - 在 V2 场景布置地被")]
        public static void Scatter()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path != ScenePath)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }
            var prefabs = GroundcoverBuilder.BuildPrefabs();
            if (!prefabs.ContainsKey(Clover) || !prefabs.ContainsKey(Purple)) return;

            var seabed = GameObject.Find("REEF / environment/Rippling sand seabed");
            var seabedCol = seabed ? seabed.GetComponentInChildren<Collider>() : null;
            if (!seabedCol) { Debug.LogError("[Groundcover] Seabed collider not found."); return; }

            var rocks = GameObject.Find("REEF / environment").transform.Cast<Transform>()
                .Where(t => t.name.StartsWith("Reef limestone")).ToList();
            var weeds = GameObject.Find("REEF / corals and seagrass/Seaweed 水草 (3x3 patches)").transform.Cast<Transform>().ToList();
            // corals have no colliders: keep clumps out of each coral's footprint
            coralFootprints = GameObject.Find("REEF / corals and seagrass").transform.Cast<Transform>()
                .Where(t => t.name.StartsWith("Coral garden")).SelectMany(t => t.GetComponentsInChildren<Renderer>())
                .Select(r => r.bounds).ToList();

            foreach (var r in scene.GetRootGameObjects())
                if (r.name == RootName) Object.DestroyImmediate(r);
            var root = new GameObject(RootName).transform;

            int total = 0, seed = 1;
            foreach (var a in Plan)
            {
                var pool = a.kind == Kind.Rock ? rocks : weeds;
                var anchor = pool.OrderBy(t => (Flat(Centre(t)) - new Vector2(a.x, a.z)).sqrMagnitude).First();
                var centre = Centre(anchor);
                float footprint = a.kind == Kind.Rock ? Radius(anchor) : 1.1f;      // seaweed patch half-size
                var patch = new GameObject($"{(a.kind == Kind.Rock ? "Rock" : "Seaweed")} · {anchor.name}").transform;
                patch.SetParent(root, false);
                var lists = new Dictionary<string, List<Matrix4x4>> { [Clover] = new List<Matrix4x4>(), [Purple] = new List<Matrix4x4>() };
                ScatterAround(a.kind, centre, footprint, a.emphasis, a.drift, seabedCol, lists, new System.Random(seed++ * 7919));
                foreach (var kv in lists)
                {
                    if (kv.Value.Count == 0) continue;
                    var go = new GameObject(kv.Key == Clover ? "Clover 三叶草" : "Purple tufts 紫色叶丛");
                    go.transform.SetParent(patch, false);
                    var s = go.AddComponent<GroundcoverScatter>();
                    s.mesh = prefabs[kv.Key].GetComponent<MeshFilter>().sharedMesh;
                    s.material = prefabs[kv.Key].GetComponent<MeshRenderer>().sharedMaterial;
                    s.SetInstances(kv.Value);
                    total += kv.Value.Count;
                }
            }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Selection.activeGameObject = root.gameObject;
            Debug.Log($"[Groundcover] {total} clumps in {Plan.Length} patches.");
        }

        /// <summary>
        /// One organic patch: densest at the anchor's foot, thinning outward through noise so the edge
        /// breaks into islands; drifts to one side; the main type for the anchor with a few of the other.
        /// </summary>
        static void ScatterAround(Kind kind, Vector3 centre, float footprint, float emphasis, float driftDeg, Collider seabed,
            Dictionary<string, List<Matrix4x4>> lists, System.Random rng)
        {
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            float spread = Mathf.Lerp(1.2f, 4.2f, emphasis);                // how far the carpet reaches past the anchor
            int tries = Mathf.RoundToInt(Mathf.Lerp(90, 900, emphasis) * (0.6f + footprint * 0.25f));
            float drift = driftDeg * Mathf.Deg2Rad;
            float noiseOff = R(0, 100);
            string main = kind == Kind.Rock ? Clover : Purple, other = kind == Kind.Rock ? Purple : Clover;
            float inner = kind == Kind.Rock ? footprint * 0.85f : 0f;       // seaweed: fill underneath too

            var placed = new List<(string type, Matrix4x4 m)>();
            for (int i = 0; i < tries; i++)
            {
                float ang = R(0, Mathf.PI * 2);
                float lean = 0.55f + 0.45f * Mathf.Cos(ang - drift);         // reach further on the drift side
                float t = Mathf.Pow((float)rng.NextDouble(), 1.6f);           // biased toward the anchor
                float d = inner + (footprint - inner + spread * lean) * t;
                var p = centre + new Vector3(Mathf.Cos(ang) * d, 0, Mathf.Sin(ang) * d);

                float edge = d / (footprint + spread * lean);                // 0 at the anchor, 1 at the rim
                float n = Mathf.PerlinNoise(p.x * 0.45f + noiseOff, p.z * 0.45f + noiseOff);
                if (n + (1 - edge) * 0.75f < 0.85f) continue;                 // broken, island-y edges

                if (InCoral(p) || !Ground(p, seabed, out var hit)) continue;
                string type = rng.NextDouble() < (kind == Kind.Rock ? 0.12 + 0.2 * edge : 0.08 + 0.15 * edge) ? other : main;
                var up = Vector3.Slerp(Vector3.up, hit.normal, 0.6f);
                var rot = Quaternion.FromToRotation(Vector3.up, up) * Quaternion.Euler(0, R(0, 360), 0);
                float s = R(1.5f, 2.2f) * Mathf.Lerp(1.3f, 0.85f, edge);     // this reef is big: clumps ~0.4-0.6 m, fuller near the anchor
                placed.Add((type, Matrix4x4.TRS(hit.point - up * 0.01f, rot, new Vector3(s, s * R(0.85f, 1.15f), s))));
            }
            // drop stragglers: a clump needs a few neighbours, so every patch reads as one drift
            for (int i = 0; i < placed.Count; i++)
            {
                Vector3 pi = placed[i].m.GetColumn(3);
                int near = 0;
                for (int j = 0; j < placed.Count && near < 3; j++)
                    if (i != j && ((Vector3)placed[j].m.GetColumn(3) - pi).sqrMagnitude < 0.8f * 0.8f) near++;
                if (near >= 3) lists[placed[i].type].Add(placed[i].m);
            }
        }

        /// <summary>Finds the seabed below p; refuses spots covered by a rock, the wreck or anything else.</summary>
        static bool Ground(Vector3 p, Collider seabed, out RaycastHit seabedHit)
        {
            seabedHit = default;
            var hits = Physics.RaycastAll(new Vector3(p.x, 60f, p.z), Vector3.down, 120f, ~0, QueryTriggerInteraction.Ignore);
            if (hits.Length == 0) return false;
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            if (hits[0].collider != seabed) return false;                    // something sits on top here
            seabedHit = hits[0];
            return seabedHit.normal.y > 0.55f;                                // not on steep dune faces
        }

        static List<Bounds> coralFootprints = new List<Bounds>();

        static bool InCoral(Vector3 p)
        {
            foreach (var b in coralFootprints)
            {
                float ex = Mathf.Max(b.extents.x * 0.75f, 0.08f), ez = Mathf.Max(b.extents.z * 0.75f, 0.08f);
                float dx = (p.x - b.center.x) / ex, dz = (p.z - b.center.z) / ez;
                if (dx * dx + dz * dz < 1f) return true;
            }
            return false;
        }

        static Bounds RendererBounds(Transform t)
        {
            var rs = t.GetComponentsInChildren<Renderer>();
            var b = rs.Length > 0 ? rs[0].bounds : new Bounds(t.position, Vector3.zero);
            foreach (var r in rs) b.Encapsulate(r.bounds);
            return b;
        }

        static Vector3 Centre(Transform t) => RendererBounds(t).center;
        static Vector2 Flat(Vector3 v) => new Vector2(v.x, v.z);
        static float Radius(Transform t) { var b = RendererBounds(t); return Mathf.Max(b.extents.x, b.extents.z) * 0.9f; }
    }
}
