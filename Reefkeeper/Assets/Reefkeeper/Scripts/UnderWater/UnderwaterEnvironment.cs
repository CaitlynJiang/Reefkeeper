using UnityEngine;

namespace Reefkeeper.UnderWater
{
    /// <summary>
    /// One place to tune the underwater look. Pushes values to global shader
    /// properties read by UnderwaterCommon.hlsl. Works in edit mode for live tweaking.
    /// Right-click the component header for look presets.
    /// </summary>
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class UnderwaterEnvironment : MonoBehaviour
    {
        [Header("Water colour 水色")]
        public Color shallowColor = new Color(0.60f, 0.93f, 0.86f);
        public Color deepColor = new Color(0.16f, 0.45f, 0.56f);

        [Header("Depth range 深度范围 (world Y)")]
        public float surfaceY = 15f;
        public float floorY = -10f;

        [Header("Fog 雾")]
        [Range(0f, 0.2f)] public float fogDensity = 0.03f;
        [Min(0f)] public float fogStart = 3f;
        [Tooltip("Light lost per metre for R, G, B. Red is absorbed fastest in real water.\n每米吸收的 RGB，红色衰减最快")]
        public Vector3 absorptionPerMeter = new Vector3(0.03f, 0.012f, 0.008f);

        [Header("Sun 阳光")]
        public Color sunGlowColor = new Color(1f, 0.97f, 0.85f);
        [Range(1f, 64f)] public float sunGlowTightness = 5f;
        [Range(0f, 2f)] public float sunGlowStrength = 0.6f;
        [Tooltip("How fast sunlight fades below the surface.\n阳光随深度衰减的速度")]
        [Range(0f, 0.2f)] public float depthLightFalloff = 0.025f;

        [Header("Ambient 环境光")]
        [Range(0f, 2f)] public float ambientStrength = 1.1f;
        [Tooltip("Ambient colour in shadow. Cool blue-violet keeps shadows colourful.\n阴影里的环境光颜色，偏冷的蓝紫让暗部有颜色")]
        public Color shadowTint = new Color(0.62f, 0.68f, 1f);

        [Header("Caustics 焦散")]
        public Texture2D causticsTexture;
        public Color causticsColor = new Color(1f, 1f, 0.9f);
        [Range(0f, 4f)] public float causticsStrength = 1.6f;
        [Tooltip("Pattern repeats per metre. Smaller = bigger light cells.\n每米重复次数，越小光斑越大")]
        [Range(0.02f, 1f)] public float causticsScale = 0.22f;
        [Range(0f, 0.2f)] public float causticsSpeed = 0.03f;
        [Tooltip("Rainbow fringe on the light lines.\n光线边缘的彩色色散")]
        [Range(0f, 0.02f)] public float causticsColorSplit = 0.004f;
        [Tooltip("How much the pattern blurs per metre below the surface.\n每深一米的模糊程度")]
        [Range(0f, 0.3f)] public float causticsDepthBlur = 0.08f;

        static readonly int ShallowId = Shader.PropertyToID("_UW_ShallowColor");
        static readonly int DeepId = Shader.PropertyToID("_UW_DeepColor");
        static readonly int AbsorptionId = Shader.PropertyToID("_UW_Absorption");
        static readonly int SunGlowId = Shader.PropertyToID("_UW_SunGlow");
        static readonly int SunGlowStrengthId = Shader.PropertyToID("_UW_SunGlowStrength");
        static readonly int FogDensityId = Shader.PropertyToID("_UW_FogDensity");
        static readonly int FogStartId = Shader.PropertyToID("_UW_FogStart");
        static readonly int SurfaceId = Shader.PropertyToID("_UW_SurfaceY");
        static readonly int FloorId = Shader.PropertyToID("_UW_FloorY");
        static readonly int DepthFalloffId = Shader.PropertyToID("_UW_DepthLightFalloff");
        static readonly int AmbientId = Shader.PropertyToID("_UW_AmbientStrength");
        static readonly int ShadowTintId = Shader.PropertyToID("_UW_ShadowTint");
        static readonly int CausticsTexId = Shader.PropertyToID("_UW_CausticsTex");
        static readonly int CausticsColorId = Shader.PropertyToID("_UW_CausticsColor");
        static readonly int CausticsId = Shader.PropertyToID("_UW_Caustics");

        void OnEnable() => Apply();
        void OnValidate() => Apply();
        void Update() => Apply();

        [ContextMenu("Preset: Stylized bright 风格化明亮")]
        public void PresetStylized() => SetPreset(
            new Color(0.60f, 0.93f, 0.86f), new Color(0.16f, 0.45f, 0.56f), 0.03f, 3f,
            new Vector3(0.03f, 0.012f, 0.008f), new Color(1f, 0.97f, 0.85f), 5f, 0.6f, 0.025f,
            1.1f, new Color(0.62f, 0.68f, 1f), new Color(1f, 1f, 0.9f), 1.6f);

        [ContextMenu("Preset: Realistic deep 写实深水")]
        public void PresetRealistic() => SetPreset(
            new Color(0.36f, 0.80f, 0.82f), new Color(0.02f, 0.17f, 0.30f), 0.035f, 2f,
            new Vector3(0.06f, 0.02f, 0.012f), new Color(0.75f, 1f, 0.95f), 6f, 0.45f, 0.04f,
            0.9f, Color.white, new Color(0.85f, 1f, 0.95f), 1.4f);

        void SetPreset(Color shallow, Color deep, float density, float start, Vector3 absorption,
            Color glow, float glowTightness, float glowStrength, float depthFalloff,
            float ambient, Color shadow, Color caustic, float causticStrength)
        {
#if UNITY_EDITOR
            UnityEditor.Undo.RecordObject(this, "Underwater preset");
#endif
            shallowColor = shallow; deepColor = deep; fogDensity = density; fogStart = start;
            absorptionPerMeter = absorption; sunGlowColor = glow; sunGlowTightness = glowTightness;
            sunGlowStrength = glowStrength; depthLightFalloff = depthFalloff; ambientStrength = ambient;
            shadowTint = shadow; causticsColor = caustic; causticsStrength = causticStrength;
            Apply();
        }

        public void Apply()
        {
            // Colours picked in the inspector are sRGB; shaders work in linear.
            Shader.SetGlobalVector(ShallowId, shallowColor.linear);
            Shader.SetGlobalVector(DeepId, deepColor.linear);
            Shader.SetGlobalVector(AbsorptionId, absorptionPerMeter);
            Color glow = sunGlowColor.linear;
            Shader.SetGlobalVector(SunGlowId, new Vector4(glow.r, glow.g, glow.b, sunGlowTightness));
            Shader.SetGlobalFloat(SunGlowStrengthId, sunGlowStrength);
            Shader.SetGlobalFloat(FogDensityId, fogDensity);
            Shader.SetGlobalFloat(FogStartId, fogStart);
            Shader.SetGlobalFloat(SurfaceId, surfaceY);
            Shader.SetGlobalFloat(FloorId, floorY);
            Shader.SetGlobalFloat(DepthFalloffId, depthLightFalloff);
            Shader.SetGlobalFloat(AmbientId, ambientStrength);
            Shader.SetGlobalVector(ShadowTintId, shadowTint.linear);

            Shader.SetGlobalTexture(CausticsTexId, causticsTexture ? causticsTexture : Texture2D.blackTexture);
            Color caustic = causticsColor.linear;
            Shader.SetGlobalVector(CausticsColorId, new Vector4(caustic.r, caustic.g, caustic.b, causticsStrength));
            Shader.SetGlobalVector(CausticsId, new Vector4(causticsScale, causticsSpeed, causticsColorSplit, causticsDepthBlur));
        }
    }
}
