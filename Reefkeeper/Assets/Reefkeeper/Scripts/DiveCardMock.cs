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
        // The modelled PVC card look (restored from V3): lit 3D parts drawn over the scene like the UI.
        readonly Transform[] tabMeshes=new Transform[4];
        readonly Material[] tabMaterials=new Material[4];
        Shader modelShader;
        // Pages: 0 is the blank writing page, 1-4 are GUIDE, CORALS, MARINE and NOTES (tabs 0-3).
        public int Page { get; private set; }
        const int PageCount=5;
        static readonly Color FrontSheetColor=new Color(.55f,.77f,.85f);
        Transform hinge, leaf;
        Canvas printCanvas;
        Text hintText;
        Material[] leafMaterials;int[] leafQueues;
        Material backSheetFace;Color backSheetColor;
        // Turning a page: the top card swings up over the binding rings and round to the back.
        bool flipping, swapped;int flipDir, flipTarget;float flipT;
        const float FlipSeconds=.6f, FlipAngle=320f;
        // Bare hands: turning the right palm over turns the page.
        float palmRoll, rollAccum, turnQuietUntil;bool hadRoll;
        const float TurnDegrees=110f, TurnMemory=.45f;
        public bool Flipping => flipping;
        public Material OverlayMaterial { get; private set; }
        public bool EditorHandPreview { get; set; }
        public bool FollowsHand => handLayout;
        RectTransform printRect, controlsButton, closeButton;
        Text objectiveText;
        bool controlsPage;
        public bool ControlsVisible => controlsPage;
        /// <summary>Selected category tab (0-3), or -1 on the blank first page.</summary>
        public int SelectedTab => Page-1;
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
            cards=new GameObject("Dive journal / PVC cards").transform;
            cards.SetParent(transform,false);
            BuildModel();BuildPrint();AlignTabs();ShowPage(0);cards.gameObject.SetActive(false);
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
                if(key!=null&&!flipping)
                {
                    if(key.rightArrowKey.wasPressedThisFrame||key.pageDownKey.wasPressedThisFrame)FlipBy(1);
                    if(key.leftArrowKey.wasPressedThisFrame||key.pageUpKey.wasPressedThisFrame)FlipBy(-1);
                }
                if(Hands.ReefInput.HandsMode&&!EditorHandPreview)PalmTurn();
                if(flipping){if(doodles)doodles.EndStroke();}
                else if(Hands.ReefInput.HandsMode&&!EditorHandPreview)FingerDraw();
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
            UpdateFlip();
            if(hintText&&Page==0){string hint=Hint();if(hintText.text!=hint)hintText.text=hint;}
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
            if(flipping||Time.unscaledTime<turnQuietUntil)return true;
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
        /// <summary>Turns to a category tab's page (0-3).</summary>
        public void SelectTab(int index)=>GoToPage(Mathf.Clamp(index,0,3)+1);
        public void FlipBy(int direction)=>GoToPage(Mathf.Clamp(Page+direction,0,PageCount-1));
        /// <summary>Turns to a page: animated over the rings while the journal is open, instantly otherwise.</summary>
        public void GoToPage(int page)
        {
            page=Mathf.Clamp(page,0,PageCount-1);
            if(flipping||page==Page)return;
            if(!IsOpen||reveal<.5f||!hinge){ShowPage(page);return;}
            flipTarget=page;flipDir=page>Page?1:-1;flipT=0;swapped=false;flipping=true;
            // The card underneath shows as a fresh page while the top one is turning.
            if(backSheetFace)backSheetFace.SetColor("_Color",FrontSheetColor);
            if(doodles)doodles.Clear();
            UpdateFlip();
        }
        void UpdateFlip()
        {
            if(!flipping||!hinge)return;
            flipT=Mathf.Min(1,flipT+Time.unscaledDeltaTime/FlipSeconds);
            float e=Mathf.SmoothStep(0,1,flipT);
            // Forward: the top card lifts towards the eyes, over the rings and round behind the stack.
            // Back: the same swing in reverse, bringing the previous card over the rings to the front.
            float angle=flipDir>0?Mathf.Lerp(0,FlipAngle,e):Mathf.Lerp(FlipAngle,0,e);
            hinge.localRotation=Quaternion.Euler(angle,0,0);
            if(!swapped&&(flipDir>0?angle>=90:angle<=90)){ShowPage(flipTarget);swapped=true;}
            // Printing only shows while the card faces the eyes.
            if(printCanvas)printCanvas.enabled=angle<90;
            SetLeafBehind(angle>180);
            if(flipT>=1)
            {
                flipping=false;hinge.localRotation=Quaternion.identity;
                if(printCanvas)printCanvas.enabled=true;
                SetLeafBehind(false);
                if(backSheetFace)backSheetFace.SetColor("_Color",backSheetColor);
            }
        }
        void SetLeafBehind(bool behind)
        {
            if(leafMaterials==null)return;
            for(int i=0;i<leafMaterials.Length;i++)leafMaterials[i].renderQueue=behind?3950:leafQueues[i];
        }
        void ShowPage(int page)
        {
            Page=Mathf.Clamp(page,0,PageCount-1);controlsPage=false;
            if(doodles)doodles.Clear();
            int tab=Page-1;
            if(cornerStickers)cornerStickers.SetActive(!habitatJournal&&tab>=0&&tab!=1);
            if(growthStickers)growthStickers.SetActive(!habitatJournal&&tab==1);
            if(habitatJournal)habitatJournal.Show(tab);
            for(int i=0;i<4;i++)if(tabMaterials[i])tabMaterials[i].SetColor("_Color",i==tab?selectedTab:tabColors[i]);
            if(pageTitle)pageTitle.text=tab>=0?titles[tab]:"";
            if(objectiveText)objectiveText.gameObject.SetActive(tab>=0);
            if(hintText){hintText.gameObject.SetActive(Page==0);hintText.text=Hint();}
        }
        /// <summary>The one-line help printed low on the blank first page.</summary>
        static string Hint() => Hands.ReefInput.HandsMode
            ? "Draw with your fingertip  ·  Turn your right palm over to turn the page  ·  Tap a tab"
            : UnityEngine.XR.XRSettings.isDeviceActive
            ? "Hold the trigger on the page to draw  ·  Point at a tab to turn the page"
            : "Drag on the page to draw  ·  Left / right arrow keys or a tab turn the page";
        /// <summary>
        /// Bare hands: an open right hand whose palm turns over (about 110 degrees within half a second)
        /// turns the page: palm turning up = next page, palm turning down = previous page.
        /// </summary>
        void PalmTurn()
        {
            var hand=Hands.ReefHands.Get(1);
            if(!hand.Tracked||!hand.Open||!handLayout){hadRoll=false;rollAccum=0;return;}
            float roll=PalmRoll(hand.Palm.rotation),dt=Mathf.Max(Time.unscaledDeltaTime,1e-4f);
            if(hadRoll)
            {
                float delta=Mathf.DeltaAngle(palmRoll,roll);
                rollAccum=rollAccum*Mathf.Exp(-dt/TurnMemory)+delta;
                // A turning palm is not a swim stroke, during the turn and just after it.
                if(Mathf.Abs(delta)/dt>90f)turnQuietUntil=Time.unscaledTime+.5f;
            }
            palmRoll=roll;hadRoll=true;
            if(flipping||Mathf.Abs(rollAccum)<TurnDegrees)return;
            int direction=rollAccum<0?1:-1;rollAccum=0;turnQuietUntil=Time.unscaledTime+.6f;
            FlipBy(direction);
        }
        /// <summary>
        /// Palm roll about the finger direction, in degrees, measured from "palm facing down".
        /// For a right hand, turning the palm up (supination) decreases it.
        /// </summary>
        public static float PalmRoll(Quaternion palm)
        {
            Vector3 fingers=palm*Vector3.forward,facing=palm*Vector3.down;
            var reference=Vector3.ProjectOnPlane(Vector3.down,fingers);
            if(reference.sqrMagnitude<1e-4f)reference=Vector3.ProjectOnPlane(Vector3.forward,fingers);
            return Vector3.SignedAngle(reference.normalized,Vector3.ProjectOnPlane(facing,fingers),fingers);
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
            var layer=canvas.GetComponent<Canvas>();layer.renderMode=RenderMode.WorldSpace;layer.sortingOrder=1000;printCanvas=layer;
            printRect=canvas.GetComponent<RectTransform>();printRect.sizeDelta=new Vector2(650,425);printRect.localScale=Vector3.one*.001f;printRect.localPosition=new Vector3(0,0,FrontFace-.0012f);
            var ink=new Color(.12f,.30f,.36f);
            for(int i=0;i<4;i++)
            {
                // Invisible hit area + label over the modelled tab; AlignTabs places it.
                var image=Panel(canvas.transform,"Category tab "+i,Vector2.zero,new Vector2(82,43),Color.clear);
                tabs[i]=image.transform;
                Print(tabs[i],new[]{"GUIDE","CORALS","MARINE","NOTES"}[i],new Vector2(0,4),new Vector2(80,26),13,ink);
            }
            // Clear of the binding rings at the top left.
            pageTitle=Print(canvas.transform,"FIELD GUIDE",new Vector2(-100,181),new Vector2(260,32),23,ink);
            hintText=Print(canvas.transform,"",new Vector2(0,-62),new Vector2(600,26),14,ink);
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
            // The printing turns with the top card.
            if(leaf)canvas.transform.SetParent(leaf.parent,true);
        }
        /// <summary>
        /// The V3 modelled journal: four rounded PVC cards with category tabs, binding rings, a tethered
        /// pencil and printed water lines. Drawn back to front by render queue with JournalOverlayLit,
        /// so it stays visible over the hand and reef like the UI printed on it.
        /// </summary>
        void BuildModel()
        {
            modelShader=Resources.Load<Shader>("DiveCards/JournalOverlayLit");
            int order=0;
            // Binding rings pass through two punched holes in every card. Each ring is split where it
            // crosses the stack, so its back arc sits behind the cards and its front arc in front.
            var ringBack=Torus(RingRadius,RingTube,RingFrontEnd,RingFrontStart+360);
            var ringFront=Torus(RingRadius,RingTube,RingFrontStart,RingFrontEnd);
            var metalBack=ModelMaterial("Soft metal rings (back arc)",new Color(.65f,.73f,.71f),.42f,.55f,order++);
            foreach(float x in RingX){var t=Part("Binding ring back",ringBack,new[]{metalBack});t.localPosition=new Vector3(x,RingY,RingZ);t.localEulerAngles=new Vector3(0,RingYaw,0);}
            hinge=new GameObject("Page hinge (binding rings)").transform;hinge.SetParent(cards,false);hinge.localPosition=new Vector3(0,RingY,RingZ);
            var plate=RoundedPlate(.65f,.425f,SheetDepth,.019f,SheetBevel);var tabPlate=RoundedPlate(.082f,.043f,SheetDepth,.008f,.001f);var hole=Disc(.0048f);
            var holeColor=new Color(.07f,.17f,.21f);
            for(int i=3;i>=0;i--)
            {
                // Each sheet's tab first, then the sheet over its lower half, then the sheet's holes.
                tabColors[i]=i%2==0?new Color(.98f,.74f,.23f):new Color(.93f,.48f,.22f);
                tabMaterials[i]=ModelMaterial("Category tab "+i,tabColors[i],.28f,0,order++);
                var faceColor=i==0?FrontSheetColor:new Color(.18f,.53f,.69f);
                var face=ModelMaterial("PVC card "+(i+1),faceColor,i==0?.23f:.26f,0,order);
                var edge=ModelMaterial("PVC card edge "+(i+1),new Color(.18f,.53f,.69f),.26f,0,order++);
                var holeMaterial=ModelMaterial("Punched holes "+(i+1),holeColor,.1f,0,order++);
                var sheet=Part("PVC card "+(i+1),plate,new[]{face,edge});
                sheet.localPosition=new Vector3(i*.009f,-i*.007f,i*SheetStep);
                sheet.localEulerAngles=new Vector3(0,0,-i*1.1f);
                var tab=Part("Category tab "+(i+1),tabPlate,new[]{tabMaterials[i],tabMaterials[i]});
                tab.SetParent(sheet,false);tab.localPosition=new Vector3(-.10f+i*.105f,.215f+i*.012f,.001f);
                // Tabs stay put while the top card turns over.
                tab.SetParent(cards,true);tabMeshes[i]=tab;
                foreach(float x in RingX)
                {
                    var h=Part("Punched hole",hole,new[]{holeMaterial});h.SetParent(sheet,false);
                    var p=sheet.InverseTransformPoint(cards.TransformPoint(RingCrossing(x,sheet.localPosition.z)));
                    h.localPosition=new Vector3(p.x,p.y,-SheetDepth*.5f-.0004f);
                }
                if(i==1){backSheetFace=face;backSheetColor=faceColor;}
                if(i==0){leaf=sheet;leafMaterials=new[]{face,edge,holeMaterial};}
            }
            leafQueues=new int[leafMaterials.Length];for(int i=0;i<leafMaterials.Length;i++)leafQueues[i]=leafMaterials[i].renderQueue;
            leaf.SetParent(hinge,true);
            var pattern=ModelMaterial("Soft blue water print",new Color(.39f,.64f,.75f),.12f,0,order++);
            // Printed on the top card, so it goes behind the stack with it.
            System.Array.Resize(ref leafMaterials,leafMaterials.Length+1);leafMaterials[leafMaterials.Length-1]=pattern;
            leafQueues=new int[leafMaterials.Length];for(int i=0;i<leafMaterials.Length;i++)leafQueues[i]=leafMaterials[i].renderQueue;
            for(int row=0;row<5;row++)
            {
                var line=new GameObject("Printed water ripple").AddComponent<LineRenderer>();line.transform.SetParent(leaf,false);
                line.useWorldSpace=false;line.positionCount=64;line.startWidth=line.endWidth=.0008f;line.sharedMaterial=pattern;line.generateLightingData=true;
                for(int j=0;j<64;j++){float x=Mathf.Lerp(-.30f,.30f,j/63f);line.SetPosition(j,new Vector3(x,-.15f+row*.015f+Mathf.Sin(x*52+row*.7f)*.003f,PrintZ));}
            }
            for(int i=0;i<8;i++)
            {
                var bubble=new GameObject("Printed bubble").AddComponent<LineRenderer>();bubble.transform.SetParent(leaf,false);
                bubble.useWorldSpace=false;bubble.loop=true;bubble.positionCount=24;bubble.startWidth=bubble.endWidth=.0008f;bubble.sharedMaterial=pattern;bubble.generateLightingData=true;
                float x=i%2==0?-.289f:.289f,y=-.04f+(i/2)*.045f,r=.004f+(i%3)*.002f;
                for(int j=0;j<24;j++){float a=j*Mathf.PI*2/24;bubble.SetPosition(j,new Vector3(x+Mathf.Cos(a)*r,y+Mathf.Sin(a)*r,PrintZ));}
            }
            var metalFront=ModelMaterial("Soft metal rings (front arc)",new Color(.65f,.73f,.71f),.42f,.55f,order++);
            foreach(float x in RingX){var t=Part("Binding ring front",ringFront,new[]{metalFront});t.localPosition=new Vector3(x,RingY,RingZ);t.localEulerAngles=new Vector3(0,RingYaw,0);}
            var yellow=ModelMaterial("Yellow pencil",new Color(.98f,.74f,.23f),.28f,0,order++);
            var graphite=ModelMaterial("Pencil graphite",new Color(.14f,.22f,.22f),.1f,0,order++);
            var pen=GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pen.name="Tethered yellow pencil";pen.transform.SetParent(cards,false);
            pen.transform.localPosition=new Vector3(.37f,-.018f,-.008f);
            pen.transform.localScale=new Vector3(.009f,.105f,.009f);
            pen.transform.localEulerAngles=new Vector3(0,0,-7);
            pen.GetComponent<Renderer>().sharedMaterial=yellow;Destroy(pen.GetComponent<Collider>());
            var tip=GameObject.CreatePrimitive(PrimitiveType.Sphere);tip.name="Graphite tip";
            tip.transform.SetParent(cards,false);tip.transform.localPosition=new Vector3(.357f,-.126f,-.008f);
            tip.transform.localScale=new Vector3(.007f,.015f,.007f);
            tip.GetComponent<Renderer>().sharedMaterial=graphite;Destroy(tip.GetComponent<Collider>());
            var cord=new GameObject("Pencil tether").AddComponent<LineRenderer>();cord.transform.SetParent(cards,false);
            cord.useWorldSpace=false;cord.positionCount=33;cord.startWidth=cord.endWidth=.0025f;
            cord.sharedMaterial=yellow;cord.numCapVertices=4;cord.generateLightingData=true;
            for(int i=0;i<33;i++)
            {
                float t=i/32f;
                var a=new Vector3(.309f,.17f,.001f);var b=new Vector3(.465f,.13f,-.008f);
                var c=new Vector3(.465f,-.23f,-.008f);var d=new Vector3(.37f,.087f,-.008f);
                cord.SetPosition(i,Mathf.Pow(1-t,3)*a+3*(1-t)*(1-t)*t*b+3*(1-t)*t*t*c+t*t*t*d);
            }
        }
        // Binding rings (journal-local): two rings along the top-left edge, tilted like the V3 ones.
        static readonly float[] RingX={-.285f,-.225f};
        // Card thickness and stacking (m). Thick enough, with a bevel, that the edges read as volume.
        const float SheetDepth=.006f,SheetStep=.011f,SheetBevel=.0015f;
        const float FrontFace=-SheetDepth*.5f,PrintZ=FrontFace-.0003f;
        const float RingY=.2f,RingRadius=.031f,RingTube=.0032f,RingYaw=35f;
        const float RingZ=(FrontFace+3*SheetStep+SheetDepth*.5f)*.5f;
        // The front arc (in front of the top card) runs between these ring angles (degrees).
        static readonly float RingFrontStart=180f+FrontArcHalf(),RingFrontEnd=360f-FrontArcHalf();
        static float FrontArcHalf()=>Mathf.Asin(Mathf.Clamp01((RingZ-FrontFace)/(Mathf.Cos(RingYaw*Mathf.Deg2Rad)*RingRadius)))*Mathf.Rad2Deg;
        /// <summary>Where a ring crosses a card lying at depth z, below the card's top edge (journal-local).</summary>
        static Vector3 RingCrossing(float x,float z)
        {
            float yaw=RingYaw*Mathf.Deg2Rad;
            float s=Mathf.Clamp((z-RingZ)/(Mathf.Cos(yaw)*RingRadius),-1,1);
            float u=Mathf.PI-Mathf.Asin(s);
            return new Vector3(x+Mathf.Sin(yaw)*RingRadius*Mathf.Sin(u),RingY+RingRadius*Mathf.Cos(u),z);
        }
        Mesh Disc(float radius)
        {
            const int steps=24;var verts=new List<Vector3>{Vector3.zero};var tris=new List<int>();
            for(int i=0;i<steps;i++){float a=i*Mathf.PI*2/steps;verts.Add(new Vector3(Mathf.Cos(a)*radius,Mathf.Sin(a)*radius,0));}
            for(int i=0;i<steps;i++)tris.AddRange(new[]{0,1+(i+1)%steps,1+i});
            var mesh=new Mesh{name="Punched hole"};mesh.SetVertices(verts);mesh.SetTriangles(tris,0);mesh.RecalculateNormals();mesh.RecalculateBounds();generated.Add(mesh);return mesh;
        }
        /// <summary>Puts each tab's hit area and label over its modelled tab (sheets are offset and tilted).</summary>
        void AlignTabs()
        {
            for(int i=0;i<4;i++)if(tabMeshes[i]&&tabs[i])
            {
                var rect=(RectTransform)tabs[i];
                Vector3 local=printRect.InverseTransformPoint(tabMeshes[i].position);
                rect.anchoredPosition=new Vector2(local.x,local.y);
                rect.localEulerAngles=new Vector3(0,0,tabMeshes[i].localEulerAngles.z);
            }
        }
        Material ModelMaterial(string label,Color color,float smoothness,float metallic,int order)
        {
            var m=new Material(modelShader){name=label};
            m.SetColor("_Color",color);m.SetFloat("_Smoothness",smoothness);m.SetFloat("_Metallic",metallic);
            // Below the UI overlay (4000): printed text and buttons always sit on top.
            m.renderQueue=3960+order;
            generated.Add(m);return m;
        }
        Transform Part(string label,Mesh mesh,Material[] materials)
        {
            var go=new GameObject(label,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(cards,false);
            go.GetComponent<MeshFilter>().sharedMesh=mesh;
            var renderer=go.GetComponent<MeshRenderer>();renderer.sharedMaterials=materials;
            renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;renderer.receiveShadows=false;
            return go.transform;
        }
        Mesh RoundedPlate(float width,float height,float depth,float radius,float bevel)
        {
            const int cornerSteps=10, count=44;
            var verts=new List<Vector3>();var front=new List<int>();var sides=new List<int>();
            for(int ring=0;ring<3;ring++)
            {
                float inset=ring==1?0:bevel;
                for(int corner=0;corner<4;corner++)for(int step=0;step<=cornerSteps;step++)
                {
                    float a=(corner*90+step*90f/cornerSteps)*Mathf.Deg2Rad;
                    var center=new Vector2((corner==0||corner==3?1:-1)*(width*.5f-radius),(corner<2?1:-1)*(height*.5f-radius));
                    var p=center+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*(radius-inset);
                    verts.Add(new Vector3(p.x,p.y,(ring-1)*depth*.5f));
                }
            }
            verts.Add(new Vector3(0,0,-depth*.5f));verts.Add(new Vector3(0,0,depth*.5f));
            for(int i=0;i<count;i++)
            {
                int n=(i+1)%count;front.AddRange(new[]{count*3,n,i});
                sides.AddRange(new[]{count*3+1,count*2+i,count*2+n});
                for(int r=0;r<2;r++){int a=r*count+i,b=r*count+n;sides.AddRange(new[]{a,b,b+count,a,b+count,a+count});}
            }
            var mesh=new Mesh{name="Thin rounded PVC plate"};mesh.SetVertices(verts);mesh.subMeshCount=2;
            mesh.SetTriangles(front,0);mesh.SetTriangles(sides,1);mesh.RecalculateNormals();mesh.RecalculateBounds();generated.Add(mesh);return mesh;
        }
        Mesh Torus(float radius,float tube,float fromDegrees=0,float toDegrees=360)
        {
            const int around=40,section=10;var verts=new List<Vector3>();var triangles=new List<int>();
            for(int i=0;i<=around;i++)for(int j=0;j<=section;j++)
            {float u=Mathf.Lerp(fromDegrees,toDegrees,i/(float)around)*Mathf.Deg2Rad,v=j*Mathf.PI*2/section;float r=radius+Mathf.Cos(v)*tube;verts.Add(new Vector3(Mathf.Sin(v)*tube,Mathf.Cos(u)*r,Mathf.Sin(u)*r));}
            for(int i=0;i<around;i++)for(int j=0;j<section;j++)
            {int a=i*(section+1)+j,b=a+section+1;triangles.AddRange(new[]{a,b,a+1,a+1,b,b+1});}
            var mesh=new Mesh{name="Binding ring"};mesh.SetVertices(verts);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();generated.Add(mesh);return mesh;
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
