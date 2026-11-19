using UnityEngine;
using UnityEngine.UI;

namespace Reefkeeper.Corals
{
    /// <summary>
    /// Temporary coral HUD, built from the Figma "V2 · Coral gameplay HUD" page:
    /// a Coral Status card beside the aim dot for the planted coral being looked at (name, stage
    /// pips, stage progress, and Growing / Moss / Fertilize / Fully grown), and Lucky Cat's speech
    /// bubble above the cat. Prompts and objectives go through the existing ReefCleaningHUD.
    /// </summary>
    [DefaultExecutionOrder(210)]
    public sealed class CoralGameplayHUD : MonoBehaviour
    {
        public CoralPlanting planting;
        public CoralShop shop;
        public Font font, boldFont;
        public Sprite panel, dot, leaf, moss, check, fertilize, mouseLeft, bubbleTail;

        // Figma "Reefkeeper HUD" colour variables.
        static readonly Color Navy = new Color(0.15f, 0.22f, 0.28f, 0.94f);
        static readonly Color Cream = new Color(0.97f, 0.93f, 0.81f);
        static readonly Color Green = new Color(0.68f, 0.79f, 0.36f);
        static readonly Color Amber = new Color(0.93f, 0.72f, 0.36f);
        static readonly Color Ink = new Color(0.12f, 0.18f, 0.22f);
        const float CardWidth = 272, TrackWidth = 176;

        RectTransform canvasRect, card, fill, status, button, bubble;
        CanvasGroup cardGroup, bubbleGroup;
        Text title, stageLabel, percent, statusLabel, bubbleLine;
        Image statusIcon, fillImage;
        readonly Image[] pipRings = new Image[3], pips = new Image[3];
        CoralNurture shown;
        XRWorldHud worldHud;

