using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Reefkeeper
{
    /// <summary>Desktop swimming prototype. No head tracking or glove input is simulated here.</summary>
    [RequireComponent(typeof(CharacterController))]
    public sealed class DiveMotor : MonoBehaviour
    {
        [Header("Swimming — metres / seconds")]
        public float cruiseSpeed = 3.2f;
        public float sprintSpeed = 6.5f;
        public float acceleration = 4.5f;
        public float waterDrag = 0.75f;
        public float brakeDeceleration = 12f;
        [Header("View")]
        public Camera view;
        public float mouseSensitivity = 0.095f;
        public float pitchLimit = 82f;
        [Header("Dive boundary")]
        public float surfaceHeight = 15f;
        public float explorationRadius = 44f;

        public Vector3 Velocity { get; private set; }
        public bool Braking { get; private set; }
        public bool Sprinting { get; private set; }
        public float Depth => Mathf.Max(0, surfaceHeight - transform.position.y);
        public bool Automated { get; set; }
        CharacterController body;
        Vector3 spawnPosition;
        Quaternion spawnRotation;
        float pitch;
        bool controlling;

        void Awake()
        {
            body = GetComponent<CharacterController>();
            spawnPosition = transform.position;
            spawnRotation = transform.rotation;
            if (!view) view = GetComponentInChildren<Camera>();
        }

        void Start() { SetControl(true); }
        void OnDisable() { SetControl(false); }
        void OnApplicationFocus(bool focused) { if (!focused) SetControl(false); }

        public void SetControl(bool enabled)
        {
            controlling = enabled;
            Cursor.lockState = enabled ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !enabled;
            if (!enabled) { Velocity = Vector3.zero; Braking = false; Sprinting = false; }
        }

        void Update()
        {
            if (Automated) return;
            bool escape, click, reset, boost, brake;
            Vector2 look;
            Vector3 axes;
#if ENABLE_INPUT_SYSTEM
            var k = Keyboard.current;
            var m = Mouse.current;
            if (k == null || m == null) return;
            escape = k.escapeKey.wasPressedThisFrame;
            click = m.leftButton.wasPressedThisFrame;
            reset = k.rKey.wasPressedThisFrame;
            boost = k.leftShiftKey.isPressed || k.rightShiftKey.isPressed;
            brake = m.rightButton.isPressed || k.xKey.isPressed;
            look = m.delta.ReadValue();
            axes = new Vector3((k.dKey.isPressed ? 1 : 0) - (k.aKey.isPressed ? 1 : 0),
                (k.spaceKey.isPressed ? 1 : 0) - (k.cKey.isPressed ? 1 : 0),
                (k.wKey.isPressed ? 1 : 0) - (k.sKey.isPressed ? 1 : 0));
#else
            escape = Input.GetKeyDown(KeyCode.Escape);
            click = Input.GetMouseButtonDown(0);
            reset = Input.GetKeyDown(KeyCode.R);
            boost = Input.GetKey(KeyCode.LeftShift);
            brake = Input.GetMouseButton(1) || Input.GetKey(KeyCode.X);
            look = new Vector2(Input.GetAxisRaw("Mouse X"), Input.GetAxisRaw("Mouse Y")) * 15;
            axes = new Vector3(Input.GetAxisRaw("Horizontal"),
                (Input.GetKey(KeyCode.Space) ? 1 : 0) - (Input.GetKey(KeyCode.C) ? 1 : 0),
                Input.GetAxisRaw("Vertical"));
#endif
            if (ReefToolMenu.BlocksWorldInput) { Tick(axes, boost, brake, Time.deltaTime); return; }
            if (escape) { SetControl(false); return; }
            if (!controlling)
            {
                if (click) SetControl(true);
                return;
            }
            if (reset) { ResetDive(); return; }
            transform.Rotate(0, look.x * mouseSensitivity, 0, Space.World);
            pitch = Mathf.Clamp(pitch - look.y * mouseSensitivity, -pitchLimit, pitchLimit);
            view.transform.localRotation = Quaternion.Euler(pitch, 0, 0);
            Tick(axes, boost, brake, Time.deltaTime);
        }

        // Separate input from motion so a later glove/XR adapter can supply the same commands.
        public void Tick(Vector3 localInput, bool boost, bool brake, float deltaTime)
        {
            float dt = Mathf.Clamp(deltaTime, 0, 0.05f);
            Vector3 direction = view.transform.right * localInput.x
                + Vector3.up * localInput.y + view.transform.forward * localInput.z;
            direction = Vector3.ClampMagnitude(direction, 1);
            Braking = brake;
            Sprinting = boost && !brake && direction.sqrMagnitude > 0.001f;
            Velocity = IntegrateVelocity(Velocity, direction, boost, brake, dt,
                cruiseSpeed, sprintSpeed, acceleration, waterDrag, brakeDeceleration);
            body.Move(Velocity * dt);
            // Surface and outer reef bounds are gentle velocity stops, not teleport triggers.
            Vector3 p = transform.position;
            if (p.y > surfaceHeight - 0.8f)
            {
                body.Move(Vector3.down * (p.y - surfaceHeight + 0.8f));
                Velocity = new Vector3(Velocity.x, Mathf.Min(0, Velocity.y), Velocity.z);
            }
            Vector3 radial = new Vector3(p.x, 0, p.z);
            if (radial.magnitude > explorationRadius)
            {
                body.Move(radial.normalized * (explorationRadius - radial.magnitude));
                float outward = Vector3.Dot(Velocity, radial.normalized);
                if (outward > 0) Velocity -= radial.normalized * outward;
            }
        }

        public static Vector3 IntegrateVelocity(Vector3 velocity, Vector3 direction,
            bool sprint, bool brake, float dt, float cruise, float fast, float thrust,
            float drag, float stopping)
        {
            if (brake) return Vector3.MoveTowards(velocity, Vector3.zero, stopping * dt);
            if (direction.sqrMagnitude > 0.001f)
                return Vector3.MoveTowards(velocity, Vector3.ClampMagnitude(direction, 1)
                    * (sprint ? fast : cruise), thrust * dt);
            velocity *= Mathf.Exp(-drag * dt);
            return velocity.sqrMagnitude < 0.0001f ? Vector3.zero : velocity;
        }

        void OnControllerColliderHit(ControllerColliderHit hit)
        {
            float intoSurface = Vector3.Dot(Velocity, hit.normal);
            if (intoSurface < 0) Velocity -= hit.normal * intoSurface;
        }

        public void ResetDive()
        {
            body.enabled = false;
            transform.SetPositionAndRotation(spawnPosition, spawnRotation);
            body.enabled = true;
            pitch = 0;
            view.transform.localRotation = Quaternion.identity;
            Velocity = Vector3.zero;
        }
    }
}
