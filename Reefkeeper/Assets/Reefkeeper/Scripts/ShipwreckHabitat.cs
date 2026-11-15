using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Reefkeeper.Corals;
using Reefkeeper.Hands;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Reefkeeper
{
    /// <summary>V4-only fixed-slot habitat MVP. Inventory is reserved only when a placement succeeds.</summary>
    [DefaultExecutionOrder(130)]
    public sealed class ShipwreckHabitat : MonoBehaviour
    {
        public static ShipwreckHabitat Instance { get; private set; }
        public CoralGrowth[] species;
        public Transform[] slots;
        public GameObject[] visitors;
        public Transform visitorCenter;
        public bool persist=true;
        public string saveKey="Reefkeeper.V4.Habitat.1";
        [Serializable] public class State
        {
            public int[] inventory;
            public int[] placed=new[]{-1,-1,-1,-1,-1};
            public bool[] unlocked=new bool[2];
            public bool testKitUsed;
        }
        public State Data { get; private set; }
        public int Selected { get; private set; }=-1;
        public string Prompt { get; private set; }="";
        public string Notice { get; private set; }="";
        public int Revision { get; private set; }
        public int UnlockedCount => Data.unlocked.Count(x=>x);
        public readonly string[] VisitorNames={"Reef shoal","Striped reef fish"};
        public readonly string[] Recipes={"2 Branch Coral + 1 Mushroom Coral","2 Lemon Tube Sponge + 1 Mushroom Coral"};
        readonly List<GameObject> placedVisuals=new List<GameObject>();
        readonly List<GameObject> visitorGroups=new List<GameObject>();
        readonly RaycastHit[] hits=new RaycastHit[80];
        ReefWorkshop workshop;
        ReefCoralPickup pickup;
        CoralGrowth ghost;
        Material ghostMaterial;
        float noticeUntil;
        int hovered=-1;
        bool wasLeft;
        public int Count(int i)=>i>=0&&i<Data.inventory.Length?Data.inventory[i]:0;
        public int Id(string name)=>Array.FindIndex(species,p=>p&&p.name==name);
        public string Name(int i)=>CoralRules.DisplayName(species[i].name);
        public Texture2D Icon(int i)=>Resources.Load<Texture2D>("DiveCards/CoralStickers/"+species[i].name+"_S3");
        public Texture2D VisitorIcon(int i)=>Resources.Load<Texture2D>("DiveCards/Marine/Visitor"+i);
        void Awake()
        {
            Instance=this;workshop=FindFirstObjectByType<ReefWorkshop>();pickup=workshop.GetComponent<ReefCoralPickup>();
#if UNITY_EDITOR
            if(!UnityEngine.XR.XRSettings.isDeviceActive){workshop.xrGroup.SetActive(false);workshop.desktopPlayer.SetActive(true);}
#endif
        }
        IEnumerator Start()
        {
            Load();
            ghostMaterial=new Material(Shader.Find("Universal Render Pipeline/Lit"));
            ghostMaterial.SetColor("_BaseColor",new Color(.4f,.95f,.8f,.38f));ghostMaterial.SetFloat("_Surface",1);
            ghostMaterial.SetFloat("_SrcBlend",(float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            ghostMaterial.SetFloat("_DstBlend",(float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            ghostMaterial.SetFloat("_ZWrite",0);ghostMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");ghostMaterial.renderQueue=3000;
            Rebuild();yield return null;
            // This testing scene exposes the journal immediately; the V3 tutorial remains unchanged.
            if(DiveCardMock.Instance)DiveCardMock.Instance.Available=true;
            Say("V4: J > CORALS for your collection and test kit");
        }
        public void Load()
        {
            Data=null;
            if(persist&&PlayerPrefs.HasKey(saveKey))try{Data=JsonUtility.FromJson<State>(PlayerPrefs.GetString(saveKey));}catch(ArgumentException){}
            if(Data==null||Data.inventory==null||Data.inventory.Length!=species.Length||Data.placed==null||Data.placed.Length!=5||Data.unlocked==null||Data.unlocked.Length!=2)
                Data=new State{inventory=new int[species.Length]};
            for(int i=0;i<Data.inventory.Length;i++)Data.inventory[i]=Mathf.Clamp(Data.inventory[i],0,999);
            for(int i=0;i<5;i++)if(Data.placed[i]<-1||Data.placed[i]>=species.Length)Data.placed[i]=-1;
        }
        void Changed(){Revision++;if(persist){PlayerPrefs.SetString(saveKey,JsonUtility.ToJson(Data));PlayerPrefs.Save();}}
        public void ReloadSave(){Cancel();Load();Revision++;Rebuild();}
        public void Say(string text){Notice=text;noticeUntil=Time.unscaledTime+5;}
        public bool Harvest(CoralNurture nurture)
        {
            if(!nurture||!nurture.FullyGrown||nurture.Paused)return false;
            int id=Id(nurture.species);if(id<0)return false;
            // Do not detach a mushroom platform while another coral depends on it.
            if(nurture.GetComponentsInChildren<RackSocket>().Any(s=>s.occupant)) {Say("Move dependent corals first");return false;}
            if(!FindFirstObjectByType<CoralPlanting>().Harvest(nurture))return false;
            Data.inventory[id]++;Changed();Say(Name(id)+" collected! J > CORALS to decorate");return true;
        }
        public bool Select(int id)
        {
            if(id<0||id>=species.Length||Count(id)==0)return false;
            workshop.ReturnBrush();pickup.ClearHeld();Selected=id;hovered=-1;BuildGhost();
            Say("Aim at a glowing shipwreck spot; G / trigger / pinch to place");return true;
        }
        public void Cancel(){Selected=-1;hovered=-1;if(ghost)Destroy(ghost.gameObject);ghost=null;}
        public bool Place(int slot)
        {
            if(slot<0||slot>=5||Selected<0||Count(Selected)<1||Data.placed[slot]>=0)return false;
            Data.inventory[Selected]--;Data.placed[slot]=Selected;Cancel();Evaluate();Changed();Rebuild();return true;
        }
        public bool Retrieve(int slot)
        {
            if(slot<0||slot>=5||Data.placed[slot]<0)return false;
            int id=Data.placed[slot];Data.inventory[id]++;Data.placed[slot]=-1;Changed();Rebuild();Say(Name(id)+" returned to collection");return true;
        }
        public bool Matches(int visitor)
        {
            int a=Id(visitor==0?"BranchCoral":"TubeSponge_Lemon"),b=Id("DiscCoral");
            return a>=0&&b>=0&&Data.placed.Count(x=>x==a)>=2&&Data.placed.Count(x=>x==b)>=1;
        }
        void Evaluate()
        {
            for(int i=0;i<2;i++)if(!Data.unlocked[i]&&Matches(i)){Data.unlocked[i]=true;Say("Discovered: "+VisitorNames[i]+"! J > MARINE");}
        }
        public bool GiveTestKit()
        {
            if(Data.testKitUsed)return false;
            Data.inventory[Id("BranchCoral")]+=2;Data.inventory[Id("TubeSponge_Lemon")]+=2;Data.inventory[Id("DiscCoral")]++;
            Data.testKitUsed=true;Changed();Say("Test kit: 5 mature corals added to collection");return true;
        }
        public void ResetPrototype()
        {
            Cancel();Data=new State{inventory=new int[species.Length]};Changed();Rebuild();Say("V4 collection and discoveries reset");
        }
        void Update()
        {
            if(Data==null)return;
            if(Time.unscaledTime>noticeUntil)Notice="";
            Prompt="";hovered=-1;
            if(ReefToolMenu.BlocksWorldInput){if(ghost)ghost.gameObject.SetActive(false);return;}
            bool left=Mouse.current?.leftButton.isPressed??false;
            bool center=pickup.CenterAimMode;
            if(center&&Camera.main)
                Aim(new Ray(Camera.main.transform.position,Camera.main.transform.forward),Keyboard.current?.gKey.wasPressedThisFrame==true,left&&!wasLeft);
            else for(int i=0;i<2;i++)
            {
                var origin=ReefInput.RayOrigin(i);if(!origin)continue;
                if(Aim(new Ray(origin.position,origin.forward),ReefInput.UseDown(i),ReefInput.UseDown(i)))break;
            }
            wasLeft=left;
            if(Selected>=0)
            {
                if(hovered<0)Prompt=Name(Selected)+": aim at a wreck spot (Esc cancels)";
                if(Keyboard.current?.escapeKey.wasPressedThisFrame==true)Cancel();
                else if(ghost)
                {
                    ghost.gameObject.SetActive(hovered>=0&&Data.placed[hovered]<0);
                    if(hovered>=0)Seat(ghost,slots[hovered]);
                }
            }
        }
        bool Aim(Ray ray,bool place,bool use)
        {
            int count=Physics.RaycastNonAlloc(ray,hits,5,~0,QueryTriggerInteraction.Collide);RaycastHit best=default;float d=float.MaxValue;
            for(int i=0;i<count;i++)
            {
                var c=hits[i].collider;
                if(c.transform.IsChildOf(workshop.desktopPlayer.transform)||c.transform.IsChildOf(workshop.xrGroup.transform))continue;
                if(c.GetComponentInParent<ReefBrush>()||c.GetComponentInParent<ReefCoralFragment>())continue;
                if(c.isTrigger&&!c.GetComponentInParent<HabitatSpot>()&&!c.GetComponentInParent<CoralNurture>())continue;
                if(hits[i].distance<d){best=hits[i];d=best.distance;}
            }
            if(!best.collider)return false;
            var spot=best.collider.GetComponentInParent<HabitatSpot>();
            if(spot)
            {
                hovered=spot.index;int occupied=Data.placed[hovered];
                string action=pickup.CenterAimMode?"G":ReefInput.HandsMode?"Pinch":"Trigger";
                Prompt=occupied>=0?action+": return coral":Selected>=0?action+": place "+Name(Selected):"J > CORALS: choose a coral";
                if(place){if(occupied>=0)Retrieve(hovered);else Place(hovered);}return true;
            }
            var nurture=best.collider.GetComponentInParent<CoralNurture>();
            if(!nurture){var socket=best.collider.GetComponentInParent<RackSocket>();if(socket&&socket.occupant)nurture=socket.occupant.GetComponent<CoralNurture>();}
            if(nurture&&nurture.FullyGrown){Prompt="Click / trigger: harvest "+nurture.DisplayName;if(use)Harvest(nurture);return true;}
            return false;
        }
        void BuildGhost()
        {
            if(ghost)Destroy(ghost.gameObject);ghost=MakeCoral(Selected,transform);
            foreach(var r in ghost.GetComponentsInChildren<Renderer>())r.sharedMaterials=Enumerable.Repeat(ghostMaterial,r.sharedMaterials.Length).ToArray();
            ghost.gameObject.SetActive(false);
        }
        CoralGrowth MakeCoral(int id,Transform parent)
        {
            var coral=Instantiate(species[id],parent);coral.name=species[id].name+" habitat display";coral.SetStage(Mathf.Min(2,coral.stages.Length-1),false);coral.enabled=false;
            foreach(var c in coral.GetComponentsInChildren<Collider>(true))Destroy(c);
            foreach(var socket in coral.GetComponentsInChildren<RackSocket>(true))Destroy(socket);
            return coral;
        }
        static void Seat(CoralGrowth coral,Transform spot)
        {
            coral.transform.SetPositionAndRotation(spot.position,spot.rotation);coral.transform.localScale=Vector3.one;
            var renderers=coral.Current.GetComponentsInChildren<Renderer>();if(renderers.Length==0)return;
            var bounds=renderers[0].bounds;foreach(var r in renderers)bounds.Encapsulate(r.bounds);
            coral.transform.localScale*=.65f/Mathf.Max(bounds.size.x,bounds.size.y,bounds.size.z);
            bounds=renderers[0].bounds;foreach(var r in renderers)bounds.Encapsulate(r.bounds);
            coral.transform.position+=Vector3.up*(spot.position.y-bounds.min.y+.03f);
        }
        void Rebuild()
        {
            foreach(var go in placedVisuals)if(go)Destroy(go);placedVisuals.Clear();
            for(int i=0;i<5;i++)if(Data.placed[i]>=0){var coral=MakeCoral(Data.placed[i],slots[i]);Seat(coral,slots[i]);placedVisuals.Add(coral.gameObject);}
            foreach(var go in visitorGroups)if(go)Destroy(go);visitorGroups.Clear();
            for(int i=0;i<2;i++)if(Data.unlocked[i])
            {
                var group=new GameObject(VisitorNames[i]+" habitat visitors");group.transform.SetParent(transform);group.transform.position=visitorCenter.position+Vector3.up*(.7f+i*.8f);
                var school=group.AddComponent<ReefFishSchool>();school.orbit=new Vector2(2.6f+i,1.7f+i*.3f);school.fish=new Transform[3];
                for(int j=0;j<3;j++){var fish=Instantiate(visitors[i],group.transform);fish.transform.localScale=Vector3.one*.38f;school.fish[j]=fish.transform;}
                visitorGroups.Add(group);
            }
        }
        void OnDestroy(){if(Instance==this)Instance=null;if(ghostMaterial)Destroy(ghostMaterial);}
    }
}
