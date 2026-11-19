// Historical fixture for the experimental proportional-gesture / comfort version.
// The current comparison build intentionally rolls those APIs back. Do not run
// this fixture against it; see README.md and CheckHandControls.cs instead.
using System;
using System.Reflection;
using System.Text;
using Unity.Collections;
using UnityEngine;
using UnityEngine.XR.Hands;
using UnityEngine.XR.Hands.Processing;
using UnityEngine.XR.Hands.ProviderImplementation;
using UnityEngine.SceneManagement;
using Reefkeeper;
using Reefkeeper.Hands;

public static class CheckSwimHandShapes
{
    const BindingFlags Flags = BindingFlags.Static | BindingFlags.NonPublic;
    static void Require(bool ok, string message) { if (!ok) throw new Exception(message); }
    public static void Joint(XRHand hand, XRHandJointID id, Vector3 position, Quaternion rotation, bool valid = true)
    {
        var joints = hand.GetRawJointArray();
        joints[id.ToIndex()] = XRHandProviderUtility.CreateJoint(hand.handedness,
            valid ? XRHandJointTrackingState.Pose : XRHandJointTrackingState.None, id, new Pose(position, rotation));
    }
    public static XRHand Create(Handedness side)
    {
        object boxed = XRHandProviderUtility.CreateHand(side, Allocator.Persistent);
        typeof(XRHand).GetProperty("isTracked").SetValue(boxed, true);
        return (XRHand)boxed;
    }
    public static void Shape(XRHand hand, float size, bool fist, Pose palm)
    {
        float side = hand.handedness == Handedness.Left ? 1 : -1;
        Joint(hand, XRHandJointID.Palm, palm.position, palm.rotation);
        Joint(hand, XRHandJointID.Wrist, palm.position + palm.rotation * Vector3.back * .04f * size, palm.rotation);
        Joint(hand, XRHandJointID.ThumbTip, palm.position + palm.rotation * new Vector3(side * .055f, -.005f, .025f) * size, palm.rotation);
        var starts = new[] { XRHandJointID.IndexProximal, XRHandJointID.MiddleProximal, XRHandJointID.RingProximal, XRHandJointID.LittleProximal };
        float[] xs = { .025f, .008f, -.012f, -.03f }, lengths = { .065f, .075f, .068f, .05f };
        for (int f = 0; f < 4; f++)
        {
            var p = new Vector3(side * xs[f], 0, .025f);
            for (int j = 0; j < 4; j++)
            {
                Joint(hand, starts[f] + j, palm.position + palm.rotation * p * size, palm.rotation);
                if (fist)
                    p += (j == 0 ? Vector3.down : j == 1 ? Vector3.back : Vector3.up) * lengths[f] / 3;
                else p += Vector3.forward * lengths[f] / 3;
            }
        }
    }
    public static ReefHands.State Read(XRHand hand, ReefHands.State previous = default)
    {
        object[] args = { previous, hand, true };
        typeof(ReefHands).GetMethod("Update", Flags).Invoke(null, args);
        return (ReefHands.State)args[0];
    }
    public static string Main()
    {
        var output = new StringBuilder();
        foreach (var side in new[] { Handedness.Left, Handedness.Right })
        {
            var hand = Create(side);
            try
            {
                foreach (float size in new[] { .75f, 1f, 1.25f })
                foreach (var rotation in new[] { Quaternion.identity, Quaternion.Euler(45, 70, 20) })
                {
                    var palm = new Pose(new Vector3(.2f, 1.2f, .4f), rotation);
                    Shape(hand, size, false, palm);
                    var open = Read(hand);
                    Shape(hand, size, true, palm);
                    var fist = Read(hand, open);
                    Shape(hand, size, false, palm);
                    var released = Read(hand, fist);
                    Require(open.OpenPalm && !open.Fist && fist.Fist && !fist.OpenPalm && released.OpenPalm && !released.Fist,
                        $"Hand classification failed: {side}, size {size}");
                    output.AppendLine($"{side} size={size} rotated={rotation != Quaternion.identity}: open={open.OpenPalm}, falseFist={open.Fist}, fist={fist.Fist}, reopened={released.OpenPalm}, stuckFist={released.Fist}");
                    Joint(hand, XRHandJointID.LittleIntermediate, Vector3.zero, rotation, false);
                    Require(Read(hand).OpenPalm, "One occluded finger vetoed an open hand");
                    Joint(hand, XRHandJointID.RingIntermediate, Vector3.zero, rotation, false);
                    var missing = Read(hand, fist);
                    Require(!missing.OpenPalm && !missing.Fist, "Insufficient joints inferred a gesture");
                }
            }
            finally { XRHandProviderUtility.DisposeHand(hand); }
        }
        Require(Application.isPlaying, "Run in Play mode for movement checks");
        if (DiveCardMock.Instance) DiveCardMock.Instance.SetOpen(false);
        var scene = SceneManager.CreateScene("Swim joint regression", new CreateSceneParameters(LocalPhysicsMode.Physics3D));
        var rig = new GameObject("Swim joint test"); SceneManager.MoveGameObjectToScene(rig, scene);
        rig.transform.position = new Vector3(0, 4, 0);
        rig.AddComponent<CharacterController>();
        var swim = rig.AddComponent<ReefXRSwim>(); swim.enabled = false;
        var camera = new GameObject("Camera"); camera.transform.SetParent(rig.transform, false);
        camera.transform.localPosition = new Vector3(0, 1.6f, 0);
        swim.view = camera.AddComponent<Camera>(); swim.view.enabled = false;
        var left = Create(Handedness.Left); var right = Create(Handedness.Right);
        var tick = typeof(ReefXRSwim).GetMethod("HandSwimStep", BindingFlags.Instance | BindingFlags.NonPublic);
        var states = (ReefHands.State[])typeof(ReefHands).GetField("states", Flags).GetValue(null);
        try
        {
            foreach (float size in new[] { .75f, 1f, 1.25f })
            foreach (int fps in new[] { 30, 72, 90 })
            {
                float dt = 1f / fps;
                Action step = () => {
                    typeof(ReefHands).GetField("frame", Flags).SetValue(null, Time.frameCount);
                    tick.Invoke(swim, new object[] { dt });
                };
                Action<float, Quaternion, bool> pose = (z, rotation, fist) => {
                    Shape(left, size, fist, new Pose(new Vector3(-.12f, 1.45f, z), rotation));
                    states[0] = Read(left, states[0]); states[1] = default;
                };
                swim.LatchHandBrake();
                // A real backward palm sweep, using classification from full joints.
                var strokeRotation = Quaternion.Euler(90, 0, 0);
                pose(.7f, strokeRotation, false); step();
                float startZ = rig.transform.position.z;
                for (int i = 1; i <= fps / 2; i++) { pose(.7f - .9f * i * dt, strokeRotation, false); step(); }
                Require(!swim.HandBrakeLatched && swim.Velocity.z > .02f && rig.transform.position.z > startZ + .002f,
                    $"Joint-driven stroke blocked: size={size}, fps={fps}, speed={swim.Velocity}");
                // Empty fists stop immediately. Opening them at full reach does not resume.
                Shape(left, size, true, new Pose(new Vector3(-.12f, 1.45f, .65f), Quaternion.identity));
                Shape(right, size, true, new Pose(new Vector3(.12f, 1.45f, .65f), Quaternion.identity));
                states[0] = Read(left, states[0]); states[1] = Read(right); step();
                Require(swim.HandBrakeLatched && swim.Velocity == Vector3.zero, "Joint fists failed to stop immediately");
                for (int i = 0; i < fps * 2; i++) { pose(.65f, Quaternion.identity, false); step(); }
                Require(swim.HandBrakeLatched && swim.Velocity == Vector3.zero, "Opening fists resumed travel");
                pose(.2f, Quaternion.identity, false); step();
                for (int i = 1; i <= fps; i++) { pose(.2f + .45f * i * dt, Quaternion.identity, false); step(); }
                for (int i = 0; i < fps * 3; i++) { pose(.65f, Quaternion.identity, false); step(); }
                Require(swim.Gliding && swim.Velocity.z > .2f, $"Fresh joint-driven reach failed: size={size}, fps={fps}");
                float glideSpeed = swim.Velocity.magnitude;
                swim.LatchHandBrake();
                pose(.2f, Quaternion.identity, false); step();
                for (int i = 0; i < fps * 2; i++) { pose(.2f + .003f * Mathf.Sin(i), Quaternion.identity, false); step(); }
                Require(swim.Velocity == Vector3.zero, "Small hand jitter generated swimming");
                // An outward push with palm facing forward gives backward travel.
                var reverseRotation = Quaternion.Euler(-90, 0, 0);
                pose(.2f, reverseRotation, false); step();
                for (int i = 1; i <= fps / 2; i++) { pose(.2f + .9f * i * dt, reverseRotation, false); step(); }
                Require(swim.Velocity.z < -.02f, "Joint-driven backward stroke blocked");
                output.AppendLine($"PASS motion size={size}, {fps}fps: stroke, immediate fist brake, no auto-resume, fresh glide ({glideSpeed:F3}m/s), jitter rejection, reverse stroke.");
            }
        }
        finally
        {
            XRHandProviderUtility.DisposeHand(left); XRHandProviderUtility.DisposeHand(right);
            UnityEngine.Object.Destroy(rig); SceneManager.UnloadSceneAsync(scene);
            typeof(ReefHands).GetField("frame", Flags).SetValue(null, -1);
        }
        return output.ToString();
    }
}
