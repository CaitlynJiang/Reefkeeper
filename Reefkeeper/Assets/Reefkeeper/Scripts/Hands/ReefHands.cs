using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Hands;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Readers;

namespace Reefkeeper.Hands
{
    /// <summary>
    /// Hand gestures read straight from XR Hands joints, so they behave the same on PICO and Quest.
    /// State is worked out lazily once per frame on first access. Index 0 = left, 1 = right.
    /// Joint poses are in tracking space (relative to the XR Origin's camera offset).
    /// </summary>
    public static class ReefHands
    {
        // Thumb tip to index tip (m): closes below On, opens above Off.
        const float PinchOn = 0.02f, PinchOff = 0.035f;
        // Average of middle, ring and little fingertips to the palm (m).
        const float FistOn = 0.08f, FistOff = 0.1f;
        // Index tip to the palm (m). A fist curls the index too; pointing leaves it out.
        const float IndexFistOn = 0.1f, IndexFistOff = 0.12f;

        public struct State
        {
            public bool Tracked;
            public bool Pinch, PinchDown, PinchUp;
            public bool Fist, FistDown, FistUp;
            /// <summary>Pinch or fist: what grabs a tool or coral.</summary>
            public bool Grab, GrabDown, GrabUp;
            public Pose Palm;
            public Pose IndexTip;
        }

        static readonly State[] states = new State[2];
        static readonly List<XRHandSubsystem> subsystems = new List<XRHandSubsystem>();
        static int frame = -1;

        public static State Get(int hand) { Refresh(); return states[hand]; }

        static void Refresh()
        {
            if (frame == Time.frameCount) return;
            frame = Time.frameCount;
            XRHandSubsystem subsystem = null;
            subsystems.Clear();
            SubsystemManager.GetSubsystems(subsystems);
            foreach (var s in subsystems) if (s.running) { subsystem = s; break; }
            for (int i = 0; i < 2; i++)
                Update(ref states[i], subsystem == null ? default : i == 0 ? subsystem.leftHand : subsystem.rightHand, subsystem != null);
        }

        static void Update(ref State s, XRHand hand, bool available)
        {
            bool wasPinch = s.Pinch, wasFist = s.Fist, wasGrab = s.Grab;
            s.Tracked = available && hand.isTracked;
            if (s.Tracked && Pose(hand, XRHandJointID.Palm, out var palm) && Pose(hand, XRHandJointID.ThumbTip, out var thumb)
                && Pose(hand, XRHandJointID.IndexTip, out var index))
            {
                s.Palm = palm; s.IndexTip = index;
                float pinch = Vector3.Distance(thumb.position, index.position);
                s.Pinch = wasPinch ? pinch < PinchOff : pinch < PinchOn;
                float curl = 0; int n = 0;
                foreach (var id in new[] { XRHandJointID.MiddleTip, XRHandJointID.RingTip, XRHandJointID.LittleTip })
                    if (Pose(hand, id, out var tip)) { curl += Vector3.Distance(tip.position, palm.position); n++; }
                curl = n > 0 ? curl / n : 1f;
                float indexCurl = Vector3.Distance(index.position, palm.position);
                s.Fist = wasFist ? curl < FistOff && indexCurl < IndexFistOff : curl < FistOn && indexCurl < IndexFistOn;
            }
            else s.Pinch = s.Fist = false;
            s.Grab = s.Pinch || s.Fist;
            s.PinchDown = s.Pinch && !wasPinch; s.PinchUp = !s.Pinch && wasPinch;
            s.FistDown = s.Fist && !wasFist; s.FistUp = !s.Fist && wasFist;
            s.GrabDown = s.Grab && !wasGrab; s.GrabUp = !s.Grab && wasGrab;
        }

        static bool Pose(XRHand hand, XRHandJointID id, out Pose pose)
        {
            pose = default;
            return hand.GetJoint(id).TryGetPose(out pose);
        }

        /// <summary>An XRI button driven by a hand gesture, for interactor select inputs.</summary>
        public sealed class Button : IXRInputButtonReader
        {
            public enum Kind { Grab, Pinch }
            readonly int hand; readonly Kind kind;
            public Button(int hand, Kind kind) { this.hand = hand; this.kind = kind; }
            public bool ReadIsPerformed() { var s = Get(hand); return kind == Kind.Grab ? s.Grab : s.Pinch; }
            public bool ReadWasPerformedThisFrame() { var s = Get(hand); return kind == Kind.Grab ? s.GrabDown : s.PinchDown; }
            public bool ReadWasCompletedThisFrame() { var s = Get(hand); return kind == Kind.Grab ? s.GrabUp : s.PinchUp; }
            public float ReadValue() => ReadIsPerformed() ? 1f : 0f;
            public bool TryReadValue(out float value) { value = ReadValue(); return Get(hand).Tracked; }
        }
    }
}
