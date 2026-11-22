using System.Collections;
using System.Collections.Generic;
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
        [Min(.05f)] public float collectionDistance = .4f;
        [Min(.1f)] public float revealSeconds = .8f;
        static readonly HashSet<ReefPhotoWall> activeWalls = new HashSet<ReefPhotoWall>();
        Material developMaterial;
        Material[] originalMaterials;
        Coroutine[] reveals;
        bool ready;
        static readonly int Reveal = Shader.PropertyToID("_Reveal"), PlaceholderMap = Shader.PropertyToID("_PlaceholderMap");

        void OnEnable() => activeWalls.Add(this);
        void OnDisable()
        {
            activeWalls.Remove(this);
            if (ready) for (int i = 0; i < slots.Length; i++) Show(i, false);
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetWalls() => activeWalls.Clear();

        public bool TryGetSlotPose(string species, out Pose pose)
        {
            pose = default;
            int i = ReefSpecies.IndexOf(species);
            if (!ready || i < 0 || i >= slots.Length || !slots[i] || !slots[i].enabled || !slots[i].gameObject.activeInHierarchy) return false;
            pose = new Pose(slots[i].bounds.center + transform.forward * .025f, Quaternion.LookRotation(-transform.forward, transform.up));
            return true;
        }

        public static bool TryFindDeposit(string species, Vector3 position, out ReefPhotoWall wall)
        {
            wall = null;
            float nearest = float.PositiveInfinity;
            foreach (var candidate in activeWalls)
            {
                if (!candidate || !candidate.isActiveAndEnabled || !candidate.TryGetSlotPose(species, out var pose)) continue;
                if (Vector3.Dot(position - pose.position, candidate.transform.forward) < -.025f) continue;
                foreach (var slot in candidate.slots)
                {
                    if (!slot || !slot.enabled || !slot.gameObject.activeInHierarchy) continue;
                    float distance = (slot.bounds.ClosestPoint(position) - position).sqrMagnitude;
                    if (distance <= candidate.collectionDistance * candidate.collectionDistance && distance < nearest)
                    { nearest = distance; wall = candidate; }
                }
            }
            return wall;
        }
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
            reveals = new Coroutine[slots.Length];
            originalMaterials = new Material[slots.Length];
            developMaterial = new Material(Resources.Load<Shader>("Photos/PhotoDevelop"));
            for (int i = 0; i < slots.Length; i++) if (slots[i])
            { originalMaterials[i] = slots[i].sharedMaterial; slots[i].sharedMaterial = developMaterial; }
            ready = true;
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
            activeWalls.Remove(this);
            if (originalMaterials != null) for (int i = 0; i < slots.Length; i++)
                if (slots[i]) slots[i].sharedMaterial = originalMaterials[i];
            if (developMaterial) Destroy(developMaterial);
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
            block.SetTexture(PlaceholderMap, Placeholder(i, id));
            block.SetFloat(Reveal, pop && photo ? 0 : 1);
            block.SetColor(BaseColor, Color.white);
            block.SetFloat(Emission, photo ? .45f : .2f);
            slot.SetPropertyBlock(block);
            if (labels) Label(i, photo ? displayName : "???");
            if (reveals[i] != null) { StopCoroutine(reveals[i]); reveals[i] = null; }
            if (pop && photo && isActiveAndEnabled) reveals[i] = StartCoroutine(Develop(i));
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
                go.AddComponent<ReefPhotoCaption>().Bind(t);
                t.anchor = TextAnchor.MiddleCenter; t.alignment = TextAlignment.Center;
                t.fontSize = 64; t.characterSize = .0045f; t.color = Ink;
                names[i] = t;
            }
            names[i].text = text;
        }

        IEnumerator Develop(int i)
        {
            // Develop onto the existing paper; no new particles, cameras or UI panels.
            for (float t = 0; t < 1; t += Time.deltaTime / revealSeconds)
            {
                slots[i].GetPropertyBlock(block);
                block.SetFloat(Reveal, Mathf.SmoothStep(0, 1, t));
                slots[i].SetPropertyBlock(block);
                yield return null;
            }
            slots[i].GetPropertyBlock(block); block.SetFloat(Reveal, 1); slots[i].SetPropertyBlock(block);
            reveals[i] = null;
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
