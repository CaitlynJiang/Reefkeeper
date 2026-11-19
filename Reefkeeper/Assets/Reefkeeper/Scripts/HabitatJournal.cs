using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
namespace Reefkeeper
{
    public sealed class HabitatJournal : MonoBehaviour
    {
        ShipwreckHabitat habitat; DiveCardMock card; GameObject page; Font font;
        bool controls;
        int tab,revision=-1,selectedVisitor,inventoryPage; float resetArmed;
        readonly List<(RectTransform rect,Action action)> buttons=new List<(RectTransform,Action)>();
        static readonly Color Ink=new Color(.09f,.25f,.30f),Paper=new Color(.77f,.89f,.9f),Yellow=new Color(.98f,.83f,.43f);
        public void Initialize(DiveCardMock owner){card=owner;habitat=ShipwreckHabitat.Instance;var hud=FindFirstObjectByType<ReefCleaningHUD>();font=hud.boldFont?hud.boldFont:hud.font;}
        public void ShowControls(bool value){controls=value;Rebuild();var ink=transform.Find("Temporary black doodles");if(ink)ink.SetAsLastSibling();}
        public void Show(int index){tab=index;controls=false;Rebuild();var ink=transform.Find("Temporary black doodles");if(ink)ink.SetAsLastSibling();}
        void Update(){if(habitat&&habitat.Data!=null&&revision!=habitat.Revision){Rebuild();var ink=transform.Find("Temporary black doodles");if(ink)ink.SetAsLastSibling();}}
        public bool Click(Ray ray)
        {
            foreach(var b in buttons)if(b.rect&&b.rect.gameObject.activeInHierarchy&&new Plane(b.rect.forward,b.rect.position).Raycast(ray,out float d))
                if(b.rect.rect.Contains(b.rect.InverseTransformPoint(ray.GetPoint(d)))){b.action();return true;}
            return false;
        }
        void Rebuild()
        {
            if(!habitat||habitat.Data==null)return;
            revision=habitat.Revision;buttons.Clear();if(page){page.SetActive(false);Destroy(page);}
            page=new GameObject("V4 journal page",typeof(RectTransform),typeof(Image));page.transform.SetParent(transform,false);
            var rect=page.GetComponent<RectTransform>();rect.sizeDelta=new Vector2(610,294);rect.localPosition=new Vector3(0,-13,-.6f);
            page.GetComponent<Image>().material=card.OverlayMaterial;page.GetComponent<Image>().color=Paper;page.GetComponent<Image>().raycastTarget=false;
            var drawing=transform.Find("Temporary black doodles");if(drawing)drawing.SetAsLastSibling();
            if(controls)
            {
                Label("CONTROLS",new Vector2(0,113),new Vector2(560,32),23);
                string instructions=Hands.ReefInput.HandsMode
                    ? "Raise left palm: journal / Lower palm: close\nRight index: touch tabs, buttons and draw\nMove your right hand away from the card to swim\nPinch: pick up / use tools and corals"
                    : UnityEngine.XR.XRSettings.isDeviceActive
                    ? "Left Y: journal / Right B: close\nRight controller: point + trigger to select or draw\nLeft stick: swim / Right stick: turn, rise or sink\nRight A: tools"
                    : "J: journal / Esc: close / Tab: tools\nLeft mouse: select or draw / Hold RMB: look\nWASD: swim / Space, C: up, down / X: brake\nF2: desktop / XR simulator / F6: hand layout preview";
                Label(instructions,new Vector2(0,12),new Vector2(565,165),19);
                Label("Draw for fun! Ink fades after a few seconds.",new Vector2(0,-112),new Vector2(560,30),16);
            }
            else if(tab==0)
            {
                Label("YOUR SHIPWRECK GARDEN",new Vector2(0,111),new Vector2(565,32),24);
                Label("1  Grow and harvest a mature coral.\n2  CORALS: select it from your collection.\n3  Aim at a wreck spot; G / trigger / pinch to place.\n4  Mix corals to welcome new marine visitors!",new Vector2(0,25),new Vector2(560,130),19);
                Label("PC: J journal / RMB look / LMB doodle\nF2 switches desktop / XR simulator",new Vector2(0,-83),new Vector2(560,54),16);
            }
            else if(tab==1)
            {
                Label("COLLECTED CORALS",new Vector2(-110,118),new Vector2(340,30),22);
                int start=inventoryPage*6;
                for(int j=0;j<6&&start+j<habitat.species.Length;j++)
                {
                    int id=start+j;float x=-195+(j%3)*195,y=61-(j/3)*107;
                    Button("Choose "+habitat.species[id].name,new Vector2(x,y),new Vector2(180,97),()=>{if(habitat.Select(id))card.SetOpen(false);},habitat.Count(id)>0?Yellow:new Color(.66f,.79f,.8f));
                    Picture(habitat.Icon(id),new Vector2(x,y+7),new Vector2(70,70));
                    Label(habitat.Name(id)+" x"+habitat.Count(id),new Vector2(x,y-31),new Vector2(178,26),13);
                }
                Button("Previous",new Vector2(-235,-121),new Vector2(90,29),()=>{inventoryPage=0;Rebuild();});
                Button("Next",new Vector2(-130,-121),new Vector2(90,29),()=>{inventoryPage=(inventoryPage+1)%Mathf.CeilToInt(habitat.species.Length/6f);Rebuild();});
                Button(habitat.Data.testKitUsed?"Test kit claimed":"Get TEST KIT",new Vector2(135,-121),new Vector2(240,29),()=>habitat.GiveTestKit());
            }
            else if(tab==2)
            {
                bool unlocked=habitat.Data.unlocked[selectedVisitor];
                Label(unlocked?habitat.VisitorNames[selectedVisitor]:"UNDISCOVERED VISITOR",new Vector2(-95,112),new Vector2(360,32),21);
                if(unlocked)Picture(habitat.VisitorIcon(selectedVisitor),new Vector2(-110,35),new Vector2(180,110));
                else Label("?",new Vector2(-110,35),new Vector2(180,110),75);
                Label("WRECK COMBINATION\n"+habitat.Recipes[selectedVisitor],new Vector2(-95,-65),new Vector2(365,65),17);
                Label(unlocked?"Recorded! This visitor stays discovered.":"Place this combination to discover it.",new Vector2(-95,-119),new Vector2(365,28),13);
                for(int i=0;i<2;i++)
                {
                    int id=i;float y=65-i*114;Button("Visitor "+i,new Vector2(204,y),new Vector2(140,98),()=>{selectedVisitor=id;Rebuild();},selectedVisitor==i?Yellow:Color.white);
                    if(habitat.Data.unlocked[i])Picture(habitat.VisitorIcon(i),new Vector2(204,y+10),new Vector2(105,60));
                    else Label("?",new Vector2(204,y+10),new Vector2(100,50),36);
                    Label(habitat.Data.unlocked[i]?habitat.VisitorNames[i]:"Locked",new Vector2(204,y-32),new Vector2(135,24),12);
                }
            }
            else
            {
                Label("V4 PLAYTEST NOTES",new Vector2(0,112),new Vector2(560,32),23);
                Label("Five fixed spots / two discoverable visitors\nSelect an occupied spot to return its coral.\nCollection, layout and discoveries save locally.\nRecipes are game rules, not ecology claims.",new Vector2(0,24),new Vector2(560,130),19);
                Button("Cancel selected coral",new Vector2(-140,-91),new Vector2(265,38),()=>habitat.Cancel());
                Button(Time.unscaledTime<resetArmed?"Confirm RESET":"Reset V4 save",new Vector2(140,-91),new Vector2(265,38),()=>{if(Time.unscaledTime<resetArmed){habitat.ResetPrototype();resetArmed=0;}else resetArmed=Time.unscaledTime+5;Rebuild();});
            }
        }
        RectTransform Rect(string name,Vector2 at,Vector2 size){var go=new GameObject(name,typeof(RectTransform));go.transform.SetParent(page.transform,false);var r=go.GetComponent<RectTransform>();r.anchoredPosition=at;r.sizeDelta=size;return r;}
        void Label(string value,Vector2 at,Vector2 size,int pixels){var t=Rect(value,at,size).gameObject.AddComponent<Text>();t.material=card.OverlayMaterial;t.font=font;t.text=value;t.fontSize=pixels;t.color=Ink;t.alignment=TextAnchor.MiddleCenter;t.raycastTarget=false;}
        void Picture(Texture texture,Vector2 at,Vector2 size){if(!texture)return;var i=Rect("Actual asset photo",at,size).gameObject.AddComponent<RawImage>();i.material=card.OverlayMaterial;i.texture=texture;i.raycastTarget=false;}
        void Button(string label,Vector2 at,Vector2 size,Action action,Color? tint=null){var r=Rect(label,at,size);var image=r.gameObject.AddComponent<Image>();image.material=card.OverlayMaterial;image.color=tint??Yellow;image.raycastTarget=false;buttons.Add((r,action));if(!label.StartsWith("Choose ")&&!label.StartsWith("Visitor "))Label(label,at,size,14);}
    }
}
