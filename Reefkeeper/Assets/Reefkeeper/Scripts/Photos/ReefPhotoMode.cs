using System.Collections.Generic;
using Reefkeeper.Hands;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Reefkeeper.Photos
{
    /// <summary>
    /// Wildlife photos. Near a species that has no photo yet (inside its 3 m detection sphere, see
    /// <see cref="ReefPhotoTargets"/>), a HUD card shows the camera gesture. Making a rectangle with
    /// both thumbs and index fingers enters photo mode: a see-through viewfinder with corner brackets,
    /// a focus ring on the subject and a shutter button. A pinch with either hand takes the photo,
    /// which is saved to the album (and the photo wall) and printed in mid-air in front of the player.
    /// Lowering both hands leaves photo mode. While it is on, photo mode owns pinches, so they don't
    /// grab, swim or hold surfaces. Editor/desktop: P enters or leaves, Enter takes the photo.
    /// </summary>
    [DefaultExecutionOrder(-32100)] // before swimming, surface holds and XRI read the hands
    public sealed class ReefPhotoMode : MonoBehaviour
    {
        public static ReefPhotoMode Instance { get; private set; }
        /// <summary>True while photo mode uses the hands' pinches (and briefly after, until they open).</summary>
        public static bool OwnsHands => Instance && Instance.owning;
        /// <summary>False until the player has a camera (Lucky Cat's gift): no hint, no gesture while locked.</summary>
        public static bool Unlocked = true;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetUnlock() => Unlocked = true;

        [Header("Gesture")]
        [Tooltip("Seconds the frame gesture must be held to enter photo mode.")] public float holdToEnter = 0.3f;
        [Tooltip("Hands this far below the eyes (m) count as lowered.")] public float lowerDrop = 0.35f;
        [Tooltip("Seconds lowered before photo mode closes.")] public float lowerSeconds = 0.3f;
        [Header("Photo")]
        [Tooltip("Vertical field of view of the photo (degrees); the viewfinder matches it.")] public float fieldOfView = 46f;
        public int photoWidth = 600, photoHeight = 400;
        [Tooltip("Seconds after a photo before photo mode closes by itself, freeing the hands to take the print.")]
        public float closeAfterShot = 1.1f;

        public bool Active { get; private set; }
        /// <summary>The new species in range right now, or null.</summary>
        public string NearbySpecies => hasTarget ? target.species : null;

        const int UILayer = 5;
        const float ViewfinderDistance = 0.6f, HintDistance = 0.8f, ArmDelay = 0.35f, MaxShotAngleMargin = 2f;
        // Same palette as the rest of the HUD (CoralGameplayHUD / ReefToolMenu); Figma: "Reefkeeper - Photo Mode UI".
        static readonly Color Cream = new Color(.97f, .93f, .81f), Panel = new Color(.15f, .22f, .28f, .94f), Yellow = new Color(.953f, .808f, .388f);
        static Color Fade(Color c, float a) => new Color(c.r, c.g, c.b, a);

        ReefPhotoTargets.Subject target;
        bool hasTarget, owning;
        float frameHeld, lowerHeld, untrackedFor, armedAt, closeAt = -1, toastUntil, shotCooldown, flash, shutterPop;
        string toast;
        Camera view, captureCam;
        Font font, bodyFont;
        Material overlay;
        Sprite disc, ring, thinRing;
        AudioSource audioSource;
        AudioClip shutterClip, printClip, openClip;
        // viewfinder
        Transform finder; CanvasGroup finderGroup; Text finderTitle, finderHint; Image shutterDisc, shutterRing, flashImage, titlePill, hintPill; RectTransform focus, frameRect;
        // HUD hint card
        Transform hint; CanvasGroup hintGroup; Text hintTitle, hintBody; RectTransform glyph; Image[] glyphLeft, glyphRight; Image glyphLens, hintCard;
        readonly List<Renderer> hidden = new List<Renderer>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            Ensure();
            SceneManager.sceneLoaded += (_, _) => Ensure();
        }

        static void Ensure()
        {
            if (Instance || !Object.FindFirstObjectByType<ReefFishSchool>(FindObjectsInactive.Include)
                && !Object.FindFirstObjectByType<ReefWorkshop>(FindObjectsInactive.Include)) return;
            new GameObject("Reef photo mode 拍照模式").AddComponent<ReefPhotoMode>();
            ReefPhotoWall.EnsureInScene();
        }

        void Awake()
        {
            Instance = this;
            var hud = FindFirstObjectByType<ReefCleaningHUD>(FindObjectsInactive.Include);
            font = hud && hud.boldFont ? hud.boldFont : hud && hud.font ? hud.font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            bodyFont = hud && hud.font ? hud.font : font;
            var shader = Resources.Load<Shader>("DiveCards/JournalOverlay");
            overlay = shader ? new Material(shader) { name = "Photo UI overlay" } : null;
            disc = MakeSprite(128, 0f); ring = MakeSprite(128, .78f); thinRing = MakeSprite(128, .875f);
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false; audioSource.spatialBlend = 0; audioSource.volume = .8f;
            shutterClip = ReefPhotoSounds.Shutter(); printClip = ReefPhotoSounds.Print(); openClip = ReefPhotoSounds.Open();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (captureCam) Destroy(captureCam.gameObject);
            if (overlay) Destroy(overlay);
        }

        void Update()
        {
            view = Camera.main;
            if (!view) { if (Active) Close("no camera"); return; }
            BuildUI();
            var head = view.transform;
            bool blocked = ReefToolMenu.BlocksWorldInput || DiveCardMock.Instance && DiveCardMock.Instance.IsOpen;
            hasTarget = Unlocked && ReefPhotoTargets.TryFind(head.position, head.forward, out target);
            string nearby = hasTarget ? target.species : null;
            if (nearby != lastNearby) { Log(nearby != null ? $"near new species {nearby} ({target.distance:F1} m, {target.angle:F0} deg)" : $"left the zone of {lastNearby}"); lastNearby = nearby; }
            float dt = Time.unscaledDeltaTime;
            shotCooldown -= dt;

            if (blocked && Active) Close("journal/menu opened");
            bool hands = ReefInput.HandsMode;
            var left = ReefHands.Get(0); var right = ReefHands.Get(1);
            var keys = Keyboard.current;

            if (!Active)
            {
                string why = !hands ? "not in hands mode" : blocked ? "journal/menu open" : null;
                bool frame = why == null && hasTarget && FrameGesture(head, left, right, out why);
                if (hasTarget && Time.unscaledTime >= nextGestureLog) { nextGestureLog = Time.unscaledTime + 1; Log("gesture: " + (frame ? "OK, holding " + frameHeld.ToString("F2") + " s" : why)); }
                frameHeld = frame ? frameHeld + dt : 0;
                if (frameHeld >= holdToEnter) Open();
                else if (keys != null && keys.pKey.wasPressedThisFrame && !blocked && Unlocked)
                {
                    if (hasTarget) Open();
                    else Toast("No new species nearby");
                }
            }
            else
            {
                if (keys != null && (keys.pKey.wasPressedThisFrame || keys.escapeKey.wasPressedThisFrame)) Close("key");
                else if (hands)
                {
                    untrackedFor = left.Tracked || right.Tracked ? 0 : untrackedFor + dt;
                    lowerHeld = HandsLowered(head, left, right) ? lowerHeld + dt : 0;
                    if (lowerHeld >= lowerSeconds || untrackedFor > .6f) Close(lowerHeld >= lowerSeconds ? "hands lowered" : "hands lost");
                    else if (left.PinchDown || right.PinchDown)
                    {
                        if (Time.unscaledTime >= armedAt) Shoot();
                        else Log("pinch ignored: viewfinder still arming");
                    }
                }
                if (Active && keys != null && (keys.enterKey.wasPressedThisFrame || keys.numpadEnterKey.wasPressedThisFrame)) Shoot();
                if (Active && closeAt > 0 && Time.unscaledTime >= closeAt) Close("after the shot");
            }
            // Keep the pinch until both hands open again, so a shot never grabs or holds on release.
            owning = Active || hands && (left.Pinch || right.Pinch) && owning;
            UpdateUI(head, dt, hands);
        }

        // ---------------- gesture ----------------

        /// <summary>Both hands make an "L", the two L's facing each other to close a rectangle in front of the eyes.</summary>
        static bool FrameGesture(Transform head, ReefHands.State l, ReefHands.State r, out string why)
        {
            why = null;
            var space = ReefInput.HandTrackingSpace;
            if (!space) { why = "no tracking space"; return false; }
            if (!l.Tracked || !r.Tracked) { why = $"tracked L={l.Tracked} R={r.Tracked}"; return false; }
            if (!l.FrameCorner || !r.FrameCorner) { why = $"L-shape L={l.FrameCorner} R={r.FrameCorner} | L: {ReefHands.FrameDebug(0)} | R: {ReefHands.FrameDebug(1)}"; return false; }
            if (ReefInput.HandBusy(0) || ReefInput.HandBusy(1)) { why = "a hand is holding something"; return false; }
            Vector3 pl = space.TransformPoint(l.Palm.position), pr = space.TransformPoint(r.Palm.position);
            float fl = Vector3.Dot(pl - head.position, head.forward), fr = Vector3.Dot(pr - head.position, head.forward);
            if (fl < .1f || fr < .1f) { why = $"hands not in front: {fl:F2} / {fr:F2} m"; return false; }
            float apart = Vector3.Distance(space.TransformPoint(l.ThumbTip), space.TransformPoint(r.ThumbTip));
            if (apart < .04f || apart > .8f) { why = $"thumb tips {apart:F2} m apart"; return false; }
            Vector3 il = space.TransformDirection(l.IndexDirection), ir = space.TransformDirection(r.IndexDirection);
            Vector3 tl = space.TransformDirection(l.ThumbDirection), tr = space.TransformDirection(r.ThumbDirection);
            // Index against index and thumb against thumb (or crossed, for a rotated hand).
            float ii = Vector3.Dot(il, ir), tt = Vector3.Dot(tl, tr), it = Vector3.Dot(il, tr), ti = Vector3.Dot(tl, ir);
            bool straight = ii < -.2f && tt < -.2f;
            bool crossed = it < -.2f && ti < -.2f;
            if (!straight && !crossed) { why = $"corners not facing: index-index {ii:F2} thumb-thumb {tt:F2} index-thumb {it:F2} thumb-index {ti:F2}"; return false; }
            return true;
        }

        static bool HandsLowered(Transform head, ReefHands.State l, ReefHands.State r)
        {
            var space = ReefInput.HandTrackingSpace;
            if (!space) return true;
            float drop = Instance ? Instance.lowerDrop : .35f;
            bool Raised(ReefHands.State s) => s.Tracked && space.TransformPoint(s.Palm.position).y > head.position.y - drop;
            return !Raised(l) && !Raised(r);
        }

        // ---------------- mode ----------------

        void Open()
        {
            Log("photo mode OPEN (" + (hasTarget ? target.species : "-") + ")");
            Active = true; frameHeld = lowerHeld = untrackedFor = 0; closeAt = -1;
            armedAt = Time.unscaledTime + ArmDelay;
            finderGroup.alpha = 0; finder.localScale = Vector3.one * .00108f;
            Play(openClip, .5f);
        }

        void Close(string reason)
        {
            Log("photo mode closed: " + reason);
            Active = false; closeAt = -1; frameHeld = lowerHeld = 0;
        }

        bool InFrame(ReefPhotoTargets.Subject s)
        {
            var local = view.transform.InverseTransformPoint(s.point);
            if (local.z < .05f) return false;
            float halfV = Mathf.Tan(fieldOfView * .5f * Mathf.Deg2Rad), halfH = halfV * photoWidth / photoHeight;
            float margin = Mathf.Tan(MaxShotAngleMargin * Mathf.Deg2Rad);
            return Mathf.Abs(local.x / local.z) < halfH + margin && Mathf.Abs(local.y / local.z) < halfV + margin;
        }

        void Shoot()
        {
            if (shotCooldown > 0 || closeAt > 0) return;
            shotCooldown = .6f;
            shutterPop = 1;
            if (!hasTarget) { Log("shot refused: no new species in range"); Toast("Too far away. Swim closer"); return; }
            if (!InFrame(target)) { Log($"shot refused: {target.species} out of frame ({target.angle:F0} deg)"); Toast("Turn toward the " + ReefSpecies.DisplayName(target.species)); return; }
            var photo = Capture();
            if (!photo) { Log("shot failed: capture returned nothing"); return; }
            Log("PHOTO taken: " + target.species);
            string species = target.species, name = ReefSpecies.DisplayName(species);
            flash = 1;
            Play(shutterClip, 1);
            ReefPrintedPhoto.Spawn(photo, view.transform, name, species);
            Play(printClip, .55f, .25f);
            closeAt = Time.unscaledTime + closeAfterShot;
            Toast("placeholder text", 4f);
        }

        Texture2D Capture()
        {
            if (!captureCam)
            {
                var go = new GameObject("Photo capture camera") { hideFlags = HideFlags.DontSave };
                go.transform.SetParent(transform, false);
                captureCam = go.AddComponent<Camera>();
                captureCam.enabled = false;
                go.AddComponent<UniversalAdditionalCameraData>();
            }
            var cam = captureCam;
            cam.transform.SetPositionAndRotation(view.transform.position, view.transform.rotation);
            cam.clearFlags = view.clearFlags; cam.backgroundColor = view.backgroundColor;
            cam.cullingMask = view.cullingMask & ~(1 << UILayer);
            cam.nearClipPlane = Mathf.Max(.03f, view.nearClipPlane); cam.farClipPlane = view.farClipPlane;
            cam.fieldOfView = fieldOfView; cam.aspect = (float)photoWidth / photoHeight;
            cam.allowHDR = view.allowHDR; cam.allowMSAA = false;
            var src = view.GetUniversalAdditionalCameraData(); var dst = cam.GetUniversalAdditionalCameraData();
            // Capture one flat photo without the built-in-only stereoTargetEye API.
            dst.allowXRRendering = false;
            if (src && dst)
            {
                dst.renderPostProcessing = true;
                dst.antialiasing = AntialiasingMode.FastApproximateAntialiasing;
                dst.volumeLayerMask = src.volumeLayerMask;
                dst.requiresColorOption = src.requiresColorOption;
                dst.requiresDepthOption = src.requiresDepthOption;
                dst.renderShadows = src.renderShadows;
            }

            // The player's hands are framing the shot: leave them out of the picture.
            hidden.Clear();
            for (int i = 0; i < 2; i++)
            {
                var root = ReefInput.DeviceRoot(i);
                if (root) foreach (var r in root.GetComponentsInChildren<Renderer>()) if (r.enabled) { r.enabled = false; hidden.Add(r); }
            }
            var rt = RenderTexture.GetTemporary(new RenderTextureDescriptor(photoWidth, photoHeight, RenderTextureFormat.ARGB32, 24) { sRGB = true, msaaSamples = 1 });
            var previous = RenderTexture.active;
            Texture2D photo = null;
            try
            {
                var request = new UniversalRenderPipeline.SingleCameraRequest { destination = rt };
                if (RenderPipeline.SupportsRenderRequest(cam, request)) RenderPipeline.SubmitRenderRequest(cam, request);
                else { cam.targetTexture = rt; cam.Render(); cam.targetTexture = null; }
                RenderTexture.active = rt;
                photo = new Texture2D(photoWidth, photoHeight, TextureFormat.RGBA32, false, false) { name = "Photo " + target.species, wrapMode = TextureWrapMode.Clamp };
                photo.ReadPixels(new UnityEngine.Rect(0, 0, photoWidth, photoHeight), 0, 0);
                var pixels = photo.GetPixels32();
                for (int i = 0; i < pixels.Length; i++) pixels[i].a = 255;
                photo.SetPixels32(pixels);
                photo.Apply(false);
            }
            catch (System.Exception e) { Debug.LogWarning("[Photos] Capture failed: " + e.Message); if (photo) Destroy(photo); photo = null; }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(rt);
                foreach (var r in hidden) if (r) r.enabled = true;
                hidden.Clear();
            }
            return photo;
        }

        void Play(AudioClip clip, float volume, float delay = 0)
        {
            if (!clip) return;
            if (delay <= 0) audioSource.PlayOneShot(clip, volume);
            else StartCoroutine(PlayLater(clip, volume, delay));
        }
        System.Collections.IEnumerator PlayLater(AudioClip clip, float volume, float delay)
        {
            yield return new WaitForSecondsRealtime(delay);
            audioSource.PlayOneShot(clip, volume);
        }

        float nextGestureLog;
        string lastNearby;
        static string logPath;

        /// <summary>Diagnostics: logcat ("[Photos]") and persistentDataPath/photo-debug.log on the device.</summary>
        static void Log(string message)
        {
            Debug.Log("[Photos] " + message);
            try
            {
                logPath ??= System.IO.Path.Combine(Application.persistentDataPath, "photo-debug.log");
                System.IO.File.AppendAllText(logPath, System.DateTime.Now.ToString("HH:mm:ss.f ") + message + "\n");
            }
            catch (System.Exception) { }
        }

        void Toast(string text, float seconds = 2.5f) { toast = text; toastUntil = Time.unscaledTime + seconds; }

        // ---------------- UI ----------------

        void UpdateUI(Transform head, float dt, bool hands)
        {
            bool showToast = Time.unscaledTime < toastUntil;
            // Viewfinder
            float a = Mathf.MoveTowards(finderGroup.alpha, Active ? 1 : 0, dt * 6);
            finderGroup.alpha = a;
            finder.gameObject.SetActive(a > 0.001f || flash > 0);
            finder.localScale = Vector3.one * Mathf.Lerp(finder.localScale.x, .001f, 1 - Mathf.Exp(-dt * 14));
            flash = Mathf.Max(0, flash - dt * 3.2f);
            flashImage.color = new Color(1, 1, 1, flash * .85f);
            shutterPop = Mathf.Max(0, shutterPop - dt * 5);
            shutterDisc.rectTransform.localScale = Vector3.one * (1 - Mathf.Sin(shutterPop * Mathf.PI) * .22f);
            if (Active)
            {
                bool inFrame = hasTarget && InFrame(target);
                bool ready = inFrame && closeAt < 0;
                shutterDisc.color = ready ? Yellow : Fade(Cream, .3f);
                shutterRing.color = Fade(Cream, ready ? .95f : .45f);
                string name = hasTarget ? ReefSpecies.DisplayName(target.species) : null;
                SetPill(titlePill, finderTitle, showToast ? toast
                    : !hasTarget ? "Out of range. Swim closer"
                    : inFrame ? "NEW SPECIES  ·  " + name
                    : "Turn toward the " + name);
                SetPill(hintPill, finderHint, hands ? "Pinch to take a photo  ·  Lower your hands to exit" : "Enter: take photo  ·  P: exit");
                focus.gameObject.SetActive(hasTarget);
                if (hasTarget)
                {
                    var local = head.InverseTransformPoint(target.point);
                    float d = ViewfinderDistance * 1000f;
                    var p = local.z > .05f ? new Vector2(local.x / local.z * d, local.y / local.z * d) : new Vector2(0, -2000);
                    var half = frameRect.sizeDelta * .5f;
                    p = new Vector2(Mathf.Clamp(p.x, -half.x, half.x), Mathf.Clamp(p.y, -half.y, half.y));
                    focus.anchoredPosition = Vector2.Lerp(focus.anchoredPosition, p, 1 - Mathf.Exp(-dt * 18));
                    focus.localScale = Vector3.one * (1 + Mathf.Sin(Time.unscaledTime * 5) * .06f);
                    focus.GetComponent<Image>().color = inFrame ? Yellow : Fade(Cream, .5f);
                }
            }
            // HUD hint card (outside photo mode); the toast variant drops the glyph.
            bool showHint = !Active && (hasTarget || showToast);
            hintGroup.alpha = Mathf.MoveTowards(hintGroup.alpha, showHint ? 1 : 0, dt * 4);
            hint.gameObject.SetActive(hintGroup.alpha > 0.001f);
            if (showHint)
            {
                bool toastCard = showToast && !hasTarget;
                if (toastCard) SetCard(toast, "Photos hang on the board by the cat stall", false);
                else SetCard("New species nearby: " + ReefSpecies.DisplayName(target.species),
                    hands ? "Make a frame with both thumbs and index fingers" : "Press P for photo mode", true);
                glyph.localScale = Vector3.one * (!toastCard && frameHeld <= 0 ? 1 + Mathf.Sin(Time.unscaledTime * 4) * .06f : 1);
                var dim = Fade(Cream, .3f);
                bool leftOk = !hands || ReefHands.Get(0).FrameCorner, rightOk = !hands || ReefHands.Get(1).FrameCorner;
                foreach (var g in glyphLeft) g.color = leftOk ? Cream : dim;
                foreach (var g in glyphRight) g.color = rightOk ? Yellow : dim;
                glyphLens.rectTransform.localScale = Vector3.one * Mathf.Clamp01(frameHeld / holdToEnter);
            }
        }

        /// <summary>Pill sized to its text (Figma "Pill": 28 px side padding).</summary>
        static void SetPill(Image pill, Text label, string value)
        {
            if (label.text == value) return;
            label.text = value;
            pill.rectTransform.sizeDelta = new Vector2(label.preferredWidth + 56, pill.rectTransform.sizeDelta.y);
        }

        /// <summary>Hint card sized to its text (Figma "Hint card": 28 | glyph 96 | 24 | text | 36).</summary>
        void SetCard(string title, string body, bool withGlyph)
        {
            if (hintTitle.text == title && hintBody.text == body && glyph.gameObject.activeSelf == withGlyph) return;
            hintTitle.text = title; hintBody.text = body;
            glyph.gameObject.SetActive(withGlyph);
            float textWidth = Mathf.Max(hintTitle.preferredWidth, hintBody.preferredWidth);
            float left = withGlyph ? 28 + 96 + 24 : 36, width = left + textWidth + 36;
            hintCard.rectTransform.sizeDelta = new Vector2(width, 112);
            glyph.anchoredPosition = new Vector2(-width * .5f + 28 + 48, 0);
            foreach (var t in new[] { hintTitle, hintBody })
            {
                // Move the label's holder: the Text itself stretches to fill it.
                var rt = (RectTransform)t.transform.parent;
                rt.sizeDelta = new Vector2(textWidth + 4, 36);
                rt.anchoredPosition = new Vector2(-width * .5f + left + textWidth * .5f, rt.anchoredPosition.y);
            }
        }

        void BuildUI()
        {
            if (finder && finder.parent == view.transform) return;
            if (finder) Destroy(finder.gameObject);
            if (hint) Destroy(hint.gameObject);
            float halfV = ViewfinderDistance * 1000f * Mathf.Tan(fieldOfView * .5f * Mathf.Deg2Rad);
            var frameSize = new Vector2(halfV * 2 * photoWidth / photoHeight, halfV * 2);

            // ---- viewfinder (Figma "2 · Viewfinder"): rounded corner brackets, title pill, focus ring, shutter, hint pill, flash
            finder = MakeCanvas("Photo viewfinder", ViewfinderDistance, 0, out finderGroup);
            frameRect = Rect(finder, "Frame", Vector2.zero, frameSize);
            const float arm = 80, thick = 7;
            foreach (var sx in new[] { -1, 1 })
                foreach (var sy in new[] { -1, 1 })
                {
                    var corner = new Vector2(sx * frameSize.x * .5f, sy * frameSize.y * .5f);
                    Box(frameRect, "Bracket", corner + new Vector2(-sx * arm * .5f, -sy * thick * .5f), new Vector2(arm, thick), Fade(Cream, .95f), disc, 18);
                    Box(frameRect, "Bracket", corner + new Vector2(-sx * thick * .5f, -sy * arm * .5f), new Vector2(thick, arm), Fade(Cream, .95f), disc, 18);
                }
            focus = Box(frameRect, "Focus ring", Vector2.zero, new Vector2(92, 92), Yellow, thinRing).rectTransform;
            titlePill = Box(finder, "Title pill", new Vector2(0, frameSize.y * .5f + 16 + 26), new Vector2(400, 52), Panel, disc, 2.4f);
            finderTitle = Label(titlePill, "", 24, Cream);
            shutterRing = Box(finder, "Shutter ring", new Vector2(0, -frameSize.y * .5f - 12 - 56), new Vector2(112, 112), Fade(Cream, .95f), thinRing);
            shutterDisc = Box(shutterRing.transform, "Shutter button", Vector2.zero, new Vector2(80, 80), Yellow, disc);
            hintPill = Box(finder, "Hint pill", new Vector2(0, -frameSize.y * .5f - 12 - 112 - 14 - 24), new Vector2(400, 48), Panel, disc, 2.6f);
            finderHint = Label(hintPill, "", 21, Cream);
            flashImage = Box(finder, "Flash", Vector2.zero, new Vector2(4000, 4000), new Color(1, 1, 1, 0));
            finder.gameObject.SetActive(false);

            // ---- HUD hint card (Figma "Hint card"): gesture glyph + yellow title + cream body, low in the view
            hint = MakeCanvas("Photo hint", HintDistance, -.27f, out hintGroup);
            hintCard = Box(hint, "Card", Vector2.zero, new Vector2(700, 112), Panel, disc, 2.1f);
            glyph = Rect(hintCard.transform, "Frame gesture glyph", Vector2.zero, new Vector2(96, 70));
            // Two L's, left hand cream and right hand yellow, closing a rectangle; each half lights while that
            // hand holds its "L" (live gesture feedback) and the lens fills while the gesture is held.
            glyphLeft = new[] { Box(glyph, "Left index", new Vector2(-44, -2), new Vector2(8, 58), Cream, disc, 16), Box(glyph, "Left thumb", new Vector2(-17, -27), new Vector2(62, 8), Cream, disc, 16) };
            glyphRight = new[] { Box(glyph, "Right index", new Vector2(44, 2), new Vector2(8, 58), Yellow, disc, 16), Box(glyph, "Right thumb", new Vector2(17, 27), new Vector2(62, 8), Yellow, disc, 16) };
            Box(glyph, "Lens", Vector2.zero, new Vector2(26, 26), Fade(Cream, .85f), ring);
            glyphLens = Box(glyph, "Lens fill", Vector2.zero, new Vector2(16, 16), Yellow, disc);
            hintTitle = Label(Rect(hintCard.transform, "Title", new Vector2(0, 17), new Vector2(560, 36)), "", 26, Yellow, TextAnchor.MiddleLeft);
            hintBody = Label(Rect(hintCard.transform, "Body", new Vector2(0, -19), new Vector2(560, 36)), "", 19, Cream, TextAnchor.MiddleLeft, bodyFont);
            hintGroup.alpha = 0;
            hint.gameObject.SetActive(false);
        }

        Transform MakeCanvas(string name, float distance, float down, out CanvasGroup group)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(UnityEngine.Canvas), typeof(CanvasGroup)) { layer = UILayer };
            go.transform.SetParent(view.transform, false);
            go.transform.localPosition = new Vector3(0, down, distance);
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one * .001f; // 1 unit = 1 mm
            var canvas = go.GetComponent<UnityEngine.Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 50;
            ((RectTransform)go.transform).sizeDelta = new Vector2(1600, 1200);
            group = go.GetComponent<CanvasGroup>();
            group.interactable = group.blocksRaycasts = false;
            return go.transform;
        }

        static RectTransform Rect(Transform parent, string name, Vector2 at, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform)) { layer = UILayer };
            go.transform.SetParent(parent, false);
            var r = (RectTransform)go.transform;
            r.anchoredPosition = at; r.sizeDelta = size;
            return r;
        }

        /// <summary>A UI image; a <paramref name="slice"/> multiplier makes it a 9-sliced rounded shape (radius = 63 / slice units).</summary>
        Image Box(Transform parent, string name, Vector2 at, Vector2 size, Color color, Sprite sprite = null, float slice = 0)
        {
            var image = Rect(parent, name, at, size).gameObject.AddComponent<Image>();
            image.sprite = sprite; image.color = color; image.raycastTarget = false; image.material = overlay;
            if (slice > 0) { image.type = Image.Type.Sliced; image.pixelsPerUnitMultiplier = slice; }
            return image;
        }

        Text Label(Component parent, string value, int size, Color color, TextAnchor anchor = TextAnchor.MiddleCenter, Font face = null)
        {
            var r = parent.transform as RectTransform;
            var go = new GameObject("Text", typeof(RectTransform)) { layer = UILayer };
            go.transform.SetParent(r, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
            var t = go.AddComponent<Text>();
            t.font = face ? face : font; t.text = value; t.fontSize = size; t.color = color; t.alignment = anchor;
            t.material = overlay; t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Overflow; t.verticalOverflow = VerticalWrapMode.Overflow;
            // Same soft text shadow as the other HUDs (Figma style "Text shadow").
            var shadow = go.AddComponent<Shadow>(); shadow.effectColor = new Color(0, .04f, .05f, .35f); shadow.effectDistance = new Vector2(0, -2);
            return t;
        }

        /// <summary>Anti-aliased disc (hole 0) or ring (hole = inner radius fraction), 9-slice ready.</summary>
        static Sprite MakeSprite(int size, float hole)
        {
            var t = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = hole > 0 ? "Photo ring" : "Photo disc" };
            var px = new Color32[size * size];
            float r = size * .5f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + .5f, y + .5f), new Vector2(r, r));
                    float alpha = Mathf.Clamp01(r - d) * (hole > 0 ? Mathf.Clamp01(d - r * hole) : 1);
                    px[y * size + x] = new Color32(255, 255, 255, (byte)(alpha * 255));
                }
            t.SetPixels32(px); t.Apply(false, true);
            float b = size * .5f - 1;
            return Sprite.Create(t, new UnityEngine.Rect(0, 0, size, size), new Vector2(.5f, .5f), 100, 0, SpriteMeshType.FullRect, new Vector4(b, b, b, b));
        }

#if UNITY_EDITOR
        [UnityEditor.MenuItem("Reefkeeper/Photos/Reset photo album - 清空照片墙")]
        static void ResetAlbumMenu()
        {
            if (Application.isPlaying) ReefPhotoAlbum.ResetAll();
            else
            {
                var folder = System.IO.Path.Combine(Application.persistentDataPath, "ReefPhotos");
                if (System.IO.Directory.Exists(folder)) System.IO.Directory.Delete(folder, true);
                Debug.Log("[Photos] Album cleared: " + folder);
            }
        }
#endif
    }
}
