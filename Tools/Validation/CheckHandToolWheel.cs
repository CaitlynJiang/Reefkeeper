using System;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Inputs;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using Reefkeeper;
using Reefkeeper.Hands;

public static class CheckHandToolWheel
{
    const BindingFlags F = BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic;
    static void Need(bool ok, string why) { if (!ok) throw new Exception(why); }
    static void Set(object obj, string name, object value) => obj.GetType().GetField(name,F).SetValue(obj,value);
    static T Get<T>(object obj, string name) => (T)obj.GetType().GetField(name,F).GetValue(obj);
    static void Sample(int i, ReefHands.State state)
    {
        ((ReefHands.State[])typeof(ReefHands).GetField("states",F).GetValue(null))[i]=state;
        typeof(ReefHands).GetField("frame",F).SetValue(null,Time.frameCount);
    }
    public static string Main()
    {
        Need(Application.isPlaying,"Enter Play and finish the opening transition first");
        var shop=UnityEngine.Object.FindFirstObjectByType<ReefWorkshop>();
        var wheel=ReefToolMenu.Instance; var card=DiveCardMock.Instance;
        var mode=typeof(XRInputModalityManager).GetField("s_CurrentInputMode",F).GetValue(null);
        var value=mode.GetType().GetProperty("Value"); var oldMode=value.GetValue(mode);
        var roots=(Transform[])typeof(ReefInput).GetField("hands",F).GetValue(null);
        var active=roots.Select(t=>t.gameObject.activeSelf).ToArray();
        var canvas=wheel.Canvas; var renderMode=canvas.renderMode;
        var oldPos=canvas.transform.position;var oldRot=canvas.transform.rotation;var oldScale=canvas.transform.localScale;
        var oldSize=((RectTransform)canvas.transform).sizeDelta; var oldCamera=canvas.worldCamera;
        var owned=Get<bool[]>(wheel,"owned");var oldOwned=(bool[])owned.Clone();
        bool oldAvailable=card.Available,oldCardOpen=card.IsOpen,oldWheelOpen=wheel.IsOpen;
        Transform pointer=null;Vector3 pointerPos=default;Quaternion pointerRot=default;
        var gesture=typeof(ReefToolMenu).GetMethod("UpdateHandGesture",F);
        var updatePointer=typeof(ReefToolMenu).GetMethod("UpdatePointer",F);
        var click=typeof(ReefToolMenu).GetMethod("HandlePointer",F);
        Action<float> tick=dt=>gesture.Invoke(wheel,new object[]{dt});
        var right=new ReefHands.State{Tracked=true,Palm=new Pose(new Vector3(.2f,1.3f,.3f),Quaternion.identity)};
        try
        {
            value.SetValue(mode,XRInputModalityManager.InputMode.TrackedHand);
            foreach(var root in roots) root.gameObject.SetActive(true);
            shop.ReturnTool(shop.brush);shop.ReturnTool(shop.scraper);
            card.SetOpen(false);card.Available=true;wheel.SetOpen(false);
            Sample(0,default);Sample(1,right);tick(.16f);
            right.MiddlePinch=true;Sample(1,right);tick(.49f);Need(!wheel.IsOpen,"Opens before half-second hold");
            tick(.02f);Need(wheel.IsOpen,"Right middle pinch failed to open");
            tick(2);Need(wheel.IsOpen,"Held gesture retriggered");
            right.MiddlePinch=false;Sample(1,right);tick(.16f);Need(wheel.IsOpen,"Release closed the wheel");
            right.MiddlePinch=true;Sample(1,right);tick(.51f);Need(!wheel.IsOpen,"Fresh gesture failed to close");
            right.MiddlePinch=false;Sample(1,right);tick(.16f);
            card.SetOpen(true);
            right.MiddlePinch=true;Sample(1,right);tick(.51f);
            Need(wheel.IsOpen&&!card.IsOpen,"Opening wheel did not close journal");
            card.SetOpen(true);Need(!wheel.IsOpen&&card.IsOpen,"Opening journal did not close wheel");
            card.SetOpen(false);Set(card,"reveal",0f);Set(card,"waitForRelease",false);
            right.MiddlePinch=false;Sample(1,right);tick(.16f);
            right.MiddlePinch=true;Sample(1,right);tick(.51f);
            right.Tracked=false;Sample(1,right);tick(.02f);Need(!wheel.IsOpen,"Tracking loss left hand-opened wheel active");
            right.Tracked=true;Sample(1,right);tick(1);Need(!wheel.IsOpen,"Tracking regain with held gesture reopened wheel");
            right.MiddlePinch=false;Sample(1,right);tick(.16f);
            right.MiddlePinch=true;Sample(1,right);tick(.51f);Need(wheel.IsOpen,"Could not reopen after tracked release");

            // Exercise actual pointer projection and pinch click, not TryEquip directly.
            Set(wheel,"expansion",1f);
            canvas.renderMode=RenderMode.WorldSpace;canvas.worldCamera=shop.GetComponent<ReefBrushPickup>().View;
            ((RectTransform)canvas.transform).sizeDelta=new Vector2(1280,720);
            canvas.transform.SetPositionAndRotation(canvas.worldCamera.transform.position+canvas.worldCamera.transform.forward*1.2f,canvas.worldCamera.transform.rotation);
            canvas.transform.localScale=Vector3.one*.001f;
            Canvas.ForceUpdateCanvases();
            pointer=ReefInput.RayOrigin(1);Need(pointer,"Right hand ray unavailable");pointerPos=pointer.position;pointerRot=pointer.rotation;
            foreach(int index in new[]{0,1})
            {
                wheel.SetOpen(true);Set(wheel,"expansion",1f);
                right.Pinch=right.PinchDown=right.MiddlePinch=false;right.Grab=false;Sample(1,right);
                typeof(ReefToolMenu).GetMethod("LateUpdate",F).Invoke(wheel,null);
                var target=wheel.SlotRect(index).position;
                pointer.SetPositionAndRotation(canvas.worldCamera.transform.position,Quaternion.LookRotation(target-canvas.worldCamera.transform.position,canvas.transform.up));
                updatePointer.Invoke(wheel,null);Need(Get<bool>(wheel,"pointerValid"),"Hand ray did not project onto wheel");
                click.Invoke(wheel,null); // Arm only after index and middle are released.
                owned[index]=false;right.Pinch=right.PinchDown=right.Grab=true;Sample(1,right);
                click.Invoke(wheel,null);Need(wheel.IsOpen,"Unowned tool closed wheel");
                owned[index]=true;
                click.Invoke(wheel,null);
                var tool=index==0?shop.brush:shop.scraper;
                var grab=tool.GetComponent<ReefBrushInteractable>();
                Need(!wheel.IsOpen&&grab.isSelected,"Hand pinch did not equip selected tool");
                Need(ReferenceEquals(grab.interactorsSelecting[0],ReefInput.BareHandInteractor(1)),"Tool equipped to wrong interactor");
                Need(!Get<bool>(wheel,"consumeClick"),"Tool hold kept world input blocked");
                right.Pinch=right.PinchDown=right.Grab=false;Sample(1,right);
                Need(!grab.IsSelectableBy((IXRSelectInteractor)ReefInput.BareHandInteractor(1)),"Release still selects tool");
                shop.ReturnTool(tool);
            }
            Sample(1,default);Need(wheel.RightHand==null,"Lost hand fell back to stale controller pointer");
            return "PASS: half-second right-middle toggle, held/release debounce, menu/journal exclusion, tracking loss/recovery, actual hand-ray projection, locked slots, pinch equip for brush/scraper and release eligibility; no swim tuning changed. Synthetic inputs, headset check pending.";
        }
        finally
        {
            shop.ReturnTool(shop.brush);shop.ReturnTool(shop.scraper);wheel.SetOpen(false);card.SetOpen(false);
            if(pointer)pointer.SetPositionAndRotation(pointerPos,pointerRot);
            canvas.renderMode=renderMode;canvas.worldCamera=oldCamera;canvas.transform.SetPositionAndRotation(oldPos,oldRot);canvas.transform.localScale=oldScale;((RectTransform)canvas.transform).sizeDelta=oldSize;
            Array.Copy(oldOwned,owned,owned.Length);card.Available=oldAvailable;
            value.SetValue(mode,oldMode);for(int i=0;i<roots.Length;i++)roots[i].gameObject.SetActive(active[i]);
            typeof(ReefHands).GetField("frame",F).SetValue(null,-1);
            card.SetOpen(oldCardOpen);wheel.SetOpen(oldWheelOpen);
        }
    }
}
