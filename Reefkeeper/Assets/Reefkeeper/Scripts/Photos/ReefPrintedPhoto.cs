using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace Reefkeeper.Photos
{
    /// <summary>
    /// An instant photo printed in mid-air in front of the player: it slides out, develops from dark,
    /// and floats until picked up. Carrying it near the wall deposits it into the matching slot.
    /// Released prints remain available until their unattended lifetime expires.
    /// </summary>
    public sealed class ReefPrintedPhoto : MonoBehaviour
    {
        public float lifetime = 120f;
        static readonly HashSet<ReefPrintedPhoto> pending = new HashSet<ReefPrintedPhoto>();
        Texture2D photoTexture;
        string species;
        bool stored, depositing;
        float releasedAt;
        public string Species => species;
        public bool Depositing => depositing;
        public static bool HasPending(string id)
        {
            foreach (var p in pending) if (p && !p.vanishing && p.species == id) return true;
            return false;
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetPending() => pending.Clear();
        const float Width = .17f, Height = .15f, PicWidth = .15f, PicHeight = .1f, Thickness=.003f;

        XRGrabInteractable grab;
        Material picture, paper;
        Transform visual;
        float born, developed;
        bool vanishing, everHeld;
        Vector3 rest;

        public static ReefPrintedPhoto Spawn(Texture2D photo, Transform head, string caption, string species)
        {
            var forward = head.forward;
            var position = head.position + forward * .42f - head.up * .04f;
            var go = new GameObject("Printed photo 照片 - " + caption);
            go.transform.SetPositionAndRotation(position, Quaternion.LookRotation(position - head.position, head.up));
            var box = go.AddComponent<BoxCollider>();
            box.size = new Vector3(Width, Height, .012f);
            var body = go.AddComponent<Rigidbody>();
            body.isKinematic = true; body.useGravity = false;
            var printed = go.AddComponent<ReefPrintedPhoto>();
            printed.species = species;
            printed.photoTexture = photo;
            pending.Add(printed);
            printed.Build(photo, caption);
            return printed;
        }

        void Build(Texture2D photo, string caption)
        {
            visual = new GameObject("Visual").transform;
            visual.SetParent(transform, false);
            paper = Toon(new Color(.984f, .965f, .925f), null, .45f);
            var stock=GameObject.CreatePrimitive(PrimitiveType.Cube);stock.name="Paper";
            Destroy(stock.GetComponent<Collider>());stock.transform.SetParent(visual,false);
            stock.transform.localScale=new Vector3(Width,Height,Thickness);
            stock.GetComponent<MeshRenderer>().sharedMaterial=paper;
            stock.GetComponent<MeshRenderer>().shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
            picture = Toon(Color.black, photo, .85f);
            Quad("Picture", new Vector3(0, (Height - PicHeight) * .5f - .01f, -Thickness*.5f-.0002f), new Vector2(PicWidth, PicHeight), picture, false);
            var font = FindFirstObjectByType<ReefCleaningHUD>(FindObjectsInactive.Include);
            var label = new GameObject("Caption").AddComponent<TextMesh>();
            label.transform.SetParent(visual, false);
            label.transform.localPosition = new Vector3(0, -Height * .5f + .0125f, -Thickness*.5f-.0004f);
            label.text = caption; label.anchor = TextAnchor.MiddleCenter; label.alignment = TextAlignment.Center;
            label.font = font && font.boldFont ? font.boldFont : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.gameObject.AddComponent<ReefPhotoCaption>().Bind(label);
            label.fontSize = 64; label.characterSize = .0024f; label.color = new Color(.09f, .25f, .30f);

            grab = gameObject.AddComponent<XRGrabInteractable>();
            grab.colliders.Clear();grab.colliders.Add(GetComponent<BoxCollider>());
            grab.movementType = XRBaseInteractable.MovementType.Instantaneous;
            grab.throwOnDetach = false;
            grab.useDynamicAttach = true;
            grab.retainTransformParent = false;
            ReefUnderwaterThrow.Install(grab, paper: true);
            grab.enabled = false; // not while it is still sliding out
            grab.selectEntered.AddListener(_ => everHeld = true);
            grab.selectExited.AddListener(_ => { releasedAt = Time.time; rest = transform.position; });
            born = Time.time;
            releasedAt = born;
            rest = transform.position;
            StartCoroutine(PrintOut());
        }

        IEnumerator PrintOut()
        {
            // Slides down out of an invisible slot and unfolds to full height.
            for (float t = 0; t < 1; t += Time.deltaTime / .5f)
            {
                float e = 1 - Mathf.Pow(1 - t, 3);
                visual.localScale = new Vector3(1, Mathf.Lerp(.05f, 1, e), 1);
                visual.localPosition = new Vector3(0, (1 - e) * Height * .5f, 0);
                yield return null;
            }
            visual.localScale = Vector3.one; visual.localPosition = Vector3.zero;
            grab.enabled = true;
        }

        void Update()
        {
            // Develops from a dark blue-grey to the full picture, like an instant photo.
            developed = Mathf.Min(1, developed + Time.deltaTime / 1.8f);
            picture.SetColor("_BaseColor", Color.Lerp(new Color(.16f, .2f, .26f), Color.white, Mathf.SmoothStep(0, 1, developed)));
            if (vanishing || depositing) return;
            if (grab.isSelected) { TryDeposit(); return; }
            if (!everHeld) transform.position = rest + Vector3.up * Mathf.Sin((Time.time - born) * 1.6f) * .006f;
            if (Time.time - releasedAt > lifetime) Vanish();
        }

        bool TryDeposit()
        {
            if (!everHeld || depositing || vanishing || ReefPhotoAlbum.Has(species)) return false;
            if (!ReefPhotoWall.TryFindDeposit(species, transform.position, out var wall)) return false;
            depositing = true;
            GetComponent<ReefUnderwaterThrow>()?.StopMotion();
            // Release XRI ownership before moving the print ourselves.
            grab.enabled = false;
            StartCoroutine(Deposit(wall));
            return true;
        }

        IEnumerator Deposit(ReefPhotoWall wall)
        {
            Vector3 start = transform.position;
            Quaternion rotation = transform.rotation;
            for (float t = 0; t < 1; t += Time.deltaTime / .4f)
            {
                if (!wall || !wall.isActiveAndEnabled || !wall.TryGetSlotPose(species, out var pose))
                { depositing = false; releasedAt = Time.time; grab.enabled = true; yield break; }
                float eased = Mathf.SmoothStep(0, 1, t);
                transform.SetPositionAndRotation(Vector3.Lerp(start, pose.position, eased), Quaternion.Slerp(rotation, pose.rotation, eased));
                yield return null;
            }
            if (wall && wall.isActiveAndEnabled && wall.TryGetSlotPose(species, out var finalPose))
            {
                transform.SetPositionAndRotation(finalPose.position, finalPose.rotation);
                if (!ReefPhotoAlbum.Has(species))
                {
                    ReefPhotoAlbum.Store(species, photoTexture);
                    stored = ReefPhotoAlbum.Get(species) == photoTexture;
                }
                Vanish();
            }
            else { depositing = false; releasedAt = Time.time; grab.enabled = true; }
        }

        /// <summary>Shrinks with a little spin and is gone.</summary>
        public void Vanish()
        {
            if (vanishing) return;
            vanishing = true;
            GetComponent<ReefUnderwaterThrow>()?.StopMotion();
            if (grab) grab.enabled = false;
            StartCoroutine(Shrink());
        }

        IEnumerator Shrink()
        {
            var start = transform.localScale;
            for (float t = 0; t < 1; t += Time.deltaTime / .3f)
            {
                transform.localScale = start * (1 - t * t);
                visual.localRotation = Quaternion.Euler(0, 0, t * 70);
                yield return null;
            }
            Destroy(gameObject);
        }

        void OnDestroy()
        {
            pending.Remove(this);
            if (!stored && photoTexture) Destroy(photoTexture);
            if (picture) Destroy(picture); if (paper) Destroy(paper);
        }

        void Quad(string name, Vector3 at, Vector2 size, Material material, bool back)
        {
            var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Destroy(q.GetComponent<Collider>());
            q.name = name;
            q.transform.SetParent(visual, false);
            q.transform.localPosition = at;
            q.transform.localRotation = back ? Quaternion.Euler(0, 180, 0) : Quaternion.identity;
            q.transform.localScale = new Vector3(size.x, size.y, 1);
            var r = q.GetComponent<MeshRenderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        static Material Toon(Color color, Texture texture, float selfLight)
        {
            var shader = Shader.Find("Reefkeeper/UnderWater/Toon") ?? Shader.Find("Universal Render Pipeline/Unlit");
            var m = new Material(shader) { name = "Printed photo" };
            m.SetColor("_BaseColor", color);
            if (texture) m.SetTexture("_BaseMap", texture);
            if (m.HasProperty("_Emission")) m.SetFloat("_Emission", selfLight);
            if (m.HasProperty("_CausticsAmount")) m.SetFloat("_CausticsAmount", .15f);
            return m;
        }
    }
}
