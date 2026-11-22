using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace Reefkeeper.Corals
{
    public sealed class ShopRecord : XRGrabInteractable
    {
        public const int Price = 50;
        internal VinylPlayer player;
        internal ReefScallopReward wallet;
        internal CoralCart sales;
        internal MeshFilter sleeve;
        internal string productName;
        internal string unlockKey;
        internal bool owned;
        internal Vector3 home;
        internal Quaternion homeRotation;

        public override bool IsSelectableBy(IXRSelectInteractor interactor) =>
            !ReefToolMenu.BlocksWorldInput && (owned || (wallet && wallet.isActiveAndEnabled && wallet.Balance >= Price))
            && base.IsSelectableBy(interactor);

        public override void ProcessInteractable(XRInteractionUpdateOrder.UpdatePhase phase)
        {
            base.ProcessInteractable(phase);
            if (isSelected && player && phase == XRInteractionUpdateOrder.UpdatePhase.Dynamic)
                player.HighlightForHeldRecord(transform.position);
        }

        protected override void OnSelectEntered(SelectEnterEventArgs args)
        {
            base.OnSelectEntered(args);
            if (owned) return;
            if (!wallet || !wallet.TrySpend(Price,transform.position))
            {
                sales.Refuse(productName, Price);
                interactionManager.SelectExit(args.interactorObject,this);
                return;
            }
            owned=true;
            PlayerPrefs.SetInt(unlockKey,1);
            PlayerPrefs.Save();
        }

        protected override void OnSelectExited(SelectExitEventArgs args)
        {
            base.OnSelectExited(args);
            if (!args.isCanceled && player) player.Release(this);
        }
    }
}
