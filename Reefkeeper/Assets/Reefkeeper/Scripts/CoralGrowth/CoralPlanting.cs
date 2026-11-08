using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Reefkeeper.Corals
{
    /// <summary>
    /// Planting and care loop at the nursery. While carrying a coral, aim at a socket and press G
    /// to plant it (with real controllers: point a controller ray at the socket and pull that
    /// hand's trigger, or let go near the socket). The socket's grade must fit the coral and its
    /// moss must be brushed off first. Aim at a planted coral to see its stage; when a stage is
    /// full, click (or point and pull the trigger, or reach and pull it) to fertilize and grow it.
    /// </summary>
    [DefaultExecutionOrder(120)]
    public sealed class CoralPlanting : MonoBehaviour
    {
        public ReefWorkshop workshop;
        public ReefCoralPickup corals;
        public ReefBrushPickup brushes;
        [Tooltip("Line material for corals hung on hooks.")] public Material rope;
        [Tooltip("Moss pad prefab, added to sockets that appear later (e.g. on a grown mushroom coral).")]
        public RackDirtPatch mossPrefab;
        public float reach = 3f;
        [Tooltip("With real controllers: how close a hand must be to plant on release or to tend a coral (m).")]
        public float handReach = 0.3f;
        [Tooltip("With real controllers: how far a controller ray reaches sockets and corals (m).")]
        public float rayReach = 4f;
        [Min(1)] public float secondsPerStage = 30f;
        public Vector2 mossReturnSeconds = new Vector2(25f, 50f);

        public string Prompt { get; private set; } = "";
        public string PromptKey { get; private set; } = "";
        /// <summary>The planted coral being looked at (or reached for), if any.</summary>
        public CoralNurture Hovered { get; private set; }
        public IReadOnlyList<CoralNurture> Planted => planted;
        public bool Automated { get; set; }

        readonly List<CoralNurture> planted = new List<CoralNurture>();
        readonly List<RackSocket> sockets = new List<RackSocket>();
        readonly RaycastHit[] hits = new RaycastHit[64];
        Transform[] hands = new Transform[0];
        // Index 0 = left, 1 = right.
        readonly Transform[] rayOrigins = new Transform[2];
        InputAction[] triggers;

        void Awake()
        {
            corals.PlantOnRelease = TryPlantNear;
            triggers = new[]
            {
                new InputAction("Plant or fertilize left", InputActionType.Button, "<XRController>{LeftHand}/triggerPressed"),
                new InputAction("Plant or fertilize right", InputActionType.Button, "<XRController>{RightHand}/triggerPressed"),
            };
            foreach (var t in triggers) t.Enable();
        }

        void Start()
        {
            sockets.AddRange(FindObjectsByType<RackSocket>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).Where(s => s.moss));
            hands = workshop.xrGroup.GetComponentsInChildren<Transform>(true)
                .Where(t => t.name == "Left Controller" || t.name == "Right Controller").ToArray();
            foreach (var hand in hands)
            {
                // Aim along the controller's visible ray when there is one.
                var nearFar = hand.GetComponentInChildren<UnityEngine.XR.Interaction.Toolkit.Interactors.NearFarInteractor>(true);
                rayOrigins[hand.name.StartsWith("Left") ? 0 : 1] = nearFar ? nearFar.transform : hand;
            }
        }

        void OnDestroy()
        {
            if (triggers != null) foreach (var t in triggers) t.Dispose();
            if (corals) corals.PlantOnRelease = null;
        }

        void Update()
        {
            Prompt = ""; PromptKey = ""; Hovered = null;
            TrackTutorial();
            bool canAct = Automated || corals.Automated || workshop.xrGroup.activeSelf || Cursor.lockState == CursorLockMode.Locked;
            if (!canAct) return;
            if (corals.CenterAimMode)
            {
                var view = corals.View;
                if (view) AimAlong(new Ray(view.transform.position, view.transform.forward), reach,
                    Keyboard.current != null && Keyboard.current.gKey.wasPressedThisFrame,
                    Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame, false);
            }
            else if (workshop.xrGroup.activeSelf) ReachWithHands();
        }

        // ---------------------------------------------------------------- aiming (view or controller ray)

        /// <summary>Prompts for (and acts on) what the ray points at. Returns true if it pointed at something relevant.</summary>
        bool AimAlong(Ray ray, float distance, bool plantPressed, bool carePressed, bool controller)
        {
            var hit = Nearest(ray, distance);
            if (!hit.collider) return false;
            // The moss pad around a planted coral is often hit first; it still means that coral.
            var nurture = hit.collider.GetComponentInParent<CoralNurture>();
            var socket = SocketAt(hit.collider, hit.point);
            if (!nurture && socket && socket.occupant) nurture = socket.occupant.GetComponent<CoralNurture>();
            if (nurture) Hovered = nurture;

            if (corals.Held)
            {
                if (!socket) return nurture;
                CarryPrompt(socket, plantPressed, controller);
                return true;
            }
            if (nurture) { CarePrompt(nurture, carePressed, controller); return true; }
            if (socket && socket.IsFree && socket.Mossy && !BrushInHand) { Prompt = socket.CleaningHint; return true; }
            return false;
        }

        void CarryPrompt(RackSocket socket, bool pressed, bool controller)
        {
            string species = CoralRules.Species(corals.Held.gameObject), name = CoralRules.DisplayName(species);
            if (!socket.IsFree) { Prompt = "This spot is taken"; return; }
            if (!CoralRules.CanPlant(species, socket.grade)) { Prompt = $"Needs a {Where(species)}"; return; }
            if (socket.Mossy) { Prompt = socket.CleaningHint; return; }
            Prompt = controller ? $"Pull the trigger to plant {name}" : $"Plant {name}";
            PromptKey = controller ? "" : "keyboard_g";
            if (pressed) Plant(socket);
        }

        void CarePrompt(CoralNurture nurture, bool pressed, bool controller)
        {
            if (nurture.Paused) { if (!BrushInHand) Prompt = nurture.socket.CleaningHint; return; }
            if (!nurture.Ready) return;
            Prompt = controller ? "Pull the trigger to fertilize" : "Fertilize";
            PromptKey = controller ? "" : "mouse_left";
            if (pressed) Fertilize(nurture);
        }

        // Carrying covers the desktop hand; Held covers a real controller's grip.
        bool BrushInHand => brushes.ToolHeld;

        static string Where(string species) => species == CoralRules.Disc ? "cairn or flat cradle"
            : species == CoralRules.Finger ? "hanging hook" : "flat cradle";

        // The socket behind a hit: the one it belongs to, or, on an occupied coral that offers its own
        // sockets (a grown mushroom coral), the nearest free one of those.
        static RackSocket SocketAt(Collider collider, Vector3 point)
        {
            var socket = collider.GetComponentInParent<RackSocket>();
            if (!socket || socket.IsFree) return socket;
            var nurture = collider.GetComponentInParent<CoralNurture>();
            if (!nurture || !nurture.coral.Current) return socket;
            var top = nurture.coral.Current.GetComponentsInChildren<RackSocket>()
                .Where(s => s.IsFree).OrderBy(s => (s.transform.position - point).sqrMagnitude).FirstOrDefault();
            return top ? top : socket;
        }

        RaycastHit Nearest(Ray ray, float distance)
        {
            int count = Physics.RaycastNonAlloc(ray, hits, distance, ~0, QueryTriggerInteraction.Collide);
            var best = new RaycastHit(); float nearest = float.PositiveInfinity;
            for (int i = 0; i < count; i++)
            {
                var c = hits[i].collider;
                if (c.transform.IsChildOf(workshop.desktopPlayer.transform) || c.transform.IsChildOf(workshop.xrGroup.transform)) continue;
                if (corals.Held && c.transform.IsChildOf(corals.Held.transform)) continue;
                if (workshop.brush.Held && c.transform.IsChildOf(workshop.brush.transform)) continue;
                if (workshop.scraper && workshop.scraper.Held && c.transform.IsChildOf(workshop.scraper.transform)) continue;
                if (c.isTrigger && !c.GetComponentInParent<RackSocket>()) continue;
                if (hits[i].distance < nearest) { nearest = hits[i].distance; best = hits[i]; }
            }
            return best;
        }

        // ---------------------------------------------------------------- real controllers

        void ReachWithHands()
        {
            // Point a controller ray and pull that hand's trigger.
            for (int i = 0; i < rayOrigins.Length; i++)
            {
                var origin = rayOrigins[i];
                if (!origin || !origin.gameObject.activeInHierarchy) continue;
                bool pressed = triggers[i].WasPressedThisFrame();
                if (AimAlong(new Ray(origin.position, origin.forward), rayReach, pressed, pressed, true)) return;
            }
            // Or reach: let go of a coral near a socket, or bring a hand up to a coral.
            if (corals.Held)
            {
                var socket = NearestPlantable(corals.Held);
                if (socket) Prompt = $"Let go to plant {CoralRules.DisplayName(CoralRules.Species(corals.Held.gameObject))}";
                return;
            }
            float best = handReach;
            foreach (var hand in hands)
            {
                if (!hand || !hand.gameObject.activeInHierarchy) continue;
                foreach (var nurture in planted)
                {
                    float d = Vector3.Distance(hand.position, Center(nurture.coral));
                    if (d < best) { best = d; Hovered = nurture; }
                }
            }
            if (!Hovered) return;
            if (Hovered.Paused) Prompt = Hovered.socket.CleaningHint;
            else if (Hovered.Ready)
            {
                Prompt = "Pull the trigger to fertilize";
                if (triggers[0].WasPressedThisFrame() || triggers[1].WasPressedThisFrame()) Fertilize(Hovered);
            }
        }

        bool TryPlantNear(ReefCoralFragment fragment)
        {
            if (corals.CenterAimMode) return false;
            var socket = NearestPlantable(fragment);
            if (!socket) return false;
            Plant(socket);
            return true;
        }

        RackSocket NearestPlantable(ReefCoralFragment fragment)
        {
            string species = CoralRules.Species(fragment.gameObject);
            var at = fragment.transform.position;
            return sockets.Where(s => s && s.isActiveAndEnabled && s.IsFree && !s.Mossy && CoralRules.CanPlant(species, s.grade))
                .Select(s => (s, d: Vector3.Distance(s.transform.position, at)))
                .Where(p => p.d <= handReach).OrderBy(p => p.d).Select(p => p.s).FirstOrDefault();
        }

        static Vector3 Center(CoralGrowth coral)
        {
            var b = new Bounds(coral.transform.position, Vector3.zero);
            if (coral.Current) foreach (var r in coral.Current.GetComponentsInChildren<Renderer>()) b.Encapsulate(r.bounds);
            return b.center;
        }

        // ---------------------------------------------------------------- actions

        public CoralNurture Plant(RackSocket socket)
        {
            if (!socket || !socket.IsFree || socket.Mossy || !corals.Held
                || !CoralRules.CanPlant(CoralRules.Species(corals.Held.gameObject), socket.grade)) return null;
            var fragment = corals.TakeHeld();
            if (!fragment) return null;
            var coral = fragment.GetComponent<CoralGrowth>();
            string species = CoralRules.Species(fragment.gameObject);
            Destroy(fragment);   // planted corals stay put; the grab component goes
            foreach (var c in coral.GetComponents<Collider>()) c.isTrigger = true;   // the brush reaches the moss through it
            CoralSeating.Seat(coral, socket, null, rope);
            socket.occupant = coral;

            var nurture = coral.gameObject.AddComponent<CoralNurture>();
            nurture.secondsPerStage = secondsPerStage;
            nurture.mossReturnSeconds = mossReturnSeconds;
            nurture.Begin(socket, coral, species);
            planted.Add(nurture);
            coral.Poke();
            return nurture;
        }

        public bool Fertilize(CoralNurture nurture)
        {
            if (!nurture.Fertilize()) return false;
            // A grown mushroom coral becomes a flat (B) socket itself.
            foreach (var top in nurture.coral.Current.GetComponentsInChildren<RackSocket>())
            {
                top.grade = RackSocket.Grade.B;
                if (!top.moss && mossPrefab)
                {
                    top.moss = Instantiate(mossPrefab, top.transform);
                    top.moss.transform.localPosition = Vector3.zero;
                    top.moss.transform.rotation = Quaternion.identity;
                    var s = top.transform.lossyScale;
                    top.moss.transform.localScale = new Vector3(0.6f / s.x, 0.6f / s.y, 0.6f / s.z);
                    top.moss.ApplyStroke(1000f);   // starts clean
                }
                if (!sockets.Contains(top)) sockets.Add(top);
            }
            return true;
        }

        /// <summary>F5: removes every planted coral and covers all sockets in moss again.</summary>
        public void ResetAll()
        {
            foreach (var nurture in planted)
            {
                if (!nurture) continue;
                var socket = nurture.socket;
                if (socket)
                {
                    socket.occupant = null;
                    foreach (Transform c in socket.transform) if (c.name == "Coral line") Destroy(c.gameObject);
                }
                Destroy(nurture.gameObject);
            }
            planted.Clear();
            cleanedThree = gotCoral = plantedOne = false;
            sockets.RemoveAll(s => !s || s.GetComponentInParent<CoralNurture>());
            foreach (var s in sockets) s.moss.ResetDirt();
        }

        /// <summary>What the player should do next.</summary>
        // Opening steps, in order: clean 3 mossy spots, talk to Lucky Cat, plant a coral.
        // Each step stays done once reached.
        const int MossToClean = 3;
        bool cleanedThree, gotCoral, plantedOne;
        int cleanSockets;

        void TrackTutorial()
        {
            cleanSockets = 0;
            foreach (var s in sockets) if (s && s.moss && !s.Mossy) cleanSockets++;
            cleanedThree |= cleanSockets >= MossToClean;
            gotCoral |= corals.Held || planted.Count > 0;
            plantedOne |= planted.Count > 0;
        }

        public string Objective
        {
            get
            {
                planted.RemoveAll(n => !n);
                if (!cleanedThree) return $"Clear {MossToClean} planting spots ({Mathf.Min(cleanSockets, MossToClean)}/{MossToClean})";
                if (!gotCoral) return "Talk to Lucky Cat";
                if (!plantedOne) return corals.Held
                    ? $"Plant the {CoralRules.DisplayName(CoralRules.Species(corals.Held.gameObject))}"
                    : "Plant a coral";
                if (planted.Any(n => n.Ready && !n.Paused)) return "Fertilize a coral that is ready";
                if (corals.Held)
                {
                    var species = CoralRules.Species(corals.Held.gameObject);
                    return $"Plant the {CoralRules.DisplayName(species)}";
                }
                if (planted.Any(n => n.Paused)) return "Clear the growth around your corals";
                if (planted.Count == 0) return "Get a coral from Lucky Cat";
                if (planted.All(n => n.FullyGrown)) return "All corals fully grown!";
                return "Let your corals grow";
            }
        }
    }
}
