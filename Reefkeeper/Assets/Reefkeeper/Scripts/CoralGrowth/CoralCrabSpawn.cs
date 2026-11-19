using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Reefkeeper.Corals
{
    /// <summary>
    /// On the red coral: when it reaches its last stage there is a <see cref="chance"/> that a coral
    /// crab was sleeping under it all along. The mature coral's "rock" is the crab's shell, so the
    /// coral is swapped for the crab in the same pose and the crab wakes up and walks away with it.
    /// </summary>
    [RequireComponent(typeof(CoralGrowth))]
    public sealed class CoralCrabSpawn : MonoBehaviour
    {
        [Range(0f, 1f)] public float chance = 0.1f;
        public CoralCrab crabPrefab;
        [Tooltip("Seconds after reaching the last stage before the swap (lets the growth pop finish).")]
        public float delay = 1.2f;
        [Tooltip("Test: press C to make every mature red coral turn into a crab.")]
        public bool testKey = true;

        /// <summary>Raised when a crab appears, with the crab.</summary>
        public event System.Action<CoralCrab> CrabSpawned;

        CoralGrowth coral;
        int lastStage = -1;
        float timer = -1f;

        void Awake() => coral = GetComponent<CoralGrowth>();

        bool Mature => coral.stage >= coral.StageCount - 1;

        void Update()
        {
            // a stage change onto the last stage rolls the dice once
            if (coral.stage != lastStage)
            {
                if (lastStage >= 0 && Mature && Current && Random.value < chance) timer = delay;
                lastStage = coral.stage;
            }
            if (timer >= 0f && (timer -= Time.deltaTime) < 0f) Spawn();
#if ENABLE_INPUT_SYSTEM
            if (testKey && Keyboard.current != null && Keyboard.current.cKey.wasPressedThisFrame && Mature && Current) Spawn();
#endif
        }

        GameObject Current => coral.Current && coral.Current.gameObject.activeSelf ? coral.Current.gameObject : null;

        /// <summary>Swap the mature coral for a crab now.</summary>
        public CoralCrab Spawn()
        {
            timer = -1f;
            if (!crabPrefab || !Mature || !Current) return null;
            var crab = Instantiate(crabPrefab, transform.position, transform.rotation);
            crab.transform.localScale = transform.lossyScale;
            crab.name = crabPrefab.name;

            // The crab now carries the coral away. A planted coral leaves its socket free;
            // a test coral just hides its last stage until it grows again.
            var nurture = GetComponent<CoralNurture>();
            if (nurture)
            {
                if (nurture.socket && nurture.socket.occupant == coral) nurture.socket.occupant = null;
                Destroy(gameObject);
            }
            else Current.SetActive(false);
            CrabSpawned?.Invoke(crab);
            return crab;
        }
    }
}
