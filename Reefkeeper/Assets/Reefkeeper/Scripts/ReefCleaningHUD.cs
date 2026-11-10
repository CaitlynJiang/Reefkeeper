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
        // Shown while the dive journal is open: how to draw, look around (desktop) and close it.
        RectTransform journalHints;
        readonly Image[] journalHintIcons = new Image[3];
        readonly Text[] journalHintLabels = new Text[3];
        // Headset: no keyboard, so the left menu button toggles Controls and left X folds Objectives.
        // Left Y belongs to the dive-card journal (DiveCardMock). Key icons become button labels.
        InputAction helpButton, objectivesButton;
        bool helpWasDown, objectivesWasDown;
        Image helpKeyIcon, closeHelpKeyIcon, objectiveKeyIcon;
        Text closeHelpLetter, objectiveLetter;
        static bool Headset => UnityEngine.XR.XRSettings.isDeviceActive;
        XRInteractionSimulator simulator;
        ReefSoundscape soundscape;
        AudioSource MusicSource => soundscape ? soundscape.MusicSource : null;
        readonly Dictionary<string, Sprite> glyphs = new Dictionary<string, Sprite>();
        readonly List<Image> helpIcons = new List<Image>(), helpSecondaryIcons = new List<Image>();
        readonly List<Text> helpLabels = new List<Text>();
        float completedAt = -1, displayedProgress;
        bool collapsedOnComplete;
        const float CompletionHold = 1.2f, CompletionFade = 0.6f;
        const int MusicRow = 13;   // last row; row 12 lists the dive journal
        static readonly Color Cream = new Color(0.97f, 0.93f, 0.81f);
        static readonly Color Navy = new Color(0.15f, 0.22f, 0.28f, 0.94f);
        public float DisplayedProgress => displayedProgress;
        public string Status => pickup.ActivePatch.Complete ? "Restoration complete" : promptLabel.text;
        public bool ProgressVisible => progress && progress.gameObject.activeInHierarchy && progressGroup.alpha > 0;
        public bool HelpVisible { get; private set; }
        public bool ObjectivesExpanded { get; private set; } = true;
        public void ToggleHelp() => HelpVisible = !HelpVisible;
        public void ToggleObjectives() => ObjectivesExpanded = !ObjectivesExpanded;
        public bool UsesQuestPrompts => pickup.workshop.xrGroup.activeSelf && !pickup.CenterMode;
        public string CurrentPromptGlyph => promptIcon.enabled && promptIcon.sprite ? promptIcon.sprite.name : "";
        public string ControllerGripGlyph => GripGlyph(pickup.ActiveInteractable.isSelected || pickup.ActiveInteractable.isHovered
            ? pickup.ActiveInteractable : pickup.corals.Aimed);

        void Awake()
        {
            soundscape = GetComponent<ReefSoundscape>();
            foreach (var sprite in inputGlyphs) if (sprite) glyphs[sprite.name] = sprite;
            simulator = pickup.workshop.xrGroup.GetComponentInChildren<XRInteractionSimulator>(true);
            var go = new GameObject("Reef HUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            go.layer = 5;
            go.transform.SetParent(transform, false);
            canvas = go.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 80;
            var scaler = go.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720); scaler.matchWidthOrHeight = 1;
            go.AddComponent<XRWorldHud>();   // floats in front of the player in a headset

            objectives = Rect("Objectives", go.transform, new Vector2(0, 1), new Vector2(178, -64), new Vector2(308, 86));
            Background(objectives);
            var objectiveTitle = Label(objectives, "Objectives title", new Vector2(0, 19), new Vector2(260, 22), 16, true);
            objectiveTitle.text = "Objectives"; objectiveTitle.alignment = TextAnchor.MiddleLeft;
            objectiveKeyIcon = Icon(objectives, "Objective key", new Vector2(120, 19), new Vector2(28, 28));
            SetIcon(objectiveKeyIcon, "keyboard_o");
            objectiveLetter = Label(objectiveKeyIcon.transform, "Controller button", Vector2.zero, new Vector2(28, 28), 16, true);
            objectiveLetter.text = "X"; objectiveLetter.enabled = false;
            objectiveBody = Label(objectives, "Objective", new Vector2(0, -13), new Vector2(260, 28), 18);
            objectiveBody.alignment = TextAnchor.MiddleLeft;

            helpKeyIcon = Icon(go.transform, "Help key", new Vector2(40, 26), new Vector2(32, 28), Vector2.zero);
            SetIcon(helpKeyIcon, "keyboard_f1");
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

            helpPanel = Rect("Foldable controls", go.transform, new Vector2(0, 1), new Vector2(228, -354), new Vector2(408, 468));
            Background(helpPanel);
            var helpTitle = Label(helpPanel, "Controls title", new Vector2(0, 198), new Vector2(344, 28), 21, true);
            helpTitle.text = "Controls"; helpTitle.alignment = TextAnchor.MiddleLeft;
            closeHelpKeyIcon = Icon(helpPanel, "Close controls key", new Vector2(150, 198), new Vector2(32, 28));
            SetIcon(closeHelpKeyIcon, "keyboard_f1");
            closeHelpLetter = Label(helpPanel, "Close controls button", new Vector2(150, 198), new Vector2(32, 28), 18, true);
            closeHelpLetter.text = "Menu"; closeHelpLetter.fontSize = 14; closeHelpLetter.enabled = false;
            closeHelpLetter.horizontalOverflow = HorizontalWrapMode.Overflow;
            // "menu" is the control name in both the OpenXR (Quest) and PICO controller layouts.
            helpButton = new InputAction("Controls", InputActionType.Button, "<XRController>{LeftHand}/menu");
            objectivesButton = new InputAction("Objectives", InputActionType.Button, "<XRController>{LeftHand}/primaryButton");
            helpButton.Enable(); objectivesButton.Enable();
            for (int i = 0; i < MusicRow + 1; i++)
            {
                var row = Rect("Control " + i, helpPanel, Vector2.one * 0.5f, new Vector2(0, 158 - i * 28), new Vector2(344, 26));
                helpIcons.Add(Icon(row, "Input", new Vector2(-150, 0), new Vector2(48, 24)));
                helpSecondaryIcons.Add(Icon(row, "Alternative input", new Vector2(-112, 0), new Vector2(36, 24)));
                var label = Label(row, "Action", Vector2.zero, new Vector2(344, 26), 16);
                label.alignment = TextAnchor.MiddleLeft; helpLabels.Add(label);
            }
            // Journal hints: pills stacked up from the context-hint position, same style.
            journalHints = Rect("Dive journal hints", go.transform, new Vector2(0.5f, 0), new Vector2(0, 0), new Vector2(420, 200));
            journalHints.pivot = new Vector2(0.5f, 0); journalHints.anchoredPosition = new Vector2(0, 30);
            for (int i = 0; i < journalHintLabels.Length; i++)
            {
                var pill = Rect("Journal hint " + i, journalHints, new Vector2(0.5f, 0), new Vector2(0, 23 + i * 54), new Vector2(420, 46));
                Background(pill);
                journalHintIcons[i] = Icon(pill, "Input glyph", new Vector2(-172, 0), new Vector2(34, 34));
                journalHintLabels[i] = Label(pill, "Hint text", new Vector2(18, 0), new Vector2(340, 34), 17);
                journalHintLabels[i].alignment = TextAnchor.MiddleLeft;
            }
            journalHints.gameObject.SetActive(false);
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
            // Rising edges, so a held button toggles once.
            bool helpDown = helpButton.IsPressed(), objectivesDown = objectivesButton.IsPressed();
            if (helpDown && !helpWasDown) HelpVisible = !HelpVisible;
            if (objectivesDown && !objectivesWasDown) ObjectivesExpanded = !ObjectivesExpanded;
            helpWasDown = helpDown; objectivesWasDown = objectivesDown;

            var keyboard = Keyboard.current; if (keyboard == null) return;
            if (keyboard.f1Key.wasPressedThisFrame) HelpVisible = !HelpVisible;
            if (keyboard.oKey.wasPressedThisFrame) ObjectivesExpanded = !ObjectivesExpanded;
            if (keyboard.mKey.wasPressedThisFrame)
            {
                var music = MusicSource;
                if (music) music.mute = !music.mute;
            }
        }
        void LateUpdate()
        {
            if (!pickup.View) return;
            var aimDot=canvas.transform.Find("Aim dot");if(aimDot)aimDot.gameObject.SetActive(!DiveCardMock.BlocksControls);
            bool journalOpen = DiveCardMock.Instance && DiveCardMock.Instance.IsOpen;
            journalHints.gameObject.SetActive(journalOpen);
            if (journalOpen) UpdateJournalHints();
            if (DiveCardMock.BlocksControls || ReefToolMenu.Instance && ReefToolMenu.Instance.IsOpen)
            { prompt.gameObject.SetActive(false); progress.gameObject.SetActive(false); helpPanel.gameObject.SetActive(false); return; }
            helpPanel.gameObject.SetActive(HelpVisible);
            bool headset = Headset;
            helpHint.text = (headset ? "Menu   " : "") + (HelpVisible ? "Close controls" : "Controls");
            helpHint.rectTransform.anchoredPosition = new Vector2(headset ? 106 : 142, 26);
            helpHint.horizontalOverflow = HorizontalWrapMode.Overflow;
            helpKeyIcon.enabled = !headset && helpKeyIcon.sprite;
            closeHelpKeyIcon.enabled = !headset && closeHelpKeyIcon.sprite; closeHelpLetter.enabled = headset;
            objectiveKeyIcon.enabled = !headset && objectiveKeyIcon.sprite; objectiveLetter.enabled = headset;
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
                : pickup.SpotInReach);
            if (!planting && complete && completionAge >= CompletionHold + CompletionFade && !collapsedOnComplete)
            { ObjectivesExpanded = false; collapsedOnComplete = true; }
            objectiveBody.text = shop && shop.AwaitingJournal ? $"Open your dive journal ({Corals.CoralShop.JournalKey})" : planting ? planting.Objective : complete ? "Nursery restored" : "Clean the nursery frame";
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
                if (pickup.ToolHeld) { context = pickup.ActiveTool.Scrubbing ? "" : pickup.Carrying ? "A: switch tools" : "Release to let go of " + pickup.ActiveTool.ToolName; if (pickup.Carrying) key = ""; }
                else if (pickup.ActiveInteractable.isHovered) context = "Hold to grab " + pickup.ActiveTool.ToolName;
                else if (pickup.corals.HasPrompt) context = pickup.corals.Held ? "Hold to replace coral" : "Hold to pick up coral";
            }
            else
            {
                key = "keyboard_g";
                context = pickup.AimingBrush ? "Pick up " + pickup.AimedToolName : pickup.corals.HasPrompt
                    ? pickup.corals.Held ? "Replace coral" : "Pick up coral" : "";
                if (pickup.SpotInReach && pickup.Carrying)
                { context = complete || pickup.IsScrubbing ? "" : pickup.ActiveTool.toolKind == CleaningToolKind.Scraper ? "Hold to scrape" : "Hold to scrub"; key = "mouse_left"; }
                else if (!complete && pickup.SpotInReach && context.Length == 0)
                { context = pickup.Target.CleaningHint; key = ""; }
            }
            // Coral loop prompts (talk, plant, fertilize, moss notes); scrubbing keeps priority.
            if (shop && shop.AimingCat) { context = shop.Prompt; key = shop.PromptKey; }
            else if (shop && shop.JournalPrompt.Length > 0) { context = shop.JournalPrompt; key = ""; }
            else if (planting && planting.Prompt.Length > 0 && !(pickup.SpotInReach && pickup.ToolHeld))
            { context = planting.Prompt; key = planting.PromptKey; }
            if (pickup.SpotInReach && pickup.ToolHeld && !pickup.CorrectTool)
            { context = pickup.Target.CleaningHint; key = ""; }
            if (!pickup.workshop.xrGroup.activeSelf && Cursor.lockState != CursorLockMode.Locked && !pickup.Automated && !pickup.corals.Automated)
            { context = "Click to dive"; key = "mouse_left"; }
            SetIcon(promptIcon, key);
            promptLabel.rectTransform.anchoredPosition = new Vector2(promptIcon.enabled ? 24 : 0, 0);
            promptLabel.rectTransform.sizeDelta = new Vector2(promptIcon.enabled ? 250 : 300, 38);
            promptLabel.text = context; prompt.gameObject.SetActive(context.Length > 0);
        }
        void UpdateHelp()
        {
            var music = MusicSource;
            HelpRow(MusicRow, "", music && music.mute ? "M   Unmute BGM" : "M   Mute BGM");
            helpLabels[MusicRow].transform.parent.gameObject.SetActive(music && music.clip);
            bool xr = pickup.workshop.xrGroup.activeSelf;
            if (Headset)
            {
                // Real headset: controller bindings only (ReefXRSwim, CoralShop, CoralPlanting, ReefToolMenu).
                string grip = ControllerGripGlyph;
                HelpRow(0, "", "Left stick   Swim");
                HelpRow(1, "", "Click left stick   Boost");
                HelpRow(2, "", "Right stick   Turn / rise and sink");
                HelpRow(3, grip, "Hold grip   Grab a tool or coral");
                HelpRow(4, grip, "Grip at Lucky Cat   Talk");
                HelpRow(5, "", "Trigger   Plant / fertilize");
                HelpRow(6, "", "Brush algae; scrape fire coral");
                HelpRow(7, "", "A   Tools menu     B   Close it");
                HelpRow(8, "", "X   Fold objectives");
                HelpRow(9, "", "Y   Dive journal     B   Close it");
                HelpRow(10, "", "Menu   Show / hide controls");
                for (int i = 11; i < helpLabels.Count; i++) HelpRow(i, "", "");
                helpLabels[MusicRow].transform.parent.gameObject.SetActive(false);
                return;
            }
            if (UsesQuestPrompts)
            {
                string grip = ControllerGripGlyph;
                HelpRow(0, grip, "Hold to grab a tool"); HelpRow(1, grip, "Release to let go of tool");
                HelpRow(2, "", "Brush algae; scrape fire coral"); HelpRow(3, "", "Scraper: push and pull");
                HelpRow(4, "keyboard_g", "Simulator: hold for Grip");
                HelpRow(5, "keyboard_w", "WASD  Move controller"); HelpRow(6, "keyboard_q", "Down / up", "keyboard_e");
                HelpRow(7, "keyboard_tab", ReefToolMenu.Instance ? "Tools; ` cycles device mode" : "Body / device mode"); HelpRow(8, "", "[ / ]  Select left / right hand");
                HelpRow(9, "mouse_right", "Toggle mouse look"); HelpRow(10, "keyboard_f2", "Desktop / XR simulator");
                HelpRow(11, "keyboard_f5", "Reset nursery");
                HelpRow(12, "", "J   Dive journal     Esc   Close it"); return;
            }
            HelpRow(0, "keyboard_w", "WASD  Swim");
            HelpRow(1, xr ? "keyboard_q" : "keyboard_space", xr ? "Down / up" : "Up / down", xr ? "keyboard_e" : "keyboard_c");
            HelpRow(2, "keyboard_shift", "Boost"); HelpRow(3, xr ? "keyboard_b" : "mouse_right", "Brake", xr ? "" : "keyboard_x");
            HelpRow(4, "keyboard_g", "Talk / plant / pick up a tool"); HelpRow(5, "mouse_left", "Hold: clean  Click: fertilize");
            HelpRow(6, "keyboard_h", "Return tools to tray");
            HelpRow(7, ReefToolMenu.Instance ? "keyboard_tab" : xr ? "keyboard_tab" : "keyboard_escape", ReefToolMenu.Instance ? "Tools; ` cycles simulator mode" : xr ? "Body / device mode" : "Cursor; click to resume");
            HelpRow(8, xr ? "mouse_right" : "keyboard_r", xr ? "Toggle mouse look" : "Return to start");
            HelpRow(9, "keyboard_f2", "Desktop / XR simulator"); HelpRow(10, "keyboard_f5", "Reset nursery");
            HelpRow(11, "keyboard_f11", "Fullscreen");
            HelpRow(12, "", "J   Dive journal     Esc   Close it");
        }
        // Bottom row first. Inputs match DiveCardMock: desktop LMB draws, RMB looks, Esc / J close;
        // headset right trigger draws where the right controller points, B / Y close.
        void UpdateJournalHints()
        {
            if (Headset)
            {
                JournalHint(0, "", "B   Close journal   (or Y)");
                JournalHint(1, "", "Hold right trigger on the page to draw");
                JournalHint(2, null, "");
            }
            else
            {
                JournalHint(0, "keyboard_escape", "Close journal   (or J)");
                JournalHint(1, "mouse_right", "Hold to look around");
                JournalHint(2, "mouse_left", "Hold and drag on the page to draw");
            }
        }
        void JournalHint(int index, string key, string text)
        {
            var pill = journalHintLabels[index].transform.parent.gameObject;
            pill.SetActive(key != null);
            if (key == null) return;
            SetIcon(journalHintIcons[index], key);
            var label = journalHintLabels[index]; label.text = text;
            label.rectTransform.anchoredPosition = new Vector2(journalHintIcons[index].enabled ? 18 : 0, 0);
            label.rectTransform.sizeDelta = new Vector2(journalHintIcons[index].enabled ? 340 : 380, 34);
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
        void OnDestroy() { if (fillMaterial) Destroy(fillMaterial); helpButton?.Dispose(); objectivesButton?.Dispose(); }
    }
}
