using System;
using UnityEngine;
using UnityEngine.UI;

namespace Reefkeeper.Hands
{
    /// <summary>
    /// Bare-hands menu: turn the left palm up and a small panel floats above it; push the right
    /// index fingertip through a button to use it. Holds what the controllers reach with buttons:
    /// the two cleaning tools, the dive journal, Controls and Objectives.
    /// </summary>
    [DefaultExecutionOrder(120)]
    public sealed class ReefPalmMenu : MonoBehaviour
    {
        // Palm normal against world up: shows above On, hides below Off.
        const float PalmUpOn = 0.75f, PalmUpOff = 0.5f, ShowAfter = 0.3f, HideAfter = 0.25f;
        // Canvas units are half millimetres.
        const float Scale = 0.0005f, ButtonWidth = 200, ButtonHeight = 62, Gap = 14;
        const float Lift = 0.075f, PressDepth = 0.012f, ReleaseDepth = 0.03f;

        static readonly Color Navy = new Color(0.15f, 0.22f, 0.28f, 0.94f), Cream = new Color(.973f, .941f, .843f),
            Ink = new Color(.161f, .306f, .322f), Hover = new Color(0.27f, 0.42f, 0.47f, 0.96f);

        public static ReefPalmMenu Instance { get; private set; }
        public bool Visible { get; private set; }

        sealed class Entry
        {
            public string label;
            public Func<bool> available, on;
            public Action act;
            public RectTransform rect;
            public Image background;
            public Text text;
        }

        ReefWorkshop workshop;
        ReefBrushPickup pickup;
        ReefCleaningHUD hud;
        Canvas canvas;
        Entry[] entries;
        float upTime, downTime;
        int pressed = -1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            var workshop = FindFirstObjectByType<ReefWorkshop>();
            if (workshop && !workshop.GetComponent<ReefPalmMenu>()) workshop.gameObject.AddComponent<ReefPalmMenu>();
        }

        void Awake() { Instance = this; }

        void Start()
        {
            workshop = GetComponent<ReefWorkshop>();
            pickup = GetComponent<ReefBrushPickup>();
            hud = GetComponent<ReefCleaningHUD>();
            entries = new[]
            {
                ToolEntry("Brush", CleaningToolKind.Brush, () => workshop.brush),
                ToolEntry("Scraper", CleaningToolKind.Scraper, () => workshop.scraper),
                new Entry
                {
                    label = "Journal",
                    available = () => DiveCardMock.Instance && (DiveCardMock.Instance.Available || DiveCardMock.Instance.IsOpen),
                    on = () => DiveCardMock.Instance && DiveCardMock.Instance.IsOpen,
                    act = () => DiveCardMock.Instance.SetOpen(!DiveCardMock.Instance.IsOpen),
                },
                new Entry { label = "Controls", available = () => hud, on = () => hud.HelpVisible, act = () => hud.ToggleHelp() },
                new Entry { label = "Objectives", available = () => hud, on = () => hud.ObjectivesExpanded, act = () => hud.ToggleObjectives() },
            };
            Build();
            SetVisible(false);
        }

        Entry ToolEntry(string label, CleaningToolKind kind, Func<ReefBrush> tool) => new Entry
        {
            label = label,
            available = () => tool() && ReefToolMenu.Instance && ReefToolMenu.Instance.Owns(kind),
            on = () => pickup.ToolHeld && pickup.ActiveTool && pickup.ActiveTool.toolKind == kind,
            act = () =>
            {
                if (pickup.ToolHeld && pickup.ActiveTool.toolKind == kind) workshop.ReturnTool(pickup.ActiveTool);
                else ReefToolMenu.Instance.TryEquip((int)kind);
            },
        };

