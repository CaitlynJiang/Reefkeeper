using System.IO;
using UnityEditor;
using UnityEngine;

namespace Reefkeeper.UnderWaterTools
{
    /// <summary>
    /// Generates a tileable caustics texture: bright thin webs along the borders of
    /// warped Voronoi cells (distance to 2nd nearest point minus nearest).
    /// </summary>
    public static class UnderwaterCausticsGenerator
    {
        public const string TexturePath = "Assets/Reefkeeper/Textures/UnderWater/UW_Caustics.png";
        const int Size = 512;
        const int Cells = 8;

        [MenuItem("Reefkeeper/UnderWater/Generate caustics texture - 生成焦散贴图")]
        static void Regenerate() => Generate(true);

        public static Texture2D GetOrCreate() => Generate(false);

        static Texture2D Generate(bool force)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
            if (existing && !force) return existing;

            var rng = new System.Random(1234);
            var points = new Vector2[Cells, Cells];
            for (int y = 0; y < Cells; y++)
            for (int x = 0; x < Cells; x++)
                points[x, y] = new Vector2((float)rng.NextDouble(), (float)rng.NextDouble()) * 0.8f + Vector2.one * 0.1f;

            var pixels = new Color32[Size * Size];
            const float tau = Mathf.PI * 2f;
            for (int py = 0; py < Size; py++)
            for (int px = 0; px < Size; px++)
            {
                // Position in cell units. Warp with whole-number frequencies so it still tiles.
                float u = (px + 0.5f) / Size * Cells;
                float v = (py + 0.5f) / Size * Cells;
                float wu = u + 0.32f * Mathf.Sin(tau * 2f * v / Cells) + 0.12f * Mathf.Sin(tau * 5f * (u + v) / Cells);
                float wv = v + 0.32f * Mathf.Sin(tau * 3f * u / Cells) + 0.12f * Mathf.Sin(tau * 4f * (u - v) / Cells);

                int cx = Mathf.FloorToInt(wu), cy = Mathf.FloorToInt(wv);
                float f1 = 9f, f2 = 9f;
                for (int oy = -1; oy <= 1; oy++)
                for (int ox = -1; ox <= 1; ox++)
                {
                    int gx = cx + ox, gy = cy + oy;
                    var p = points[Mod(gx, Cells), Mod(gy, Cells)] + new Vector2(gx, gy);
                    float d = Vector2.Distance(p, new Vector2(wu, wv));
                    if (d < f1) { f2 = f1; f1 = d; }
                    else if (d < f2) f2 = d;
                }

                float edge = 1f - Mathf.SmoothStep(0f, 0.2f, f2 - f1);
                float c = edge * edge;
                byte b = (byte)Mathf.RoundToInt(Mathf.Clamp01(c) * 255f);
                pixels[py * Size + px] = new Color32(b, b, b, 255);
            }

            var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false, true);
            tex.SetPixels32(pixels);
            tex.Apply();
            Directory.CreateDirectory(Path.GetDirectoryName(TexturePath));
            File.WriteAllBytes(TexturePath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);

            AssetDatabase.ImportAsset(TexturePath, ImportAssetOptions.ForceUpdate);
            var importer = (TextureImporter)AssetImporter.GetAtPath(TexturePath);
            importer.sRGBTexture = false;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.mipmapEnabled = true;
            importer.filterMode = FilterMode.Trilinear;
            importer.anisoLevel = 4;
            importer.SaveAndReimport();
            Debug.Log($"[UnderWater] Caustics texture written to {TexturePath}");
            return AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
        }

        static int Mod(int a, int n) => ((a % n) + n) % n;
    }
}
