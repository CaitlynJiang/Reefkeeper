using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Random = UnityEngine.Random;

namespace Reefkeeper.Editor
{
    public static class ReefSceneBuilder
    {
        const string Generated = "Assets/Reefkeeper/Generated";
        public const string ScenePath = "Assets/Reefkeeper/Scenes/ShallowReef.unity";
        static Material sand, stone, orange, violet, mint, grass, gold;

        [MenuItem("Reefkeeper/Create or rebuild shallow reef")]
        public static void CreateScene()
        {
            if (!Application.isBatchMode && File.Exists(ScenePath) &&
                !EditorUtility.DisplayDialog("Rebuild prototype scene?",
                    "This regenerates ShallowReef and its generated assets. Save custom scene edits as another scene first.",
                    "Rebuild", "Cancel")) return;
            Directory.CreateDirectory(Generated);
            Directory.CreateDirectory("Assets/Reefkeeper/Scenes");
            AssetDatabase.Refresh();
            var savedRandom = Random.state;
            Random.InitState(417);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Settings/PC_RPAsset.asset");
            if (!pipeline) throw new Exception("Bundled URP pipeline asset is missing.");
            GraphicsSettings.defaultRenderPipeline = pipeline;
            QualitySettings.renderPipeline = pipeline;
            pipeline.msaaSampleCount = 4;
            pipeline.shadowDistance = 50;
            pipeline.supportsCameraDepthTexture = true;
            pipeline.supportsCameraOpaqueTexture = true;
            EditorUtility.SetDirty(pipeline);
            ConfigureUnderwaterEffect();
            PlayerSettings.companyName = "Reefkeeper";
            PlayerSettings.productName = "Lucky Dive Co.";
            PlayerSettings.defaultScreenWidth = 1600;
            PlayerSettings.defaultScreenHeight = 900;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.runInBackground = true;
            PlayerSettings.colorSpace = ColorSpace.Linear;
            QualitySettings.vSyncCount = 1;

            sand = Surface("Sand", new Color(0.69f, 0.73f, 0.57f), 0.95f, 1);
            stone = Surface("Limestone", new Color(0.38f, 0.48f, 0.42f), 0.6f);
            orange = Surface("FireCoral", new Color(0.94f, 0.37f, 0.13f), 0.3f);
            violet = Surface("LavenderCoral", new Color(0.63f, 0.39f, 0.66f), 0.3f);
            mint = Surface("PlateCoral", new Color(0.26f, 0.65f, 0.52f), 0.45f);
            grass = Surface("Seagrass", new Color(0.25f, 0.49f, 0.31f), 0.3f, 0, 0.55f);
            grass.SetFloat("_Cull", 0);
            gold = Surface("Fish", new Color(0.97f, 0.75f, 0.23f), 0.1f);

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.055f, 0.30f, 0.35f);
            RenderSettings.fogStartDistance = 9;
            RenderSettings.fogEndDistance = 65;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.35f, 0.64f, 0.65f);
            RenderSettings.ambientEquatorColor = new Color(0.15f, 0.38f, 0.4f);
            RenderSettings.ambientGroundColor = new Color(0.08f, 0.18f, 0.16f);
            RenderSettings.skybox = null;
            var sun = new GameObject("Sunlight through water").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(0.83f, 1, 0.9f);
            sun.intensity = 2.1f;
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(57, -35, 0);
            RenderSettings.sun = sun;
            var volume = new GameObject("Underwater color grade").AddComponent<Volume>();
            volume.isGlobal = true;
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            var tonemap = profile.Add<Tonemapping>(true);
            tonemap.mode.Override(TonemappingMode.ACES);
            var color = profile.Add<ColorAdjustments>(true);
            color.postExposure.Override(0.35f);
            color.contrast.Override(7);
            color.saturation.Override(8);
            var bloom = profile.Add<Bloom>(true);
            bloom.intensity.Override(0.2f);
            bloom.threshold.Override(1.1f);
            var vignette = profile.Add<Vignette>(true);
            vignette.intensity.Override(0.2f);
            vignette.smoothness.Override(0.7f);
            // Save subassets too: a serialized volume must survive an Editor restart.
            string profilePath = Generated + "/UnderwaterGrade.asset";
            if (File.Exists(profilePath))
            {
                volume.sharedProfile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(profilePath);
                UnityEngine.Object.DestroyImmediate(profile);
            }
            else
            {
                AssetDatabase.CreateAsset(profile, profilePath);
                foreach (var component in profile.components) AssetDatabase.AddObjectToAsset(component, profile);
                volume.sharedProfile = profile;
            }

