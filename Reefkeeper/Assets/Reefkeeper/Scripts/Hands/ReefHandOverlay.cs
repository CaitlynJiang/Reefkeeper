using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;
using UnityEngine.XR.Hands;

namespace Reefkeeper.Hands
{
    /// <summary>Read-only, binocular visualization of the joint poses that drive gameplay.</summary>
    public sealed class ReefHandOverlay : MonoBehaviour
    {
        public event Action<bool> VisibilityChanged;
        bool visible;
        WristSwitch wristSwitch;
        float nextReadout;
        Mesh mesh;
        GameObject drawing;
        readonly Text[] readouts = new Text[2];
        readonly List<Vector3> vertices = new List<Vector3>(450);
        readonly List<Color32> colors = new List<Color32>(450);
        readonly List<int> triangles = new List<int>(680);
        readonly Vector3[] joints = new Vector3[XRHandJointID.EndMarker.ToIndex()];
        readonly bool[] valid = new bool[XRHandJointID.EndMarker.ToIndex()];
        static readonly Color32 Left = new Color32(75, 223, 255, 255);
        static readonly Color32 Right = new Color32(255, 163, 76, 255);
        static readonly Color32 Pinched = new Color32(117, 255, 118, 255);

        public void Toggle()
        {
            visible = !visible;
            if (drawing && !visible) drawing.SetActive(false);
            nextReadout = 0;
            VisibilityChanged?.Invoke(visible);
        }

        void LateUpdate()
        {
            var space = ReefInput.HandTrackingSpace;
            if (!space || !ReefInput.HandsMode)
            {
                wristSwitch.Update(null, 0);
                if (drawing && drawing.activeSelf) drawing.SetActive(false);
                return;
            }
            var left = ReefHands.TrackedHand(0);
            var right = ReefHands.TrackedHand(1);
            UpdateWristToggle(left, right);
            if (!visible) return;
            var camera = Camera.main;
            if (!camera) return;
            if (!drawing) CreateDrawing(space);
            if (drawing.transform.parent != space) drawing.transform.SetParent(space, false);
            drawing.SetActive(true);
            vertices.Clear();
            colors.Clear();
            triangles.Clear();
            var facing = Quaternion.Inverse(space.rotation) * camera.transform.rotation;
            DrawHand(left, 0, facing);
            DrawHand(right, 1, facing);
            mesh.Clear();
            mesh.SetVertices(vertices);
            mesh.SetColors(colors);
            mesh.SetTriangles(triangles, 0);
            if (Time.unscaledTime >= nextReadout) nextReadout = Time.unscaledTime + .1f;
        }

        void UpdateWristToggle(XRHand left, XRHand right)
        {
            float? distance = null;
            if (left.isTracked && right.isTracked &&
                left.GetJoint(XRHandJointID.Wrist).TryGetPose(out var wrist) &&
                right.GetJoint(XRHandJointID.IndexTip).TryGetPose(out var finger))
                distance = Vector3.Distance(wrist.position, finger.position);
            if (wristSwitch.Update(distance, Time.unscaledDeltaTime)) Toggle();
        }

        internal struct WristSwitch
        {
            float hold;
            bool latched;

            public bool Update(float? distance, float elapsed)
            {
                // Losing sight of a hand interrupts the hold. Only a visible release rearms it.
                if (distance > .07f) latched = false;
                if (!distance.HasValue || distance > .045f || latched)
                {
                    hold = 0;
                    return false;
                }
                hold += Mathf.Min(elapsed, .1f);
                if (hold < 1f) return false;
                latched = true;
                return true;
            }
        }

