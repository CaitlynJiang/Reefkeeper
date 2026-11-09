using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Simulation;

namespace Reefkeeper
{
    // Swimming for the XR rig.
    // Headset: left stick swims where you look, left stick click boosts,
    // right stick left/right snap-turns, right stick up/down rises/sinks.
    // Editor simulator (FPS mode): WASD + Q/E, Shift boosts, B brakes.
    // Run before the simulator; it still owns head/hand tracking and grip input.
    [DefaultExecutionOrder(-32000)]
    [RequireComponent(typeof(CharacterController))]
    public sealed class ReefXRSwim : MonoBehaviour
    {
        public Camera view;
        public XRInteractionSimulator simulator;
        public bool Automated;
        [Tooltip("Degrees per snap turn.\n每次转身的角度")]
        public float snapTurnAngle = 45;
        public Vector3 Velocity { get; private set; }
        CharacterController body;
        InputAction moveAction, lookAction, boostAction;
        bool turnArmed = true;

        const float StickDeadzone = 0.15f, TurnPress = 0.75f, TurnRelease = 0.3f, RiseDeadzone = 0.4f;

        void Awake()
        {
            body = GetComponent<CharacterController>();
            // The starter rig's 1 mm cutoff discards slow movement at high frame rates.
            body.minMoveDistance = 0;
            moveAction = new InputAction("Swim", InputActionType.Value, "<XRController>{LeftHand}/{Primary2DAxis}", expectedControlType: "Vector2");
            lookAction = new InputAction("Turn and rise", InputActionType.Value, "<XRController>{RightHand}/{Primary2DAxis}", expectedControlType: "Vector2");
            boostAction = new InputAction("Boost", InputActionType.Button, "<XRController>{LeftHand}/{Primary2DAxisClick}");
        }
        void OnEnable() { moveAction.Enable(); lookAction.Enable(); boostAction.Enable(); }
        void OnDisable() { moveAction.Disable(); lookAction.Disable(); boostAction.Disable(); }
        void OnDestroy() { moveAction.Dispose(); lookAction.Dispose(); boostAction.Dispose(); }

        void Update()
        {
            if (DiveCardMock.BlocksControls) { Velocity=Vector3.zero; return; }
            if (!view) return;
            // The simulator is EditorOnly, so it is missing on the headset.
            if (simulator)
            {
                bool swimming = simulator.manipulatingFPS;
                simulator.bodyTranslateMultiplier = swimming ? 0 : 0.65f;
                if (Automated) return;
                if (swimming) { KeyboardSwim(); return; }
            }
            if (Automated) return;
            ControllerSwim();
        }

        void KeyboardSwim()
        {
            var k = Keyboard.current;
            if (k == null) return;
            var axes = new Vector3((k.dKey.isPressed ? 1 : 0) - (k.aKey.isPressed ? 1 : 0),
                (k.eKey.isPressed ? 1 : 0) - (k.qKey.isPressed ? 1 : 0),
                (k.wKey.isPressed ? 1 : 0) - (k.sKey.isPressed ? 1 : 0));
            var direction = view.transform.right * axes.x + Vector3.up * axes.y + view.transform.forward * axes.z;
            Tick(direction, k.leftShiftKey.isPressed, k.bKey.isPressed, Time.deltaTime);
        }

        void ControllerSwim()
        {
            Vector2 move = moveAction.ReadValue<Vector2>();
            if (move.magnitude < StickDeadzone) move = Vector2.zero;
            Vector2 look = lookAction.ReadValue<Vector2>();

            // Right stick: sideways snap-turns, up/down rises/sinks; whichever axis dominates wins.
            bool sideways = Mathf.Abs(look.x) > Mathf.Abs(look.y);
            if (turnArmed && sideways && Mathf.Abs(look.x) > TurnPress)
            {
                transform.RotateAround(view.transform.position, Vector3.up, Mathf.Sign(look.x) * snapTurnAngle);
                Physics.SyncTransforms(); // so the CharacterController doesn't snap back
                Velocity = Quaternion.Euler(0, Mathf.Sign(look.x) * snapTurnAngle, 0) * Velocity;
                turnArmed = false;
            }
            else if (Mathf.Abs(look.x) < TurnRelease) turnArmed = true;
            float rise = !sideways && Mathf.Abs(look.y) > RiseDeadzone
                ? Mathf.Sign(look.y) * Mathf.InverseLerp(RiseDeadzone, 1, Mathf.Abs(look.y)) : 0;

            var direction = view.transform.right * move.x + view.transform.forward * move.y + Vector3.up * rise;
            Tick(Vector3.ClampMagnitude(direction, 1), boostAction.IsPressed(), false, Time.deltaTime);
        }

        public void Tick(Vector3 worldDirection, bool boost, bool brake, float dt)
        {
            if (DiveCardMock.BlocksControls) { Velocity=Vector3.zero; return; }
            dt = Mathf.Clamp(dt, 0, 0.05f);
            body.center = transform.InverseTransformPoint(view.transform.position);
            Velocity = DiveMotor.IntegrateVelocity(Velocity, worldDirection, boost, brake, dt, 2.1f, 4, 3.5f, 1.1f, 12);
            body.Move(Velocity * dt);
            if (view.transform.position.y > 14.2f)
            {
                body.Move(Vector3.down * (view.transform.position.y - 14.2f));
                Velocity = Vector3.zero;
            }
            var p = view.transform.position;
            var radial = new Vector3(p.x, 0, p.z);
            if (radial.magnitude > 44) { body.Move(radial.normalized * (44 - radial.magnitude)); Velocity = Vector3.zero; }
        }
        void OnControllerColliderHit(ControllerColliderHit hit)
        {
            float into = Vector3.Dot(Velocity, hit.normal);
            if (into < 0) Velocity -= hit.normal * into;
        }
    }
}
