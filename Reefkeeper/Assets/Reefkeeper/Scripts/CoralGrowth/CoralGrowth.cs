using UnityEngine;

namespace Reefkeeper.Corals
{
    /// <summary>
    /// One coral with a few growth stages (child objects, one shown at a time).
    /// Stage changes and pokes are animated with a springy squash and stretch on the
    /// stage transform; an idle sway keeps it alive. No custom shader needed.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CoralGrowth : MonoBehaviour
    {
        [Tooltip("Stage objects, youngest first. Only the current one is active.")]
        public Transform[] stages = new Transform[0];

        [Min(0)] public int stage;

        [Header("Spring 弹性")]
        [Tooltip("Higher = snappier.")] public float stiffness = 170f;
        [Tooltip("Lower = more wobble.")] public float damping = 9f;
        [Tooltip("Squash a new stage starts from (x/z widen, y shortens).")]
        public Vector3 popFrom = new Vector3(1.18f, 0.55f, 1.18f);
        [Tooltip("How hard a poke squashes the coral.")] public float pokeStrength = 0.22f;

        [Header("Idle sway 摇摆")]
        public float swayDegrees = 1.6f;
        public float swaySpeed = 0.7f;

        Vector3 scale = Vector3.one, velocity;
        float phase;

        public int StageCount => stages.Length;
        public Transform Current => stage >= 0 && stage < stages.Length ? stages[stage] : null;

        void Awake()
        {
            // Stable per-instance phase so neighbours don't sway in lockstep.
            phase = Mathf.Repeat(transform.position.x * 1.7f + transform.position.z * 2.3f, Mathf.PI * 2f);
            ShowOnly(Mathf.Clamp(stage, 0, Mathf.Max(0, stages.Length - 1)));
        }

        /// <summary>Show a stage. With animate, the new stage pops in with a springy squash.</summary>
        public void SetStage(int index, bool animate = true)
        {
            if (stages.Length == 0) return;
            index = Mathf.Clamp(index, 0, stages.Length - 1);
            bool changed = index != stage;
            ShowOnly(index);
            if (!animate) { scale = Vector3.one; velocity = Vector3.zero; Apply(0f); return; }
            if (changed) { scale = popFrom; velocity = new Vector3(-0.6f, 2.5f, -0.6f); }
            else Poke();
        }

        /// <summary>Advance one stage (wraps to the first when loop is true). Returns false at the last stage.</summary>
        public bool Grow(bool loop = false)
        {
            if (stage < stages.Length - 1) { SetStage(stage + 1); return true; }
            if (loop) SetStage(0);
            return false;
        }

        /// <summary>A touch: squash down and jiggle back.</summary>
        public void Poke(float strength = 1f)
        {
            float s = pokeStrength * strength;
            velocity += new Vector3(s * 6f, -s * 12f, s * 6f);
        }

        void ShowOnly(int index)
        {
            stage = index;
            for (int i = 0; i < stages.Length; i++)
                if (stages[i]) stages[i].gameObject.SetActive(i == index);
        }

        void Update()
        {
            float dt = Mathf.Min(Time.deltaTime, 1f / 30f);
            // Damped spring toward rest scale; slightly under-damped so it overshoots once or twice.
            Vector3 accel = stiffness * (Vector3.one - scale) - damping * velocity;
            velocity += accel * dt;
            scale += velocity * dt;
            Apply(Time.time);
        }

        void Apply(float t)
        {
            var current = Current;
            if (!current) return;
            current.localScale = scale;
            float sx = Mathf.Sin(t * swaySpeed * 2.1f + phase) * swayDegrees;
            float sz = Mathf.Sin(t * swaySpeed * 1.6f + phase * 1.3f) * swayDegrees;
            current.localRotation = Quaternion.Euler(sx, 0f, sz);
        }

#if UNITY_EDITOR
        // Preview the chosen stage in edit mode (deferred: SetActive is not allowed inside OnValidate).
        void OnValidate()
        {
            if (Application.isPlaying || stages.Length == 0) return;
            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (this && !UnityEditor.EditorUtility.IsPersistent(this)) ShowOnly(Mathf.Clamp(stage, 0, stages.Length - 1));
            };
        }

        [ContextMenu("Grow 生长一级")] void GrowMenu() => Grow(true);
        [ContextMenu("Poke 戳一下")] void PokeMenu() => Poke();
#endif
    }
}
