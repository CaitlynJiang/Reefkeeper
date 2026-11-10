using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Simulation;

namespace Reefkeeper
{
    /// <summary>Journal visual prototype, available from J / left Y during normal play.</summary>
    [DefaultExecutionOrder(-32600)]
    public sealed class DiveCardMock : MonoBehaviour
    {
        public bool IsOpen { get; private set; }
        public Transform Cards => cards;
        public Transform Tab(int index) => tabs[index];
        public static DiveCardMock Instance { get; private set; }
        public static bool BlocksControls => Instance && Instance.isActiveAndEnabled && (Instance.IsOpen || Instance.reveal>0 || Instance.waitForRelease);
        public bool MouseLookActive => IsOpen && reveal>.98f && Camera.main && Camera.main.GetComponentInParent<DiveMotor>() && (Mouse.current?.rightButton.isPressed??false);
        [Range(.6f,1.6f)] public float previewScale=1.15f;
        public bool openOnStart;
        readonly List<Object> generated = new List<Object>();
        Transform cards;
        Vector3 anchor;
        Quaternion facing;
        float reveal;
        bool waitForRelease, simulatorWasEnabled, locked;
        XRInteractionSimulator simulator;
        CursorLockMode oldCursor;
        bool oldCursorVisible;
        InputAction journalAction, closeAction, selectAction;
        bool journalDown, closeDown, selectDown, keyboardJournalDown;
        readonly Transform[] tabs=new Transform[4];
        readonly Material[] tabColors=new Material[4];
        readonly Dictionary<Renderer,bool> hiddenTools=new Dictionary<Renderer,bool>();
        Material selectedTab;
        public int SelectedTab { get; private set; }
        Text pageTitle;
        DiveCardDoodles doodles;
        GameObject cornerStickers, growthStickers;
        bool tabPress;
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
            var cream=Material("Sky blue printed PVC",new Color(.55f,.77f,.85f),.23f);
            var blue=Material("Lake blue PVC",new Color(.18f,.53f,.69f),.26f);
            var yellow=Material("Yellow tabs and pencil",new Color(.98f,.74f,.23f),.28f);
            var orange=Material("Apricot tabs",new Color(.93f,.48f,.22f),.28f);
            selectedTab=Material("Selected cream tab",new Color(1,.95f,.76f),.24f);
            var metal=Material("Soft metal rings",new Color(.65f,.73f,.71f),.42f,.55f);
            var graphite=Material("Pencil graphite",new Color(.14f,.22f,.22f),.1f);
            cards=new GameObject("Blank PVC card stack / 3D study").transform;
            cards.SetParent(transform,false);
            var plate=RoundedPlate(.65f,.425f,.004f,.019f);
            for(int i=3;i>=0;i--)
            {
                var sheet=Part("PVC card "+(i+1),plate,new[]{i==0?cream:blue,blue});
                sheet.localPosition=new Vector3(i*.009f,-i*.007f,i*.009f);
                sheet.localEulerAngles=new Vector3(0,0,-i*1.1f);
                tabColors[i]=i%2==0?yellow:orange;
                var tab=Part("Category tab "+(i+1),RoundedPlate(.082f,.043f,.004f,.008f),new[]{tabColors[i],tabColors[i]});
                tab.SetParent(sheet,false);tab.localPosition=new Vector3(-.10f+i*.105f,.215f+i*.012f,.001f);tabs[i]=tab;
            }
            var ring=Torus(.019f,.0025f);
            foreach(float x in new[]{-.205f,-.145f})
            {var t=Part("Binding ring",ring,new[]{metal});t.localPosition=new Vector3(x-.055f,.208f,.012f);t.localEulerAngles=new Vector3(0,35,0);}
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
            BuildPrint();SelectTab(0);cards.gameObject.SetActive(false);if(openOnStart)SetOpen(true);
        }

