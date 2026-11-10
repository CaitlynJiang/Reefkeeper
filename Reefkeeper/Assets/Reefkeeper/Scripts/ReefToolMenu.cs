using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Readers;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Simulation;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace Reefkeeper
{
    [DefaultExecutionOrder(-32500)]
    public sealed class ReefToolMenu : MonoBehaviour
    {
        public static ReefToolMenu Instance { get; private set; }
        public static bool BlocksWorldInput => DiveCardMock.BlocksControls || Instance && (Instance.IsOpen || Instance.consumeClick || Time.frameCount <= Instance.closedFrame + 1);
        public bool IsOpen { get; private set; }
        public bool Owns(CleaningToolKind kind) => owned[(int)kind];
        public RectTransform ToggleRect => toggle;
        public RectTransform SlotRect(int index) => slots[index];
        public Transform RightHand { get; private set; }
        public Canvas Canvas => canvas;
        public int HoveredSlot { get; private set; } = -1;

        readonly bool[] owned = new bool[2];
        readonly RectTransform[] slots = new RectTransform[5];
        readonly CanvasGroup[] groups = new CanvasGroup[5];
        readonly ReefPebbleGraphic[] backgrounds = new ReefPebbleGraphic[5];
        readonly Image[] icons = new Image[2];
        readonly Text[] names = new Text[2];
        readonly GameObject[] displays = new GameObject[2];
        readonly Dictionary<Renderer, bool> hidden = new Dictionary<Renderer, bool>();
        readonly Vector2[] positions = { new Vector2(143,185), new Vector2(235,80), new Vector2(250,-50), new Vector2(200,-142), new Vector2(110,-195) };
        readonly Color cream = new Color(.973f,.941f,.843f), ink = new Color(.161f,.306f,.322f);
        readonly Color yellow = new Color(.953f,.808f,.388f), blue = new Color(.40f,.741f,.714f);
        ReefWorkshop workshop;
        ReefBrushPickup pickup;
        XRInteractionSimulator simulator;
        XRInputButtonReader originalCycle, originalMouseToggle;
        XRInputValueReader<Vector2> originalMouseDelta;
        InputAction cycle, openAction, backAction, trigger;
        Canvas canvas;
        RectTransform toggle, pointer, toolHint, closeHint;
        CanvasGroup toggleGroup;
        Image openGlyph, closeGlyph;
        Text openKeyText, closeKeyText;
        Sprite tabGlyph, escapeGlyph;
        Font font;
        Sprite[] toolIcons;
        Transform rayOrigin;
        float expansion;
        int closedFrame = -10;
        bool consumeClick, wasXr, wasCenter, pointerValid;
        bool openWasDown, backWasDown, triggerWasDown, triggerPressed;
        CursorLockMode previousCursor;
        bool previousCursorVisible;
        Vector2 pointerScreen;
        LineRenderer rayLine;
        Material rayMaterial;

        void Awake()
        {
            Instance = this;
            workshop = GetComponent<ReefWorkshop>(); pickup = GetComponent<ReefBrushPickup>();
            var hud = GetComponent<ReefCleaningHUD>(); font = hud.boldFont ? hud.boldFont : hud.font;
            tabGlyph = hud.inputGlyphs.FirstOrDefault(s => s && s.name == "keyboard_tab");
            escapeGlyph = hud.inputGlyphs.FirstOrDefault(s => s && s.name == "keyboard_escape");
            toolIcons = new[] { Resources.Load<Sprite>("ToolMenu/Brush"), Resources.Load<Sprite>("ToolMenu/Scraper") };
            openAction = new InputAction("Open tools", InputActionType.Button, "<XRController>{RightHand}/primaryButton");
            backAction = new InputAction("Close tools", InputActionType.Button, "<XRController>{RightHand}/secondaryButton");
            trigger = new InputAction("Choose tool", InputActionType.Button, "<XRController>{RightHand}/triggerPressed");
            openAction.Enable(); backAction.Enable(); trigger.Enable();
            BuildUI();
        }
        void Start()
        {
            simulator = workshop.xrGroup.GetComponentInChildren<XRInteractionSimulator>(true);
            if (simulator)
            {
                originalCycle = simulator.cycleDevicesInput;
                cycle = new InputAction("Simulator device mode", InputActionType.Button, "<Keyboard>/backquote");
                cycle.Enable();
                simulator.cycleDevicesInput = new XRInputButtonReader { inputSourceMode = XRInputButtonReader.InputSourceMode.InputAction, inputActionPerformed = cycle, inputActionValue = cycle };
            }
            RightHand = workshop.xrGroup.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "Right Controller");
            var nearFar = RightHand ? RightHand.GetComponentInChildren<NearFarInteractor>(true) : null;
            rayOrigin = nearFar ? nearFar.transform : RightHand;
            displays[0] = MakeDisplay(workshop.brush, workshop.brushHome);
            displays[1] = MakeDisplay(workshop.scraper, workshop.scraperHome);
            wasXr = workshop.xrGroup.activeSelf; wasCenter = pickup.CenterMode;
        }

        void Update()
        {
            if (DiveCardMock.BlocksControls) return;
            if (consumeClick && !(Mouse.current?.leftButton.isPressed ?? false) && !trigger.IsPressed()) consumeClick = false;
            bool xr = workshop.xrGroup.activeSelf, center = pickup.CenterMode;
            if (xr != wasXr || center != wasCenter) { if (IsOpen) SetOpen(false); wasXr = xr; wasCenter = center; }
            var keyboard = Keyboard.current;
            // XR pose/button updates can occur before rendering as well as before Update.
            // Sample rising edges here so a button is handled exactly once by the UI.
            bool openDown=openAction.IsPressed(), backDown=backAction.IsPressed(), triggerDown=trigger.IsPressed();
            triggerPressed=triggerDown&&!triggerWasDown;
            if (keyboard?.tabKey.wasPressedThisFrame == true || xr && openDown&&!openWasDown) SetOpen(!IsOpen);
            if (IsOpen && (keyboard?.escapeKey.wasPressedThisFrame == true || xr && backDown&&!backWasDown)) SetOpen(false);
            openWasDown=openDown;backWasDown=backDown;triggerWasDown=triggerDown;
            if (keyboard?.f5Key.wasPressedThisFrame == true && IsOpen) SetOpen(false);
            if (!XRSettings.isDeviceActive) { UpdatePointer(); HandlePointer(); }
        }

        void LateUpdate()
        {
            expansion = Mathf.MoveTowards(expansion, IsOpen ? 1 : 0, Time.unscaledDeltaTime / .26f);
            float reveal = Mathf.SmoothStep(0, 1, expansion);
            float pop = 1 + 2.3f * Mathf.Pow(expansion - 1, 3) + 1.3f * Mathf.Pow(expansion - 1, 2);
            toggle.gameObject.SetActive(expansion > 0);
            toggleGroup.alpha = reveal;
            toggle.anchoredPosition = Vector2.Lerp(new Vector2(-25, -35), new Vector2(65, 0), reveal);
            toggle.localScale = Vector3.one * Mathf.LerpUnclamped(.65f, 1, pop);
            toggle.localEulerAngles = new Vector3(0, 0, Mathf.Lerp(-16, 7, reveal));
            if (IsOpen)
            {
                HideHeld();
                // Input belongs to the UI while the simulator continues to track and swim.
                if (!XRSettings.isDeviceActive) { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
            }
            UpdatePointer();
            HoveredSlot = -1;
            for (int i = 0; i < slots.Length; i++)
            {
                float t = Mathf.SmoothStep(0,1,Mathf.Clamp01(expansion * 1.25f - i * .05f));
                slots[i].anchoredPosition = Vector2.Lerp(new Vector2(65,0), positions[i], t);
                slots[i].localScale = Vector3.one * Mathf.Lerp(.3f,1,t);
                groups[i].alpha = t;
                if (IsOpen && pointerValid && Contains(slots[i],pointerScreen)) HoveredSlot = i;
                bool selected = i < 2 && pickup.ToolHeld && (int)pickup.ActiveTool.toolKind == i;
                backgrounds[i].Tint(selected ? cream : blue);
                if (i < 2)
                {
                    icons[i].color = new Color(1,1,1,owned[i] ? 1 : .35f);
                    names[i].text = owned[i] ? i == 0 ? "Brush" : "Scraper" : "Pick up first";
                    if (HoveredSlot == i && owned[i] && !selected) backgrounds[i].Tint(Color.Lerp(blue,cream,.35f));
                }
            }
            if (XRSettings.isDeviceActive) HandlePointer();
            bool controller = workshop.xrGroup.activeSelf && !pickup.CenterMode;
            openGlyph.enabled = !controller && tabGlyph;
            closeGlyph.enabled = !controller && escapeGlyph;
            openKeyText.text = controller ? "A" : tabGlyph ? "" : "Tab";
            closeKeyText.text = controller ? "B" : escapeGlyph ? "" : "Esc";
            closeHint.gameObject.SetActive(IsOpen);
            for (int i = 0; i < displays.Length; i++) if (displays[i])
            {
                var tool = i == 0 ? workshop.brush : workshop.scraper;
                var home = i == 0 ? workshop.brushHome : workshop.scraperHome;
                displays[i].SetActive(tool.Held || Vector3.Distance(tool.transform.position,home.position) > .03f);
            }
        }

        void HandlePointer()
        {
            if (DiveCardMock.BlocksControls) return;
            bool click = Mouse.current?.leftButton.wasPressedThisFrame == true;
            if (workshop.xrGroup.activeSelf && !pickup.CenterMode) click |= triggerPressed;
            if (!pointerValid || !click) return;
            if (Contains(toolHint,pointerScreen)) SetOpen(!IsOpen);
            else if (IsOpen && Contains(closeHint,pointerScreen)) SetOpen(false);
            else if (IsOpen && expansion > .6f)
            {
                if (Contains(toggle,pointerScreen)) SetOpen(false);
                else for (int i=0;i<slots.Length;i++) if (Contains(slots[i],pointerScreen)) { TryEquip(i); break; }
            }
        }
        public void RecordPickup(CleaningToolKind kind) { owned[(int)kind] = true; }
        public bool TryEquip(int index)
        {
            if (index < 0 || index >= owned.Length || !owned[index]) return false;
            SetOpen(false);
            return pickup.EquipFromMenu(index == 0 ? pickup.brush : pickup.scraper);
        }
        public void SetOpen(bool value)
        {
            if (IsOpen == value) return;
            IsOpen = value;
            if (value)
            {
                previousCursor = Cursor.lockState; previousCursorVisible = Cursor.visible;
                if (simulator)
                {
                    originalMouseDelta = simulator.mouseRotationDeltaInput; originalMouseToggle = simulator.toggleMouseInput;
                    simulator.mouseRotationDeltaInput = new XRInputValueReader<Vector2> { inputSourceMode = XRInputValueReader.InputSourceMode.Unused };
                    simulator.toggleMouseInput = new XRInputButtonReader { inputSourceMode = XRInputButtonReader.InputSourceMode.Unused };
                }
                if (!XRSettings.isDeviceActive) { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
                HideHeld();
            }
            else
            {
                RestoreHeld();
                if (simulator && originalMouseDelta != null) { simulator.mouseRotationDeltaInput = originalMouseDelta; simulator.toggleMouseInput = originalMouseToggle; originalMouseDelta = null; }
                Cursor.lockState = previousCursor; Cursor.visible = previousCursorVisible;
                closedFrame = Time.frameCount; consumeClick = true;
            }
        }
        void HideHeld()
        {
            if (!pickup.ToolHeld) return;
            foreach (var renderer in pickup.ActiveTool.GetComponentsInChildren<Renderer>())
            { if (!hidden.ContainsKey(renderer)) hidden.Add(renderer,renderer.forceRenderingOff); renderer.forceRenderingOff = true; }
        }
        void RestoreHeld() { foreach (var pair in hidden) if (pair.Key) pair.Key.forceRenderingOff = pair.Value; hidden.Clear(); }
        void OnApplicationFocus(bool focused) { if (!focused && IsOpen) SetOpen(false); }
        void OnEnable() { if (canvas) canvas.gameObject.SetActive(true); }
        void OnDisable() { if (IsOpen) SetOpen(false); RestoreHeld(); if (canvas) canvas.gameObject.SetActive(false); }
        void OnDestroy()
        {
            if (simulator && originalCycle != null) simulator.cycleDevicesInput = originalCycle;
            cycle?.Dispose(); openAction?.Dispose(); backAction?.Dispose(); trigger?.Dispose();
            if (rayMaterial) Destroy(rayMaterial);
            if (Instance == this) Instance = null;
        }

        bool Contains(RectTransform rect, Vector2 screen) => RectTransformUtility.RectangleContainsScreenPoint(rect,screen,canvas.renderMode == RenderMode.WorldSpace ? pickup.View : null);
        void UpdatePointer()
        {
            pointerValid = false;
            bool tracked = XRSettings.isDeviceActive && workshop.xrGroup.activeSelf && rayOrigin;
            if (tracked && IsOpen)
            {
                var ray = new Ray(rayOrigin.position,rayOrigin.forward);
                var plane = new Plane(canvas.transform.forward,canvas.transform.position);
                if (plane.Raycast(ray,out float distance) && distance < 5)
                {
                    var hit = ray.GetPoint(distance); pointerScreen = pickup.View.WorldToScreenPoint(hit); pointerValid = true;
                    rayLine.SetPosition(0,ray.origin); rayLine.SetPosition(1,hit);
                    pointer.position = hit - canvas.transform.forward * .002f;
                }
            }
            else if (!XRSettings.isDeviceActive && Mouse.current != null && Cursor.lockState != CursorLockMode.Locked)
            { pointerScreen = Mouse.current.position.ReadValue(); pointerValid = true; }
            rayLine.enabled = tracked && IsOpen && pointerValid;
            pointer.gameObject.SetActive(rayLine.enabled);
        }
        GameObject MakeDisplay(ReefBrush tool, Transform home)
        {
            var display = new GameObject(tool.ToolName + " / persistent tutorial display"); display.transform.SetParent(home,false);
            display.transform.localScale = tool.transform.localScale;
            foreach (var mesh in tool.GetComponentsInChildren<MeshFilter>())
            {
                if (mesh.name.Contains("outline")) continue;
                var renderer = mesh.GetComponent<MeshRenderer>(); if (!renderer) continue;
                var part = new GameObject(mesh.name); part.transform.SetParent(display.transform,false);
                part.transform.localPosition = tool.transform.InverseTransformPoint(mesh.transform.position);
                part.transform.localRotation = Quaternion.Inverse(tool.transform.rotation) * mesh.transform.rotation;
                var scale = mesh.transform.lossyScale; var root = tool.transform.lossyScale;
                part.transform.localScale = new Vector3(scale.x/root.x,scale.y/root.y,scale.z/root.z);
                part.AddComponent<MeshFilter>().sharedMesh = mesh.sharedMesh;
                part.AddComponent<MeshRenderer>().sharedMaterials = renderer.sharedMaterials;
            }
            display.SetActive(false); return display;
        }
        void BuildUI()
        {
            var go = new GameObject("Pebble tool menu",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler)); go.transform.SetParent(transform,false); go.layer=5;
            canvas = go.GetComponent<Canvas>(); canvas.renderMode=RenderMode.ScreenSpaceOverlay; canvas.sortingOrder=90;
            var scaler=go.GetComponent<CanvasScaler>(); scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution=new Vector2(1280,720); scaler.matchWidthOrHeight=1;
            go.AddComponent<XRWorldHud>();
            var menu=Rect("Tool arc",go.transform,new Vector2(0,.5f),new Vector2(32,-40),Vector2.zero);
            toggle=Rect("Tools",menu,Vector2.one*.5f,new Vector2(65,0),new Vector2(116,82));
            toggle.localEulerAngles=new Vector3(0,0,7);
            var mainPebble=Pebble(toggle,yellow,.8f); mainPebble.softShadow=true; mainPebble.highlight=true;
            Label(toggle,"Tools",Vector2.zero,new Vector2(100,45),24);
            toggleGroup=toggle.gameObject.AddComponent<CanvasGroup>(); toggleGroup.alpha=0;
            toggle.gameObject.SetActive(false);
            for(int i=0;i<slots.Length;i++)
            {
                slots[i]=Rect("Tool slot "+i,menu,Vector2.one*.5f,positions[i],i<2?new Vector2(100,90):new Vector2(52,48));
                slots[i].localEulerAngles=new Vector3(0,0,i%2==0?7:-9);
                backgrounds[i]=Pebble(slots[i],blue,i*1.7f);
                groups[i]=slots[i].gameObject.AddComponent<CanvasGroup>(); groups[i].alpha=0;
                if(i<2)
                {
                    var icon=Rect("Actual model screenshot",slots[i],Vector2.one*.5f,new Vector2(0,9),new Vector2(76,57));
                    icons[i]=icon.gameObject.AddComponent<Image>(); icons[i].sprite=toolIcons[i]; icons[i].preserveAspect=true; icons[i].raycastTarget=false;
                    names[i]=Label(slots[i],i==0?"Brush":"Scraper",new Vector2(0,-29),new Vector2(92,22),13);
                    names[i].rectTransform.localEulerAngles=-slots[i].localEulerAngles;
                }
                else Label(slots[i],"·",Vector2.zero,new Vector2(40,42),22);
            }
            toolHint=KeyHint(go.transform,"Tools",new Vector2(82,65),tabGlyph,out openGlyph,out openKeyText);
            closeHint=KeyHint(go.transform,"Close",new Vector2(202,65),escapeGlyph,out closeGlyph,out closeKeyText);
            closeHint.gameObject.SetActive(false);
            pointer=Rect("Controller pointer",go.transform,Vector2.one*.5f,Vector2.zero,new Vector2(8,8)); Pebble(pointer,cream,0); pointer.gameObject.SetActive(false);
            var line=new GameObject("Tool menu pointer ray"); line.transform.SetParent(go.transform,false);
            rayLine=line.AddComponent<LineRenderer>(); rayLine.positionCount=2; rayLine.startWidth=.0015f; rayLine.endWidth=.0015f;
            // URP/Unlit is stripped from player builds when no material references it, and a null
            // shader threw here and aborted Awake on device. Sprites/Default always ships.
            var rayShader=Shader.Find("Universal Render Pipeline/Unlit"); if(!rayShader) rayShader=Shader.Find("Sprites/Default");
            rayMaterial=new Material(rayShader); rayMaterial.color=cream; rayLine.sharedMaterial=rayMaterial; rayLine.enabled=false;
        }
        static RectTransform Rect(string name,Transform parent,Vector2 anchor,Vector2 position,Vector2 size)
        {
            var rect=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();rect.gameObject.layer=5;rect.SetParent(parent,false);
            rect.anchorMin=rect.anchorMax=anchor;rect.pivot=Vector2.one*.5f;rect.anchoredPosition=position;rect.sizeDelta=size;return rect;
        }
        ReefPebbleGraphic Pebble(RectTransform rect,Color fill,float seed)
        { var p=rect.gameObject.AddComponent<ReefPebbleGraphic>();p.seed=seed;p.raycastTarget=false;p.Tint(fill);return p; }
        RectTransform KeyHint(Transform parent,string value,Vector2 position,Sprite sprite,out Image glyph,out Text keyText)
        {
            var root=Rect(value+" shortcut",parent,Vector2.zero,position,new Vector2(116,32));
            glyph=Rect("Key icon",root,Vector2.one*.5f,new Vector2(-42,0),new Vector2(32,28)).gameObject.AddComponent<Image>();
            if (sprite == tabGlyph) glyph.rectTransform.sizeDelta=new Vector2(36,36);
            glyph.sprite=sprite; glyph.preserveAspect=true; glyph.raycastTarget=false;
            glyph.color=new Color(.97f,.93f,.81f);
            keyText=Label(root,"",new Vector2(-42,0),new Vector2(36,28),14); keyText.color=glyph.color;
            var label=Label(root,value,new Vector2(14,0),new Vector2(64,24),14);
            label.font=GetComponent<ReefCleaningHUD>().font; label.color=glyph.color; label.alignment=TextAnchor.MiddleLeft;
            var shadow=label.gameObject.AddComponent<Shadow>();shadow.effectColor=new Color(0,.04f,.05f,.35f);shadow.effectDistance=new Vector2(0,-1);
            return root;
        }
        Text Label(Transform parent,string value,Vector2 position,Vector2 size,int pixels,Vector2? anchor=null)
        {
            var text=Rect(value,parent,anchor??Vector2.one*.5f,position,size).gameObject.AddComponent<Text>();
            text.text=value;text.font=font;text.fontSize=pixels;text.color=ink;text.alignment=TextAnchor.MiddleCenter;text.raycastTarget=false;return text;
        }
    }
}
