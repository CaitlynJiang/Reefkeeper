using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Reefkeeper
{
    /// <summary>
    /// Standalone-headset GPU savings (PICO and Quest) that need no scene edits. Measured on
    /// Quest 3, which shares PICO 4 Ultra's GPU class. Runs only in Android player builds;
    /// the editor and desktop builds keep post-processing and the full far plane.
    /// </summary>
    public sealed class XRHeadsetPerformance : MonoBehaviour
    {
        /// <summary>Underwater fog amount past which nothing is distinguishable from the water colour.</summary>
        const float HiddenFog = 0.96f;

        Camera tuned;

#if UNITY_ANDROID && !UNITY_EDITOR
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            var go = new GameObject("XR headset performance");
            DontDestroyOnLoad(go);
            go.AddComponent<XRHeadsetPerformance>();
            ReplaceProceduralCaustics();
        }
#endif

        /// <summary>
        /// The vendored WaterUnder graph (sand, rock and coral) computes caustics per pixel from four
        /// Voronoi noises; on Quest 3 it alone cost ~17 ms of a 39 ms frame. In headset players its
        /// materials switch to Reefkeeper's UnderWater/Lit, whose caustics come from a texture.
        /// Shared material assets are edited in memory only (player builds never save them), so
        /// corals the cart spawns later get the cheap shader too. Returns the number of materials.
        /// </summary>
        public static int ReplaceProceduralCaustics()
        {
            var lite = Shader.Find("Reefkeeper/UnderWater/Lit");
            if (!lite) return 0;
            int count = 0;
            foreach (var m in Resources.FindObjectsOfTypeAll<Material>())
            {
                if (!m || !m.shader || m.shader.name != "Shader Graphs/WaterUnder") continue;
                // WaterUnder's exposed properties keep their generated reference names.
                var color = m.GetColor("Color_DB714CC1");
                var tiling = m.GetVector("Vector4_47405F4E");
                float caustics = m.HasProperty("_Caustics") ? m.GetFloat("_Caustics") : 1f;
                Texture texture = null;
                foreach (var name in m.GetTexturePropertyNames()) { var t = m.GetTexture(name); if (t) { texture = t; break; } }
                m.shader = lite;
                m.SetColor("_BaseColor", color);
                m.SetFloat("_CausticsAmount", caustics);
                m.SetFloat("_Wrap", 0.5f);
                if (texture) { m.SetTexture("_BaseMap", texture); m.SetTextureScale("_BaseMap", new Vector2(tiling.x, tiling.y)); }
                count++;
            }
            Debug.Log($"[XRPerf] WaterUnder -> UnderWater/Lit on {count} materials");
            return count;
        }

        void Update()
        {
            var camera = Camera.main;
            if (!camera || camera == tuned) return;
            tuned = camera;
            // Post-processing costs an intermediate texture and a full-screen pass per eye.
            var data = camera.GetUniversalAdditionalCameraData();
            if (data) data.renderPostProcessing = false;
            // The underwater shaders fade to the water colour with 1 - exp(-(d - start) * density)
            // (UnderwaterCommon.hlsl). Stop the far plane where that reaches HiddenFog.
            float density = Shader.GetGlobalFloat("_UW_FogDensity"), start = Shader.GetGlobalFloat("_UW_FogStart");
            if (density > 0.001f)
                camera.farClipPlane = Mathf.Min(camera.farClipPlane, start - Mathf.Log(1f - HiddenFog) / density);
            Debug.Log($"[XRPerf] Camera '{camera.name}': post-processing off, far clip {camera.farClipPlane:F0} m");
        }
    }
}
