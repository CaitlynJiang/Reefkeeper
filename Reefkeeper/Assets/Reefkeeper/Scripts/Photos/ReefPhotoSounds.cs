using UnityEngine;

namespace Reefkeeper.Photos
{
    /// <summary>Small synthesized camera sounds, so the feature needs no audio assets. Swap for recordings any time.</summary>
    public static class ReefPhotoSounds
    {
        const int Rate = 44100;

        /// <summary>Two-blade mechanical shutter: a sharp click, a short whoosh, a second softer click.</summary>
        public static AudioClip Shutter()
        {
            var random = new System.Random(7);
            return Make("Photo shutter", .22f, (t, i) =>
            {
                float noise = (float)(random.NextDouble() * 2 - 1);
                float click1 = Mathf.Exp(-t * 900) * (noise * .8f + Mathf.Sin(t * 2 * Mathf.PI * 2400) * .5f);
                float t2 = t - .085f;
                float click2 = t2 > 0 ? Mathf.Exp(-t2 * 700) * (noise * .55f + Mathf.Sin(t2 * 2 * Mathf.PI * 1800) * .35f) : 0;
                float whoosh = Mathf.Exp(-Mathf.Pow((t - .045f) / .03f, 2)) * noise * .12f;
                return click1 + click2 + whoosh;
            });
        }

        /// <summary>Instant-camera print: a soft motor whirr that rises and fades.</summary>
        public static AudioClip Print()
        {
            var random = new System.Random(11);
            float lowpass = 0;
            return Make("Photo print", .75f, (t, i) =>
            {
                float envelope = Mathf.Clamp01(t / .06f) * Mathf.Clamp01((.75f - t) / .2f);
                float motor = Mathf.Sin(t * 2 * Mathf.PI * (150 + t * 40)) * .35f + Mathf.Sin(t * 2 * Mathf.PI * 300) * .12f;
                lowpass = Mathf.Lerp(lowpass, (float)(random.NextDouble() * 2 - 1), .12f);
                return envelope * (motor * (.8f + .2f * Mathf.Sin(t * 2 * Mathf.PI * 22)) + lowpass * .5f) * .5f;
            });
        }

        /// <summary>Soft two-note chime when the viewfinder opens.</summary>
        public static AudioClip Open() => Make("Photo mode open", .28f, (t, i) =>
        {
            float a = Mathf.Sin(t * 2 * Mathf.PI * 880) * Mathf.Exp(-t * 18);
            float t2 = t - .09f;
            float b = t2 > 0 ? Mathf.Sin(t2 * 2 * Mathf.PI * 1320) * Mathf.Exp(-t2 * 16) : 0;
            return (a + b) * .35f;
        });

        static AudioClip Make(string name, float seconds, System.Func<float, int, float> sample)
        {
            int n = Mathf.CeilToInt(seconds * Rate);
            var data = new float[n];
            for (int i = 0; i < n; i++) data[i] = Mathf.Clamp(sample(i / (float)Rate, i), -1, 1);
            var clip = AudioClip.Create(name, n, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
