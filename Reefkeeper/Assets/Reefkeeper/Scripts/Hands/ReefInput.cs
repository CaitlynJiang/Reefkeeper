using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit.Inputs;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Readers;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace Reefkeeper.Hands
{
    /// <summary>
    /// One input source for the headset whether the player holds controllers or uses bare hands.
    /// XRInputModalityManager decides which rig parts are live; this answers "where does this hand
    /// point" and "did it just act" for either. Index 0 = left, 1 = right.
    /// Controllers: Use = trigger, Talk = grip. Hands: both = pinch.
    /// </summary>
    public static class ReefInput
    {
        static XROrigin origin;
        static readonly Transform[] controllers = new Transform[2], hands = new Transform[2];
        static readonly Transform[] controllerRays = new Transform[2], handRays = new Transform[2];
        static InputAction[] use, talk;

        /// <summary>True while the player uses tracked hands instead of controllers.</summary>
        public static bool HandsMode => XRInputModalityManager.currentInputMode.Value == XRInputModalityManager.InputMode.TrackedHand;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            use = new[]
            {
                new InputAction("Use left", InputActionType.Button, "<XRController>{LeftHand}/triggerPressed"),
                new InputAction("Use right", InputActionType.Button, "<XRController>{RightHand}/triggerPressed"),
            };
            talk = new[]
            {
                new InputAction("Talk left", InputActionType.Button, "<XRController>{LeftHand}/{GripButton}"),
                new InputAction("Talk right", InputActionType.Button, "<XRController>{RightHand}/{GripButton}"),
            };
            foreach (var a in use) a.Enable();
            foreach (var a in talk) a.Enable();
            Bind();
            SceneManager.sceneLoaded += (_, _) => Bind();
        }

        /// <summary>Finds the rig and makes hand interactors grab with the Reefkeeper pinch/fist gesture.</summary>
        static void Bind()
        {
            origin = Object.FindFirstObjectByType<XROrigin>(FindObjectsInactive.Include);
            if (!origin) return;
            var modality = origin.GetComponent<XRInputModalityManager>();
            for (int i = 0; i < 2; i++)
            {
                controllers[i] = modality ? (i == 0 ? modality.leftController : modality.rightController)?.transform : null;
                hands[i] = modality ? (i == 0 ? modality.leftHand : modality.rightHand)?.transform : null;
                controllerRays[i] = RayOf(controllers[i]);
                handRays[i] = RayOf(hands[i]);
                if (hands[i] && hands[i].GetComponentInChildren<NearFarInteractor>(true) is { } nearFar)
                {
                    nearFar.selectInput.inputSourceMode = XRInputButtonReader.InputSourceMode.ObjectReference;
                    nearFar.selectInput.SetObjectReference(new ReefHands.Button(i, ReefHands.Button.Kind.Grab));
                }
            }
        }

        static Transform RayOf(Transform device) =>
            device ? device.GetComponentInChildren<NearFarInteractor>(true)?.transform ?? device : null;

        static bool Live(Transform t) => t && t.gameObject.activeInHierarchy;

        /// <summary>The active device's pointing ray origin for this hand, or null.</summary>
        public static Transform RayOrigin(int hand)
        {
            if (!origin) Bind();
            if (HandsMode) return Live(handRays[hand]) && ReefHands.Get(hand).Tracked ? handRays[hand] : null;
            return Live(controllerRays[hand]) ? controllerRays[hand] : null;
        }

        /// <summary>World position of the controller or palm, if that hand is live.</summary>
        public static bool TryGetPosition(int hand, out Vector3 position)
        {
            if (!origin) Bind();
            position = default;
            if (HandsMode)
            {
                var s = ReefHands.Get(hand);
                if (!s.Tracked || !origin) return false;
                position = origin.CameraFloorOffsetObject.transform.TransformPoint(s.Palm.position);
                return true;
            }
            if (!Live(controllers[hand])) return false;
            position = controllers[hand].position;
            return true;
        }

        /// <summary>Trigger pulled, or pinch closed, this frame.</summary>
        public static bool UseDown(int hand) =>
            HandsMode ? ReefHands.Get(hand).PinchDown : use != null && use[hand].WasPressedThisFrame();

        /// <summary>Grip pressed, or pinch closed, this frame.</summary>
        public static bool TalkDown(int hand) =>
            HandsMode ? ReefHands.Get(hand).PinchDown : talk != null && talk[hand].WasPressedThisFrame();

        /// <summary>"Pull the trigger" / "Pinch", for prompts.</summary>
        public static string UseVerb => HandsMode ? "Pinch" : "Pull the trigger";
    }
}
