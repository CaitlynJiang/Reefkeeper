using System.Collections;
using UnityEngine;

namespace Reefkeeper.Corals
{
    /// <summary>
    /// The coral crab that sleeps under a mature red coral (spawned by <see cref="CoralCrabSpawn"/>).
    /// It sleeps a moment, wakes up (WakeUp clip: shiver, peek, rise, stretch), then scuttles
    /// sideways (Walk clip) slowly away, and finally burrows into the sand and is removed.
    /// The clips come from Blender (coral_crab.py); the Animator goes WakeUp -> Walk by itself.
    /// </summary>
    public sealed class CoralCrab : MonoBehaviour
    {
        [Tooltip("Seconds asleep before it wakes up.")] public float sleepSeconds = 1.5f;
        [Tooltip("Walking speed (m/s).")] public float walkSpeed = 0.1f;
        [Tooltip("How far it walks before burrowing (m).")] public float walkDistance = 2.5f;
        [Tooltip("Seconds to sink into the sand at the end.")] public float burrowSeconds = 1.5f;
        [Tooltip("Wake up on its own after sleepSeconds.")] public bool wakeOnStart = true;
        [Tooltip("Remove the crab when it has burrowed.")] public bool destroyWhenGone = true;

        public enum Phase { Sleeping, Waking, Walking, Burrowing, Gone }
        public Phase State { get; private set; }

        Animator animator;
        Transform legLeft, legRight;

        void Awake()
        {
            animator = GetComponentInChildren<Animator>();
            foreach (var t in GetComponentsInChildren<Transform>(true))
            {
                if (t.name == "Leg_L0") legLeft = t;
                if (t.name == "Leg_R0") legRight = t;
            }
        }

        void Start()
        {
            if (animator) animator.Play("Sleep", 0, Random.value);
            if (wakeOnStart) StartCoroutine(Run());
        }

        /// <summary>Wake up now (if still asleep) and walk off.</summary>
        public void WakeUp()
        {
            if (State != Phase.Sleeping) return;
            StopAllCoroutines();
            sleepSeconds = 0f;
            StartCoroutine(Run());
        }

        IEnumerator Run()
        {
            State = Phase.Sleeping;
            if (sleepSeconds > 0f) yield return new WaitForSeconds(sleepSeconds);

            State = Phase.Waking;
            if (animator)
            {
                animator.CrossFade("WakeUp", 0.1f, 0);
                yield return null;
                // wait for the controller to move on to Walk at the end of WakeUp
                while (!animator.GetCurrentAnimatorStateInfo(0).IsName("Walk")) yield return null;
            }

            State = Phase.Walking;
            // The Walk clip scuttles toward the crab's right side; pick a side at random.
            var side = legLeft && legRight ? legRight.position - legLeft.position : transform.right;
            side.y = 0f;
            var dir = side.normalized * (Random.value < 0.5f ? 1f : -1f);
            float walked = 0f;
            while (walked < walkDistance)
            {
                // ease in over the first 20 cm so it doesn't jump into its stride
                float ease = Mathf.Clamp01(walked / 0.2f) * 0.7f + 0.3f;
                float step = walkSpeed * ease * Time.deltaTime;
                transform.position += dir * step;
                walked += step;
                yield return null;
            }

            State = Phase.Burrowing;
            var start = transform.position;
            float depth = Mathf.Max(0.15f, ShellHeight());
            for (float t = 0f; t < 1f; t += Time.deltaTime / Mathf.Max(0.1f, burrowSeconds))
            {
                float e = t * t;
                transform.position = start + dir * (walkSpeed * 0.4f * t * burrowSeconds) - Vector3.up * depth * e;
                yield return null;
            }
            State = Phase.Gone;
            if (destroyWhenGone) Destroy(gameObject);
            else gameObject.SetActive(false);
        }

        float ShellHeight()
        {
            var b = new Bounds(transform.position, Vector3.zero);
            foreach (var r in GetComponentsInChildren<Renderer>()) b.Encapsulate(r.bounds);
            return b.max.y - transform.position.y;
        }
    }
}
