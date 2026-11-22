using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using Reefkeeper;

public static class CheckScallopReward
{
 const BindingFlags F=BindingFlags.NonPublic|BindingFlags.Instance;
 static void Need(bool ok,string why){if(!ok)throw new Exception(why);}
 static T Get<T>(object o,string n)=>(T)o.GetType().GetField(n,F).GetValue(o);
 static RackDirtPatch Patch(Vector3 p,ReefSurfaceKind kind,Material material)
 {
  var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.SetActive(false);go.name="Reward test patch";
  go.transform.position=p;go.transform.localScale=new Vector3(.35f,.015f,.35f);
  var renderer=go.GetComponent<Renderer>();renderer.sharedMaterial=material;
  var patch=go.AddComponent<RackDirtPatch>();patch.dirtVisual=renderer;patch.surfaceKind=kind;patch.requiredTravel=.035f;
  go.SetActive(true);return patch;
 }
 static void Clean(ReefBrush tool,RackDirtPatch patch)
 {
  patch.transform.rotation=Quaternion.FromToRotation(Vector3.up,tool.bristles.up);
  Physics.SyncTransforms();
  Vector3 from=patch.transform.position+patch.transform.up*.02f;
  Vector3 tangent=Vector3.ProjectOnPlane(tool.bristles.forward,patch.transform.up).normalized;
  typeof(ReefBrush).GetMethod("Sweep",F).Invoke(tool,new object[]{from,from+tangent*.055f});
 }
 public static string Main()
 {
  Need(Application.isPlaying,"Run in underwater Play mode");
  var shop=UnityEngine.Object.FindFirstObjectByType<ReefWorkshop>();
  var reward=shop.GetComponent<ReefScallopReward>();Need(reward,"Workshop did not install reward component");
  var material=new Material(Shader.Find("Universal Render Pipeline/Lit"));
  var algae=Patch(new Vector3(100,100,100),ReefSurfaceKind.Algae,material);
  var fire=Patch(new Vector3(102,100,100),ReefSurfaceKind.FireCoral,material);
  float volume=reward.volume;reward.volume=0;
  var advance=typeof(ReefScallopReward).GetMethod("Advance",F);
  Action<float> tick=dt=>advance.Invoke(reward,new object[]{dt});
  var source=Get<AudioSource>(reward,"audioSource");
  var arrivals=new List<int>();
  Action<int> arrived=n=>{Need(reward.DisplayedBalance==n,"Number and arrival were not synchronized");Need(source.isPlaying,"Arrival did not start collection audio");arrivals.Add(n);};
  reward.Collected+=arrived;
  try
  {
   reward.ResetSession();Need(!reward.Visible&&reward.Balance==0,"Counter visible at idle/startup");
   Need(reward.collectClip&&reward.collectClip.length<.3f,"Short Bubble collection clip missing");
   Need(Get<Sprite>(reward,"shell"),"User scallop sprite missing");
   var angles=Get<Sprite[]>(reward,"shellAngles");
   Need(angles.Length==6&&Get<Sprite>(reward,"shell")==angles[2],"Six angles or upper-right counter selection missing");
   foreach(var angle in angles)Need(angle&&angle.texture==angles[0].texture,"Angles do not share one texture");
   var number=Get<Text>(reward,"number");
   Need(number.font.name=="LilitaOne-Regular"&&((Color32)number.color).Equals(new Color32(255,194,172,255)),"Selected font or peach color missing");
   algae.ApplyStroke(100,CleaningToolKind.Brush);Need(reward.Balance==0,"Programmatic setup clear awarded currency");
   algae.ResetDirt();Clean(shop.scraper,algae);Need(reward.Balance==0&&!algae.Complete,"Wrong tool earned reward");
   Clean(shop.brush,algae);Need(algae.Complete&&reward.Balance==3,"Brush cleaning completion failed to award three shells");
   Need(reward.DisplayedBalance==0&&!source.isPlaying&&arrivals.Count==0,"Award sounded/counted before visual arrival");
   Clean(shop.brush,algae);Need(reward.Balance==3,"Completed patch rewarded twice");
   for(int i=0;i<5;i++)tick(.05f);
   Need(reward.InFlight>0&&reward.DisplayedBalance==0&&!source.isPlaying,"Spawn/flight caused premature sound or count");
   foreach(var entry in Get<Array>(reward,"flights"))
   {
    var type=entry.GetType();
    if(!(bool)type.GetField("active").GetValue(entry))continue;
    var icon=(Image)type.GetField("image").GetValue(entry);
    var trail=(Image)type.GetField("trail").GetValue(entry);
    Need(Array.IndexOf(angles,icon.sprite)>=0&&trail.sprite==icon.sprite,"Flight/trail angle mismatch");
   }
   for(int i=0;i<70;i++)tick(.05f);
   Need(reward.DisplayedBalance==3&&arrivals.Count==3&&!reward.Visible&&reward.InFlight==0,"Arrival count or fade-out failed");
   Clean(shop.brush,fire);Need(!fire.Complete&&reward.Balance==3,"Brush cleaned fire coral/rewarded it");
   Clean(shop.scraper,fire);Need(fire.Complete&&reward.Balance==6,"Scraper completion failed to award");
   algae.ResetDirt();Clean(shop.brush,algae);Need(reward.Balance==9,"Regrown algae could not earn a new reward");
   for(int i=0;i<100;i++)tick(.05f);
   Need(reward.DisplayedBalance==9&&arrivals.Count==9,"Overlapping cleaning batches lost/duplicated rewards");
   for(int i=0;i<arrivals.Count;i++)Need(arrivals[i]==i+1,"Counter arrived out of sequence");
   var canvas=Get<Canvas>(reward,"canvas");
   Need(!canvas.GetComponent<GraphicRaycaster>(),"Collection feedback intercepts pointing");
   foreach(var graphic in canvas.GetComponentsInChildren<Graphic>(true))Need(!graphic.raycastTarget,"Reward graphic intercepts input");
   var adapter=canvas.GetComponent<XRWorldHud>();
   typeof(XRWorldHud).GetMethod("Switch",F).Invoke(adapter,new object[]{true});
   Need(canvas.renderMode==RenderMode.WorldSpace&&canvas.worldCamera,"XR feedback is not world-space");
   reward.Award(37,Camera.main.transform.position-Camera.main.transform.forward);
   for(int i=0;i<220;i++){tick(.05f);Need(reward.InFlight<=18,"Exceeded fixed visual pool");}
   Need(reward.Balance==46&&reward.DisplayedBalance==46&&!reward.Visible,"Queued bursts/offscreen origin lost rewards or did not fade");
   typeof(XRWorldHud).GetMethod("Switch",F).Invoke(adapter,new object[]{false});
   reward.Award(3,algae.transform.position);tick(.05f);reward.enabled=false;
   Need(!reward.Visible&&reward.DisplayedBalance==reward.Balance&&reward.InFlight==0,"Disabling lost currency or left visible tokens");
   reward.enabled=true;reward.ResetSession();
   Need(reward.Balance==0&&reward.DisplayedBalance==0&&!reward.Visible,"Reset did not clear the prototype session balance");
   return "PASS: actual brush/scraper Sweep completion, wrong-tool/setup/duplicate guards, algae regrowth, staggered flights, arrival-only audio and numeric increments, fade-out, overlapping/queued rewards, fixed pool, XR world canvas, nonblocking UI, disable/reset. Editor fixtures; PICO visual/audio feel still needs headset testing.";
  }
  finally
  {
   reward.Collected-=arrived;reward.enabled=true;reward.ResetSession();reward.volume=volume;
   UnityEngine.Object.DestroyImmediate(algae.gameObject);UnityEngine.Object.DestroyImmediate(fire.gameObject);UnityEngine.Object.DestroyImmediate(material);
  }
 }
}