        void CreateDrawing(Transform space)
        {
            drawing = new GameObject("Hand tracking overlay", typeof(MeshFilter), typeof(MeshRenderer));
            drawing.transform.SetParent(space, false);
            mesh = new Mesh { name = "Tracked joints and bones" };
            mesh.MarkDynamic();
            drawing.GetComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = drawing.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = GetComponent<DiveCardMock>().OverlayMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            for (int i = 0; i < 2; i++)
            {
                var canvas = new GameObject("Gesture readout", typeof(RectTransform), typeof(Canvas));
                canvas.transform.SetParent(drawing.transform, false);
                canvas.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
                canvas.transform.localScale = Vector3.one * .00048f;
                var label = new GameObject("Pinch distance", typeof(RectTransform), typeof(Text));
                label.transform.SetParent(canvas.transform, false);
                var text = readouts[i] = label.GetComponent<Text>();
                text.rectTransform.sizeDelta = new Vector2(285, 42);
                text.font = font;
                text.fontSize = 26;
                text.alignment = TextAnchor.MiddleCenter;
                text.material = renderer.sharedMaterial;
                text.raycastTarget = false;
            }
        }

        void DrawHand(XRHand hand, int side, Quaternion facing)
        {
            var label = readouts[side];
            var anchor = label.transform.parent;
            if (!hand.isTracked) { anchor.gameObject.SetActive(false); return; }
            var tint = side == 0 ? Left : Right;
            Vector3 up = facing * Vector3.up, across = facing * Vector3.right;
            var normal = facing * Vector3.forward;
            for (int i = 0; i < joints.Length; i++)
            {
                valid[i] = hand.GetJoint(XRHandJointIDUtility.FromIndex(i)).TryGetPose(out var pose);
                if (!valid[i]) continue;
                var p = joints[i] = pose.position;
                Quad(p - across*.0034f, p + up*.0034f, p + across*.0034f, p - up*.0034f, Color.white);
            }
            Bone(XRHandJointID.Wrist, XRHandJointID.Palm, normal, tint);
            for (var finger = XRHandFingerID.Thumb; finger <= XRHandFingerID.Little; finger++)
            {
                var first = finger.GetFrontJointID();
                Bone(XRHandJointID.Wrist, first, normal, tint);
                for (var joint = first; joint < finger.GetBackJointID(); joint++)
                    Bone(joint, joint + 1, normal, tint);
            }
            int thumb = XRHandJointID.ThumbTip.ToIndex(), index = XRHandJointID.IndexTip.ToIndex();
            int wrist = XRHandJointID.Wrist.ToIndex();
            if (!valid[thumb] || !valid[index] || !valid[wrist]) { anchor.gameObject.SetActive(false); return; }
            var state = ReefHands.Get(side);
            Bone(XRHandJointID.ThumbTip, XRHandJointID.IndexTip, normal, state.Pinch ? Pinched : tint);
            anchor.gameObject.SetActive(true);
            anchor.localPosition = joints[wrist] - up*.028f;
            anchor.localRotation = facing;
            if (Time.unscaledTime >= nextReadout)
            {
                float cm = Vector3.Distance(joints[thumb], joints[index])*100f;
                label.text = $"{cm:F1} cm  {(state.Pinch ? "PINCH" : state.MiddlePinch ? "MIDDLE" : state.Fist ? "FIST" : "—")}";
                label.color = state.Pinch ? Pinched : tint;
            }
        }

        void Bone(XRHandJointID start, XRHandJointID end, Vector3 normal, Color32 tint)
        {
            int a = start.ToIndex(), b = end.ToIndex();
            if (!valid[a] || !valid[b]) return;
            var width = Vector3.Cross(normal, joints[b] - joints[a]).normalized * .0012f;
            Quad(joints[a]-width, joints[a]+width, joints[b]+width, joints[b]-width, tint);
        }

        void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color32 tint)
        {
            int first = vertices.Count;
            vertices.Add(a); vertices.Add(b); vertices.Add(c); vertices.Add(d);
            for (int i = 0; i < 4; i++) colors.Add(tint);
            triangles.Add(first); triangles.Add(first+1); triangles.Add(first+2);
            triangles.Add(first); triangles.Add(first+2); triangles.Add(first+3);
        }

        void OnDisable()
        {
            wristSwitch.Update(null, 0);
            if (drawing) drawing.SetActive(false);
        }

        void OnDestroy()
        {
            if (drawing) Destroy(drawing);
            if (mesh) Destroy(mesh);
        }
    }
}
