using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;

namespace Reefkeeper.Corals
{
    /// <summary>
    /// Lucky Cat's wheelbarrow as a shop: a few corals stand in a row in the cart, spaced apart so a
    /// hand can't take the wrong one. Looking at one (head gaze) or pointing a hand/controller ray at it
    /// highlights it and shows its price above it (scallop icon and number). Grabbing it buys it,
    /// paid from the scallop balance (ReefScallopReward); one the player can't afford shows its price
    /// in red and can't be picked up, and trying makes the cat say so. A bought coral is held only while pinching, like the
    /// brush, and stays where it is let go (unless it is planted). The empty slot restocks after a while.
    /// </summary>
    [DefaultExecutionOrder(116)]
    public sealed class CoralCart : MonoBehaviour
    {
        [Tooltip("Corals on display at once.")] public int slots = 3;
        [Tooltip("Space between neighbouring corals (m).")] public float spacing = 0.22f;
        [Tooltip("Offset of the row along the cart, from its socket (m): keeps clear of the sign post.")] public float rowOffset = 0.05f;
        [Tooltip("Seconds before an emptied slot gets a new coral.")] public float restockSeconds = 4f;
        [Tooltip("How far head gaze reaches (m).")] public float gazeReach = 4f;

        CoralShop shop;
        Transform cart;
        Camera view;
        readonly ReefCoralFragment[] items = new ReefCoralFragment[8];
        readonly float[] restockAt = new float[8];
        readonly Vector3[] spots = new Vector3[8];
        readonly RaycastHit[] hits = new RaycastHit[16];
        Quaternion facing;
        ReefScallopReward wallet;
        float nextRefusal;
        ReefCoralFragment gazed;
        Transform tag;
        Text price;
        Material overlay;
        static readonly Color Cream = new Color(.97f, .93f, .81f), Panel = new Color(.15f, .22f, .28f, .94f);

        /// <summary>The coral the price tag is showing (gazed at or ray-hovered), if any.</summary>
        public ReefCoralFragment Highlighted { get; private set; }

        public void Initialize(CoralShop owner, Transform cartSocket)
        {
            shop = owner; cart = cartSocket;
            wallet = shop.workshop ? shop.workshop.GetComponent<ReefScallopReward>() : null;
            if (!wallet) wallet = FindFirstObjectByType<ReefScallopReward>();
            // The decorative heap in the cart makes way for the few corals on sale.
            var heap = cart.Find("Cart corals (display)");
            if (heap) heap.gameObject.SetActive(false);
            slots = Mathf.Clamp(slots, 1, items.Length);
            // Row along the cart's long side, on the tray floor.
            var along = Vector3.ProjectOnPlane(cart.right, Vector3.up).normalized;
            var front = Vector3.ProjectOnPlane(shop.transform.forward, Vector3.up).normalized;
            facing = Quaternion.LookRotation(front, Vector3.up);
            for (int i = 0; i < slots; i++)
            {
                var p = cart.position + along * (rowOffset + (i - (slots - 1) * .5f) * spacing);
                spots[i] = Physics.Raycast(p + Vector3.up * .8f, Vector3.down, out var hit, 2f, ~0, QueryTriggerInteraction.Ignore)
                    && hit.collider.transform.IsChildOf(shop.transform) ? hit.point : p;
                Stock(i);
            }
            BuildTag();
        }

        void Stock(int i)
        {
            if (shop.stock.Length == 0) return;
            // A species not already on show, when there are enough of them.
            var shown = new HashSet<string>();
            foreach (var it in items) if (it) shown.Add(CoralRules.Species(it.gameObject));
            var choices = new List<CoralGrowth>();
            foreach (var s in shop.stock) if (s && !shown.Contains(s.name)) choices.Add(s);
            if (choices.Count == 0) choices.AddRange(shop.stock);
            var prefab = choices[Random.Range(0, choices.Count)];
            var coral = Instantiate(prefab, spots[i], facing * Quaternion.Euler(0, Random.Range(-20f, 20f), 0));
            coral.name = CoralRules.Species(prefab.gameObject);
            coral.SetStage(0, false);
            var fragment = CoralSeating.MakePickable(coral, shop.corals, shop.outline, shop.corals.GetComponent<XRInteractionManager>());
            // Bought corals behave like the brush: pinch to hold, stays where it is let go.
            fragment.pinchToHold = true;
            fragment.leaveWhereReleased = true;
            fragment.selectEntered.AddListener(OnGrabbed);
            int price = CoralRules.Price(coral.name);
            fragment.purchaseAllowed = () => Affordable(price);
            fragment.purchaseRefused = () => Refuse(fragment);
            IgnoreBodies(fragment);
            items[i] = fragment;
            restockAt[i] = 0;
        }

