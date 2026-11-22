using System.Collections.Generic;
using UnityEngine;

namespace Reefkeeper.Corals
{
    public sealed class VinylPlayer : MonoBehaviour
    {
        [SerializeField] VinylDisplay[] displays;
        BoxCollider slot;
        ReefSoundscape soundscape;
        ShopRecord playing;
        Mesh ringMesh;
        MeshRenderer ring;
        MaterialPropertyBlock tint;
        int baseColorId;
        readonly List<ShopRecord> inserted = new List<ShopRecord>(3);

        public static void Install(CoralShop shop, CoralCart sales)
        {
            var front=Vector3.ProjectOnPlane(shop.transform.forward,Vector3.up).normalized;
            var right=Vector3.Cross(Vector3.up,front);
            var anchor=shop.counterSpot.position + right*.35f + front*.14f;
            var player = shop.GetComponentInChildren<VinylPlayer>();
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
                col.size=new Vector3(.166f,.166f,.011f);
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
                item.productName = color + " record";
                // Only the disc is grabbable. The sleeve participates in gaze targeting alone.
                var sleeve = set.transform.Find("Sleeve");
                sales.RegisterRecord(item, col, sleeve ? sleeve.GetComponent<MeshFilter>() : null);
                if (!player.ring)
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
            var visual = new GameObject("Vinyl placement hologram");
            visual.transform.SetPositionAndRotation(transform.TransformPoint(slot.center), transform.rotation);
            visual.transform.localScale = Vector3.one * diameter;
            visual.transform.SetParent(transform, true);

            // A single mesh outlines the record and spindle hole without shading the empty centre.
            const int segments = 40;
            float[] radii = { .50f, .475f, .035f, .018f };
            var vertices = new Vector3[segments * radii.Length];
            var triangles = new int[segments * 12];
            for (int i = 0; i < segments; i++)
            {
                float angle = i * (2f * Mathf.PI / segments);
                var direction = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                int here = i * 4;
                int next = ((i + 1) % segments) * 4;
                for (int edge = 0; edge < radii.Length; edge++)
                    vertices[here + edge] = direction * radii[edge];
                for (int band = 0; band < 2; band++)
                {
                    int a = here + band * 2, b = next + band * 2;
                    int t = i * 12 + band * 6;
                    triangles[t] = a; triangles[t + 1] = a + 1; triangles[t + 2] = b;
                    triangles[t + 3] = b; triangles[t + 4] = a + 1; triangles[t + 5] = b + 1;
                }
            }
            ringMesh = new Mesh { name = "Vinyl placement rings", vertices = vertices, triangles = triangles };
            ringMesh.RecalculateNormals();
            visual.AddComponent<MeshFilter>().sharedMesh = ringMesh;
            ring = visual.AddComponent<MeshRenderer>();
            ring.sharedMaterial = Resources.Load<Material>("PlacementHologram");
            ring.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ring.receiveShadows = false;
        }

        internal void HighlightForHeldRecord(Vector3 position)
        {
            if (!ring || playing) return;
            float distance = Vector3.Distance(position, slot.ClosestPoint(position));
            float strength = 1f - Mathf.SmoothStep(0f, 1f, distance / .45f);
            tint.SetColor(baseColorId, Color.Lerp(ring.sharedMaterial.GetColor(baseColorId),
                new Color(.42f, .9f, 1f, .85f), strength));
            ring.SetPropertyBlock(tint);
        }

        void OnTriggerEnter(Collider other)
        {
            if (!other.TryGetComponent<ShopRecord>(out var item) || item.player != this || !item.owned) return;
            if (!inserted.Contains(item)) inserted.Add(item);
            if (!playing) Play(item);
        }
        void OnTriggerExit(Collider other)
        {
            if (other.TryGetComponent<ShopRecord>(out var item)) RemoveRecord(item);
        }
        void RemoveRecord(ShopRecord item)
        {
            inserted.Remove(item);
            if (playing != item) return;
            StopMusic();
            if (inserted.Count > 0) Play(inserted[0]);
        }

        internal void Release(ShopRecord item)
        {
            if (ring) ring.SetPropertyBlock(null);
            bool inside=slot.ClosestPoint(item.transform.position)==item.transform.position;
            if(item.owned && inside && (!playing || playing==item)){
                item.transform.SetPositionAndRotation(transform.TransformPoint(slot.center),transform.rotation*Quaternion.Euler(90,0,0));
                if(!playing) Play(item);
            } else {
                RemoveRecord(item);
                item.transform.SetPositionAndRotation(item.home,item.homeRotation);
            }
        }
        void Play(ShopRecord item)
        {
            playing=item;
            if (ring) ring.enabled = false;
            var music=soundscape?soundscape.MusicSource:null;
            if(music && music.clip){music.mute=false;music.Play();}
        }
        void StopMusic()
        {
            var music=soundscape?soundscape.MusicSource:null;
            if(music){music.Stop();music.mute=true;}
            playing=null;
            if (ring) ring.enabled = true;
        }
        void OnDisable()
        {
            inserted?.Clear();
            if (playing) StopMusic();
        }
        void OnDestroy(){if(ringMesh) Destroy(ringMesh);}
    }
}
