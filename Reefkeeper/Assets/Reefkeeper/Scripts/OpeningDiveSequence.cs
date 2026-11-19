using System.Collections;
using System.Collections.Generic;
using Reefkeeper.Hands;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using UnityEngine.XR;

namespace Reefkeeper
{
    /// <summary>Seated surface opening and water entry in the same reef scene and player rig.</summary>
    [DefaultExecutionOrder(1000)]
    public sealed class OpeningDiveSequence : MonoBehaviour
    {
        public enum Phase { Preparing, Seated, FadingOut, FadingIn, Swimming }
        public ReefWorkshop workshop;
        public GameObject seatedBody, underwaterEnvironment;
        public Material surfaceSky, fadeTemplate, bubbleTemplate;
        public Renderer surfaceWater;
        public Vector3 surfaceSunAngles = new Vector3(20, 180, 0);
        public Vector3 seatedEye = new Vector3(0, 16.57f, -14.47f);
        public Vector3 underwaterEye = new Vector3(-.456255f, 1.674831f, -17.225203f);
        public float underwaterHeading = 2.8949914f, desktopEntryPitch = 10.749868f;
        public float fadeOutSeconds = .5f, fadeInSeconds = 1.1f;
        public Phase State { get; private set; }
        public float FadeAlpha { get; private set; } = 1;
        public int DiveCount { get; private set; }
        public bool IsXR { get; private set; }
        public Camera ActiveView => view;
        public string LastError { get; private set; }

        Camera view;
        DiveMotor desktop;
        XROrigin origin;
        Transform fade;
        Material fadeMaterial, reefSky;
        Canvas desktopFade;
        Image desktopCurtain;
        Mesh bubbleMesh;
        AudioSource entryAudio;
        AudioClip splash;
        InputAction confirm;
        readonly List<Behaviour> movement = new List<Behaviour>();
        readonly List<GameObject> waterParticles = new List<GameObject>();
        float yaw, pitch = 18, reefFarClip;
        bool reefFog, environmentWasActive, workshopWasEnabled, ambienceWasMuted;
        bool shuttingDown, surfaceApplied, switchingPreview;
        bool inputArmed, pointerCaptured;
        float inputReleasedAt = -1;
        const float InputReleaseSeconds = .2f;
        CameraClearFlags reefClearFlags;
        Color reefAmbient;
        AmbientMode reefAmbientMode;
        ReefSoundscape soundscape;
        Light openingSun;
        Quaternion reefSunRotation;
        UniversalAdditionalCameraData surfaceCameraData;
        CameraOverrideOption reefDepthOption, reefColorOption;

        IEnumerator Start()
        {
            // Workshop and habitat Awake choose the existing desktop or XR player first.
            if (!workshop || !(view = Camera.main))
            {
                LastError = "Opening requires the existing workshop and active camera.";
                Debug.LogError("[OpeningDive] " + LastError);
                enabled = false;
                yield break;
            }
            desktop = view.GetComponentInParent<DiveMotor>();
            origin = view.GetComponentInParent<XROrigin>();
            IsXR = !desktop && origin;
            foreach (var motor in workshop.desktopPlayer.GetComponentsInChildren<DiveMotor>(true)) Pause(motor);
            foreach (var motor in workshop.xrGroup.GetComponentsInChildren<ReefXRSwim>(true)) Pause(motor);
            workshopWasEnabled = workshop.enabled;
            workshop.enabled = false; // Keep the selected rig stable until entry; its existing journal stays usable.
            reefSky = RenderSettings.skybox; reefFog = RenderSettings.fog;
            reefAmbient = RenderSettings.ambientLight; reefAmbientMode = RenderSettings.ambientMode;
            environmentWasActive = underwaterEnvironment && underwaterEnvironment.activeSelf;
            if (underwaterEnvironment) underwaterEnvironment.SetActive(false);
            openingSun = RenderSettings.sun;
            if (openingSun)
            {
                reefSunRotation = openingSun.transform.rotation;
                openingSun.transform.rotation = Quaternion.Euler(surfaceSunAngles);
            }
            RenderSettings.skybox = surfaceSky; RenderSettings.fog = false;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(.67f, .75f, .78f);
            ApplySurfaceCamera();
            surfaceApplied = true;
            soundscape = workshop.GetComponent<ReefSoundscape>();
            if (soundscape && soundscape.AmbienceSource)
            {
                ambienceWasMuted = soundscape.AmbienceSource.mute;
                soundscape.AmbienceSource.mute = true;
            }
            CreateFade(); CreateAudio();
            confirm = new InputAction("Start dive", InputActionType.Button, "<XRController>/triggerPressed");
            confirm.Enable();
            MoveEye(seatedEye);
            if (desktop) view.transform.rotation = Quaternion.Euler(pitch, 0, 0);
            Cursor.lockState = CursorLockMode.None; Cursor.visible = !IsXR;
            // Preserve native tracking and align once after its first pose, never animate the head.
            yield return null;
            if (IsXR) MoveEye(seatedEye);
            yield return FadeTo(0, .6f);
            State = Phase.Seated;
        }

