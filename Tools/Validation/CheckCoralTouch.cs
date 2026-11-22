using System;
using System.Reflection;
using UnityEngine;
using UnityEditor;
using UnityEngine.XR.Interaction.Toolkit.Inputs;
using Reefkeeper;
using Reefkeeper.Corals;
using Reefkeeper.Hands;

public static class CheckCoralTouch
{
    const BindingFlags F = BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    static void Need(bool ok, string why) { if (!ok) throw new Exception(why); }
    static void Set(object o, string n, object v) => o.GetType().GetField(n,F).SetValue(o,v);
    static T Get<T>(object o, string n) => (T)o.GetType().GetField(n,F).GetValue(o);
    static void Hand(int i, ReefHands.State s)
    {
        ((ReefHands.State[])typeof(ReefHands).GetField("states",F).GetValue(null))[i]=s;
        typeof(ReefHands).GetField("frame",F).SetValue(null,Time.frameCount);
    }
    public static string Main()
    {
        Need(Application.isPlaying,"Run in Play mode");
        var root=new GameObject("Coral touch validation");
        var coral=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<CoralGrowth>("Assets/Reefkeeper/Prefabs/CoralGrowth/BranchCoral.prefab"),root.transform);
        coral.transform.position=new Vector3(100,100,100);
        var socket=root.AddComponent<RackSocket>();socket.occupant=coral;
        var care=coral.gameObject.AddComponent<CoralNurture>();care.Begin(socket,coral,"BranchCoral");
        var touch=coral.GetComponent<CoralTouchReaction>();
        var step=typeof(CoralNurture).GetMethod("AdvanceCare",F);
        var contact=typeof(CoralTouchReaction).GetMethod("SampleContact",F);
        var distance=typeof(CoralTouchReaction).GetMethod("DistanceToCoral",F);
        var mode=typeof(XRInputModalityManager).GetField("s_CurrentInputMode",F).GetValue(null);
        var value=mode.GetType().GetProperty("Value");var oldMode=value.GetValue(mode);
        Action<float> advance=dt=>step.Invoke(care,new object[]{dt});
        var mesh=coral.Current.GetComponent<MeshRenderer>();
        Vector3 center=mesh.bounds.center,away=center+Vector3.one*3;
        Action<Vector3,bool,bool,float> sample=(at,tracked,free,dt)=>contact.Invoke(touch,new object[]{0,tracked,free,at,at,dt});
        Action rearm=()=> { Set(touch,"nextReaction",0f);sample(away,true,true,.16f); };
        try
        {
            Need(touch,"Planting did not install contact handling");
            sample(center,true,true,.2f);Need(!care.TouchPaused,"Placing a coral immediately counted as touching it");
            advance(1);float progress=care.Progress;Need(progress>0,"Normal growth failed");
            rearm();sample(center,true,true,.016f);
            Need(care.TouchPaused&&Mathf.Abs(care.TouchPauseRemaining-3)<.001f,"Touch did not start three-second rest");
            Need(Get<Vector3>(coral,"velocity").y<0,"Touch did not use the existing recoil spring");
            var emote=coral.GetComponent<CoralCareEmote>();Need(emote,"No existing emote renderer reused");
            var show=typeof(CoralCareEmote).GetMethod("LateUpdate",F);show.Invoke(emote,null);
            Need(Get<SpriteRenderer>(emote,"bubble").sprite==Resources.Load<Sprite>("CoralEmotes/Crying"),"Touch did not show existing crying sprite");
            advance(1.5f);Need(care.Progress==progress,"Growth advanced during touch rest");
            sample(center,true,true,1);Need(Mathf.Abs(care.TouchPauseRemaining-1.5f)<.001f,"Held contact repeatedly reset rest");
            advance(1.5f);Need(!care.TouchPaused&&care.Progress==progress,"Rest duration incorrect");
            sample(center,true,true,.5f);Need(!care.TouchPaused,"Stationary hand retriggered after rest");
            advance(.3f);Need(Mathf.Abs(care.Progress-progress-.01f)<.0001f,"Growth did not resume normally");
            rearm();sample(center,true,true,.016f);advance(3.3f);
            Need(Mathf.Abs(care.Progress-progress-.02f)<.0001f,"Expiry discarded or double-counted the unpaused fraction of a frame");
            for(int n=0;n<100;n++)show.Invoke(emote,null);
            Need(!Get<SpriteRenderer>(emote,"bubble").enabled,"Touch-only emote enabled unrelated ambient care cues");

            // Busy hands consume contact entry but never react; dropping a tool in
            // contact is not another entry. A distinct leave/re-enter is required.
            rearm();sample(center,true,false,.02f);Need(!care.TouchPaused,"Busy hand triggered distress");
            sample(center,true,true,.02f);Need(!care.TouchPaused,"Releasing a tool in contact triggered distress");
            rearm();sample(center,true,true,.02f);Need(care.TouchPaused,"New empty-hand entry failed");advance(3);
            sample(away,false,true,.2f);sample(center,true,true,.2f);Need(!care.TouchPaused,"Tracking recovery in contact retriggered distress");

            // The spring and idle sway must not change the hit volume during a touch.
            rearm();float before=(float)distance.Invoke(touch,new object[]{center+Vector3.up*.2f});
            coral.Current.localScale=new Vector3(1.2f,.55f,1.2f);coral.Current.localRotation=Quaternion.Euler(12,0,15);
            float after=(float)distance.Invoke(touch,new object[]{center+Vector3.up*.2f});
            Need(Mathf.Abs(before-after)<.0001f,"Recoil changed contact volume and could retrigger a stationary hand");
            coral.SetStage(1,false);mesh=coral.Current.GetComponent<MeshRenderer>();center=mesh.bounds.center;
            Need((float)distance.Invoke(touch,new object[]{center})<.001f,"Touch geometry did not follow growth stage");

            var moss=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<RackDirtPatch>("Assets/Reefkeeper/Prefabs/CoralGrowth/MossPad.prefab"),root.transform);
            socket.moss=moss;moss.ResetDirt();care.ReactToTouch();progress=care.Progress;advance(4);
            Need(!care.TouchPaused&&care.Paused&&care.Progress==progress,"Touch recovery cleared the independent algae block");
            moss.ApplyStroke(100,CleaningToolKind.Brush);advance(.3f);Need(care.Progress>progress,"Cleared algae failed to resume growth");
            Set(care,"<Progress>k__BackingField",1f);care.ReactToTouch();int stage=care.Stage;
            Need(!care.Ready&&!care.Fertilize()&&care.Stage==stage,"Fertilizer bypassed touch rest");advance(3);
            Need(care.Ready&&care.Fertilize()&&care.Stage==stage+1,"Fertilizing did not recover after rest");
            coral.SetStage(CoralRules.StageCount-1,false);Set(care,"<Progress>k__BackingField",1f);care.ReactToTouch();
            Need(care.FullyGrown&&care.TouchPaused,"Mature coral lost harvest state during its visual reaction");advance(3);

