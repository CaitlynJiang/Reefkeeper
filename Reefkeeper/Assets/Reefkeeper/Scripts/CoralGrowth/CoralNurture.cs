using UnityEngine;

namespace Reefkeeper.Corals
{
    /// <summary>
    /// A planted coral's care state. Each stage fills over <see cref="secondsPerStage"/>; moss on
    /// the socket pauses it. When a stage is full the coral waits for fertilizer, which plays the
    /// growth animation and starts the next stage. Moss creeps back now and then while growing.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CoralNurture : MonoBehaviour
    {
        public RackSocket socket;
        public CoralGrowth coral;
        public string species;
        [Min(1)] public float secondsPerStage = 30f;
        [Tooltip("Moss returns after a random time in this range (s) while the coral is growing.")]
        public Vector2 mossReturnSeconds = new Vector2(25f, 50f);
        [Tooltip("Growth speed multiplier; set by CoralColony from the coral's neighbours.")]
        public float growthRate = 1f;
        [Min(0f), Tooltip("Seconds growth rests after an empty-hand touch.")]
        public float touchPauseSeconds = 3f;

        /// <summary>Progress of the current stage, 0 to 1.</summary>
        public float Progress { get; private set; }
        /// <summary>Current stage, 0-based (0 to <see cref="CoralRules.StageCount"/> - 1).</summary>
        public int Stage => coral ? coral.stage : 0;
        public bool FinalStage => Stage >= CoralRules.StageCount - 1;
        public bool Paused => socket && socket.Mossy;
        public float TouchPauseRemaining { get; private set; }
        public bool TouchPaused => TouchPauseRemaining > 0f;
        public bool GrowthPaused => Paused || TouchPaused;
        public bool Ready => !TouchPaused && !FinalStage && Progress >= 1f;
        public bool FullyGrown => FinalStage && Progress >= 1f;
        public string DisplayName => CoralRules.DisplayName(species);

        /// <summary>Raised after fertilizer moves the coral to its next stage.</summary>
        public event System.Action<CoralNurture> Grew;

        float mossTimer;

        public void Begin(RackSocket at, CoralGrowth growth, string id)
        {
            socket = at; coral = growth; species = id;
            coral.SetStage(0, false);
            Progress = 0f;
            TouchPauseRemaining = 0f;
            ResetMossTimer();
            var touch = GetComponent<CoralTouchReaction>();
            if (!touch) touch = gameObject.AddComponent<CoralTouchReaction>();
            touch.Bind(this);
        }

        /// <summary>Visual recoil and a brief rest; repeated touches reset, never stack, the timer.</summary>
        public void ReactToTouch()
        {
            if (!isActiveAndEnabled || !coral) return;
            coral.Poke();
            // A coral waiting for fertilizer has nothing left to rest from, and fertilizing by hand means reaching
            // into it: a rest here cancelled every attempt (headset). Just the recoil.
            if (!FinalStage && Progress >= 1f) return;
            TouchPauseRemaining = Mathf.Max(0f, touchPauseSeconds);
            var emote = GetComponent<CoralCareEmote>();
            if (!emote)
            {
                emote = gameObject.AddComponent<CoralCareEmote>();
                emote.Bind(this, false);
            }
        }

        void Update() => AdvanceCare(Time.deltaTime);

        void AdvanceCare(float dt)
        {
            dt = Mathf.Max(0f, dt);
            float rested = Mathf.Min(dt, TouchPauseRemaining);
            TouchPauseRemaining = Mathf.Max(0f, TouchPauseRemaining - dt);
            dt -= rested;
            if (!coral || Paused || dt <= 0f) return;
            if (Progress < 1f)
            {
                Progress = Mathf.Min(1f, Progress + dt * growthRate / secondsPerStage);
                // Moss only creeps back while the coral is actively growing.
                mossTimer -= dt;
                // Only algae returns on this short care timer. Cleared fire coral stays cleared.
                if (mossTimer <= 0f && socket && socket.moss && socket.moss.surfaceKind == ReefSurfaceKind.Algae)
                { socket.moss.ResetDirt(); ResetMossTimer(); }
            }
        }

        /// <summary>Plays the growth animation and starts the next stage. Returns false if not ready.</summary>
        public bool Fertilize()
        {
            if (!Ready || Paused) return false;
            coral.Grow();
            Progress = 0f;
            ResetMossTimer();
            Grew?.Invoke(this);
            return true;
        }

        void ResetMossTimer() => mossTimer = Random.Range(mossReturnSeconds.x, mossReturnSeconds.y);
    }
}