        void Pause(Behaviour behaviour)
        {
            if (!behaviour.enabled) return;
            movement.Add(behaviour); behaviour.enabled = false;
        }

        void Update()
        {
            if (State != Phase.Seated || !view || switchingPreview) return;
            if (!HasInputFocus()) { pointerCaptured = false; DisarmInput(); return; }
#if UNITY_EDITOR
            if (!XRSettings.isDeviceActive && Keyboard.current?.f2Key.wasPressedThisFrame == true
                && !ReefToolMenu.BlocksWorldInput)
            {
                StartCoroutine(SwitchSurfacePreview());
                return;
            }
#endif
            if (!IsXR)
            {
                var mouse = Mouse.current;
                bool looking = mouse != null && mouse.rightButton.isPressed;
                Cursor.lockState = looking ? CursorLockMode.Locked : CursorLockMode.None;
                Cursor.visible = !looking;
                if (looking)
                {
                    Vector2 delta = mouse.delta.ReadValue();
                    yaw += delta.x * .095f;
                    pitch = Mathf.Clamp(pitch - delta.y * .095f, -65, 85);
                    view.transform.rotation = Quaternion.Euler(pitch, yaw, 0);
                }
                bool pointerInside = mouse != null && view.pixelRect.Contains(mouse.position.ReadValue());
                // The first click belongs to entering Game view, even if Unity auto-focused it on Play.
                if (!pointerCaptured)
                {
                    if (pointerInside && !ReefToolMenu.BlocksWorldInput &&
                        (mouse.leftButton.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame))
                    { pointerCaptured = true; DisarmInput(); }
                    return;
                }
                bool held = mouse != null && (mouse.leftButton.isPressed || mouse.rightButton.isPressed);
                if (AcceptDivePress(mouse?.leftButton.wasPressedThisFrame == true, held,
                    pointerInside && !looking && !ReefToolMenu.BlocksWorldInput)) TryBeginDive();
            }
            else
            {
                var left = ReefHands.Get(0); var right = ReefHands.Get(1);
                bool held = confirm.IsPressed() || left.Pinch || right.Pinch;
                bool pressed = confirm.WasPressedThisFrame() || left.PinchDown || right.PinchDown;
                if (AcceptDivePress(pressed, held, !ReefToolMenu.BlocksWorldInput)) TryBeginDive();
            }
        }

        bool HasInputFocus()
        {
            if (!Application.isFocused) return false;
#if UNITY_EDITOR
            // Unity can keep the application focused while an Inspector or toolbar owns input.
            // A click used to focus Game view must never count as dive confirmation.
            var window = UnityEditor.EditorWindow.focusedWindow;
            if (!window || window.GetType().FullName != "UnityEditor.GameView") return false;
#endif
            return true;
        }

