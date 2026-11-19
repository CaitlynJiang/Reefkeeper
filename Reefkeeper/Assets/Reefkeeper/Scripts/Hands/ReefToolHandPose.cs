using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Transformers;

namespace Reefkeeper.Hands
{
    /// <summary>Final tool pose for tracked hands; the normal transformer still handles controllers.</summary>
    public sealed class ReefToolHandPose : XRBaseGrabTransformer
    {
        Quaternion filteredRotation;
        bool initialized;
        int sampledFrame = -1;

        protected override RegistrationMode registrationMode => RegistrationMode.None;

        public override void OnGrab(XRGrabInteractable grabInteractable)
        {
            initialized = false;
            sampledFrame = -1;
        }

        public static Pose GripPose(Pose palm, Vector3 pinchPoint)
        {
            Vector3 gripPoint = palm.position + palm.rotation * Vector3.down * .03f;
            Vector3 adjustedPinchPoint = pinchPoint + palm.rotation * Vector3.up * .03f;
            Vector3 forward = adjustedPinchPoint - gripPoint;
            if (forward.sqrMagnitude < .0004f) forward = palm.rotation * Vector3.forward;
            forward.Normalize();
            Vector3 up = Vector3.ProjectOnPlane(palm.rotation * Vector3.up, forward);
            if (up.sqrMagnitude < .01f) up = Vector3.Cross(forward, palm.rotation * Vector3.right);
            // Visual grip calibration only; gesture detection keeps the raw joint positions.
            return new Pose(gripPoint,
                Quaternion.LookRotation(forward, up.normalized));
        }

        public override void Process(XRGrabInteractable tool, XRInteractionUpdateOrder.UpdatePhase phase,
            ref Pose targetPose, ref Vector3 localScale)
        {
            if (tool.interactorsSelecting.Count != 1
                || !ReefInput.TryGetBareHand(tool.interactorsSelecting[0], out int hand)) return;
            var state = ReefHands.Get(hand);
            var space = ReefInput.HandTrackingSpace;
            if (!state.Tracked || !space) return;
            var grip = GripPose(state.Palm, state.PinchPoint);
            // Filter in tracking space so swimming/turning the rig adds no drag to the tool.
            // Reuse the sample in BeforeRender rather than smoothing twice in one frame.
            if (sampledFrame != Time.frameCount)
            {
                filteredRotation = initialized
                    ? Quaternion.Slerp(filteredRotation, grip.rotation, 1 - Mathf.Exp(-35 * Time.unscaledDeltaTime))
                    : grip.rotation;
                sampledFrame = Time.frameCount;
                initialized = true;
            }
            var attach = tool.attachTransform ? tool.attachTransform : tool.transform;
            Quaternion attachRotation = Quaternion.Inverse(tool.transform.rotation) * attach.rotation;
            Vector3 attachOffset = tool.transform.InverseTransformPoint(attach.position);
            targetPose.rotation = space.rotation * filteredRotation * Quaternion.Inverse(attachRotation);
            targetPose.position = space.TransformPoint(grip.position)
                - targetPose.rotation * Vector3.Scale(attachOffset, tool.transform.lossyScale);
        }
    }
}
