using Reefkeeper.Hands;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace Reefkeeper
{
    /// <summary>Short, damped throws from tracked hands/controllers; locomotion never adds launch speed.</summary>
    [DefaultExecutionOrder(1000)]
    [DisallowMultipleComponent]
    public sealed class ReefUnderwaterThrow : MonoBehaviour
    {
        public float minimumSpeed = .55f;
        public float maximumSpeed = 1.8f;
        public float waterDamping = 2.5f;
        public bool ReleasedFromTrackedInput { get; private set; }
        public bool Flying { get; private set; }

        XRGrabInteractable grab;
        Rigidbody body;
        Transform space, device;
        int hand = -1;
        bool bare, pending, settlePending;
        Vector3 launch, spin;
        float flightTime, quietTime;
        float originalDamping, originalAngularDamping;
        CollisionDetectionMode originalCollision;
        readonly Pose[] poses = new Pose[24];
        readonly float[] times = new float[24];
        int count, next;

        public static ReefUnderwaterThrow Install(XRGrabInteractable target, bool paper = false)
        {
            var result = target.GetComponent<ReefUnderwaterThrow>();
            if (!result) result = target.gameObject.AddComponent<ReefUnderwaterThrow>();
            result.Bind(target);
            if (paper) { result.maximumSpeed = 1.2f; result.waterDamping = 3.5f; }
            return result;
        }

        void Bind(XRGrabInteractable target)
        {
            if (grab) return;
            grab = target; body = GetComponent<Rigidbody>();
            originalDamping = body.linearDamping;
            originalAngularDamping = body.angularDamping;
            originalCollision = body.collisionDetectionMode;
            grab.throwOnDetach = false;
            grab.selectEntered.AddListener(Grabbed);
            grab.selectExited.AddListener(Released);
        }

        void Grabbed(SelectEnterEventArgs args)
        {
            StopMotion();
            bare = ReefInput.TryGetBareHand(args.interactorObject, out hand);
            if (!bare && !ReefInput.HandsMode)
                for (int i = 0; i < 2; i++)
                {
                    var root = ReefInput.DeviceRoot(i);
                    if (root && args.interactorObject.transform.IsChildOf(root))
                    { hand = i; device = root; break; }
                }
            space = ReefInput.HandTrackingSpace;
            if (ReadPose(out var pose)) Sample(pose, Time.unscaledTime);
        }

        bool ReadPose(out Pose pose)
        {
            pose = default;
            if (hand < 0 || !space) return false;
            if (bare)
            {
                var state = ReefHands.Get(hand);
                if (!ReefInput.HandsMode || !state.Tracked) return false;
                pose = state.Palm;
                return true;
            }
            var input = InputDevices.GetDeviceAtXRNode(hand == 0 ? XRNode.LeftHand : XRNode.RightHand);
            if (ReefInput.HandsMode || !device || !device.gameObject.activeInHierarchy ||
                !input.TryGetFeatureValue(CommonUsages.isTracked, out bool tracked) || !tracked) return false;
            pose = new Pose(space.InverseTransformPoint(device.position), Quaternion.Inverse(space.rotation) * device.rotation);
            return true;
        }

        // Fixed ring buffer: no allocations while sampling. Tracking jumps invalidate the whole gesture.
        void Sample(Pose pose, float now)
        {
            if (count > 0)
            {
                int last = (next + poses.Length - 1) % poses.Length;
                float dt = now - times[last];
                if (dt <= .0001f) return;
                if (dt > .08f || Vector3.Distance(pose.position, poses[last].position) / dt > 6f ||
                    Quaternion.Angle(pose.rotation, poses[last].rotation) / dt > 1800f) count = 0;
            }
            poses[next] = pose; times[next] = now;
            next = (next + 1) % poses.Length; count = Mathf.Min(count + 1, poses.Length);
        }

        bool Estimate(float now, out Vector3 velocity, out Vector3 angular)
        {
            velocity = angular = Vector3.zero;
            if (count < 2) return false;
            int newest = (next + poses.Length - 1) % poses.Length, oldest = newest;
            if (now - times[newest] > .05f) return false;
            for (int i = 1; i < count; i++)
            {
                int index = (newest + poses.Length - i) % poses.Length;
                if (times[newest] - times[index] > .12f) break;
                oldest = index;
            }
            float span = times[newest] - times[oldest];
            if (span < .055f) return false;
            velocity = (poses[newest].position - poses[oldest].position) / span;
            if (velocity.magnitude < minimumSpeed) { velocity = Vector3.zero; return false; }
            velocity = Vector3.ClampMagnitude(velocity * .85f, maximumSpeed);
            var rotation = poses[newest].rotation * Quaternion.Inverse(poses[oldest].rotation);
            rotation.ToAngleAxis(out float angle, out var axis);
            if (angle > 180f) angle -= 360f;
            if (axis.sqrMagnitude > .0001f && !float.IsInfinity(axis.x) && !float.IsNaN(axis.x))
                angular = Vector3.ClampMagnitude(axis.normalized * (angle * Mathf.Deg2Rad / span), 4f);
            return true;
        }

        void Released(SelectExitEventArgs args)
        {
            settlePending = !grab.isSelected;
            pending = false;
            ReleasedFromTrackedInput = hand >= 0 && !args.isCanceled;
            if (!ReleasedFromTrackedInput || !isActiveAndEnabled || grab.isSelected || ReefToolMenu.BlocksWorldInput || !ReadPose(out var pose)) return;
            Sample(pose, Time.unscaledTime);
            if (Estimate(Time.unscaledTime, out var velocity, out var angular))
            {
                launch = space.TransformDirection(velocity); spin = space.TransformDirection(angular);
                pending = true;
            }
        }

        void LateUpdate()
        {
            if (!grab || !body) return;
            if (grab.isSelected)
            {
                if (grab.interactorsSelecting.Count == 1 && !ReefToolMenu.BlocksWorldInput && ReadPose(out var pose)) Sample(pose, Time.unscaledTime);
                else count = 0;
                return;
            }
            if (!settlePending) return;
            settlePending = false;
            // Planting, auto-deposit, recalls and canceled selections take precedence over flight.
            if (pending && grab.isActiveAndEnabled && !ReefToolMenu.BlocksWorldInput) BeginFlight(launch, spin);
            else StopMotion();
            pending = false;
        }

        void BeginFlight(Vector3 velocity, Vector3 angular)
        {
            if (!body || !grab || grab.isSelected || !grab.isActiveAndEnabled) return;
            foreach (var collider in grab.colliders) if (collider) collider.isTrigger = false;
            // Only on release, never a per-frame scene scan. Small props must not push the swimmer.
            foreach (var controller in FindObjectsByType<CharacterController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                foreach (var collider in grab.colliders) if (collider) Physics.IgnoreCollision(collider, controller);
            body.isKinematic = false; body.useGravity = false;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            body.linearDamping = waterDamping; body.angularDamping = 4f;
            body.linearVelocity = Vector3.ClampMagnitude(velocity, maximumSpeed);
            body.angularVelocity = Vector3.ClampMagnitude(angular, 4f);
            body.WakeUp(); Flying = true; flightTime = quietTime = 0;
        }

        void FixedUpdate()
        {
            if (!Flying || !body) return;
            flightTime += Time.fixedDeltaTime;
            quietTime = body.linearVelocity.sqrMagnitude < .0025f && body.angularVelocity.sqrMagnitude < .01f ? quietTime + Time.fixedDeltaTime : 0;
            if (quietTime > .3f || flightTime > 4f) StopMotion();
        }

        /// <summary>Call before a recall, planting or scripted photo collection takes ownership.</summary>
        public void StopMotion()
        {
            pending = settlePending = Flying = ReleasedFromTrackedInput = false;
            count = next = 0; hand = -1; device = null;
            if (!body) return;
            if (!body.isKinematic) { body.linearVelocity = Vector3.zero; body.angularVelocity = Vector3.zero; }
            body.isKinematic = true;
            body.linearDamping = originalDamping; body.angularDamping = originalAngularDamping;
            body.collisionDetectionMode = originalCollision;
        }

        void OnDisable() => StopMotion();
        void OnApplicationFocus(bool focused) { if (!focused) StopMotion(); }
        void OnDestroy()
        {
            if (!grab) return;
            grab.selectEntered.RemoveListener(Grabbed); grab.selectExited.RemoveListener(Released);
        }
    }
}