        bool AcceptDivePress(bool pressed, bool held, bool available)
        {
            if (!available) { DisarmInput(); return false; }
            if (!inputArmed)
            {
                if (held) inputReleasedAt = -1;
                else
                {
                    if (inputReleasedAt < 0) inputReleasedAt = Time.unscaledTime;
                    if (Time.unscaledTime - inputReleasedAt >= InputReleaseSeconds) inputArmed = true;
                }
                return false;
            }
            if (!pressed) return false;
            DisarmInput();
            return true;
        }

        void DisarmInput() { inputArmed = false; inputReleasedAt = -1; }

        public bool TryBeginDive()
        {
            // Clicking the existing journal, tool wheel or their closing animation is not a dive.
            if (State != Phase.Seated || switchingPreview || ReefToolMenu.BlocksWorldInput) return false;
            State = Phase.FadingOut;
            DiveCount++;
            StartCoroutine(Dive());
            return true;
        }

        IEnumerator Dive()
        {
            Cursor.lockState = CursorLockMode.None; Cursor.visible = false;
            yield return FadeTo(1, fadeOutSeconds);
            entryAudio.PlayOneShot(splash, .45f);
            yield return new WaitForSecondsRealtime(.2f);
            if (desktop) desktop.ResetDive(); // Reset cached pitch/velocity while covered by the fade.
            if (desktop) desktop.SetViewAngles(underwaterHeading, desktopEntryPitch);
            else if (origin)
                origin.MatchOriginUpCameraForward(Vector3.up, Quaternion.Euler(0, underwaterHeading, 0) * Vector3.forward);
            MoveEye(underwaterEye);
            if (seatedBody) seatedBody.SetActive(false);
            RestoreEnvironment();
            if (surfaceWater) surfaceWater.enabled = false;
            Physics.SyncTransforms();
            PositionFade(); SpawnBubbles();
            State = Phase.FadingIn;
            yield return FadeTo(0, fadeInSeconds);
            foreach (var behaviour in movement) if (behaviour) behaviour.enabled = true;
            if (workshop) workshop.enabled = workshopWasEnabled;
            if (desktop) desktop.SetControl(true);
            else { Cursor.lockState = CursorLockMode.None; Cursor.visible = false; }
            State = Phase.Swimming;
            Debug.Log("[OpeningDive] Same scene and camera preserved; swimming restored.");
            yield return new WaitForSecondsRealtime(2);
            if (fade) Destroy(fade.gameObject);
            if (desktopFade) Destroy(desktopFade.gameObject);
            if (fadeMaterial) Destroy(fadeMaterial);
            if (splash) Destroy(splash);
            if (bubbleMesh) Destroy(bubbleMesh);
            enabled = false;
        }

        void MoveEye(Vector3 position)
        {
            Transform player = desktop ? desktop.transform : origin.transform;
            var controller = player.GetComponent<CharacterController>();
            bool wasEnabled = controller && controller.enabled;
            if (controller) controller.enabled = false;
            player.position += position - view.transform.position;
            if (controller) controller.enabled = wasEnabled;
        }

#if UNITY_EDITOR
        IEnumerator SwitchSurfacePreview()
        {
            switchingPreview = true;
            pointerCaptured = false; DisarmInput();
            Vector3 eye = view.transform.position;
            Quaternion look = view.transform.rotation;
            bool nextXR = !IsXR;
            RestoreSurfaceCamera();
            workshop.ReturnBrush();
            workshop.desktopPlayer.SetActive(!nextXR);
            workshop.xrGroup.SetActive(nextXR);
            view = Camera.main;
            desktop = view.GetComponentInParent<DiveMotor>();
            origin = view.GetComponentInParent<XROrigin>();
            IsXR = !desktop && origin;
            ApplySurfaceCamera();
            MoveEye(eye);
            // Wait for the simulator's initial tracked pose before the final alignment.
            yield return null;
            if (origin)
                origin.MatchOriginUpCameraForward(Vector3.up, Quaternion.Euler(0, look.eulerAngles.y, 0) * Vector3.forward);
            else
            {
                yaw = look.eulerAngles.y;
                pitch = Mathf.Clamp(Mathf.DeltaAngle(0, look.eulerAngles.x), -65, 85);
                desktop.SetViewAngles(yaw, pitch);
            }
            MoveEye(eye);
            foreach (var motor in workshop.desktopPlayer.GetComponentsInChildren<DiveMotor>(true)) Pause(motor);
            foreach (var motor in workshop.xrGroup.GetComponentsInChildren<ReefXRSwim>(true)) Pause(motor);
            Cursor.lockState = CursorLockMode.None; Cursor.visible = !IsXR;
            SetFade(0); PositionFade();
            DisarmInput(); switchingPreview = false;
        }
#endif

