using System.Collections.Generic;
using UnityEngine;

namespace Reefkeeper.UnderWater
{
    /// <summary>
    /// Builds a set of cone-shaped light shafts as one mesh (one draw call), leaning
    /// along the sun. Shafts start at this object's height and are scattered over
    /// <see cref="area"/>. Rebuilds when settings change or the sun turns.
    /// Use with the "Reefkeeper/UnderWater/Light Shaft" shader.
    /// </summary>
    [ExecuteAlways, RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class UnderwaterLightShafts : MonoBehaviour
    {
        [Tooltip("Sun to align with. Empty = RenderSettings.sun.\n对齐的太阳，留空则用场景主光")]
        public Light sun;
        [Min(1)] public int count = 14;
        [Tooltip("Width (X) and depth (Z) of the scatter area, centred on this object.\n分布范围")]
        public Vector2 area = new Vector2(24, 40);
        [Min(0.5f)] public float length = 24f;
        public Vector2 topRadius = new Vector2(0.4f, 1.3f);
        [Tooltip("Bottom radius = top radius × spread.\n底部相对顶部的扩散")]
        [Range(1f, 4f)] public float spread = 1.8f;
        [Range(0.2f, 1f)] public float minBrightness = 0.4f;
        [Range(6, 24)] public int segments = 10;
        public int seed = 3;

        Mesh mesh;
        Vector3 builtSunDir;
        bool dirty = true;

        void OnEnable() { dirty = true; Rebuild(); }
        void OnValidate() => dirty = true;

        void OnDisable()
        {
            if (mesh) DestroyImmediate(mesh);
            mesh = null;
        }

        void Update()
        {
            if (dirty || Vector3.Angle(SunDirection(), builtSunDir) > 0.5f) Rebuild();
        }

        // Direction light travels (downward into the water).
        Vector3 SunDirection()
        {
            var light = sun ? sun : RenderSettings.sun;
            return light ? light.transform.forward : Vector3.down;
        }

        void Rebuild()
        {
            dirty = false;
            builtSunDir = SunDirection();
            if (!mesh)
            {
                mesh = new Mesh { name = "Light Shafts", hideFlags = HideFlags.DontSave };
                GetComponent<MeshFilter>().sharedMesh = mesh;
            }

            var verts = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var colors = new List<Color>();
            var tris = new List<int>();

            var dir = transform.InverseTransformDirection(builtSunDir).normalized;
            var side = Vector3.Cross(dir, Mathf.Abs(dir.y) > 0.99f ? Vector3.forward : Vector3.up).normalized;
            var up = Vector3.Cross(side, dir);
            var rng = new System.Random(seed);
            float Rand() => (float)rng.NextDouble();

            for (int s = 0; s < count; s++)
            {
                var top = new Vector3((Rand() - 0.5f) * area.x, 0, (Rand() - 0.5f) * area.y);
                float r0 = Mathf.Lerp(topRadius.x, topRadius.y, Rand());
                float r1 = r0 * spread;
                var bottom = top + dir * length * Mathf.Lerp(0.75f, 1.1f, Rand());
                var tint = new Color(Rand(), Mathf.Lerp(minBrightness, 1f, Rand()), 0, 1);
                int start = verts.Count;

                for (int i = 0; i <= segments; i++)
                {
                    float u = i / (float)segments;
                    float a = u * Mathf.PI * 2f;
                    var radial = side * Mathf.Cos(a) + up * Mathf.Sin(a);
                    verts.Add(top + radial * r0); normals.Add(radial); uvs.Add(new Vector2(u, 0)); colors.Add(tint);
                    verts.Add(bottom + radial * r1); normals.Add(radial); uvs.Add(new Vector2(u, 1)); colors.Add(tint);
                }
                for (int i = 0; i < segments; i++)
                {
                    int a = start + i * 2;
                    tris.Add(a); tris.Add(a + 1); tris.Add(a + 2);
                    tris.Add(a + 2); tris.Add(a + 1); tris.Add(a + 3);
                }
            }

            mesh.Clear();
            mesh.SetVertices(verts);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetColors(colors);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
        }
    }
}
