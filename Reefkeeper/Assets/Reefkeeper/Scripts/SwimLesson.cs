using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace Reefkeeper
{
    // A view-space diorama driven by the player's swim and brake inputs.
    public sealed class SwimLesson : MonoBehaviour
    {
        static readonly (string title, Vector3 direction, string hands, string controller, string key, string simKey)[] Steps = {
            ("Swim forward", Vector3.forward, "Hands together, reach straight ahead.", "Left stick forward", "W", "W"),
            ("Full stop", Vector3.zero, "Make two fists and bring them together.", "Release both sticks", "X", "B"),
            ("Swim back", Vector3.back, "Open hands: Push your palms forward", "Left stick back", "S", "S"),
            ("Swim left", Vector3.left, "Open hands: Push water to your right", "Left stick left", "A", "A"),
            ("Swim right", Vector3.right, "Open hands: Push water to your left", "Left stick right", "D", "D"),
            ("Swim up", Vector3.up, "Open hands: Press your palms down", "Right stick up", "Space", "E"),
            ("Swim down", Vector3.down, "Open hands: Lift your palms up", "Right stick down", "C", "Q")
        };
        const float StepDistance = .28f;
        readonly Transform[] hands = new Transform[2];
        readonly Transform[] fists = new Transform[2];
        static readonly int FingerCurl = Shader.PropertyToID("_FingerCurl");
        Material palmMaterial;
        Transform motor;
        ReefXRSwim xr;
        DiveMotor desktop;
        Camera view;
        Text headingLabel, actionLabel;
        static readonly Color HintYellow = new Color(.953f, .808f, .388f);
        static readonly Color Cream = new Color(.97f, .93f, .81f);
        static readonly Color Success = new Color(.55f, 1f, .84f);
        Vector3 previousPosition;
        int step, shownMode = -1, shownStep = -1;
        float progress, phase, pause;

        public static void Begin(Camera camera)
        {
            var root = new GameObject("Swim lesson");
            root.transform.SetParent(camera.transform, false);
            root.transform.localPosition = new Vector3(0, -.04f, 1.05f);
            var lesson = root.AddComponent<SwimLesson>();
            lesson.view = camera;
            lesson.desktop = camera.GetComponentInParent<DiveMotor>();
            lesson.xr = camera.GetComponentInParent<ReefXRSwim>();
            lesson.motor = lesson.desktop ? lesson.desktop.transform : lesson.xr ? lesson.xr.transform : null;
            if (!lesson.motor) { Destroy(root); return; }
            lesson.previousPosition = lesson.motor.position;
            var glove = Resources.Load<Material>("SwimLesson/SwimGlove");
            if (!glove)
            {
                Debug.LogError("Missing swim lesson artwork.", root);
                Destroy(root);
                return;
            }
            lesson.palmMaterial = new Material(glove);
            for (int i = 0; i < 2; i++)
            {
                var side = i == 0 ? "Left" : "Right";
                var palm = Resources.Load<GameObject>("SwimLesson/Fuze" + side + "Palm");
                var fist = Resources.Load<GameObject>("SwimLesson/Fuze" + side + "Fist");
                if (!palm || !fist)
                {
                    Debug.LogError("Missing swim lesson " + side + " hand poses.", root);
                    Destroy(root);
                    return;
                }
                lesson.hands[i] = lesson.Model(palm, lesson.palmMaterial);
                lesson.fists[i] = lesson.Model(fist, glove);
                lesson.fists[i].gameObject.SetActive(false);
            }
            lesson.BuildInstructionCard();
        }

        void OnDestroy()
        {
            if (palmMaterial) Destroy(palmMaterial);
        }

        void BuildInstructionCard()
        {
            var card = new GameObject("Swim instruction", typeof(RectTransform), typeof(Canvas), typeof(Image));
            var rect = (RectTransform)card.transform;
            rect.SetParent(transform, false);
            var canvas = card.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = view;
            rect.localPosition = Vector3.down * .23f;
            rect.localScale = Vector3.one * .00085f;
            rect.sizeDelta = new Vector2(680, 112);

            var hud = FindFirstObjectByType<Corals.CoralGameplayHUD>(FindObjectsInactive.Include);
            var panel = card.GetComponent<Image>();
            panel.raycastTarget = false;
            panel.color = new Color(.15f, .22f, .28f, .94f);
            if (hud && hud.panel)
            {
                panel.sprite = hud.panel;
                panel.type = Image.Type.Sliced;
                panel.pixelsPerUnitMultiplier = 2;
            }
            var font = hud && hud.font ? hud.font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var bold = hud && hud.boldFont ? hud.boldFont : font;
            headingLabel = CardLabel(rect, "Direction", 21, 44, 32, bold, HintYellow);
            actionLabel = CardLabel(rect, "Action", -21, 40, 25, font, Cream);
        }

        static Text CardLabel(RectTransform card, string name, float y, float height, int size, Font font, Color color)
        {
            var label = new GameObject(name, typeof(RectTransform), typeof(Text)).GetComponent<Text>();
            var rect = label.rectTransform;
            rect.SetParent(card, false);
            rect.anchorMin = new Vector2(0, .5f);
            rect.anchorMax = new Vector2(1, .5f);
            rect.sizeDelta = new Vector2(-84, height);
            rect.anchoredPosition = new Vector2(0, y);
            label.font = font;
            label.fontSize = size;
            label.color = color;
            label.alignment = TextAnchor.MiddleLeft;
            label.raycastTarget = false;
            var shadow = label.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0, .04f, .05f, .35f);
            shadow.effectDistance = new Vector2(0, -2);
            return label;
        }

        Transform Model(GameObject model, Material material)
        {
            var part = Instantiate(model, transform, false);
            foreach (var renderer in part.GetComponentsInChildren<MeshRenderer>())
            {
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
            return part.transform;
        }

        void LateUpdate()
        {
            float dt = Time.deltaTime;
            if (dt <= 0) return;
            var delta = motor.position - previousPosition;
            previousPosition = motor.position;
            var heading = Quaternion.Euler(0, view.transform.eulerAngles.y, 0);
            var local = Quaternion.Inverse(heading) * delta;
            var velocity = Quaternion.Inverse(heading) * (desktop ? desktop.Velocity : xr.Velocity);
            if (pause > 0)
            {
                pause -= dt;
                if (pause > 0) return;
                step++;
                if (step == Steps.Length)
                {
                    headingLabel.text = "Enjoy Diving";
                    headingLabel.color = Success;
                    headingLabel.alignment = TextAnchor.MiddleCenter;
                    headingLabel.rectTransform.anchoredPosition = Vector2.zero;
                    actionLabel.enabled = false;
                    foreach (var hand in hands) hand.gameObject.SetActive(false);
                    Destroy(gameObject, 1.5f);
                    enabled = false;
                    return;
                }
                progress = phase = 0;
            }

            bool simulator = xr && xr.simulator && xr.simulator.manipulatingFPS;
            int mode = desktop || simulator ? 2 : Hands.ReefInput.HandsMode ? 0 : 1;
            var current = Steps[step];
            bool stop = current.direction == Vector3.zero;
            if (shownStep != step || shownMode != mode)
            {
                string hint = mode == 0 ? current.hands : mode == 1 ? current.controller :
                    (stop ? "Hold " : "Press ") + (simulator ? current.simKey : current.key);
                headingLabel.text = $"{step + 1}/{Steps.Length}   {current.title}";
                actionLabel.text = hint;
                actionLabel.color = Cream;
                shownStep = step; shownMode = mode;
                foreach (var hand in hands) hand.gameObject.SetActive(!stop);
                foreach (var fist in fists) fist.gameObject.SetActive(stop);
            }

            phase = Mathf.Repeat(phase + dt / 1.65f, 1f);
            if (step == 0 || stop) ShowTogetherPose(stop);
            else if (current.direction == Vector3.back) ShowBackStroke();
            else ShowDirectionalStroke(-current.direction);

            if (stop)
            {
                bool braking = desktop ? desktop.Braking : xr.Braking;
                if ((mode == 1 || braking) && velocity.sqrMagnitude < .0001f) CompleteStep();
                return;
            }
            if (step == 0 && mode == 0 && !xr.GlidingTogether) return;
            // Ignore recenter/teleport jumps and motion unrelated to swimming (such as snap turns).
            float along = Vector3.Dot(local, current.direction);
            if (Vector3.Dot(velocity, current.direction) > .08f && local.magnitude <= 5f * dt + .01f)
                progress = Mathf.Max(0, progress + along);
            if (progress >= StepDistance) CompleteStep();
        }

        void CompleteStep()
        {
            actionLabel.text = "Well done!";
            actionLabel.color = Success;
            pause = .65f;
        }

        void ShowTogetherPose(bool stop)
        {
            float blend = Mathf.SmoothStep(0, 1, Mathf.Min(phase, 1 - phase) * 4);
            var models = stop ? fists : hands;
            for (int i = 0; i < models.Length; i++)
            {
                float side = i == 0 ? -1 : 1;
                models[i].localPosition = new Vector3(side * Mathf.Lerp(.12f, stop ? .049f : .0475f, blend),
                    -.10f, stop ? -.04f : Mathf.Lerp(-.09f, .09f, blend));
                models[i].localRotation = Quaternion.Euler(stop ? 14f : 48f, side * (stop ? -135f : 8f), 0);
            }
        }

        void ShowDirectionalStroke(Vector3 push)
        {
            // Keep the palm planted through the stroke. Only the fingers soften on the return.
            const float endPush = .55f, startReturn = .64f, endReturn = .91f;
            float travel = Mathf.SmoothStep(0, 1, phase / endPush)
                - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(startReturn, endReturn, phase));
            float curl = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(endPush, startReturn, phase))
                * (1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(endReturn, 1, phase)));
            palmMaterial.SetFloat(FingerCurl, curl);

            // A fixed three-quarter view keeps the fingers visible, including on the vertical strokes.
            Vector3 fingers = push.y == 0 ? Vector3.up : Vector3.forward;
            var facing = Quaternion.LookRotation(push + Vector3.back * .4f, fingers);
            for (int i = 0; i < hands.Length; i++)
            {
                float x = i == 0 ? -.17f : .17f;
                hands[i].localPosition = new Vector3(x, -.03f, 0) + push * ((travel - .5f) * .16f);
                hands[i].localRotation = facing;
            }
        }

        void ShowBackStroke()
        {
            // Slow edge-on recovery, then a quicker push with the palm facing the water.
            bool pushing = phase < .42f;
            float travel = pushing ? Mathf.SmoothStep(0, 1, phase / .42f)
                : 1f - Mathf.SmoothStep(0, 1, (phase - .42f) / .58f);
            float roll = Mathf.SmoothStep(0, 1, Mathf.Clamp01((phase - .42f) / .09f)) *
                Mathf.SmoothStep(0, 1, Mathf.Clamp01((1 - phase) / .09f));
            for (int i = 0; i < hands.Length; i++)
            {
                float side = i == 0 ? -1 : 1;
                hands[i].localPosition = new Vector3(side * .17f, -.03f, (travel - .5f) * .16f);
                hands[i].localRotation = Quaternion.Euler(0, side * 80 * roll, 0);
            }
        }
    }
}
