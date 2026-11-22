using System;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Inputs;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using Reefkeeper;
using Reefkeeper.Hands;

public static class CheckUnderwaterThrow
{
 const BindingFlags F=BindingFlags.Instance|BindingFlags.Static|BindingFlags.NonPublic;
 static void Need(bool ok,string message){if(!ok)throw new Exception(message);}
 static void Set(object o,string n,object v)=>o.GetType().GetField(n,F).SetValue(o,v);
 static object Call(object o,string n,params object[] v)=>o.GetType().GetMethod(n,F).Invoke(o,v);
 static void Hand(int index,Vector3 at,bool tracked=true,bool pinch=false)
 {
  ((ReefHands.State[])typeof(ReefHands).GetField("states",F).GetValue(null))[index]=new ReefHands.State{Tracked=tracked,Pinch=pinch,Grab=pinch,Palm=new Pose(at,Quaternion.identity),PinchPoint=at+Vector3.forward*.05f};
  typeof(ReefHands).GetField("frame",F).SetValue(null,Time.frameCount);
 }
 static void History(ReefUnderwaterThrow t,float speed)
 {
  Set(t,"count",0);Set(t,"next",0);
  for(int i=0;i<12;i++)Call(t,"Sample",new Pose(Vector3.right*speed*(i*.01f),Quaternion.identity),Time.unscaledTime-.12f+i*.01f);
 }
 public static string Main()
 {
  Need(Application.isPlaying,"Play mode required");
  var mode=typeof(XRInputModalityManager).GetField("s_CurrentInputMode",F).GetValue(null);
  var value=mode.GetType().GetProperty("Value");var oldMode=value.GetValue(mode);
  var roots=(Transform[])typeof(ReefInput).GetField("hands",F).GetValue(null);
  var active=roots.Select(r=>r.gameObject.activeSelf).ToArray();
  var simulation=Physics.simulationMode;
  var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name="Throw validation";go.transform.position=new Vector3(1000,1000,1000);go.transform.localScale=Vector3.one*.1f;
  var body=go.AddComponent<Rigidbody>();body.isKinematic=true;body.useGravity=false;
  var grab=go.AddComponent<XRGrabInteractable>();grab.movementType=XRBaseInteractable.MovementType.Instantaneous;
  var throwing=ReefUnderwaterThrow.Install(grab);
  var manager=UnityEngine.Object.FindFirstObjectByType<XRInteractionManager>();
  var interactor=ReefInput.BareHandInteractor(0);
  bool oldEnabled=interactor.enabled;
  var space=ReefInput.HandTrackingSpace;var oldPosition=space.position;var oldRotation=space.rotation;
  GameObject barrier=null;
  try
  {
   if(DiveCardMock.Instance)DiveCardMock.Instance.SetOpen(false);
   if(ReefToolMenu.Instance)ReefToolMenu.Instance.SetOpen(false);
   // Closing the menu intentionally reserves two frames; this fixture starts after that guard.
   if(ReefToolMenu.Instance)Set(ReefToolMenu.Instance,"closedFrame",-100);
   value.SetValue(mode,XRInputModalityManager.InputMode.TrackedHand);
   roots[0].gameObject.SetActive(true);interactor.enabled=true;interactor.interactionManager=manager;
   Action select=()=>{Hand(0,Vector3.zero,true,true);manager.SelectEnter((IXRSelectInteractor)interactor,(IXRSelectInteractable)grab);Need(grab.isSelected,"Real XRI selection failed");};
   Action<float,bool> release=(speed,tracked)=>{History(throwing,speed);Hand(0,Vector3.right*speed*.12f,tracked);manager.SelectExit((IXRSelectInteractor)interactor,(IXRSelectInteractable)grab);Call(throwing,"LateUpdate");};

   select();release(.15f,true);Need(!throwing.Flying&&body.isKinematic,"Slow placement launched");
   select();release(3f,true);Need(throwing.Flying&&!body.isKinematic,"Tracked release did not launch through XRI");
   Need(body.linearVelocity.magnitude<=throwing.maximumSpeed+.001f,"Speed cap failed");
   select();Need(!throwing.Flying&&body.isKinematic,"Regrab did not stop flight");
   release(.1f,true);Need(body.isKinematic&&!throwing.Flying,"Slow release after regrab left a dynamic body");
   select();release(2f,false);Need(!throwing.Flying&&body.isKinematic,"Tracking loss launched");
   select();History(throwing,2);manager.CancelInteractableSelection((IXRSelectInteractable)grab);Call(throwing,"LateUpdate");Need(!throwing.Flying,"Canceled selection launched");
   select();History(throwing,1);Hand(0,Vector3.right*10);manager.SelectExit((IXRSelectInteractor)interactor,(IXRSelectInteractable)grab);Call(throwing,"LateUpdate");Need(!throwing.Flying,"Tracking jump launched");

   // Move/rotate the tracking origin while the palm is stationary in it.
   select();History(throwing,0);space.SetPositionAndRotation(oldPosition+Vector3.forward*2,Quaternion.Euler(0,70,0)*oldRotation);
   Hand(0,Vector3.zero);manager.SelectExit((IXRSelectInteractor)interactor,(IXRSelectInteractable)grab);Call(throwing,"LateUpdate");Need(!throwing.Flying,"Swimming/turning added launch velocity");
   space.SetPositionAndRotation(oldPosition,oldRotation);

   // Deferred release must yield to a recall or a disabled collector.
   select();History(throwing,2);Hand(0,Vector3.right*.24f);manager.SelectExit((IXRSelectInteractor)interactor,(IXRSelectInteractable)grab);throwing.StopMotion();Call(throwing,"LateUpdate");Need(!throwing.Flying,"Recall lost to queued launch");
   select();History(throwing,2);Hand(0,Vector3.right*.24f);manager.SelectExit((IXRSelectInteractor)interactor,(IXRSelectInteractable)grab);grab.enabled=false;Call(throwing,"LateUpdate");Need(!throwing.Flying,"Disabled collector launched");grab.enabled=true;

   // Isolated, real rigidbody simulation: drag, thin barrier and return to idle.
   Physics.simulationMode=SimulationMode.Script;
   go.transform.position=new Vector3(1000,1000,1000);body.position=go.transform.position;
   barrier=GameObject.CreatePrimitive(PrimitiveType.Cube);barrier.transform.position=go.transform.position+Vector3.right*.35f;barrier.transform.localScale=new Vector3(.02f,2,2);
   Physics.SyncTransforms();Call(throwing,"BeginFlight",Vector3.right*1.8f,Vector3.zero);
   float initial=body.linearVelocity.magnitude;
   for(int i=0;i<150;i++){Physics.Simulate(.02f);Call(throwing,"FixedUpdate");}
   Need(body.position.x<1000.36f,"Thrown prop crossed thin barrier");
   Need(!throwing.Flying&&body.isKinematic,"Flight did not settle");
   Need(Vector3.Distance(body.position,new Vector3(1000,1000,1000))>.03f,"Object never moved");
   UnityEngine.Object.DestroyImmediate(barrier);barrier=null;
   throwing.StopMotion();body.position=new Vector3(1000,1000,1000);Physics.SyncTransforms();
   Call(throwing,"BeginFlight",Vector3.right*1.8f,Vector3.zero);Physics.Simulate(.1f);Need(body.linearVelocity.magnitude<initial,"Water drag missing");
   var workshop=UnityEngine.Object.FindFirstObjectByType<ReefWorkshop>();
   Need(workshop.brush.GetComponent<ReefUnderwaterThrow>()&&workshop.scraper.GetComponent<ReefUnderwaterThrow>(),"Tool installation missing");
   Need(UnityEngine.Object.FindObjectsByType<ReefCoralFragment>(FindObjectsSortMode.None).All(c=>c.GetComponent<ReefUnderwaterThrow>()),"Coral installation missing");
   return "PASS: real XRI select/release, gentle placement, capped launch, regrab and second release, tracking-loss/jump/cancellation guards, rig translation/rotation exclusion, recall/disabled-collector priority; real physics drag, thin-barrier collision and sleeping. Brush/scraper/coral components installed. Synthetic hand samples; headset feel not yet tested.";
  }
  finally
  {
   if(grab&&grab.isSelected)manager.CancelInteractableSelection((IXRSelectInteractable)grab);
   throwing.StopMotion();space.SetPositionAndRotation(oldPosition,oldRotation);Physics.simulationMode=simulation;
   value.SetValue(mode,oldMode);for(int i=0;i<roots.Length;i++)roots[i].gameObject.SetActive(active[i]);interactor.enabled=oldEnabled;
   typeof(ReefHands).GetField("frame",F).SetValue(null,-1);
   if(barrier)UnityEngine.Object.DestroyImmediate(barrier);UnityEngine.Object.DestroyImmediate(go);
  }
 }
}