            // Run the real Update path with tracked-joint samples, including menu ownership.
            value.SetValue(mode,XRInputModalityManager.InputMode.TrackedHand);
            var wheel=ReefToolMenu.Instance;wheel.SetOpen(false);Set(wheel,"consumeClick",false);Set(wheel,"closedFrame",-100);
            var card=DiveCardMock.Instance;card.SetOpen(false);Set(card,"reveal",0f);Set(card,"waitForRelease",false);
            var space=ReefInput.HandTrackingSpace;Need(space,"XR tracking space missing");
            mesh=coral.Current.GetComponent<MeshRenderer>();center=mesh.bounds.center;
            var hand=new ReefHands.State{Tracked=true,Palm=new Pose(space.InverseTransformPoint(away),Quaternion.identity),IndexTip=new Pose(space.InverseTransformPoint(away),Quaternion.identity)};
            Hand(0,hand);Hand(1,default);
            Set(touch,"nextReaction",0f);var update=typeof(CoralTouchReaction).GetMethod("Update",F);
            for(int n=0;n<100;n++)update.Invoke(touch,null);
            hand.IndexTip.position=space.InverseTransformPoint(center);Hand(0,hand);update.Invoke(touch,null);
            Need(care.TouchPaused,"Real joint-input path failed to detect fingertip touch");advance(3);
            var poll=(Action)Delegate.CreateDelegate(typeof(Action),touch,update);
            for(int n=0;n<32;n++)poll();
            long allocated=GC.GetAllocatedBytesForCurrentThread();
            for(int n=0;n<1000;n++)poll();
            Need(GC.GetAllocatedBytesForCurrentThread()==allocated,"Steady contact polling allocated managed memory");
            wheel.SetOpen(true);update.Invoke(touch,null);wheel.SetOpen(false);Set(wheel,"consumeClick",false);Set(wheel,"closedFrame",-100);
            update.Invoke(touch,null);Need(!care.TouchPaused,"Closing a menu in contact caused a poke");
            foreach(var prefab in UnityEngine.Object.FindFirstObjectByType<CoralShop>().stock)
            {
                var sampleCoral=UnityEngine.Object.Instantiate(prefab,root.transform);
                var sampleCare=sampleCoral.gameObject.AddComponent<CoralNurture>();sampleCare.Begin(socket,sampleCoral,prefab.name);
                var sampleTouch=sampleCoral.GetComponent<CoralTouchReaction>();
                for(int s=0;s<sampleCoral.StageCount;s++)
                {
                    sampleCoral.SetStage(s,false);
                    foreach(var renderer in sampleCoral.Current.GetComponentsInChildren<MeshRenderer>())
                        Need((float)distance.Invoke(sampleTouch,new object[]{renderer.bounds.center})<.025f,prefab.name+" stage "+s+" has no touch geometry");
                }
                UnityEngine.Object.DestroyImmediate(sampleCoral.gameObject);
            }
            return "PASS: real coral prefab and existing Crying sprite; recoil, 3-second pause/resume, bounded repeated touches, no held-contact retriggers, busy/tracking/menu exclusions, stable stage geometry, algae independence, fertilizer and mature-harvest state, actual tracked-fingertip Update route. Synthetic Editor inputs; headset feel pending.";
        }
        finally
        {
            value.SetValue(mode,oldMode);Hand(0,default);Hand(1,default);
            typeof(ReefHands).GetField("frame",F).SetValue(null,-1);
            UnityEngine.Object.DestroyImmediate(root);
        }
    }
}