            var environment = new GameObject("REEF / environment").transform;
            var ground = MeshObject("Rippling sand seabed", SaveMesh(Seabed(), "Seabed"), sand, environment);
            ground.AddComponent<MeshCollider>().sharedMesh = ground.GetComponent<MeshFilter>().sharedMesh;

            Mesh[] rocks = new Mesh[5];
            for (int i = 0; i < rocks.Length; i++) rocks[i] = SaveMesh(Rock(i), "Rock" + i);
            PlaceRock(environment, rocks[0], new Vector3(-11, 1.2f, 4), new Vector3(8, 6, 7));
            PlaceRock(environment, rocks[1], new Vector3(13, 0.8f, 9), new Vector3(9, 5, 8));
            PlaceRock(environment, rocks[2], new Vector3(-16, 2, 23), new Vector3(10, 8, 9));
            PlaceRock(environment, rocks[3], new Vector3(17, 1, 28), new Vector3(12, 7, 10));
            // A distant landmark gives the player something to swim toward.
            PlaceRock(environment, rocks[1], new Vector3(-3.5f, 3, 20), new Vector3(3.2f, 8, 4));
            PlaceRock(environment, rocks[2], new Vector3(4.8f, 3.5f, 22), new Vector3(3.2f, 9, 4));
            PlaceRock(environment, rocks[3], new Vector3(0.5f, 7, 21), new Vector3(10, 3, 4));
            for (int i = 0; i < 44; i++)
            {
                float x = Random.Range(-34f, 34f), z = Random.Range(-27f, 36f);
                if (Mathf.Abs(x) < 4.5f && z < 18) continue;
                float s = Random.Range(0.7f, 3.3f);
                PlaceRock(environment, rocks[i % 5], new Vector3(x, FloorHeight(x, z) + s * 0.18f, z),
                    new Vector3(s * 1.5f, s, s * 1.3f));
            }

            var life = new GameObject("REEF / corals and seagrass").transform;
            Mesh[] branches = new Mesh[4];
            for (int i = 0; i < 4; i++) branches[i] = SaveMesh(Coral(i), "BranchCoral" + i);
            Mesh sponge = SaveMesh(Sponge(), "TubeSponge");
            Mesh plate = SaveMesh(Plate(), "TableCoral");
            Mesh blades = SaveMesh(Seagrass(), "Seagrass");
            // Deliberate foreground clusters frame the sandy swim corridor.
            Cluster(life, branches, sponge, plate, -6, -9, 1.35f);
            Cluster(life, branches, sponge, plate, 6.5f, -6, 1.5f);
            Cluster(life, branches, sponge, plate, -7, 4, 1.2f);
            Cluster(life, branches, sponge, plate, 7, 9, 1.5f);
            Cluster(life, branches, sponge, plate, -9, 16, 1.1f);
            for (int i = 0; i < 25; i++)
            {
                float x = Random.Range(-29f, 29f), z = Random.Range(-25f, 32f);
                if (Mathf.Abs(x) < 4 && z < 16) continue;
                Cluster(life, branches, sponge, plate, x, z, Random.Range(0.45f, 0.9f));
            }
            for (int i = 0; i < 150; i++)
            {
                float x = Random.Range(-32f, 32f), z = Random.Range(-28f, 35f);
                if (Mathf.Abs(x) < 3.5f) continue;
                var g = MeshObject("Seagrass", blades, grass, life);
                g.transform.position = new Vector3(x, FloorHeight(x, z) - 0.05f, z);
                g.transform.rotation = Quaternion.Euler(0, Random.Range(0, 360), 0);
                g.transform.localScale = Vector3.one * Random.Range(0.45f, 1.2f);
            }
            var water = MeshObject("Surface / viewed from below", SaveMesh(WaterQuad(), "WaterSurface"),
                MaterialAsset("Water", "Reefkeeper/Water Ceiling", new Color(0.13f, 0.59f, 0.62f)), environment);
            water.transform.position = new Vector3(0, 15, 0);
            water.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            var beamMat = MaterialAsset("Sunbeams", "Reefkeeper/Water Light", new Color(0.36f, 0.72f, 0.58f, 0.065f));
            for (int i = 0; i < 9; i++)
            {
                var beam = MeshObject("Shaft of sunlight", SaveMesh(LightShaft(Random.Range(0.5f, 1.2f)), "Shaft" + i), beamMat, environment);
                beam.transform.position = new Vector3(Random.Range(-16f, 16f), 0, Random.Range(-8f, 30f));
                beam.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            }
            CreatePlankton(environment);
            CreateFish(environment);

