using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Reefkeeper.Corals
{
    /// <summary>
    /// Draws many copies of one ground-cover clump with GPU instancing, from a stored list of
    /// transforms (placed by the editor scatter tool). For seabeds that are meshes, not Terrains.
    /// Works in edit mode and play mode; skipped when the patch is farther than maxDistance.
    /// </summary>
    [ExecuteAlways]
    public sealed class GroundcoverScatter : MonoBehaviour
    {
        public Mesh mesh;
        public Material material;
        [Tooltip("World-space transforms of every clump.")] public List<Matrix4x4> instances = new List<Matrix4x4>();
        [Tooltip("Beyond this camera distance the patch is not drawn (m).")] public float maxDistance = 45f;
        [SerializeField, HideInInspector] Bounds bounds;

        const int Batch = 1023;
        readonly List<Matrix4x4[]> batches = new List<Matrix4x4[]>();

        public int Count => instances.Count;

        public void SetInstances(List<Matrix4x4> list)
        {
            instances = list;
            RecalculateBounds();
            Rebuild();
        }

        void RecalculateBounds()
        {
            if (instances.Count == 0) { bounds = new Bounds(transform.position, Vector3.one); return; }
            var b = new Bounds(instances[0].GetColumn(3), Vector3.zero);
            foreach (var m in instances) b.Encapsulate(m.GetColumn(3));
            b.Expand(0.6f);                       // clump size + sway
            bounds = b;
        }

        void Rebuild()
        {
            batches.Clear();
            for (int i = 0; i < instances.Count; i += Batch)
                batches.Add(instances.GetRange(i, Mathf.Min(Batch, instances.Count - i)).ToArray());
        }

        void OnEnable()
        {
            if (bounds.size == Vector3.zero) RecalculateBounds();
            Rebuild();
            RenderPipelineManager.beginCameraRendering += Draw;
        }

        void OnDisable() => RenderPipelineManager.beginCameraRendering -= Draw;

        void Draw(ScriptableRenderContext ctx, Camera cam)
        {
            if (!mesh || !material || batches.Count == 0) return;
            if (cam.cameraType == CameraType.Preview || cam.cameraType == CameraType.Reflection) return;
            if (bounds.SqrDistance(cam.transform.position) > maxDistance * maxDistance) return;
            var rp = new RenderParams(material)
            {
                camera = cam,
                worldBounds = bounds,
                shadowCastingMode = ShadowCastingMode.Off,
                receiveShadows = true,
                layer = gameObject.layer,
            };
            foreach (var batch in batches) Graphics.RenderMeshInstanced(rp, mesh, 0, batch);
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.8f, 0.6f, 1f, 0.6f);
            Gizmos.DrawWireCube(bounds.center, bounds.size);
        }
    }
}
