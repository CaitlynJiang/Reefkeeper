using System.IO;
using System.Linq;
using Reefkeeper.Corals;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Reefkeeper.V2
{
    /// <summary>
    /// Wires the coral gameplay loop into the open ShallowReef_V2: socket grades (Cairn = A,
    /// Tree and shipwreck = B, Hanging hooks = C, grown mushroom coral = B), a moss pad on every
    /// socket, empty racks, Lucky Cat's shop with a display cart, and the planting manager.
    /// Safe to run again; it keeps your rack layout.
    /// </summary>
    public static class CoralGameplaySetup
    {
        const string PrefabDir = "Assets/Reefkeeper/Prefabs/CoralGrowth";
        const string MossPrefabPath = PrefabDir + "/MossPad.prefab";
        const string Generated = "Assets/Reefkeeper/Generated";
        const float TableMoss = 0.5f, HookMoss = 0.3f;   // pad width in metres

        [MenuItem("Reefkeeper/V2/Set up coral gameplay - 布置珊瑚玩法")]
        public static void Run()
        {
            var scene = SceneManager.GetActiveScene();
            var workshop = Object.FindFirstObjectByType<ReefWorkshop>();
            if (!workshop) { Debug.LogError("[V2] Open ShallowReef_V2 first."); return; }
            Undo.RegisterFullObjectHierarchyUndo(workshop.gameObject, "Set up coral gameplay");

            var mossPrefab = MossPad();
            SetDiscCoralTopsToB();

            // Racks under the workshop: grade by rack type, start empty, moss on every socket.
            // Sockets on the racks themselves (not the top of a coral that is about to be cleared).
            var rackSockets = workshop.GetComponentsInChildren<RackSocket>(true).Where(x => !x.GetComponentInParent<CoralGrowth>()).ToArray();
            foreach (var socket in rackSockets)
            {
                var rack = socket.GetComponentInParent<MeshCollider>();
                string n = rack ? rack.name : "";
                socket.grade = n.Contains("Cairn") ? RackSocket.Grade.A
                    : n.Contains("Hanging") || socket.kind == RackSocket.Kind.Hang ? RackSocket.Grade.C
                    : RackSocket.Grade.B;
                Clear(socket);
                AddMoss(socket, mossPrefab);
            }
            var pickup = workshop.GetComponent<ReefCoralPickup>();
            pickup.fragments = new ReefCoralFragment[0];
            EditorUtility.SetDirty(pickup);

            // Shipwreck deck sockets are flat (B).
            foreach (var root in scene.GetRootGameObjects())
            foreach (var socket in root.GetComponentsInChildren<RackSocket>(true))
            {
                var hull = socket.GetComponentInParent<MeshCollider>();
                if (socket.transform.IsChildOf(workshop.transform) || !hull || !hull.name.StartsWith("Shipwreck")) continue;
                socket.grade = RackSocket.Grade.B;
                AddMoss(socket, mossPrefab);
            }

            var planting = workshop.GetComponent<CoralPlanting>();
            if (!planting) planting = workshop.gameObject.AddComponent<CoralPlanting>();
            planting.workshop = workshop;
            planting.corals = pickup;
            planting.brushes = workshop.GetComponent<ReefBrushPickup>();
            planting.rope = AssetDatabase.LoadAssetAtPath<Material>($"{Generated}/NurseryRope.mat");
            planting.mossPrefab = mossPrefab;
            EditorUtility.SetDirty(planting);

            Shop(scene, workshop, pickup);
            Hud(workshop, planting, Object.FindFirstObjectByType<CoralShop>());

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[V2] Coral gameplay set up.");
        }

        const string IconDir = "Assets/UI/Gameplay";

        // Coral HUD from the Figma page "V2 · Coral gameplay HUD": icons exported from Figma
        // (white, tinted in Unity) plus a generated dot for the stage pips.
        static void Hud(ReefWorkshop workshop, CoralPlanting planting, CoralShop shop)
        {
            string dotPath = $"{IconDir}/icon_dot.png";
            if (!File.Exists(dotPath))
            {
                const int n = 96;
                var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
                for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(n / 2f, n / 2f));
                    tex.SetPixel(x, y, new Color(1, 1, 1, Mathf.Clamp01(n / 2f - d)));
                }
                File.WriteAllBytes(dotPath, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                AssetDatabase.ImportAsset(dotPath);
            }
            Sprite Load(string name)
            {
                string path = $"{IconDir}/{name}.png";
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                if (importer.textureType != TextureImporterType.Sprite || importer.spriteImportMode != SpriteImportMode.Single)
                {
                    importer.textureType = TextureImporterType.Sprite;
                    importer.spriteImportMode = SpriteImportMode.Single;
                    importer.alphaIsTransparency = true;
                    importer.mipmapEnabled = false;
                    importer.SaveAndReimport();
                }
                return AssetDatabase.LoadAssetAtPath<Sprite>(path);
            }

            var cleaning = workshop.GetComponent<ReefCleaningHUD>();
            var hud = workshop.GetComponent<CoralGameplayHUD>();
            if (!hud) hud = workshop.gameObject.AddComponent<CoralGameplayHUD>();
            hud.planting = planting;
            hud.shop = shop;
            hud.font = cleaning.font;
            hud.boldFont = cleaning.boldFont;
            hud.panel = cleaning.panelSprite;
            hud.dot = Load("icon_dot");
            hud.leaf = Load("icon_leaf");
            hud.moss = Load("icon_moss");
            hud.check = Load("icon_check");
            hud.fertilize = Load("icon_fertilize");
            hud.mouseLeft = Load("icon_mouse_left");
            hud.bubbleTail = Load("bubble_tail");
            EditorUtility.SetDirty(hud);

            cleaning.planting = planting;
            cleaning.shop = shop;
            EditorUtility.SetDirty(cleaning);
        }

        static void Clear(RackSocket socket)
        {
            if (socket.occupant) Object.DestroyImmediate(socket.occupant.gameObject);
            socket.occupant = null;
            foreach (Transform c in socket.transform.Cast<Transform>().ToArray())
                if (c.name == "Coral line") Object.DestroyImmediate(c.gameObject);
            EditorUtility.SetDirty(socket);
        }

        static void AddMoss(RackSocket socket, RackDirtPatch prefab)
        {
            if (!socket.moss)
            {
                socket.moss = (RackDirtPatch)PrefabUtility.InstantiatePrefab(prefab, socket.transform);
                socket.moss.name = "Moss";
            }
            var t = socket.moss.transform;
            t.localPosition = Vector3.zero;
            t.rotation = Quaternion.identity;
            float width = socket.kind == RackSocket.Kind.Hang ? HookMoss : TableMoss;
            var s = socket.transform.lossyScale;
            t.localScale = new Vector3(width / s.x, width / s.y, width / s.z);
            EditorUtility.SetDirty(socket);
        }

        // A 1 m moss pad (scaled per socket): algae crust plus a few fronds, cleaned by the brush.
        static RackDirtPatch MossPad()
        {
            var existing = AssetDatabase.LoadAssetAtPath<RackDirtPatch>(MossPrefabPath);
            if (existing) return existing;
            var root = new GameObject("MossPad");
            var box = root.AddComponent<BoxCollider>();
            box.isTrigger = true; box.center = new Vector3(0, 0.08f, 0); box.size = new Vector3(1.05f, 0.3f, 1.05f);
            var patch = root.AddComponent<RackDirtPatch>();
            patch.requiredTravel = 1.2f;

            var coat = new GameObject("Moss coating");
            coat.transform.SetParent(root.transform, false);
            coat.transform.localScale = new Vector3(0.95f, 0.18f, 0.95f);
            coat.AddComponent<MeshFilter>().sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>($"{Generated}/TexturedAlgaeCrust.asset");
            coat.AddComponent<MeshRenderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>($"{Generated}/AttachedAlgae.mat");
            patch.dirtVisual = coat.GetComponent<MeshRenderer>();

            var tuftMesh = AssetDatabase.LoadAssetAtPath<Mesh>($"{Generated}/AlgaeTuft.asset");
            var tuftMat = AssetDatabase.LoadAssetAtPath<Material>($"{Generated}/AlgaeFronds.mat");
            var rng = new System.Random(11);
            var tufts = new Transform[9];
            for (int i = 0; i < tufts.Length; i++)
            {
                var tuft = new GameObject($"Moss tuft {i}").transform;
                tuft.SetParent(root.transform, false);
                float a = (float)rng.NextDouble() * Mathf.PI * 2f, r = 0.36f * Mathf.Sqrt((float)rng.NextDouble());
                tuft.localPosition = new Vector3(Mathf.Cos(a) * r, 0.07f, Mathf.Sin(a) * r);
                tuft.localRotation = Quaternion.Euler(0, (float)rng.NextDouble() * 360f, 0);
                tuft.localScale = Vector3.one * Mathf.Lerp(2.4f, 3.6f, (float)rng.NextDouble());
                tuft.gameObject.AddComponent<MeshFilter>().sharedMesh = tuftMesh;
                tuft.gameObject.AddComponent<MeshRenderer>().sharedMaterial = tuftMat;
                tufts[i] = tuft;
            }
            patch.growth = tufts;
            Directory.CreateDirectory(PrefabDir);
            var saved = PrefabUtility.SaveAsPrefabAsset(root, MossPrefabPath).GetComponent<RackDirtPatch>();
            Object.DestroyImmediate(root);
            return saved;
        }

        static void SetDiscCoralTopsToB()
        {
            string path = $"{PrefabDir}/DiscCoral.prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            foreach (var s in root.GetComponentsInChildren<RackSocket>(true)) s.grade = RackSocket.Grade.B;
            PrefabUtility.SaveAsPrefabAsset(root, path);
            PrefabUtility.UnloadPrefabContents(root);
        }

        static void Shop(Scene scene, ReefWorkshop workshop, ReefCoralPickup pickup)
        {
            var stall = scene.GetRootGameObjects().FirstOrDefault(r => r.name == "CatStall");
            if (!stall) { Debug.LogWarning("[V2] No CatStall in the scene; shop skipped."); return; }
            var cat = stall.GetComponentInChildren<LuckyCat>();
            var shop = stall.GetComponent<CoralShop>();
            if (!shop) shop = stall.AddComponent<CoralShop>();
            shop.cat = cat;
            shop.workshop = workshop;
            shop.corals = pickup;
            shop.outline = AssetDatabase.LoadAssetAtPath<Material>($"{Generated}/CoralPickupOutline.mat");
            shop.stock = RackCoralTools.CoralNames
                .Select(n => AssetDatabase.LoadAssetAtPath<CoralGrowth>($"{PrefabDir}/{n}.prefab")).Where(c => c).ToArray();

            // Aim / touch target around the cat's head and body.
            var bounds = new Bounds(cat.transform.position, Vector3.zero);
            foreach (var r in cat.GetComponentsInChildren<Renderer>())
                if (r.name.Contains("Head") || r.name.Contains("Body")) bounds.Encapsulate(r.bounds);
            var col = cat.GetComponent<SphereCollider>();
            if (!col) col = cat.gameObject.AddComponent<SphereCollider>();
            col.isTrigger = true;
            col.center = cat.transform.InverseTransformPoint(bounds.center);
            col.radius = bounds.extents.magnitude * 0.6f / Mathf.Max(0.001f, cat.transform.lossyScale.x);
            shop.catCollider = col;
            EditorUtility.SetDirty(shop);

            // Display corals in the cart: one of each kind on offer, smallest stage.
            var cart = stall.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name.StartsWith("Cart"));
            if (!cart) return;
            var old = cart.Cast<Transform>().FirstOrDefault(t => t.name.StartsWith("Cart corals"));
            if (old) Object.DestroyImmediate(old.gameObject);
            var group = new GameObject("Cart corals (display)").transform;
            group.SetParent(cart, false);
            group.position = cart.position;
            group.rotation = Quaternion.Euler(0, stall.transform.eulerAngles.y, 0);
            group.localScale = new Vector3(1 / cart.lossyScale.x, 1 / cart.lossyScale.y, 1 / cart.lossyScale.z);
            for (int i = 0; i < shop.stock.Length; i++)
            {
                var coral = (CoralGrowth)PrefabUtility.InstantiatePrefab(shop.stock[i], group);
                coral.SetStage(0, false);
                int row = i / 5, col5 = i % 5;
                var local = new Vector3((col5 - 2) * 0.2f, 0, (row - 0.5f) * 0.22f);
                coral.transform.position = group.position + group.rotation * local;
                coral.transform.rotation = group.rotation * Quaternion.Euler(0, i * 47f, 0);
                coral.transform.localScale = Vector3.one;
            }
        }
    }
}
