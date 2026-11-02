using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR;

namespace Reefkeeper
{
    /// <summary>
    /// Screen-overlay HUDs are invisible in a headset. When a real XR device is running (e.g. PICO),
    /// this turns the canvas into a world-space panel that floats in front of the player's view and
    /// follows it softly. In the editor and on desktop it stays a normal overlay.
    /// </summary>
    [RequireComponent(typeof(Canvas))]
    public sealed class XRWorldHud : MonoBehaviour
    {
        [Tooltip("Distance from the eyes (m).")] public float distance = 1.1f;
        [Tooltip("Metres per canvas unit; 1280 units wide -> 1.15 m.")] public float metresPerUnit = 0.0009f;
        [Tooltip("How quickly the panel catches up with head turns.")] public float follow = 5f;

        public bool InWorld { get; private set; }
        public Camera View => Camera.main;
        Canvas canvas;
        RectTransform rect;

        void Awake()
        {
            canvas = GetComponent<Canvas>();
            rect = (RectTransform)transform;
        }

        void LateUpdate()
        {
            bool want = XRSettings.isDeviceActive;
            if (want != InWorld) Switch(want);
            if (!InWorld || !View) return;
            var head = View.transform;
            // Follow the gaze, but keep the panel upright (no roll) and within a comfortable pitch.
            var euler = head.rotation.eulerAngles;
            var rotation = Quaternion.Euler(Mathf.Clamp(Mathf.DeltaAngle(0, euler.x), -35, 35), euler.y, 0);
            var target = head.position + rotation * Vector3.forward * distance;
            float t = 1 - Mathf.Exp(-follow * Time.unscaledDeltaTime);
            transform.SetPositionAndRotation(Vector3.Lerp(transform.position, target, t), Quaternion.Slerp(transform.rotation, rotation, t));
        }

        void Switch(bool world)
        {
            InWorld = world;
            var scaler = GetComponent<CanvasScaler>();
            if (world)
            {
                canvas.renderMode = RenderMode.WorldSpace;
                canvas.worldCamera = View;
                if (scaler) { scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize; scaler.dynamicPixelsPerUnit = 3; }
                rect.sizeDelta = new Vector2(1280, 720);
                rect.localScale = Vector3.one * metresPerUnit;
                if (View)
                {
                    var head = View.transform;
                    var forward = Vector3.ProjectOnPlane(head.forward, Vector3.up).normalized;
                    if (forward.sqrMagnitude < 0.01f) forward = Vector3.forward;
                    transform.SetPositionAndRotation(head.position + forward * distance, Quaternion.LookRotation(forward));
                }
            }
            else
            {
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                if (scaler) scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                rect.localScale = Vector3.one;
            }
        }
    }
}
