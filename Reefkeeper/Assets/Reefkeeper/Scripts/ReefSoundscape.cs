using System.Collections;
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
        Coroutine musicTransition;
        float albumGain = 1f;
        float PlaybackVolume => Mathf.Clamp01(musicVolume * albumGain);
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
            if (MusicSource&&musicTransition==null&&!MusicSource.mute) MusicSource.volume = PlaybackVolume;
        }
        public void PlayMusic(AudioClip clip,float fadeSeconds,float gain=1f)
        {
            if(!MusicSource||!clip)return;
            albumGain=Mathf.Clamp(gain,0,4);
            if(musicTransition!=null)StopCoroutine(musicTransition);
            musicTransition=StartCoroutine(ChangeMusic(clip,Mathf.Max(0,fadeSeconds)));
        }
        public void StopMusic(float fadeSeconds)
        {
            if(!MusicSource)return;
            if(musicTransition!=null)StopCoroutine(musicTransition);
            musicTransition=StartCoroutine(FadeOut(Mathf.Max(0,fadeSeconds)));
        }
        IEnumerator ChangeMusic(AudioClip clip,float seconds)
        {
            if(MusicSource.isPlaying&&!MusicSource.mute&&MusicSource.clip!=clip)yield return FadeVolume(0,seconds);
            if(!MusicSource.isPlaying||MusicSource.mute||MusicSource.clip!=clip)
            {
                MusicSource.Stop();MusicSource.clip=clip;MusicSource.volume=0;MusicSource.mute=false;MusicSource.Play();
            }
            yield return FadeVolume(PlaybackVolume,seconds);musicTransition=null;
        }
        IEnumerator FadeOut(float seconds)
        {
            yield return FadeVolume(0,seconds);
            MusicSource.Stop();MusicSource.mute=true;musicTransition=null;
        }
        IEnumerator FadeVolume(float target,float seconds)
        {
            float start=MusicSource.mute?0:MusicSource.volume;
            double began=Time.realtimeSinceStartupAsDouble;
            while(seconds>0)
            {
                float elapsed=(float)(Time.realtimeSinceStartupAsDouble-began);
                if(elapsed>=seconds)break;
                MusicSource.volume=Mathf.Lerp(start,target,Mathf.SmoothStep(0,1,elapsed/seconds));
                yield return null;
            }
            MusicSource.volume=target;
        }
        void OnDisable()
        {
            if(musicTransition!=null)StopCoroutine(musicTransition);musicTransition=null;
            if(MusicSource){MusicSource.Stop();MusicSource.mute=true;MusicSource.volume=0;}
        }
    }
}
