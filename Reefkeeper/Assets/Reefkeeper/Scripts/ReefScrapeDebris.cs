using UnityEngine;

namespace Reefkeeper
{
    /// <summary>One bounded, world-space emitter shared by the scraper's accepted contact strokes.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(ParticleSystem))]
    public sealed class ReefScrapeDebris : MonoBehaviour
    {
        [Min(0)] public float particlesPerMetre = 110;
        public ParticleSystem Particles { get; private set; }
        public int EmittedCount { get; private set; }
        public int AliveCount => Particles ? Particles.particleCount : 0;
        float remainder;
        float lastStroke = float.NegativeInfinity;
        RackDirtPatch lastPatch;

        void Awake() { Particles = GetComponent<ParticleSystem>(); Particles.Play(); }

        public void EmitScrape(RackDirtPatch patch, Vector3 contact, Vector3 stroke, float removedTravel)
        {
            if (!isActiveAndEnabled || !Particles || !patch || patch.surfaceKind != ReefSurfaceKind.FireCoral
                || removedTravel <= 0 || float.IsNaN(removedTravel) || float.IsInfinity(removedTravel)) return;
            Vector3 normal = patch.transform.up;
            Vector3 direction = Vector3.ProjectOnPlane(stroke, normal).normalized;
            if (direction.sqrMagnitude < 0.01f) return;
            if (lastPatch != patch || Time.time - lastStroke > 0.15f) remainder = 0;
            lastPatch = patch; lastStroke = Time.time;
            remainder += removedTravel * particlesPerMetre;
            int count = Mathf.Min(Mathf.FloorToInt(remainder), 8);
            remainder = Mathf.Repeat(remainder, 1);
            Vector3 sideways = Vector3.Cross(normal, direction);
            for (int i = 0; i < count; i++)
            {
                float size = Random.Range(0.008f, 0.017f);
                var particle = new ParticleSystem.EmitParams
                {
                    position = contact + normal * 0.004f + sideways * Random.Range(-0.009f, 0.009f)
                        + direction * Random.Range(-0.007f, 0.007f),
                    velocity = normal * Random.Range(0.09f, 0.17f) + direction * Random.Range(0.13f, 0.29f)
                        + sideways * Random.Range(-0.09f, 0.09f) + Vector3.up * 0.025f,
                    startSize3D = new Vector3(size, size * Random.Range(0.45f, 0.8f), size * Random.Range(0.8f, 1.5f)),
                    rotation3D = new Vector3(Random.Range(0, 360f), Random.Range(0, 360f), Random.Range(0, 360f)),
                    startLifetime = Random.Range(0.65f, 1.05f),
                    startColor = Color.Lerp(new Color(1, 0.73f, 0.34f), new Color(1, 0.95f, 0.76f), Random.value)
                };
                Particles.Emit(particle, 1);
                EmittedCount++;
            }
        }

        public void Clear()
        {
            remainder = 0; lastPatch = null; lastStroke = float.NegativeInfinity;
            if (Particles) Particles.Clear(true);
        }
        void OnDisable() => Clear();
    }
}
