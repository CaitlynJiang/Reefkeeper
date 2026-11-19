using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Reefkeeper
{
    /// <summary>Temporary ink in card-local space, shared by mouse and XR pointer input.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class DiveCardDoodles : MaskableGraphic
    {
        sealed class Stroke
        {
            public readonly List<Vector2> points = new List<Vector2>();
            public float released = -1, alpha = 1;
        }
        const float HoldSeconds = 3, FadeSeconds = 1.5f, Radius = 1.5f;
        readonly List<Stroke> strokes = new List<Stroke>();
        Stroke active;
        public int StrokeCount => strokes.Count;

        public void Draw(Ray ray, bool pressed)
        {
            if (!pressed || !new Plane(transform.forward, transform.position).Raycast(ray, out float distance))
            { EndStroke(); return; }
            Vector2 point = transform.InverseTransformPoint(ray.GetPoint(distance));
            // Leave an inset at rounded corners, so ink cannot spill beyond the PVC.
            var area = rectTransform.rect;
            if (Mathf.Abs(point.x) > area.width * .5f - 20 || Mathf.Abs(point.y) > area.height * .5f - 20)
            { EndStroke(); return; }
            if (active == null)
            {
                // Keep the combined UI mesh below Unity's 65k vertex limit.
                if (strokes.Count >= 8) strokes.RemoveAt(0);
                active = new Stroke(); strokes.Add(active);
            }
            if (active.points.Count > 0 && Vector2.Distance(active.points[active.points.Count - 1], point) < 1.5f) return;
            if (active.points.Count >= 512) { EndStroke(); return; }
            active.points.Add(point); SetVerticesDirty();
        }

        public void EndStroke()
        {
            if (active == null) return;
            active.released = Time.unscaledTime; active = null;
        }

        public void Clear()
        {
            active = null; strokes.Clear(); SetVerticesDirty();
        }

        void Update()
        {
            bool dirty = false;
            for (int i = strokes.Count - 1; i >= 0; i--)
            {
                var stroke = strokes[i];
                if (stroke.released < 0) continue;
                float alpha = 1 - Mathf.Clamp01((Time.unscaledTime - stroke.released - HoldSeconds) / FadeSeconds);
                if (alpha != stroke.alpha) { stroke.alpha = alpha; dirty = true; }
                if (alpha <= 0) strokes.RemoveAt(i);
            }
            if (dirty) SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            foreach (var stroke in strokes)
            {
                Color32 ink = new Color(.045f, .05f, .055f, stroke.alpha * .95f);
                for (int i = 0; i < stroke.points.Count; i++)
                {
                    var p = stroke.points[i];
                    // Small round caps also make a simple click leave an ink dot.
                    int start = vh.currentVertCount;
                    vh.AddVert(p, ink, Vector2.zero);
                    for (int j = 0; j < 8; j++)
                    {
                        float angle = j * Mathf.PI / 4;
                        vh.AddVert(p + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * Radius, ink, Vector2.zero);
                    }
                    for (int j = 0; j < 8; j++) vh.AddTriangle(start, start + 1 + j, start + 1 + (j + 1) % 8);
                    if (i == 0) continue;
                    var previous = stroke.points[i - 1]; var delta = (p - previous).normalized;
                    var side = new Vector2(-delta.y, delta.x) * Radius;
                    start = vh.currentVertCount;
                    vh.AddVert(previous - side, ink, Vector2.zero); vh.AddVert(previous + side, ink, Vector2.zero);
                    vh.AddVert(p + side, ink, Vector2.zero); vh.AddVert(p - side, ink, Vector2.zero);
                    vh.AddTriangle(start, start + 1, start + 2); vh.AddTriangle(start, start + 2, start + 3);
                }
            }
        }
    }
}