        /// <summary>
        /// Shop corals (and bought ones left lying around) never push the player's body: hands still
        /// grab them, but leaning over the cart must not shove the swimmer back.
        /// </summary>
        public static void IgnoreBodies(ReefCoralFragment fragment)
        {
            foreach (var body in FindObjectsByType<CharacterController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                foreach (var c in fragment.colliders)
                    if (c && body) Physics.IgnoreCollision(body, c, true);
        }

        void OnGrabbed(SelectEnterEventArgs args)
        {
            var fragment = args.interactableObject as ReefCoralFragment;
            int i = System.Array.IndexOf(items, fragment);
            if (i < 0) return;
            // Bought: pay for it (the grab was only allowed if the balance covers the price).
            string species = CoralRules.Species(fragment.gameObject);
            int price = CoralRules.Price(species);
            if (wallet && !wallet.TrySpend(price, fragment.transform.position))
                Debug.LogWarning($"[Cart] {species} taken without enough scallops ({wallet.Balance}/{price})");
            fragment.selectEntered.RemoveListener(OnGrabbed);
            fragment.purchaseAllowed = null; fragment.purchaseRefused = null;
            if (fragment == gazed) { fragment.SetAim(false); gazed = null; }
            items[i] = null;
            restockAt[i] = Time.time + restockSeconds;
            shop.Sold(species, price);
        }

        bool Affordable(int price) => !wallet || wallet.Balance >= price;

        void Refuse(ReefCoralFragment fragment)
        {
            if (Time.time < nextRefusal) return;
            nextRefusal = Time.time + 1.5f;
            string species = CoralRules.Species(fragment.gameObject);
            shop.TooExpensive(species, CoralRules.Price(species), wallet ? wallet.Balance : 0);
            if (wallet) wallet.ShowShortfall();
        }

        /// <summary>Headset: a grab attempt (pinch/fist, or grip) on a highlighted coral the player can't afford.</summary>
        void CheckRefusedGrab(ReefCoralFragment show)
        {
            if (!show || show.PurchaseAllowed || shop.corals.CenterAimMode) return;
            for (int i = 0; i < 2; i++)
            {
                bool tried = Hands.ReefInput.HandsMode ? Hands.ReefHands.Get(i).GrabDown : Hands.ReefInput.TalkDown(i);
                if (tried) { Refuse(show); return; }
            }
        }

        void Update()
        {
            if (!shop) return;
            for (int i = 0; i < slots; i++)
                if (!items[i] && restockAt[i] > 0 && Time.time >= restockAt[i] && SpotIsClear(spots[i])) Stock(i);
            view = Camera.main;
            ReefCoralFragment target = null;
            // Head gaze (headset; the desktop crosshair is handled by ReefCoralPickup's own aim).
            if (view && !shop.corals.CenterAimMode && !ReefToolMenu.BlocksWorldInput)
            {
                var t = view.transform;
                int n = Physics.SphereCastNonAlloc(new Ray(t.position, t.forward), .03f, hits, gazeReach, ~0, QueryTriggerInteraction.Collide);
                float nearest = float.MaxValue;
                for (int k = 0; k < n; k++)
                {
                    var f = hits[k].collider.GetComponentInParent<ReefCoralFragment>();
                    if (!f || System.Array.IndexOf(items, f) < 0 || hits[k].distance >= nearest) continue;
                    nearest = hits[k].distance; target = f;
                }
            }
            if (gazed != target)
            {
                if (gazed) gazed.SetAim(false);
                gazed = target;
                if (gazed) gazed.SetAim(true);
            }
            // The tag follows whichever shop coral is highlighted: ray/hand hover first, then gaze or crosshair.
            ReefCoralFragment show = null;
            foreach (var it in items) if (it && it.isHovered && !it.isSelected) { show = it; break; }
            if (!show) foreach (var it in items) if (it && it.Highlighted) { show = it; break; }
            Highlighted = show;
            UpdateTag(show);
            CheckRefusedGrab(show);
        }

        bool SpotIsClear(Vector3 spot)
        {
            int n = Physics.OverlapSphereNonAlloc(spot + Vector3.up * .08f, .08f, overlaps, ~0, QueryTriggerInteraction.Collide);
            // Only a coral the player put down here blocks the slot; the neighbours on sale don't.
            for (int k = 0; k < n; k++)
            {
                var f = overlaps[k].GetComponentInParent<ReefCoralFragment>();
                if (f && !f.isSelected && System.Array.IndexOf(items, f) < 0
                    && Vector3.ProjectOnPlane(f.transform.position - spot, Vector3.up).magnitude < spacing * .5f) return false;
            }
            return true;
        }
        readonly Collider[] overlaps = new Collider[16];

        // ---------------- price tag: navy pill, scallop icon + price, above the coral, facing the viewer

        void BuildTag()
        {
            var shader = Resources.Load<Shader>("DiveCards/JournalOverlay");
            overlay = shader ? new Material(shader) { name = "Price tag overlay" } : null;
            var hud = FindFirstObjectByType<ReefCleaningHUD>(FindObjectsInactive.Include);
            var font = hud && hud.boldFont ? hud.boldFont : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var go = new GameObject("Coral price tag", typeof(RectTransform), typeof(Canvas)) { layer = 5 };
            tag = go.transform;
            var canvas = go.GetComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace; canvas.sortingOrder = 40;
            ((RectTransform)tag).sizeDelta = new Vector2(160, 64);
            tag.localScale = Vector3.one * .0017f; // readable from a couple of metres
            var pill = Child<Image>(tag, "Pill", Vector2.zero, new Vector2(150, 60));
            pill.sprite = Pill(); pill.type = Image.Type.Sliced; pill.pixelsPerUnitMultiplier = 2.1f; pill.color = Panel;
            var icon = Child<RawImage>(pill.transform, "Scallop", new Vector2(-34, 0), new Vector2(44, 39));
            icon.texture = Resources.Load<Texture2D>("Shop/scallop_coin");
            price = Child<Text>(pill.transform, "Price", new Vector2(22, 0), new Vector2(70, 60));
            price.font = font; price.fontSize = 34; price.color = Cream; price.alignment = TextAnchor.MiddleCenter;
            price.horizontalOverflow = HorizontalWrapMode.Overflow;
            var shadow = price.gameObject.AddComponent<Shadow>(); shadow.effectColor = new Color(0, .04f, .05f, .35f); shadow.effectDistance = new Vector2(0, -2);
            tag.gameObject.SetActive(false);
        }

        T Child<T>(Transform parent, string name, Vector2 at, Vector2 size) where T : Graphic
        {
            var go = new GameObject(name, typeof(RectTransform)) { layer = 5 };
            go.transform.SetParent(parent, false);
            var r = (RectTransform)go.transform; r.anchoredPosition = at; r.sizeDelta = size;
            var g = go.AddComponent<T>(); g.raycastTarget = false; g.material = overlay;
            return g;
        }

        void UpdateTag(ReefCoralFragment coral)
        {
            if (!tag) return;
            tag.gameObject.SetActive(coral && view);
            if (!coral || !view) return;
            var b = coral.colliders.Count > 0 && coral.colliders[0] ? coral.colliders[0].bounds : new Bounds(coral.transform.position, Vector3.one * .1f);
            tag.position = new Vector3(b.center.x, b.max.y + .09f, b.center.z);
            tag.rotation = Quaternion.LookRotation(tag.position - view.transform.position, Vector3.up);
            int cost = CoralRules.Price(CoralRules.Species(coral.gameObject));
            price.text = cost.ToString();
            // Too expensive for the current balance: the price shows in soft red.
            price.color = Affordable(cost) ? Cream : new Color(.95f, .5f, .45f);
        }

        static Sprite pillSprite;
        static Sprite Pill()
        {
            if (pillSprite) return pillSprite;
            const int size = 128; float r = size * .5f;
            var t = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "Price pill" };
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    px[y * size + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(r - Vector2.Distance(new Vector2(x + .5f, y + .5f), new Vector2(r, r))) * 255));
            t.SetPixels32(px); t.Apply(false, true);
            float border = r - 1;
            return pillSprite = Sprite.Create(t, new Rect(0, 0, size, size), new Vector2(.5f, .5f), 100, 0, SpriteMeshType.FullRect, new Vector4(border, border, border, border));
        }

        void OnDestroy()
        {
            if (tag) Destroy(tag.gameObject);
            if (overlay) Destroy(overlay);
        }
    }
}
