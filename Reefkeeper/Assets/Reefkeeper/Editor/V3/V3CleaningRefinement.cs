using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using Object = UnityEngine.Object;

namespace Reefkeeper.Editor
{
    /// <summary>Authored V3 feedback pass: spiny fire coral, a hand file, and two clear teaching stations.</summary>
    public static class V3CleaningRefinement
    {
        const string Folder = "Assets/Reefkeeper/Generated/V3";
        const string PracticeFire = "Practice spot / fire coral";
        const string Samples = "Fire coral / tree samples";

        [MenuItem("Reefkeeper/V3/Apply spiny coral and file feedback")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path != V3CleaningSetup.ScenePath) throw new InvalidOperationException("Open ShallowReef_V3 first.");
            var w = Object.FindFirstObjectByType<ReefWorkshop>();
            var pickup = w.GetComponent<ReefBrushPickup>();
            Undo.RegisterFullObjectHierarchyUndo(w.gameObject, "Refine V3 cleaning stations");
            Directory.CreateDirectory(Folder);
            var pad = BuildSpinyCoral();
            BuildFile(w, pickup);
            ArrangeToolTable(w);
            var practiceFire = ArrangePractice(w, pickup, pad);
            var treeSamples = AttachTreeSamples(w, pad);
            w.patches = new[] { pickup.practiceSpot, practiceFire }.Concat(treeSamples).ToArray();
            EditorUtility.SetDirty(w); EditorUtility.SetDirty(pickup);
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(scene);
            Validate();
            EditorSceneManager.SaveScene(scene);
            Debug.Log("V3_FEEDBACK_READY: spiny fire coral, file, paired practice targets, widened tool table, four attached tree samples.");
        }

