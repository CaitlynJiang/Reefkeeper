using System.Collections.Generic;
using Reefkeeper.Corals;
using UnityEngine;

namespace Reefkeeper.Photos
{
    /// <summary>
    /// Finds photographable subjects of species not yet in the album: ambient fish schools, the coral
    /// crab, and corals that have finished growing (CoralNurture.FullyGrown). Each subject has a
    /// detection sphere of <see cref="ZoneDiameter"/> around it; the eye must be inside it. Fish of a
    /// school count from <see cref="SchoolReach"/>, since they only pass by.
    /// Distance checks only, so nothing new can block raycasts, pickups or surface holds.
    /// </summary>
    public static class ReefPhotoTargets
    {
        /// <summary>Diameter (m) of the sphere around a subject in which it can be photographed.</summary>
        public const float ZoneDiameter = 3f;
        /// <summary>How close (m) one fish of a school must come to the eye. Headset test: per-fish 1.5 m
        /// almost never fired, the whole swimming loop was far too wide.</summary>
        public const float SchoolReach = 2.5f;

        public struct Subject
        {
            public string species;
            public Vector3 point;
            /// <summary>Distance from the eye to the subject's surface (m).</summary>
            public float distance;
            /// <summary>Angle between the view direction and the subject (degrees).</summary>
            public float angle;
            public Transform transform;
        }

        static readonly List<ReefFishSchool> schools = new List<ReefFishSchool>();
        static readonly List<CoralCrab> crabs = new List<CoralCrab>();
        static readonly List<LuckyCat> cats = new List<LuckyCat>();
        static readonly Dictionary<Transform, float> radii = new Dictionary<Transform, float>();
        static readonly Dictionary<Transform, Renderer> bodies = new Dictionary<Transform, Renderer>();
        static float nextScan = -1;

        /// <summary>The best new-species subject whose zone contains the eye: in view first, then nearest.</summary>
        public static bool TryFind(Vector3 eye, Vector3 forward, out Subject best)
        {
            Scan();
            Subject chosen = default;
            bool found = false;
            float bestScore = float.MaxValue;
            void Consider(string species, Transform t, Vector3 point, float reach = ZoneDiameter * .5f)
            {
                if (!t || !ReefSpecies.Known(species) || ReefPhotoAlbum.Has(species) || ReefPrintedPhoto.HasPending(species)) return;
                float distance = Mathf.Max(0, Vector3.Distance(eye, point) - Radius(t));
                if (distance > reach) return;
                float angle = Vector3.Angle(forward, point - eye);
                // In front of the camera matters more than a few centimetres of distance.
                float score = distance + angle * .02f;
                if (score >= bestScore) return;
                bestScore = score; found = true;
                chosen = new Subject { species = species, point = point, distance = distance, angle = angle, transform = t };
            }
            foreach (var school in schools)
            {
                if (!school || !school.isActiveAndEnabled || school.fish == null || school.fish.Length == 0 || !school.fish[0]) continue;
                // Fish keep swimming past, so a school gets a slightly wider reach than a coral:
                // any of its fish within SchoolReach of the eye (the best-placed one in view wins).
                foreach (var fish in school.fish)
                    if (fish && fish.gameObject.activeInHierarchy)
                        Consider(ReefSpecies.FromObjectName(fish.name), fish, Center(fish), SchoolReach);
            }
            foreach (var crab in crabs)
                if (crab && crab.isActiveAndEnabled) Consider("CoralCrab", crab.transform, Center(crab.transform));
            foreach (var cat in cats)
                if (cat && cat.isActiveAndEnabled) Consider("LuckyCat", cat.transform, Center(cat.transform));
            foreach (var coral in CoralGrowth.Active)
            {
                if (!coral || !coral.TryGetComponent<CoralNurture>(out var nurture) || !nurture.FullyGrown) continue;
                Consider(nurture.species, coral.transform, Center(coral.Current ? coral.Current : coral.transform));
            }
            best = chosen;
            return found;
        }

        static void Scan()
        {
            if (Time.unscaledTime < nextScan) return;
            nextScan = Time.unscaledTime + 1f;
            schools.Clear(); crabs.Clear(); cats.Clear();
            cats.AddRange(Object.FindObjectsByType<LuckyCat>(FindObjectsSortMode.None));
            schools.AddRange(Object.FindObjectsByType<ReefFishSchool>(FindObjectsSortMode.None));
            crabs.AddRange(Object.FindObjectsByType<CoralCrab>(FindObjectsSortMode.None));
            if (radii.Count > 256) { radii.Clear(); bodies.Clear(); }
        }

        static Vector3 Center(Transform t)
        {
            if (!bodies.TryGetValue(t, out var r) || !r) bodies[t] = r = t.GetComponentInChildren<Renderer>();
            return r ? r.bounds.center : t.position;
        }

        /// <summary>Rough size of the subject, so the big manta ray counts from its wingtips.</summary>
        static float Radius(Transform t)
        {
            if (radii.TryGetValue(t, out float r)) return r;
            var renderers = t.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) r = 0;
            else
            {
                var b = renderers[0].bounds;
                foreach (var x in renderers) b.Encapsulate(x.bounds);
                r = Mathf.Min(b.extents.magnitude * .7f, 3f);
            }
            radii[t] = r;
            return r;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { schools.Clear(); crabs.Clear(); cats.Clear(); radii.Clear(); bodies.Clear(); nextScan = -1; }
    }
}
