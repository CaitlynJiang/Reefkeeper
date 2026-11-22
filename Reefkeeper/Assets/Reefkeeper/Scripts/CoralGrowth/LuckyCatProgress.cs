using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using Reefkeeper.Photos;

namespace Reefkeeper.Corals
{
    /// <summary>
    /// What Lucky Cat's counter holds as the game moves on (session only, like the scallop balance):
    /// <list type="number">
    /// <item>First time the player comes near: the cat waves them over to talk, and tells them about the cart.</item>
    /// <item>The counter starts with an empty record crate (a hint of things to come).</item>
    /// <item>Every coral planted pays <see cref="plantReward"/> scallops. The first one brings an instant camera
    /// onto the counter, a gift.</item>
    /// <item>Taking the camera unlocks photo mode; every new photo delivered to the wall pays
    /// <see cref="photoReward"/>. The cat asks for a photo of itself (it takes the coral crab's old place on the wall).</item>
    /// <item>The Lucky Cat photo brings the record player and records onto the counter (Caitlyn's vinyl shop:
    /// VinylPlayer / ShopRecord, hidden until then). The music corner turntable stands on the player's slot
    /// and spins while a record plays.</item>
    /// </list>
    /// </summary>
    [DefaultExecutionOrder(117)]
    public sealed class LuckyCatProgress : MonoBehaviour
    {
        [Tooltip("The cat waves the player over the first time they come this close (m).")] public float approachRadius = 4.5f;
        [Tooltip("Scallops for each coral planted.")] public int plantReward = 3;
        [Tooltip("Scallops for each new photo delivered to the photo wall.")] public int photoReward = 5;
        [Tooltip("How far head gaze reaches the counter items (m).")] public float gazeReach = 4f;

        public bool CameraOwned { get; private set; }
        public bool VinylShopOpen { get; private set; }
        public const string CatSpecies = "LuckyCat";

        CoralShop shop;
        ReefScallopReward wallet;
        Vector3 counterSpot, front, right;
        ShopCounterItem cameraItem;
        VinylPlayer vinylPlayer;
        Transform platter;
        readonly System.Collections.Generic.List<GameObject> vinylShop = new System.Collections.Generic.List<GameObject>();
        ShopPriceTag tag;
        ShopCounterItem gazed;
        bool talked, cameraOffered;
        int plantings;
        float nextGreeting, nextRefusal;
        readonly RaycastHit[] hits = new RaycastHit[16];

        public void Initialize(CoralShop owner)
        {
            shop = owner;
            wallet = shop.workshop ? shop.workshop.GetComponent<ReefScallopReward>() : null;
            if (!wallet) wallet = FindFirstObjectByType<ReefScallopReward>();
            counterSpot = shop.counterSpot ? shop.counterSpot.position : shop.transform.position;
            front = Vector3.ProjectOnPlane(shop.transform.forward, Vector3.up).normalized;
            right = Vector3.Cross(Vector3.up, front);
            tag = new ShopPriceTag("Counter price tag");
            ReefPhotoMode.Unlocked = false;   // the camera has to be earned first
            Place("RecordCrate", -.62f);      // empty for now
            HideVinylShop();
            CoralPlanting.CoralPlanted += OnPlanted;
            ReefPhotoAlbum.Changed += OnPhoto;
            shop.Talked += OnTalked;
        }

        void OnDestroy()
        {
            CoralPlanting.CoralPlanted -= OnPlanted;
            ReefPhotoAlbum.Changed -= OnPhoto;
            if (shop) shop.Talked -= OnTalked;
            tag?.Destroy();
            ReefPhotoMode.Unlocked = true;
        }

        void OnTalked() => talked = true;

        public bool CanAfford(int price) => !wallet || wallet.Balance >= price;

        /// <summary>What the cat says when talked to at this point of the story, if anything special.</summary>
        public bool TalkLine(out string line, out LuckyCat.Mood mood)
        {
            mood = LuckyCat.Mood.Beckon;
            if (cameraItem && !CameraOwned) line = "The camera on the counter is a gift for you. Take it!";
            else if (CameraOwned && !VinylShopOpen && !ReefPhotoAlbum.Has(CatSpecies)) line = "Make a frame with both hands and take a photo of me! Then pin it on the photo wall.";
            else if (VinylShopOpen) line = $"Fancy some music? Records are {ShopRecord.Price} scallops. Put one on the record player!";
            else line = null;
            return line != null;
        }

