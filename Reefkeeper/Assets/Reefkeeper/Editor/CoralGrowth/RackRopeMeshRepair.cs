using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Reefkeeper.CoralGrowthTools
{
    /// <summary>
    /// Repairs the legacy hanging rack FBX, whose global decimator collapsed cylindrical
    /// cords into 2-4 triangles. Edits the imported mesh in place, keeping its asset IDs,
    /// atlas, sockets, renderer and collider references. No runtime work or extra draws.
    /// New exports preserve their cords and therefore pass through unchanged.
    /// </summary>
    public sealed class RackRopeMeshRepair : AssetPostprocessor
    {
        const string ModelPath = "Assets/Reefkeeper/Meshes/CoralGrowth/RackBases/RackBase_Hanging.fbx";
        public override uint GetVersion() => 2;

        void OnPostprocessModel(GameObject model)
        {
            if (assetPath != ModelPath) return;
            var sockets = model.GetComponentsInChildren<Transform>(true)
                .Where(t => t.name.StartsWith("Socket_Hang_", StringComparison.Ordinal))
                .Select(t => model.transform.InverseTransformPoint(t.position)).ToArray();
            foreach (var filter in model.GetComponentsInChildren<MeshFilter>(true))
            {
                int count = Repair(filter.sharedMesh, sockets);
                if (count > 0) Debug.Log($"[RackRope] Restored {count} closed six-sided cords in {assetPath}.");
            }
        }

        public static int Repair(Mesh mesh, Vector3[] sockets)
        {
            if (!mesh || mesh.subMeshCount != 1 || sockets.Length == 0) return 0;
            var vertices = mesh.vertices;
            var normals = mesh.normals;
            var uv = mesh.uv;
            var triangles = mesh.triangles;
            if (normals.Length != vertices.Length || uv.Length != vertices.Length) return 0;

            // Weld only for connectivity; retain the original UV seams and normals in the output.
            var parent = Enumerable.Range(0, vertices.Length).ToArray();
            var welded = new Dictionary<Vector3Int, int>();
            Func<int, int> find = null;
            find = i => parent[i] == i ? i : parent[i] = find(parent[i]);
            Action<int, int> join = (a, b) => parent[find(a)] = find(b);
            for (int i = 0; i < vertices.Length; i++)
            {
                var p = vertices[i] * 100000f;
                var key = new Vector3Int(Mathf.RoundToInt(p.x), Mathf.RoundToInt(p.y), Mathf.RoundToInt(p.z));
                if (welded.TryGetValue(key, out int other)) join(i, other); else welded[key] = i;
            }
            for (int i = 0; i < triangles.Length; i += 3) { join(triangles[i], triangles[i + 1]); join(triangles[i], triangles[i + 2]); }
            var groups = new Dictionary<int, List<int>>();
            for (int i = 0; i < triangles.Length; i += 3)
            {
                int root = find(triangles[i]);
                if (!groups.TryGetValue(root, out var faces)) groups[root] = faces = new List<int>();
                faces.Add(i);
            }
            var replacements = new List<(int root, Vector3 bottom, Vector3 top, float radius, Vector2 uv)>();
            foreach (var group in groups)
            {
                // A proper closed six-sided cylinder has 20+ triangles; do not touch it.
                if (group.Value.Count > 4) continue;
                var ids = group.Value.SelectMany(i => new[] { triangles[i], triangles[i + 1], triangles[i + 2] }).Distinct().ToArray();
                var bounds = new Bounds(vertices[ids[0]], Vector3.zero);
                foreach (int id in ids) bounds.Encapsulate(vertices[id]);
                var bottom = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
                var top = new Vector3(bottom.x, bounds.max.y, bottom.z);
                float radius = .003f;
                if (bounds.size.y >= .08f && bounds.size.x <= .012f && bounds.size.z <= .012f)
                {
                    var socket = sockets.OrderBy(s => (s - bottom).sqrMagnitude).First();
                    if ((socket - bottom).sqrMagnitude < .0001f) bottom = socket;
                    else if (Mathf.Abs(bottom.x) < .006f && Mathf.Abs(bottom.z) < .006f
                             && Mathf.Abs(bottom.y - 1.78f) < .01f && Mathf.Abs(bounds.max.y - 2.03f) < .01f)
                    { bottom.x = bottom.z = 0; radius = .004f; }
                    else continue;
                    top = new Vector3(bottom.x, bounds.max.y, bottom.z);
                }
                else
                {
                    // The two short diagonal buoy cords have known anchors in the legacy generator.
                    var buoyAnchor = new Vector3(0, 2, 0);
                    var ends = new[] { new Vector3(.09f, 2.01f, .03f), new Vector3(-.07f, 2.02f, -.05f) };
                    var end = ends.FirstOrDefault(p => Vector3.Distance(bounds.center, (buoyAnchor + p) * .5f) < .008f);
                    if (end == Vector3.zero || bounds.size.magnitude < .05f) continue;
                    bottom = buoyAnchor; top = end;
                }

                // Sample inside an existing rope triangle, not across separate atlas islands.
                int face = group.Value.OrderByDescending(i => Vector3.Cross(vertices[triangles[i + 1]] - vertices[triangles[i]], vertices[triangles[i + 2]] - vertices[triangles[i]]).sqrMagnitude).First();
                var sample = (uv[triangles[face]] + uv[triangles[face + 1]] + uv[triangles[face + 2]]) / 3f;
                replacements.Add((group.Key, bottom, top, radius, sample));
            }
            if (replacements.Count == 0) return 0;
            var removed = new HashSet<int>(replacements.Select(r => r.root));
            var resultVertices = new List<Vector3>(vertices);
            var resultNormals = new List<Vector3>(normals);
            var resultUv = new List<Vector2>(uv);
            var resultTriangles = new List<int>(triangles.Length + replacements.Count * 72);
            for (int i = 0; i < triangles.Length; i += 3)
                if (!removed.Contains(find(triangles[i]))) resultTriangles.AddRange(new[] { triangles[i], triangles[i + 1], triangles[i + 2] });
            foreach (var rope in replacements)
                AddCylinder(rope.bottom, rope.top, rope.radius, rope.uv, resultVertices, resultNormals, resultUv, resultTriangles);
            mesh.SetVertices(resultVertices); mesh.SetNormals(resultNormals); mesh.SetUVs(0, resultUv);
            mesh.SetTriangles(resultTriangles, 0); mesh.RecalculateBounds(); mesh.RecalculateTangents();
            return replacements.Count;
        }

        static void AddCylinder(Vector3 bottom, Vector3 top, float radius, Vector2 uv,
            List<Vector3> vertices, List<Vector3> normals, List<Vector2> uvs, List<int> triangles)
        {
            const int sides = 6;
            int start = vertices.Count;
            var rotation = Quaternion.FromToRotation(Vector3.up, (top - bottom).normalized);
            for (int row = 0; row < 2; row++) for (int i = 0; i < sides; i++)
            {
                float angle = i * Mathf.PI * 2 / sides;
                var normal = rotation * new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
                vertices.Add((row == 0 ? bottom : top) + normal * radius); normals.Add(normal); uvs.Add(uv);
            }
            for (int i = 0; i < sides; i++)
            {
                int a = start + i, b = start + (i + 1) % sides;
                triangles.AddRange(new[] { a, a + sides, b, b, a + sides, b + sides });
            }
            for (int cap = 0; cap < 2; cap++)
            {
                int center = vertices.Count;
                var normal = rotation * (cap == 0 ? Vector3.down : Vector3.up);
                var position = cap == 0 ? bottom : top;
                vertices.Add(position); normals.Add(normal); uvs.Add(uv);
                for (int i = 0; i < sides; i++)
                {
                    float angle = i * Mathf.PI * 2 / sides;
                    vertices.Add(position + rotation * new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * radius);
                    normals.Add(normal); uvs.Add(uv);
                }
                for (int i = 0; i < sides; i++)
                {
                    int a = center + 1 + i, b = center + 1 + (i + 1) % sides;
                    triangles.AddRange(cap == 0 ? new[] { center, a, b } : new[] { center, b, a });
                }
            }
        }
    }
}
