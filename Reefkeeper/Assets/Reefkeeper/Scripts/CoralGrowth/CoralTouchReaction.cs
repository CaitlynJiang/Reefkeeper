using System.Linq;
using UnityEngine;
using Reefkeeper.Hands;

namespace Reefkeeper.Corals
{
    /// <summary>Contact-enter reaction for planted corals; no extra physics bodies or UI.</summary>
    [DisallowMultipleComponent, DefaultExecutionOrder(125)]
    public sealed class CoralTouchReaction : MonoBehaviour
    {
        [Min(0f)] public float touchDistance = .018f;
        [Min(0f)] public float releaseDistance = .06f;
        const float ReleaseSeconds = .15f, RetriggerSeconds = .3f;
        CoralNurture nurture;
        Transform stage;
        struct Shape { public MeshRenderer renderer; public Bounds bounds; public Matrix4x4 toCoral, fromCoral; }
        Shape[] shapes = new Shape[0];
        readonly bool[] armed = new bool[2];
        readonly float[] awayTime = new float[2];
        float nextReaction;
        bool focused = true;

        public void Bind(CoralNurture care) { nurture = care; stage = null; ResetContact(); }
        void OnDisable() => ResetContact();
        void OnApplicationFocus(bool value) { focused = value; if (!value) ResetContact(); }
        void ResetContact()
        {
            for (int i = 0; i < 2; i++) { armed[i] = false; awayTime[i] = 0f; }
        }

        void Update()
        {
            if (!focused || !nurture || !nurture.isActiveAndEnabled || !nurture.coral
                || !ReefInput.HandsMode || ReefToolMenu.BlocksWorldInput)
            { ResetContact(); return; }
            var space = ReefInput.HandTrackingSpace;
            if (!space) { ResetContact(); return; }
            for (int i = 0; i < 2; i++)
            {
                var hand = ReefHands.Get(i);
                bool free = !hand.Grab && !hand.MiddlePinch && !ReefInput.HandBusy(i);
                SampleContact(i, hand.Tracked, free, space.TransformPoint(hand.Palm.position),
                    space.TransformPoint(hand.IndexTip.position), Time.deltaTime);
            }
        }

        void SampleContact(int hand, bool tracked, bool free, Vector3 palm, Vector3 fingertip, float dt)
        {
            if (!tracked) { armed[hand] = false; awayTime[hand] = 0f; return; }
            float distance = Mathf.Min(DistanceToCoral(palm), DistanceToCoral(fingertip));
            if (distance > Mathf.Max(touchDistance, releaseDistance))
            {
                awayTime[hand] += Mathf.Max(0f, dt);
                if (awayTime[hand] >= ReleaseSeconds) armed[hand] = true;
                return;
            }
            awayTime[hand] = 0f;
            if (distance > touchDistance || !armed[hand]) return;
            armed[hand] = false;
            // Busy contact consumes the entry too: releasing a tool here must not
            // become a new poke until the hand has actually moved away and returned.
            if (!free || Time.time < nextReaction) return;
            nextReaction = Time.time + RetriggerSeconds;
            nurture.ReactToTouch();
        }

        float DistanceToCoral(Vector3 worldPoint)
        {
            var current = nurture.coral.Current;
            if (stage != current)
            {
                stage = current;
                shapes = stage ? stage.GetComponentsInChildren<MeshRenderer>()
                    .Where(r => r.GetComponentInParent<CoralGrowth>() == nurture.coral)
                    .Select(r =>
                    {
                        // Use the stage's resting shape. Its recoil must not make a
                        // stationary hand look as if it exited and touched again.
                        var rest = Matrix4x4.TRS(stage.localPosition, Quaternion.identity, Vector3.one)
                            * stage.worldToLocalMatrix * r.transform.localToWorldMatrix;
                        return new Shape { renderer = r, bounds = r.localBounds, toCoral = rest, fromCoral = rest.inverse };
                    }).ToArray() : new Shape[0];
            }
            float best = float.PositiveInfinity;
            Vector3 localPoint = nurture.coral.transform.InverseTransformPoint(worldPoint);
            foreach (var shape in shapes)
            {
                if (!shape.renderer || !shape.renderer.enabled || !shape.renderer.gameObject.activeInHierarchy) continue;
                Vector3 closest = shape.bounds.ClosestPoint(shape.fromCoral.MultiplyPoint3x4(localPoint));
                Vector3 worldClosest = nurture.coral.transform.TransformPoint(shape.toCoral.MultiplyPoint3x4(closest));
                best = Mathf.Min(best, (worldPoint - worldClosest).sqrMagnitude);
            }
            return Mathf.Sqrt(best);
        }
    }
}
