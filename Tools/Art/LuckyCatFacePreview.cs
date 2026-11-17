using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Run through Unity Pipeline: script run --file ../Tools/Art/LuckyCatFacePreview.cs
// Native mesh prototype. The original prefab, FBX, atlas and animation clips stay intact.
public static class LuckyCatFacePreview
{
    const string Source = "Assets/Reefkeeper/Prefabs/CatShop/LuckyCat.prefab";
    const string Out = "Assets/Reefkeeper/Art/CharacterVariants/LuckyCatCute";
    const string Review = "../artifacts/lucky-cat-face";
    static readonly string[] Palette = { "493A36", "B8C785", "8C9E64", "FFF7DE", "F2AEA0" };

    public static string Main()
    {
        if (EditorApplication.isPlaying) throw new Exception("Stop Play mode before building the face variant.");
        Directory.CreateDirectory(Out);
        Directory.CreateDirectory(Review);
        AssetDatabase.Refresh();
        var original = AssetDatabase.LoadAssetAtPath<GameObject>(Source);
        var cat = (GameObject)PrefabUtility.InstantiatePrefab(original);
        try
        {
            cat.name = "LuckyCat_CuteFace";
            var material = CreatePalette();
            foreach (string name in new[] { "Cat_Eye_L", "Cat_Eye_R" })
            {
                var eye = Find(cat, name);
                var builder = new Geometry();
                // Slightly domed, round eyes. Colour pieces share one atlas/material.
                builder.Oval(0, 0, .105f, .103f, .008f, 0);
                builder.Oval(0, -.002f, .095f, .092f, .011f, 1);
                builder.Oval(0, .009f, .062f, .079f, .014f, 0);
                builder.Oval(-.025f, .045f, .023f, .026f, .017f, 3, 20);
                builder.Oval(.028f, -.032f, .010f, .012f, .018f, 3, 16);
                var mesh = SaveMesh(builder.Mesh(name + "_Cute"), name + "_Cute.asset");
                eye.GetComponent<MeshFilter>().sharedMesh = mesh;
                eye.GetComponent<MeshRenderer>().sharedMaterials = new[] { material };
                PrefabUtility.RecordPrefabInstancePropertyModifications(eye.GetComponent<MeshFilter>());
                PrefabUtility.RecordPrefabInstancePropertyModifications(eye.GetComponent<MeshRenderer>());
            }
            var nose = Find(cat, "Cat_Nose");
            nose.localScale = new Vector3(.78f, .86f, .78f);
            PrefabUtility.RecordPrefabInstancePropertyModifications(nose);

            // A small, round W smile on the existing white muzzle, above the lucky coin.
            var mouth = new Geometry();
            var points = new List<Vector3>();
            for (int side = -1; side <= 1; side += 2)
            {
                points.Clear();
                for (int i = 0; i <= 16; i++)
                {
                    float t = i / 16f;
                    points.Add(new Vector3(side * .065f * t, .305f - .022f * Mathf.Sin(t * Mathf.PI), .543f));
                }
                mouth.Tube(points, .0045f, 0);
            }
            mouth.Tube(new List<Vector3> { new Vector3(0, .338f, .547f), new Vector3(0, .306f, .543f) }, .004f, 0);
            var smile = new GameObject("CuteFace_Smile");
            smile.transform.SetParent(Find(cat, "Head"), false);
            smile.AddComponent<MeshFilter>().sharedMesh = SaveMesh(mouth.Mesh("CuteFace_Smile"), "CuteFace_Smile.asset");
            var renderer = smile.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            PrefabUtility.SaveAsPrefabAsset(cat, Out + "/LuckyCat_CuteFace.prefab");
        }
        finally { UnityEngine.Object.DestroyImmediate(cat); }
        AssetDatabase.SaveAssets();
        string report = CaptureAndValidate(original, AssetDatabase.LoadAssetAtPath<GameObject>(Out + "/LuckyCat_CuteFace.prefab"));
        File.WriteAllText(Review + "/validation.txt", report);
        File.WriteAllText(Out + "/README.txt", "LUCKY CAT FACE PROTOTYPE\nOriginal prefab and FBX preserved. This is a prefab variant for review, not yet placed in the game.\nRound sage eyes, larger pupils, cream highlights, smaller rose nose and a subtle W smile.\nUses the original eye bones and seven mood clips. Mouth stays a gentle smile; no new expression rig.\nGeometry is native Unity mesh data, not generated character imagery. One shared 40x8 palette and original underwater toon shader.\nRebuild: Unity Pipeline script run --file ../Tools/Art/LuckyCatFacePreview.cs\nReview renders: artifacts/lucky-cat-face (outside Assets).\n");
        AssetDatabase.Refresh();
        return report;
    }

