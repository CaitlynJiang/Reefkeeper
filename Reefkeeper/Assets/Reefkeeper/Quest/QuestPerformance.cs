using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.XR;

namespace Reefkeeper.Quest
{
    /// <summary>
    /// quest3 branch only: optional fixed foveated rendering (off; it doubled GPU time on device)
    /// and a live adb A/B probe for measuring GPU costs without rebuilding.
    /// </summary>
    public sealed class QuestPerformance : MonoBehaviour
    {
        // 0 = off … 1 = high. Off: on device it doubled GPU frame time instead of saving it.
        const float FoveationLevel = 0f;

        readonly List<XRDisplaySubsystem> displays = new List<XRDisplaySubsystem>();
        bool foveationSet;
        Camera tuned;

#if UNITY_ANDROID && !UNITY_EDITOR
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            var go = new GameObject("Quest performance");
            DontDestroyOnLoad(go);
            go.AddComponent<QuestPerformance>();
        }
#endif

        void Update()
        {
            if (!foveationSet && FoveationLevel > 0f)
            {
                SubsystemManager.GetSubsystems(displays);
                foreach (var display in displays)
                {
                    if (!display.running) continue;
                    display.foveatedRenderingLevel = FoveationLevel;
                    display.foveatedRenderingFlags = XRDisplaySubsystem.FoveatedRenderingFlags.None;
                    foveationSet = true;
                    Debug.Log("[Quest] Foveated rendering level " + FoveationLevel);
                }
            }

            // Post-processing and the fog-limited far plane are shared with PICO: XRHeadsetPerformance (v3).
            PollProbe();
        }

        // ---- Live A/B probe -------------------------------------------------------------------
        // adb push a perf.txt into Application.persistentDataPath, e.g.
        //   scale=0.7  msaa=1  shadow=0  far=40  hide=Lit Sway,Outline
        // Missing keys fall back to the shipped values. Lets one session measure each cost
        // on device without rebuilding.
        float nextPoll, defaultFar = -1, defaultShadow = -1;
        int defaultMsaa = -1;
        string lastProbe = "";
        Renderer[] allRenderers;

        void PollProbe()
        {
            if (Time.unscaledTime < nextPoll) return;
            nextPoll = Time.unscaledTime + 1f;
            if (!tuned) { tuned = Camera.main; defaultFar = tuned ? tuned.farClipPlane : -1; }
            string path = System.IO.Path.Combine(Application.persistentDataPath, "perf.txt");
            string text = System.IO.File.Exists(path) ? System.IO.File.ReadAllText(path).Trim() : "";
            if (text == lastProbe) return;
            lastProbe = text;

            var values = new Dictionary<string, string>();
            foreach (var line in text.Split('\n'))
            {
                int eq = line.IndexOf('=');
                if (eq > 0) values[line.Substring(0, eq).Trim()] = line.Substring(eq + 1).Trim();
            }
            var urp = UniversalRenderPipeline.asset;
            if (urp && defaultMsaa < 0) { defaultMsaa = urp.msaaSampleCount; defaultShadow = urp.shadowDistance; }

            XRSettings.eyeTextureResolutionScale = values.TryGetValue("scale", out var s) ? float.Parse(s) : 1f;
            if (urp)
            {
                urp.msaaSampleCount = values.TryGetValue("msaa", out var m) ? int.Parse(m) : defaultMsaa;
                urp.shadowDistance = values.TryGetValue("shadow", out var sh) ? float.Parse(sh) : defaultShadow;
            }
            if (tuned && defaultFar > 0)
                tuned.farClipPlane = values.TryGetValue("far", out var f) ? float.Parse(f) : defaultFar;

            allRenderers ??= FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            string[] hide = values.TryGetValue("hide", out var h) && h.Length > 0 ? h.Split(',') : new string[0];
            int hidden = 0;
            foreach (var r in allRenderers)
            {
                if (!r) continue;
                bool off = false;
                foreach (var mat in r.sharedMaterials)
                    if (mat && mat.shader)
                        foreach (var key in hide)
                            if (key.Trim().Length > 0 && mat.shader.name.Contains(key.Trim())) off = true;
                r.forceRenderingOff = off;
                if (off) hidden++;
            }
            Debug.Log($"[QuestProbe] scale={XRSettings.eyeTextureResolutionScale} msaa={(urp ? urp.msaaSampleCount : 0)} shadow={(urp ? urp.shadowDistance : 0)} far={(tuned ? tuned.farClipPlane : 0)} hidden={hidden} ({text.Replace('\n', ' ')})");
        }
    }
}
