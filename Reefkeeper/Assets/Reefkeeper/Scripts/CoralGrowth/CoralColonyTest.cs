using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Reefkeeper.Corals
{
    /// <summary>
    /// Test helper for <see cref="CoralColony"/>: in Play mode every coral under this object grows
    /// on its own timer, sped up by the neighbours it has fused with, so close pairs visibly
    /// overtake a lone coral. When all are grown it waits, then starts over.
    /// N grows all now, R restarts, P pokes all.
    /// </summary>
    public sealed class CoralColonyTest : MonoBehaviour
    {
        public CoralColony colony;
        [Min(0.2f)] public float secondsPerStage = 4f;
        [Tooltip("Pause with everything grown before starting over (s).")] public float holdSeconds = 5f;
        public bool showLabels = true;

        CoralGrowth[] corals;
        float[] progress;
        float hold;

        void Start()
        {
            corals = GetComponentsInChildren<CoralGrowth>(true);
            progress = new float[corals.Length];
            Restart();
        }

        void Restart()
        {
            for (int i = 0; i < corals.Length; i++) { corals[i].SetStage(0, false); progress[i] = 0f; }
            hold = 0f;
        }

        void Update()
        {
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb != null && kb.nKey.wasPressedThisFrame) foreach (var c in corals) c.Grow();
            if (kb != null && kb.rKey.wasPressedThisFrame) Restart();
            if (kb != null && kb.pKey.wasPressedThisFrame) foreach (var c in corals) c.Poke();
#endif
            bool allGrown = true;
            for (int i = 0; i < corals.Length; i++)
            {
                var c = corals[i];
                if (c.stage >= c.StageCount - 1) continue;
                allGrown = false;
                progress[i] += Time.deltaTime * (colony ? colony.RateFor(c) : 1f) / secondsPerStage;
                if (progress[i] >= 1f) { progress[i] = 0f; c.Grow(); }
            }
            if (allGrown && (hold += Time.deltaTime) >= holdSeconds) Restart();
        }

        void OnGUI()
        {
            if (!showLabels || corals == null) return;
            var cam = Camera.main;
            if (!cam) return;
            var style = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 13 };
            for (int i = 0; i < corals.Length; i++)
            {
                var c = corals[i];
                var p = cam.WorldToScreenPoint(c.transform.position + Vector3.up * 0.75f);
                if (p.z <= 0f) continue;
                float rate = colony ? colony.RateFor(c) : 1f;
                int n = colony ? colony.NeighbourCount(c) : 0;
                string text = $"{c.stage + 1}/{c.StageCount}  x{rate:0.00}  ({n})";
                GUI.Label(new Rect(p.x - 70, Screen.height - p.y - 10, 140, 20), text, style);
            }
        }
    }
}
