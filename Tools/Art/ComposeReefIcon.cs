using System;using System.IO;using System.Linq;using System.Collections.Generic;using System.Reflection;using UnityEngine;using UnityEngine.UI;using UnityEditor;
public static class ComposeReefIcon
{
 const int N=1024;const string Root="Assets/Reefkeeper/Textures/UI/AssetPortraits";const string Out="Assets/Reefkeeper/Art/AppIcon";
 static readonly List<UnityEngine.Object> cleanup=new List<UnityEngine.Object>();
 public static string Main()
 {
  Directory.CreateDirectory(Root+"/Stickers");Directory.CreateDirectory(Out+"/WithHelmet");Directory.CreateDirectory(Out+"/WithoutHelmet");Directory.CreateDirectory(Path.GetFullPath("../artifacts/icon-review"));
  try
  {
   var border=typeof(Reefkeeper.Editor.CoralStickerCapture).GetMethod("WhiteBorder",BindingFlags.NonPublic|BindingFlags.Static);
   foreach(var path in Directory.GetFiles(Root+"/Corals","*.png")){var t=Load(path);border.Invoke(null,new object[]{t});Save(t,Root+"/Stickers/"+Path.GetFileName(path));}
   var bg=New();var colors=Enumerable.Repeat(new Color(.22f,.53f,.76f,1),N*N).ToArray();bg.SetPixels(colors);bg.Apply();
   var middle=New();Draw(middle,Load(Root+"/Stickers/BranchCoral.png"),305,726,430,-18);Draw(middle,Load(Root+"/Stickers/PolypPillar_Pink.png"),724,710,410,17);Draw(middle,Load(Root+"/Stickers/DiscCoral.png"),306,300,420,-12);Draw(middle,Load(Root+"/Stickers/TubeSponge_Lemon.png"),720,294,410,18);
   var previewTextures=new List<Texture2D>();
   foreach(bool helmet in new[]{true,false})
   {
    string dir=Out+(helmet?"/WithHelmet":"/WithoutHelmet");var front=New();Draw(front,Load(Root+"/LuckyCat/"+(helmet?"LuckyCat_Helmet":"LuckyCat_Head")+".png"),512,504,helmet?750:800,0);
    Save(bg,dir+"/icon_bg.png");Save(middle,dir+"/icon_fg2.png");Save(front,dir+"/icon_fg3.png");var merged=Clone(bg);Draw(merged,middle,512,512,1024,0);Draw(merged,front,512,512,1024,0);Save(merged,dir+"/preview.png");previewTextures.Add(merged);
    var small=New(128,128);Draw(small,merged,64,64,128,0);Save(small,dir+"/preview_128.png");
    foreach(var pair in new[]{("corals",middle),("cat",front)}){float maxR=0;int minX=N,minY=N,maxX=0,maxY=0;var px=pair.Item2.GetPixels32();for(int y=0;y<N;y++)for(int x=0;x<N;x++)if(px[y*N+x].a>2){maxR=Mathf.Max(maxR,new Vector2(x-511.5f,y-511.5f).magnitude);minX=Math.Min(minX,x);minY=Math.Min(minY,y);maxX=Math.Max(maxX,x);maxY=Math.Max(maxY,y);}if(maxR>490)throw new Exception("Foreground too close to circular mask: "+pair.Item1);File.AppendAllText(Path.GetFullPath("../artifacts/icon-review/validation.txt"),dir+" / "+pair.Item1+": bounds="+minX+","+minY+".."+maxX+","+maxY+"; max radius="+maxR.ToString("F1")+" px (mask 512)\n");}
   }
   Sheet(previewTextures.ToArray(),new[]{"A / GLASS HELMET","B / HEAD ONLY"},2,1,650,730,Path.GetFullPath("../artifacts/icon-review/icon-comparison.png"),false);
   var portraits=Directory.GetFiles(Root+"/Stickers","*.png").OrderBy(p=>p).ToArray();Sheet(portraits.Select(Load).ToArray(),portraits.Select(p=>Path.GetFileNameWithoutExtension(p).Replace("_"," ")).ToArray(),4,3,340,360,Path.GetFullPath("../artifacts/icon-review/coral-contact-sheet.png"),true);
   Sheet(new[]{Load(Root+"/LuckyCat/LuckyCat_Helmet.png"),Load(Root+"/LuckyCat/LuckyCat_Head.png")},new[]{"GLASS + BRASS RING / transparent PNG","HEAD ONLY / transparent PNG"},2,1,650,700,Path.GetFullPath("../artifacts/icon-review/cat-comparison.png"),true);
   File.WriteAllText(Out+"/README.txt","REEFKEEPER / THREE-LAYER APP ICON STUDY\n\nWithHelmet and WithoutHelmet each contain:\nicon_bg.png: opaque blue background, 1024 square, sRGB values\nicon_fg2.png: four stickers captured from actual mature coral prefabs\nicon_fg3.png: actual Lucky Cat head, with/without original glass and brass ring\npreview.png / preview_128.png: flat review composites, not extra layers\n\nForegrounds remain within the inscribed circle; exact extents are in artifacts/icon-review/validation.txt. No additional fake shadows or system glints are baked into layers.\nThe helmet has genuine semitransparent pixels. PICO recommends crisp foreground silhouettes and discourages semitransparent highlights/shadows: treat WithHelmet as an exploratory comparison, not a validated submission asset. WithoutHelmet is the safer first layered-icon candidate.\nOnly image assets are prepared; PlayerSettings, SDK packages and Android manifests have not been changed. Current project uses PICO Unity Integration SDK 3.4.0; the documented Icon Configurator belongs to SDK 6.1.x. Device/SDK integration is pending.\n\nSources checked 2026-11-16:\nhttps://developer-cn.picoxr.com/document/spatial-design/app-icon/\nhttps://developer-cn.picoxr.com/document/pico-unity-quickstart/icon-configurator/\nhttps://developer-cn.picoxr.com/document/pico-unreal-quickstart/unreal-icon-configurator/\n\nLayered launcher icons: 2-3 layers, supported on Space Pro / OS 6; PICO 4/4 Ultra use 2D icons. General design guidance: 1024 square PNG, <=4 MB per layer, foregrounds inside inscribed circle.\n");
  }
  finally{foreach(var obj in cleanup)if(obj)UnityEngine.Object.DestroyImmediate(obj);cleanup.Clear();}
  AssetDatabase.Refresh();return "Created all coral stickers, two 3-layer icon variants and three review sheets.";
 }
 static Texture2D New(int w=N,int h=N){var t=new Texture2D(w,h,TextureFormat.RGBA32,false);t.SetPixels(new Color[w*h]);t.Apply();cleanup.Add(t);return t;}
 static Texture2D Load(string p){var t=New(2,2);t.LoadImage(File.ReadAllBytes(p));return t;}
 static Texture2D Clone(Texture2D source){var t=New(source.width,source.height);t.SetPixels(source.GetPixels());t.Apply();return t;}
 static void Save(Texture2D t,string p){File.WriteAllBytes(p,t.EncodeToPNG());}
 static void Draw(Texture2D dst,Texture2D src,float cx,float cy,float size,float degrees)
 {
  var dest=dst.GetPixels();float a=degrees*Mathf.Deg2Rad,c=Mathf.Cos(a),s=Mathf.Sin(a),half=size*.72f;
  for(int y=Mathf.Max(0,Mathf.FloorToInt(cy-half));y<Mathf.Min(dst.height,Mathf.CeilToInt(cy+half));y++)for(int x=Mathf.Max(0,Mathf.FloorToInt(cx-half));x<Mathf.Min(dst.width,Mathf.CeilToInt(cx+half));x++)
  {float dx=x+.5f-cx,dy=y+.5f-cy,u=(c*dx+s*dy)/size+.5f,v=(-s*dx+c*dy)/size+.5f;if(u<0||u>1||v<0||v>1)continue;var fg=src.GetPixelBilinear(u,v);if(fg.a<.001f)continue;int i=y*dst.width+x;var bg=dest[i];float alpha=fg.a+bg.a*(1-fg.a);var f=fg.linear;var b=bg.linear;var col=(f*fg.a+b*(bg.a*(1-fg.a)))/alpha;col=col.gamma;col.a=alpha;dest[i]=col;}
  dst.SetPixels(dest);dst.Apply();
 }
 static void Sheet(Texture2D[] textures,string[] labels,int cols,int rows,int cellW,int cellH,string path,bool light)
 {
  var root=new GameObject("Temporary asset review sheet",typeof(RectTransform),typeof(Canvas));cleanup.Add(root);var canvas=root.GetComponent<Canvas>();canvas.renderMode=RenderMode.WorldSpace;var rect=root.GetComponent<RectTransform>();rect.sizeDelta=new Vector2(cols*cellW,rows*cellH);root.transform.position=new Vector3(2000,2000,2000);root.transform.localScale=Vector3.one*.001f;
  Font font=UnityEngine.Object.FindFirstObjectByType<Reefkeeper.ReefCleaningHUD>().boldFont;
  for(int i=0;i<textures.Length;i++)
  {float x=(i%cols+.5f)*cellW-cols*cellW*.5f,y=rows*cellH*.5f-(i/cols+.5f)*cellH;
   var picture=new GameObject("Actual capture",typeof(RectTransform),typeof(RawImage));picture.transform.SetParent(root.transform,false);var r=picture.GetComponent<RectTransform>();r.sizeDelta=Vector2.one*(cellW-32);r.anchoredPosition=new Vector2(x,y+14);var raw=picture.GetComponent<RawImage>();raw.texture=textures[i];
   var label=new GameObject("Caption",typeof(RectTransform),typeof(Text));label.transform.SetParent(root.transform,false);r=label.GetComponent<RectTransform>();r.sizeDelta=new Vector2(cellW-10,48);r.anchoredPosition=new Vector2(x,y-cellH/2+30);var text=label.GetComponent<Text>();text.font=font;text.fontSize=cols==4?19:24;text.alignment=TextAnchor.MiddleCenter;text.color=light?new Color(.1f,.25f,.31f):Color.white;text.text=labels[i];
  }
  foreach(var t in root.GetComponentsInChildren<Transform>())t.gameObject.layer=31;
  var go=new GameObject("Review camera");cleanup.Add(go);var cam=go.AddComponent<Camera>();cam.enabled=false;cam.orthographic=true;cam.orthographicSize=rows*cellH*.0005f;cam.cullingMask=1<<31;cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=light?new Color(.80f,.87f,.88f):new Color(.055f,.12f,.18f);cam.nearClipPlane=.01f;cam.farClipPlane=2;cam.transform.position=root.transform.position+Vector3.back;
  var rt=new RenderTexture(cols*cellW,rows*cellH,24);var prior=RenderTexture.active;try{cam.targetTexture=rt;Canvas.ForceUpdateCanvases();cam.Render();RenderTexture.active=rt;var t=New(rt.width,rt.height);t.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0);t.Apply();Save(t,path);}finally{cam.targetTexture=null;RenderTexture.active=prior;rt.Release();UnityEngine.Object.DestroyImmediate(rt);root.SetActive(false);}
 }
}
