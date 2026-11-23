using System;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEditor;
using Reefkeeper.Photos;
public static class CheckPhotoWallSway
{
 static void Need(bool ok,string why){if(!ok)throw new Exception(why);}
 public static string Main()
 {
  Need(Application.isPlaying,"Play mode required");
  var root=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Reefkeeper/Resources/Photos/PhotoBoard.prefab"),new Vector3(1000,1000,1000),Quaternion.Euler(0,27,0));
  try
  {
   var s=root.GetComponent<ReefPhotoWallSway>();var wall=root.GetComponent<ReefPhotoWall>();
   float idleBend=s.idleBend;s.idleBend=0; // Test the existing touch-only response separately.
   Need(s.vines.Length==6,"Expected six vines");
   Need(root.GetComponentsInChildren<SkinnedMeshRenderer>().Length==0,"Whole-wall skin still present");
   var frame=root.GetComponentsInChildren<Transform>().Where(t=>t.name.StartsWith("Pb_Post")||t.name=="Pb_Beam").ToArray();
   var framePos=frame.Select(t=>t.position).ToArray();var frameRot=frame.Select(t=>t.rotation).ToArray();
   foreach(var card in wall.cards)if(card)Need(card.parent.name.StartsWith("Independent vine"),"Photo outside vine group: "+card.name);
   Action reset=()=>typeof(ReefPhotoWallSway).GetMethod("OnDisable",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(s,null);
   float max=0;
   for(int n=0;n<6;n++)
   {
    var v=s.vines[n];var pivot=v.pivot.position;var tip=v.pivot.TransformPoint(v.localTip);
    var at=v.pivot.TransformPoint(v.localTip*.65f);
    s.SampleHandContact(0,true,true,at+root.transform.forward*.4f,at+root.transform.forward*.3f,.1f);
    s.SampleHandContact(0,true,true,at+root.transform.forward*.2f,at+root.transform.forward*.02f,.1f);
    Need(s.Moving,"Contact failed for vine "+n);
    for(int k=0;k<15;k++)s.StepMotion(1f/60);
    Need(v.angle.magnitude>.5f,"Selected vine barely moved");
    max=Mathf.Max(max,Vector3.Distance(tip,v.pivot.TransformPoint(v.localTip)));
    Need(Vector3.Distance(pivot,v.pivot.position)<.00001f,"Suspension point moved");
    for(int i=0;i<6;i++)if(i!=n)Need(s.vines[i].angle==Vector2.zero,"Neighbour moved");
    for(int i=0;i<frame.Length;i++)Need(Vector3.Distance(frame[i].position,framePos[i])<.00001f&&Quaternion.Angle(frame[i].rotation,frameRot[i])<.001f,"Wooden frame moved");
    for(int k=0;k<600;k++)s.StepMotion(1f/60);
    Need(!s.Moving&&Vector3.Distance(tip,v.pivot.TransformPoint(v.localTip))<.00001f,"Vine did not settle");
    reset();
   }
   var target=s.vines[0].pivot.TransformPoint(s.vines[0].localTip*.6f);
   s.SampleHand(0,true,true,target+root.transform.forward*.3f,.1f);s.SampleHand(0,true,false,target,.1f);
   Need(!s.Moving,"Occupied hand moved vine");reset();
   s.SampleHand(0,true,true,target+root.transform.forward*.4f,.1f);s.SampleHand(0,true,true,target,.01f);
   Need(!s.Moving,"Tracking jump moved vine");
   reset();s.idleBend=idleBend;Need(idleBend>0&&idleBend<=.35f,"Idle should be very subtle by default");
   var tips=s.vines.Select(v=>v.pivot.TransformPoint(v.localTip)).ToArray();
   var pivots=s.vines.Select(v=>v.pivot.position).ToArray();var displacements=new float[6];
   for(int k=0;k<900;k++)
   {
    s.StepMotion(1f/60);
    for(int i=0;i<6;i++)
    {
     var v=s.vines[i];displacements[i]=Mathf.Max(displacements[i],Vector3.Distance(tips[i],v.pivot.TransformPoint(v.localTip)));
     Need(Vector3.Distance(pivots[i],v.pivot.position)<.00001f,"Idle moved suspension");
     Need(v.angle==Vector2.zero&&!v.moving,"Idle woke the touch spring");
    }
   }
   Need(displacements.All(d=>d>.0005f&&d<.015f),"Idle is absent or too strong");
   for(int i=0;i<frame.Length;i++)Need(Vector3.Distance(frame[i].position,framePos[i])<.00001f&&Quaternion.Angle(frame[i].rotation,frameRot[i])<.001f,"Idle moved wooden frame");
   var touched=s.vines[0];s.Push(touched.pivot.TransformPoint(touched.localTip*.65f),root.transform.right);
   for(int k=0;k<15;k++)s.StepMotion(1f/60);
   Need(touched.angle.magnitude>.5f,"Idle prevented touch response");
   for(int k=0;k<600;k++)s.StepMotion(1f/60);
   Need(!s.Moving,"Touch did not settle while idle was enabled");
   s.idleBend=0;s.StepMotion(1f/60);
   Need(s.vines.All(v=>Vector3.Distance(tips[Array.IndexOf(s.vines,v)],v.pivot.TransformPoint(v.localTip))<.00001f),"Disabling idle left a pose offset");
   return "PASS: six independent touch responses, fixed wooden frame/suspension, photos follow vines, touch guards preserved; tiny idle does not wake springs and touch settles back to idle. Maximum idle tip displacement "+displacements.Max()+" m; touch "+max+" m. No skinning or added physics.";
  }
  finally{UnityEngine.Object.DestroyImmediate(root);}
 }
}

