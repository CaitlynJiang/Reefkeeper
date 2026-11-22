using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace Reefkeeper.Corals
{
    /// <summary>
    /// Something Lucky Cat puts on the counter: a gift (price 0) or an item for sale. Bare hands take
    /// it with a pinch, like the brush. An item the player can't afford can't be picked up. Taking it
    /// hands it to <see cref="LuckyCatProgress"/>, which decides what happens (the camera is kept as
    /// gear; the record player becomes the player's). A <see cref="placeable"/> item the player owns
    /// can be picked up again and set down anywhere: let go and it settles onto the surface below.
    /// </summary>
    public sealed class ShopCounterItem : XRGrabInteractable
    {
        public enum Kind { Camera, RecordPlayer }
        public Kind kind;
        [Tooltip("Scallops; 0 means a gift.")] public int price;
        [Tooltip("Once owned, can be picked up and set down anywhere.")] public bool placeable;
        public LuckyCatProgress progress;
        public Material outlineMaterial;

        public bool Owned { get; set; }
        public bool Highlighted { get; private set; }
        public string DisplayName => kind == Kind.Camera ? "Instant camera" : "Record player";

        readonly List<Renderer> outlines = new List<Renderer>();
        bool aimed;

        protected override void Awake()
        {
            movementType = MovementType.Instantaneous;
            throwOnDetach = false;
            useDynamicAttach = true;
            retainTransformParent = false;
            selectMode = InteractableSelectMode.Single;
            base.Awake();
        }

        /// <summary>Call once the outline material is set: builds the highlight shells.</summary>
        public void BuildOutline()
        {
            foreach (var mesh in GetComponentsInChildren<MeshFilter>(false))
            {
                if (mesh.name == "Shop outline") continue;
                var shell = new GameObject("Shop outline");
                shell.transform.SetParent(mesh.transform, false);
                shell.AddComponent<MeshFilter>().sharedMesh = mesh.sharedMesh;
                var r = shell.AddComponent<MeshRenderer>();
                r.sharedMaterial = outlineMaterial; r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false;
                r.enabled = false;
                outlines.Add(r);
            }
        }

        public bool PurchaseAllowed => Owned || isSelected || price <= 0 || !progress || progress.CanAfford(price);

        public override bool IsSelectableBy(IXRSelectInteractor interactor) =>
            PurchaseAllowed &&
            (!Hands.ReefInput.TryGetBareHand(interactor, out int hand) || Hands.ReefHands.Get(hand).Pinch) &&
            !ReefToolMenu.BlocksWorldInput && base.IsSelectableBy(interactor);

        public void SetAim(bool value) { aimed = value; Refresh(); }
        void Refresh()
        {
            Highlighted = !isSelected && (aimed || isHovered);
            foreach (var r in outlines) if (r) r.enabled = Highlighted;
        }
        protected override void OnHoverEntered(HoverEnterEventArgs args) { base.OnHoverEntered(args); Refresh(); }
        protected override void OnHoverExited(HoverExitEventArgs args) { base.OnHoverExited(args); Refresh(); }

        protected override void OnSelectEntered(SelectEnterEventArgs args)
        {
            base.OnSelectEntered(args);
            foreach (var c in colliders) if (c) c.isTrigger = true;
            aimed = false; Refresh();
            if (progress) progress.OnItemTaken(this);
        }

        protected override void OnSelectExited(SelectExitEventArgs args)
        {
            base.OnSelectExited(args);
            foreach (var c in colliders) if (c) c.isTrigger = false;
            Refresh();
            if (Owned && placeable) Settle();
        }

        /// <summary>Upright, and resting on the surface just below if there is one (otherwise it stays where it was let go).</summary>
        public void Settle()
        {
            var t = transform;
            var yaw = Quaternion.Euler(0, t.eulerAngles.y, 0);
            t.rotation = yaw;
            var hits = Physics.RaycastAll(t.position + Vector3.up * .3f, Vector3.down, 1.2f, ~0, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue; RaycastHit floor = default; bool found = false;
            foreach (var h in hits)
            {
                if (h.collider.transform.IsChildOf(t) || h.collider.GetComponentInParent<CharacterController>()) continue;
                if (h.distance < best) { best = h.distance; floor = h; found = true; }
            }
            if (found && floor.point.y > t.position.y - .9f)
                t.SetPositionAndRotation(floor.point, Quaternion.FromToRotation(Vector3.up, floor.normal) * yaw);
        }
    }
}
