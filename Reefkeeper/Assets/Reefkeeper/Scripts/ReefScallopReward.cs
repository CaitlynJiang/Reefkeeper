using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Reefkeeper
{
    /// <summary>Temporary collection feedback, shared by desktop and the XR world-space UI.</summary>
    [DisallowMultipleComponent, DefaultExecutionOrder(350)]
    public sealed class ReefScallopReward : MonoBehaviour
    {
        [Range(1,12)] public int rewardPerPatch = 3;
        [Min(.2f)] public float flightSeconds = .95f;
        [Min(.03f)] public float staggerSeconds = .12f;
        [Min(0)] public float holdSeconds = .8f;
        [Min(.05f)] public float fadeSeconds = .45f;
        [Range(0,1)] public float volume = .35f;
        public Vector2 targetViewport = new Vector2(.86f,.83f);
        public AudioClip collectClip;
        public int Balance { get; private set; }
        public int DisplayedBalance { get; private set; }
        public bool Visible => canvas && canvas.enabled && group.alpha > 0;
        public int InFlight { get; private set; }
        public event Action<int> Collected;

        const int PoolSize = 18;
        sealed class Batch { public int remaining; public Vector3 origin; }
        sealed class Flight
        {
            public Image image, trail;
            public bool active;
            public float age, turn;
            public Vector2 origin, scatter;
        }
        readonly Queue<Batch> pending = new Queue<Batch>(16);
        readonly Flight[] flights = new Flight[PoolSize];
        readonly System.Random cosmeticRandom = new System.Random();
        ReefWorkshop workshop;
        Canvas canvas;
        CanvasGroup group;
        RectTransform root, counter;
        Text number;
        Sprite shell;
        Sprite[] shellAngles;
        Material overlay;
        AudioSource audioSource;
        float spawnWait, idleAge, pulse;
        int sequence;

        void Awake() => workshop = GetComponent<ReefWorkshop>();
        void OnEnable() => ReefBrush.PatchCleaned += OnCleaned;
        void Start() => BuildPresentation();
        void OnDisable()
        {
            ReefBrush.PatchCleaned -= OnCleaned;
            ClearPresentation();
        }
        void OnDestroy()
        {
            if (canvas) Destroy(canvas.gameObject);
            if (overlay) Destroy(overlay);
            if (audioSource) Destroy(audioSource);
            if (shellAngles != null) foreach (var angle in shellAngles) if (angle) Destroy(angle);
        }
        void OnCleaned(ReefBrush tool, RackDirtPatch patch, Vector3 at)
        {
            if (!workshop || (tool != workshop.brush && tool != workshop.scraper)) return;
            Award(rewardPerPatch, at);
        }

        /// <summary>Credit this session immediately; show and sound each unit only on visual arrival.</summary>
        public void Award(int amount, Vector3 worldOrigin)
        {
            if (amount <= 0 || !isActiveAndEnabled) return;
            amount = Math.Min(amount, int.MaxValue - Balance);
            if (amount == 0) return;
            BuildPresentation();
            Balance += amount;
            pending.Enqueue(new Batch { remaining = amount, origin = worldOrigin });
            idleAge = 0;
            canvas.enabled = true;
            // Intentionally no sound or displayed-number change here.
        }

        public void ResetSession()
        {
            Balance = 0;
            ClearPresentation();
        }
        void ClearPresentation()
        {
            pending.Clear();InFlight=0;spawnWait=idleAge=pulse=0;
            DisplayedBalance=Balance;
            foreach(var f in flights) if(f != null)
            { f.active=false;f.image.gameObject.SetActive(false);f.trail.gameObject.SetActive(false); }
            if(number)number.text=DisplayedBalance.ToString();
            if(group)group.alpha=0;
            if(canvas)canvas.enabled=false;
            if(audioSource)audioSource.Stop();
        }

        void LateUpdate() => Advance(Time.unscaledDeltaTime);
        void Advance(float dt)
        {
            if (!canvas || !canvas.enabled) return;
            dt=Mathf.Clamp(dt,0,.1f);
            Vector2 size=root.rect.size;
            Vector2 destination=Vector2.Scale(size,new Vector2(Mathf.Clamp(targetViewport.x,.6f,.9f)-.5f,
                Mathf.Clamp(targetViewport.y,.6f,.9f)-.5f));
            counter.anchoredPosition=destination;
            spawnWait=Mathf.Max(0,spawnWait-dt);
            if(pending.Count>0 && spawnWait<=0)
            {
                foreach(var f in flights) if(!f.active)
                {
                    var batch=pending.Peek();
                    Spawn(f,ProjectOrigin(batch.origin,size));
                    if(--batch.remaining==0)pending.Dequeue();
                    spawnWait=Mathf.Max(.03f,staggerSeconds);
                    break;
                }
            }
            foreach(var f in flights) if(f.active)
            {
                f.age+=dt;
                float t=f.age/Mathf.Max(.2f,flightSeconds);
                if(t>=1)
                {
                    f.active=false;f.image.gameObject.SetActive(false);f.trail.gameObject.SetActive(false);InFlight--;
                    DisplayedBalance++;
                    number.text=DisplayedBalance.ToString();
                    pulse=1;idleAge=0;
                    // Number, pulse and audio share the same arrival event/frame.
                    if(collectClip)audioSource.PlayOneShot(collectClip,volume);
                    Collected?.Invoke(DisplayedBalance);
                    continue;
                }
                f.image.rectTransform.anchoredPosition=Position(f,destination,t);
                float scale=t<.2f?Mathf.Lerp(.35f,1.15f,Mathf.Sin(t/.2f*Mathf.PI*.5f))
                    :Mathf.Lerp(1.15f,.38f,Mathf.Pow((t-.2f)/.8f,2));
                f.image.rectTransform.localScale=Vector3.one*scale;
                f.image.rectTransform.localEulerAngles=new Vector3(0,0,Mathf.Sin(t*Mathf.PI)*f.turn);
                f.trail.gameObject.SetActive(t>.25f);
                f.trail.rectTransform.anchoredPosition=Position(f,destination,Mathf.Max(0,t-.065f));
                f.trail.rectTransform.localScale=Vector3.one*scale*.85f;
                f.trail.rectTransform.localRotation=f.image.rectTransform.localRotation;
            }
            pulse=Mathf.MoveTowards(pulse,0,dt*5);
            counter.localScale=Vector3.one*(1+.14f*Mathf.Sin(pulse*Mathf.PI*.5f));
            bool busy=pending.Count>0||InFlight>0;
            idleAge=busy?0:idleAge+dt;
            float alpha=busy||idleAge<holdSeconds?1:1-Mathf.Clamp01((idleAge-holdSeconds)/Mathf.Max(.05f,fadeSeconds));
            group.alpha=Mathf.MoveTowards(group.alpha,alpha,dt*10);
            if(!busy && group.alpha<=0)canvas.enabled=false;
        }

        void Spawn(Flight f,Vector2 origin)
        {
            int n=sequence++;
            // Pick once per shell, so it does not flicker between views during flight.
            var angle=shellAngles[cosmeticRandom.Next(shellAngles.Length)];
            f.image.sprite=angle;f.trail.sprite=angle;
            f.active=true;f.age=0;f.origin=origin;f.turn=Mathf.Sin(n*2.4f)*28;
            f.scatter=new Vector2(Mathf.Sin(n*2.4f)*42,32+Mathf.Cos(n*1.7f)*16);
            f.image.gameObject.SetActive(true);f.trail.gameObject.SetActive(false);InFlight++;
        }
        static Vector2 Position(Flight f,Vector2 end,float t)
        {
            if(t<.2f)return f.origin+f.scatter*Mathf.Sin(t/.2f*Mathf.PI*.5f);
            float u=Mathf.Pow(Mathf.Clamp01((t-.2f)/.8f),1.55f),v=1-u;
            Vector2 start=f.origin+f.scatter;
            Vector2 control=start+new Vector2(-45,125);
            return v*v*start+2*v*u*control+u*u*end;
        }
        Vector2 ProjectOrigin(Vector3 world,Vector2 size)
        {
            var camera=Camera.main;
            Vector3 p=camera?camera.WorldToViewportPoint(world):new Vector3(.45f,.4f,1);
            if(p.z<=0||float.IsNaN(p.x)||float.IsNaN(p.y))p=new Vector3(.45f,.4f,1);
            // Keep the initial pop in view and away from the destination, including offscreen cleaning.
            return Vector2.Scale(size,new Vector2(Mathf.Clamp(p.x,.12f,.65f)-.5f,Mathf.Clamp(p.y,.18f,.68f)-.5f));
        }

        void BuildPresentation()
        {
            if(canvas)return;
            var atlas=Resources.Load<Texture2D>("ScallopReward/Scallop");
            shellAngles=new Sprite[6];
            float tileWidth=atlas.width/3f,tileHeight=atlas.height/2f;
            for(int i=0;i<shellAngles.Length;i++)
            {
                var rect=new Rect((i%3)*tileWidth,(1-i/3)*tileHeight,tileWidth,tileHeight);
                shellAngles[i]=Sprite.Create(atlas,rect,Vector2.one*.5f,100,0,SpriteMeshType.FullRect);
                shellAngles[i].name="Scallop angle "+(i+1);
            }
            shell=shellAngles[2]; // Upper-right view in the approved six-angle comparison.
            if(!collectClip)collectClip=Resources.Load<AudioClip>("ScallopReward/Collect");
            overlay=new Material(Resources.Load<Shader>("DiveCards/JournalOverlay")){name="Scallop collection overlay"};
            var go=new GameObject("Scallop collection feedback",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(CanvasGroup));
            go.layer=5;go.transform.SetParent(transform,false);root=(RectTransform)go.transform;
            canvas=go.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=95;
            var scaler=go.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution=new Vector2(1280,720);scaler.matchWidthOrHeight=1;
            go.AddComponent<XRWorldHud>();
            group=go.GetComponent<CanvasGroup>();group.blocksRaycasts=false;group.interactable=false;group.alpha=0;
            counter=Rect("Temporary balance",root,new Vector2(150,60));
            var badge=Icon("Scallop",counter,52);badge.rectTransform.anchoredPosition=new Vector2(-26,0);
            var label=Rect("Balance",counter,new Vector2(125,52));label.anchoredPosition=new Vector2(56,0);
            number=label.gameObject.AddComponent<Text>();var hud=GetComponent<ReefCleaningHUD>();
            number.font=Resources.Load<Font>("ScallopReward/LilitaOne-Regular");
            if(!number.font)number.font=hud?(hud.boldFont?hud.boldFont:hud.font):Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            number.fontSize=32;number.resizeTextForBestFit=true;number.resizeTextMinSize=18;number.resizeTextMaxSize=32;
            number.alignment=TextAnchor.MiddleLeft;number.color=new Color32(255,194,172,255);number.material=overlay;
            number.raycastTarget=false;number.text=DisplayedBalance.ToString();
            var outline=label.gameObject.AddComponent<Outline>();outline.effectColor=new Color(.1f,.22f,.25f,.85f);outline.effectDistance=new Vector2(1.3f,-1.3f);
            for(int i=0;i<PoolSize;i++)
            {
                var trail=Icon("Scallop trail",root,58);trail.color=new Color(1,1,1,.16f);
                var icon=Icon("Flying scallop",root,58);
                flights[i]=new Flight{image=icon,trail=trail};icon.gameObject.SetActive(false);trail.gameObject.SetActive(false);
            }
            counter.SetAsLastSibling();
            audioSource=gameObject.AddComponent<AudioSource>();audioSource.playOnAwake=false;audioSource.loop=false;
            audioSource.spatialBlend=0;audioSource.volume=1;audioSource.priority=80;
            canvas.enabled=false;
        }
        RectTransform Rect(string name,Transform parent,Vector2 size)
        {
            var go=new GameObject(name,typeof(RectTransform));go.layer=5;var rect=(RectTransform)go.transform;
            rect.SetParent(parent,false);rect.anchorMin=rect.anchorMax=rect.pivot=Vector2.one*.5f;rect.sizeDelta=size;return rect;
        }
        Image Icon(string name,Transform parent,float size)
        {
            var image=Rect(name,parent,Vector2.one*size).gameObject.AddComponent<Image>();
            image.sprite=shell;image.preserveAspect=true;image.material=overlay;image.raycastTarget=false;return image;
        }
    }
}
