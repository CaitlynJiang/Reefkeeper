using System.Collections.Generic;
using UnityEngine;

namespace Reefkeeper.Corals
{
    /// <summary>
    /// Corals planted close together react to each other. When the footprints of two upright corals
    /// come within <see cref="reach"/> of each other they become neighbours, and over
    /// <see cref="fuseSeconds"/> each one:
    /// - leans away from the other (a springy lean, so a neighbour's growth pop nudges it),
    ///   and whatever reaches into the other's footprint is pushed back out;
    /// - takes on some of the other's colour on the side facing it;
    /// - grows faster (see <see cref="RateFor"/>, fed into <see cref="CoralNurture.growthRate"/>).
    /// The look is done in the vertex/fragment shader "Reefkeeper/UnderWater/Lit Coral"; this sends
    /// each coral its values through a MaterialPropertyBlock. Links are rebuilt only when a coral is
    /// added, removed or changes stage.
    /// </summary>
    [DefaultExecutionOrder(50)]
    public sealed class CoralColony : MonoBehaviour
    {
        const int MaxNeighbours = 4;   // matches the shader's arrays

        [Header("Neighbours 邻居判定")]
        [Tooltip("Largest gap between two corals' footprints that still makes them neighbours (m).")]
        public float reach = 0.15f;
        [Tooltip("Corals whose bases differ more than this in height never link (m).")]
        public float maxHeightDifference = 0.15f;
        [Tooltip("Only corals under this object take part (empty = the whole scene).")]
        public Transform scope;

        [Header("Growth dependency 生长依赖")]
        [Tooltip("Extra growth speed for each neighbour (0.35 = +35%).")]
        public float bonusPerNeighbour = 0.35f;
        [Tooltip("Growth speed never goes above this multiplier.")]
        public float maxRate = 2f;

        [Header("Look 表现")]
        [Tooltip("How far a coral's top leans away from a close neighbour (m).")]
        public float leanAmount = 0.07f;
        [Tooltip("Seconds for a new neighbour's lean and colour to fully come in.")]
        public float fuseSeconds = 2.5f;
        [Tooltip("Lean spring: higher = snappier.")] public float stiffness = 45f;
        [Tooltip("Lean spring: lower = more wobble.")] public float damping = 5f;

        sealed class Link { public CoralGrowth a, b; public float weight; public bool alive; }

        sealed class Member
        {
            public CoralGrowth coral;
            public Transform stage;
            public Renderer[] renderers;
            public Vector3 lean, velocity;
            public bool touched;
        }

        readonly Dictionary<long, Link> links = new Dictionary<long, Link>();
        readonly Dictionary<CoralGrowth, Member> members = new Dictionary<CoralGrowth, Member>();
        readonly Dictionary<CoralGrowth, float> rates = new Dictionary<CoralGrowth, float>();
        readonly List<CoralGrowth> candidates = new List<CoralGrowth>();
        readonly List<long> scratchKeys = new List<long>();
        readonly List<CoralGrowth> scratchCorals = new List<CoralGrowth>();
        readonly Vector4[] neighbourData = new Vector4[MaxNeighbours];
        readonly Vector4[] colourData = new Vector4[MaxNeighbours];
        MaterialPropertyBlock block;
        bool dirty = true;

        static readonly int BaseId = Shader.PropertyToID("_ColonyBase");
        static readonly int LeanId = Shader.PropertyToID("_ColonyLean");
        static readonly int NeighbourId = Shader.PropertyToID("_ColonyNeighbour");
        static readonly int ColourId = Shader.PropertyToID("_ColonyColour");

        void OnEnable() { CoralGrowth.AnyChanged += MarkDirty; dirty = true; }

        void OnDisable()
        {
            CoralGrowth.AnyChanged -= MarkDirty;
            foreach (var m in members.Values) Clear(m);
            members.Clear();
            links.Clear();
        }

        void MarkDirty(CoralGrowth _) => dirty = true;

