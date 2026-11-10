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
        [SerializeField] AudioClip purrMeow, shortMeow;

        public bool AimingCat { get; private set; }
        /// <summary>The cat has handed over the dive journal and waits for the player to open and close it.</summary>
        public bool AwaitingJournal { get; private set; }
        /// <summary>Button that opens the journal on the current setup.</summary>
        public static string JournalKey => UnityEngine.XR.XRSettings.isDeviceActive ? "Y" : "J";
        /// <summary>How to open the journal on the current setup, as an instruction.</summary>
        static string OpenJournal => Hands.ReefInput.HandsMode ? "Turn your left palm up and tap Journal" : $"Press {JournalKey}";
        /// <summary>Screen hint while the handed-over journal is still unopened.</summary>
        public string JournalPrompt => AwaitingJournal && DiveCardMock.Instance && !DiveCardMock.Instance.IsOpen
            ? $"{OpenJournal} to open your dive journal" : "";
        public string Prompt => AimingCat ? (corals.Held ? "Plant your coral first"
            : aimingHand >= 0 && Hands.ReefInput.HandsMode ? "Pinch to talk to Lucky Cat" : "Talk to Lucky Cat") : "";
        public string PromptKey => !AimingCat || corals.Held || aimingHand >= 0 && Hands.ReefInput.HandsMode ? ""
            : aimingHand == 0 ? "quest_grip_left" : aimingHand == 1 ? "quest_grip_right" : "keyboard_g";
        /// <summary>What the cat is saying, empty when quiet.</summary>
        public string Line => Time.time < lineUntil ? line : "";
        public bool Automated { get; set; }

        readonly RaycastHit[] hits = new RaycastHit[32];
        // Index 0 = left, 1 = right.
        int aimingHand = -1;
        CoralPlanting planting;
        bool journalSeenOpen;
        string line = "";
        float lineUntil, nextTouch;
        Coroutine mood;
        AudioSource meowSource;

        void Awake()
        {
            if (cat && (purrMeow || shortMeow))
            {
                meowSource = cat.gameObject.AddComponent<AudioSource>();
                meowSource.playOnAwake = false;
                meowSource.spatialBlend = 1f;
                meowSource.dopplerLevel = 0f;
            }
        }

        void Start()
        {
            // With the tutorial running, the journal is the cat's to hand over (see Talk).
            planting = FindFirstObjectByType<CoralPlanting>();
            if (planting && DiveCardMock.Instance) DiveCardMock.Instance.Available = false;
        }

        void Update()
        {
            AimingCat = false; aimingHand = -1;
            // Runs before the input block below: the open journal blocks world input.
            if (AwaitingJournal && DiveCardMock.Instance)
            {
                if (DiveCardMock.Instance.IsOpen) journalSeenOpen = true;
                else if (journalSeenOpen && !DiveCardMock.BlocksControls)
                {
                    AwaitingJournal = false; journalSeenOpen = false;
                    GiveCoral("Now your first coral! ");
                }
            }
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
                // Point a controller or hand ray at the cat and press Grip (controller) or pinch (hand).
                // Both rays may be on the cat; the hand that presses is the one that talks.
                for (int i = 0; i < 2; i++)
                {
                    var origin = Hands.ReefInput.RayOrigin(i);
                    if (!origin) continue;
                    if (!RayHitsCat(new Ray(origin.position, origin.forward), rayReach)) continue;
                    if (!AimingCat) { AimingCat = true; aimingHand = i; }
                    if (Hands.ReefInput.TalkDown(i)) { aimingHand = i; nextTouch = Time.time + 2f; Talk(); break; }
                }
                // Or reach out and touch the cat.
                if (Time.time >= nextTouch)
                    for (int i = 0; i < 2; i++)
                        if (Hands.ReefInput.TryGetPosition(i, out var at) &&
                            Vector3.Distance(catCollider.ClosestPoint(at), at) <= touchReach)
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

        /// <summary>
        /// Talks to the cat. In the tutorial: before three spots are clean the cat asks for that;
        /// the first talk after hands over the dive journal, and closing the journal brings the
        /// first coral. Otherwise hands the player a random coral. Returns it, or null.
        /// </summary>
        public ReefCoralFragment Talk()
        {
            var journal = DiveCardMock.Instance;
            if (journal && !journal.Available && planting)
            {
                if (!planting.ClearedFirstSpots)
                {
                    Say($"Clear {planting.SpotsToClear} mossy planting spots first, then come see me!", LuckyCat.Mood.Beckon);
                    return null;
                }
                journal.Available = true; AwaitingJournal = true; journalSeenOpen = false;
                Say($"Great cleaning! This dive journal is yours. {OpenJournal} to open it.", LuckyCat.Mood.Happy, 6f);
                return null;
            }
            if (AwaitingJournal)
            {
                Say($"Have a look in your journal first! {OpenJournal}.", LuckyCat.Mood.Beckon);
                return null;
            }
            if (corals.Held)
            {
                Say("Plant that one first, then come back!", LuckyCat.Mood.Beckon);
                return null;
            }
            return GiveCoral("");
        }

        ReefCoralFragment GiveCoral(string intro)
        {
            if (corals.Held) return null;
            var prefab = stock[Random.Range(0, stock.Length)];
            var coral = Instantiate(prefab, corals.hand.transform.position, Quaternion.identity);
            string species = CoralRules.Species(prefab.gameObject);
            coral.name = species;
            coral.SetStage(0, false);
            var fragment = CoralSeating.MakePickable(coral, corals, outline, corals.GetComponent<XRInteractionManager>());
            corals.Give(fragment);
            Say($"{intro}Here's a {CoralRules.DisplayName(species)}! {CoralRules.Needs(species)}.", LuckyCat.Mood.Happy);
            return fragment;
        }

        void Say(string text, LuckyCat.Mood face, float seconds = 0)
        {
            line = text; lineUntil = Time.time + (seconds > 0 ? seconds : lineSeconds);
            if (!cat) return;
            if (meowSource)
            {
                bool playPurr = purrMeow && (!shortMeow || Random.value < 0.5f);
                meowSource.clip = playPurr ? purrMeow : shortMeow;
                meowSource.volume = playPurr ? 0.8f : 0.55f;
                meowSource.Play();
            }
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
