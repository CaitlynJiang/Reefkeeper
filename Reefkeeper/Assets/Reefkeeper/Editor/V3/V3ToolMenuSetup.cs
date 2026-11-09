using UnityEditor;
namespace Reefkeeper.Editor
{
    public static class V3ToolMenuSetup
    {
        [MenuItem("Reefkeeper/V3/Import tool screenshot icons")]
        public static void ImportIcons()
        {
            foreach (var name in new[] { "Brush", "Scraper" })
            {
                var path = "Assets/Reefkeeper/Resources/ToolMenu/" + name + ".png";
                AssetDatabase.ImportAsset(path);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = 100;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }
        }
    }
}