        void Awake()
        {
            var go = new GameObject("Coral HUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            go.layer = 5;
            go.transform.SetParent(transform, false);
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 81;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
            scaler.matchWidthOrHeight = 1;
            canvasRect = (RectTransform)go.transform;
            worldHud = go.AddComponent<XRWorldHud>();   // floats in front of the player in a headset
            BuildCard();
            BuildBubble();
        }

        // ---------------------------------------------------------------- Coral Status card

        void BuildCard()
        {
            // Left edge 44 px right of the aim dot, centred on it.
            card = Rect("Coral Status", canvasRect, new Vector2(0.5f, 0.5f), new Vector2(0, 0.5f), new Vector2(44, 0), new Vector2(CardWidth, 132));
            cardGroup = card.gameObject.AddComponent<CanvasGroup>();
            cardGroup.alpha = 0; cardGroup.interactable = false; cardGroup.blocksRaycasts = false;
            Panel(card, Navy, 2);

            title = Label(card, "Coral name", TopLeft(20, 16, 232, 24), 18, true, Cream);
            stageLabel = Label(card, "Stage label", TopLeft(20, 48, 140, 20), 14, false, Cream);
            stageLabel.color = new Color(Cream.r, Cream.g, Cream.b, 0.75f);
            for (int i = 0; i < 3; i++)
            {
                // Right-aligned pips; the current stage gets a cream ring.
                float x = CardWidth - 20 - 6 - (2 - i) * 18;
                pipRings[i] = Icon(card, "Pip ring " + i, dot, new Vector2(x, -58), 14, Cream);
                pips[i] = Icon(card, "Pip " + i, dot, new Vector2(x, -58), 10, Green);
            }

            var track = Rect("Progress track", card, new Vector2(0, 1), new Vector2(0, 0.5f), new Vector2(20, -86), new Vector2(TrackWidth, 8));
            Panel(track, new Color(Cream.r, Cream.g, Cream.b, 0.18f), 13);
            fill = Rect("Progress fill", track, new Vector2(0, 0.5f), new Vector2(0, 0.5f), Vector2.zero, new Vector2(TrackWidth, 8));
            fillImage = Panel(fill, Green, 13);
            percent = Label(card, "Progress label", TopLeft(204, 77, 48, 18), 14, true, Cream);
            percent.alignment = TextAnchor.MiddleRight;

            status = Rect("Status row", card, new Vector2(0, 1), new Vector2(0, 1), new Vector2(20, -104), new Vector2(232, 20));
            statusIcon = Icon(status, "Status icon", leaf, new Vector2(9, -10), 18, Cream);
            statusLabel = Label(status, "Status text", TopLeft(26, 0, 206, 20), 14, false, Cream);

            button = Rect("Fertilize button", card, new Vector2(0, 1), new Vector2(0, 1), new Vector2(20, -100), new Vector2(232, 36));
            Panel(button, Green, 3);
            Icon(button, "Mouse", mouseLeft, new Vector2(74, -18), 20, Ink);
            Icon(button, "Fertilize icon", fertilize, new Vector2(98, -18), 20, Ink);
            var label = Label(button, "Fertilize", TopLeft(112, 0, 100, 36), 15, true, Ink);
            label.text = "Fertilize";
            label.alignment = TextAnchor.MiddleLeft;
            label.GetComponent<Shadow>().enabled = false;
        }

        void UpdateCard()
        {
            var hovered = planting ? planting.Hovered : null;
            // While scrubbing, the existing arc shows the cleaning progress in the same spot.
            if (planting && planting.brushes && planting.brushes.ToolHeld && planting.brushes.SpotInReach) hovered = null;
            if (hovered) shown = hovered;
            cardGroup.alpha = Mathf.MoveTowards(cardGroup.alpha, hovered ? 1 : 0, Time.unscaledDeltaTime * 8);
            if (!shown || cardGroup.alpha <= 0) return;

            int stage = shown.Stage;
            title.text = shown.DisplayName;
            stageLabel.text = $"Stage {stage + 1} of {CoralRules.StageCount}";
            for (int i = 0; i < 3; i++)
            {
                pips[i].color = i <= stage ? Green : new Color(Cream.r, Cream.g, Cream.b, 0.22f);
                pipRings[i].enabled = i == stage;
            }

            float p = shown.Progress;
            fill.gameObject.SetActive(p > 0.01f);
            fill.sizeDelta = new Vector2(Mathf.Max(12, TrackWidth * p), 8);
            fillImage.color = shown.Paused ? Amber : Green;
            percent.text = Mathf.FloorToInt(p * 100) + "%";

            bool ready = shown.Ready && !shown.Paused;
            button.gameObject.SetActive(ready);
            status.gameObject.SetActive(!ready);
            card.sizeDelta = new Vector2(CardWidth, ready ? 150 : 132);
            if (ready) return;
            if (shown.Paused) SetStatus(moss, shown.socket.moss.SurfaceName + " is blocking growth", Amber, 1);
            else if (shown.FullyGrown) SetStatus(check, "Fully grown", Green, 1);
            else if (shown.Ready) SetStatus(fertilize, "Ready to fertilize", Green, 1);
            else SetStatus(leaf, "Growing", Cream, 0.75f);
        }

        void SetStatus(Sprite icon, string text, Color color, float alpha)
        {
            statusIcon.sprite = icon; statusIcon.color = color;
            statusLabel.text = text; statusLabel.color = new Color(color.r, color.g, color.b, alpha);
        }

        // ---------------------------------------------------------------- Lucky Cat bubble

        void BuildBubble()
        {
            // Pivot at the tail tip so the bubble can be placed straight over the cat's head.
            bubble = Rect("Cat Bubble", canvasRect, Vector2.zero, new Vector2(0.14f, 0), Vector2.zero, new Vector2(320, 80));
            bubbleGroup = bubble.gameObject.AddComponent<CanvasGroup>();
            bubbleGroup.alpha = 0; bubbleGroup.interactable = false; bubbleGroup.blocksRaycasts = false;
            var body = Rect("Body", bubble, new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 17), new Vector2(320, 80));
            body.anchorMax = new Vector2(1, 1);
            body.offsetMin = new Vector2(0, 17); body.offsetMax = Vector2.zero;
            Panel(body, Navy, 2.4f);
            var tail = Rect("Tail", bubble, Vector2.zero, Vector2.zero, new Vector2(30, 0), new Vector2(28, 18));
            var tailImage = tail.gameObject.AddComponent<Image>();
            tailImage.sprite = bubbleTail; tailImage.color = Navy; tailImage.raycastTarget = false;
            var speaker = Label(body, "Speaker", TopLeft(18, 12, 284, 18), 13, true, Green);
            speaker.text = "Lucky Cat";
            bubbleLine = Label(body, "Line", TopLeft(18, 32, 284, 40), 16, false, Cream);
            bubbleLine.alignment = TextAnchor.UpperLeft;
            bubbleLine.horizontalOverflow = HorizontalWrapMode.Wrap;
            bubbleLine.verticalOverflow = VerticalWrapMode.Overflow;
        }

