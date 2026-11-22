using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using Reefkeeper.Hands;

namespace Reefkeeper
{
    /// <summary>Empty-hand pinch near a solid surface stops locomotion, without constraining tracking.</summary>
    public sealed class ReefSurfaceHold : MonoBehaviour
    {
        public static ReefSurfaceHold Instance { get; private set; }
        const float Reach = .07f, BreakDistance = .25f, ResumeDistance = .08f;
        const int SurfaceMask = ~((1 << 2) | (1 << 4) | (1 << 5));
        readonly Collider[] overlaps = new Collider[24], held = new Collider[2];
        readonly Vector3[] contactLocal = new Vector3[2], contactWorld = new Vector3[2], resumePalm = new Vector3[2];
        readonly bool[] armed = new bool[2], consumed = new bool[2], resumePending = new bool[2], resumeSeeded = new bool[2];
        bool focused = true;
        public bool Holding => held[0] || held[1];
        public bool OwnsHand(int hand) => isActiveAndEnabled && consumed[hand];

        void Awake() { Instance = this; }
        void OnDestroy() { if (Instance == this) Instance = null; }
        void OnDisable() { ReleaseAll(); }
        void OnApplicationFocus(bool value) { focused = value; if (!value) ReleaseAll(); }

        public void ReleaseAll()
        {
            if (Holding) RememberResumePose();
            for (int i = 0; i < 2; i++) { held[i] = null; armed[i] = false; }
        }

        // Called before swimming and XRI selection, once per frame. No overlap query
        // while idle or holding: only on a newly closed, previously released pinch.
        public bool Step(Transform space)
        {
            bool wasHolding = Holding;
            bool allowed = focused && space && ReefInput.HandsMode && !ReefToolMenu.BlocksWorldInput;
            for (int i = 0; i < 2; i++)
            {
                var hand = ReefHands.Get(i);
                if (!hand.Tracked) resumeSeeded[i] = false;
                if (!hand.Tracked || !allowed)
                { held[i] = null; armed[i] = false; continue; }
                if (!hand.Pinch)
                { held[i] = null; consumed[i] = false; armed[i] = true; continue; }
                Vector3 point = space.TransformPoint(hand.PinchPoint);
                if (held[i])
                {
                    var surface = held[i];
                    if (!surface.enabled || !surface.gameObject.activeInHierarchy || ReefInput.HandBusy(i)
                        || Vector3.Distance(point, contactWorld[i]) > BreakDistance
                        || Vector3.Distance(surface.transform.TransformPoint(contactLocal[i]), contactWorld[i]) > .03f)
                        held[i] = null;
                    continue;
                }
                if (!hand.PinchDown || !armed[i] || consumed[i]) continue;
                armed[i] = false;
                if (ReefInput.HandBusy(i)) continue;
                var target = FindSurface(point);
                if (!target) continue;
                held[i] = target; consumed[i] = true;
                contactWorld[i] = point; contactLocal[i] = target.transform.InverseTransformPoint(point);
            }
            if (Holding || wasHolding)
            {
                RememberResumePose();
                return true;
            }
            return false;
        }

        Collider FindSurface(Vector3 point)
        {
            int count = Physics.OverlapSphereNonAlloc(point, Reach, overlaps, SurfaceMask, QueryTriggerInteraction.Collide);
            // A full buffer could conceal a pickup: fail closed instead of stealing its pinch.
            if (count == overlaps.Length) return null;
            Collider candidate = null;
            for (int n = 0; n < count; n++)
            {
                var c = overlaps[n];
                if (!c || c.transform.IsChildOf(transform)) continue;
                if (c.GetComponentInParent<XRBaseInteractable>() || c.GetComponentInParent<ReefBrush>()
                    || c.GetComponentInParent<Corals.LuckyCat>()) return null;
                if (c.isTrigger || c.attachedRigidbody) continue;
                candidate = c;
            }
            return candidate;
        }

        void RememberResumePose()
        {
            for (int i = 0; i < 2; i++)
            {
                var hand = ReefHands.Get(i);
                resumePending[i] = true; resumeSeeded[i] = hand.Tracked;
                resumePalm[i] = hand.Palm.position;
            }
        }

        // Only applies after a surface hold. An already extended arm must not
        // restart the old glide as soon as the player lets go.
        public bool CanSwim(int hand, ReefHands.State state)
        {
            if (!resumePending[hand]) return true;
            if (!state.Tracked) { resumeSeeded[hand] = false; return false; }
            if (!resumeSeeded[hand])
            { resumePalm[hand] = state.Palm.position; resumeSeeded[hand] = true; return false; }
            if (Vector3.Distance(state.Palm.position, resumePalm[hand]) < ResumeDistance) return false;
            resumePending[hand] = false;
            return true;
        }
    }
}