        /// <summary>Growth speed multiplier for a coral from its neighbours.</summary>
        public float RateFor(CoralGrowth coral) => coral && rates.TryGetValue(coral, out var r) ? r : 1f;

        /// <summary>How many neighbours this coral has.</summary>
        public int NeighbourCount(CoralGrowth coral)
        {
            int n = 0;
            foreach (var l in links.Values) if (l.alive && (l.a == coral || l.b == coral)) n++;
            return n;
        }

        void LateUpdate()
        {
            if (dirty) { dirty = false; RebuildLinks(); }
            float dt = Mathf.Min(Time.deltaTime, 1f / 30f);
            UpdateLinks(dt);
            UpdateMembers(dt);
            UpdateRates();
        }

        // ------------------------------------------------------------------ who is next to whom

        void RebuildLinks()
        {
            candidates.Clear();
            foreach (var c in CoralGrowth.Active)
                if (c && c.Current && c.Current.gameObject.activeInHierarchy && (!scope || c.transform.IsChildOf(scope)) && c.transform.up.y > 0.8f)
                    candidates.Add(c);

            foreach (var l in links.Values) l.alive = false;
            for (int i = 0; i < candidates.Count; i++)
            for (int j = i + 1; j < candidates.Count; j++)
            {
                var a = candidates[i]; var b = candidates[j];
                var fa = Footprint.Of(a); var fb = Footprint.Of(b);
                if (Mathf.Abs(fa.center.y - fb.center.y) > maxHeightDifference) continue;
                var d = fb.center - fa.center; d.y = 0f;
                if (d.magnitude > fa.radius + fb.radius + reach) continue;
                long key = Key(a, b);
                if (!links.TryGetValue(key, out var link)) links[key] = link = new Link { a = a, b = b };
                link.alive = true;
            }
        }

        // New links fade in, broken ones fade out and are dropped.
        void UpdateLinks(float dt)
        {
            scratchKeys.Clear();
            float step = dt / Mathf.Max(0.05f, fuseSeconds);
            foreach (var kv in links)
            {
                var l = kv.Value;
                if (!l.a || !l.b) { scratchKeys.Add(kv.Key); continue; }
                l.weight = Mathf.MoveTowards(l.weight, l.alive ? 1f : 0f, l.alive ? step : step * 1.5f);
                if (!l.alive && l.weight <= 0f) scratchKeys.Add(kv.Key);
            }
            foreach (var k in scratchKeys) links.Remove(k);
        }

