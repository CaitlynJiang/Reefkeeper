using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit;

namespace Reefkeeper.Corals
{
    /// <summary>
    /// Lucky Cat's coral cart. Aim at the cat and press G (desktop), or point a controller ray at it
    /// and press that hand's Grip, or touch it with a controller, to talk:
    /// the cat hands over a random coral from the cart, at its smallest stage, straight into the
    /// player's hand. One coral at a time: plant it before asking for another.
    /// </summary>
    [DefaultExecutionOrder(115)]
    public sealed class CoralShop : MonoBehaviour
    {
        public LuckyCat cat;
        [Tooltip("The cat's aim/touch collider.")] public Collider catCollider;
        public ReefWorkshop workshop;
        public ReefCoralPickup corals;
        [Tooltip("Corals the cart offers (growth prefabs).")] public CoralGrowth[] stock = new CoralGrowth[0];
        public Material outline;
        public float reach = 4f;
        [Tooltip("With real controllers: how close a hand must come to the cat (m).")]
        public float touchReach = 0.35f;
        public float lineSeconds = 3.5f;
        [Tooltip("With real controllers: how far a controller ray reaches the cat (m).")]
        public float rayReach = 8f;

        public bool AimingCat { get; private set; }
        public string Prompt => AimingCat ? (corals.Held ? "Plant your coral first" : "Talk to Lucky Cat") : "";
        public string PromptKey => !AimingCat || corals.Held ? ""
            : aimingHand == 0 ? "quest_grip_left" : aimingHand == 1 ? "quest_grip_right" : "keyboard_g";
        /// <summary>What the cat is saying, empty when quiet.</summary>
        public string Line => Time.time < lineUntil ? line : "";
        public bool Automated { get; set; }

        readonly RaycastHit[] hits = new RaycastHit[32];
        Transform[] hands = new Transform[0];
        // Index 0 = left, 1 = right.
        readonly Transform[] rayOrigins = new Transform[2];
        InputAction[] grips;
        int aimingHand = -1;
        string line = "";
        float lineUntil, nextTouch;
        Coroutine mood;

        void Awake()
        {
            grips = new[]
            {
                new InputAction("Talk left", InputActionType.Button, "<XRController>{LeftHand}/{GripButton}"),
                new InputAction("Talk right", InputActionType.Button, "<XRController>{RightHand}/{GripButton}"),
            };
        }
        void OnEnable() { foreach (var g in grips) g.Enable(); }
        void OnDisable() { foreach (var g in grips) g.Disable(); }
        void OnDestroy() { foreach (var g in grips) g.Dispose(); }

        void Start()
        {
            hands = workshop.xrGroup.GetComponentsInChildren<Transform>(true)
                .Where(t => t.name == "Left Controller" || t.name == "Right Controller").ToArray();
            foreach (var hand in hands)
            {
                // Aim along the controller's visible ray when there is one.
                var nearFar = hand.GetComponentInChildren<UnityEngine.XR.Interaction.Toolkit.Interactors.NearFarInteractor>(true);
                rayOrigins[hand.name.StartsWith("Left") ? 0 : 1] = nearFar ? nearFar.transform : hand;
            }
        }

        void Update()
        {
            AimingCat = false; aimingHand = -1;
            if (ReefToolMenu.BlocksWorldInput) return;
            bool canAct = Automated || corals.Automated || workshop.xrGroup.activeSelf || Cursor.lockState == CursorLockMode.Locked;
            if (!canAct || !corals.View) return;
            if (corals.CenterAimMode)
            {
                var view = corals.View.transform;
                AimingCat = RayHitsCat(new Ray(view.position, view.forward), reach);
                if (AimingCat && Keyboard.current != null && Keyboard.current.gKey.wasPressedThisFrame) Talk();
            }
            else if (workshop.xrGroup.activeSelf)
            {
                // Point a controller ray at the cat and press that hand's Grip.
                for (int i = 0; i < rayOrigins.Length; i++)
                {
                    var origin = rayOrigins[i];
                    if (!origin || !origin.gameObject.activeInHierarchy) continue;
                    if (!RayHitsCat(new Ray(origin.position, origin.forward), rayReach)) continue;
                    AimingCat = true; aimingHand = i;
                    if (grips[i].WasPressedThisFrame()) { nextTouch = Time.time + 2f; Talk(); }
                    break;
                }
                // Or reach out and touch the cat.
                if (Time.time >= nextTouch)
                    foreach (var hand in hands)
                        if (hand && hand.gameObject.activeInHierarchy &&
                            Vector3.Distance(catCollider.ClosestPoint(hand.position), hand.position) <= touchReach)
                        { nextTouch = Time.time + 2f; Talk(); break; }
            }
        }

        bool RayHitsCat(Ray ray, float distance)
        {
            int count = Physics.RaycastNonAlloc(ray, hits, distance, ~0, QueryTriggerInteraction.Collide);
            float nearest = float.PositiveInfinity; Collider first = null;
            for (int i = 0; i < count; i++)
            {
                var c = hits[i].collider;
                if (c.transform.IsChildOf(workshop.desktopPlayer.transform) || c.transform.IsChildOf(workshop.xrGroup.transform)) continue;
                if (corals.Held && c.transform.IsChildOf(corals.Held.transform)) continue;
                if (c.isTrigger && c != catCollider) continue;
                if (hits[i].distance < nearest) { nearest = hits[i].distance; first = c; }
            }
            return first == catCollider;
        }

        /// <summary>Hands the player a random coral. Returns it, or null if they already carry one.</summary>
        public ReefCoralFragment Talk()
        {
            if (corals.Held)
            {
                Say("Plant that one first, then come back!", LuckyCat.Mood.Beckon);
                return null;
            }
            var prefab = stock[Random.Range(0, stock.Length)];
            var coral = Instantiate(prefab, corals.hand.transform.position, Quaternion.identity);
            string species = CoralRules.Species(prefab.gameObject);
            coral.name = species;
            coral.SetStage(0, false);
            var fragment = CoralSeating.MakePickable(coral, corals, outline, corals.GetComponent<XRInteractionManager>());
            corals.Give(fragment);
            Say($"Here's a {CoralRules.DisplayName(species)}! {CoralRules.Needs(species)}.", LuckyCat.Mood.Happy);
            return fragment;
        }

        void Say(string text, LuckyCat.Mood face)
        {
            line = text; lineUntil = Time.time + lineSeconds;
            if (!cat) return;
            if (mood != null) StopCoroutine(mood);
            mood = StartCoroutine(Mood(face));
        }

        IEnumerator Mood(LuckyCat.Mood face)
        {
            cat.Play(face);
            yield return new WaitForSeconds(2f);
            cat.Play(LuckyCat.Mood.Idle);
        }
    }
}
