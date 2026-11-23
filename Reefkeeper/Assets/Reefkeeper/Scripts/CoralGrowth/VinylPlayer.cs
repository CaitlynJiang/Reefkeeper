using UnityEngine;

namespace Reefkeeper.Corals
{
    public sealed class VinylPlayer : MonoBehaviour
    {
        [SerializeField] VinylDisplay[] displays;
        [Tooltip("Optional scene-authored rack. Edit its Transform and each child record's Transform in Edit Mode; shop progression preserves their placement.")]
        public Transform recordRack;
        [Tooltip("Scene-authored turntable. Runtime preserves its Transform.")]
        public Transform turntableModel;
        public VinylTonearm tonearm;
        [Tooltip("Visual child of this placement target. Move this target to move the ring and record landing point together.")]
        public MeshRenderer placementRing;
        [Tooltip("Degrees per second for the broken light arcs.")]
        public float ringRotationSpeed=12f;
        [Min(0)] public float musicFadeIn=1.2f,musicFadeOut=.8f;
        BoxCollider slot;
        ReefSoundscape soundscape;
        ShopRecord playing,seated;
        MaterialPropertyBlock tint;
        int baseColorId;
        /// <summary>The record currently playing, or null (read by LuckyCatProgress to spin the turntable).</summary>
        public ShopRecord Playing => playing;
        public ShopRecord Seated => seated;
        /// <summary>Where a record sits to play.</summary>
        public BoxCollider Slot => slot ? slot : slot=GetComponent<BoxCollider>();
        public Vector3 SeatPosition => transform.TransformPoint(Slot.center);
        public Vector3 RecordSurfacePosition => SeatPosition+transform.up*(seated?seated.GetComponent<MeshFilter>().sharedMesh.bounds.extents.z*Mathf.Abs(seated.transform.lossyScale.z):0);

        public static void Install(CoralShop shop, CoralCart sales)
        {
            var front=Vector3.ProjectOnPlane(shop.transform.forward,Vector3.up).normalized;
            var right=Vector3.Cross(Vector3.up,front);
            var anchor=shop.counterSpot.position + right*.35f + front*.14f;
            var player = shop.GetComponentInChildren<VinylPlayer>(true);
            bool placedInScene = player != null;
            if (!placedInScene)
            {
                var station = new GameObject("Lucky Cat - record player");
                station.transform.SetPositionAndRotation(anchor + right * .69f, Quaternion.LookRotation(front));
                station.transform.SetParent(shop.transform, true);
                player = station.AddComponent<VinylPlayer>();
                player.slot = station.AddComponent<BoxCollider>();
                player.slot.isTrigger = true;
                player.slot.size = new Vector3(.24f, .105f, .24f);
            }
            else player.slot = player.GetComponent<BoxCollider>();

            player.soundscape = shop.workshop.GetComponent<ReefSoundscape>();
            var wallet = shop.workshop.GetComponent<ReefScallopReward>();
            var colors = new[] { "Yellow", "Orange", "Teal" };
            int count = placedInScene ? player.displays.Length : colors.Length;
            for (int i = 0; i < count; i++)
            {
                GameObject set, disc;
                string color;
                if (placedInScene)
                {
                    var display = player.displays[i];
                    if (!display || !display.record) continue;
                    color = display.variant;
                    set = display.gameObject;
                    disc = display.record.gameObject;
                }
                else
                {
                    color = colors[i];
                    var model = Resources.Load<GameObject>("VinylShop/" + color);
                    if (!model) continue;
                    set = Instantiate(model, anchor + right * i * .20f + Vector3.up * .10f,
                        Quaternion.LookRotation(-front));
                    set.transform.SetParent(shop.transform, true);
                    disc = set.transform.Find("Record").gameObject;
                    disc.transform.localPosition = Vector3.up * .037f;
                }
                var col=disc.AddComponent<BoxCollider>();
                col.size=new Vector3(.166f,.166f,Mathf.Max(disc.GetComponent<MeshFilter>().sharedMesh.bounds.size.z,.014f/Mathf.Max(.001f,Mathf.Abs(disc.transform.lossyScale.z))));
                var body=disc.AddComponent<Rigidbody>();
                body.isKinematic=true; body.useGravity=false;
                var item=disc.AddComponent<ShopRecord>();
                item.throwOnDetach=false;
                item.useDynamicAttach=true;
                item.home=disc.transform.position;
                item.homeRotation=disc.transform.rotation;
                item.wallet=wallet; item.player=player;
                item.unlockKey="Reefkeeper.Vinyl."+color;
                item.owned=PlayerPrefs.GetInt(item.unlockKey)==1;
                item.sales = sales;
                item.music=placedInScene?player.displays[i].music:null;
                item.productName = color + " record";
                // Only the disc is grabbable. The sleeve participates in gaze targeting alone.
                var sleeve = set.transform.Find("Sleeve");
                sales.RegisterRecord(item, col, sleeve ? sleeve.GetComponent<MeshFilter>() : null);
                if (i==0)
                {
                    var mesh = disc.GetComponent<MeshFilter>().sharedMesh;
                    float diameter = mesh ? mesh.bounds.size.x * Mathf.Abs(disc.transform.lossyScale.x) : .166f;
                    player.CreateProjection(diameter);
                }
            }
        }

