using UnityEngine;

namespace Reefkeeper
{
    [DefaultExecutionOrder(210)]
    public sealed class ReefBrushAudio : MonoBehaviour
    {
        public ReefBrushPickup pickup;
        public AudioClip scrubClip;
        [Range(0, 1)] public float volume = 0.35f;
        [Range(10, 22000)] public float lowPassCutoff = 22000f;
        public AudioSource Source { get; private set; }
        ReefBrush tool;
        float activeUntil = -1;

        void Awake()
        {
            tool = GetComponent<ReefBrush>();
            Source = gameObject.AddComponent<AudioSource>();
            Source.clip = scrubClip; Source.loop = true; Source.playOnAwake = false;
            Source.volume = volume; Source.priority = 64; Source.spatialBlend = 0.6f;
            Source.rolloffMode = AudioRolloffMode.Linear; Source.minDistance = 0.3f;
            Source.maxDistance = 3; Source.dopplerLevel = 0;
            if (lowPassCutoff < 22000f)
                gameObject.AddComponent<AudioLowPassFilter>().cutoffFrequency = lowPassCutoff;
        }
        void LateUpdate()
        {
            var brush = tool;
            if (brush.Scrubbing) activeUntil = Time.unscaledTime + 0.08f;
            // Bridge tiny gaps at a stroke's turning point, but stop on release, lost contact or completion.
            bool active = !ReefToolMenu.BlocksWorldInput && brush.Held && brush.InContact && Time.unscaledTime < activeUntil
                && brush.ContactPatch && !brush.ContactPatch.Complete && (!pickup.AssistedMotion || pickup.IsScrubbing);
            Source.volume = volume;
            if (active && scrubClip) { if (!Source.isPlaying) Source.Play(); }
            else if (Source.isPlaying) Source.Stop();
        }
        void OnDisable() { activeUntil = -1; if (Source) Source.Stop(); }
    }
}
