using UnityEngine;
using UnityEngine.UI;

namespace Reefkeeper.Corals
{
    /// <summary>
    /// The shop's floating price tag: a navy pill with the scallop icon and a price (cream, or soft
    /// red when the player can't afford it), or a short label such as "Gift". It hovers above an
    /// item and turns to face the viewer. Used by the cart and the counter.
    /// </summary>
    public sealed class ShopPriceTag
    {
        static readonly Color Cream = new Color(.97f, .93f, .81f), Panel = new Color(.15f, .22f, .28f, .94f), TooDear = new Color(.95f, .5f, .45f);
        static Sprite pillSprite;
        readonly Transform root;
        readonly Image pill;
        readonly RawImage icon;
        readonly Text text;
        readonly Material overlay;

        public ShopPriceTag(string name)
        {
            var shader = Resources.Load<Shader>("DiveCards/JournalOverlay");
            overlay = shader ? new Material(shader) { name = name + " overlay" } : null;
            var hud = Object.FindFirstObjectByType<ReefCleaningHUD>(FindObjectsInactive.Include);
            var font = hud && hud.boldFont ? hud.boldFont : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var go = new GameObject(name, typeof(RectTransform), typeof(Canvas)) { layer = 5 };
            root = go.transform;
            var canvas = go.GetComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace; canvas.sortingOrder = 40;
            ((RectTransform)root).sizeDelta = new Vector2(220, 64);
            root.localScale = Vector3.one * .0017f; // readable from a couple of metres
            pill = Child<Image>(root, "Pill", Vector2.zero, new Vector2(150, 60));
            pill.sprite = Pill(); pill.type = Image.Type.Sliced; pill.pixelsPerUnitMultiplier = 2.1f; pill.color = Panel;
            icon = Child<RawImage>(pill.transform, "Scallop", new Vector2(-34, 0), new Vector2(44, 39));
            icon.texture = Resources.Load<Texture2D>("Shop/scallop_coin");
            text = Child<Text>(pill.transform, "Price", new Vector2(22, 0), new Vector2(70, 60));
            text.font = font; text.fontSize = 34; text.color = Cream; text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            var shadow = text.gameObject.AddComponent<Shadow>(); shadow.effectColor = new Color(0, .04f, .05f, .35f); shadow.effectDistance = new Vector2(0, -2);
            root.gameObject.SetActive(false);
        }

        /// <summary>Shows a price above <paramref name="bounds"/>, red when not affordable.</summary>
        public void ShowPrice(Bounds bounds, Camera view, int price, bool affordable)
        {
            Place(bounds, view);
            icon.gameObject.SetActive(true);
            text.text = price.ToString(); text.fontSize = 34;
            text.color = affordable ? Cream : TooDear;
            Layout(150, new Vector2(22, 0));
        }

        /// <summary>Shows a short word instead of a price (e.g. "Gift").</summary>
        public void ShowLabel(Bounds bounds, Camera view, string label)
        {
            Place(bounds, view);
            icon.gameObject.SetActive(false);
            text.text = label; text.fontSize = 30; text.color = Cream;
            Layout(text.preferredWidth + 48, Vector2.zero);
        }

        public void Hide() { if (root) root.gameObject.SetActive(false); }

        public void Destroy()
        {
            if (root) Object.Destroy(root.gameObject);
            if (overlay) Object.Destroy(overlay);
        }

        void Place(Bounds b, Camera view)
        {
            root.gameObject.SetActive(view);
            if (!view) return;
            root.position = new Vector3(b.center.x, b.max.y + .09f, b.center.z);
            root.rotation = Quaternion.LookRotation(root.position - view.transform.position, Vector3.up);
        }

        void Layout(float width, Vector2 textAt)
        {
            pill.rectTransform.sizeDelta = new Vector2(width, 60);
            text.rectTransform.anchoredPosition = textAt;
        }

        T Child<T>(Transform parent, string name, Vector2 at, Vector2 size) where T : Graphic
        {
            var go = new GameObject(name, typeof(RectTransform)) { layer = 5 };
            go.transform.SetParent(parent, false);
            var r = (RectTransform)go.transform; r.anchoredPosition = at; r.sizeDelta = size;
            var g = go.AddComponent<T>(); g.raycastTarget = false; g.material = overlay;
            return g;
        }

        static Sprite Pill()
        {
            if (pillSprite) return pillSprite;
            const int size = 128; float r = size * .5f;
            var t = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "Price pill" };
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    px[y * size + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(r - Vector2.Distance(new Vector2(x + .5f, y + .5f), new Vector2(r, r))) * 255));
            t.SetPixels32(px); t.Apply(false, true);
            float border = r - 1;
            return pillSprite = Sprite.Create(t, new Rect(0, 0, size, size), new Vector2(.5f, .5f), 100, 0, SpriteMeshType.FullRect, new Vector4(border, border, border, border));
        }
    }
}
