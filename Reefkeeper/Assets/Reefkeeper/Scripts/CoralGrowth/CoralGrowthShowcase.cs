using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Reefkeeper.Corals
{
    /// <summary>
    /// Test helper: in Play mode, grows every coral under this object one stage at a time,
    /// staggered so the pops ripple across the row. N grows all now, P pokes all.
    /// </summary>
    public sealed class CoralGrowthShowcase : MonoBehaviour
    {
        public bool autoGrow = true;
        [Min(0.2f)] public float secondsPerStage = 2.5f;
        [Tooltip("Delay between neighbouring corals, for a ripple.")] public float stagger = 0.12f;

        CoralGrowth[] corals;
        float timer;

        void Start()
        {
            corals = GetComponentsInChildren<CoralGrowth>(true);
            foreach (var c in corals) c.SetStage(0, false);
        }

        void Update()
        {
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb != null && kb.nKey.wasPressedThisFrame) { GrowAll(); timer = 0; }
            if (kb != null && kb.pKey.wasPressedThisFrame) foreach (var c in corals) c.Poke();
#endif
            if (!autoGrow) return;
            timer += Time.deltaTime;
            if (timer >= secondsPerStage) { timer = 0; GrowAll(); }
        }

        void GrowAll()
        {
            for (int i = 0; i < corals.Length; i++) StartCoroutine(GrowLater(corals[i], i * stagger));
        }

        System.Collections.IEnumerator GrowLater(CoralGrowth coral, float delay)
        {
            if (delay > 0) yield return new WaitForSeconds(delay);
            coral.Grow(true);
        }
    }
}