        void Build()
        {
            var root = new GameObject("Palm menu", typeof(RectTransform));
            canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 50;
            root.AddComponent<CanvasScaler>().dynamicPixelsPerUnit = 3;
            var rootRect = (RectTransform)root.transform;
            rootRect.sizeDelta = new Vector2(ButtonWidth, entries.Length * (ButtonHeight + Gap));
            root.transform.localScale = Vector3.one * Scale;
            Font font = hud ? (hud.boldFont ? hud.boldFont : hud.font) : null;
            for (int i = 0; i < entries.Length; i++)
            {
                var e = entries[i];
                var go = new GameObject(e.label, typeof(RectTransform));
                e.rect = (RectTransform)go.transform;
                e.rect.SetParent(rootRect, false);
                e.rect.sizeDelta = new Vector2(ButtonWidth, ButtonHeight);
                // First entry on top.
                e.rect.anchoredPosition = new Vector2(0, (entries.Length - 1) * 0.5f * (ButtonHeight + Gap) - i * (ButtonHeight + Gap));
                e.background = go.AddComponent<Image>();
                if (hud && hud.panelSprite) { e.background.sprite = hud.panelSprite; e.background.type = Image.Type.Sliced; e.background.pixelsPerUnitMultiplier = 6; }
                e.background.raycastTarget = false;
                var label = new GameObject("Label", typeof(RectTransform));
                var labelRect = (RectTransform)label.transform;
                labelRect.SetParent(e.rect, false);
                labelRect.anchorMin = Vector2.zero; labelRect.anchorMax = Vector2.one; labelRect.offsetMin = labelRect.offsetMax = Vector2.zero;
                e.text = label.AddComponent<Text>();
                e.text.font = font; e.text.fontSize = 30; e.text.alignment = TextAnchor.MiddleCenter; e.text.raycastTarget = false;
                e.text.text = e.label;
            }
        }

        void SetVisible(bool value)
        {
            Visible = value;
            if (canvas) canvas.gameObject.SetActive(value);
            if (!value) pressed = -1;
        }

        void LateUpdate()
        {
            var view = Camera.main;
            if (!view || !canvas || !ReefInput.HandsMode || !workshop.xrGroup.activeSelf) { upTime = 0; SetVisible(false); return; }
            var space = view.transform.parent;
            var left = ReefHands.Get(0);
            Vector3 palm = space.TransformPoint(left.Palm.position);
            Vector3 palmFacing = space.TransformDirection(left.Palm.rotation * Vector3.down);
            bool up = left.Tracked && !left.Grab && Vector3.Dot(palmFacing, Vector3.up) > (Visible ? PalmUpOff : PalmUpOn);
            upTime = up ? upTime + Time.deltaTime : 0;
            downTime = up ? 0 : downTime + Time.deltaTime;
            if (!Visible && upTime >= ShowAfter) SetVisible(true);
            else if (Visible && downTime >= HideAfter) SetVisible(false);
            if (!Visible) return;

            // Float above the palm, turned to the eyes.
            Vector3 target = palm + Vector3.up * Lift;
            var t = canvas.transform;
            bool fresh = upTime < ShowAfter + Time.deltaTime * 1.5f;
            t.position = fresh ? target : Vector3.Lerp(t.position, target, 1 - Mathf.Exp(-20 * Time.deltaTime));
            Vector3 away = t.position - view.transform.position;
            if (away.sqrMagnitude > 1e-4f) t.rotation = Quaternion.LookRotation(away, Vector3.up);

            // Right index fingertip: hover in front, press when it reaches the button face.
            var right = ReefHands.Get(1);
            int hovered = -1;
            if (right.Tracked && !right.Grab)
            {
                Vector3 tip = space.TransformPoint(right.IndexTip.position);
                for (int i = 0; i < entries.Length; i++)
                {
                    Vector3 local = entries[i].rect.InverseTransformPoint(tip);
                    // Canvas forward points away from the eyes, so an approaching finger has local z < 0.
                    float depth = -local.z * Scale;
                    if (!entries[i].rect.rect.Contains((Vector2)local) || depth > 0.05f || depth < -0.03f) continue;
                    hovered = i;
                    if (pressed < 0 && depth < PressDepth && entries[i].available())
                    {
                        pressed = i;
                        entries[i].act();
                    }
                    break;
                }
                if (pressed >= 0)
                {
                    Vector3 local = entries[pressed].rect.InverseTransformPoint(tip);
                    if (!entries[pressed].rect.rect.Contains((Vector2)local) || -local.z * Scale > ReleaseDepth) pressed = -1;
                }
            }
            else pressed = -1;

            for (int i = 0; i < entries.Length; i++)
            {
                var e = entries[i];
                bool available = e.available(), on = available && e.on();
                e.background.color = on ? Cream : hovered == i && available ? Hover : Navy;
                e.text.color = on ? Ink : new Color(Cream.r, Cream.g, Cream.b, available ? 1 : 0.4f);
                e.rect.localScale = Vector3.one * (pressed == i ? 0.94f : 1);
            }
        }

        void OnDestroy() { if (canvas) Destroy(canvas.gameObject); if (Instance == this) Instance = null; }
    }
}