    static Transform Find(GameObject root, string name) => root.GetComponentsInChildren<Transform>(true).First(t => t.name == name);
    static Material CreatePalette()
    {
        var tex = new Texture2D(40, 8, TextureFormat.RGBA32, false);
        for (int x = 0; x < 40; x++)
        {
            ColorUtility.TryParseHtmlString("#" + Palette[x / 8], out var color);
            for (int y = 0; y < 8; y++) tex.SetPixel(x, y, color);
        }
        tex.Apply();
        string path = Out + "/FacePalette.png";
        File.WriteAllBytes(path, tex.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.mipmapEnabled = false;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.filterMode = FilterMode.Point;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.SaveAndReimport();
        var mat = AssetDatabase.LoadAssetAtPath<Material>(Out + "/CuteFace.mat");
        if (!mat)
        {
            mat = new Material(AssetDatabase.LoadAssetAtPath<Material>("Assets/Reefkeeper/Materials/CatShop/LuckyCat_Toon.mat"));
            mat.name = "CuteFace";
            AssetDatabase.CreateAsset(mat, Out + "/CuteFace.mat");
        }
        mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(path));
        EditorUtility.SetDirty(mat);
        return mat;
    }
    static Mesh SaveMesh(Mesh value, string filename)
    {
        string path = Out + "/" + filename;
        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing) { EditorUtility.CopySerialized(value, existing); UnityEngine.Object.DestroyImmediate(value); return existing; }
        AssetDatabase.CreateAsset(value, path);
        return value;
    }
    sealed class Geometry
    {
        readonly List<Vector3> vertices = new List<Vector3>();
        readonly List<Vector2> uv = new List<Vector2>();
        readonly List<int> triangles = new List<int>();
        int Vertex(Vector3 p, int color) { vertices.Add(p); uv.Add(new Vector2((color + .5f) / Palette.Length, .5f)); return vertices.Count - 1; }
        public void Oval(float x, float y, float rx, float ry, float z, int color, int n = 32)
        {
            int c = Vertex(new Vector3(x, y, z + .004f), color);
            for (int i = 0; i < n; i++) { float a = i * Mathf.PI * 2 / n; Vertex(new Vector3(x + rx * Mathf.Cos(a), y + ry * Mathf.Sin(a), z), color); }
            for (int i = 0; i < n; i++) triangles.AddRange(new[] { c, c + i + 1, c + (i + 1) % n + 1 });
        }
        public void Tube(List<Vector3> points, float radius, int color)
        {
            int start = vertices.Count;
            for (int i = 0; i < points.Count; i++)
            {
                var tangent = (points[Mathf.Min(i + 1, points.Count - 1)] - points[Mathf.Max(0, i - 1)]).normalized;
                var a = Vector3.Cross(tangent, Vector3.forward).normalized;
                for (int j = 0; j < 6; j++) { float angle = j * Mathf.PI / 3; Vertex(points[i] + radius * (a * Mathf.Cos(angle) + Vector3.forward * Mathf.Sin(angle)), color); }
            }
            for (int i = 0; i < points.Count - 1; i++) for (int j = 0; j < 6; j++)
            {
                int a = start + i * 6 + j, b = start + i * 6 + (j + 1) % 6;
                triangles.AddRange(new[] { a, b, a + 6, b, b + 6, a + 6 });
            }
        }
        public Mesh Mesh(string name)
        {
            var mesh = new Mesh { name = name };
            mesh.SetVertices(vertices); mesh.SetUVs(0, uv); mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            return mesh;
        }
    }

    static string CaptureAndValidate(GameObject before, GameObject after)
    {
        var globals = new Dictionary<string, float>();
        foreach (string key in new[] { "_UW_FogDensity", "_UW_DepthLightFalloff", "_UW_AmbientStrength" }) globals[key] = Shader.GetGlobalFloat(key);
        var absorption = Shader.GetGlobalVector("_UW_Absorption");
        var caustics = Shader.GetGlobalVector("_UW_CausticsColor");
        var cameraObject = new GameObject("Temporary cat portrait camera") { hideFlags = HideFlags.HideAndDontSave };
        var camera = cameraObject.AddComponent<Camera>();
        camera.enabled = false; camera.cullingMask = 1 << 31; camera.orthographic = true;
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.15f, .32f, .35f);
        camera.nearClipPlane = .01f; camera.farClipPlane = 50;
        var data = camera.GetUniversalAdditionalCameraData(); data.renderShadows = false; data.renderPostProcessing = false;
        var rt = new RenderTexture(800, 800, 24, RenderTextureFormat.ARGB32); rt.antiAliasing = 4;
        var oldRT = RenderTexture.active;
        var report = new System.Text.StringBuilder();
        try
        {
            Shader.SetGlobalFloat("_UW_FogDensity", 0); Shader.SetGlobalFloat("_UW_DepthLightFalloff", 0); Shader.SetGlobalFloat("_UW_AmbientStrength", .75f);
            Shader.SetGlobalVector("_UW_Absorption", Vector4.zero); Shader.SetGlobalVector("_UW_CausticsColor", Vector4.zero);
            foreach (var prefab in new[] { before, after })
            {
                var cat = UnityEngine.Object.Instantiate(prefab);
                try
                {
                    cat.transform.position = new Vector3(0, 1000, 0);
                    foreach (var t in cat.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = 31;
                    foreach (var b in cat.GetComponentsInChildren<Behaviour>()) b.enabled = false;
                    var animator = cat.GetComponent<Animator>();
                    var clips = animator.runtimeAnimatorController.animationClips;
                    var idle = clips.First(c => c.name.EndsWith("Idle"));
                    idle.SampleAnimation(cat, 0);
                    string label = prefab == before ? "before" : "after";
                    foreach (var r in cat.GetComponentsInChildren<Renderer>()) if (r.name == "Cat_Helmet" || r.name == "Cat_HelmetRing") r.enabled = false;
                    var head = Find(cat, "Cat_Head").GetComponent<Renderer>();
                    var target = head.bounds.center;
                    camera.orthographicSize = .78f;
                    foreach (string angle in new[] { "front", "three-quarter" })
                    {
                        var direction = angle == "front" ? new Vector3(.03f, .02f, 1) : new Vector3(.60f, .08f, 1);
                        camera.transform.position = target + direction.normalized * 4;
                        camera.transform.LookAt(target);
                        Capture(camera, rt, Review + "/" + label + "-" + angle + ".png");
                    }
                    foreach (var r in cat.GetComponentsInChildren<Renderer>()) if (r.name == "Cat_Helmet" || r.name == "Cat_HelmetRing") r.enabled = true;
                    camera.orthographicSize = 1.15f;
                    target = head.bounds.center + Vector3.down * .38f;
                    camera.transform.position = target + new Vector3(.15f, .05f, 1).normalized * 4;
                    camera.transform.LookAt(target);
                    Capture(camera, rt, Review + "/" + label + "-helmet.png");
                    int tris = cat.GetComponentsInChildren<MeshFilter>().Sum(f => f.sharedMesh.triangles.Length / 3);
                    report.AppendLine(label + ": " + tris + " triangles; " + cat.GetComponentsInChildren<Renderer>().Length + " renderers; same controller=" + (animator.runtimeAnimatorController == before.GetComponent<Animator>().runtimeAnimatorController));
                    foreach (string mood in new[] { "Idle", "Happy", "Beckon", "Surprised", "Sleepy", "Angry", "Sad" })
                    {
                        var clip = clips.First(c => c.name.EndsWith(mood));
                        foreach (float fraction in new[] { 0f, .25f, .5f, .75f, 1f })
                        {
                            clip.SampleAnimation(cat, clip.length * fraction);
                            foreach (var t in cat.GetComponentsInChildren<Transform>()) if (float.IsNaN(t.position.x)) throw new Exception("Invalid pose: " + mood);
                        }
                        report.AppendLine(label + " " + mood + ": sampled 5 poses");
                        if (label == "after" && (mood == "Happy" || mood == "Surprised" || mood == "Sleepy"))
                        {
                            clip.SampleAnimation(cat, clip.length * .25f);
                            Capture(camera, rt, Review + "/after-" + mood.ToLowerInvariant() + ".png");
                        }
                    }
                }
                finally { UnityEngine.Object.DestroyImmediate(cat); }
            }
        }
        finally
        {
            foreach (var item in globals) Shader.SetGlobalFloat(item.Key, item.Value);
            Shader.SetGlobalVector("_UW_Absorption", absorption); Shader.SetGlobalVector("_UW_CausticsColor", caustics);
            RenderTexture.active = oldRT; camera.targetTexture = null; rt.Release();
            UnityEngine.Object.DestroyImmediate(rt); UnityEngine.Object.DestroyImmediate(cameraObject);
        }
        return report.ToString();
    }
    static void Capture(Camera camera, RenderTexture rt, string path)
    {
        camera.targetTexture = rt; camera.Render(); RenderTexture.active = rt;
        var image = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
        try { image.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0); image.Apply(); File.WriteAllBytes(path, image.EncodeToPNG()); }
        finally { UnityEngine.Object.DestroyImmediate(image); }
    }
}
