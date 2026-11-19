using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Reefkeeper
{
    /// <summary>Depth-independent UI journal. Left hand carries it; right hand operates it.</summary>
    [DefaultExecutionOrder(-32600)]
    public sealed class DiveCardMock : MonoBehaviour
    {
        public bool IsOpen { get; private set; }
        public Transform Cards => cards;
        public Transform Tab(int index) => tabs[index];
        public static DiveCardMock Instance { get; private set; }
        // Journal owns world interactions while open/closing; locomotion remains independent.
        public static bool BlocksControls => Instance && Instance.isActiveAndEnabled && (Instance.IsOpen || Instance.reveal>0 || Instance.waitForRelease);
        public bool MouseLookActive => IsOpen && reveal>.98f && Camera.main && Camera.main.GetComponentInParent<DiveMotor>() && (Mouse.current?.rightButton.isPressed??false);
        [Range(.6f,1.6f)] public float previewScale=1.15f;
        public bool openOnStart;
        /// <summary>Whether J / left Y opens the journal. Lucky Cat (CoralShop) hands it over after the first task.</summary>
        public bool Available { get; set; } = true;
        readonly List<Object> generated = new List<Object>();
        Transform cards;
        Vector3 anchor;
        Quaternion facing;
        float reveal;
        bool waitForRelease, locked;
        CursorLockMode oldCursor;
        bool oldCursorVisible;
        InputAction journalAction, closeAction, selectAction;
        bool journalDown, closeDown, selectDown, keyboardJournalDown;
        readonly Transform[] tabs=new Transform[4];
        readonly Color[] tabColors=new Color[4];
        readonly Dictionary<Renderer,bool> hiddenTools=new Dictionary<Renderer,bool>();
        readonly Color selectedTab=new Color(1,.95f,.76f);
        public Material OverlayMaterial { get; private set; }
        public bool EditorHandPreview { get; set; }
        public bool FollowsHand => handLayout;
        RectTransform printRect, controlsButton, closeButton;
        Text objectiveText;
        bool controlsPage;
        public bool ControlsVisible => controlsPage;
        public int SelectedTab { get; private set; }
        Text pageTitle;
        DiveCardDoodles doodles;
        HabitatJournal habitatJournal;
        GameObject cornerStickers, growthStickers;
        bool tabPress;
        // Bare hands: the journal opens within reach and smaller, and a fingertip draws on it.
        bool handLayout, fingerOnPage;
        const float HandScale=.8f, TouchFront=.012f, TouchBehind=.04f;
        readonly string[] titles={"FIELD GUIDE","CORAL LOG","MARINE LIFE","NOTES"};
        void Awake()
        {
            Instance=this;
            journalAction=new InputAction("Dive cards",InputActionType.Button,"<XRController>{LeftHand}/secondaryButton");
            closeAction=new InputAction("Close dive cards",InputActionType.Button,"<XRController>{RightHand}/secondaryButton");
            selectAction=new InputAction("Card selection",InputActionType.Button,"<XRController>{RightHand}/triggerPressed");
            journalAction.Enable();closeAction.Enable();selectAction.Enable();
        }

        void Start()
        {
            // Stored in Resources so Android builds retain the overlay shader.
            OverlayMaterial=new Material(Resources.Load<Shader>("DiveCards/JournalOverlay")){name="Journal UI overlay"};
            generated.Add(OverlayMaterial);
            cards=new GameObject("Dive journal / UI only").transform;
            cards.SetParent(transform,false);
            BuildPrint();SelectTab(0);cards.gameObject.SetActive(false);
            if(openOnStart)SetOpen(true);
        }

        public void SetOpen(bool value)
        {
            if(!Camera.main||!cards)return;
            if(IsOpen==value)return;
            IsOpen=value;
            if(value)
            {
                var view=Camera.main.transform;
                handLayout=Hands.ReefInput.HandsMode||UnityEngine.XR.XRSettings.isDeviceActive||EditorHandPreview;
                facing=view.rotation; anchor=view.TransformPoint(handLayout?new Vector3(0,-.16f,.42f):new Vector3(-.025f,-.015f,.86f));
                if(ReefToolMenu.Instance)ReefToolMenu.Instance.SetOpen(false);
                if(!locked)
                {
                    oldCursor=Cursor.lockState;oldCursorVisible=Cursor.visible;locked=true;
                    // Leave XR simulation live: the journal must follow moving simulated hands.
                    var pickup=FindFirstObjectByType<ReefBrushPickup>();
                    if(pickup&&pickup.ToolHeld)foreach(var r in pickup.ActiveTool.GetComponentsInChildren<Renderer>()){hiddenTools[r]=r.forceRenderingOff;r.forceRenderingOff=true;}
                }
                Cursor.lockState=CursorLockMode.None;Cursor.visible=true;
            }
            else { waitForRelease=true;EditorHandPreview=false;if(doodles)doodles.Clear(); }
        }
        void Update()
        {
            var key=Keyboard.current;
            bool j=journalAction.IsPressed(),b=closeAction.IsPressed(),trigger=selectAction.IsPressed();
            bool keyboardJournal=key?.jKey.isPressed??false;
            if(Available&&(keyboardJournal&&!keyboardJournalDown||j&&!journalDown))SetOpen(!IsOpen);
            if((key?.escapeKey.wasPressedThisFrame==true||b&&!closeDown)&&IsOpen)SetOpen(false);
            UpdatePose();
            if(IsOpen&&reveal>.8f&&!MouseLookActive)
            {
                Ray ray=Camera.main.ScreenPointToRay(Mouse.current!=null?Mouse.current.position.ReadValue():Vector2.zero);
                bool xr=UnityEngine.XR.XRSettings.isDeviceActive&&ReefToolMenu.Instance&&ReefToolMenu.Instance.RightHand&&ReefToolMenu.Instance.RightHand.gameObject.activeInHierarchy;
                if(xr)
                {var hand=ReefToolMenu.Instance.RightHand;ray=new Ray(hand.position,hand.forward);}
                if(Hands.ReefInput.HandsMode&&!EditorHandPreview)FingerDraw();
                else
                {
                    bool pressed=xr?trigger:(Mouse.current?.leftButton.isPressed??false);
                    if(!pressed)tabPress=false;
                    if(xr?trigger&&!selectDown:Mouse.current?.leftButton.wasPressedThisFrame==true)tabPress=ClickUI(ray);
                    if(doodles)doodles.Draw(ray,IsOpen&&pressed&&!tabPress);
                }
            }
            else if(doodles)doodles.EndStroke();
            if(waitForRelease&&!trigger&&!(Mouse.current?.leftButton.isPressed??false)&&!Hands.ReefHands.Get(1).Pinch)waitForRelease=false;
            journalDown=j;closeDown=b;selectDown=trigger;keyboardJournalDown=keyboardJournal;
        }
        void LateUpdate()
        {
            if(!cards||!Camera.main)return;
            reveal=Mathf.MoveTowards(reveal,IsOpen?1:0,Time.unscaledDeltaTime/.48f);
            if(IsOpen&&!UnityEngine.XR.XRSettings.isDeviceActive){Cursor.lockState=MouseLookActive?CursorLockMode.Locked:CursorLockMode.None;Cursor.visible=!MouseLookActive;}
            cards.gameObject.SetActive(reveal>0);
            if(!IsOpen&&reveal==0&&locked)RestoreControls();
            UpdatePose();
            if(objectiveText)
            {
                var hud=GetComponent<ReefCleaningHUD>();
                string value="OBJECTIVE  /  "+(hud?hud.CurrentObjective:"Explore the reef");
                if(objectiveText.text!=value)objectiveText.text=value;
            }
        }
        void UpdatePose()
        {
            if(!cards||!Camera.main)return;
            var view=Camera.main.transform;
            if(handLayout&&!EditorHandPreview&&Hands.ReefInput.HandsMode&&!Hands.ReefHands.Get(0).Tracked)return;
            if(handLayout && !EditorHandPreview && Hands.ReefInput.TryGetPosition(0,out Vector3 palm))
            {
                var away=palm-view.position;
                facing=away.sqrMagnitude>.0001f?Quaternion.LookRotation(away,Vector3.up):view.rotation;
                // Attach the lower edge to the palm; preserve readable orientation toward the eyes.
                anchor=palm+facing*new Vector3(0,.24f,0);
            }
            else
            {
                facing=view.rotation;
                anchor=view.TransformPoint(EditorHandPreview?new Vector3(-.12f,-.08f,.65f):new Vector3(-.025f,-.015f,.86f));
            }
            float t=Mathf.SmoothStep(0,1,reveal);
            cards.SetPositionAndRotation(anchor+facing*Vector3.Lerp(new Vector3(-.16f,-.20f,.03f),Vector3.zero,t),
                facing*Quaternion.Euler(0,0,Mathf.Lerp(-24,0,t)));
            cards.localScale=Vector3.one*(previewScale*(handLayout?HandScale:1)*Mathf.Lerp(.85f,1,t));
        }
        static bool Hit(RectTransform rect,Ray ray) => rect && new Plane(rect.forward,rect.position).Raycast(ray,out float d) && rect.rect.Contains(rect.InverseTransformPoint(ray.GetPoint(d)));
        public bool ClickUI(Ray ray)
        {
            if(Hit(closeButton,ray)){SetOpen(false);return true;}
            if(Hit(controlsButton,ray)){controlsPage=!controlsPage;if(habitatJournal)habitatJournal.ShowControls(controlsPage);return true;}
            for(int i=0;i<4;i++)if(Hit((RectTransform)tabs[i],ray)){SelectTab(i);return true;}
            return habitatJournal && habitatJournal.Click(ray);
        }
        public bool OwnsHand(int index)
        {
            if(!BlocksControls||!handLayout)return false;
            if(index==0)return true;
            var hand=Hands.ReefHands.Get(1);
            if(!hand.Tracked||!Camera.main||!printRect)return false;
            var p=printRect.InverseTransformPoint(Camera.main.transform.parent.TransformPoint(hand.IndexTip.position));
            // A generous interaction zone prevents a reaching/withdrawing finger becoming a swim stroke.
            return Mathf.Abs(p.x)<430 && Mathf.Abs(p.y)<340 && Mathf.Abs(p.z)<240;
        }
        /// <summary>
        /// Bare hands: the right index fingertip touching the page (within TouchFront in front of it)
        /// draws, and touching a tab selects it. The tip is projected straight onto the page.
        /// </summary>
        void FingerDraw()
        {
            var view=Camera.main.transform;var hand=Hands.ReefHands.Get(1);
            if(!doodles||!hand.Tracked){if(doodles)doodles.EndStroke();return;}
            Vector3 tip=view.parent.TransformPoint(hand.IndexTip.position),normal=doodles.transform.forward;
            // The page faces the eyes, so its forward points away and a finger in front has depth < 0.
            float depth=Vector3.Dot(tip-doodles.transform.position,normal);
            bool touching=depth>-TouchFront&&depth<TouchBehind;
            var ray=new Ray(tip-normal*.05f,normal);
            if(touching&&!fingerOnPage)tabPress=ClickUI(ray);
            if(!touching)tabPress=false;
            doodles.Draw(ray,IsOpen&&touching&&!tabPress);
            fingerOnPage=touching;
        }
        public void SelectTab(int index)
        {
            if(doodles)doodles.Clear();
            SelectedTab=Mathf.Clamp(index,0,3);controlsPage=false;
            if(cornerStickers)cornerStickers.SetActive(!habitatJournal&&SelectedTab!=1);
            if(growthStickers)growthStickers.SetActive(!habitatJournal&&SelectedTab==1);
            if(habitatJournal)habitatJournal.Show(SelectedTab);
            for(int i=0;i<4;i++)tabs[i].GetComponent<Image>().color=i==SelectedTab?selectedTab:tabColors[i];
            if(pageTitle)pageTitle.text=titles[SelectedTab];
        }
        Image Panel(Transform parent,string label,Vector2 at,Vector2 size,Color color)
        {
            var go=new GameObject(label,typeof(RectTransform),typeof(Image));go.transform.SetParent(parent,false);
            var rect=go.GetComponent<RectTransform>();rect.sizeDelta=size;rect.anchoredPosition=at;
            var image=go.GetComponent<Image>();image.color=color;image.material=OverlayMaterial;image.raycastTarget=false;
            var hud=GetComponent<ReefCleaningHUD>();
            if(hud&&hud.panelSprite){image.sprite=hud.panelSprite;image.type=Image.Type.Sliced;image.pixelsPerUnitMultiplier=5;}
            return image;
        }
        void BuildPrint()
        {
            var canvas=new GameObject("Printed card face",typeof(RectTransform),typeof(Canvas));canvas.transform.SetParent(cards,false);
            var layer=canvas.GetComponent<Canvas>();layer.renderMode=RenderMode.WorldSpace;layer.sortingOrder=1000;
            printRect=canvas.GetComponent<RectTransform>();printRect.sizeDelta=new Vector2(650,425);printRect.localScale=Vector3.one*.001f;
            var ink=new Color(.12f,.30f,.36f);
            for(int i=3;i>0;i--)Panel(canvas.transform,"Offset card "+i,new Vector2(i*9,-i*7),new Vector2(650,425),new Color(.22f,.53f,.65f));
            Panel(canvas.transform,"Blue PVC face",Vector2.zero,new Vector2(650,425),new Color(.55f,.77f,.85f));
            for(int i=0;i<4;i++)
            {
                tabColors[i]=i%2==0?new Color(.98f,.78f,.32f):new Color(.97f,.61f,.35f);
                var image=Panel(canvas.transform,"Category tab "+i,new Vector2(-135+i*115,227),new Vector2(106,46),tabColors[i]);
                tabs[i]=image.transform;
                Print(tabs[i],new[]{"GUIDE","CORALS","MARINE","NOTES"}[i],Vector2.zero,new Vector2(103,40),17,ink);
            }
            pageTitle=Print(canvas.transform,"FIELD GUIDE",new Vector2(-160,181),new Vector2(280,32),23,ink);
            objectiveText=Print(canvas.transform,"",new Vector2(-40,146),new Vector2(520,32),17,ink);
            controlsButton=Panel(canvas.transform,"Controls",new Vector2(190,184),new Vector2(120,33),new Color(.98f,.83f,.43f)).rectTransform;
            Print(controlsButton,"Controls",Vector2.zero,new Vector2(115,32),17,ink);
            closeButton=Panel(canvas.transform,"Close journal",new Vector2(286,184),new Vector2(40,33),new Color(.96f,.90f,.75f)).rectTransform;
            Print(closeButton,"X",Vector2.zero,new Vector2(38,32),19,ink);
            Print(canvas.transform,"A LITTLE HELP FROM LUCKY CAT",new Vector2(-85,-188),new Vector2(420,24),14,ink);
            var texture=Resources.Load<Texture2D>("DiveCards/LuckyCatStamp");
            if(texture)
            {
                var stamp=new GameObject("Actual Lucky Cat model stamp",typeof(RectTransform),typeof(RawImage));stamp.transform.SetParent(canvas.transform,false);
                var r=stamp.GetComponent<RectTransform>();r.sizeDelta=new Vector2(45,45);r.anchoredPosition=new Vector2(260,-183);
                var image=stamp.GetComponent<RawImage>();image.texture=texture;image.color=ink;image.material=OverlayMaterial;image.raycastTarget=false;
            }
            // Flat binding and pencil details: UI graphics only, no meshes or colliders.
            for(int i=0;i<2;i++)Panel(canvas.transform,"Binding",new Vector2(-283+i*28,207),new Vector2(9,35),new Color(.88f,.93f,.88f));
            var pen=Panel(canvas.transform,"Tethered pencil",new Vector2(352,-12),new Vector2(9,188),new Color(.98f,.78f,.32f));
            pen.rectTransform.localEulerAngles=new Vector3(0,0,-7);
            Panel(canvas.transform,"Graphite tip",new Vector2(341,-111),new Vector2(6,12),ink);
            Panel(canvas.transform,"Pencil tie",new Vector2(335,82),new Vector2(36,3),new Color(.98f,.78f,.32f));
            cornerStickers=new GameObject("Corner coral stickers",typeof(RectTransform));cornerStickers.transform.SetParent(canvas.transform,false);
            Sticker(cornerStickers.transform,"BranchCoral_S4",new Vector2(-225,65),112,-12);
            Sticker(cornerStickers.transform,"DiscCoral_S3",new Vector2(225,94),120,10);
            Sticker(cornerStickers.transform,"TubeSponge_Blossom_S3",new Vector2(-225,-105),104,8);
            growthStickers=new GameObject("Coral growth stage stickers",typeof(RectTransform));growthStickers.transform.SetParent(canvas.transform,false);
            for(int i=0;i<4;i++)
            {
                Sticker(growthStickers.transform,"BranchCoral_S"+(i+1),new Vector2(-225+i*150,25),132,i%2==0?-6:6);
                Print(growthStickers.transform,"STAGE "+(i+1),new Vector2(-225+i*150,-57),new Vector2(125,24),14,ink);
            }
            var drawing=new GameObject("Temporary black doodles",typeof(RectTransform),typeof(DiveCardDoodles));
            drawing.transform.SetParent(canvas.transform,false);
            var drawingRect=drawing.GetComponent<RectTransform>();drawingRect.sizeDelta=new Vector2(650,425);
            drawingRect.localPosition=new Vector3(0,0,-.5f);
            doodles=drawing.GetComponent<DiveCardDoodles>();doodles.raycastTarget=false;doodles.material=OverlayMaterial;
            if(ShipwreckHabitat.Instance){habitatJournal=canvas.AddComponent<HabitatJournal>();habitatJournal.Initialize(this);drawingRect.localPosition=new Vector3(0,0,-1f);}
        }
        void Sticker(Transform parent,string name,Vector2 position,float size,float tilt)
        {
            var texture=Resources.Load<Texture2D>("DiveCards/CoralStickers/"+name);if(!texture)return;
            var go=new GameObject(name+" sticker",typeof(RectTransform),typeof(RawImage));go.transform.SetParent(parent,false);
            var rect=go.GetComponent<RectTransform>();rect.sizeDelta=Vector2.one*size;rect.anchoredPosition=position;rect.localEulerAngles=new Vector3(0,0,tilt);
            var image=go.GetComponent<RawImage>();image.texture=texture;image.raycastTarget=false;image.material=OverlayMaterial;
        }
        Text Print(Transform parent,string value,Vector2 position,Vector2 size,int pixels,Color tint)
        {
            var go=new GameObject(value,typeof(RectTransform),typeof(Text));go.transform.SetParent(parent,false);
            var rect=go.GetComponent<RectTransform>();rect.sizeDelta=size;rect.anchoredPosition=position;
            var text=go.GetComponent<Text>();var hud=FindFirstObjectByType<ReefCleaningHUD>();text.font=hud.boldFont?hud.boldFont:hud.font;
            text.material=OverlayMaterial;text.text=value;text.fontSize=pixels;text.alignment=TextAnchor.MiddleCenter;text.color=tint;text.raycastTarget=false;return text;
        }
        void RestoreControls()
        {
            if(!locked)return;locked=false;

            foreach(var pair in hiddenTools)if(pair.Key)pair.Key.forceRenderingOff=pair.Value;hiddenTools.Clear();
            Cursor.lockState=oldCursor;Cursor.visible=oldCursorVisible;
        }
        void OnDisable(){RestoreControls();if(cards)cards.gameObject.SetActive(false);IsOpen=false;reveal=0;waitForRelease=false;}
        void OnDestroy(){RestoreControls();journalAction?.Dispose();closeAction?.Dispose();selectAction?.Dispose();if(Instance==this)Instance=null;if(cards)Destroy(cards.gameObject);foreach(var item in generated)if(item)Destroy(item);}
    }
}
