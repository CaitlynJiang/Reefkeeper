using System;
using System.IO;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using Reefkeeper.Photos;

public static class CheckPhotoDeposit
{
 const BindingFlags F=BindingFlags.NonPublic|BindingFlags.Instance;
 static void Need(bool ok,string why){if(!ok)throw new Exception(why);}
 static void Set(object obj,string name,object val)=>obj.GetType().GetField(name,F).SetValue(obj,val);
 static bool Deposit(ReefPrintedPhoto print)=>(bool)typeof(ReefPrintedPhoto).GetMethod("TryDeposit",F).Invoke(print,null);
 static void Save(Camera camera,string path)
 {
  var rt=RenderTexture.GetTemporary(512,512,24);var old=RenderTexture.active;
  try{RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=rt});RenderTexture.active=rt;
   var t=new Texture2D(512,512,TextureFormat.RGB24,false);t.ReadPixels(new Rect(0,0,512,512),0,0);t.Apply();File.WriteAllBytes(path,t.EncodeToPNG());UnityEngine.Object.Destroy(t);
  }finally{RenderTexture.active=old;RenderTexture.ReleaseTemporary(rt);}
 }
 public static async Task<string> Main()
 {
  Need(Application.isPlaying,"Play mode required");
  _=ReefPhotoAlbum.PhotoCount;
  var dict=(Dictionary<string,Texture2D>)typeof(ReefPhotoAlbum).GetField("photos",BindingFlags.NonPublic|BindingFlags.Static).GetValue(null);
  const string id="Fish1";
  dict.TryGetValue(id,out var previous);
  string file=Path.Combine(Application.persistentDataPath,"ReefPhotos",id+".png");
  byte[] backup=File.Exists(file)?File.ReadAllBytes(file):null;
  dict.Remove(id);
  var root=new GameObject("Photo deposit validation");root.transform.position=new Vector3(1000,1000,1000);
  ReefPrintedPhoto print=null;Texture2D photo=null;Material slotMaterial=null;
  int arrivals=0;Action<string> counted=s=>{if(s==id)arrivals++;};ReefPhotoAlbum.Changed+=counted;
  try {
   var wall=root.AddComponent<ReefPhotoWall>();
   var quad=GameObject.CreatePrimitive(PrimitiveType.Quad);UnityEngine.Object.Destroy(quad.GetComponent<Collider>());
   quad.layer=31;
   quad.transform.SetParent(root.transform,false);quad.transform.localRotation=Quaternion.Euler(0,180,0);quad.transform.localScale=new Vector3(.3f,.2f,1);
   slotMaterial=new Material(Shader.Find("Universal Render Pipeline/Unlit"));quad.GetComponent<Renderer>().sharedMaterial=slotMaterial;
   wall.slots=new[]{quad.GetComponent<Renderer>()};wall.cards=new[]{quad.transform};wall.labels=false;
   await Task.Delay(150);
   Need(wall.TryGetSlotPose(id,out var pose),"Matching slot not found");
   Need(!wall.TryGetSlotPose("Fish2",out _),"Missing slot accepted");
   Need(!ReefPhotoWall.TryFindDeposit(id,root.transform.position+Vector3.back*.2f,out _),"Rear side accepted a deposit");
   var head=new GameObject("test head").transform;head.SetParent(root.transform,false);head.localPosition=new Vector3(0,0,2);head.localRotation=Quaternion.Euler(0,180,0);
   photo=new Texture2D(16,16);var pixels=new Color[256];for(int i=0;i<pixels.Length;i++)pixels[i]=i%16<8?Color.cyan:Color.red;photo.SetPixels(pixels);photo.Apply();
   print=ReefPrintedPhoto.Spawn(photo,head,"Reef Shoal Fish",id);
   await Task.Delay(650);
   Need(!ReefPhotoAlbum.Has(id)&&arrivals==0&&ReefPrintedPhoto.HasPending(id),"Capture automatically stored the photo");
   print.transform.position=pose.position+wall.transform.forward*.2f;
   Need(!Deposit(print),"Unheld print deposited itself");
   Set(print,"everHeld",true);print.transform.position=pose.position+wall.transform.forward*2;
   Need(!Deposit(print),"Distant print deposited");
   var grab=print.GetComponent<XRGrabInteractable>();grab.selectExited.Invoke(new SelectExitEventArgs());
   await Task.Delay(350);Need(print&&!print.Depositing,"Release destroyed the undelivered print");
   // Render the actual caption from both sides, including its paper depth occlusion.
   foreach(var t in print.GetComponentsInChildren<Transform>())t.gameObject.layer=31;
   var cameraObject=new GameObject("photo validation camera");cameraObject.transform.SetParent(root.transform,false);
   var camera=cameraObject.AddComponent<Camera>();camera.enabled=false;camera.cullingMask=1<<31;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.gray;camera.nearClipPlane=.01f;camera.orthographic=true;camera.orthographicSize=.11f;
   camera.GetUniversalAdditionalCameraData().allowXRRendering=false;
   string folder=Path.GetFullPath("../artifacts/photo-deposit");Directory.CreateDirectory(folder);
   for(int side=0;side<2;side++) {
    camera.transform.position=print.transform.position+print.transform.forward*(side==0?-.4f:.4f);
    camera.transform.LookAt(print.transform.position,print.transform.up);
    var rt=RenderTexture.GetTemporary(512,512,24);var old=RenderTexture.active;
    try {RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=rt});RenderTexture.active=rt;
     var image=new Texture2D(512,512,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,512,512),0,0);image.Apply();File.WriteAllBytes(Path.Combine(folder,side==0?"front.png":"back.png"),image.EncodeToPNG());UnityEngine.Object.Destroy(image);
    }finally{RenderTexture.active=old;RenderTexture.ReleaseTemporary(rt);}
   }
   print.transform.position=pose.position+wall.transform.forward*.2f;
   Need(Deposit(print),"Held near-wall print did not begin deposit");
   Need(!Deposit(print)&&!ReefPhotoAlbum.Has(id),"Duplicate trigger or early storage");
   await Task.Delay(500);
   Need(ReefPhotoAlbum.Get(id)==photo&&arrivals==1,"Arrival did not store exactly once");
   var block=new MaterialPropertyBlock();wall.slots[0].GetPropertyBlock(block);
   Need(block.GetFloat("_Reveal")<1,"Wall did not animate reveal");
   camera.transform.position=root.transform.position+Vector3.forward*.8f;camera.transform.LookAt(root.transform.position);camera.orthographicSize=.18f;
   Save(camera,Path.Combine(folder,"wall-developing.png"));
   await Task.Delay(1100);
   wall.slots[0].GetPropertyBlock(block);Need(block.GetFloat("_Reveal")>.99f,"Wall reveal did not settle");
   Save(camera,Path.Combine(folder,"wall-complete.png"));
   Need(!print&&!ReefPrintedPhoto.HasPending(id)&&photo,"Print cleanup destroyed the stored texture or retained pending state");
   Need(File.Exists(file),"Delivered photo was not saved");
   return "PASS: no auto-store, pending duplicate guard, matching/front-side/range checks, released print remains, exact arrival storage, wall reveal, texture ownership and persistence. Front/back renders saved. Headset interaction still needs testing.";
  } finally {
   ReefPhotoAlbum.Changed-=counted;
   if(print)UnityEngine.Object.Destroy(print.gameObject);
   UnityEngine.Object.Destroy(root);if(slotMaterial)UnityEngine.Object.Destroy(slotMaterial);
   dict.Remove(id);if(previous)dict[id]=previous;
   if(backup!=null)File.WriteAllBytes(file,backup);else if(File.Exists(file))File.Delete(file);
   var changed=(Action<string>)typeof(ReefPhotoAlbum).GetField("Changed",BindingFlags.Static|BindingFlags.NonPublic).GetValue(null);changed?.Invoke(null);
   if(photo)UnityEngine.Object.Destroy(photo);
  }
 }
}
