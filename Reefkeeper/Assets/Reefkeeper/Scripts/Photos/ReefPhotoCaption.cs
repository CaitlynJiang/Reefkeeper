using UnityEngine;

namespace Reefkeeper.Photos
{
    // Default TextMesh font shaders can draw from both sides and over the paper.
    // Each caption uses a depth-tested, back-face-culled material, retaining its font atlas.
    public sealed class ReefPhotoCaption : MonoBehaviour
    {
        TextMesh label;
        Material material;
        public void Bind(TextMesh text)
        {
            label = text;
            material = new Material(Resources.Load<Shader>("Photos/PhotoCaption"));
            material.mainTexture = label.font.material.mainTexture;
            label.GetComponent<MeshRenderer>().sharedMaterial = material;
            Font.textureRebuilt += RefreshAtlas;
        }
        void RefreshAtlas(Font font)
        { if (label && label.font == font && material) material.mainTexture = font.material.mainTexture; }
        void OnDestroy()
        { Font.textureRebuilt -= RefreshAtlas; if (material) Destroy(material); }
    }
}
