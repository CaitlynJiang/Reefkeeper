using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Reefkeeper;
using Reefkeeper.Photos;

public static class PreviewUnderwaterThrow
{
 const BindingFlags F=BindingFlags.NonPublic|BindingFlags.Instance;
 static void Call(ReefUnderwaterThrow t,string name,params object[] args)=>typeof(ReefUnderwaterThrow).GetMethod(name,F).Invoke(t,args);
 static void Render(Camera camera,string file,int width=960,int height=720)
 {
  var rt=RenderTexture.GetTemporary(width,height,24);var old=RenderTexture.active;
  try{RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=rt});RenderTexture.active=rt;
   var texture=new Texture2D(width,height,TextureFormat.RGB24,false);texture.ReadPixels(new Rect(0,0,width,height),0,0);texture.Apply();File.WriteAllBytes(file,texture.EncodeToPNG());UnityEngine.Object.DestroyImmediate(texture);
  }finally{RenderTexture.active=old;RenderTexture.ReleaseTemporary(rt);}
 }
 public static async Task<string> Main()
 {
  if(!Application.isPlaying)throw new Exception("Play mode required");
  var workshop=UnityEngine.Object.FindFirstObjectByType<ReefWorkshop>();
  var brush=workshop.brush.GetComponent<ReefUnderwaterThrow>();var scraper=workshop.scraper.GetComponent<ReefUnderwaterThrow>();
  var b=new Pose(brush.transform.position,brush.transform.rotation);var s=new Pose(scraper.transform.position,scraper.transform.rotation);
  var cameraObject=new GameObject("Throw preview camera");var camera=cameraObject.AddComponent<Camera>();camera.enabled=false;
  camera.transform.position=new Vector3(.65f,.65f,-15.35f);camera.transform.LookAt(new Vector3(.65f,.25f,-13.8f));
  camera.fieldOfView=58;camera.nearClipPlane=.05f;camera.farClipPlane=150;camera.GetUniversalAdditionalCameraData().allowXRRendering=false;
  var simulation=Physics.simulationMode;float fixedDelta=Time.fixedDeltaTime;
  ReefPrintedPhoto print=null;Texture2D texture=null;
  try
  {
   string folder=Path.GetFullPath("../artifacts/underwater-throw");Directory.CreateDirectory(folder);
   Render(camera,Path.Combine(folder,"scene.png"),256,192);
   texture=new Texture2D(2,2);texture.LoadImage(File.ReadAllBytes(Path.Combine(folder,"scene.png")));
   print=ReefPrintedPhoto.Spawn(texture,camera.transform,"","throw-preview-only");texture=null;
   await Task.Delay(700);
   typeof(ReefPrintedPhoto).GetField("everHeld",F).SetValue(print,true);
   var paper=print.GetComponent<ReefUnderwaterThrow>();
   brush.StopMotion();scraper.StopMotion();paper.StopMotion();
   brush.transform.position=b.position+new Vector3(-.25f,.5f,-.1f);
   scraper.transform.position=s.position+new Vector3(.05f,.5f,-.1f);
   print.transform.SetPositionAndRotation(new Vector3(.63f,.66f,-13.85f),Quaternion.LookRotation(new Vector3(.63f,.66f,-13.85f)-camera.transform.position));
   Physics.SyncTransforms();Physics.simulationMode=SimulationMode.Script;Time.fixedDeltaTime=1f/30;
   for(int i=0;i<270;i++)
   {
    if(i==20)Call(brush,"BeginFlight",new Vector3(-.8f,.25f,.6f),new Vector3(0,0,2.4f));
    if(i==100)Call(scraper,"BeginFlight",new Vector3(.8f,.2f,.6f),new Vector3(0,2.5f,-1f));
    if(i==180)Call(paper,"BeginFlight",new Vector3(.1f,.3f,-.8f),new Vector3(1.2f,1.5f,.4f));
    Physics.Simulate(1f/30);Call(brush,"FixedUpdate");Call(scraper,"FixedUpdate");Call(paper,"FixedUpdate");
    Render(camera,Path.Combine(folder,i.ToString("D3")+".png"));
   }
   return "Saved 270 native URP scene frames: brush, scraper and photo water-damped throws. Scripted launch impulses, not headset input footage. Scene objects restored.";
  }
  finally
  {
   Physics.simulationMode=simulation;Time.fixedDeltaTime=fixedDelta;
   brush.StopMotion();scraper.StopMotion();brush.transform.SetPositionAndRotation(b.position,b.rotation);scraper.transform.SetPositionAndRotation(s.position,s.rotation);Physics.SyncTransforms();
   if(print)UnityEngine.Object.Destroy(print.gameObject);if(texture)UnityEngine.Object.Destroy(texture);UnityEngine.Object.Destroy(cameraObject);
  }
 }
}
