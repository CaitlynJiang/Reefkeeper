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
        [Range(1,3)] public float vrCounterScale = 1.8f;
        [Min(.03f)] public float worldShellSize = .16f;
        public AudioClip collectClip;
        public int Balance { get; private set; }
        public int DisplayedBalance { get; private set; }
        public bool Visible => canvas && canvas.enabled && group.alpha > 0;
        public int InFlight { get; private set; }
        public event Action<int> Collected;
        /// <summary>Raised with the amount when a purchase is paid.</summary>
        public event Action<int> Spent;

        const int PoolSize = 18;
        sealed class Batch { public int remaining; public Vector3 origin; public bool spend; }
        sealed class Flight
        {
            public Sprite sprite;
            public bool active, spend;
            public float age, turn;
            public Vector3 origin, scatter;
        }
        readonly Queue<Batch> pending = new Queue<Batch>(16);
        readonly Flight[] flights = new Flight[PoolSize];
        readonly System.Random cosmeticRandom = new System.Random();
        ReefWorkshop workshop;
        Canvas canvas;
        CanvasGroup group;
        RectTransform root, counter;
        RectTransform counterIcon;
        XRWorldHud worldHud;
        ScallopWorldSprites worldSprites;
        Text number;
        Sprite shell;
        Sprite[] shellAngles;
        Material overlay;
        AudioSource audioSource;
        float spawnWait, idleAge, pulse, shortfall;
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
            worldSprites?.Dispose();
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

        /// <summary>
        /// Pays <paramref name="amount"/> if the balance covers it: debited immediately; the counter
        /// counts down as shells leave it and fly to <paramref name="worldTarget"/> (the reward flight
        /// in reverse). Returns false, changing nothing, when the balance is too low.
        /// </summary>
        public bool TrySpend(int amount, Vector3 worldTarget)
        {
            if (amount <= 0) return true;
            if (!isActiveAndEnabled || Balance < amount) return false;
            BuildPresentation();
            Balance -= amount;
            pending.Enqueue(new Batch { remaining = amount, origin = worldTarget, spend = true });
            idleAge = 0;
            canvas.enabled = true;
            Spent?.Invoke(amount);
            return true;
        }

        /// <summary>Shows the counter with a short red shake: not enough scallops.</summary>
        public void ShowShortfall()
        {
            if (!isActiveAndEnabled) return;
            BuildPresentation();
            shortfall = 1; idleAge = 0;
            canvas.enabled = true;
        }

        public void ResetSession()
        {
            Balance = 0;
            ClearPresentation();
        }
        void ClearPresentation()
        {
            pending.Clear();InFlight=0;spawnWait=idleAge=pulse=shortfall=0;
            DisplayedBalance=Balance;
            foreach(var f in flights) if(f != null)
            { f.active=false; }
            worldSprites?.Hide();
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
            var camera=Camera.main;
            bool vr=worldHud.InWorld;
            var target=vr?new Vector2(.80f,.80f):targetViewport;
            Vector2 size=root.rect.size;
            Vector2 destination=Vector2.Scale(size,new Vector2(Mathf.Clamp(target.x,.6f,.9f)-.5f,
                Mathf.Clamp(target.y,.6f,.9f)-.5f));
            shortfall=Mathf.MoveTowards(shortfall,0,dt*1.6f);
            counter.anchoredPosition=destination+new Vector2(Mathf.Sin(Time.unscaledTime*45)*7*shortfall,0);
            number.color=Color.Lerp(new Color32(255,194,172,255),new Color32(240,96,84,255),Mathf.Clamp01(shortfall*1.5f));
            counter.localScale=Vector3.one*(vr?vrCounterScale:1)*(1+.14f*Mathf.Sin(pulse*Mathf.PI*.5f));
            Vector3 end=FlightDestination(camera);
            spawnWait=Mathf.Max(0,spawnWait-dt);
            if(pending.Count>0 && spawnWait<=0)
            {
                foreach(var f in flights) if(!f.active)
                {
                    var batch=pending.Peek();
                    // Earned shells can still be in flight when a purchase is accepted.
                    // Wait for a visible unit before animating its debit, avoiding negative UI balances.
                    if(batch.spend&&DisplayedBalance<=0)break;
                    Spawn(f,batch.origin,camera,batch.spend);
                    if(batch.spend)
                    {
                        // Paying: the number drops as each shell leaves the counter.
                        DisplayedBalance--;number.text=DisplayedBalance.ToString();pulse=1;
                        if(collectClip)audioSource.PlayOneShot(collectClip,volume*.7f);
                    }
                    if(--batch.remaining==0)pending.Dequeue();
                    spawnWait=Mathf.Max(.03f,staggerSeconds);
                    break;
                }
            }
            worldSprites.Begin();
            foreach(var f in flights) if(f.active)
            {
                f.age+=dt;
                float t=f.age/Mathf.Max(.2f,flightSeconds);
                if(t>=1)
                {
                    f.active=false;InFlight--;
                    if(f.spend)continue; // A paid shell simply arrives at the purchase.
                    DisplayedBalance++;
                    number.text=DisplayedBalance.ToString();
                    pulse=1;idleAge=0;
                    // Number, pulse and audio share the same arrival event/frame.
                    if(collectClip)audioSource.PlayOneShot(collectClip,volume);
                    Collected?.Invoke(DisplayedBalance);
                    continue;
                }
                // Paid shells travel the same path backwards, from the counter to the purchase.
                float path=f.spend?1-t:t;
                float scale=path<.2f?Mathf.Lerp(.35f,1.15f,Mathf.Sin(path/.2f*Mathf.PI*.5f))
                    :Mathf.Lerp(1.15f,.38f,Mathf.Pow((path-.2f)/.8f,2));
                if(camera)
                {
                    var position=Position(f,end,path);
                    var facing=camera.transform.rotation*Quaternion.AngleAxis(Mathf.Sin(t*Mathf.PI)*f.turn,Vector3.forward);
                    float nearFade=Mathf.InverseLerp(.2f,.45f,Vector3.Dot(position-camera.transform.position,camera.transform.forward));
                    float trailPath=f.spend?Mathf.Min(1,path+.065f):Mathf.Max(0,path-.065f);
                    if(t>.25f)worldSprites.Draw(f.sprite,Position(f,end,trailPath),facing,worldShellSize*scale*.85f,nearFade*.16f);
                    worldSprites.Draw(f.sprite,position,facing,worldShellSize*scale,nearFade);
                }
            }
            worldSprites.End();
            pulse=Mathf.MoveTowards(pulse,0,dt*5);
            counter.localScale=Vector3.one*(vr?vrCounterScale:1)*(1+.14f*Mathf.Sin(pulse*Mathf.PI*.5f));
            bool busy=pending.Count>0||InFlight>0||shortfall>0;
            idleAge=busy?0:idleAge+dt;
            float alpha=busy||idleAge<holdSeconds?1:1-Mathf.Clamp01((idleAge-holdSeconds)/Mathf.Max(.05f,fadeSeconds));
            group.alpha=Mathf.MoveTowards(group.alpha,alpha,dt*10);
            if(!busy && group.alpha<=0)canvas.enabled=false;
        }

        void Spawn(Flight f,Vector3 origin,Camera camera,bool spend=false)
        {
            f.spend=spend;
            int n=sequence++;
            // Pick once per shell, so it does not flicker between views during flight.
            var angle=shellAngles[cosmeticRandom.Next(shellAngles.Length)];
            f.sprite=angle;
            var towards=camera?(camera.transform.position-origin).normalized:Vector3.forward;
            var right=camera?camera.transform.right:Vector3.right;
            f.active=true;f.age=0;f.origin=origin+towards*.04f;f.turn=Mathf.Sin(n*2.4f)*28;
            f.scatter=right*(Mathf.Sin(n*2.4f)*.075f)+Vector3.up*(.10f+Mathf.Cos(n*1.7f)*.035f);
            InFlight++;
        }
        static Vector3 Position(Flight f,Vector3 end,float t)
        {
            if(t<.2f)return f.origin+f.scatter*Mathf.Sin(t/.2f*Mathf.PI*.5f);
            float u=Mathf.Pow(Mathf.Clamp01((t-.2f)/.8f),1.55f),v=1-u;
            Vector3 start=f.origin+f.scatter;
            Vector3 control=Vector3.Lerp(start,end,.4f)+Vector3.up*.22f;
            return v*v*start+2*v*u*control+u*u*end;
        }
        Vector3 FlightDestination(Camera camera)
        {
            if(canvas.renderMode==RenderMode.WorldSpace)return counterIcon.position;
            if(!camera)return transform.position+Vector3.forward;
            var screen=RectTransformUtility.WorldToScreenPoint(canvas.renderMode==RenderMode.ScreenSpaceOverlay?null:canvas.worldCamera,counterIcon.position);
            // Match the counter's screen position while keeping arrival safely in front of the eyes.
            return camera.ScreenToWorldPoint(new Vector3(screen.x,screen.y,1.1f));
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
            worldSprites=new ScallopWorldSprites(transform,atlas,PoolSize*2);
            if(!collectClip)collectClip=Resources.Load<AudioClip>("ScallopReward/Collect");
            overlay=new Material(Resources.Load<Shader>("DiveCards/JournalOverlay")){name="Scallop collection overlay"};
            var go=new GameObject("Scallop collection feedback",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(CanvasGroup));
            go.layer=5;go.transform.SetParent(transform,false);root=(RectTransform)go.transform;
            canvas=go.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=95;
            var scaler=go.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution=new Vector2(1280,720);scaler.matchWidthOrHeight=1;
            worldHud=go.AddComponent<XRWorldHud>();
            group=go.GetComponent<CanvasGroup>();group.blocksRaycasts=false;group.interactable=false;group.alpha=0;
            counter=Rect("Temporary balance",root,new Vector2(150,60));
            var badge=Icon("Scallop",counter,52);badge.rectTransform.anchoredPosition=new Vector2(-26,0);
            counterIcon=badge.rectTransform;
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
                flights[i]=new Flight();
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
