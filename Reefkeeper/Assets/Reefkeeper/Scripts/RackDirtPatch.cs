using UnityEngine;

namespace Reefkeeper
{
    public enum ReefSurfaceKind { Algae, FireCoral }

    public sealed class RackDirtPatch : MonoBehaviour
    {
        public ReefSurfaceKind surfaceKind;
        public float requiredTravel = 0.38f;
        public Renderer dirtVisual;
        public Transform[] growth = new Transform[0];
        public float Cleaned { get; private set; }
        public bool Complete => Cleaned >= 0.999f;
        public CleaningToolKind RequiredTool => surfaceKind == ReefSurfaceKind.FireCoral ? CleaningToolKind.Scraper : CleaningToolKind.Brush;
        public string SurfaceName => surfaceKind == ReefSurfaceKind.FireCoral ? "Fire coral" : "Algae";
        public string CleaningHint => surfaceKind == ReefSurfaceKind.FireCoral ? "Use the scraper on fire coral" : "Use the brush on algae";
        public bool Accepts(CleaningToolKind tool) => tool == RequiredTool;
        MaterialPropertyBlock properties;
        Vector3 originalScale;
        Color dirtColor;
        Vector3[] growthScale;
        Vector3[] growthOnCoating;
        Bounds coatingBounds;
        Vector2 edgeFeather;

        void Awake()
        {
            originalScale = dirtVisual.transform.localScale;
            dirtColor = dirtVisual.sharedMaterial.GetColor("_BaseColor");
            properties = new MaterialPropertyBlock();
            growthScale = new Vector3[growth.Length];
            growthOnCoating = new Vector3[growth.Length];
            coatingBounds = dirtVisual.localBounds;
            var worldScale = dirtVisual.transform.lossyScale;
            edgeFeather = new Vector2(0.025f / Mathf.Max(0.001f, Mathf.Abs(worldScale.x)), 0.025f / Mathf.Max(0.001f, Mathf.Abs(worldScale.z)));
            for (int i = 0; i < growth.Length; i++)
            {
                growthScale[i] = growth[i].localScale;
                growthOnCoating[i] = dirtVisual.transform.InverseTransformPoint(growth[i].position);
            }
        }
        public void ApplyStroke(float tangentDistance) => ApplyStroke(tangentDistance, CleaningToolKind.Brush);
        public void ApplyStroke(float tangentDistance, CleaningToolKind tool)
        {
            if (!Accepts(tool) || float.IsNaN(tangentDistance) || float.IsInfinity(tangentDistance) || tangentDistance <= 0 || Complete) return;
            Cleaned = Mathf.Clamp01(Cleaned + tangentDistance / Mathf.Max(0.05f, requiredTravel));
            if (Complete) Cleaned = 1;
            Refresh();
        }
        public void ResetDirt() { Cleaned = 0; Refresh(); }
        void Refresh()
        {
            if (!dirtVisual) return;
            dirtVisual.enabled = !Complete;
            var scale = originalScale;
            float width = Mathf.Lerp(1, 0.08f, Cleaned), depth = Mathf.Lerp(1, 0.25f, Cleaned);
            scale.x *= width;
            scale.z *= depth;
            dirtVisual.transform.localScale = scale;
            properties ??= new MaterialPropertyBlock();
            properties.SetColor("_BaseColor", Color.Lerp(dirtColor, dirtColor * 1.15f, Cleaned));
            dirtVisual.SetPropertyBlock(properties);
            for (int i = 0; i < growth.Length; i++)
            {
                // The roots stay on the rack. Remove tufts as the shrinking coating edge reaches them,
                // using their position rather than their randomized creation order.
                var root = growthOnCoating[i];
                float edgeX = coatingBounds.extents.x * width - Mathf.Abs(root.x - coatingBounds.center.x * width);
                float edgeZ = coatingBounds.extents.z * depth - Mathf.Abs(root.z - coatingBounds.center.z * depth);
                float remaining = Complete ? 0 : Cleaned == 0 ? 1 : Mathf.Clamp01(Mathf.Min(edgeX / edgeFeather.x, edgeZ / edgeFeather.y));
                growth[i].gameObject.SetActive(remaining > 0.01f);
                growth[i].localScale = growthScale[i] * remaining;
            }
        }
    }
}
