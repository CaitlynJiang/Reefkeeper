using System.Collections;
using UnityEngine;

namespace Reefkeeper.Photos
{
    /// <summary>
    /// The standing photo board by the cat stall (Blender: photo_board.blend). Each of its 16 photos
    /// belongs to one species (<see cref="ReefSpecies.All"/>, same order). Until that species is
    /// photographed the slot shows a faded silhouette and "???"; the first photo replaces it with a pop.
    /// The prefab root faces +Z (towards the viewer), built by PhotoBoardBuilder.
    /// </summary>
    public sealed class ReefPhotoWall : MonoBehaviour
    {
        [Tooltip("Picture quads (the *_Pic meshes), one per species in ReefSpecies order.")]
        public Renderer[] slots = new Renderer[0];
        [Tooltip("The paper card each picture is on; it pops when a new photo arrives.")]
        public Transform[] cards = new Transform[0];
        [Tooltip("Write the species name under each photo.")] public bool labels = true;

        static readonly int BaseMap = Shader.PropertyToID("_BaseMap"), BaseColor = Shader.PropertyToID("_BaseColor"), Emission = Shader.PropertyToID("_Emission");
        static readonly Color Ink = new Color(.24f, .17f, .13f), Paper = new Color(.84f, .87f, .86f);
        TextMesh[] names;
        Texture[] placeholders;
        MaterialPropertyBlock block;

        void Start()
        {
            block = new MaterialPropertyBlock();
            names = new TextMesh[slots.Length];
            placeholders = new Texture[slots.Length];
            for (int i = 0; i < slots.Length; i++) Show(i, false);
            // More photos on the board than species: take the spare cards (and their clips) down.
            for (int i = ReefSpecies.Count; i < cards.Length; i++)
            {
                if (!cards[i]) continue;
                cards[i].gameObject.SetActive(false);
                var clip = cards[i].parent && cards[i].parent.parent ? cards[i].parent.parent.Find("Pb_Clip_" + cards[i].name.Substring("Photo_".Length)) : null;
                if (clip) clip.gameObject.SetActive(false);
            }
            ReefPhotoAlbum.Changed += OnPhoto;
        }

        void OnDestroy()
        {
            ReefPhotoAlbum.Changed -= OnPhoto;
            if (placeholders != null) foreach (var t in placeholders) if (t) Destroy(t);
        }

        void OnPhoto(string species)
        {
            if (species == null) { for (int i = 0; i < slots.Length; i++) Show(i, false); return; }
            int index = ReefSpecies.IndexOf(species);
            if (index >= 0 && index < slots.Length) Show(index, true);
        }

        void Show(int i, bool pop)
        {
            var slot = slots[i];
            if (!slot || i >= ReefSpecies.Count) return;
            var (id, displayName, _) = ReefSpecies.All[i];
            var photo = ReefPhotoAlbum.Get(id);
            slot.GetPropertyBlock(block);
            block.SetTexture(BaseMap, photo ? photo : Placeholder(i, id));
            block.SetColor(BaseColor, Color.white);
            block.SetFloat(Emission, photo ? .45f : .2f);
            slot.SetPropertyBlock(block);
            if (labels) Label(i, photo ? displayName : "???");
            if (pop && i < cards.Length && cards[i]) StartCoroutine(Pop(cards[i]));
        }