            var player = new GameObject("Diver / desktop input");
            player.transform.position = new Vector3(0, 3.4f, -17);
            var cc = player.AddComponent<CharacterController>();
            cc.height = 1.25f;
            cc.radius = 0.3f;
            cc.center = Vector3.zero;
            cc.stepOffset = 0;
            cc.slopeLimit = 90;
            cc.skinWidth = 0.04f;
            cc.minMoveDistance = 0;
            var cameraObject = new GameObject("Diver view");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.SetParent(player.transform, false);
            var camera = cameraObject.AddComponent<Camera>();
            camera.fieldOfView = 72;
            camera.nearClipPlane = 0.06f;
            camera.farClipPlane = 110;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = RenderSettings.fogColor;
            camera.allowHDR = true;
            camera.GetUniversalAdditionalCameraData().renderPostProcessing = true;
            cameraObject.AddComponent<AudioListener>();
            var motor = player.AddComponent<DiveMotor>();
            motor.view = camera;
            player.AddComponent<DiveHUD>().diver = motor;
            player.AddComponent<PrototypeSmokeCheck>();

            var seabedSettings = ground.AddComponent<ReefSeabed>();
            var anchors = new List<Transform>();
            foreach (var renderer in life.GetComponentsInChildren<MeshRenderer>()) anchors.Add(renderer.transform);
            foreach (Transform child in environment)
                if (child.name == "Reef limestone") anchors.Add(child);
            seabedSettings.groundedProps = anchors.ToArray();

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            Random.state = savedRandom;
            Debug.Log("REEFKEEPER_SCENE_READY " + ScenePath);
            ValidateMovement();
        }

        [MenuItem("Reefkeeper/Validate swimming math")]
        public static void ValidateMovement()
        {
            Vector3 v = Vector3.zero;
            for (int i = 0; i < 600; i++) v = DiveMotor.IntegrateVelocity(v, Vector3.forward, false, false, 1f / 60, 3.2f, 6.5f, 4.5f, 0.75f, 12);
            Require(Mathf.Abs(v.magnitude - 3.2f) < 0.001f, "Cruise speed");
            for (int i = 0; i < 600; i++) v = DiveMotor.IntegrateVelocity(v, Vector3.one, true, false, 1f / 60, 3.2f, 6.5f, 4.5f, 0.75f, 12);
            Require(Mathf.Abs(v.magnitude - 6.5f) < 0.001f, "Diagonal speed cap");
            Vector3 coast = DiveMotor.IntegrateVelocity(v, Vector3.zero, false, false, 0.1f, 3.2f, 6.5f, 4.5f, 0.75f, 12);
            Require(coast.magnitude < v.magnitude && coast.magnitude > 0, "Water drag preserves glide");
            for (int i = 0; i < 60; i++) v = DiveMotor.IntegrateVelocity(v, Vector3.forward, true, true, 1f / 60, 3.2f, 6.5f, 4.5f, 0.75f, 12);
            Require(v == Vector3.zero, "Brake overrides thrust and stops");
            Vector3 a = Vector3.forward * 4, b = a;
            for (int i = 0; i < 30; i++) a = DiveMotor.IntegrateVelocity(a, Vector3.zero, false, false, 1f / 30, 3.2f, 6.5f, 4.5f, 0.75f, 12);
            for (int i = 0; i < 120; i++) b = DiveMotor.IntegrateVelocity(b, Vector3.zero, false, false, 1f / 120, 3.2f, 6.5f, 4.5f, 0.75f, 12);
            Require((a - b).magnitude < 0.0001f, "Frame-rate independent coast");
            Debug.Log("REEFKEEPER_MOVEMENT_CHECKS_PASSED: cruise, sprint, diagonal cap, coasting, braking, frame-rate independence");
        }
        static void Require(bool condition, string label) { if (!condition) throw new Exception("Check failed: " + label); }

        public static void BuildWindows()
        {
            if (!File.Exists(ScenePath)) CreateScene();
            ValidateMovement();
            Directory.CreateDirectory("Builds/Windows");
            var result = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes = new[] { ScenePath }, locationPathName = "Builds/Windows/Reefkeeper.exe",
                target = BuildTarget.StandaloneWindows64, options = BuildOptions.Development });
            if (result.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
                throw new Exception("Windows build failed: " + result.summary.result);
            File.Copy("Assets/ThirdParty/URP-WaterShaders/LICENSE.txt", "Builds/Windows/URP-WaterShaders-LICENSE.txt", true);
            if (File.Exists("Assets/Samples/XRI-LICENSE.md")) File.Copy("Assets/Samples/XRI-LICENSE.md", "Builds/Windows/XRI-LICENSE.md", true);
            if (File.Exists("Assets/ThirdParty/QuaterniusAnimatedFish/License.txt"))
                File.Copy("Assets/ThirdParty/QuaterniusAnimatedFish/License.txt", "Builds/Windows/Quaternius-Fish-LICENSE.txt", true);
            if (File.Exists("Assets/UI/Fonts/Nunito/OFL.txt"))
                File.Copy("Assets/UI/Fonts/Nunito/OFL.txt", "Builds/Windows/Nunito-OFL.txt", true);
            if (File.Exists("Assets/UI/InputPrompts/KENNEY-LICENSE.txt"))
                File.Copy("Assets/UI/InputPrompts/KENNEY-LICENSE.txt", "Builds/Windows/Kenney-InputPrompts-LICENSE.txt", true);
            Debug.Log("REEFKEEPER_BUILD_PASSED");
        }

        public static float FloorHeight(float x, float z)
        {
            return ReefSeabed.Height(x, z);
        }
        static Material Surface(string name, Color color, float caustics, float sandAmount = 0, float sway = 0)
        {
            // Preserve vertex animation on grass and avoid world-fixed caustics on moving fish.
            if (sway == 0 && name != "Fish")
            {
                var underwater = MaterialAsset(name, "Shader Graphs/WaterUnder", color);
                underwater.SetTexture("Texture2D_D71B5CBC", Texture2D.whiteTexture);
                underwater.SetColor("Color_DB714CC1", color);
                underwater.SetColor("Color_2B7C42F7", new Color(1.2f, 1.8f, 1.65f) * caustics);
                underwater.SetFloat("Vector1_3E4E7F58", 15);
                underwater.SetFloat("Vector1_810FF6ED", 1.2f);
                underwater.SetFloat("Vector1_DE7903F8", 1.5f);
                underwater.SetFloat("Vector1_994EA204", 3.5f);
                underwater.SetFloat("Vector1_E79FBA7D", 4);
                underwater.SetFloat("Vector1_5443DC82", 1);
                underwater.SetFloat("Vector1_A8CC0681", 2);
                underwater.SetVector("Vector4_85FB229A", new Vector4(0.08f, 0.05f, -0.04f, 0.06f));
                EditorUtility.SetDirty(underwater);
                return underwater;
            }
            var m = MaterialAsset(name, "Reefkeeper/Reef Surface", color);
            m.SetFloat("_Caustics", 0); m.SetFloat("_Sand", sandAmount); m.SetFloat("_Sway", sway);
            EditorUtility.SetDirty(m);
            return m;
        }
        static void ConfigureUnderwaterEffect()
        {
            var material = MaterialAsset("UnderwaterDistortion", "Shader Graphs/WaterDistortion", Color.white);
            material.SetFloat("_World_Water_Level", 15);
            material.SetFloat("_Distortion", 0.002f);
            material.SetFloat("_Distortion_Scale", 5);
            material.SetFloat("_DistortionBlendDistance", 2);
            material.SetVector("_Speeds", new Vector4(0.035f, 0.025f, 0, 0));
            material.SetColor("_UnderwaterTint", new Color(0.83f, 0.96f, 1, 0));
            EditorUtility.SetDirty(material);
            var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>("Assets/Settings/PC_Renderer.asset");
            if (!renderer) throw new Exception("PC renderer is missing.");
            FullScreenPassRendererFeature feature = null;
            foreach (var existing in renderer.rendererFeatures)
                if (existing.name == "Reef underwater distortion") feature = existing as FullScreenPassRendererFeature;
            if (!feature)
            {
                feature = ScriptableObject.CreateInstance<FullScreenPassRendererFeature>();
                feature.name = "Reef underwater distortion";
                AssetDatabase.AddObjectToAsset(feature, renderer);
                renderer.rendererFeatures.Add(feature);
            }
            feature.passMaterial = material;
            feature.injectionPoint = FullScreenPassRendererFeature.InjectionPoint.BeforeRenderingPostProcessing;
            feature.fetchColorBuffer = true;
            feature.requirements = ScriptableRenderPassInput.Depth;
            feature.SetActive(true);
            EditorUtility.SetDirty(feature);
            renderer.SetDirty();
            EditorUtility.SetDirty(renderer);
        }

        public static void RebuildAndBuildWindows()
        {
            CreateScene();
            BuildWindows();
        }
        static Material MaterialAsset(string name, string shader, Color color)
        {
            var s = Shader.Find(shader);
            if (!s) throw new Exception("Shader missing: " + shader);
            string path = Generated + "/" + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!m) { m = new Material(s); AssetDatabase.CreateAsset(m, path); }
            m.shader = s; m.SetColor("_BaseColor", color);
            EditorUtility.SetDirty(m);
            return m;
        }
        static Mesh SaveMesh(Mesh mesh, string name)
        {
            string path = Generated + "/" + name + ".asset";
            mesh.name = name;
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing) { EditorUtility.CopySerialized(mesh, existing); UnityEngine.Object.DestroyImmediate(mesh); return existing; }
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }
        static GameObject MeshObject(string name, Mesh mesh, Material material, Transform parent)
        {
            var o = new GameObject(name);
            o.transform.SetParent(parent, false);
            o.AddComponent<MeshFilter>().sharedMesh = mesh;
            o.AddComponent<MeshRenderer>().sharedMaterial = material;
            return o;
        }
        static void PlaceRock(Transform parent, Mesh mesh, Vector3 position, Vector3 size)
        {
            var rock = MeshObject("Reef limestone", mesh, stone, parent);
            rock.transform.SetPositionAndRotation(position, Quaternion.Euler(0, Random.Range(0, 360), Random.Range(-10f, 10f)));
            rock.transform.localScale = size;
            rock.AddComponent<MeshCollider>().sharedMesh = mesh;
        }
        static void Cluster(Transform parent, Mesh[] coral, Mesh sponge, Mesh plate, float x, float z, float scale)
        {
            var root = new GameObject("Coral garden").transform;
            root.SetParent(parent, false);
            root.position = new Vector3(x, FloorHeight(x, z), z);
            root.localScale = Vector3.one * scale;
            root.rotation = Quaternion.Euler(0, Random.Range(0, 360), 0);
            var branching = MeshObject("Branching coral", coral[Random.Range(0, 4)], orange, root);
            branching.transform.localScale = Vector3.one * Random.Range(0.9f, 1.5f);
            for (int i = 0; i < 4; i++)
            {
                var tube = MeshObject("Purple tube sponge", sponge, violet, root);
                tube.transform.localPosition = new Vector3(1.15f + Random.Range(-0.35f, 0.35f), 0, Random.Range(-0.55f, 0.55f));
                tube.transform.localScale = new Vector3(Random.Range(0.6f, 1), Random.Range(0.7f, 1.5f), Random.Range(0.6f, 1));
                tube.transform.localRotation = Quaternion.Euler(Random.Range(-12f, 12f), 0, Random.Range(-12f, 12f));
            }
            for (int i = 0; i < 3; i++)
            {
                var p = MeshObject("Layered table coral", plate, mint, root);
                p.transform.localPosition = new Vector3(-1.0f - i * 0.16f, i * 0.3f, 0.4f + i * 0.15f);
                p.transform.localScale = Vector3.one * (1.2f - i * 0.22f);
            }
        }

        static Mesh Seabed()
        {
            var d = new MeshDraft(); const int n = 192;
            for (int z = 0; z <= n; z++) for (int x = 0; x <= n; x++)
            {
                float px = (x / (float)n - 0.5f) * 110, pz = (z / (float)n - 0.5f) * 110;
                d.Vertex(new Vector3(px, FloorHeight(px, pz), pz), new Vector2(x / (float)n, z / (float)n));
            }
            for (int z = 0; z < n; z++) for (int x = 0; x < n; x++)
            {
                int a = z * (n + 1) + x;
                d.Triangle(a, a + n + 1, a + 1); d.Triangle(a + 1, a + n + 1, a + n + 2);
            }
            return d.Build();
        }
        static Mesh Rock(int seed)
        {
            var d = new MeshDraft(); const int lat = 16, lon = 24;
            for (int y = 0; y <= lat; y++) for (int x = 0; x <= lon; x++)
            {
                float a = x * Mathf.PI * 2 / lon, b = y * Mathf.PI / lat;
                Vector3 p = new Vector3(Mathf.Sin(b) * Mathf.Cos(a), Mathf.Cos(b), Mathf.Sin(b) * Mathf.Sin(a));
                float noise = Mathf.PerlinNoise(p.x * 2 + seed * 3 + 20, p.z * 2 + p.y + 20);
                p *= 0.43f + noise * 0.17f;
                d.Vertex(p, new Vector2(x / (float)lon, y / (float)lat));
            }
            for (int y = 0; y < lat; y++) for (int x = 0; x < lon; x++)
            {
                int a = y * (lon + 1) + x;
                d.Triangle(a, a + 1, a + lon + 1); d.Triangle(a + 1, a + lon + 2, a + lon + 1);
            }
            return d.Build();
        }
        static Mesh Coral(int seed)
        {
            var d = new MeshDraft();
            for (int i = 0; i < 4; i++)
                Branch(d, new Vector3((i - 1.5f) * 0.14f, 0, 0),
                    Quaternion.Euler(Random.Range(-15f, 15f), i * 95 + seed * 13, (i - 1.5f) * 18) * Vector3.up,
                    Random.Range(0.55f, 0.8f), 0.085f, 3);
            return d.Build();
        }
        static void Branch(MeshDraft d, Vector3 start, Vector3 direction, float length, float radius, int depth)
        {
            Vector3 end = start + direction * length;
            d.Tube(start, end, radius, radius * 0.68f, 7);
            if (depth <= 0) return;
            for (int i = 0; i < 3; i++)
            {
                Vector3 next = Quaternion.AngleAxis(i * 120 + Random.Range(-15f, 15f), direction)
                    * (Quaternion.FromToRotation(Vector3.up, direction) * Quaternion.Euler(0, 0, Random.Range(24f, 46f)) * Vector3.up);
                Branch(d, end, (next + Vector3.up * 0.25f).normalized, length * Random.Range(0.56f, 0.73f), radius * 0.65f, depth - 1);
            }
        }
        static Mesh Sponge()
        {
            var d = new MeshDraft(); const int rings = 12, sides = 20;
            for (int r = 0; r <= rings; r++) for (int s = 0; s <= sides; s++)
            {
                float y = r / (float)rings, a = s * Mathf.PI * 2 / sides;
                float radius = (0.2f + y * 0.08f) * (1 + Mathf.Sin(y * 40) * 0.04f);
                d.Vertex(new Vector3(Mathf.Cos(a) * radius, y * 1.25f, Mathf.Sin(a) * radius), new Vector2(s / (float)sides, y));
            }
            for (int r = 0; r < rings; r++) for (int s = 0; s < sides; s++)
            {
                int a = r * (sides + 1) + s;
                d.Triangle(a, a + sides + 1, a + 1); d.Triangle(a + 1, a + sides + 1, a + sides + 2);
            }
            // Fold the rim inward and down to make a genuinely open tube.
            for (int s = 0; s < sides; s++)
            {
                float a = s * Mathf.PI * 2 / sides, b = (s + 1) * Mathf.PI * 2 / sides;
                Vector3 outerA = new Vector3(Mathf.Cos(a) * 0.28f, 1.25f, Mathf.Sin(a) * 0.28f);
                Vector3 outerB = new Vector3(Mathf.Cos(b) * 0.28f, 1.25f, Mathf.Sin(b) * 0.28f);
                Vector3 innerA = new Vector3(Mathf.Cos(a) * 0.2f, 1.23f, Mathf.Sin(a) * 0.2f);
                Vector3 innerB = new Vector3(Mathf.Cos(b) * 0.2f, 1.23f, Mathf.Sin(b) * 0.2f);
                d.Quad(outerA, innerA, innerB, outerB);
                d.Quad(innerA, new Vector3(innerA.x * 0.7f, 0.3f, innerA.z * 0.7f),
                    new Vector3(innerB.x * 0.7f, 0.3f, innerB.z * 0.7f), innerB);
            }
            return d.Build();
        }
        static Mesh Plate()
        {
            var d = new MeshDraft(); const int sides = 48;
            d.Tube(Vector3.zero, Vector3.up * 0.35f, 0.1f, 0.15f, 9);
            for (int s = 0; s < sides; s++)
            {
                float a = s * Mathf.PI * 2 / sides, b = (s + 1) * Mathf.PI * 2 / sides;
                Vector3 edgeA = new Vector3(Mathf.Cos(a), 0.36f + Mathf.Sin(a * 8) * 0.03f, Mathf.Sin(a)) * new Vector3(1, 1, 0.8f).magnitude / 1.62f;
                Vector3 edgeB = new Vector3(Mathf.Cos(b), 0.36f + Mathf.Sin(b * 8) * 0.03f, Mathf.Sin(b)) * new Vector3(1, 1, 0.8f).magnitude / 1.62f;
                d.Face(new Vector3(0, 0.45f, 0), edgeB, edgeA);
                d.Face(new Vector3(0, 0.28f, 0), edgeA, edgeB);
            }
            return d.Build();
        }
        static Mesh Seagrass()
        {
            var d = new MeshDraft();
            for (int blade = 0; blade < 9; blade++)
            {
                float angle = blade * 2.4f;
                Vector3 direction = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
                Vector3 side = Vector3.Cross(direction, Vector3.up) * 0.065f;
                float height = Random.Range(0.5f, 1.3f);
                for (int j = 0; j < 8; j++)
                {
                    float t = j / 8f, u = (j + 1) / 8f;
                    Vector3 a = Vector3.up * height * t + direction * (0.07f + t * t * 0.38f);
                    Vector3 b = Vector3.up * height * u + direction * (0.07f + u * u * 0.38f);
                    d.Quad(a - side * (1 - t), a + side * (1 - t), b + side * (1 - u), b - side * (1 - u));
                }
            }
            return d.Build();
        }
        static Mesh WaterQuad()
        {
            var d = new MeshDraft();
            d.Quad(new Vector3(-100, 0, -100), new Vector3(-100, 0, 100), new Vector3(100, 0, 100), new Vector3(100, 0, -100));
            return d.Build();
        }
        static Mesh LightShaft(float width)
        {
            var d = new MeshDraft();
            Vector3 top = new Vector3(-5, 15, 2), bottom = new Vector3(0, -1, 0);
            d.Quad(bottom - Vector3.right * width * 1.7f, bottom + Vector3.right * width * 1.7f,
                top + Vector3.right * width, top - Vector3.right * width);
            d.Quad(bottom - Vector3.forward * width * 1.7f, bottom + Vector3.forward * width * 1.7f,
                top + Vector3.forward * width, top - Vector3.forward * width);
            return d.Build();
        }
        static void CreatePlankton(Transform parent)
        {
            var o = new GameObject("Suspended marine particles");
            o.transform.SetParent(parent, false); o.transform.position = new Vector3(0, 7, 0);
            var ps = o.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main; main.loop = true; main.prewarm = true; main.startLifetime = 24;
            main.startSpeed = 0.025f; main.startSize = new ParticleSystem.MinMaxCurve(0.015f, 0.045f);
            main.startColor = new Color(0.65f, 0.88f, 0.78f, 0.4f); main.maxParticles = 850;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = ps.emission; emission.rateOverTime = 30;
            var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Box; shape.scale = new Vector3(70, 13, 70);
            var velocity = ps.velocityOverLifetime; velocity.enabled = true;
            velocity.x = 0.035f; velocity.y = 0.025f;
            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = MaterialAsset("Plankton", "Reefkeeper/Water Light", new Color(0.5f, 0.85f, 0.72f, 0.65f));
            renderer.shadowCastingMode = ShadowCastingMode.Off;
        }
        static void CreateFish(Transform parent)
        {
            var root = new GameObject("School of reef fish"); root.transform.SetParent(parent, false);
            var school = root.AddComponent<ReefLife>(); school.fish = new Transform[24];
            var d = new MeshDraft();
            Vector3 nose = new Vector3(0, 0, 0.3f), tail = new Vector3(0, 0, -0.24f);
            Vector3 top = new Vector3(0, 0.14f, 0), bottom = new Vector3(0, -0.12f, 0);
            Vector3 left = new Vector3(-0.07f, 0, 0), right = new Vector3(0.07f, 0, 0);
            d.Face(nose, top, left); d.Face(nose, left, bottom); d.Face(nose, bottom, right); d.Face(nose, right, top);
            d.Face(tail, left, top); d.Face(tail, bottom, left); d.Face(tail, right, bottom); d.Face(tail, top, right);
            Vector3 finA = new Vector3(0, 0.16f, -0.4f), finB = new Vector3(0, -0.16f, -0.4f);
            d.Face(tail, finA, finB); d.Face(tail, finB, finA);
            Mesh mesh = SaveMesh(d.Build(), "Fish");
            for (int i = 0; i < school.fish.Length; i++)
            {
                var fish = MeshObject("Reef fish", mesh, gold, root.transform);
                fish.transform.localScale = Vector3.one * Random.Range(0.65f, 1.2f);
                fish.transform.position = school.center + new Vector3(i % 6, i / 6f, 0);
                fish.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
                school.fish[i] = fish.transform;
            }
        }

        sealed class MeshDraft
        {
            readonly List<Vector3> vertices = new List<Vector3>();
            readonly List<Vector2> uv = new List<Vector2>();
            readonly List<int> triangles = new List<int>();
            public int Vertex(Vector3 p, Vector2 tex) { int i = vertices.Count; vertices.Add(p); uv.Add(tex); return i; }
            public void Triangle(int a, int b, int c) { triangles.Add(a); triangles.Add(b); triangles.Add(c); }
            public void Face(Vector3 a, Vector3 b, Vector3 c)
            { int i = Vertex(a, Vector2.zero); Vertex(b, Vector2.right); Vertex(c, Vector2.up); Triangle(i, i + 1, i + 2); }
            public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
            {
                int i = Vertex(a, Vector2.zero); Vertex(b, Vector2.right); Vertex(c, Vector2.one); Vertex(d, Vector2.up);
                Triangle(i, i + 1, i + 2); Triangle(i, i + 2, i + 3);
            }
            public void Tube(Vector3 a, Vector3 b, float r1, float r2, int sides)
            {
                Vector3 up = (b - a).normalized;
                Vector3 right = Vector3.Cross(up, Mathf.Abs(up.y) > 0.95f ? Vector3.forward : Vector3.up).normalized;
                Vector3 forward = Vector3.Cross(up, right);
                int offset = vertices.Count;
                for (int j = 0; j < 2; j++) for (int i = 0; i <= sides; i++)
                {
                    float t = i * Mathf.PI * 2 / sides;
                    Vertex((j == 0 ? a : b) + (right * Mathf.Cos(t) + forward * Mathf.Sin(t)) * (j == 0 ? r1 : r2), new Vector2(i / (float)sides, j));
                }
                for (int i = 0; i < sides; i++)
                {
                    int x = offset + i;
                    Triangle(x, x + 1, x + sides + 1); Triangle(x + 1, x + sides + 2, x + sides + 1);
                }
                int cap = Vertex(b, new Vector2(0.5f, 0.5f));
                for (int i = 0; i < sides; i++) Triangle(cap, offset + sides + 1 + i, offset + sides + 2 + i);
            }
            public Mesh Build()
            {
                var mesh = new Mesh();
                if (vertices.Count > 65535) mesh.indexFormat = IndexFormat.UInt32;
                mesh.SetVertices(vertices); mesh.SetUVs(0, uv); mesh.SetTriangles(triangles, 0);
                var colors = new Color[vertices.Count]; for (int i = 0; i < colors.Length; i++) colors[i] = Color.white;
                mesh.colors = colors;
                mesh.RecalculateNormals(); mesh.RecalculateBounds();
                return mesh;
            }
        }
    }
}
