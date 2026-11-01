using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Reefkeeper.Corals
{
    /// <summary>
    /// The lucky-cat shopkeeper. Its moods are the animation clips keyed in Blender
    /// (Blender/lucky_cat_anim.py); each one is a state of the same name in the Animator.
    /// Call Play(Mood.Happy) etc. from game code. Surprised plays once and returns to Idle.
    /// In Play mode, keys 1-7 preview the moods.
    /// </summary>
    public sealed class LuckyCat : MonoBehaviour
    {
        public enum Mood { Idle, Happy, Beckon, Surprised, Sleepy, Angry, Sad }

        public Mood startMood = Mood.Idle;
        [Tooltip("Blend time between moods (s).")] public float crossFade = 0.25f;
        public bool previewKeys = true;

        public Mood Current { get; private set; }
        Animator animator;

        void Awake() => animator = GetComponentInChildren<Animator>();

        void Start() => Play(startMood);

        public void Play(Mood mood)
        {
            Current = mood;
            if (animator) animator.CrossFadeInFixedTime(mood.ToString(), crossFade);
        }

        void Update()
        {
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (!previewKeys || kb == null) return;
            if (kb.digit1Key.wasPressedThisFrame) Play(Mood.Idle);
            if (kb.digit2Key.wasPressedThisFrame) Play(Mood.Happy);
            if (kb.digit3Key.wasPressedThisFrame) Play(Mood.Beckon);
            if (kb.digit4Key.wasPressedThisFrame) Play(Mood.Surprised);
            if (kb.digit5Key.wasPressedThisFrame) Play(Mood.Sleepy);
            if (kb.digit6Key.wasPressedThisFrame) Play(Mood.Angry);
            if (kb.digit7Key.wasPressedThisFrame) Play(Mood.Sad);
#endif
        }
    }
}