        /// <summary>Paper-coloured card with the species' sticker as a faded silhouette (corals), or blank.</summary>
        Texture Placeholder(int i, string id)
        {
            if (placeholders[i]) return placeholders[i];
            var rt = new RenderTexture(300, 200, 0, RenderTextureFormat.ARGB32) { name = "Photo placeholder " + id };
            rt.Create();
            var previous = RenderTexture.active;
            RenderTexture.active = rt;
            GL.Clear(true, true, Paper);
            var icon = Resources.Load<Texture2D>("DiveCards/CoralStickers/" + id + "_S3")
                ?? (id == "Fish1" ? Resources.Load<Texture2D>("DiveCards/Marine/Visitor0") : id == "Fish3" ? Resources.Load<Texture2D>("DiveCards/Marine/Visitor1") : null);
            if (icon)
            {
                GL.PushMatrix();
                GL.LoadPixelMatrix(0, 300, 200, 0);
                float h = 170, w = Mathf.Min(270, h * icon.width / icon.height);
                h = w * icon.height / icon.width;
                // Graphics.DrawTexture halves the tint; this gives a soft, see-through grey-blue ghost.
                Graphics.DrawTexture(new Rect(150 - w * .5f, 100 - h * .5f, w, h), icon, new Rect(0, 0, 1, 1), 0, 0, 0, 0, new Color(.2f, .28f, .34f, .3f));
                GL.PopMatrix();
            }
            RenderTexture.active = previous;
            placeholders[i] = rt;
            return rt;
        }

        void Label(int i, string text)
        {
            if (!names[i])
            {
                var slot = slots[i];
                var b = slot.bounds;
                var go = new GameObject("Name " + ReefSpecies.All[i].id);
                go.transform.SetParent(i < cards.Length && cards[i] ? cards[i] : slot.transform, true);
                // Under the picture on the paper's bottom margin, facing out like the picture.
                go.transform.SetPositionAndRotation(b.center - transform.up * .081f + transform.forward * .004f, Quaternion.LookRotation(-transform.forward, transform.up));
                var t = go.AddComponent<TextMesh>();
                var hud = FindFirstObjectByType<ReefCleaningHUD>(FindObjectsInactive.Include);
                t.font = hud && hud.boldFont ? hud.boldFont : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                go.GetComponent<MeshRenderer>().sharedMaterial = t.font.material;
                t.anchor = TextAnchor.MiddleCenter; t.alignment = TextAlignment.Center;
                t.fontSize = 64; t.characterSize = .0045f; t.color = Ink;
                names[i] = t;
            }
            names[i].text = text;
        }

        static IEnumerator Pop(Transform card)
        {
            var baseScale = card.localScale;
            float velocity = 0, scale = 1.35f;
            for (float t = 0; t < 1.2f; t += Time.deltaTime)
            {
                velocity += (-(scale - 1) * 260f - velocity * 11f) * Time.deltaTime;
                scale += velocity * Time.deltaTime;
                card.localScale = baseScale * scale;
                yield return null;
            }
            card.localScale = baseScale;
        }

        /// <summary>
        /// Scenes saved before the board existed get one at runtime, from Resources/Photos/PhotoBoard,
        /// standing beside the cat stall.
        /// </summary>
        public static void EnsureInScene()
        {
            if (FindFirstObjectByType<ReefPhotoWall>(FindObjectsInactive.Include)) return;
            var stall = GameObject.Find("CatStall");
            var prefab = Resources.Load<GameObject>("Photos/PhotoBoard");
            if (!stall || !prefab) return;
            var board = Instantiate(prefab);
            board.name = "Photo board 照片墙";
            PlaceBeside(board.transform, stall.transform);
        }

        /// <summary>Stands the board on the seabed to the right of the stall (past the basket), turned a little toward its customers.</summary>
        public static void PlaceBeside(Transform board, Transform stall)
        {
            // CatStall faces its +Z (the cat stands behind the counter, on the -Z side).
            var front = Vector3.ProjectOnPlane(stall.forward, Vector3.up).normalized;
            if (front.sqrMagnitude < .01f) front = Vector3.forward;
            var right = Vector3.Cross(Vector3.up, front);
            var spot = stall.position + right * 3.1f + front * .2f;
            if (Physics.Raycast(spot + Vector3.up * 6, Vector3.down, out var hit, 20, ~0, QueryTriggerInteraction.Ignore)) spot.y = hit.point.y;
            board.SetPositionAndRotation(spot, Quaternion.LookRotation((front - right * .35f).normalized, Vector3.up));
        }
    }
}
