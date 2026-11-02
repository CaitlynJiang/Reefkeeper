using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace Reefkeeper
{
    [DefaultExecutionOrder(110)]
    public sealed class ReefBrushPickup : MonoBehaviour
    {
        public ReefWorkshop workshop;
        public ReefCoralPickup corals;
        public ReefBrushInteractable brush;
        public ReefPickupInteractor hand;
        public RackDirtPatch practiceSpot;
        public float grabReach = 3, scrubReach = 1.35f;
        public bool Automated { get; set; }
        public bool Carrying => hand && hand.isPerformingManualInteraction && brush.isSelected;
        public bool CenterMode => corals.CenterAimMode;
        public bool AimingBrush { get; private set; }
        public bool IsScrubbing { get; private set; }
        public bool SpotInReach { get; private set; }
        /// <summary>The moss / algae patch in scrub reach right now, if any.</summary>
        public RackDirtPatch Target { get; private set; }
        /// <summary>The patch the HUD should report on: the one in reach, else the last one scrubbed.</summary>
        public RackDirtPatch ActivePatch => Target ? Target : lastTarget ? lastTarget : practiceSpot;
        public float Progress => ActivePatch.Cleaned;
        RackDirtPatch lastTarget;
        public Camera View => corals.View;
        readonly RaycastHit[] hits = new RaycastHit[64];
        Vector3 tipLocal;
        float strokePhase;
        bool posed, quitting;

        void Start() { tipLocal = brush.transform.InverseTransformPoint(workshop.brush.bristles.position); }
        void Update()
        {
            if (!View) return;
            // Native controller mode retains real tracked brush motion.
            if (Carrying && !CenterMode) workshop.ReturnBrush();
            bool canAct = CenterMode && (Automated || workshop.xrGroup.activeSelf || Cursor.lockState == CursorLockMode.Locked);
            var hit = FindVisible(grabReach);
            AimingBrush = canAct && !brush.isSelected && hit.collider && hit.collider.GetComponentInParent<ReefBrushInteractable>() == brush;
            brush.SetAim(AimingBrush);
            if (AimingBrush && Keyboard.current != null && Keyboard.current.gKey.wasPressedThisFrame) Grab();
            var patch = hit.collider ? PatchOf(hit.collider) : null;
            SpotInReach = canAct && patch && !patch.Complete && hit.distance <= scrubReach;
            Target = SpotInReach ? patch : null;
            if (Target) lastTarget = Target;
            bool heldButton = Mouse.current != null && Mouse.current.leftButton.isPressed;
            IsScrubbing = Carrying && canAct && SpotInReach && !Target.Complete && heldButton;
            if (Carrying) Pose(); else posed = false;
            if (Carrying && Keyboard.current != null && Keyboard.current.hKey.wasPressedThisFrame) workshop.ReturnBrush();
        }
        // A patch hit directly, or the moss on the socket of a planted coral.
        static RackDirtPatch PatchOf(Collider c)
        {
            var patch = c.GetComponent<RackDirtPatch>();
            if (patch) return patch;
            var socket = c.GetComponentInParent<Corals.RackSocket>();
            return socket ? socket.moss : null;
        }
        RaycastHit FindVisible(float distance)
        {
            int count = Physics.RaycastNonAlloc(new Ray(View.transform.position, View.transform.forward), hits, distance, ~0, QueryTriggerInteraction.Collide);
            var nearest = new RaycastHit(); float best = float.PositiveInfinity;
            for (int i = 0; i < count; i++)
            {
                var c = hits[i].collider;
                if (c.transform.IsChildOf(workshop.desktopPlayer.transform) || c.transform.IsChildOf(workshop.xrGroup.transform)) continue;
                if (brush.isSelected && c.transform.IsChildOf(brush.transform)) continue;
                if (corals.Held && c.transform.IsChildOf(corals.Held.transform)) continue;
                if (c.isTrigger && !c.GetComponent<RackDirtPatch>()) continue;
                if (hits[i].distance < best) { best = hits[i].distance; nearest = hits[i]; }
            }
            return nearest;
        }
        public void Grab()
        {
            if (!AimingBrush || brush.isSelected) return;
            corals.ClearHeld();
            hand.transform.SetParent(transform, true);
            SetIdlePose(); posed = true;
            hand.Requested = brush;
            hand.StartManualInteraction((IXRSelectInteractable)brush);
            AimingBrush = false; brush.SetAim(false);
        }
        public void Release()
        {
            IsScrubbing = false; posed = false;
            if (hand && hand.isPerformingManualInteraction) hand.EndManualInteraction();
            if (hand) hand.Requested = null;
        }
        void SetIdlePose()
        {
            hand.transform.SetPositionAndRotation(View.transform.TransformPoint(new Vector3(0.22f, -0.2f, 0.4f)),
                View.transform.rotation * Quaternion.Euler(-12, -18, -14));
        }
        void Pose()
        {
            Vector3 targetPosition;
            Quaternion targetRotation;
            if (IsScrubbing)
            {
                strokePhase += Time.deltaTime * 2 * Mathf.PI * 2.2f;
                var surface = (Target ? Target : practiceSpot).transform;
                var forward = Vector3.ProjectOnPlane(View.transform.forward, surface.up).normalized;
                if (forward.sqrMagnitude < 0.01f) forward = surface.forward;
                targetRotation = Quaternion.LookRotation(forward, surface.up);
                // Two short tangent strokes move the actual bristles across the contact collider.
                Vector3 tip = surface.position + surface.up * 0.025f
                    + surface.right * (Mathf.Sin(strokePhase) * 0.075f)
                    + surface.forward * (Mathf.Sin(strokePhase * 1.6f) * 0.025f);
                targetPosition = tip - targetRotation * tipLocal;
            }
            else
            {
                strokePhase = 0;
                targetPosition = View.transform.TransformPoint(new Vector3(0.22f, -0.2f, 0.4f));
                targetRotation = View.transform.rotation * Quaternion.Euler(-12, -18, -14);
            }
            float blend = posed ? 1 - Mathf.Exp(-18 * Time.deltaTime) : 1;
            hand.transform.position = Vector3.Lerp(hand.transform.position, targetPosition, blend);
            hand.transform.rotation = Quaternion.Slerp(hand.transform.rotation, targetRotation, blend);
            posed = true;
        }
        void OnApplicationQuit() { quitting = true; }
        void OnDisable() { if (quitting) return; Release(); if (brush) brush.SetAim(false); }
    }
}
