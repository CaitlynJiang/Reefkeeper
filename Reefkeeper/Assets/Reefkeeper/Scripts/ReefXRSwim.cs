using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Simulation;

namespace Reefkeeper
{
    // Swimming for the XR rig.
    // Headset: left stick swims where you look, left stick click boosts,
    // right stick left/right snap-turns, right stick up/down rises/sinks.
    // Bare hands: sweep an open palm back to stroke forward (breaststroke), or hold an open
    // hand out at arm's length to glide where the fingers point.
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
        [Header("Bare hands")]
        [Tooltip("Body acceleration per m/s of palm speed during a stroke.\n划水推力")]
        public float strokeGain = 2.5f;
        [Tooltip("Slower palm motion is reaching, not swimming (m/s).")]
        public float strokeMinSpeed = 0.35f;
        public float strokeMaxSpeed = 3f;
        [Tooltip("Palm this far from the head counts as an outstretched arm (m).")]
        public float glideReach = 0.42f;
        public float glideHoldSeconds = 0.35f;
        [Tooltip("Glide speed as a fraction of cruise speed.\n滑行速度")]
        public float glideSpeed = 0.55f;
        public Vector3 Velocity { get; private set; }
        /// <summary>True while an outstretched hand is gliding.</summary>
        public bool Gliding { get; private set; }
        readonly Vector3[] lastPalm = new Vector3[2], palmVelocity = new Vector3[2];
        readonly bool[] hadPalm = new bool[2];
        readonly float[] glideHeld = new float[2];
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
            if (Hands.ReefInput.HandsMode) HandSwim();
            else ControllerSwim();
        }

        void HandSwim()
        {
            float dt = Time.deltaTime;
            Gliding = false;
            if (dt <= 0) return;
            // Tapping the palm menu is not swimming; coast instead.
            if (Hands.ReefPalmMenu.Instance && Hands.ReefPalmMenu.Instance.Visible)
            {
                hadPalm[0] = hadPalm[1] = false; glideHeld[0] = glideHeld[1] = 0;
                Tick(Vector3.zero, false, false, dt);
                return;
            }
            // Hand joints and the camera's local pose share the camera offset's space.
            var space = view.transform.parent;
            Vector3 head = view.transform.localPosition, thrust = Vector3.zero, glide = Vector3.zero;
            for (int i = 0; i < 2; i++)
            {
                var hand = Hands.ReefHands.Get(i);
                // A hand that pinches, grips or holds something is working, not swimming.
                if (!hand.Tracked || hand.Grab || Hands.ReefInput.HandBusy(i)) { hadPalm[i] = false; glideHeld[i] = 0; continue; }
                Vector3 relative = hand.Palm.position - head;
                palmVelocity[i] = hadPalm[i] ? Vector3.Lerp(palmVelocity[i], (relative - lastPalm[i]) / dt, 0.5f) : Vector3.zero;
                lastPalm[i] = relative; hadPalm[i] = true;

                Vector3 palmFacing = space.TransformDirection(hand.Palm.rotation * Vector3.down);
                thrust += StrokeThrust(space.TransformVector(palmVelocity[i]), palmFacing, strokeMinSpeed) * strokeGain;

                bool armOut = relative.magnitude > glideReach && palmVelocity[i].magnitude < 0.25f;
                glideHeld[i] = armOut ? glideHeld[i] + dt : 0;
                if (glideHeld[i] >= glideHoldSeconds) glide += space.TransformDirection(hand.Palm.rotation * Vector3.forward);
            }
            if (thrust.sqrMagnitude > 0) Velocity = Vector3.ClampMagnitude(Velocity + thrust * Mathf.Min(dt, 0.05f), strokeMaxSpeed);
            Gliding = glide.sqrMagnitude > 0.01f;
            Tick(Gliding ? glide.normalized * glideSpeed : Vector3.zero, false, false, dt);
        }

        /// <summary>
        /// Push from one palm moving through the water: opposite the hand's motion, scaled by how
        /// far it beats <paramref name="minSpeed"/> and by how squarely the palm faces that motion
        /// (an edge-on recovery stroke pushes nothing).
        /// </summary>
        public static Vector3 StrokeThrust(Vector3 handVelocity, Vector3 palmFacing, float minSpeed)
        {
            float speed = handVelocity.magnitude;
            if (speed <= minSpeed) return Vector3.zero;
            float facing = Vector3.Dot(palmFacing, handVelocity / speed);
            if (facing < 0.3f) return Vector3.zero;
            return -handVelocity / speed * (speed - minSpeed) * facing;
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