        // ---------------- story beats

        void OnPlanted(CoralNurture nurture)
        {
            var at = nurture ? nurture.transform.position : counterSpot;
            if (wallet) wallet.Award(plantReward, at);
            plantings++;
            if (plantings == 1 && !cameraOffered) StartCoroutine(After(1.2f, OfferCamera));
        }

        void OfferCamera()
        {
            cameraOffered = true;
            cameraItem = Offer("InstantCamera", -.28f, ShopCounterItem.Kind.Camera, 0, false);
            shop.Speak("Your first coral, well done! I got an instant camera in for you. It's on the counter, a gift!", LuckyCat.Mood.Happy, 6f);
        }

        void OnPhoto(string species)
        {
            if (species == null) return;
            // Every new photo pinned on the wall pays (photos are stored when they reach the wall).
            var wall = FindFirstObjectByType<ReefPhotoWall>();
            if (wallet && photoReward > 0) wallet.Award(photoReward, wall ? wall.transform.position + Vector3.up : counterSpot);
            if (species == CatSpecies && CameraOwned && !VinylShopOpen) StartCoroutine(After(2f, OpenVinylShop));
        }

        // ---------------- Caitlyn's vinyl shop, held back until the Lucky Cat photo

        void HideVinylShop()
        {
            vinylPlayer = shop.GetComponentInChildren<VinylPlayer>(true);
            foreach (var display in shop.GetComponentsInChildren<VinylDisplay>(true)) vinylShop.Add(display.gameObject);
            foreach (var record in shop.GetComponentsInChildren<ShopRecord>(true))
                if (!vinylShop.Exists(g => record.transform.IsChildOf(g.transform))) vinylShop.Add(record.gameObject);
            if (vinylPlayer)
            {
                vinylShop.Add(vinylPlayer.gameObject);
                AddTurntable();
            }
            foreach (var g in vinylShop) if (g) g.SetActive(false);
        }

        void OpenVinylShop()
        {
            if (VinylShopOpen) return;
            VinylShopOpen = true;
            foreach (var g in vinylShop) if (g) { g.SetActive(true); StartCoroutine(PopIn(g.transform)); }
            shop.Speak($"What a lovely photo, thank you! I got a record player and some records in. Records are {ShopRecord.Price} scallops!", LuckyCat.Mood.Happy, 6f);
        }