        void OnDrawGizmos()
        {
            if (Application.isPlaying || !TryGetComponent<BoxCollider>(out var box)) return;
            var previousMatrix = Gizmos.matrix;
            var previousColor = Gizmos.color;
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = new Color(.12f, .58f, 1f);
            Gizmos.DrawWireCube(box.center, box.size);
            Gizmos.matrix = previousMatrix;
            Gizmos.color = previousColor;
        }

        void CreateProjection(float diameter)
        {
            tint = new MaterialPropertyBlock();
            baseColorId = Shader.PropertyToID("_BaseColor");
            if(!placementRing)
            {
                var visual = new GameObject("Placement light arcs");
                visual.transform.SetPositionAndRotation(SeatPosition, transform.rotation);
                visual.transform.localScale = Vector3.one * diameter;
                visual.transform.SetParent(transform, true);
                visual.AddComponent<MeshFilter>().sharedMesh=Resources.Load<Mesh>("VinylShop/PlacementRingMesh");
                placementRing=visual.AddComponent<MeshRenderer>();
                placementRing.sharedMaterial=Resources.Load<Material>("VinylShop/PlacementRing");
            }
            placementRing.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
            placementRing.receiveShadows=false;
            placementRing.enabled=!seated;
        }

        void LateUpdate()
        {
            if(placementRing&&placementRing.enabled)
                placementRing.transform.Rotate(Vector3.up,ringRotationSpeed*Time.deltaTime,Space.Self);
            if(playing&&(!tonearm||!NeedleOverRecord(tonearm.StylusPosition)))StopMusic();
        }

        internal void HighlightForHeldRecord(Vector3 position)
        {
            if (!placementRing || seated) return;
            float distance = Vector3.Distance(position, slot.ClosestPoint(position));
            float strength = 1f - Mathf.SmoothStep(0f, 1f, distance / .45f);
            tint.SetColor(baseColorId, Color.Lerp(placementRing.sharedMaterial.GetColor(baseColorId),
                new Color(.42f, .9f, 1f, .85f), strength));
            placementRing.SetPropertyBlock(tint);
        }

        // Merely touching the slot never starts music or steals a held record.
        void OnTriggerExit(Collider other)
        {
            if(other.TryGetComponent<ShopRecord>(out var item)&&item==seated)LiftRecord(item);
        }
        internal void LiftRecord(ShopRecord item)
        {
            if(item!=seated)return;
            StopMusic();seated=null;
            if(placementRing)placementRing.enabled=true;
            if(tonearm)tonearm.CancelPlayback();
        }

        internal void Release(ShopRecord item)
        {
            if (placementRing) placementRing.SetPropertyBlock(null);
            bool inside=slot.ClosestPoint(item.transform.position)==item.transform.position;
            if(item.owned && inside && (!seated || seated==item)){
                item.transform.SetPositionAndRotation(SeatPosition,transform.rotation*Quaternion.Euler(90,0,0));
                StopMusic();seated=item;
                if(placementRing)placementRing.enabled=false;
                if(tonearm)tonearm.BeginPlayback();
            } else {
                LiftRecord(item);
                item.transform.SetPositionAndRotation(item.home,item.homeRotation);
            }
        }
        public bool NeedleOverRecord(Vector3 needle,bool checkHeight=true)
        {
            if(!seated||seated.isSelected)return false;
            var mesh=seated.GetComponent<MeshFilter>().sharedMesh;
            float radius=mesh.bounds.extents.x*Mathf.Abs(seated.transform.lossyScale.x);
            var delta=Quaternion.Inverse(transform.rotation)*(needle-RecordSurfacePosition);
            float radial=new Vector2(delta.x,delta.z).magnitude;
            return radial>radius*.18f&&radial<radius*.96f&&(!checkHeight||Mathf.Abs(delta.y)<.018f);
        }
        public void TryPlayFromTonearm()
        {
            if(tonearm&&!tonearm.IsCueing&&NeedleOverRecord(tonearm.StylusPosition))Play(seated);
        }
        public void LiftTonearm(){if(playing)StopMusic();}
        void Play(ShopRecord item)
        {
            playing=item;
            if (placementRing) placementRing.enabled = false;
            if(soundscape)soundscape.PlayMusic(item.music?item.music:soundscape.music,musicFadeIn);
        }
        void StopMusic()
        {
            if(playing&&soundscape)soundscape.StopMusic(musicFadeOut);
            playing=null;
            if (placementRing) placementRing.enabled = !seated;
        }
        void OnDisable()
        {
            if (playing) StopMusic();
        }
    }
}
