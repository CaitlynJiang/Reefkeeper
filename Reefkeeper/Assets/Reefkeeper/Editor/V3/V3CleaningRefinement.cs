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
    /// <summary>Authored V3 feedback pass: branching fire coral, a hand file, and two clear teaching stations.</summary>
    public static class V3CleaningRefinement
    {
        const string Folder = "Assets/Reefkeeper/Generated/V3";
        const string PracticeFire = "Practice spot / fire coral";
        const string Samples = "Fire coral / tree samples";

        [MenuItem("Reefkeeper/V3/Apply coral and file feedback")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path != V3CleaningSetup.ScenePath) throw new InvalidOperationException("Open ShallowReef_V3 first.");
            var w = Object.FindFirstObjectByType<ReefWorkshop>();
            var pickup = w.GetComponent<ReefBrushPickup>();
            Undo.RegisterFullObjectHierarchyUndo(w.gameObject, "Refine V3 cleaning stations");
            Directory.CreateDirectory(Folder);
            var pad = BuildBranchingCoral();
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
            Debug.Log("V3_FEEDBACK_READY: branching fire coral, file, paired practice targets, widened tool table, attached tree samples.");
        }

        [MenuItem("Reefkeeper/V3/Update fire coral shape and tree targets")]
        public static void UpdateFireCoral()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path != V3CleaningSetup.ScenePath) throw new InvalidOperationException("Open ShallowReef_V3 first.");
            var w = Object.FindFirstObjectByType<ReefWorkshop>();
            Undo.RegisterFullObjectHierarchyUndo(w.gameObject, "Reshape fire coral and tree targets");
            var prefab = BuildBranchingCoral();
            var practice = w.patches.First(p => p.name == PracticeFire);
            var samples = AttachTreeSamples(w, prefab);
            w.patches = new[] { w.GetComponent<ReefBrushPickup>().practiceSpot, practice }.Concat(samples).ToArray();
            EditorUtility.SetDirty(w);
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(scene);
            Validate();
            EditorSceneManager.SaveScene(scene);
            Debug.Log("V3_CORAL_UPDATED: pale curved branches, narrow irregular footprint, larger cleanable targets on planting trees.");
        }

        public static RackDirtPatch BuildBranchingCoral()
        {
            var orange = Mat("FireCoralCoating", new Color(1f, 0.77f, 0.40f), 0, 0.24f);
            var amber = Mat("FireCoralAmber", Color.white, 0, 0.25f);
            amber.SetTexture("_BaseMap", BranchGradient());
            var branchMeshes = Enumerable.Range(0, 3).Select(i => SaveMesh("FireCoralSpine" + i, Branches(i))).ToArray();
            var crust = SaveMesh("FireCoralAttachedCrust", Crust());
            var root = new GameObject("Fire coral on planting surface");
            var patch = root.AddComponent<RackDirtPatch>();
            patch.surfaceKind = ReefSurfaceKind.FireCoral; patch.requiredTravel = 1.8f;
            var contact = root.AddComponent<BoxCollider>();
            contact.isTrigger = true; contact.center = new Vector3(0, 0.16f, 0); contact.size = new Vector3(1.04f, 0.36f, 0.24f);
            patch.dirtVisual = MeshObject("Fire coral crust", root.transform, crust, new[] { orange }).GetComponent<Renderer>();
            var random = new System.Random(3419);
            var growth = new List<Transform>();
            for (int i = 0; i < 9; i++)
            {
                float x = -0.37f + i * 0.092f;
                var branch = MeshObject("Fire coral branch " + i, root.transform, branchMeshes[i % 3], new[] { amber }).transform;
                branch.localPosition = new Vector3(x, 0.006f, Mathf.Sin(i * 1.7f) * 0.022f);
                float height = 0.18f + (float)random.NextDouble() * 0.16f;
                branch.localScale = new Vector3(height * 0.85f, height, height * 0.70f);
                branch.localRotation = Quaternion.Euler(Mathf.Sin(i * 2.1f) * 7, Mathf.Sin(i * 1.9f) * 13, Mathf.Cos(i * 1.5f) * 9);
                growth.Add(branch);
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
            // Select the actual nursery tree with planting sockets, not background tree scenery.
            var tree = w.GetComponentsInChildren<MeshCollider>(true).First(c => c.name.StartsWith("RackBase_Tree")
                && c.GetComponentsInChildren<Reefkeeper.Corals.RackSocket>(true).Length > 0);
            var old = tree.transform.Cast<Transform>().FirstOrDefault(t => t.name == Samples); if (old) Object.DestroyImmediate(old.gameObject);
            var group = new GameObject(Samples).transform; group.SetParent(tree.transform, false);
            Physics.SyncTransforms();
            var candidates = new List<Vector2> { new Vector2(0, 0.62f), new Vector2(0.20f, 0.48f), new Vector2(-0.06f, 0.95f), new Vector2(-0.035f, 1.31f), new Vector2(0, 1.46f) };
            for (int i = 0; i < 12; i++)
                foreach (float x in new[] { 0f, -0.10f, 0.10f, -0.20f, 0.20f })
                    candidates.Add(new Vector2(x, 0.46f + i * 0.085f));
            var result = new List<RackDirtPatch>();
            foreach (var candidate in candidates)
            {
                var ray = new Ray(tree.transform.TransformPoint(new Vector3(candidate.x, candidate.y, -1)), tree.transform.forward);
                if (!tree.Raycast(ray, out var hit, 5) || result.Any(p => Vector3.Distance(p.transform.position, hit.point) < 0.43f)) continue;
                var pad = (RackDirtPatch)PrefabUtility.InstantiatePrefab(prefab, group);
                pad.name = "Attached fire coral sample " + (result.Count + 1);
                var along = Vector3.ProjectOnPlane(Vector3.up, hit.normal).normalized;
                if (along.sqrMagnitude < 0.01f) along = tree.transform.forward;
                // The narrow colony runs lengthwise up the bark instead of spreading into a disc.
                pad.transform.SetPositionAndRotation(hit.point + hit.normal * 0.004f, Quaternion.LookRotation(Vector3.Cross(along, hit.normal), hit.normal));
                var parentScale = group.lossyScale;
                float size = 0.50f + result.Count * 0.035f;
                pad.transform.localScale = new Vector3(size / parentScale.x, size / parentScale.y, size / parentScale.z);
                ConformToTree(pad, tree, result.Count + 1);
                result.Add(pad);
                if (result.Count == 4) break;
            }
            if (result.Count < 4) throw new InvalidOperationException("Could not seat four fire coral samples on actual tree surfaces.");
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
                var localBend = spine.localRotation;
                // On vertical bark, branches reach upward and outward so their forks remain
                // readable from the player's approach, rather than pointing straight at the camera.
                var growthDirection = (Vector3.up * 0.80f + hit.normal * 0.45f).normalized;
                var fanNormal = Vector3.ProjectOnPlane(hit.normal, growthDirection).normalized;
                if (fanNormal.sqrMagnitude < 0.01f) fanNormal = pad.transform.forward;
                spine.SetPositionAndRotation(hit.point + pad.transform.up * 0.002f,
                    Quaternion.LookRotation(fanNormal, growthDirection) * localBend);
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
            if (samples.Length < 4 || samples.Any(p => !w.patches.Contains(p))) throw new InvalidOperationException("Tree samples are missing reset wiring.");
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
            if (existing)
            {
                // Explicit mesh setters invalidate the live GPU buffers as well as serialized data.
                // CopySerialized alone can leave the previous shape visible until an asset reload.
                existing.Clear(); existing.indexFormat = mesh.indexFormat;
                existing.vertices = mesh.vertices; existing.normals = mesh.normals; existing.uv = mesh.uv;
                existing.subMeshCount = mesh.subMeshCount;
                for (int i = 0; i < mesh.subMeshCount; i++) existing.SetIndices(mesh.GetIndices(i), mesh.GetTopology(i), i);
                existing.bounds = mesh.bounds; existing.UploadMeshData(false);
                Object.DestroyImmediate(mesh); EditorUtility.SetDirty(existing); return existing;
            }
            AssetDatabase.CreateAsset(mesh, path); return mesh;
        }

        static Texture2D BranchGradient()
        {
            string path = Folder + "/FireCoralBranchGradient.asset";
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            bool create = !texture;
            if (create) texture = new Texture2D(4, 64, TextureFormat.RGBA32, false) { name = "Fire coral cream-gold gradient" };
            texture.wrapMode = TextureWrapMode.Clamp; texture.filterMode = FilterMode.Bilinear;
            var colors = new Color[4 * 64];
            for (int y = 0; y < 64; y++)
            {
                float t = y / 63f;
                var color = Color.Lerp(new Color(1f, 0.76f, 0.38f), new Color(1f, 0.95f, 0.74f), Mathf.SmoothStep(0, 1, t));
                for (int x = 0; x < 4; x++) colors[y * 4 + x] = color;
            }
            texture.SetPixels(colors); texture.Apply();
            if (create) AssetDatabase.CreateAsset(texture, path); else EditorUtility.SetDirty(texture);
            return texture;
        }

        static Mesh Branches(int variant)
        {
            var vertices = new List<Vector3>(); var uv = new List<Vector2>(); var triangles = new List<int>();
            float bend = variant == 1 ? -1 : 1;
            var a = Vector3.zero;
            var b = new Vector3(-0.24f * bend, 0.30f, 0.015f);
            var c = new Vector3(0.24f * bend, 0.72f, -0.02f);
            var d = new Vector3(0.07f * bend, 1f, 0.01f);
            AddBranch(vertices, uv, triangles, a, b, c, d, 0.065f, 0);
            var fork = Bezier(a, b, c, d, 0.38f);
            AddBranch(vertices, uv, triangles, fork, fork + new Vector3(0.28f * bend, 0.09f, 0),
                new Vector3(0.43f * bend, 0.68f, 0.015f), new Vector3(0.31f * bend, 0.84f, 0), 0.044f, 0.38f);
            if (variant != 2)
            {
                fork = Bezier(a, b, c, d, 0.64f);
                AddBranch(vertices, uv, triangles, fork, fork + new Vector3(-0.23f * bend, 0.05f, 0),
                    new Vector3(-0.31f * bend, 0.90f, -0.01f), new Vector3(-0.23f * bend, 1.04f, 0), 0.036f, 0.64f);
            }
            var mesh = Make(vertices, new[] { triangles }); mesh.SetUVs(0, uv); return mesh;
        }

        static Vector3 Bezier(Vector3 a, Vector3 b, Vector3 c, Vector3 d, float t)
        {
            float s = 1 - t;
            return s * s * s * a + 3 * s * s * t * b + 3 * s * t * t * c + t * t * t * d;
        }

        static void AddBranch(List<Vector3> vertices, List<Vector2> uv, List<int> triangles,
            Vector3 a, Vector3 b, Vector3 c, Vector3 d, float radius, float colorStart)
        {
            const int sides = 8, rings = 15;
            int start = vertices.Count;
            for (int ring = 0; ring < rings; ring++)
            {
                float t = ring / (float)(rings - 1), s = 1 - t;
                var center = Bezier(a, b, c, d, t);
                var tangent = (3 * s * s * (b - a) + 6 * s * t * (c - b) + 3 * t * t * (d - c)).normalized;
                var across = Vector3.Cross(tangent, Vector3.forward).normalized;
                var depth = Vector3.Cross(across, tangent).normalized;
                float cap = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(0.84f, 1f, t));
                float width = radius * Mathf.Lerp(1, 0.48f, t) * Mathf.Sqrt(1 - cap * cap);
                for (int side = 0; side < sides; side++)
                {
                    float angle = side * Mathf.PI * 2 / sides;
                    vertices.Add(center + across * (Mathf.Cos(angle) * width) + depth * (Mathf.Sin(angle) * width * 0.65f));
                    uv.Add(new Vector2(side / (float)sides, Mathf.Lerp(colorStart, 1, t)));
                    if (ring == 0) continue;
                    int previous = start + (ring - 1) * sides + side;
                    int next = start + (ring - 1) * sides + (side + 1) % sides;
                    triangles.AddRange(new[] { previous, previous + sides, next, next, previous + sides, next + sides });
                }
            }
        }
        static Mesh Crust()
        {
            var vertices = new List<Vector3>(); var triangles = new List<int>();
            const int rows = 21, columns = 5;
            for (int row = 0; row < rows; row++)
            {
                float t = row / (float)(rows - 1), x = Mathf.Lerp(-0.47f, 0.47f, t);
                float width = (0.032f + 0.025f * Mathf.Sin(t * Mathf.PI)) * (1 + Mathf.Sin(row * 2.3f) * 0.21f);
                float middle = Mathf.Sin(t * 8) * 0.019f;
                for (int column = 0; column < columns; column++)
                {
                    float across = column / (float)(columns - 1);
                    vertices.Add(new Vector3(x, 0.003f + Mathf.Sin(across * Mathf.PI) * 0.004f,
                        middle + Mathf.Lerp(-width, width, across)));
                    if (row == 0 || column == 0) continue;
                    int index = row * columns + column;
                    triangles.AddRange(new[] { index - columns - 1, index, index - 1, index - columns - 1, index - columns, index });
                }
            }
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
