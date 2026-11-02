using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Simulation;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace Reefkeeper
{
    [DefaultExecutionOrder(200)]
    public sealed class ReefCleaningHUD : MonoBehaviour
    {
        public ReefBrushPickup pickup;
        public Font font, boldFont;
        public Sprite panelSprite, arcTrack, arcFill;
        public Material arcMaterial;
        public Sprite[] inputGlyphs;
        [Tooltip("Coral planting loop; when set, its prompts and objectives take over.")]
        public Corals.CoralPlanting planting;
        public Corals.CoralShop shop;
        Canvas canvas;
        RectTransform progress, prompt, helpPanel, objectives;
        CanvasGroup progressGroup;
        Material fillMaterial;
        Text progressLabel, promptLabel, helpHint, objectiveBody;
        Image promptIcon;
        XRInteractionSimulator simulator;
        readonly Dictionary<string, Sprite> glyphs = new Dictionary<string, Sprite>();
        readonly List<Image> helpIcons = new List<Image>(), helpSecondaryIcons = new List<Image>();
        readonly List<Text> helpLabels = new List<Text>();
        float completedAt = -1, displayedProgress;
        bool collapsedOnComplete;
        const float CompletionHold = 1.2f, CompletionFade = 0.6f;
        static readonly Color Cream = new Color(0.97f, 0.93f, 0.81f);
        static readonly Color Navy = new Color(0.15f, 0.22f, 0.28f, 0.94f);
        public float DisplayedProgress => displayedProgress;
        public string Status => pickup.ActivePatch.Complete ? "Restoration complete" : promptLabel.text;
        public bool ProgressVisible => progress && progress.gameObject.activeInHierarchy && progressGroup.alpha > 0;
        public bool HelpVisible { get; private set; }
        public bool ObjectivesExpanded { get; private set; } = true;
        public bool UsesQuestPrompts => pickup.workshop.xrGroup.activeSelf && !pickup.CenterMode;
        public string CurrentPromptGlyph => promptIcon.enabled && promptIcon.sprite ? promptIcon.sprite.name : "";
        public string ControllerGripGlyph => GripGlyph(pickup.brush.isSelected || pickup.brush.isHovered
            ? pickup.brush : pickup.corals.Aimed);

        void Awake()
        {
            foreach (var sprite in inputGlyphs) if (sprite) glyphs[sprite.name] = sprite;
            simulator = pickup.workshop.xrGroup.GetComponentInChildren<XRInteractionSimulator>(true);
            var go = new GameObject("Reef HUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            go.layer = 5;
            go.transform.SetParent(transform, false);
            canvas = go.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 80;
            var scaler = go.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720); scaler.matchWidthOrHeight = 1;

            objectives = Rect("Objectives", go.transform, new Vector2(0, 1), new Vector2(178, -64), new Vector2(308, 86));
            Background(objectives);
            var objectiveTitle = Label(objectives, "Objectives title", new Vector2(0, 19), new Vector2(260, 22), 16, true);
            objectiveTitle.text = "Objectives"; objectiveTitle.alignment = TextAnchor.MiddleLeft;
            SetIcon(Icon(objectives, "Objective key", new Vector2(120, 19), new Vector2(28, 28)), "keyboard_o");
            objectiveBody = Label(objectives, "Objective", new Vector2(0, -13), new Vector2(260, 28), 18);
            objectiveBody.alignment = TextAnchor.MiddleLeft;

            SetIcon(Icon(go.transform, "Help key", new Vector2(40, 26), new Vector2(32, 28), Vector2.zero), "keyboard_f1");
            helpHint = Label(go.transform, "Help hint", new Vector2(142, 26), new Vector2(156, 24), 14, false, Vector2.zero);
            helpHint.alignment = TextAnchor.MiddleLeft;
            var dot = Rect("Aim dot", go.transform, Vector2.one * 0.5f, Vector2.zero, new Vector2(3, 3));
            dot.gameObject.AddComponent<Image>().color = Cream;

            progress = Rect("Arc progress", go.transform, Vector2.one * 0.5f, new Vector2(60, 0), new Vector2(220, 220));
            progressGroup = progress.gameObject.AddComponent<CanvasGroup>();
            progressGroup.interactable = false; progressGroup.blocksRaycasts = false;
            ArcLayer("Track", arcTrack, Color.white);
            var fill = ArcLayer("Fill", arcFill, new Color(0.68f, 0.79f, 0.36f));
            fillMaterial = new Material(arcMaterial); fill.material = fillMaterial;
            progressLabel = Label(progress, "Progress", new Vector2(74, -83), new Vector2(100, 26), 17, true);

            prompt = Rect("Context hint", go.transform, new Vector2(0.5f, 0), new Vector2(0, 74), new Vector2(352, 62));
            Background(prompt);
            promptLabel = Label(prompt, "Hint text", Vector2.zero, new Vector2(300, 38), 19);
            promptIcon = Icon(prompt, "Input glyph", new Vector2(-132, 0), new Vector2(42, 42));

            helpPanel = Rect("Foldable controls", go.transform, new Vector2(0, 1), new Vector2(228, -340), new Vector2(408, 440));
            Background(helpPanel);
            var helpTitle = Label(helpPanel, "Controls title", new Vector2(0, 184), new Vector2(344, 28), 21, true);
            helpTitle.text = "Controls"; helpTitle.alignment = TextAnchor.MiddleLeft;
            SetIcon(Icon(helpPanel, "Close controls key", new Vector2(150, 184), new Vector2(32, 28)), "keyboard_f1");
            for (int i = 0; i < 12; i++)
            {
                var row = Rect("Control " + i, helpPanel, Vector2.one * 0.5f, new Vector2(0, 144 - i * 28), new Vector2(344, 26));
                helpIcons.Add(Icon(row, "Input", new Vector2(-150, 0), new Vector2(48, 24)));
                helpSecondaryIcons.Add(Icon(row, "Alternative input", new Vector2(-112, 0), new Vector2(36, 24)));
                var label = Label(row, "Action", Vector2.zero, new Vector2(344, 26), 16);
                label.alignment = TextAnchor.MiddleLeft; helpLabels.Add(label);
            }
            helpPanel.gameObject.SetActive(false); progress.gameObject.SetActive(false); prompt.gameObject.SetActive(false);
        }
        Image ArcLayer(string name, Sprite sprite, Color tint)
        {
            // Same square rect, pivot and untrimmed sprite canvas for both supplied layers.
            var rect = Rect(name, progress, Vector2.one * 0.5f, Vector2.zero, new Vector2(220, 220));
            var image = rect.gameObject.AddComponent<Image>(); image.sprite = sprite; image.color = tint;
            image.type = Image.Type.Simple; image.preserveAspect = true; image.useSpriteMesh = false; image.raycastTarget = false;
            return image;
        }
        void Update()
        {
            var keyboard = Keyboard.current; if (keyboard == null) return;
            if (keyboard.f1Key.wasPressedThisFrame) HelpVisible = !HelpVisible;
            if (keyboard.oKey.wasPressedThisFrame) ObjectivesExpanded = !ObjectivesExpanded;
        }
        void LateUpdate()
        {
            if (!pickup.View) return;
            helpPanel.gameObject.SetActive(HelpVisible);
            helpHint.text = HelpVisible ? "Close controls" : "Controls";
            if (HelpVisible) UpdateHelp();

            displayedProgress = pickup.Progress;
            fillMaterial.SetFloat("_Progress", displayedProgress);
            bool complete = pickup.ActivePatch.Complete;
            if (!complete) { completedAt = -1; collapsedOnComplete = false; }
            else if (completedAt < 0) completedAt = Time.unscaledTime;
            float completionAge = complete ? Time.unscaledTime - completedAt : 0;
            progressGroup.alpha = complete ? Mathf.Clamp01(1 - (completionAge - CompletionHold) / CompletionFade) : 1;
            progressLabel.text = complete ? "Clean!" : (displayedProgress * 100).ToString("0") + "%";
            progress.gameObject.SetActive(complete ? completionAge < CompletionHold + CompletionFade
                : pickup.SpotInReach || (pickup.workshop.brush.Held && !pickup.CenterMode));
            if (!planting && complete && completionAge >= CompletionHold + CompletionFade && !collapsedOnComplete)
            { ObjectivesExpanded = false; collapsedOnComplete = true; }
            objectiveBody.text = planting ? planting.Objective : complete ? "Nursery restored" : "Clean the nursery frame";
            objectiveBody.gameObject.SetActive(ObjectivesExpanded);
            objectives.sizeDelta = new Vector2(308, ObjectivesExpanded ? 86 : 48);
            objectives.anchoredPosition = new Vector2(178, ObjectivesExpanded ? -64 : -45);
            var objectiveTitle = objectives.GetChild(0) as RectTransform;
            objectiveTitle.anchoredPosition = new Vector2(0, ObjectivesExpanded ? 19 : 0);
            ((RectTransform)objectives.Find("Objective key")).anchoredPosition = new Vector2(120, ObjectivesExpanded ? 19 : 0);

            string context = "", key = "";
            if (UsesQuestPrompts)
            {
                key = ControllerGripGlyph;
                if (pickup.brush.isSelected) context = pickup.workshop.brush.Scrubbing ? "" : "Release to let go of brush";
                else if (pickup.brush.isHovered) context = "Hold to grab brush";
                else if (pickup.corals.HasPrompt) context = pickup.corals.Held ? "Hold to replace coral" : "Hold to pick up coral";
            }
            else
            {
                key = "keyboard_g";
                context = pickup.AimingBrush ? "Pick up brush" : pickup.corals.HasPrompt
                    ? pickup.corals.Held ? "Replace coral" : "Pick up coral" : "";
                if (pickup.SpotInReach && pickup.Carrying)
                { context = complete || pickup.IsScrubbing ? "" : "Hold to scrub"; key = "mouse_left"; }
                else if (!complete && pickup.SpotInReach && context.Length == 0)
                { context = "Pick up the brush from the tray"; key = ""; }
            }
            // Coral loop prompts (talk, plant, fertilize, moss notes); scrubbing keeps priority.
            if (shop && shop.AimingCat) { context = shop.Prompt; key = shop.PromptKey; }
            else if (planting && planting.Prompt.Length > 0 && !(pickup.SpotInReach && pickup.Carrying))
            { context = planting.Prompt; key = planting.PromptKey; }
            if (!pickup.workshop.xrGroup.activeSelf && Cursor.lockState != CursorLockMode.Locked && !pickup.Automated && !pickup.corals.Automated)
            { context = "Click to dive"; key = "mouse_left"; }
            SetIcon(promptIcon, key);
            promptLabel.rectTransform.anchoredPosition = new Vector2(promptIcon.enabled ? 24 : 0, 0);
            promptLabel.rectTransform.sizeDelta = new Vector2(promptIcon.enabled ? 250 : 300, 38);
            promptLabel.text = context; prompt.gameObject.SetActive(context.Length > 0);
        }
        void UpdateHelp()
        {
            bool xr = pickup.workshop.xrGroup.activeSelf;
            if (UsesQuestPrompts)
            {
                string grip = ControllerGripGlyph;
                HelpRow(0, grip, "Hold to grab brush"); HelpRow(1, grip, "Release to let go of brush");
                HelpRow(2, "", "Move brush across the algae"); HelpRow(3, "", "Coral stays held after release");
                HelpRow(4, "keyboard_g", "Simulator: hold for Grip");
                HelpRow(5, "keyboard_w", "WASD  Move controller"); HelpRow(6, "keyboard_q", "Down / up", "keyboard_e");
                HelpRow(7, "keyboard_tab", "Body / device mode"); HelpRow(8, "", "[ / ]  Select left / right hand");
                HelpRow(9, "mouse_right", "Toggle mouse look"); HelpRow(10, "keyboard_f2", "Desktop / XR simulator");
                HelpRow(11, "keyboard_f5", "Reset nursery"); return;
            }
            HelpRow(0, "keyboard_w", "WASD  Swim");
            HelpRow(1, xr ? "keyboard_q" : "keyboard_space", xr ? "Down / up" : "Up / down", xr ? "keyboard_e" : "keyboard_c");
            HelpRow(2, "keyboard_shift", "Boost"); HelpRow(3, xr ? "keyboard_b" : "mouse_right", "Brake", xr ? "" : "keyboard_x");
            HelpRow(4, "keyboard_g", "Talk to cat / plant / pick up"); HelpRow(5, "mouse_left", "Hold: scrub  Click: fertilize");
            HelpRow(6, "keyboard_h", "Return brush");
            HelpRow(7, xr ? "keyboard_tab" : "keyboard_escape", xr ? "Body / device mode" : "Cursor; click to resume");
            HelpRow(8, xr ? "mouse_right" : "keyboard_r", xr ? "Toggle mouse look" : "Return to start");
            HelpRow(9, "keyboard_f2", "Desktop / XR simulator"); HelpRow(10, "keyboard_f5", "Reset nursery");
            HelpRow(11, "keyboard_f11", "Fullscreen");
        }
        void HelpRow(int index, string key, string text, string secondary = "")
        {
            SetIcon(helpIcons[index], key); SetIcon(helpSecondaryIcons[index], secondary);
            var label = helpLabels[index]; label.text = text;
            float x = secondary.Length > 0 ? 52 : key.Length > 0 ? 29 : 0;
            label.rectTransform.anchoredPosition = new Vector2(x, 0);
            label.rectTransform.sizeDelta = new Vector2(344 - x * 2, 26);
        }
        string GripGlyph(XRBaseInteractable target)
        {
            XRBaseInteractor hand = null;
            if (target)
            {
                foreach (var selected in target.interactorsSelecting)
                    if (selected is XRBaseInputInteractor input) { hand = input; break; }
                if (!hand) foreach (var hovered in target.interactorsHovering)
                    if (hovered is XRBaseInputInteractor input) { hand = input; break; }
            }
            bool left = hand ? hand.handedness == InteractorHandedness.Left : simulator && simulator.manipulatingLeftController;
            // The scene builder validates that controller Select actions are bound to GripButton.
            return left ? "quest_grip_left" : "quest_grip_right";
        }
        Image Icon(Transform parent, string name, Vector2 position, Vector2 size, Vector2? anchor = null)
        {
            var image = Rect(name, parent, anchor ?? Vector2.one * 0.5f, position, size).gameObject.AddComponent<Image>();
            image.preserveAspect = true; image.color = Cream; image.raycastTarget = false; return image;
        }
        void SetIcon(Image image, string name)
        {
            image.sprite = glyphs.TryGetValue(name, out var sprite) ? sprite : null;
            image.enabled = image.sprite;
        }
        void Background(RectTransform rect)
        {
            var image = rect.gameObject.AddComponent<Image>(); image.sprite = panelSprite;
            image.type = Image.Type.Sliced; image.color = Navy; image.raycastTarget = false;
            // The 1024px source border stays modest at the final HUD size.
            image.pixelsPerUnitMultiplier = 2;
        }
        static RectTransform Rect(string name, Transform parent, Vector2 anchor, Vector2 position, Vector2 size)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>(); rect.SetParent(parent, false);
            rect.gameObject.layer = 5;
            rect.anchorMin = rect.anchorMax = anchor; rect.pivot = Vector2.one * 0.5f;
            rect.anchoredPosition = position; rect.sizeDelta = size; return rect;
        }
        Text Label(Transform parent, string name, Vector2 position, Vector2 size, int sizeInPixels, bool bold = false, Vector2? anchor = null)
        {
            var text = Rect(name, parent, anchor ?? Vector2.one * 0.5f, position, size).gameObject.AddComponent<Text>();
            text.font = bold ? boldFont : font; text.fontSize = sizeInPixels;
            text.alignment = TextAnchor.MiddleCenter; text.color = Cream; text.raycastTarget = false;
            var shadow = text.gameObject.AddComponent<Shadow>(); shadow.effectColor = new Color(0, 0.04f, 0.05f, 0.35f); shadow.effectDistance = new Vector2(0, -1);
            return text;
        }
        void OnDestroy() { if (fillMaterial) Destroy(fillMaterial); }
    }
}
