using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace Reefkeeper
{
    public enum CleaningToolKind { Brush, Scraper }

    [RequireComponent(typeof(XRGrabInteractable))]
    public sealed class ReefBrush : MonoBehaviour
    {
        public Transform bristles;
        public CleaningToolKind toolKind;
        public float contactRadius = 0.075f;
        public ReefScrapeDebris scrapeDebris;
        public string ToolName => toolKind == CleaningToolKind.Scraper ? "scraper" : "brush";
        public RackDirtPatch ContactPatch { get; private set; }
        public bool InContact { get; private set; }
        public bool Scrubbing { get; private set; }
        public bool Held => grab && grab.isSelected;
        XRGrabInteractable grab;
        Vector3 lastTip;
        bool wasHeld;
        ReefBrushPickup assisted;
        readonly Collider[] contacts = new Collider[32];
        readonly HashSet<RackDirtPatch> visited = new HashSet<RackDirtPatch>();

        void Awake() { grab = GetComponent<XRGrabInteractable>(); assisted = GetComponent<ReefBrushInteractable>()?.owner; }
        void LateUpdate()
        {
            Vector3 tip = bristles.position;
            InContact = false;
            Scrubbing = false;
            ContactPatch = null;
            if (!ReefToolMenu.BlocksWorldInput && Held && wasHeld && (!assisted || !assisted.AssistedMotion || assisted.IsScrubbing)) Sweep(lastTip, tip);
            lastTip = tip;
            wasHeld = Held;
        }
        void Sweep(Vector3 from, Vector3 to)
        {
            Vector3 delta = to - from;
            // Ignore pickup snapping and teleports; touching without motion never cleans.
            if (delta.magnitude > 0.25f) return;
            int steps = Mathf.Clamp(Mathf.CeilToInt(delta.magnitude / (contactRadius * 0.5f)), 1, 10);
            visited.Clear();
            for (int step = 0; step <= steps; step++)
            {
                Vector3 point = Vector3.Lerp(from, to, step / (float)steps);
                int count = Physics.OverlapSphereNonAlloc(point, contactRadius, contacts, ~0, QueryTriggerInteraction.Collide);
                for (int i = 0; i < count; i++)
                {
                    var patch = contacts[i].GetComponent<RackDirtPatch>();
                    if (!patch || patch.Complete || !visited.Add(patch)) continue;
                    Vector3 normal = patch.transform.up;
                    // Bristles face the surface, and their center stays on its exposed side.
                    if (Vector3.Dot(bristles.up, normal) < 0.55f || Vector3.Dot(point - patch.transform.position, normal) < -0.015f) continue;
                    ContactPatch = patch;
                    if (!patch.Accepts(toolKind)) continue;
                    InContact = true;
                    Vector3 tangent = Vector3.ProjectOnPlane(delta, normal);
                    // The scraper cuts forwards/backwards across its blade; sideways shuffling does not cut.
                    float travel = toolKind == CleaningToolKind.Scraper
                        ? Mathf.Abs(Vector3.Dot(tangent, Vector3.ProjectOnPlane(bristles.forward, normal).normalized))
                        : tangent.magnitude;
                    if (travel > 0.0005f)
                    {
                        float before = patch.Cleaned;
                        patch.ApplyStroke(travel, toolKind);
                        float removed = patch.Cleaned - before;
                        if (removed > 0 && toolKind == CleaningToolKind.Scraper && scrapeDebris)
                            scrapeDebris.EmitScrape(patch, point, tangent, removed * Mathf.Max(0.05f, patch.requiredTravel));
                        Scrubbing = !patch.Complete;
                    }
                }
            }
        }
        void OnDisable() { wasHeld = false; InContact = false; Scrubbing = false; ContactPatch = null; }
    }
}
