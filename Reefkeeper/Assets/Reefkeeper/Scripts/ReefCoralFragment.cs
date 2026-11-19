using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace Reefkeeper
{
    public sealed class ReefCoralFragment : XRGrabInteractable
    {
        public ReefCoralPickup owner;
        public Material outlineMaterial;
        public GameObject suspensionLine;
        readonly List<Renderer> outlines = new List<Renderer>();
        bool aimed;
        public bool Highlighted { get; private set; }

        protected override void Awake()
        {
            base.Awake();
            foreach (var mesh in GetComponentsInChildren<MeshFilter>(true))
            {
                if (mesh.name == "Pickup outline") continue;
                var child = mesh.transform.Find("Pickup outline");
                if (!child)
                {
                    child = new GameObject("Pickup outline").transform;
                    child.SetParent(mesh.transform, false);
                    child.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh.sharedMesh;
                    var renderer = child.gameObject.AddComponent<MeshRenderer>();
                    renderer.sharedMaterial = outlineMaterial;
                    renderer.shadowCastingMode = ShadowCastingMode.Off;
                    renderer.receiveShadows = false;
                }
                outlines.Add(child.GetComponent<Renderer>());
            }
            RefreshOutline();
        }
        public void SetAim(bool value) { aimed = value; RefreshOutline(); }
        // G also drives the simulator's right grip in FPS mode. Give center aim
        // exclusive ownership there so one key press cannot grab two fragments.
        public override bool IsSelectableBy(IXRSelectInteractor interactor) =>
            (!ReefToolMenu.BlocksWorldInput || isSelected) &&
            (isSelected || !owner || !owner.CenterAimMode || ReferenceEquals(interactor, owner.hand)) && base.IsSelectableBy(interactor);
        public override bool IsHoverableBy(IXRHoverInteractor interactor) =>
            !ReefToolMenu.BlocksWorldInput &&
            (!owner || !owner.CenterAimMode || ReferenceEquals(interactor, owner.hand)) && base.IsHoverableBy(interactor);
        void RefreshOutline()
        {
            Highlighted = (aimed || isHovered) && !isSelected && (!owner || owner.Held != this);
            foreach (var renderer in outlines) if (renderer) renderer.enabled = Highlighted;
        }
        protected override void OnHoverEntered(HoverEnterEventArgs args) { base.OnHoverEntered(args); RefreshOutline(); }
        protected override void OnHoverExited(HoverExitEventArgs args) { base.OnHoverExited(args); RefreshOutline(); }
        protected override void OnSelectEntered(SelectEnterEventArgs args)
        {
            base.OnSelectEntered(args);
            owner.Claim(this);
            // Native XRI casts find solid colliders; carried fragments should not push the swimmer.
            foreach (var collider in colliders) collider.isTrigger = true;
            if (suspensionLine) suspensionLine.SetActive(false);
            aimed = false;
            RefreshOutline();
        }
        protected override void OnSelectExited(SelectExitEventArgs args)
        {
            base.OnSelectExited(args);
            // A released simulator grip transfers to the persistent single-item slot.
            if (owner) owner.KeepInHand(this, args.interactorObject.transform);
            RefreshOutline();
        }
    }
}
