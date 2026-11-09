using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace Reefkeeper
{
    public sealed class ReefBrushInteractable : XRGrabInteractable
    {
        public ReefBrushPickup owner;
        public Material outlineMaterial;
        readonly List<Renderer> outlines = new List<Renderer>();
        bool aimed;
        public bool Highlighted { get; private set; }
        protected override void Awake()
        {
            base.Awake();
            foreach (var mesh in GetComponentsInChildren<MeshFilter>())
            {
                var shell = new GameObject("Brush pickup outline");
                shell.transform.SetParent(mesh.transform, false);
                shell.AddComponent<MeshFilter>().sharedMesh = mesh.sharedMesh;
                var renderer = shell.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = outlineMaterial;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.enabled = false; outlines.Add(renderer);
            }
        }
        public void SetAim(bool value) { aimed = value; Refresh(); }
        void Refresh()
        {
            Highlighted = !isSelected && (aimed || isHovered);
            foreach (var shell in outlines) shell.enabled = Highlighted;
        }
        public override bool IsSelectableBy(IXRSelectInteractor interactor) =>
            (!ReefToolMenu.BlocksWorldInput || isSelected || owner && owner.MenuEquipping && ReferenceEquals(interactor, owner.hand)) &&
            (isSelected || !owner || !owner.CenterMode || ReferenceEquals(interactor, owner.hand)) && base.IsSelectableBy(interactor);
        public override bool IsHoverableBy(IXRHoverInteractor interactor) =>
            !ReefToolMenu.BlocksWorldInput &&
            (!owner || !owner.CenterMode || ReferenceEquals(interactor, owner.hand)) && base.IsHoverableBy(interactor);
        protected override void OnHoverEntered(HoverEnterEventArgs args) { base.OnHoverEntered(args); Refresh(); }
        protected override void OnHoverExited(HoverExitEventArgs args) { base.OnHoverExited(args); Refresh(); }
        protected override void OnSelectEntered(SelectEnterEventArgs args)
        {
            base.OnSelectEntered(args);
            if (owner) owner.OnToolSelected(this);
            foreach (var c in colliders) c.isTrigger = true;
            aimed = false; Refresh();
        }
        protected override void OnSelectExited(SelectExitEventArgs args)
        {
            base.OnSelectExited(args);
            foreach (var c in colliders) c.isTrigger = false;
            Refresh();
        }
    }
}
