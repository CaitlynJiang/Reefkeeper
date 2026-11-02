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

        /// <summary>Progress of the current stage, 0 to 1.</summary>
        public float Progress { get; private set; }
        /// <summary>Current stage, 0-based (0 to <see cref="CoralRules.StageCount"/> - 1).</summary>
        public int Stage => coral ? coral.stage : 0;
        public bool FinalStage => Stage >= CoralRules.StageCount - 1;
        public bool Paused => socket && socket.Mossy;
        public bool Ready => !FinalStage && Progress >= 1f;
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
            ResetMossTimer();
        }

        void Update()
        {
            if (!coral || Paused) return;
            if (Progress < 1f)
            {
                Progress = Mathf.Min(1f, Progress + Time.deltaTime / secondsPerStage);
                // Moss only creeps back while the coral is actively growing.
                mossTimer -= Time.deltaTime;
                if (mossTimer <= 0f && socket && socket.moss) { socket.moss.ResetDirt(); ResetMossTimer(); }
            }
        }

        /// <summary>Plays the growth animation and starts the next stage. Returns false if not ready.</summary>
        public bool Fertilize()
        {
            if (!Ready) return false;
            coral.Grow();
            Progress = 0f;
            ResetMossTimer();
            Grew?.Invoke(this);
            return true;
        }

        void ResetMossTimer() => mossTimer = Random.Range(mossReturnSeconds.x, mossReturnSeconds.y);
    }
}