        void UpdateMembers(float dt)
        {
            foreach (var m in members.Values) m.touched = false;
            foreach (var l in links.Values)
            {
                Touch(l.a); Touch(l.b);
            }

            scratchCorals.Clear();
            foreach (var kv in members)
            {
                var m = kv.Value;
                if (!m.coral) { scratchCorals.Add(kv.Key); continue; }
                if (m.stage != m.coral.Current) { Clear(m); m.stage = m.coral.Current; m.renderers = m.stage ? m.stage.GetComponentsInChildren<Renderer>(true) : new Renderer[0]; }

                var foot = Footprint.Of(m.coral);
                int count = 0;
                var target = Vector3.zero;
                foreach (var l in links.Values)
                {
                    if (l.a != m.coral && l.b != m.coral) continue;
                    var other = l.a == m.coral ? l.b : l.a;
                    var fo = Footprint.Of(other);
                    float w = Ease(l.weight);
                    var away = foot.center - fo.center; away.y = 0f;
                    float dist = away.magnitude;
                    // any neighbour makes it lean a bit; closer ones (and bigger ones) push harder
                    float closeness = 0.35f + 0.65f * Mathf.Clamp01((foot.radius + fo.radius + reach - dist) / (reach + 0.5f * (foot.radius + fo.radius)));
                    if (dist > 1e-4f) target += away / dist * leanAmount * closeness * w * Mathf.Lerp(0.6f, 1.2f, fo.radius / Mathf.Max(0.01f, foot.radius + fo.radius) * 2f);
                    if (count < MaxNeighbours)
                    {
                        neighbourData[count] = new Vector4(fo.center.x, fo.center.y, fo.center.z, fo.radius);
                        var c = CoralTint.Of(other);
                        if (QualitySettings.activeColorSpace == ColorSpace.Linear) c = c.linear;
                        colourData[count] = new Vector4(c.r, c.g, c.b, w);
                        count++;
                    }
                }
                target = Vector3.ClampMagnitude(target, leanAmount * 1.5f);

                // springy lean: a neighbour's growth pop (or a new link) makes it sway into place
                var accel = stiffness * (target - m.lean) - damping * m.velocity;
                m.velocity += accel * dt;
                m.lean += m.velocity * dt;

                bool resting = count == 0 && m.lean.sqrMagnitude < 1e-8f && m.velocity.sqrMagnitude < 1e-8f;
                if (resting) { scratchCorals.Add(kv.Key); continue; }
                for (int i = count; i < MaxNeighbours; i++) { neighbourData[i] = Vector4.zero; colourData[i] = Vector4.zero; }

                block ??= new MaterialPropertyBlock();
                block.Clear();
                block.SetVector(BaseId, new Vector4(foot.center.x, foot.center.y, foot.center.z, foot.height));
                block.SetVector(LeanId, new Vector4(m.lean.x, 0f, m.lean.z, 0f));
                block.SetVectorArray(NeighbourId, neighbourData);
                block.SetVectorArray(ColourId, colourData);
                foreach (var r in m.renderers) if (r) r.SetPropertyBlock(block);
            }
            foreach (var c in scratchCorals) { if (members.TryGetValue(c, out var m)) Clear(m); members.Remove(c); }
        }

        void Touch(CoralGrowth c)
        {
            if (!c) return;
            if (!members.TryGetValue(c, out var m)) members[c] = m = new Member { coral = c };
            m.touched = true;
        }

        static void Clear(Member m)
        {
            if (m.renderers == null) return;
            foreach (var r in m.renderers) if (r) r.SetPropertyBlock(null);
        }

        static float Ease(float x) => x * x * (3f - 2f * x);

        void UpdateRates()
        {
            rates.Clear();
            foreach (var l in links.Values)
            {
                float bonus = bonusPerNeighbour * Ease(l.weight);
                Add(l.a, bonus); Add(l.b, bonus);
            }
            foreach (var c in CoralGrowth.Active)
            {
                if (!c) continue;
                var nurture = c.GetComponent<CoralNurture>();
                if (nurture) nurture.growthRate = RateFor(c);
            }
        }

        void Add(CoralGrowth c, float bonus)
        {
            if (!c) return;
            rates.TryGetValue(c, out var r);
            rates[c] = Mathf.Min(maxRate, (r == 0f ? 1f : r) + bonus);
        }

        static long Key(Object a, Object b)
        {
            long x = a.GetInstanceID(), y = b.GetInstanceID();
            if (x > y) (x, y) = (y, x);
            return (x << 32) ^ (y & 0xffffffffL);
        }

        void OnDrawGizmosSelected()
        {
            foreach (var c in CoralGrowth.Active)
            {
                if (!c || !c.Current) continue;
                var f = Footprint.Of(c);
                Gizmos.color = new Color(1f, 0.8f, 0.3f, 0.6f);
                DrawCircle(f.center, f.radius);
                Gizmos.color = new Color(1f, 0.8f, 0.3f, 0.2f);
                DrawCircle(f.center, f.radius + reach * 0.5f);
            }
            foreach (var l in links.Values)
            {
                if (!l.a || !l.b) continue;
                Gizmos.color = Color.Lerp(new Color(1f, 1f, 1f, 0.2f), new Color(0.4f, 1f, 0.7f), l.weight);
                Gizmos.DrawLine(l.a.transform.position, l.b.transform.position);
            }
        }

