#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Reefkeeper.Corals;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace Reefkeeper
{
    /// <summary>Opt-in V4 integration checks. Uses a separate save key and restores all input settings.</summary>
    public sealed class ReefV4SmokeCheck : MonoBehaviour
    {
        readonly List<string> checks=new List<string>();
        readonly List<InputDevice> physical=new List<InputDevice>();
        ShipwreckHabitat habitat;InputSettings original,testSettings;Mouse mouse;Keyboard keyboard;
        string originalKey;bool originalPersist;const string TestKey="Reefkeeper.V4.AutomatedCheck";
        string output;
        public static void Run(){new GameObject("V4 integration check").AddComponent<ReefV4SmokeCheck>();}
        IEnumerator Start()
        {
            output=Path.GetFullPath("../artifacts/v4-check");Directory.CreateDirectory(output);
            var routine=Check();Exception failure=null;
            while(true)
            {
                object step=null;bool more=false;
                try{more=routine.MoveNext();if(more)step=routine.Current;}catch(Exception e){failure=e;}
                if(failure!=null||!more)break;yield return step;
            }
            File.WriteAllText(output+"/result.txt",(failure==null?"PASS: "+checks.Count+" checks":"FAIL: "+failure)+"\n"+string.Join("\n",checks));
            if(failure!=null)Debug.LogException(failure);
            EditorApplication.isPlaying=false;
        }
        void Require(bool value,string name){if(!value)throw new Exception(name);checks.Add(name);}
        IEnumerator Check()
        {
            yield return new WaitForSeconds(.5f);
            habitat=ShipwreckHabitat.Instance;Require(habitat&&habitat.slots.Length==5,"Five authored habitat slots in the V4 scene");
            originalKey=habitat.saveKey;originalPersist=habitat.persist;habitat.saveKey=TestKey;habitat.persist=true;PlayerPrefs.DeleteKey(TestKey);habitat.ReloadSave();
            original=InputSystem.settings;testSettings=Instantiate(original);InputSystem.settings=testSettings;
            testSettings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;testSettings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            foreach(var d in InputSystem.devices.ToArray())if(d.enabled&&(d is Mouse||d is Keyboard)){physical.Add(d);InputSystem.DisableDevice(d);}
            mouse=InputSystem.AddDevice<Mouse>("V4 test mouse");keyboard=InputSystem.AddDevice<Keyboard>("V4 test keyboard");mouse.MakeCurrent();keyboard.MakeCurrent();
            var w=FindFirstObjectByType<ReefWorkshop>();w.xrGroup.SetActive(false);w.desktopPlayer.SetActive(true);
            var motor=w.desktopPlayer.GetComponent<DiveMotor>();motor.Automated=true;
            var card=DiveCardMock.Instance;
            Require(habitat.UnlockedCount==0&&!habitat.Select(1),"New profile is locked and empty items cannot be selected");
            card.SetOpen(true);card.SelectTab(1);yield return new WaitForSeconds(.65f);
            var kit=card.GetComponentsInChildren<RectTransform>().First(t=>t.name=="Get TEST KIT");
            var kitPosition=kit.position;ClickAt(kitPosition,true);yield return null;yield return null;ClickAt(kitPosition,false);yield return null;
            Require(habitat.Data.testKitUsed&&habitat.Data.inventory.Sum()==5,"Mouse click on journal grants the labelled test kit");
            Require(!habitat.GiveTestKit()&&habitat.Data.inventory.Sum()==5,"Test kit cannot duplicate inventory");
            int branch=habitat.Id("BranchCoral"),disc=habitat.Id("DiscCoral"),lemon=habitat.Id("TubeSponge_Lemon");
            var choose=card.GetComponentsInChildren<RectTransform>().First(t=>t.name=="Choose BranchCoral");
            var choosePosition=choose.position;ClickAt(choosePosition,true);yield return null;yield return null;ClickAt(choosePosition,false);yield return new WaitForSeconds(.6f);
            Require(habitat.Selected==branch&&!card.IsOpen,"Collection selection equips coral and closes journal");
            Require(habitat.Data.inventory.Sum()==5,"Selecting reserves no inventory until placement");
            var cam=Camera.main;cam.transform.position=habitat.slots[0].position+new Vector3(0,1.3f,-1.8f);cam.transform.LookAt(habitat.slots[0].position+Vector3.up*.2f);
            yield return null;InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.G));yield return null;yield return null;InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return null;
            Require(habitat.Data.placed[0]==branch&&habitat.Count(branch)==1,"Aimed G input snaps a coral into a real wreck slot");
            Require(habitat.UnlockedCount==0,"One coral does not unlock a visitor");
            habitat.Select(branch);Require(!habitat.Place(0)&&habitat.Count(branch)==1,"Occupied slot rejects replacement without consuming stock");
            Require(!habitat.Place(-1)&&!habitat.Place(5),"Out-of-range placements are rejected");
            Require(habitat.Place(1),"Second branch coral places");Require(habitat.UnlockedCount==0,"Incomplete recipe remains locked");
            habitat.Select(disc);Require(habitat.Place(2),"Mushroom coral places");
            Require(habitat.Data.unlocked[0]&&!habitat.Data.unlocked[1],"First composition unlocks only its matching visitor");
            yield return null;Require(habitat.GetComponentsInChildren<ReefFishSchool>().Length==1,"Unlocked visitor creates one real animated fish school");
            habitat.Select(lemon);habitat.Place(3);habitat.Select(lemon);habitat.Place(4);yield return null;
            Require(habitat.UnlockedCount==2,"Distinct second composition unlocks the second visitor");
            Require(habitat.GetComponentsInChildren<ReefFishSchool>().Length==2,"Both unlocked visitor schools are present without duplicates");
            Require(habitat.Data.inventory.Sum()+habitat.Data.placed.Count(x=>x>=0)==5,"Inventory is conserved through placements");
            Require(habitat.Retrieve(2)&&!habitat.Retrieve(2),"Taking back a coral returns it exactly once");
            Require(habitat.Count(disc)==1&&habitat.UnlockedCount==2,"Discoveries persist after rearranging the habitat");
            habitat.Select(disc);habitat.Place(2);habitat.ReloadSave();yield return null;
            Require(habitat.UnlockedCount==2&&habitat.Data.placed.Count(x=>x>=0)==5&&habitat.Data.testKitUsed,"Local save reload preserves layout, inventory, discoveries and kit state");
            Require(habitat.VisitorIcon(0)&&habitat.VisitorIcon(1),"Encyclopedia uses captures of both real visitor models");
            cam.transform.position=new Vector3(2.3f,1.4f,6.4f);cam.transform.rotation=Quaternion.Euler(16,0,0);
            yield return new WaitForEndOfFrame();Save("decorated-wreck.png");card.SetOpen(true);card.SelectTab(2);yield return new WaitForSeconds(.65f);yield return new WaitForEndOfFrame();Save("unlocked-encyclopedia.png");
            card.SetOpen(false);yield return new WaitForSeconds(.6f);
            var planting=FindFirstObjectByType<CoralPlanting>();var pickup=w.GetComponent<ReefCoralPickup>();var shop=FindFirstObjectByType<CoralShop>();
            var socket=FindObjectsByType<RackSocket>(FindObjectsSortMode.None).First(s=>s.moss&&s.moss.surfaceKind==ReefSurfaceKind.Algae&&s.IsFree&&s.grade==RackSocket.Grade.B);
            socket.moss.ApplyStroke(1000);var coral=Instantiate(habitat.species[branch]);coral.name="BranchCoral";
            var fragment=CoralSeating.MakePickable(coral,pickup,shop.outline,pickup.hand.interactionManager);pickup.Give(fragment);yield return null;
            Require(pickup.Held==fragment,"Existing pickup flow holds a nursery coral");
            var nurture=planting.Plant(socket);Require(nurture!=null,"Normal nursery planting still works");
            Require(!habitat.Harvest(nurture),"Immature coral cannot be harvested");
            nurture.secondsPerStage=.01f;nurture.mossReturnSeconds=new Vector2(100,100);
            yield return new WaitForSeconds(.1f);Require(planting.Fertilize(nurture),"First fertilization advances nursery growth");
            yield return new WaitForSeconds(.1f);Require(planting.Fertilize(nurture),"Second fertilization reaches mature stage");
            yield return new WaitForSeconds(.1f);int before=habitat.Count(branch);
            Require(habitat.Harvest(nurture)&&habitat.Count(branch)==before+1&&socket.IsFree,"Mature harvest adds one collected coral and frees the nursery socket");
            Require(!habitat.Harvest(nurture)&&habitat.Count(branch)==before+1,"A harvested coral cannot be harvested twice");
            habitat.ResetPrototype();Require(habitat.UnlockedCount==0&&habitat.Data.inventory.Sum()==0&&habitat.Data.placed.All(x=>x==-1)&&!habitat.Data.testKitUsed,"Explicit prototype reset clears all V4 progress");
            habitat.ReloadSave();Require(habitat.UnlockedCount==0,"Reset persists to the isolated test profile");
        }
        void ClickAt(Vector3 point,bool down){InputSystem.QueueStateEvent(mouse,new MouseState{position=Camera.main.WorldToScreenPoint(point),buttons=(ushort)(down?1:0)});}
        void Save(string name){var t=ScreenCapture.CaptureScreenshotAsTexture();File.WriteAllBytes(output+"/"+name,t.EncodeToPNG());Destroy(t);}
        void OnDestroy()
        {
            if(mouse!=null&&mouse.added)InputSystem.RemoveDevice(mouse);if(keyboard!=null&&keyboard.added)InputSystem.RemoveDevice(keyboard);
            foreach(var d in physical)if(d.added)InputSystem.EnableDevice(d);if(original)InputSystem.settings=original;if(testSettings)Destroy(testSettings);
            if(habitat&&originalKey!=null){habitat.saveKey=originalKey;habitat.persist=originalPersist;}
            PlayerPrefs.DeleteKey(TestKey);PlayerPrefs.Save();
        }
    }
}
#endif
