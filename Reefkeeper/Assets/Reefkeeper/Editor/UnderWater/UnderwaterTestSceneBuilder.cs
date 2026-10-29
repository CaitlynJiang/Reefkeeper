using System.IO;
using Reefkeeper.UnderWater;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Reefkeeper.UnderWaterTools
{
    /// <summary>
    /// Builds TestScene_UnderWater: a simple layout for judging the water look —
    /// pillars receding into the distance (fog), a tall column (depth light falloff)
    /// and coloured spheres at several distances (colour absorption).
    /// </summary>
    public static class UnderwaterTestSceneBuilder
    {
        const string MaterialsDir = "Assets/Reefkeeper/Materials/UnderWater";
        public const string ScenePath = "Assets/Reefkeeper/Scenes/UnderWater/TestScene_UnderWater.unity";
        const string PropsName = "Test Props";
        const string EnvName = "Underwater Environment";
        const string ShaftsName = "Light Shafts";
        const string SurfaceName = "Water Surface";
        const string SnowName = "Marine Snow";
        const string SurfaceMeshPath = "Assets/Reefkeeper/Meshes/UnderWater/UW_SurfaceGrid.asset";
        const string VolumeName = "Post Volume";
        const string ProfilePath = "Assets/Reefkeeper/Settings/UnderWater/UW_PostProfile.asset";

        // Stylised grade: bloom for glowing lights, neutral tonemap keeps colour,
        // lifted and cool shadows, warm highlights, soft vignette.
        // Created once; tweak the asset freely afterwards.
        static VolumeProfile PostProfile()
        {
            var existing = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
            if (existing) return existing;
            Directory.CreateDirectory(Path.GetDirectoryName(ProfilePath));
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, ProfilePath);

            var bloom = Add<Bloom>(profile);
            bloom.threshold.value = 0.95f;
            bloom.intensity.value = 0.8f;
            bloom.scatter.value = 0.75f;
            bloom.tint.value = new Color(1f, 0.97f, 0.9f);
            bloom.highQualityFiltering.value = true;

            Add<Tonemapping>(profile).mode.value = TonemappingMode.Neutral;

            var adjust = Add<ColorAdjustments>(profile);
            adjust.postExposure.value = 0.15f;
            adjust.contrast.value = -8f;
            adjust.saturation.value = 18f;
            adjust.colorFilter.value = Color.white;
            adjust.hueShift.overrideState = false;

            var split = Add<SplitToning>(profile);
            split.shadows.value = new Color(0.55f, 0.5f, 0.85f);
            split.highlights.value = new Color(1f, 0.9f, 0.72f);
            split.balance.value = -15f;

            var smh = Add<ShadowsMidtonesHighlights>(profile);
            smh.shadows.value = new Vector4(0.97f, 0.97f, 1.08f, 0.05f); // slightly lifted, bluer shadows
            smh.midtones.overrideState = false;
            smh.highlights.overrideState = false;

            var vignette = Add<Vignette>(profile);
            vignette.intensity.value = 0.22f;
            vignette.smoothness.value = 0.5f;
            vignette.color.value = new Color(0.04f, 0.12f, 0.2f);
            vignette.rounded.overrideState = false;
            vignette.center.overrideState = false;

            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            return profile;
        }

        static T Add<T>(VolumeProfile profile) where T : VolumeComponent
        {
            var c = profile.Add<T>(true);
            c.name = typeof(T).Name;
            AssetDatabase.AddObjectToAsset(c, profile);
            return c;
        }

        // 160 m square, 1.25 m cells: fine enough for vertex waves.
        static Mesh SurfaceGrid()
        {
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(SurfaceMeshPath);
            if (existing) return existing;
            const int n = 128;
            const float size = 160f;
            var verts = new Vector3[(n + 1) * (n + 1)];
            var normals = new Vector3[verts.Length];
            var uvs = new Vector2[verts.Length];
            for (int z = 0; z <= n; z++)
            for (int x = 0; x <= n; x++)
            {
                int i = z * (n + 1) + x;
                verts[i] = new Vector3((x / (float)n - 0.5f) * size, 0, (z / (float)n - 0.5f) * size);
                normals[i] = Vector3.up;
                uvs[i] = new Vector2(x / (float)n, z / (float)n);
            }
            var tris = new int[n * n * 6];
            for (int z = 0, t = 0; z < n; z++)
            for (int x = 0; x < n; x++, t += 6)
            {
                int i = z * (n + 1) + x;
                tris[t] = i; tris[t + 1] = i + n + 1; tris[t + 2] = i + 1;
                tris[t + 3] = i + 1; tris[t + 4] = i + n + 1; tris[t + 5] = i + n + 2;
            }
            var mesh = new Mesh { name = "UW_SurfaceGrid", vertices = verts, normals = normals, uv = uvs, triangles = tris };
            mesh.bounds = new Bounds(Vector3.zero, new Vector3(size, 4f, size)); // room for waves
            Directory.CreateDirectory(Path.GetDirectoryName(SurfaceMeshPath));
            AssetDatabase.CreateAsset(mesh, SurfaceMeshPath);
            return mesh;
        }

        static void MarineSnow(Transform cam, Material mat)
        {
            var go = new GameObject(SnowName);
            go.transform.SetParent(cam, false);
            go.transform.localPosition = new Vector3(0, 0, 8);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.loop = true;
            main.prewarm = true;
            main.duration = 10f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(10f, 16f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.02f, 0.07f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1, 1, 1, 0.35f), new Color(1, 1, 1, 0.85f));
            main.maxParticles = 2500;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = ps.emission;
            emission.rateOverTime = 180f;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(30f, 16f, 30f);

            var velocity = ps.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = new ParticleSystem.MinMaxCurve(-0.02f, 0.02f);
            velocity.y = new ParticleSystem.MinMaxCurve(-0.05f, 0.005f); // slow sinking
            velocity.z = new ParticleSystem.MinMaxCurve(-0.02f, 0.02f);

            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.12f;
            noise.frequency = 0.25f;
            noise.scrollSpeed = 0.08f;
            noise.quality = ParticleSystemNoiseQuality.Medium;

            var fade = ps.colorOverLifetime;
            fade.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, 0.2f), new GradientAlphaKey(1, 0.8f), new GradientAlphaKey(0, 1) });
            fade.color = g;

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = mat;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            ps.Play();
        }

        // Fills TestScene_UnderWater in place: replaces only the objects this tool made,
        // reuses the scene's own Main Camera and Directional Light.
        [MenuItem("Reefkeeper/UnderWater/Set up test scene - 布置水体测试场景")]
        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Directory.CreateDirectory(MaterialsDir);
            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            AssetDatabase.Refresh();

            var scene = File.Exists(ScenePath)
                ? EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single)
                : EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            foreach (var root in scene.GetRootGameObjects())
                if (root.name == PropsName) Object.DestroyImmediate(root);

            var sand = Mat("UW_Sand", new Color(0.78f, 0.74f, 0.60f));
            var rock = Mat("UW_Rock", new Color(0.52f, 0.55f, 0.50f));
            var orange = Mat("UW_CoralOrange", new Color(0.98f, 0.45f, 0.20f));
            var violet = Mat("UW_CoralViolet", new Color(0.70f, 0.45f, 0.85f));
            var white = Mat("UW_White", new Color(0.92f, 0.92f, 0.92f));
            var sky = LoadOrCreate("UW_Skybox", "Reefkeeper/UnderWater/Skybox");

            // Our own fog replaces Unity's.
            RenderSettings.fog = false;
            RenderSettings.skybox = sky;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = Color.black;

            // Kept between runs so tuned values survive.
            var env = FindOrCreate<UnderwaterEnvironment>(EnvName);
            if (!env.causticsTexture) env.causticsTexture = UnderwaterCausticsGenerator.GetOrCreate();
            env.Apply();

            var sun = FindOrCreate<Light>("Directional Light");
            sun.type = LightType.Directional;
            sun.color = new Color(0.95f, 1f, 0.92f);
            sun.intensity = 1.6f;
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(62, -25, 0);
            RenderSettings.sun = sun;

            var cam = FindOrCreate<Camera>("Main Camera");
            cam.tag = "MainCamera";
            cam.clearFlags = CameraClearFlags.Skybox;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 400;
            cam.fieldOfView = 60;
            cam.transform.SetPositionAndRotation(new Vector3(0, 2.5f, -8), Quaternion.Euler(6, 0, 0));
            cam.GetUniversalAdditionalCameraData().renderPostProcessing = true;

            var volume = FindOrCreate<Volume>(VolumeName);
            volume.isGlobal = true;
            volume.priority = 10; // above any project-wide grade
            if (!volume.sharedProfile) volume.sharedProfile = PostProfile();

            // Light shafts: kept between runs like the environment.
            bool newShafts = !GameObject.Find(ShaftsName);
            var shafts = FindOrCreate<UnderwaterLightShafts>(ShaftsName);
            var shaftRenderer = shafts.GetComponent<MeshRenderer>();
            shaftRenderer.sharedMaterial = LoadOrCreate("UW_LightShafts", "Reefkeeper/UnderWater/Light Shaft");
            shaftRenderer.shadowCastingMode = ShadowCastingMode.Off;
            shaftRenderer.receiveShadows = false;
            shafts.sun = sun;
            if (newShafts) shafts.transform.position = new Vector3(0, env.surfaceY - 1f, 22);

            // Water surface: subdivided grid at the surface height (moves if surfaceY changes).
            var surface = FindOrCreate<MeshFilter>(SurfaceName);
            surface.sharedMesh = SurfaceGrid();
            if (!surface.TryGetComponent(out MeshRenderer surfaceRenderer)) surfaceRenderer = surface.gameObject.AddComponent<MeshRenderer>();
            surfaceRenderer.sharedMaterial = LoadOrCreate("UW_Surface", "Reefkeeper/UnderWater/Surface");
            surfaceRenderer.shadowCastingMode = ShadowCastingMode.Off; // must not shadow the whole scene
            surfaceRenderer.receiveShadows = false;
            surface.transform.SetPositionAndRotation(new Vector3(0, env.surfaceY, 20), Quaternion.identity);

            // Suspended particles follow the camera; only configured when first created.
            if (!GameObject.Find(SnowName)) MarineSnow(cam.transform, LoadOrCreate("UW_MarineSnow", "Reefkeeper/UnderWater/Particle"));

            var props = new GameObject(PropsName).transform;

            var floor = Prim(PrimitiveType.Plane, "Sand Floor", props, sand);
            floor.localScale = new Vector3(40, 1, 40);

            // Fog: pillars every 9 m into the distance.
            var pillars = Group("Fog pillars 雾距离柱", props);
            for (int i = 0; i < 10; i++)
            {
                float h = 3f + (i % 3) * 1.5f;
                var p = Prim(PrimitiveType.Cylinder, $"Pillar {4 + i * 9}m", pillars, rock);
                p.localScale = new Vector3(1.2f, h * 0.5f, 1.2f);
                p.position = new Vector3(i % 2 == 0 ? -4 : 4, h * 0.5f, 4 + i * 9);
            }

            // Depth light falloff: a column rising toward the surface.
            var column = Prim(PrimitiveType.Cylinder, "Depth column 深度光衰减柱", props, white);
            column.localScale = new Vector3(1.5f, 12f, 1.5f);
            column.position = new Vector3(-12, 12, 18);

            // Colour absorption: the same colours at 6 m, 18 m and 40 m.
            var spheres = Group("Absorption spheres 颜色吸收球", props);
            float[] distances = { 6, 18, 40 };
            foreach (float z in distances)
            {
                Sphere($"Orange {z}m", spheres, orange, new Vector3(-1.4f, 1.1f, z));
                Sphere($"Violet {z}m", spheres, violet, new Vector3(0, 1.1f, z));
                Sphere($"White {z}m", spheres, white, new Vector3(1.4f, 1.1f, z));
            }

            // A few rock mounds so the floor isn't flat.
            var mounds = Group("Rock mounds", props);
            var rng = new System.Random(7);
            for (int i = 0; i < 14; i++)
            {
                var m = Prim(PrimitiveType.Sphere, $"Mound {i}", mounds, rock);
                float s = 1.5f + (float)rng.NextDouble() * 3f;
                m.localScale = new Vector3(s * 1.4f, s * 0.6f, s);
                m.position = new Vector3((float)(rng.NextDouble() * 60 - 30), 0, (float)(rng.NextDouble() * 80 + 2));
                m.rotation = Quaternion.Euler(0, (float)rng.NextDouble() * 360, 0);
            }

            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            Selection.activeObject = env.gameObject;
            Debug.Log($"[UnderWater] Built {ScenePath}. Tune the look on 'Underwater Environment'.");
        }

        [MenuItem("Reefkeeper/UnderWater/Preset - Stylized bright 风格化明亮")]
        static void PresetStylized() => WithEnvironment(e => e.PresetStylized());

        [MenuItem("Reefkeeper/UnderWater/Preset - Realistic deep 写实深水")]
        static void PresetRealistic() => WithEnvironment(e => e.PresetRealistic());

        static void WithEnvironment(System.Action<UnderwaterEnvironment> apply)
        {
            var env = Object.FindFirstObjectByType<UnderwaterEnvironment>();
            if (!env)
            {
                EditorUtility.DisplayDialog("No Underwater Environment",
                    "Open TestScene_UnderWater (or run Set up test scene) first.\n请先打开测试场景或运行布置菜单。", "OK");
                return;
            }
            apply(env);
            EditorUtility.SetDirty(env);
            EditorSceneManager.MarkSceneDirty(env.gameObject.scene);
            Selection.activeObject = env.gameObject;
        }

        static T FindOrCreate<T>(string name) where T : Component
        {
            var go = GameObject.Find(name);
            if (!go) go = new GameObject(name);
            return go.TryGetComponent(out T c) ? c : go.AddComponent<T>();
        }

        static Material Mat(string name, Color color)
        {
            var m = LoadOrCreate(name, "Reefkeeper/UnderWater/Lit", out bool created);
            if (created) { m.SetColor("_BaseColor", color); EditorUtility.SetDirty(m); }
            return m;
        }

        static Material LoadOrCreate(string name, string shaderName) => LoadOrCreate(name, shaderName, out _);

        static Material LoadOrCreate(string name, string shaderName, out bool created)
        {
            string path = $"{MaterialsDir}/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            created = m == null;
            if (!created) return m;
            var shader = Shader.Find(shaderName);
            if (!shader) throw new System.Exception($"Shader '{shaderName}' not found. Wait for import, then retry.");
            m = new Material(shader);
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        static Transform Group(string name, Transform parent)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            return t;
        }

        static Transform Prim(PrimitiveType type, string name, Transform parent, Material mat)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.GetComponent<Renderer>().sharedMaterial = mat;
            return go.transform;
        }

        static void Sphere(string name, Transform parent, Material mat, Vector3 pos)
        {
            var t = Prim(PrimitiveType.Sphere, name, parent, mat);
            t.localScale = Vector3.one * 1.1f;
            t.position = pos;
        }
    }
}