        public static RackDirtPatch BuildSpinyCoral()
        {
            var orange = Mat("FireCoralCoating", new Color(0.93f, 0.32f, 0.045f), 0, 0.2f);
            var amber = Mat("FireCoralAmber", new Color(1f, 0.58f, 0.09f), 0, 0.23f);
            var tips = Mat("FireCoralTips", new Color(1f, 0.88f, 0.40f), 0, 0.28f);
            var spineMeshes = Enumerable.Range(0, 3).Select(i => SaveMesh("FireCoralSpine" + i, Spine(i))).ToArray();
            var crust = SaveMesh("FireCoralAttachedCrust", Crust());
            var root = new GameObject("Fire coral on planting surface");
            var patch = root.AddComponent<RackDirtPatch>();
            patch.surfaceKind = ReefSurfaceKind.FireCoral; patch.requiredTravel = 1.8f;
            var contact = root.AddComponent<BoxCollider>();
            contact.isTrigger = true; contact.center = new Vector3(0, 0.10f, 0); contact.size = new Vector3(1.02f, 0.24f, 1.02f);
            patch.dirtVisual = MeshObject("Fire coral crust", root.transform, crust, new[] { orange }).GetComponent<Renderer>();
            var random = new System.Random(3419);
            var growth = new List<Transform>();
            for (int i = 0; i < 27; i++)
            {
                float a = i * 2.39996f, r = 0.39f * Mathf.Sqrt((i + 0.3f) / 27f);
                var spine = MeshObject("Fire coral spine " + i, root.transform, spineMeshes[i % 3], new[] { orange, amber, tips }).transform;
                spine.localPosition = new Vector3(Mathf.Cos(a) * r, 0.008f, Mathf.Sin(a) * r);
                float height = 0.10f + (float)random.NextDouble() * 0.19f;
                float width = 0.017f + (float)random.NextDouble() * 0.021f;
                spine.localScale = new Vector3(width, height, width * (0.68f + (float)random.NextDouble() * 0.3f));
                spine.localRotation = Quaternion.Euler(Mathf.Sin(a) * 19, i * 53, Mathf.Cos(a) * 19);
                growth.Add(spine);
            }
            patch.growth = growth.ToArray();
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, Folder + "/FireCoralPad.prefab");
            Object.DestroyImmediate(root);
            return prefab.GetComponent<RackDirtPatch>();
        }

        public static void BuildFile(ReefWorkshop w, ReefBrushPickup pickup)
        {
            var tool = w.scraper;
            var root = tool.transform;
            var grab = tool.GetComponent<ReefBrushInteractable>();
            grab.colliders.Clear();
            foreach (Transform child in root.Cast<Transform>().ToArray()) Object.DestroyImmediate(child.gameObject);
            var grip = Mat("ScraperGrip", new Color(0.035f, 0.055f, 0.06f), 0, 0.28f);
            var yellow = Mat("ScraperBand", new Color(0.98f, 0.72f, 0.08f), 0.05f, 0.35f);
            var steel = Mat("ScraperBlade", new Color(0.45f, 0.51f, 0.53f), 0.55f, 0.32f);
            var grooves = Mat("FileCrosscut", new Color(0.14f, 0.20f, 0.22f), 0.35f, 0.22f);
            MeshObject("Ergonomic file handle", root, SaveMesh("FileHandle", Handle()), new[] { grip });
            Primitive("Yellow thumb grip", root, PrimitiveType.Sphere, new Vector3(0, 0.023f, -0.012f), new Vector3(0.034f, 0.014f, 0.082f), yellow);
            Primitive("Yellow collar", root, PrimitiveType.Cube, new Vector3(0, 0, 0.105f), new Vector3(0.062f, 0.045f, 0.018f), yellow);
            MeshObject("Long tapered file", root, SaveMesh("FileBlade", Blade()), new[] { steel });
            MeshObject("Cross-cut teeth", root, SaveMesh("FileCrosscut", Teeth()), new[] { grooves });
            var handleBox = root.gameObject.AddComponent<BoxCollider>();
            handleBox.center = new Vector3(0, 0, -0.002f); handleBox.size = new Vector3(0.064f, 0.054f, 0.21f);
            var bladeBox = root.gameObject.AddComponent<BoxCollider>();
            bladeBox.center = new Vector3(0, 0, 0.285f); bladeBox.size = new Vector3(0.047f, 0.016f, 0.34f);
            // Re-running the pass replaces only the file's known collision proxies.
            foreach (var old in root.GetComponents<Collider>().Where(c => c != handleBox && c != bladeBox).ToArray()) Object.DestroyImmediate(old);
            grab.colliders.Add(handleBox); grab.colliders.Add(bladeBox);
            var attach = new GameObject("Grip pose").transform; attach.SetParent(root, false); grab.attachTransform = attach;
            var contact = new GameObject("File working face").transform; contact.SetParent(root, false);
            contact.localPosition = new Vector3(0, -0.009f, 0.29f);
            tool.bristles = contact; tool.contactRadius = 0.045f; tool.toolKind = CleaningToolKind.Scraper;
            grab.owner = pickup; grab.outlineMaterial = pickup.brush.outlineMaterial;
            EditorUtility.SetDirty(tool); EditorUtility.SetDirty(grab);
        }

        static void ArrangeToolTable(ReefWorkshop w)
        {
            var table = Find(w, "Tool rest");
            table.localScale = new Vector3(1.05f, 0.045f, 0.66f);
            var support = Find(w, "Tool rest support");
            support.gameObject.SetActive(false);
            var oldLegs = w.transform.Find("Tool table legs");
            if (oldLegs) Object.DestroyImmediate(oldLegs.gameObject);
            var legs = new GameObject("Tool table legs").transform; legs.SetParent(w.transform, false);
            float bottom = support.localPosition.y - support.localScale.y;
            float top = table.localPosition.y - table.localScale.y * 0.5f;
            var material = table.GetComponent<Renderer>().sharedMaterial;
            foreach (float x in new[] { -0.40f, 0.40f }) foreach (float z in new[] { -0.23f, 0.23f })
            {
                var leg = Primitive("Table leg", legs, PrimitiveType.Cube,
                    new Vector3(table.localPosition.x + x, (top + bottom) * 0.5f, table.localPosition.z + z),
                    new Vector3(0.04f, top - bottom, 0.04f), material);
                leg.AddComponent<BoxCollider>();
            }
            Vector3 surface = table.localPosition + Vector3.up * table.localScale.y * 0.5f;
            Home(w.brush, w.brushHome, w.transform, surface + new Vector3(-0.245f, 0.088f, -0.10f));
            Home(w.scraper, w.scraperHome, w.transform, surface + new Vector3(0.245f, 0.029f, -0.17f));
        }

        static RackDirtPatch ArrangePractice(ReefWorkshop w, ReefBrushPickup pickup, RackDirtPatch prefab)
        {
            var bench = Find(w, "Practice bench");
            bench.localScale = new Vector3(0.94f, 0.035f, 0.38f);
            var surface = bench.localPosition + Vector3.up * (bench.localScale.y * 0.5f + 0.008f);
            pickup.practiceSpot.transform.localPosition = surface + Vector3.left * 0.235f;
            pickup.practiceSpot.transform.localScale = new Vector3(0.49f, 0.9f, 1.08f);
            pickup.practiceSpot.requiredTravel = 1.8f;
            var old = w.transform.Cast<Transform>().FirstOrDefault(t => t.name == PracticeFire);
            if (old) Object.DestroyImmediate(old.gameObject);
            var fire = (RackDirtPatch)PrefabUtility.InstantiatePrefab(prefab, w.transform);
            fire.name = PracticeFire; fire.transform.localPosition = surface + Vector3.right * 0.235f;
            fire.transform.localRotation = Quaternion.identity; fire.transform.localScale = new Vector3(0.36f, 0.40f, 0.26f);
            return fire;
        }

        static RackDirtPatch[] AttachTreeSamples(ReefWorkshop w, RackDirtPatch prefab)
        {
            var tree = w.GetComponentsInChildren<MeshCollider>(true).First(c => c.name.StartsWith("RackBase_Tree"));
            var old = tree.transform.Cast<Transform>().FirstOrDefault(t => t.name == Samples); if (old) Object.DestroyImmediate(old.gameObject);
            var group = new GameObject(Samples).transform; group.SetParent(tree.transform, false);
            Physics.SyncTransforms();
            var candidates = new List<Vector2> { new Vector2(0, 0.30f), new Vector2(0.20f, 0.48f), new Vector2(-0.06f, 0.86f), new Vector2(-0.035f, 1.20f), new Vector2(0, 1.42f) };
            for (int i = 0; i < 15; i++) candidates.Add(new Vector2(0, 0.24f + i * 0.085f));
            var result = new List<RackDirtPatch>();
            foreach (var candidate in candidates)
            {
                var ray = new Ray(tree.transform.TransformPoint(new Vector3(candidate.x, candidate.y, -1)), tree.transform.forward);
                if (!tree.Raycast(ray, out var hit, 5) || result.Any(p => Vector3.Distance(p.transform.position, hit.point) < 0.32f)) continue;
                var pad = (RackDirtPatch)PrefabUtility.InstantiatePrefab(prefab, group);
                pad.name = "Attached fire coral sample " + (result.Count + 1);
                var along = Vector3.ProjectOnPlane(Vector3.up, hit.normal).normalized;
                if (along.sqrMagnitude < 0.01f) along = tree.transform.forward;
                pad.transform.SetPositionAndRotation(hit.point + hit.normal * 0.004f, Quaternion.LookRotation(along, hit.normal));
                var parentScale = group.lossyScale;
                float size = 0.24f + result.Count * 0.025f;
                pad.transform.localScale = new Vector3(size / parentScale.x, size / parentScale.y, size / parentScale.z);
                ConformToTree(pad, tree, result.Count + 1);
                result.Add(pad);
                if (result.Count == 4) break;
            }
            if (result.Count < 3) throw new InvalidOperationException("Could not seat enough fire coral samples on actual tree surfaces.");
            return result.ToArray();
        }

        static void ConformToTree(RackDirtPatch pad, MeshCollider tree, int index)
        {
            // Project the thin crust and each growth root onto the actual curved bark.
            // The reusable prefab remains flat for tabletop and planting-socket patches.
            var filter = pad.dirtVisual.GetComponent<MeshFilter>();
            var mesh = Object.Instantiate(filter.sharedMesh);
            var vertices = mesh.vertices;
            for (int i = 0; i < vertices.Length; i++)
            {
                var hit = SeatOnTree(tree, pad.transform, filter.transform.TransformPoint(vertices[i]));
                vertices[i] = filter.transform.InverseTransformPoint(hit.point + pad.transform.up * 0.002f);
            }
            mesh.vertices = vertices; mesh.RecalculateNormals(); mesh.RecalculateBounds();
            filter.sharedMesh = SaveMesh("TreeFireCoralCrust" + index, mesh);
            PrefabUtility.RecordPrefabInstancePropertyModifications(filter);
            foreach (var spine in pad.growth)
            {
                var hit = SeatOnTree(tree, pad.transform, spine.position);
                spine.SetPositionAndRotation(hit.point + pad.transform.up * 0.002f,
                    Quaternion.FromToRotation(pad.transform.up, hit.normal) * spine.rotation);
                PrefabUtility.RecordPrefabInstancePropertyModifications(spine);
            }
        }

        static RaycastHit SeatOnTree(MeshCollider tree, Transform pad, Vector3 point)
        {
            for (int attempt = 0; attempt < 8; attempt++)
            {
                var ray = new Ray(point + pad.up * 0.20f, -pad.up);
                if (tree.Raycast(ray, out var hit, 0.50f)) return hit;
                // At a narrow branch edge, tuck the root inward instead of leaving it suspended.
                point = Vector3.Lerp(point, pad.position, 0.25f);
            }
            throw new InvalidOperationException("Could not attach fire coral geometry to the tree.");
        }

        public static void Validate()
        {
            V3CleaningSetup.Validate();
            var w = Object.FindFirstObjectByType<ReefWorkshop>();
            var fire = w.GetComponentsInChildren<RackDirtPatch>(true).FirstOrDefault(p => p.name == PracticeFire);
            if (!fire || fire.surfaceKind != ReefSurfaceKind.FireCoral || !w.patches.Contains(fire)) throw new InvalidOperationException("Practice fire coral must be clearable and resettable.");
            var samples = w.GetComponentsInChildren<RackDirtPatch>(true).Where(p => p.name.StartsWith("Attached fire coral sample")).ToArray();
            if (samples.Length < 3 || samples.Any(p => !w.patches.Contains(p))) throw new InvalidOperationException("Tree samples are missing reset wiring.");
            foreach (var p in samples)
            {
                var collider = p.GetComponentInParent<MeshCollider>();
                var ray = new Ray(p.transform.position + p.transform.up * 0.05f, -p.transform.up);
                if (!collider || !collider.Raycast(ray, out var hit, 0.12f) || Vector3.Distance(p.transform.position, hit.point) > 0.015f)
                    throw new InvalidOperationException("A fire coral sample is floating off the tree: " + p.name);
                foreach (var spine in p.growth)
                {
                    var rootRay = new Ray(spine.position + p.transform.up * 0.02f, -p.transform.up);
                    if (!collider.Raycast(rootRay, out var rootHit, 0.05f) || Vector3.Distance(spine.position, rootHit.point) > 0.01f)
                        throw new InvalidOperationException("A fire coral spine is not seated: " + p.name + "/" + spine.name);
                }
            }
            var table = Find(w, "Tool rest").GetComponent<Renderer>().bounds;
            foreach (var tool in new[] { w.brush, w.scraper })
            foreach (var renderer in tool.GetComponentsInChildren<Renderer>())
            {
                var b = renderer.bounds;
                if (b.min.x < table.min.x || b.max.x > table.max.x || b.min.z < table.min.z || b.max.z > table.max.z)
                    throw new InvalidOperationException("Tool hangs over the table: " + renderer.name);
            }
            if (w.brushHome.position.x >= w.scraperHome.position.x) throw new InvalidOperationException("Brush must be left of the file.");
            Debug.Log("V3_FEEDBACK_VALIDATED: both tools fit the table; paired tutorial patches; " + samples.Length + " tree samples seated and resettable.");
        }

        static Transform Find(ReefWorkshop w, string name) => w.GetComponentsInChildren<Transform>(true).First(t => t.name == name);
        static void Home(ReefBrush tool, Transform home, Transform parent, Vector3 position)
        {
            home.position = parent.TransformPoint(position); home.rotation = parent.rotation;
            tool.transform.SetPositionAndRotation(home.position, home.rotation);
        }
        static Material Mat(string name, Color color, float metallic, float smoothness)
        {
            string path = Folder + "/" + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!m) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, path); }
            m.SetColor("_BaseColor", color); m.SetColor("_Color", color);
            m.SetFloat("_Metallic", metallic); m.SetFloat("_Smoothness", smoothness); EditorUtility.SetDirty(m);
            return m;
        }
        static GameObject MeshObject(string name, Transform parent, Mesh mesh, Material[] materials)
        {
            var go = new GameObject(name); go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh; go.AddComponent<MeshRenderer>().sharedMaterials = materials; return go;
        }
        static GameObject Primitive(string name, Transform parent, PrimitiveType type, Vector3 position, Vector3 scale, Material material)
        {
            var go = GameObject.CreatePrimitive(type); go.name = name; go.transform.SetParent(parent, false);
            go.transform.localPosition = position; go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = material; Object.DestroyImmediate(go.GetComponent<Collider>()); return go;
        }
        static Mesh SaveMesh(string name, Mesh mesh)
        {
            string path = Folder + "/" + name + ".asset"; mesh.name = name;
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing) { EditorUtility.CopySerialized(mesh, existing); Object.DestroyImmediate(mesh); EditorUtility.SetDirty(existing); return existing; }
            AssetDatabase.CreateAsset(mesh, path); return mesh;
        }

        static Mesh Spine(int variant)
        {
            const int sides = 7;
            float[] heights = { 0, 0.10f, 0.26f, 0.47f, 0.68f, 0.85f, 1f };
            float[] radii = { 0.72f, 1f, 0.82f, 0.61f, 0.40f, 0.19f, 0.005f };
            var vertices = new List<Vector3>(); var triangles = new[] { new List<int>(), new List<int>(), new List<int>() };
            for (int ring = 0; ring < heights.Length; ring++)
            {
                float h = heights[ring];
                for (int side = 0; side < sides; side++)
                {
                    float angle = side * Mathf.PI * 2 / sides;
                    vertices.Add(new Vector3(Mathf.Cos(angle) * radii[ring] + (variant - 1) * h * h * 0.65f,
                        h, Mathf.Sin(angle) * radii[ring] + Mathf.Sin(h * 2.8f + variant) * h * 0.22f));
                }
                if (ring == 0) continue;
                var indices = triangles[ring <= 2 ? 0 : ring <= 4 ? 1 : 2];
                for (int side = 0; side < sides; side++)
                {
                    int a = (ring - 1) * sides + side, b = (ring - 1) * sides + (side + 1) % sides;
                    int c = ring * sides + side, d = ring * sides + (side + 1) % sides;
                    indices.AddRange(new[] { a, c, b, b, c, d });
                }
            }
            return Make(vertices, triangles);
        }
        static Mesh Crust()
        {
            var vertices = new List<Vector3> { new Vector3(0, 0.012f, 0) }; var triangles = new List<int>();
            for (int i = 0; i < 32; i++)
            { float a = i * Mathf.PI * 2 / 32, r = 0.47f + Mathf.Sin(i * 3.1f) * 0.018f; vertices.Add(new Vector3(Mathf.Cos(a) * r, 0.003f, Mathf.Sin(a) * r)); }
            for (int i = 0; i < 32; i++) triangles.AddRange(new[] { 0, (i + 1) % 32 + 1, i + 1 });
            return Make(vertices, new[] { triangles });
        }
        static Mesh Handle()
        {
            var vertices = new List<Vector3>(); var triangles = new List<int>(); const int sides = 12;
            float[] z = { -0.112f, -0.102f, -0.073f, -0.015f, 0.05f, 0.09f, 0.10f };
            float[] widths = { 0.014f, 0.026f, 0.033f, 0.031f, 0.025f, 0.023f, 0.018f };
            for (int ring = 0; ring < z.Length; ring++)
            {
                for (int side = 0; side < sides; side++)
                { float a = side * Mathf.PI * 2 / sides; vertices.Add(new Vector3(Mathf.Cos(a) * widths[ring], Mathf.Sin(a) * widths[ring] * 0.78f, z[ring])); }
                if (ring == 0) continue;
                for (int side = 0; side < sides; side++)
                { int a = (ring - 1) * sides + side, b = (ring - 1) * sides + (side + 1) % sides; triangles.AddRange(new[] { a, b, a + sides, b, b + sides, a + sides }); }
            }
            for (int i = 1; i < sides - 1; i++) { triangles.AddRange(new[] { 0, i + 1, i }); int end = (z.Length - 1) * sides; triangles.AddRange(new[] { end, end + i, end + i + 1 }); }
            return Make(vertices, new[] { triangles });
        }
        static Mesh Blade()
        {
            var vertices = new List<Vector3>(); var triangles = new List<int>();
            Vector2[] outline = { new Vector2(-0.020f, 0.115f), new Vector2(-0.023f, 0.36f), new Vector2(-0.016f, 0.448f), new Vector2(0.016f, 0.448f), new Vector2(0.023f, 0.36f), new Vector2(0.020f, 0.115f) };
            foreach (float y in new[] { -0.006f, 0.006f }) foreach (var v in outline) vertices.Add(new Vector3(v.x, y, v.y));
            for (int i = 1; i < 5; i++) { triangles.AddRange(new[] { 0, i + 1, i, 6, 6 + i, 7 + i }); }
            for (int i = 0; i < 6; i++) { int n = (i + 1) % 6; triangles.AddRange(new[] { i, n, i + 6, n, n + 6, i + 6 }); }
            return Make(vertices, new[] { triangles });
        }
        static Mesh Teeth()
        {
            var vertices = new List<Vector3>(); var triangles = new List<int>();
            foreach (float y in new[] { -0.0065f, 0.0065f }) foreach (float diagonal in new[] { -1f, 1f })
            for (int i = 0; i < 23; i++)
            {
                float z = 0.139f + i * 0.0125f;
                float half = z > 0.36f ? Mathf.Lerp(0.021f, 0.014f, (z - 0.36f) / 0.088f) : 0.019f;
                int start = vertices.Count;
                vertices.Add(new Vector3(-half, y, z - half * diagonal * 0.5f));
                vertices.Add(new Vector3(half, y, z + half * diagonal * 0.5f));
                vertices.Add(new Vector3(half, y, z + half * diagonal * 0.5f + 0.0011f));
                vertices.Add(new Vector3(-half, y, z - half * diagonal * 0.5f + 0.0011f));
                triangles.AddRange(y > 0 ? new[] { start, start + 2, start + 1, start, start + 3, start + 2 } : new[] { start, start + 1, start + 2, start, start + 2, start + 3 });
            }
            return Make(vertices, new[] { triangles });
        }
        static Mesh Make(List<Vector3> vertices, List<int>[] triangles)
        {
            var mesh = new Mesh(); mesh.SetVertices(vertices); mesh.subMeshCount = triangles.Length;
            for (int i = 0; i < triangles.Length; i++) mesh.SetTriangles(triangles[i], i);
            mesh.RecalculateNormals(); mesh.RecalculateBounds(); return mesh;
        }
    }
}