        /// <summary>The music corner turntable under the record player's slot: platter centred on it, its top under the record.</summary>
        void AddTurntable()
        {
            var source = Resources.Load<GameObject>("Shop/RecordPlayer");
            var slot = vinylPlayer.Slot;
            if (!source || !slot) return;
            var model = Instantiate(source, vinylPlayer.transform.position, vinylPlayer.transform.rotation);
            model.name = "Turntable (music corner)";
            foreach (var c in model.GetComponentsInChildren<Collider>(true)) Destroy(c); // the slot is the player's own trigger
            foreach (var t in model.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == "Record_Default") t.gameObject.SetActive(false); // the bought records go on the platter
                if (t.name == "Platter") platter = t;
            }
            if (!platter) { model.transform.SetParent(vinylPlayer.transform, true); return; }
            var renderers = platter.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) { model.transform.SetParent(vinylPlayer.transform, true); return; }
            // Sized to the shop's records (shown enlarged for VR) rather than the player's scaled slot:
            // the platter is a little wider than one record.
            var top = renderers[0].bounds; foreach (var r in renderers) top.Encapsulate(r.bounds);
            float platterSize = Mathf.Max(top.size.x, top.size.z);
            var record = shop.GetComponentInChildren<ShopRecord>(true);
            var recordMesh = record ? record.GetComponent<MeshFilter>() : null;
            if (recordMesh && recordMesh.sharedMesh && platterSize > .001f)
            {
                var size = Vector3.Scale(recordMesh.sharedMesh.bounds.size, record.transform.lossyScale);
                float recordSize = Mathf.Max(Mathf.Abs(size.x), Mathf.Abs(size.y), Mathf.Abs(size.z));
                model.transform.localScale = Vector3.one * (recordSize * 1.12f / platterSize);
                top = renderers[0].bounds; foreach (var r in renderers) top.Encapsulate(r.bounds);
            }
            model.transform.SetParent(vinylPlayer.transform, true);
            var seat = vinylPlayer.transform.TransformPoint(slot.center);
            // A record is set down flat at the slot centre; the platter top sits just under it.
            model.transform.position += new Vector3(seat.x - top.center.x, seat.y - .006f - top.max.y, seat.z - top.center.z);
        }

        /// <summary>An item was picked up (or taken with G on desktop).</summary>
        public void OnItemTaken(ShopCounterItem item)
        {
            if (!item || item.Owned) return;
            if (item.kind == ShopCounterItem.Kind.Camera)
            {
                item.Owned = true; CameraOwned = true;
                ReefPhotoMode.Unlocked = true;
                StartCoroutine(Keep(item));
                shop.Speak("It's yours! Frame a shot with both thumbs and index fingers. Every new species photo earns scallops. Try one of me first!", LuckyCat.Mood.Happy, 7f);
                // A Lucky Cat photo from an earlier session already counts.
                if (ReefPhotoAlbum.Has(CatSpecies)) StartCoroutine(After(7f, OpenVinylShop));
                return;
            }
            if (wallet && !wallet.TrySpend(item.price, item.transform.position))
                Debug.LogWarning($"[Counter] {item.DisplayName} taken without enough scallops ({wallet.Balance}/{item.price})");
            item.Owned = true;
            IgnoreBodies(item);
        }

        // ---------------- counter items

        void Update()
        {
            var view = Camera.main;
            if (!view || !shop) return;
            // The platter (and the record on it) turn while music plays: 33 rpm.
            if (vinylPlayer && vinylPlayer.Playing && platter)
            {
                float step = 200f * Time.deltaTime;
                platter.Rotate(Vector3.up, step, Space.World);
                vinylPlayer.Playing.transform.Rotate(Vector3.up, step, Space.World);
            }
            // First visit: the cat waves the player over until they talk to it.
            // Only while the cat isn't already saying something, and only before the story moves on.
            if (!talked && !cameraOffered && string.IsNullOrEmpty(shop.Line) && Time.time >= nextGreeting && Vector3.Distance(view.transform.position, shop.transform.position) < approachRadius)
            {
                nextGreeting = Time.time + 12f;
                shop.Speak("Hi there, diver! Come say hello: pinch or touch me to talk.", LuckyCat.Mood.Beckon, 4.5f);
            }

            // Highlight and tag: ray/hand hover, head gaze (headset) or the crosshair (desktop).
            ShopCounterItem target = null;
            if (!ReefToolMenu.BlocksWorldInput)
            {
                var t = view.transform;
                int n = Physics.SphereCastNonAlloc(new Ray(t.position, t.forward), shop.corals.CenterAimMode ? .005f : .03f, hits, gazeReach, ~0, QueryTriggerInteraction.Collide);
                float nearest = float.MaxValue;
                for (int k = 0; k < n; k++)
                {
                    var item = hits[k].collider.GetComponentInParent<ShopCounterItem>();
                    if (!item || item.isSelected || hits[k].distance >= nearest) continue;
                    if (item.Owned && !item.placeable) continue;
                    nearest = hits[k].distance; target = item;
                }
            }
            if (gazed != target) { if (gazed) gazed.SetAim(false); gazed = target; if (gazed) gazed.SetAim(true); }
            ShopCounterItem show = null;
            foreach (var item in new[] { cameraItem })
                if (item && !item.isSelected && (item.isHovered || item.Highlighted) && !item.Owned) { show = item; break; }
            ShowTag(view, show);

            if (show && shop.corals.CenterAimMode && Keyboard.current != null && Keyboard.current.gKey.wasPressedThisFrame)
            {
                if (show.PurchaseAllowed) OnItemTaken(show); else Refuse(show);
            }
            else if (show && !show.PurchaseAllowed && !shop.corals.CenterAimMode)
                for (int i = 0; i < 2; i++)
                    if (Hands.ReefInput.HandsMode ? Hands.ReefHands.Get(i).GrabDown : Hands.ReefInput.TalkDown(i)) { Refuse(show); break; }
        }

        void ShowTag(Camera view, ShopCounterItem item)
        {
            if (!item) { tag.Hide(); return; }
            var b = item.colliders.Count > 0 && item.colliders[0] ? item.colliders[0].bounds : new Bounds(item.transform.position, Vector3.one * .2f);
            if (item.price <= 0) tag.ShowLabel(b, view, "Gift");
            else tag.ShowPrice(b, view, item.price, CanAfford(item.price));
        }

        void Refuse(ShopCounterItem item)
        {
            if (Time.time < nextRefusal) return;
            nextRefusal = Time.time + 1.5f;
            int missing = item.price - (wallet ? wallet.Balance : 0);
            shop.Speak($"The {item.DisplayName.ToLowerInvariant()} is {item.price} scallops. {missing} more! Plant corals and take photos to earn some.", LuckyCat.Mood.Sad, 4f);
            if (wallet) wallet.ShowShortfall();
        }

        /// <summary>A prop standing on the counter, <paramref name="along"/> metres along it, facing the customer.</summary>
        GameObject Place(string prefab, float along)
        {
            var source = Resources.Load<GameObject>("Shop/" + prefab);
            if (!source) { Debug.LogWarning("[Counter] Missing Resources/Shop/" + prefab + " (Reefkeeper > Shop > Build counter props)"); return null; }
            var p = counterSpot + right * along - front * .17f;
            var rotation = Quaternion.LookRotation(front, Vector3.up);
            if (Physics.Raycast(p + Vector3.up * .8f, Vector3.down, out var hit, 1.6f, ~0, QueryTriggerInteraction.Ignore) && hit.collider.transform.IsChildOf(shop.transform))
            { p = hit.point; rotation = Quaternion.FromToRotation(Vector3.up, hit.normal) * rotation; }
            var go = Instantiate(source, p, rotation);
            go.name = prefab;
            return go;
        }

        ShopCounterItem Offer(string prefab, float along, ShopCounterItem.Kind kind, int price, bool placeable)
        {
            var go = Place(prefab, along);
            if (!go) return null;
            go.SetActive(false);
            var body = go.AddComponent<Rigidbody>(); body.isKinematic = true; body.useGravity = false;
            var item = go.AddComponent<ShopCounterItem>();
            item.kind = kind; item.price = price; item.placeable = placeable; item.progress = this;
            item.outlineMaterial = shop.outline;
            item.interactionManager = shop.corals.GetComponent<XRInteractionManager>();
            item.colliders.Clear();
            item.colliders.Add(go.GetComponent<BoxCollider>());
            go.SetActive(true);
            item.BuildOutline();
            IgnoreBodies(item);
            StartCoroutine(PopIn(go.transform));
            return item;
        }

        static void IgnoreBodies(ShopCounterItem item)
        {
            foreach (var body in FindObjectsByType<CharacterController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                foreach (var c in item.colliders) if (c && body) Physics.IgnoreCollision(body, c, true);
        }

        IEnumerator PopIn(Transform t)
        {
            var target = t.localScale;
            for (float k = 0; k < 1; k += Time.deltaTime / .45f)
            {
                float s = 1 + Mathf.Sin(k * Mathf.PI) * .25f * (1 - k);
                t.localScale = target * Mathf.Min(1, k * 1.6f) * s;
                yield return null;
            }
            t.localScale = target;
        }

        /// <summary>The camera goes into the player's gear: let go of it and shrink it away.</summary>
        IEnumerator Keep(ShopCounterItem item)
        {
            yield return null; // not from inside the select event
            if (!item) yield break;
            if (item.isSelected) item.interactionManager.CancelInteractableSelection((IXRSelectInteractable)item);
            item.enabled = false;
            var t = item.transform; var start = t.localScale;
            for (float k = 0; k < 1; k += Time.deltaTime / .35f)
            {
                t.localScale = start * (1 - k * k);
                t.Rotate(0, 540 * Time.deltaTime, 0, Space.World);
                yield return null;
            }
            Destroy(item.gameObject);
        }

        IEnumerator After(float seconds, System.Action action)
        {
            yield return new WaitForSeconds(seconds);
            action();
        }
    }
}