        void ApplySurfaceCamera()
        {
            reefFarClip = view.farClipPlane; reefClearFlags = view.clearFlags;
            surfaceCameraData = view.GetUniversalAdditionalCameraData();
            reefDepthOption = surfaceCameraData.requiresDepthOption;
            reefColorOption = surfaceCameraData.requiresColorOption;
            surfaceCameraData.requiresDepthOption = CameraOverrideOption.On;
            surfaceCameraData.requiresColorOption = CameraOverrideOption.On;
            view.clearFlags = CameraClearFlags.Skybox; view.farClipPlane = 1500;
            foreach (var particles in view.GetComponentsInChildren<ParticleSystem>(true))
                if (particles.gameObject.activeSelf)
                { waterParticles.Add(particles.gameObject); particles.gameObject.SetActive(false); }
        }

        void RestoreSurfaceCamera()
        {
            if (surfaceCameraData)
            {
                surfaceCameraData.requiresDepthOption = reefDepthOption;
                surfaceCameraData.requiresColorOption = reefColorOption;
            }
            if (view) { view.farClipPlane = reefFarClip; view.clearFlags = reefClearFlags; }
            foreach (var particles in waterParticles) if (particles) particles.SetActive(true);
            waterParticles.Clear();
        }

        void RestoreEnvironment()
        {
            if (!surfaceApplied) return;
            if (openingSun) openingSun.transform.rotation = reefSunRotation;
            RestoreSurfaceCamera();
            RenderSettings.skybox = reefSky; RenderSettings.fog = reefFog;
            RenderSettings.ambientMode = reefAmbientMode; RenderSettings.ambientLight = reefAmbient;
            if (underwaterEnvironment) underwaterEnvironment.SetActive(environmentWasActive);
            if (soundscape && soundscape.AmbienceSource) soundscape.AmbienceSource.mute = ambienceWasMuted;
            surfaceApplied = false;
        }

        IEnumerator FadeTo(float target, float seconds)
        {
            float from = FadeAlpha, elapsed = 0;
            while (elapsed < seconds)
            {
                elapsed += Time.unscaledDeltaTime;
                SetFade(Mathf.Lerp(from, target, Mathf.SmoothStep(0, 1, elapsed / Mathf.Max(seconds, .001f))));
                yield return null;
            }
            SetFade(target);
        }
        void SetFade(float value)
        {
            FadeAlpha = value;
            if (fadeMaterial) fadeMaterial.SetColor("_Color", new Color(.015f, .035f, .045f, value));
            if (fade) fade.gameObject.SetActive(IsXR && value > .0001f);
            if (desktopFade) desktopFade.enabled = !IsXR && value > .0001f;
            if (desktopCurtain) desktopCurtain.color = new Color(.015f, .035f, .045f, value);
        }
        void CreateFade()
        {
            var overlay = new GameObject("Desktop fade over HUD", typeof(RectTransform), typeof(Canvas));
            overlay.transform.SetParent(transform, false);
            desktopFade = overlay.GetComponent<Canvas>();
            desktopFade.renderMode = RenderMode.ScreenSpaceOverlay;
            desktopFade.sortingOrder = short.MaxValue;
            var curtain = new GameObject("Curtain", typeof(RectTransform), typeof(Image));
            curtain.transform.SetParent(overlay.transform, false);
            desktopCurtain = curtain.GetComponent<Image>();
            desktopCurtain.raycastTarget = false;
            var rect = (RectTransform)curtain.transform;
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = "Stereo-safe dive fade";
            Destroy(go.GetComponent<Collider>());
            fade = go.transform;
            fade.SetParent(transform, false);
            fadeMaterial = new Material(fadeTemplate);
            go.GetComponent<Renderer>().sharedMaterial = fadeMaterial;
            go.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            SetFade(1);
            PositionFade();
        }
        void LateUpdate() { PositionFade(); }
        void PositionFade()
        {
            if (!view || !fade) return;
            float distance = view.nearClipPlane + .02f;
            fade.SetPositionAndRotation(view.transform.position + view.transform.forward * distance, view.transform.rotation);
            fade.localScale = Vector3.one * Mathf.Max(2, distance * 12);
            // A two-metre plane this close covers both eyes, including their physical separation.
        }

