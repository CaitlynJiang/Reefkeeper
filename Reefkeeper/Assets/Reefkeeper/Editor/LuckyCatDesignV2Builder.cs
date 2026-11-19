using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Rebuild from menu: Reefkeeper > Art > Build Lucky Cat Design V2
// Native mesh prototype. The original prefab, FBX, atlas and animation clips stay intact.
public static class LuckyCatDesignV2Builder
{
    const string Source = "Assets/Reefkeeper/Prefabs/CatShop/LuckyCat.prefab";
    const string Out = "Assets/Reefkeeper/Art/CharacterVariants/LuckyCatDesignV2";
    const string Review = "../artifacts/lucky-cat-design-v2";
    static readonly string[] Palette = { "493A36", "B8C785", "8C9E64", "FFF7DE", "F2AEA0" };

    public static string Main()
    {
        if (EditorApplication.isPlaying) throw new Exception("Stop Play mode before building the face variant.");
        Directory.CreateDirectory(Out);
        Directory.CreateDirectory(Review);
        AssetDatabase.Refresh();
        var original = AssetDatabase.LoadAssetAtPath<GameObject>(Source);
        if(!original)throw new Exception("Missing source cat prefab: "+Source);
        var cat = (GameObject)PrefabUtility.InstantiatePrefab(original);
        try
        {
            cat.name = "LuckyCat_DesignV2";
            var material = CreatePalette();
            var head = Find(cat, "Head");
            var frames = new Texture2D[] { null, LoadEye("Eyes_Happy"), LoadEye("Eyes_Blink"), LoadEye("Eyes_Sad") };
            var eyes = new List<Renderer>();
            foreach(string name in new[]{"Cat_Eye_L","Cat_Eye_R"})
            {
                var eye=Find(cat,name);
                // The character faces +Z. The viewer sees its positive-X eye on the left.
                var side = name == "Cat_Eye_R" ? "ScreenLeft" : "ScreenRight";
                var eyeMat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                eyeMat.SetTexture("_BaseMap", LoadEye("Eye_" + side));
                eyeMat.SetFloat("_AlphaClip", 1);
                eyeMat.SetFloat("_Cutoff", .5f);
                eyeMat.SetFloat("_Cull", 0);
                eyeMat.EnableKeyword("_ALPHATEST_ON");
                eyeMat.renderQueue = 2450;
                eyeMat = SaveMaterial(eyeMat, "PaintedEyes_" + side + ".mat");
                var mesh=EyeSurface();
                eye.GetComponent<MeshFilter>().sharedMesh=SaveMesh(mesh,name+"_Painted.asset");
                var r=eye.GetComponent<MeshRenderer>();r.sharedMaterials=new[]{eyeMat};
                r.shadowCastingMode=ShadowCastingMode.Off; eyes.Add(r);
                PrefabUtility.RecordPrefabInstancePropertyModifications(eye.GetComponent<MeshFilter>());
                PrefabUtility.RecordPrefabInstancePropertyModifications(r);
            }
            var driver=cat.AddComponent<Reefkeeper.Corals.LuckyCatEyeExpressions>();
            driver.frames=frames; driver.eyes=eyes.ToArray();
            foreach (string name in new[] { "Cat_Ear_L", "Cat_Ear_R" })
            {
                // Imported ear meshes use local Y for depth. Enlarge the outer silhouette.
                var ear = Find(cat, name);
                ear.localScale = Vector3.Scale(ear.localScale, new Vector3(1.30f, 1f, 1.30f));
                PrefabUtility.RecordPrefabInstancePropertyModifications(ear);
            }
            var whiteMat=new Material(material);whiteMat.name="MuzzleWhite";
            var white=new Texture2D(4,4);white.SetPixels(Enumerable.Repeat(Color.white,16).ToArray());white.Apply();
            File.WriteAllBytes(Out+"/MuzzleWhite.png",white.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(white); AssetDatabase.ImportAsset(Out+"/MuzzleWhite.png");
            whiteMat.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Out+"/MuzzleWhite.png"));
            whiteMat=SaveMaterial(whiteMat,"MuzzleWhite.mat");
            foreach(string name in new[]{"Cat_Muzzle_L","Cat_Muzzle_R"})
            {
                var t=Find(cat,name);var f=t.GetComponent<MeshFilter>();var m=UnityEngine.Object.Instantiate(f.sharedMesh);
                var toHead=head.worldToLocalMatrix*t.localToWorldMatrix;var fromHead=toHead.inverse;
                var c=toHead.MultiplyPoint3x4(m.bounds.center);var v=m.vertices;
                for(int i=0;i<v.Length;i++) {var q=toHead.MultiplyPoint3x4(v[i])-c;q=Vector3.Scale(q,new Vector3(1.12f,1.12f,1.5f));v[i]=fromHead.MultiplyPoint3x4(c+q+new Vector3(0,0,.018f));}
                m.vertices=v;m.RecalculateNormals();m.RecalculateBounds();
                f.sharedMesh=SaveMesh(m,name+"_Puffy.asset");
                t.GetComponent<MeshRenderer>().sharedMaterials=Enumerable.Repeat(whiteMat,m.subMeshCount).ToArray();
                PrefabUtility.RecordPrefabInstancePropertyModifications(f);
                PrefabUtility.RecordPrefabInstancePropertyModifications(t.GetComponent<MeshRenderer>());
            }
            var nose=Find(cat,"Cat_Nose");var nf=nose.GetComponent<MeshFilter>();
            var center=head.InverseTransformPoint(nose.TransformPoint(nf.sharedMesh.bounds.center));
            center.z+=.035f;
            var heart=new Geometry();heart.Heart(center,nose.worldToLocalMatrix*head.localToWorldMatrix);
            nf.sharedMesh=SaveMesh(heart.Mesh("HeartNose"),"HeartNose.asset");
            nose.GetComponent<MeshRenderer>().sharedMaterials=new[]{material};
            PrefabUtility.RecordPrefabInstancePropertyModifications(nf);
            PrefabUtility.RecordPrefabInstancePropertyModifications(nose.GetComponent<MeshRenderer>());
            PrefabUtility.SaveAsPrefabAsset(cat, Out + "/LuckyCat_DesignV2.prefab");
        }
        finally { UnityEngine.Object.DestroyImmediate(cat); }
        AssetDatabase.SaveAssets();
        string report = CaptureAndValidate(original, AssetDatabase.LoadAssetAtPath<GameObject>(Out + "/LuckyCat_DesignV2.prefab"));
        File.WriteAllText(Review + "/validation.txt", report);
        File.WriteAllText(Out + "/README.txt", "Lucky Cat Design V2. Independent prefab variant. Scalloped black painted eyes with white highlights; 4 replaceable RGBA PNG eye frames (Sparkle, Happy, Blink, Sad). Select LuckyCatEyeExpressions.expression to preview. Existing body mood Animator is retained, not automatically wired to the new frames. Outer ears widened and raised 30% in mesh space; pure-white muzzle widened 12%, depth increased 50%; pink heart nose. Original scene and model unchanged. Rebuild: Reefkeeper > Art > Build Lucky Cat Design V2. Review: artifacts/lucky-cat-design-v2.\n");
        AssetDatabase.Refresh();
        return report;
    }

    [MenuItem("Reefkeeper/Art/Build Lucky Cat Design V2")]
    public static void Build() { Debug.Log(Main()); }
    static Material SaveMaterial(Material m,string file) {
        string path=Out+"/"+file;var old=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(old) {EditorUtility.CopySerialized(m,old);UnityEngine.Object.DestroyImmediate(m);return old;} else {AssetDatabase.CreateAsset(m,path);return m;}
    }
    static Texture2D LoadEye(string name)
    {
        var path = Out + "/" + name + ".png";
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path) ?? throw new FileNotFoundException(path);
    }
    static Mesh EyeSurface() {
        var v=new List<Vector3>();var uv=new List<Vector2>();var tri=new List<int>();const int n=8;
        for(int y=0;y<=n;y++)for(int x=0;x<=n;x++) {float u=(float)x/n,w=(float)y/n;float px=(u*2-1)*.13f,py=(w*2-1)*.13f;v.Add(new Vector3(px,py,.014f+.006f*Mathf.Max(0,1-(px*px+py*py)/.034f)));uv.Add(new Vector2(1-u,w));}
        for(int y=0;y<n;y++)for(int x=0;x<n;x++) {int a=y*(n+1)+x;tri.AddRange(new[]{a,a+1,a+n+2,a,a+n+2,a+n+1});}
        var m=new Mesh{name="PaintedEyeSurface"};m.SetVertices(v);m.SetUVs(0,uv);m.SetTriangles(tri,0);m.RecalculateNormals();m.RecalculateBounds();return m;
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
        public void Heart(Vector3 center,Matrix4x4 matrix) {
            int c=Vertex(matrix.MultiplyPoint3x4(center+new Vector3(0,0,.01f)),4);const int n=64;
            for(int i=0;i<n;i++) {float t=i*2*Mathf.PI/n;float x=16*Mathf.Pow(Mathf.Sin(t),3)/16;float y=(13*Mathf.Cos(t)-5*Mathf.Cos(2*t)-2*Mathf.Cos(3*t)-Mathf.Cos(4*t)+2)/16;Vertex(matrix.MultiplyPoint3x4(center+new Vector3(x*.036f,y*.032f,0)),4);}
            for(int i=0;i<n;i++)triangles.AddRange(new[]{c,c+(i+1)%n+1,c+i+1});
        }
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
