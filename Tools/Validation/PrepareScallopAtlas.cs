using System;
using System.IO;
using UnityEngine;
using UnityEditor;

// Run through the Editor script runner. Keeps all original 512px renders intact.
public static class PrepareScallopAtlas
{
 public static string Main()
 {
  if(Application.isPlaying)throw new InvalidOperationException("Exit Play mode before preparing assets.");
  string source=Path.GetFullPath("../ArtSource/Currency/Scallop/AnglePreviews");
  string[] names={"01_front","02_right_15","03_right_30","04_left_25","05_tilt_25","06_side_45"};
  const int tile=256;
  var atlas=new Texture2D(tile*3,tile*2,TextureFormat.RGBA32,false);
  try
  {
   for(int i=0;i<names.Length;i++)
   {
    var image=new Texture2D(2,2,TextureFormat.RGBA32,false);
    try
    {
     if(!ImageConversion.LoadImage(image,File.ReadAllBytes(Path.Combine(source,names[i]+".png"))))
      throw new InvalidOperationException("Unable to read "+names[i]);
     var pixels=new Color[tile*tile];
     for(int y=0;y<tile;y++)for(int x=0;x<tile;x++)
      pixels[y*tile+x]=image.GetPixelBilinear((x+.5f)/tile,(y+.5f)/tile);
     atlas.SetPixels(i%3*tile,(1-i/3)*tile,tile,tile,pixels);
    }
    finally {UnityEngine.Object.DestroyImmediate(image);}
   }
   atlas.Apply();
   const string path="Assets/Reefkeeper/Resources/ScallopReward/Scallop.png";
   File.WriteAllBytes(path,atlas.EncodeToPNG());
   AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceUpdate);
   var importer=(TextureImporter)AssetImporter.GetAtPath(path);
   importer.textureType=TextureImporterType.Default;importer.spriteImportMode=SpriteImportMode.None;
   importer.npotScale=TextureImporterNPOTScale.None;importer.maxTextureSize=1024;
   importer.mipmapEnabled=false;importer.isReadable=false;importer.alphaIsTransparency=true;
   importer.wrapMode=TextureWrapMode.Clamp;importer.filterMode=FilterMode.Bilinear;
   importer.textureCompression=TextureImporterCompression.Uncompressed;
   importer.SetPlatformTextureSettings(new TextureImporterPlatformSettings{
    name="Android",overridden=true,maxTextureSize=1024,format=TextureImporterFormat.ASTC_6x6,
    textureCompression=TextureImporterCompression.Compressed,compressionQuality=50});
   importer.SaveAndReimport();
   AssetDatabase.Refresh();
   return "Prepared shared 768x512 atlas: six 256px views, upper-right counter view at index 2; Android ASTC 6x6, no mipmaps, no readable CPU copy.";
  }
  finally {UnityEngine.Object.DestroyImmediate(atlas);}
 }
}
