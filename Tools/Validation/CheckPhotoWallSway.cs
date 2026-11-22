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
   return "PASS: each of six vines independently triggers; neighbours and wooden frame stay fixed; all photos correctly parented; fixed suspension; settle to rest; occupied hand and tracking jump guards. Max sampled tip displacement "+max+" m. No full-wall skin.";
  }
  finally{UnityEngine.Object.DestroyImmediate(root);}
 }
}

