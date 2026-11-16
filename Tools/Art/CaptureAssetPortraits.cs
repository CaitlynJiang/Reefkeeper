using System;using System.IO;using System.Linq;using System.Collections.Generic;using UnityEngine;using UnityEditor;using UnityEngine.Rendering.Universal;using Reefkeeper.Corals;
public static class CaptureAssetPortraits
{
 const int N=1024;const string Out="Assets/Reefkeeper/Textures/UI/AssetPortraits";
 static readonly List<string> manifest=new List<string>();
 public static string Main()
 {
  Directory.CreateDirectory(Out+"/Corals");Directory.CreateDirectory(Out+"/LuckyCat");
  var globals=new Dictionary<string,float>();foreach(string key in new[]{"_UW_FogDensity","_UW_FogStart","_UW_DepthLightFalloff","_UW_AmbientStrength"})globals[key]=Shader.GetGlobalFloat(key);
  var vectors=new Dictionary<string,Vector4>();foreach(string key in new[]{"_UW_Absorption","_UW_CausticsColor"})vectors[key]=Shader.GetGlobalVector(key);
  var go=new GameObject("Temporary portrait camera"){hideFlags=HideFlags.HideAndDontSave};var camera=go.AddComponent<Camera>();camera.enabled=false;camera.cullingMask=1<<31;camera.orthographic=true;camera.clearFlags=CameraClearFlags.SolidColor;camera.nearClipPlane=.01f;camera.farClipPlane=100;
  var data=camera.GetUniversalAdditionalCameraData();data.renderPostProcessing=false;data.renderShadows=false;data.requiresColorOption=CameraOverrideOption.Off;data.requiresDepthOption=CameraOverrideOption.Off;camera.allowHDR=true;
  var rt=new RenderTexture(N,N,24,RenderTextureFormat.ARGBFloat,RenderTextureReadWrite.Linear);rt.antiAliasing=4;var oldRT=RenderTexture.active;
  try
  {
   Shader.SetGlobalFloat("_UW_FogDensity",0);Shader.SetGlobalFloat("_UW_DepthLightFalloff",0);Shader.SetGlobalFloat("_UW_AmbientStrength",.75f);Shader.SetGlobalVector("_UW_Absorption",Vector4.zero);Shader.SetGlobalVector("_UW_CausticsColor",Vector4.zero);
   foreach(string path in AssetDatabase.FindAssets("t:Prefab",new[]{"Assets/Reefkeeper/Prefabs/CoralGrowth"}).Select(AssetDatabase.GUIDToAssetPath).OrderBy(p=>p))
   {
    var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(path);var growth=prefab.GetComponent<CoralGrowth>();if(!growth)continue;
    int stage=Mathf.Min(CoralRules.StageCount-1,growth.stages.Length-1);var model=UnityEngine.Object.Instantiate(growth.stages[stage].gameObject);
    try{Prepare(model);Frame(camera,model,new Vector3(.65f,.4f,-1),1.12f);SaveCapture(camera,rt,Out+"/Corals/"+prefab.name+".png");manifest.Add(prefab.name+" <- "+path+" / "+growth.stages[stage].name+" (playable mature stage)");}finally{UnityEngine.Object.DestroyImmediate(model);}
   }
   string firePath="Assets/Reefkeeper/Generated/V3/FireCoralPad.prefab";var fire=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(firePath));
   try{Prepare(fire);Frame(camera,fire,new Vector3(.45f,.6f,-1),1.12f);SaveCapture(camera,rt,Out+"/Corals/FireCoral.png");manifest.Add("FireCoral <- "+firePath+" (cleaning target, not nursery species)");}finally{UnityEngine.Object.DestroyImmediate(fire);}
   var cat=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Reefkeeper/Prefabs/CatShop/LuckyCat.prefab"));
   try
   {
    Prepare(cat);var head=cat.GetComponentsInChildren<Transform>(true).First(t=>t.name=="Head");
    foreach(var r in cat.GetComponentsInChildren<Renderer>(true))r.enabled=r.transform.IsChildOf(head)||r.name=="Cat_Helmet"||r.name=="Cat_HelmetRing";
    Frame(camera,cat,new Vector3(.08f,.04f,1),1.10f);SaveCapture(camera,rt,Out+"/LuckyCat/LuckyCat_Helmet.png");
    foreach(var r in cat.GetComponentsInChildren<Renderer>(true))if(r.name=="Cat_Helmet"||r.name=="Cat_HelmetRing")r.enabled=false;
    Frame(camera,cat,new Vector3(.08f,.04f,1),1.10f);SaveCapture(camera,rt,Out+"/LuckyCat/LuckyCat_Head.png");
    manifest.Add("LuckyCat_Helmet / LuckyCat_Head <- Prefabs/CatShop/LuckyCat.prefab; original head meshes/materials. Helmet variant keeps glass and brass ring; head variant hides both. Prefab rest pose.");
   }finally{UnityEngine.Object.DestroyImmediate(cat);}
   File.WriteAllText(Out+"/README.txt","ACTUAL UNITY MODEL CAPTURES / 1024 x 1024 RGBA PNG\nNo generated/repainted species or character artwork. Source meshes and materials unchanged.\nCamera studio: fixed 3/4 coral angle, almost frontal cat; no scene fog, absorption or caustics. Current scene main light retained.\nTwo-matte linear-light capture reconstructs straight alpha, including the glass dome; black background is not baked into the PNG.\nSilhouettes framed independently; icon dimensions do not represent relative physical size.\n\n"+string.Join("\n",manifest));
  }
  finally{foreach(var kv in globals)Shader.SetGlobalFloat(kv.Key,kv.Value);foreach(var kv in vectors)Shader.SetGlobalVector(kv.Key,kv.Value);RenderTexture.active=oldRT;camera.targetTexture=null;rt.Release();UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(go);}
  AssetDatabase.Refresh();foreach(var file in Directory.GetFiles(Out,"*.png",SearchOption.AllDirectories)){var imp=AssetImporter.GetAtPath(file.Replace('\\','/')) as TextureImporter;if(!imp)continue;imp.textureType=TextureImporterType.Sprite;imp.spriteImportMode=SpriteImportMode.Single;imp.alphaIsTransparency=true;imp.mipmapEnabled=false;imp.maxTextureSize=N;imp.textureCompression=TextureImporterCompression.Uncompressed;imp.SaveAndReimport();}
  return "Captured "+Directory.GetFiles(Out,"*.png",SearchOption.AllDirectories).Length+" portraits to "+Out;
 }
 static void Prepare(GameObject model){model.hideFlags=HideFlags.HideAndDontSave;model.SetActive(true);model.transform.position=new Vector3(0,1000,0);foreach(var t in model.GetComponentsInChildren<Transform>(true))t.gameObject.layer=31;foreach(var b in model.GetComponentsInChildren<Behaviour>())b.enabled=false;}
 static void Frame(Camera c,GameObject model,Vector3 direction,float padding){var renderers=model.GetComponentsInChildren<Renderer>().Where(r=>r.enabled).ToArray();var bounds=renderers[0].bounds;foreach(var r in renderers)bounds.Encapsulate(r.bounds);c.transform.position=bounds.center+direction.normalized*(bounds.size.magnitude*2+2);c.transform.LookAt(bounds.center);float extent=0;for(int i=0;i<8;i++){var p=bounds.center+Vector3.Scale(bounds.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1));var local=c.transform.InverseTransformPoint(p);extent=Mathf.Max(extent,Mathf.Abs(local.x),Mathf.Abs(local.y));}c.orthographicSize=extent*padding;}
 static Color[] Matte(Camera c,RenderTexture rt,Color background){c.backgroundColor=background;c.targetTexture=rt;c.Render();RenderTexture.active=rt;var tex=new Texture2D(N,N,TextureFormat.RGBAFloat,false,true);try{tex.ReadPixels(new Rect(0,0,N,N),0,0);tex.Apply();return tex.GetPixels();}finally{UnityEngine.Object.DestroyImmediate(tex);}}
 static void SaveCapture(Camera c,RenderTexture rt,string path){var dark=Matte(c,rt,Color.black);var light=Matte(c,rt,Color.white);var pixels=new Color32[N*N];int count=0;for(int i=0;i<pixels.Length;i++){float a=Mathf.Clamp01(1-((light[i].r-dark[i].r)+(light[i].g-dark[i].g)+(light[i].b-dark[i].b))/3);if(a<.002f){pixels[i]=Color.clear;continue;}count++;pixels[i]=new Color(Mathf.LinearToGammaSpace(Mathf.Clamp01(dark[i].r/a)),Mathf.LinearToGammaSpace(Mathf.Clamp01(dark[i].g/a)),Mathf.LinearToGammaSpace(Mathf.Clamp01(dark[i].b/a)),a);}if(count<1000)throw new Exception("Empty capture: "+path);var result=new Texture2D(N,N,TextureFormat.RGBA32,false);try{result.SetPixels32(pixels);result.Apply();File.WriteAllBytes(path,result.EncodeToPNG());}finally{UnityEngine.Object.DestroyImmediate(result);}}
}