        public void SetOpen(bool value)
        {
            if(!Camera.main||!cards)return;
            if(IsOpen==value)return;
            IsOpen=value;
            if(value)
            {
                var view=Camera.main.transform;
                facing=view.rotation; anchor=view.TransformPoint(new Vector3(-.025f,-.015f,.86f));
                if(ReefToolMenu.Instance)ReefToolMenu.Instance.SetOpen(false);
                if(!locked)
                {
                    oldCursor=Cursor.lockState;oldCursorVisible=Cursor.visible;locked=true;
                    simulator=FindFirstObjectByType<XRInteractionSimulator>();
                    simulatorWasEnabled=simulator&&simulator.enabled;if(simulator)simulator.enabled=false;
                    var pickup=FindFirstObjectByType<ReefBrushPickup>();
                    if(pickup&&pickup.ToolHeld)foreach(var r in pickup.ActiveTool.GetComponentsInChildren<Renderer>()){hiddenTools[r]=r.forceRenderingOff;r.forceRenderingOff=true;}
                }
                Cursor.lockState=CursorLockMode.None;Cursor.visible=true;
            }
            else { waitForRelease=true; if(doodles)doodles.Clear(); }
        }
        void Update()
        {
            var key=Keyboard.current;
            bool j=journalAction.IsPressed(),b=closeAction.IsPressed(),trigger=selectAction.IsPressed();
            bool keyboardJournal=key?.jKey.isPressed??false;
            if(keyboardJournal&&!keyboardJournalDown||j&&!journalDown)SetOpen(!IsOpen);
            if((key?.escapeKey.wasPressedThisFrame==true||b&&!closeDown)&&IsOpen)SetOpen(false);
            if(IsOpen&&reveal>.8f&&!MouseLookActive)
            {
                Ray ray=Camera.main.ScreenPointToRay(Mouse.current!=null?Mouse.current.position.ReadValue():Vector2.zero);
                bool xr=UnityEngine.XR.XRSettings.isDeviceActive&&ReefToolMenu.Instance&&ReefToolMenu.Instance.RightHand&&ReefToolMenu.Instance.RightHand.gameObject.activeInHierarchy;
                if(xr)
                {var hand=ReefToolMenu.Instance.RightHand;ray=new Ray(hand.position,hand.forward);}
                bool pressed=xr?trigger:(Mouse.current?.leftButton.isPressed??false);
                if(!pressed)tabPress=false;
                if(xr?trigger&&!selectDown:Mouse.current?.leftButton.wasPressedThisFrame==true)
                    for(int i=0;i<4;i++)if(new Plane(tabs[i].forward,tabs[i].position).Raycast(ray,out float distance))
                    {var p=tabs[i].InverseTransformPoint(ray.GetPoint(distance));if(Mathf.Abs(p.x)<.041f&&Mathf.Abs(p.y)<.024f){SelectTab(i);tabPress=true;break;}}
                if(doodles)doodles.Draw(ray,pressed&&!tabPress);
            }
            else if(doodles)doodles.EndStroke();
            if(waitForRelease&&!trigger&&!(Mouse.current?.leftButton.isPressed??false))waitForRelease=false;
            journalDown=j;closeDown=b;selectDown=trigger;keyboardJournalDown=keyboardJournal;
        }
        void LateUpdate()
        {
            if(!cards||!Camera.main)return;
            reveal=Mathf.MoveTowards(reveal,IsOpen?1:0,Time.unscaledDeltaTime/.48f);
            if(IsOpen&&!UnityEngine.XR.XRSettings.isDeviceActive){Cursor.lockState=MouseLookActive?CursorLockMode.Locked:CursorLockMode.None;Cursor.visible=!MouseLookActive;}
            cards.gameObject.SetActive(reveal>0);
            if(!IsOpen&&reveal==0&&locked)RestoreControls();
            float t=Mathf.SmoothStep(0,1,reveal);
            cards.SetPositionAndRotation(anchor+facing*Vector3.Lerp(new Vector3(-.55f,-.46f,.08f),Vector3.zero,t),
                facing*Quaternion.Euler(Mathf.Lerp(32,-5,t),Mathf.Lerp(-60,-12,t),Mathf.Lerp(-32,-3,t)));
            cards.localScale=Vector3.one*(previewScale*Mathf.Lerp(.7f,1,t));
        }
        public void SelectTab(int index)
        {
            if(doodles)doodles.Clear();
            SelectedTab=Mathf.Clamp(index,0,3);
            if(cornerStickers)cornerStickers.SetActive(SelectedTab!=1);
            if(growthStickers)growthStickers.SetActive(SelectedTab==1);
            for(int i=0;i<4;i++)tabs[i].GetComponent<MeshRenderer>().sharedMaterials=new[]{i==SelectedTab?selectedTab:tabColors[i],tabColors[i]};
            if(pageTitle)pageTitle.text=titles[SelectedTab];
        }
        void BuildPrint()
        {
            var canvas=new GameObject("Printed card face",typeof(RectTransform),typeof(Canvas));canvas.transform.SetParent(cards,false);
            canvas.GetComponent<Canvas>().renderMode=RenderMode.WorldSpace;
            var rect=canvas.GetComponent<RectTransform>();rect.sizeDelta=new Vector2(650,425);rect.localScale=Vector3.one*.001f;rect.localPosition=new Vector3(0,0,-.0038f);
            var ink=new Color(.12f,.39f,.36f);
            pageTitle=Print(canvas.transform,"FIELD GUIDE",new Vector2(-142,153),new Vector2(290,42),26,ink);
            Print(canvas.transform,"A LITTLE HELP FROM LUCKY CAT",new Vector2(-120,-179),new Vector2(340,24),12,new Color(.20f,.48f,.43f));
            var texture=Resources.Load<Texture2D>("DiveCards/LuckyCatStamp");
            if(texture)
            {
                var image=new GameObject("Actual Lucky Cat model stamp",typeof(RectTransform),typeof(RawImage));image.transform.SetParent(canvas.transform,false);
                var r=image.GetComponent<RectTransform>();r.sizeDelta=new Vector2(106,115);r.anchoredPosition=new Vector2(215,-91);
                image.GetComponent<RawImage>().texture=texture;image.GetComponent<RawImage>().color=new Color(.04f,.28f,.25f,1f);
            }
            Print(canvas.transform,"LUCKY CAT'S",new Vector2(215,-149),new Vector2(150,22),15,ink);
            Print(canvas.transform,"REEF SHOP",new Vector2(215,-169),new Vector2(140,18),11,ink);
            for(int i=0;i<4;i++)
            {
                var tag=new GameObject("Tab label",typeof(RectTransform),typeof(Canvas));tag.transform.SetParent(tabs[i],false);
                tag.GetComponent<Canvas>().renderMode=RenderMode.WorldSpace;tag.transform.localScale=Vector3.one*.001f;tag.transform.localPosition=new Vector3(0,.004f,-.0026f);
                Print(tag.transform,new[]{"GUIDE","CORALS","MARINE","NOTES"}[i],Vector2.zero,new Vector2(80,26),11,ink);
            }
            var pattern=Material("Soft blue water print",new Color(.39f,.64f,.75f),.12f);
            for(int row=0;row<5;row++)
            {
                var line=new GameObject("Printed water ripple").AddComponent<LineRenderer>();line.transform.SetParent(cards,false);
                line.useWorldSpace=false;line.positionCount=64;line.startWidth=line.endWidth=.0008f;line.sharedMaterial=pattern;line.generateLightingData=true;
                for(int j=0;j<64;j++){float x=Mathf.Lerp(-.30f,.30f,j/63f);line.SetPosition(j,new Vector3(x,-.15f+row*.015f+Mathf.Sin(x*52+row*.7f)*.003f,-.0033f));}
            }
            for(int i=0;i<8;i++)
            {
                var bubble=new GameObject("Printed bubble").AddComponent<LineRenderer>();bubble.transform.SetParent(cards,false);
                bubble.useWorldSpace=false;bubble.loop=true;bubble.positionCount=24;bubble.startWidth=bubble.endWidth=.0008f;bubble.sharedMaterial=pattern;bubble.generateLightingData=true;
                float x=i%2==0?-.289f:.289f,y=-.04f+(i/2)*.045f,r=.004f+(i%3)*.002f;
                for(int j=0;j<24;j++){float a=j*Mathf.PI*2/24;bubble.SetPosition(j,new Vector3(x+Mathf.Cos(a)*r,y+Mathf.Sin(a)*r,-.0033f));}
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
            doodles=drawing.GetComponent<DiveCardDoodles>();doodles.raycastTarget=false;
        }
        void Sticker(Transform parent,string name,Vector2 position,float size,float tilt)
        {
            var texture=Resources.Load<Texture2D>("DiveCards/CoralStickers/"+name);if(!texture)return;
            var go=new GameObject(name+" sticker",typeof(RectTransform),typeof(RawImage));go.transform.SetParent(parent,false);
            var rect=go.GetComponent<RectTransform>();rect.sizeDelta=Vector2.one*size;rect.anchoredPosition=position;rect.localEulerAngles=new Vector3(0,0,tilt);
            var image=go.GetComponent<RawImage>();image.texture=texture;image.raycastTarget=false;
        }
        Text Print(Transform parent,string value,Vector2 position,Vector2 size,int pixels,Color tint)
        {
            var go=new GameObject(value,typeof(RectTransform),typeof(Text));go.transform.SetParent(parent,false);
            var rect=go.GetComponent<RectTransform>();rect.sizeDelta=size;rect.anchoredPosition=position;
            var text=go.GetComponent<Text>();var hud=FindFirstObjectByType<ReefCleaningHUD>();text.font=hud.boldFont?hud.boldFont:hud.font;
            text.text=value;text.fontSize=pixels;text.alignment=TextAnchor.MiddleCenter;text.color=tint;text.raycastTarget=false;return text;
        }
        Material Material(string label,Color color,float smoothness,float metallic=0)
        {
            var m=new Material(Shader.Find("Universal Render Pipeline/Lit")){name=label};
            m.SetColor("_BaseColor",color);m.SetFloat("_Smoothness",smoothness);m.SetFloat("_Metallic",metallic);
            generated.Add(m);return m;
        }
        Transform Part(string label,Mesh mesh,Material[] materials)
        {
            var go=new GameObject(label,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(cards,false);
            go.GetComponent<MeshFilter>().sharedMesh=mesh;go.GetComponent<MeshRenderer>().sharedMaterials=materials;return go.transform;
        }
        Mesh RoundedPlate(float width,float height,float depth,float radius)
        {
            const int cornerSteps=10, count=44;
            var verts=new List<Vector3>();var front=new List<int>();var sides=new List<int>();
            for(int ring=0;ring<3;ring++)
            {
                float inset=ring==1?0:.001f;
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
        Mesh Torus(float radius,float tube)
        {
            const int around=40,section=10;var verts=new List<Vector3>();var triangles=new List<int>();
            for(int i=0;i<=around;i++)for(int j=0;j<=section;j++)
            {float u=i*Mathf.PI*2/around,v=j*Mathf.PI*2/section;float r=radius+Mathf.Cos(v)*tube;verts.Add(new Vector3(Mathf.Sin(v)*tube,Mathf.Cos(u)*r,Mathf.Sin(u)*r));}
            for(int i=0;i<around;i++)for(int j=0;j<section;j++)
            {int a=i*(section+1)+j,b=a+section+1;triangles.AddRange(new[]{a,b,a+1,a+1,b,b+1});}
            var mesh=new Mesh{name="Binding ring"};mesh.SetVertices(verts);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();generated.Add(mesh);return mesh;
        }
        void RestoreControls()
        {
            if(!locked)return;locked=false;
            if(simulator)simulator.enabled=simulatorWasEnabled;
            foreach(var pair in hiddenTools)if(pair.Key)pair.Key.forceRenderingOff=pair.Value;hiddenTools.Clear();
            Cursor.lockState=oldCursor;Cursor.visible=oldCursorVisible;
        }
        void OnDisable(){RestoreControls();if(cards)cards.gameObject.SetActive(false);IsOpen=false;reveal=0;waitForRelease=false;}
        void OnDestroy(){RestoreControls();journalAction?.Dispose();closeAction?.Dispose();selectAction?.Dispose();if(Instance==this)Instance=null;if(cards)Destroy(cards.gameObject);foreach(var item in generated)if(item)Destroy(item);}
    }
}