        static void DrawCircle(Vector3 c, float r)
        {
            var prev = c + new Vector3(r, 0, 0);
            for (int i = 1; i <= 24; i++)
            {
                float t = i / 24f * Mathf.PI * 2f;
                var p = c + new Vector3(Mathf.Cos(t) * r, 0, Mathf.Sin(t) * r);
                Gizmos.DrawLine(prev, p); prev = p;
            }
        }
    }

    /// <summary>Where a coral meets the ground: the centre of its current stage, its rough radius and height.</summary>
    public struct Footprint
    {
        public Vector3 center;
        public float radius, height;

        static readonly Dictionary<Transform, Bounds> cache = new Dictionary<Transform, Bounds>();

        public static Footprint Of(CoralGrowth coral)
        {
            var stage = coral.Current;
            if (!cache.TryGetValue(stage, out var b)) cache[stage] = b = RestBounds(stage);
            var root = coral.transform;
            var local = stage.localPosition + new Vector3(b.center.x, 0f, b.center.z);
            float scale = root.lossyScale.x;
            return new Footprint
            {
                center = root.TransformPoint(local),
                // Mesh bounds are boxes; the coral itself is rounder, so trim a little.
                radius = Mathf.Max(b.extents.x, b.extents.z) * 0.8f * scale,
                height = Mathf.Max(0.05f, b.max.y * scale),
            };
        }

        // Bounds of a stage's meshes in the stage's own space, ignoring its pop and sway.
        static Bounds RestBounds(Transform stage)
        {
            var b = new Bounds(); bool any = false;
            var toStage = stage.worldToLocalMatrix;
            foreach (var mf in stage.GetComponentsInChildren<MeshFilter>(true))
            {
                if (!mf.sharedMesh) continue;
                var m = toStage * mf.transform.localToWorldMatrix;
                var mb = mf.sharedMesh.bounds;
                for (int i = 0; i < 8; i++)
                {
                    var corner = mb.center + Vector3.Scale(mb.extents, new Vector3((i & 1) * 2 - 1, ((i >> 1) & 1) * 2 - 1, ((i >> 2) & 1) * 2 - 1));
                    var p = m.MultiplyPoint3x4(corner);
                    if (!any) { b = new Bounds(p, Vector3.zero); any = true; } else b.Encapsulate(p);
                }
            }
            return b;
        }
    }

    /// <summary>A coral's overall colour (its material colour times its texture's average), cached per material.</summary>
    public static class CoralTint
    {
        static readonly Dictionary<Material, Color> cache = new Dictionary<Material, Color>();

        /// <summary>Colour in gamma space.</summary>
        public static Color Of(CoralGrowth coral)
        {
            var r = coral.Current ? coral.Current.GetComponentInChildren<Renderer>(true) : null;
            var m = r ? r.sharedMaterial : null;
            if (!m) return new Color(0.8f, 0.6f, 0.5f);
            if (!cache.TryGetValue(m, out var c)) cache[m] = c = Average(m);
            return c;
        }

        static Color Average(Material m)
        {
            var tint = m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor") : Color.white;
            var tex = m.HasProperty("_BaseMap") ? m.GetTexture("_BaseMap") : m.mainTexture;
            if (!tex) return tint;

            // Shrink the texture on the GPU and average the result; works on unreadable textures too.
            const int size = 8;
            var rt = RenderTexture.GetTemporary(size, size, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            Graphics.Blit(tex, rt);
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var read = new Texture2D(size, size, TextureFormat.RGBA32, false);
            read.ReadPixels(new Rect(0, 0, size, size), 0, 0);
            read.Apply();
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);

            var sum = Color.clear;
            foreach (var p in read.GetPixels()) sum += p;
            Object.Destroy(read);
            var avg = sum / (size * size);
            // Averages come out muddy; push the saturation back up a little.
            Color.RGBToHSV(avg * tint, out float h, out float s, out float v);
            return Color.HSVToRGB(h, Mathf.Clamp01(s * 1.25f), Mathf.Clamp01(v * 1.05f));
        }
    }
}
