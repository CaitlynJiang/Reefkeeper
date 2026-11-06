using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Reefkeeper
{
    /// <summary>
    /// Test-scene camera: WASD to move, Q/E down/up, hold the right mouse button to look around,
    /// Shift to go faster, scroll to change speed. F jumps to the next of <see cref="focusPoints"/>.
    /// </summary>
    public sealed class FlyCamera : MonoBehaviour
    {
        [Tooltip("Metres per second.")] public float speed = 2f;
        [Tooltip("Speed multiplier while Shift is held.")] public float fastMultiplier = 3f;
        [Tooltip("Degrees per pixel of mouse movement.")] public float lookSensitivity = 0.15f;
        [Tooltip("F jumps to the next of these (looking at it from a little above and in front).")]
        public Transform[] focusPoints = new Transform[0];
        public Vector3 focusOffset = new Vector3(0f, 1.2f, -1.8f);

        float yaw, pitch;
        int nextFocus;

        void OnEnable()
        {
            var e = transform.eulerAngles;
            yaw = e.y;
            pitch = e.x > 180f ? e.x - 360f : e.x;
        }

#if ENABLE_INPUT_SYSTEM
        void Update()
        {
            var kb = Keyboard.current;
            var mouse = Mouse.current;
            if (kb == null) return;
            float dt = Time.unscaledDeltaTime;

            if (mouse != null)
            {
                if (mouse.rightButton.isPressed)
                {
                    var delta = mouse.delta.ReadValue();
                    yaw += delta.x * lookSensitivity;
                    pitch = Mathf.Clamp(pitch - delta.y * lookSensitivity, -89f, 89f);
                    transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
                }
                float scroll = mouse.scroll.ReadValue().y;
                if (scroll != 0f) speed = Mathf.Clamp(speed * (scroll > 0f ? 1.15f : 1f / 1.15f), 0.1f, 50f);
            }

            var move = new Vector3(
                (kb.dKey.isPressed ? 1 : 0) - (kb.aKey.isPressed ? 1 : 0),
                (kb.eKey.isPressed ? 1 : 0) - (kb.qKey.isPressed ? 1 : 0),
                (kb.wKey.isPressed ? 1 : 0) - (kb.sKey.isPressed ? 1 : 0));
            if (move != Vector3.zero)
            {
                float s = speed * (kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed ? fastMultiplier : 1f);
                // Forward/sideways follow the view; Q/E are straight down/up.
                var horizontal = transform.right * move.x + transform.forward * move.z;
                transform.position += (horizontal.normalized * new Vector2(move.x, move.z).magnitude + Vector3.up * move.y) * s * dt;
            }

            if (kb.fKey.wasPressedThisFrame && focusPoints.Length > 0)
            {
                var focus = focusPoints[nextFocus++ % focusPoints.Length];
                if (focus)
                {
                    transform.position = focus.position + focusOffset;
                    transform.LookAt(focus.position);
                    OnEnable();
                }
            }
        }
#endif
    }
}
