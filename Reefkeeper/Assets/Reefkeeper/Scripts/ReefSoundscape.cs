using UnityEngine;

namespace Reefkeeper
{
    public sealed class ReefSoundscape : MonoBehaviour
    {
        public AudioClip ambience, music;
        [Range(0, 1)] public float ambienceVolume = 0.75f;
        [Range(0, 1)] public float musicVolume = 0.15f;
        public AudioSource AmbienceSource { get; private set; }
        public AudioSource MusicSource { get; private set; }
        void Start()
        {
            AmbienceSource = Loop("Underwater ambience", ambience, ambienceVolume, 32);
            MusicSource = Loop("Quiet background music", music, musicVolume, 128, muted: true);
        }
        AudioSource Loop(string name, AudioClip clip, float volume, int priority, bool muted = false)
        {
            var child = new GameObject(name); child.transform.SetParent(transform, false);
            var source = child.AddComponent<AudioSource>(); source.clip = clip;
            source.loop = true; source.playOnAwake = false; source.spatialBlend = 0;
            source.volume = volume; source.priority = priority;
            source.mute = muted; // Set before Play so startup cannot emit a brief burst of music.
            if (clip) source.Play(); return source;
        }
        void OnValidate()
        {
            if (AmbienceSource) AmbienceSource.volume = ambienceVolume;
            if (MusicSource) MusicSource.volume = musicVolume;
        }
    }
}