        void UpdateBubble()
        {
            string line = shop ? shop.Line : "";
            var view = planting && planting.corals ? planting.corals.View : null;
            bool visible = line.Length > 0 && view && shop.catCollider;
            Vector2 local = Vector2.zero;
            if (visible)
            {
                var top = shop.catCollider.bounds.center + Vector3.up * (shop.catCollider.bounds.extents.y + 0.25f);
                var screen = view.WorldToScreenPoint(top);
                var uiCamera = worldHud && worldHud.InWorld ? view : null;
                visible = screen.z > 0 && RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, uiCamera, out local);
            }
            bubbleGroup.alpha = Mathf.MoveTowards(bubbleGroup.alpha, visible ? 1 : 0, Time.unscaledDeltaTime * 6);
            if (!visible) return;
            bubbleLine.text = line;
            float height = Mathf.Ceil(bubbleLine.preferredHeight);
            bubbleLine.rectTransform.sizeDelta = new Vector2(284, height);
            bubble.sizeDelta = new Vector2(320, 17 + 32 + height + 16);
            // Keep it on screen.
            var half = canvasRect.rect.size * 0.5f;
            local.x = Mathf.Clamp(local.x, -half.x + 50, half.x - 290);
            local.y = Mathf.Clamp(local.y, -half.y + 10, half.y - bubble.sizeDelta.y - 10);
            bubble.anchoredPosition = local + half;
        }

        void LateUpdate()
        {
            UpdateCard();
            UpdateBubble();
        }

        // ---------------------------------------------------------------- helpers

        static (Vector2 pos, Vector2 size) TopLeft(float x, float y, float w, float h) => (new Vector2(x, -y), new Vector2(w, h));

        static RectTransform Rect(string name, Transform parent, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.gameObject.layer = 5;
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        Image Panel(RectTransform rect, Color color, float pixelsPerUnitMultiplier)
        {
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = panel; image.type = Image.Type.Sliced; image.color = color;
            image.pixelsPerUnitMultiplier = pixelsPerUnitMultiplier; image.raycastTarget = false;
            return image;
        }

        static Image Icon(RectTransform parent, string name, Sprite sprite, Vector2 center, float size, Color color)
        {
            var rect = Rect(name, parent, new Vector2(0, 1), new Vector2(0.5f, 0.5f), center, new Vector2(size, size));
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite; image.color = color; image.preserveAspect = true; image.raycastTarget = false;
            return image;
        }

        Text Label(RectTransform parent, string name, (Vector2 pos, Vector2 size) r, int size, bool bold, Color color)
        {
            var rect = Rect(name, parent, new Vector2(0, 1), new Vector2(0, 1), r.pos, r.size);
            var text = rect.gameObject.AddComponent<Text>();
            text.font = bold ? boldFont : font; text.fontSize = size; text.color = color;
            text.alignment = TextAnchor.MiddleLeft; text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;   // Nunito line height exceeds the font size
            var shadow = text.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0, 0.04f, 0.05f, 0.35f); shadow.effectDistance = new Vector2(0, -1);
            return text;
        }
    }
}
