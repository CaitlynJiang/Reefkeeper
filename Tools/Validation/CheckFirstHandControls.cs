using System;
using System.Linq;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Reflection;
using Unity.Collections;
using UnityEngine;
using UnityEngine.XR.Hands;
using UnityEngine.XR.Hands.Processing;
using UnityEngine.XR.Hands.ProviderImplementation;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Inputs;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using Reefkeeper;
using Reefkeeper.Hands;

public static class CheckHandProgress
{
    const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    static void Require(bool ok, string message) { if (!ok) throw new Exception(message); }
    static void Field(object obj, string name, object value) => obj.GetType().GetField(name, Private).SetValue(obj,value);
    static void Joint(XRHand hand, XRHandJointID id, Vector3 p, bool tracked=true)
    {
        var joints=hand.GetRawJointArray();
        joints[id.ToIndex()]=XRHandProviderUtility.CreateJoint(hand.handedness,
            tracked?XRHandJointTrackingState.Pose:XRHandJointTrackingState.None,id,new Pose(p,Quaternion.identity));
    }
    static ReefHands.State Read(XRHand hand, ReefHands.State previous)
    {
        object[] args={previous,hand,true};
        typeof(ReefHands).GetMethod("Update",Private).Invoke(null,args);
        return (ReefHands.State)args[0];
    }
    static void SetState(int hand, ReefHands.State s)
    {
        ((ReefHands.State[])typeof(ReefHands).GetField("states",Private).GetValue(null))[hand]=s;
        typeof(ReefHands).GetField("frame",Private).SetValue(null,Time.frameCount);
    }
    public static string Main()
    {
        Require(Application.isPlaying,"Run in Play mode");
        var workshop=UnityEngine.Object.FindFirstObjectByType<ReefWorkshop>();
        var card=DiveCardMock.Instance;
        var menu=ReefPalmMenu.Instance;
        var inputMode=typeof(XRInputModalityManager).GetField("s_CurrentInputMode",Private).GetValue(null);
        var value=inputMode.GetType().GetProperty("Value");
        object previousMode=value.GetValue(inputMode);
        bool wasOpen=card.IsOpen;
        var hand=XRHandProviderUtility.CreateHand(Handedness.Left,Allocator.Temp);
        try
        {
            object boxed=hand;
            typeof(XRHand).GetProperty("isTracked").SetValue(boxed,true);
            hand=(XRHand)boxed;
            Joint(hand,XRHandJointID.Palm,Vector3.zero);
            // Gesture classification now uses each finger's articulated path, not
            // a fixed fingertip distance. Supply actual intermediate joints too.
            var proximalIds=new[]{XRHandJointID.IndexProximal,XRHandJointID.MiddleProximal,XRHandJointID.RingProximal,XRHandJointID.LittleProximal};
            float[] fingerX={.03f,0,-.02f,-.04f};
            for(int f=0;f<4;f++)
            {
                Joint(hand,proximalIds[f],new Vector3(fingerX[f],0,.04f));
                Joint(hand,proximalIds[f]+1,new Vector3(fingerX[f],0,.08f));
                Joint(hand,proximalIds[f]+2,new Vector3(fingerX[f],0,.11f));
            }
            Joint(hand,XRHandJointID.ThumbTip,new Vector3(.04f,0,.06f));
            Joint(hand,XRHandJointID.IndexTip,new Vector3(.03f,0,.14f));
            Joint(hand,XRHandJointID.MiddleTip,new Vector3(0,0,.16f));
            Joint(hand,XRHandJointID.RingTip,new Vector3(-.02f,0,.15f));
            Joint(hand,XRHandJointID.LittleTip,new Vector3(-.04f,0,.13f));
            var s=Read(hand,default);
            Require(s.Tracked&&!s.Pinch&&!s.MiddlePinch&&!s.Fist,"Open hand misclassified");
            Joint(hand,XRHandJointID.MiddleTip,new Vector3(.04f,0,.06f));
            s=Read(hand,s); Require(s.MiddlePinch&&!s.Pinch&&!s.Fist,"Middle pinch detection");
            Joint(hand,XRHandJointID.IndexTip,new Vector3(-.04f,0,.06f));
            Joint(hand,XRHandJointID.RingTip,new Vector3(-.02f,0,.03f));
            Joint(hand,XRHandJointID.LittleTip,new Vector3(-.04f,0,.03f));
            s=Read(hand,s); Require(s.MiddlePinch&&!s.Grab,"Journal gesture must not grab nearby objects");
            Joint(hand,XRHandJointID.IndexTip,new Vector3(.03f,0,.14f));
            Joint(hand,XRHandJointID.MiddleTip,new Vector3(.065f,0,.06f));
            s=Read(hand,s); Require(s.MiddlePinch,"Middle pinch hysteresis");
            Joint(hand,XRHandJointID.MiddleTip,new Vector3(0,0,.16f));
            s=Read(hand,s); Require(!s.MiddlePinch,"Middle pinch release");
            Joint(hand,XRHandJointID.IndexTip,new Vector3(.04f,0,.06f));
            s=Read(hand,s); Require(s.Pinch&&s.PinchDown&&!s.MiddlePinch,"Index pinch detection");
            Joint(hand,XRHandJointID.IndexTip,new Vector3(.065f,0,.06f));
            s=Read(hand,s); Require(s.Pinch&&!s.PinchDown,"Index pinch hysteresis");
            Joint(hand,XRHandJointID.IndexTip,new Vector3(-.03f,0,.03f));
            Joint(hand,XRHandJointID.MiddleTip,new Vector3(0,0,.03f));
            Joint(hand,XRHandJointID.RingTip,new Vector3(-.02f,0,.03f));
            Joint(hand,XRHandJointID.LittleTip,new Vector3(-.04f,0,.03f));
            s=Read(hand,s); Require(!s.Pinch&&s.PinchUp&&s.Fist&&!s.MiddlePinch,"Pinch release while fist stays closed");
            Joint(hand,XRHandJointID.Palm,Vector3.zero,false);
            s=Read(hand,s); Require(!s.Tracked&&!s.Pinch&&!s.MiddlePinch,"Missing joint should invalidate tracking");

            foreach(var rotation in new[]{Quaternion.identity,Quaternion.Euler(35,70,10),Quaternion.Euler(0,0,179)})
            foreach(float side in new[]{-1f,1f})
            {
                var palm=new Pose(new Vector3(.2f,1.3f,.4f),rotation);
                var pinch=palm.position+rotation*new Vector3(side*.025f,-.03f,.08f);
                var pose=ReefToolHandPose.GripPose(palm,pinch);
                Require(Vector3.Dot(pose.rotation*Vector3.forward,(pinch-palm.position).normalized)>.9999f,"Handle direction mismatch");
                Require(Vector3.Dot(pose.rotation*Vector3.up,palm.rotation*Vector3.up)>0,"Working face flipped");
                Require(Vector3.Distance(pose.position,palm.position+rotation*Vector3.down*.015f)<.00001f,"Grip offset must be original 1.5 cm");
                var fallback=ReefToolHandPose.GripPose(palm,palm.position);
                Require(!float.IsNaN(fallback.rotation.x),"Degenerate direction");
                var axial=ReefToolHandPose.GripPose(palm,palm.position+rotation*Vector3.up*.08f);
                Require(!float.IsNaN(axial.rotation.x),"Parallel up direction");
            }

            value.SetValue(inputMode,XRInputModalityManager.InputMode.TrackedHand);
            card.SetOpen(false);
            if(ReefToolMenu.Instance)ReefToolMenu.Instance.SetOpen(false);
            foreach(var handRoot in (Transform[])typeof(ReefInput).GetField("hands",Private).GetValue(null)) if(handRoot)handRoot.gameObject.SetActive(true);
            var left=ReefInput.BareHandInteractor(0);
            var right=ReefInput.BareHandInteractor(1);
            Require(left&&right,"Missing hand interactors");
            // Synthetic tracked-hand samples exercise the actual manager, transformers and menu.
            // These checks do not claim physical hand-tracking or comfort validation.
            foreach(var tool in new[]{workshop.brush,workshop.scraper})
            foreach(int index in new[]{0,1})
            {
                var interactor=index==0?left:right;
                interactor.gameObject.SetActive(true); interactor.enabled=true;
                var grab=tool.GetComponent<ReefBrushInteractable>();
                var state=new ReefHands.State{Tracked=true,Pinch=true,Grab=true,Palm=new Pose(new Vector3(index==0?-.2f:.2f,1.2f,.4f),Quaternion.Euler(15,25,5))};
                state.PinchPoint=state.Palm.position+state.Palm.rotation*new Vector3(.025f,-.02f,.09f);
                SetState(index,state);
                Require(grab.IsSelectableBy((IXRSelectInteractor)interactor),"Pinch should allow tool selection");
                var manager=UnityEngine.Object.FindFirstObjectByType<XRInteractionManager>();
                interactor.interactionManager=manager;
                manager.SelectEnter((IXRSelectInteractor)interactor,(IXRSelectInteractable)grab);
                Require(grab.isSelected,"Tool did not select");
                var pickup=workshop.GetComponent<ReefBrushPickup>();
                var hud=workshop.GetComponent<ReefCleaningHUD>();
                var patch=UnityEngine.Object.FindObjectsByType<RackDirtPatch>(FindObjectsSortMode.None).First(p=>p.Accepts(tool.toolKind));
                patch.ResetDirt();
                Field(pickup,"<Target>k__BackingField",patch);
                Field(pickup,"<SpotInReach>k__BackingField",true);
                Field(tool,"<InContact>k__BackingField",true);
                typeof(ReefCleaningHUD).GetMethod("LateUpdate",Private).Invoke(hud,null);
                var progress=(RectTransform)typeof(ReefCleaningHUD).GetField("progress",Private).GetValue(hud);
                Require(progress.gameObject.activeInHierarchy,"Progress hidden while correct tool contacts patch");
                Require(progress.GetComponent<Canvas>().renderMode==RenderMode.WorldSpace,"Progress is not world-space");
                Require(Mathf.Abs(progress.lossyScale.x*220-.088f)<.001f,"Wrong progress physical size");
                var palmWorld=ReefInput.HandTrackingSpace.TransformPoint(state.Palm.position);
                var expectedUI=ReefCleaningHUD.HandProgressPosition(palmWorld,pickup.View.transform.right,pickup.View.transform.position,index);
                Require(Vector3.Distance(progress.position,expectedUI)<.001f,"Progress follows wrong hand/side");
                var objective=(RectTransform)typeof(ReefCleaningHUD).GetField("objectives",Private).GetValue(hud);
                var help=(Text)typeof(ReefCleaningHUD).GetField("helpHint",Private).GetValue(hud);
                Require(!objective.gameObject.activeSelf&&!help.gameObject.activeSelf,"Ambient VR HUD still visible");
                var prompt=(Text)typeof(ReefCleaningHUD).GetField("promptLabel",Private).GetValue(hud);
                Require(!prompt.text.Contains("let go")&&!prompt.text.Contains("placeholder"),"Unwanted tool hint remains");
                Field(tool,"<InContact>k__BackingField",false);
                typeof(ReefCleaningHUD).GetMethod("LateUpdate",Private).Invoke(hud,null);
                Require(!progress.gameObject.activeSelf,"Progress remains visible away from cleaning contact");

                var transformer=grab.GetComponent<ReefToolHandPose>();
                Require(transformer,"Missing hand transformer");
                var pose=new Pose();var scale=grab.transform.localScale;
                transformer.Process(grab,XRInteractionUpdateOrder.UpdatePhase.Dynamic,ref pose,ref scale);
                var expected=ReefToolHandPose.GripPose(state.Palm,state.PinchPoint);
                Require(Vector3.Distance(pose.position,ReefInput.HandTrackingSpace.TransformPoint(expected.position))<.001f,"Tool not at palm grip");
                state.Pinch=false;state.Fist=true;state.Grab=true;SetState(index,state);
                Require(!grab.IsSelectableBy((IXRSelectInteractor)interactor),"Fist incorrectly keeps tool selected");
                typeof(XRInteractionManager).GetMethod("ClearInteractorSelection",BindingFlags.Instance|BindingFlags.NonPublic,null,new[]{typeof(IXRSelectInteractor),typeof(List<IXRInteractable>)},null).Invoke(manager,new object[]{interactor,new List<IXRInteractable>{grab}});
                Require(!grab.isSelected,"Tool did not release when pinch opened");
                workshop.ReturnTool(tool);
            }
            SetState(1,default);
            Field(menu,"armed",false);Field(menu,"holdTime",0f);Field(menu,"releaseTime",0f);
            var tick=typeof(ReefPalmMenu).GetMethod("LateUpdate",Private);
            var openHand=new ReefHands.State{Tracked=true,Palm=new Pose(Vector3.zero,Quaternion.Euler(180,0,0))};
            SetState(0,openHand);
            for(int i=0;i<200;i++)tick.Invoke(menu,null);
            Require(!card.IsOpen,"Palm up still opens journal");
            openHand.MiddlePinch=true;SetState(0,openHand);
            for(int i=0;i<200;i++)tick.Invoke(menu,null);
            Require(card.IsOpen,"Middle pinch failed to open / repeated while held");
            openHand.MiddlePinch=false;SetState(0,openHand);
            for(int i=0;i<200;i++)tick.Invoke(menu,null);
            Require(card.IsOpen,"Release should keep journal open");
            openHand.MiddlePinch=true;SetState(0,openHand);
            for(int i=0;i<200;i++)tick.Invoke(menu,null);
            Require(!card.IsOpen,"Second fresh gesture should close journal");
            return "PASS: live HUD reuses arc at 8.8cm, correct inner-side placement for brush/scraper in both hands, hidden when not cleaning, no ambient VR HUD or release hints; joint-based pinch/middle-pinch/fist distinction, hysteresis and tracking loss; mirrored/rotated/degenerate grip geometry; brush and scraper in both hands select and release through XRI even while fist remains; palm-up no longer opens journal, middle pinch toggles once and stays open on release. Synthetic joint/state inputs, not physical headset validation.";
        }
        finally
        {
            XRHandProviderUtility.DisposeHand(hand);
            card.SetOpen(wasOpen);
            value.SetValue(inputMode,previousMode);
            typeof(ReefHands).GetField("frame",Private).SetValue(null,-1);
        }
    }
}
