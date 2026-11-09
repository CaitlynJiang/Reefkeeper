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
        }
#endif

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
