using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Inputs;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using Reefkeeper;
using Reefkeeper.Hands;

public static class CheckSurfaceHold
{
    const BindingFlags F=BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
    static void Need(bool ok,string why) { if(!ok)throw new Exception(why); }
    static void Set(object o,string n,object v)=>o.GetType().GetField(n,F).SetValue(o,v);
    static void Sample(int i,ReefHands.State s)
    {
        ((ReefHands.State[])typeof(ReefHands).GetField("states",F).GetValue(null))[i]=s;
        typeof(ReefHands).GetField("frame",F).SetValue(null,Time.frameCount);
    }
    public static string Main()
    {
        Need(Application.isPlaying,"Run in underwater XR Play mode");
        var swim=UnityEngine.Object.FindFirstObjectByType<ReefXRSwim>();
        var hold=swim.GetComponent<ReefSurfaceHold>();
        var shop=UnityEngine.Object.FindFirstObjectByType<ReefWorkshop>();
        var space=swim.view.transform.parent;
        var mode=typeof(XRInputModalityManager).GetField("s_CurrentInputMode",F).GetValue(null);
        var value=mode.GetType().GetProperty("Value");var oldMode=value.GetValue(mode);
        var originalRig=swim.transform.position;var originalVelocity=swim.Velocity;
        var roots=(Transform[])typeof(ReefInput).GetField("hands",F).GetValue(null);
        var rootActive=new[]{roots[0].gameObject.activeSelf,roots[1].gameObject.activeSelf};
        var root=new GameObject("Surface hold test fixture");
        var block=GameObject.CreatePrimitive(PrimitiveType.Cube);block.transform.SetParent(root.transform);
        var collider=block.GetComponent<BoxCollider>();
        // Isolate collision checks from the scene without moving the real XR rig.
        block.transform.position=new Vector3(100,100,100);block.transform.localScale=Vector3.one*.2f;
        var hand=new ReefHands.State{Tracked=true,Palm=new Pose(space.InverseTransformPoint(block.transform.position),Quaternion.identity),PinchPoint=space.InverseTransformPoint(block.transform.position)};
        var other=hand;other.PinchPoint+=Vector3.right;
        Action release=()=> { hand.Pinch=hand.PinchDown=hand.Grab=false;Sample(0,hand);Sample(1,default);hold.Step(space); };
        Action pinch=()=> { hand.Pinch=hand.PinchDown=hand.Grab=true;Sample(0,hand); };
        try
        {
            value.SetValue(mode,XRInputModalityManager.InputMode.TrackedHand);
            shop.ReturnTool(shop.brush);shop.ReturnTool(shop.scraper);
            DiveCardMock.Instance.SetOpen(false);Set(DiveCardMock.Instance,"reveal",0f);Set(DiveCardMock.Instance,"waitForRelease",false);
            ReefToolMenu.Instance.SetOpen(false);Set(ReefToolMenu.Instance,"consumeClick",false);Set(ReefToolMenu.Instance,"closedFrame",-100);
            Physics.SyncTransforms();release();pinch();
            Need(hold.Step(space)&&hold.Holding,"Nearby solid did not anchor");
            Need(hold.OwnsHand(0)&&!hold.OwnsHand(1),"Wrong hand owns surface input");
            Need(!new ReefHands.Button(0,ReefHands.Button.Kind.Grab).ReadIsPerformed()&&!ReefInput.UseDown(0)&&!ReefInput.TalkDown(0),"Anchor pinch leaked into grab/use/talk");
            hand.PinchDown=false;Sample(0,hand);
            for(int n=0;n<200;n++)Need(hold.Step(space),"Held pinch released unexpectedly");
            release();Need(!hold.Holding&&!hold.OwnsHand(0),"Opening pinch did not release");
            Need(!hold.CanSwim(0,hand),"Release resumed stale extended-arm glide");
            var moved=hand;moved.Palm.position+=Vector3.right*.09f;
            Need(hold.CanSwim(0,moved),"Fresh hand movement did not rearm swimming");

            // A pinch in empty space must not auto-latch when a surface arrives later.
            block.SetActive(false);release();pinch();Need(!hold.Step(space),"Air pinch anchored");
            block.SetActive(true);Physics.SyncTransforms();hand.PinchDown=false;Sample(0,hand);
            Need(!hold.Step(space),"Held air pinch latched without a new edge");
            release();pinch();Need(hold.Step(space),"Fresh edge failed after air pinch");
            collider.enabled=false;hand.PinchDown=false;Sample(0,hand);hold.Step(space);
            Need(!hold.Holding&&hold.OwnsHand(0),"Disabled collider did not release or leaked consumed pinch");
            collider.enabled=true;release();

            collider.isTrigger=true;pinch();Need(!hold.Step(space)&&!hold.Holding,"Trigger became a solid anchor");release();collider.isTrigger=false;
            var rb=block.AddComponent<Rigidbody>();rb.isKinematic=true;pinch();Need(!hold.Step(space),"Rigidbody became an anchor");release();UnityEngine.Object.DestroyImmediate(rb);
            var item=GameObject.CreatePrimitive(PrimitiveType.Cube);item.transform.SetParent(root.transform);item.transform.position=block.transform.position;
            item.transform.localScale=Vector3.one*.05f;item.AddComponent<XRSimpleInteractable>();Physics.SyncTransforms();
            pinch();Need(!hold.Step(space),"Surface stole pinch from nearby interactable");release();UnityEngine.Object.DestroyImmediate(item);
            block.layer=4;pinch();Need(!hold.Step(space),"Water layer became anchor");release();block.layer=0;

            pinch();hold.Step(space);hand.PinchDown=false;hand.PinchPoint+=Vector3.right*.3f;Sample(0,hand);hold.Step(space);
            Need(!hold.Holding,"Hand pulling away did not release anchor");hand.PinchPoint-=Vector3.right*.3f;release();
            pinch();hold.Step(space);block.transform.position+=Vector3.right*.1f;Physics.SyncTransforms();hand.PinchDown=false;Sample(0,hand);hold.Step(space);
            Need(!hold.Holding,"Moving surface retained stale anchor");block.transform.position-=Vector3.right*.1f;Physics.SyncTransforms();release();
            pinch();hold.Step(space);Sample(0,default);hold.Step(space);Need(!hold.Holding,"Lost tracking left body anchored");
            hand.PinchDown=true;Sample(0,hand);Need(!hold.Step(space),"Tracking recovery retriggered without release");release();

            // Either hand can keep the body anchored independently.
            other=hand;Sample(1,other);hold.Step(space);pinch();other.Pinch=other.PinchDown=other.Grab=true;Sample(1,other);
            Need(hold.Step(space)&&hold.OwnsHand(0)&&hold.OwnsHand(1),"Two-handed anchor failed");
            hand.Pinch=hand.PinchDown=hand.Grab=false;Sample(0,hand);other.PinchDown=false;Sample(1,other);
            Need(hold.Step(space)&&hold.Holding&&!hold.OwnsHand(0),"One-hand release dropped other anchor");
            other.Pinch=other.Grab=false;Sample(1,other);hold.Step(space);Need(!hold.Holding,"Final hand failed to release");release();
            pinch();hold.Step(space);ReefToolMenu.Instance.SetOpen(true);hold.Step(space);Need(!hold.Holding,"Wheel failed to cancel anchor");
            ReefToolMenu.Instance.SetOpen(false);Set(ReefToolMenu.Instance,"consumeClick",false);Set(ReefToolMenu.Instance,"closedFrame",-100);release();
            pinch();hold.Step(space);typeof(ReefSurfaceHold).GetMethod("OnApplicationFocus",F).Invoke(hold,new object[]{false});Need(!hold.Holding,"Focus loss retained anchor");
            typeof(ReefSurfaceHold).GetMethod("OnApplicationFocus",F).Invoke(hold,new object[]{true});release();

            foreach(var r in roots)r.gameObject.SetActive(true);
            foreach(var tool in new[]{shop.brush,shop.scraper})
            {
                release();other=hand;Sample(1,other);hold.Step(space);pinch();
                var interactor=ReefInput.BareHandInteractor(0);
                var manager=UnityEngine.Object.FindFirstObjectByType<XRInteractionManager>();
                interactor.interactionManager=manager;
                var grab=tool.GetComponent<ReefBrushInteractable>();
                manager.SelectEnter((IXRSelectInteractor)interactor,(IXRSelectInteractable)grab);
                Need(grab.isSelected&&ReefInput.HandBusy(0),"Tool fixture selection failed");
                Need(!hold.Step(space)&&!hold.Holding,"Occupied hand also anchored");
                other.Pinch=other.PinchDown=other.Grab=true;Sample(1,other);
                Need(hold.Step(space)&&hold.OwnsHand(1)&&!hold.OwnsHand(0),"Free hand could not anchor while other held tool");
                Need(grab.isSelected&&new ReefHands.Button(0,ReefHands.Button.Kind.Grab).ReadIsPerformed(),"Anchor interfered with other hand's tool");
                other.Pinch=other.PinchDown=other.Grab=false;Sample(1,other);hold.Step(space);
                shop.ReturnTool(tool);release();
            }

            // Exercise the actual swim method: stop immediately, leave rig/head tracking
            // untouched, then require new movement before the original glide can resume.
            var handSwim=typeof(ReefXRSwim).GetMethod("HandSwim",F);
            Set(swim,"<Velocity>k__BackingField",Vector3.forward*2);
            pinch();handSwim.Invoke(swim,null);
            Need(swim.Velocity==Vector3.zero&&!swim.Gliding,"Anchor failed to stop actual swim velocity");
            Need(swim.transform.position==originalRig,"Anchoring moved the rig");
            hand.Pinch=hand.PinchDown=hand.Grab=false;Sample(0,hand);handSwim.Invoke(swim,null);
            for(int n=0;n<100;n++)handSwim.Invoke(swim,null);
            Need(swim.Velocity==Vector3.zero&&!swim.Gliding,"Stationary released hand restarted swimming");
            // Keep the test swim away from its radial boundary by using a normal palm pose.
            hand.Palm.position=swim.view.transform.localPosition+Vector3.forward*.6f;
            hand.Palm.rotation=Quaternion.identity;Sample(0,hand);
            for(int n=0;n<100;n++)handSwim.Invoke(swim,null);
            Need(swim.Gliding&&swim.Velocity.sqrMagnitude>0,"Original glide failed after deliberate new movement");
            return "PASS: nearby solid pinch, air pinch, held/release, both hands, interactable priority, trigger/rigidbody/water exclusions, input ownership, disabled/moving surface, break distance, tracking/focus/menu cancellation, actual swim stop and fresh-motion resume. Synthetic Editor test; headset test pending.";
        }
        finally
        {
            Sample(0,default);Sample(1,default);hold.ReleaseAll();
            shop.ReturnTool(shop.brush);shop.ReturnTool(shop.scraper);
            for(int i=0;i<2;i++)roots[i].gameObject.SetActive(rootActive[i]);
            UnityEngine.Object.DestroyImmediate(root);value.SetValue(mode,oldMode);
            swim.transform.position=originalRig;Set(swim,"<Velocity>k__BackingField",originalVelocity);Physics.SyncTransforms();
            typeof(ReefHands).GetField("frame",F).SetValue(null,-1);
        }
    }
}
