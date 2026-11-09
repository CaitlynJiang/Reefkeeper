using UnityEngine;
using UnityEngine.UI;

namespace Reefkeeper
{
    // A small, resolution-independent soft shape; the object icons remain photographed assets.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class ReefPebbleGraphic : MaskableGraphic
    {
        public Color rim = new Color(0.97f, 0.94f, 0.84f);
        public float rimWidth = 2, seed;

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            var rect = rectTransform.rect;
            const int segments = 64;
            mesh.AddVert(rect.center, color, Vector2.zero);
            for (int i = 0; i <= segments; i++)
            {
                float angle = i * Mathf.PI * 2 / segments;
                float wave = 0.93f + 0.035f * Mathf.Sin(angle * 3 + seed)
                    + 0.025f * Mathf.Cos(angle * 2 - seed);
                var outer = rect.center + new Vector2(Mathf.Cos(angle) * rect.width * .5f,
                    Mathf.Sin(angle) * rect.height * .5f) * wave;
                var inner = Vector2.MoveTowards(outer, rect.center, rimWidth);
                mesh.AddVert(inner, color, Vector2.zero);
                mesh.AddVert(outer, rimWidth > 0 ? rim : color, Vector2.zero);
                if (i == 0) continue;
                int a = 1 + (i - 1) * 2, b = a + 2;
                mesh.AddTriangle(0, a, b);
                mesh.AddTriangle(a, a + 1, b + 1);
                mesh.AddTriangle(a, b + 1, b);
            }
        }

        public void Tint(Color fill, Color edge, float width)
        {
            if (color == fill && rim == edge && Mathf.Approximately(rimWidth, width)) return;
            color = fill; rim = edge; rimWidth = width; SetVerticesDirty();
        }
    }
}