        void CreateAudio()
        {
            entryAudio = gameObject.AddComponent<AudioSource>();
            entryAudio.playOnAwake = false;
            entryAudio.spatialBlend = 0;
            entryAudio.priority = 32;
            const int rate = 22050;
            var data = new float[(int)(rate * 1.15f)];
            var random = new System.Random(4107);
            float low = 0;
            for (int i = 0; i < data.Length; i++)
            {
                float t = i / (float)rate;
                low += ((float)random.NextDouble() * 2 - 1 - low) * .17f;
                float envelope = Mathf.Min(t / .025f, 1) * Mathf.Exp(-t * 4.8f) * Mathf.Clamp01((1.15f - t) / .2f);
                data[i] = (low * .85f + Mathf.Sin(t * 2 * Mathf.PI * (95 - t * 45)) * .07f) * envelope;
            }
            splash = AudioClip.Create("Soft procedural water entry", data.Length, 1, rate, false);
            splash.SetData(data, 0);
        }

        void SpawnBubbles()
        {
            var go = new GameObject("Short entry bubbles");
            go.transform.SetParent(transform);
            go.transform.position = view.transform.position;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = false; main.playOnAwake = false; main.duration = 1.6f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(.65f, 1.5f);
            main.startSpeed = 0; main.startSize = new ParticleSystem.MinMaxCurve(.013f, .035f);
            main.startColor = new Color(.68f, .9f, .94f, .65f);
            main.maxParticles = 24; main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = ps.emission; emission.enabled = false;
            var shape = ps.shape; shape.enabled = false;
            var velocity = ps.velocityOverLifetime; velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World; velocity.y = .6f;
            var size = ps.sizeOverLifetime; size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1, new AnimationCurve(new Keyframe(0,.6f),new Keyframe(.5f,1),new Keyframe(1,0)));
            bubbleMesh = new Mesh { name = "Eight triangle bubble" };
            bubbleMesh.vertices = new[] { Vector3.up,Vector3.down,Vector3.left,Vector3.right,Vector3.forward,Vector3.back };
            bubbleMesh.triangles = new[] {0,4,3,0,3,5,0,5,2,0,2,4,1,3,4,1,5,3,1,2,5,1,4,2};
            bubbleMesh.RecalculateNormals();
            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Mesh; renderer.mesh = bubbleMesh;
            renderer.sharedMaterial = bubbleTemplate; renderer.shadowCastingMode = ShadowCastingMode.Off;
            ps.Play();
            for (int i = 0; i < 20; i++)
            {
                float side = i % 2 == 0 ? -1 : 1;
                var p = view.transform.position + view.transform.forward * (.45f + i * .012f) +
                    view.transform.right * side * (.23f + i * .006f) - Vector3.up * (.12f + i * .012f);
                ps.Emit(new ParticleSystem.EmitParams { position = p }, 1);
            }
            Destroy(go, 2);
        }

        void OnDestroy()
        {
            shuttingDown = true;
            RestoreEnvironment();
            confirm?.Dispose();
            if (fadeMaterial) Destroy(fadeMaterial);
            if (splash) Destroy(splash);
            if (bubbleMesh) Destroy(bubbleMesh);
            if (State != Phase.Swimming) { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
        }
        void OnApplicationFocus(bool focus)
        {
            pointerCaptured = false;
            DisarmInput();
            if (!focus && !shuttingDown && State == Phase.Seated)
            { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
        }
    }
}
