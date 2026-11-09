using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Reefkeeper.Corals;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Simulation;

namespace Reefkeeper
{
    /// <summary>Opt-in Play Mode integration check; absent from normal gameplay.</summary>
    public sealed class ReefV3SmokeCheck : MonoBehaviour
    {
        Keyboard keyboard;
        Mouse mouse;
        XRSimulatedController rightController;
        ReefWorkshop workshop;
        ReefBrushPickup pickup;
        string output;
        InputSettings originalInputSettings;
        InputSettings testInputSettings;
        float originalCaptureDeltaTime;
        bool settingsCaptured;
        readonly List<string> checks = new List<string>();

#if UNITY_EDITOR
        public static void RunInEditor(string outputFolder)
        {
            if (!Application.isPlaying) throw new InvalidOperationException("Enter Play Mode before running the integration check.");
            var w = FindFirstObjectByType<ReefWorkshop>();
            w.xrGroup.SetActive(false); w.desktopPlayer.SetActive(true);
            UnityEditor.EditorApplication.isPaused = false;
            new GameObject("V3 cleaning integration check").AddComponent<ReefV3SmokeCheck>().output = outputFolder;
        }
#endif

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            var args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "-reef-v3-smoke");
            if (index < 0 || index + 1 >= args.Length) return;
            new GameObject("V3 cleaning integration check").AddComponent<ReefV3SmokeCheck>().output = args[index + 1];
        }

        IEnumerator Start()
        {
            Directory.CreateDirectory(output);
            originalInputSettings = InputSystem.settings;
            testInputSettings = Instantiate(originalInputSettings);
            InputSystem.settings = testInputSettings;
            originalCaptureDeltaTime = Time.captureDeltaTime;
            settingsCaptured = true;
            Time.captureDeltaTime = 1f / 60f;
            var routine = Check();
            while (true)
            {
                object current = null; bool more = false; Exception error = null;
                try { more = routine.MoveNext(); if (more) current = routine.Current; }
                catch (Exception e) { error = e; }
                if (error != null)
                {
                    File.WriteAllText(Path.Combine(output, "result.txt"), "FAIL\n" + string.Join("\n", checks) + "\n" + error);
                    Debug.LogException(error); Finish(1); yield break;
                }
                if (!more) break;
                yield return current;
            }
            File.WriteAllText(Path.Combine(output, "result.txt"),
                "PASS: " + checks.Count + " integration assertions\n" + string.Join("\n", checks) + "\n");
            Finish(0);
        }

        IEnumerator Check()
        {
            yield return new WaitForSeconds(2);
            workshop = FindFirstObjectByType<ReefWorkshop>();
            pickup = workshop.GetComponent<ReefBrushPickup>();
            var planting = workshop.GetComponent<CoralPlanting>();
            var hud = workshop.GetComponent<ReefCleaningHUD>();
            workshop.desktopPlayer.GetComponent<DiveMotor>().Automated = true;
            pickup.Automated = pickup.corals.Automated = planting.Automated = true;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            keyboard = InputSystem.AddDevice<Keyboard>("V3 test keyboard");
            mouse = InputSystem.AddDevice<Mouse>("V3 test mouse");
            Require(workshop.scraper && pickup.scraper, "Scene contains a wired scraper");
            var menu = workshop.GetComponent<ReefToolMenu>();
            Require(menu && !menu.Owns(CleaningToolKind.Brush) && !menu.Owns(CleaningToolKind.Scraper), "Tools start locked until their first pickup");
            Require(Resources.Load<Sprite>("ToolMenu/Brush") && Resources.Load<Sprite>("ToolMenu/Scraper"), "Both tool icons load from actual model capture sprites");
            Require(!menu.TryEquip(0) && !menu.TryEquip(1) && !menu.TryEquip(4), "Locked and future slots cannot equip tools");
            var debris = workshop.scraper.scrapeDebris;
            Require(debris && debris.Particles && debris.AliveCount == 0, "Scraper VFX is wired and starts empty");
            var sockets = FindObjectsByType<RackSocket>(FindObjectsSortMode.None).Where(s => s.moss).ToArray();
            var fireSocket = sockets.First(s => s.moss.surfaceKind == ReefSurfaceKind.FireCoral);
            var algaeSocket = sockets.First(s => s.moss.surfaceKind == ReefSurfaceKind.Algae);
            var fire = fireSocket.moss; var algae = algaeSocket.moss;
            fire.ApplyStroke(100f, CleaningToolKind.Brush);
            algae.ApplyStroke(100f, CleaningToolKind.Scraper);
            Require(fire.Cleaned == 0 && algae.Cleaned == 0, "Wrong tools never change progress");
            Require(fireSocket.Mossy && planting.Plant(fireSocket) == null, "Covered socket rejects planting");
            var practiceFire = workshop.patches.FirstOrDefault(p => p.name == "Practice spot / fire coral");
            if (practiceFire)
            {
                Require(pickup.practiceSpot.surfaceKind == ReefSurfaceKind.Algae && practiceFire.surfaceKind == ReefSurfaceKind.FireCoral,
                    "Practice station presents both surface types");
                foreach (var sample in workshop.patches.Where(p => p.surfaceKind == ReefSurfaceKind.FireCoral))
                {
                    sample.ApplyStroke(100f, CleaningToolKind.Brush);
                    Require(sample.Cleaned == 0, sample.name + " rejects brush");
                    sample.ApplyStroke(100f, CleaningToolKind.Scraper);
                    Require(sample.Complete && !sample.dirtVisual.enabled, sample.name + " clears with file");
                }
                workshop.ResetWorkshop();
                Require(workshop.patches.All(p => p.Cleaned == 0 && p.dirtVisual.enabled), "Practice and attached tree samples reset together");
            }

            // Isolate contact/input checks from the scene's decorative collisions.
            var fixture = new GameObject("Cleaning test fixture");
            var a = Instantiate(algae, fixture.transform); a.transform.SetPositionAndRotation(new Vector3(0, 40, 0), Quaternion.identity);
            var f = Instantiate(fire, fixture.transform); f.transform.SetPositionAndRotation(new Vector3(3, 40, 0), Quaternion.identity);
            a.requiredTravel = f.requiredTravel = 1.8f;
            workshop.brushHome.SetPositionAndRotation(new Vector3(-1, 40, -2), Quaternion.identity);
            workshop.scraperHome.SetPositionAndRotation(new Vector3(1, 40, -2), Quaternion.identity);
            workshop.ReturnBrush();
            yield return null;
            AimTool(workshop.brush);
            yield return new WaitForSeconds(0.3f);
            Require(pickup.AimingBrush && pickup.brush.Highlighted, "Brush can be aimed at");
            Keys(Key.G); yield return new WaitForSeconds(0.15f); Keys(); yield return new WaitForSeconds(0.2f);
            Require(pickup.Carrying && pickup.ActiveTool == workshop.brush, "G picks up brush");
            Require(menu.Owns(CleaningToolKind.Brush) && !menu.Owns(CleaningToolKind.Scraper), "First pickup unlocks only the brush");
            Require(workshop.brushHome.GetComponentInChildren<MeshRenderer>(), "Tutorial table keeps a brush display while its working tool is carried");
            Aim(f); Click(true); yield return new WaitForSeconds(0.6f); Click(false);
            Require(f.Cleaned == 0 && !pickup.IsScrubbing && hud.Status.Contains("scraper"), "Brush cannot scrape fire coral; HUD names correct tool");
            Require(debris.EmittedCount == 0, "Wrong brush contact emits no coral chips");
            Aim(a); yield return new WaitForSeconds(0.3f);
            Require(a.Cleaned == 0, "Idle brush does not clean");
            var blocker = GameObject.CreatePrimitive(PrimitiveType.Cube);
            blocker.transform.position = pickup.View.transform.position + pickup.View.transform.forward * 0.3f;
            blocker.transform.localScale = Vector3.one * 0.2f; Physics.SyncTransforms();
            Click(true); yield return new WaitForSeconds(0.4f); Click(false);
            Require(a.Cleaned == 0, "Occlusion blocks assisted cleaning");
            Destroy(blocker); yield return null;
            Click(true); yield return new WaitForSeconds(0.7f); Click(false); yield return new WaitForSeconds(0.2f);
            Require(a.Cleaned > 0 && !a.Complete, "Brush moves and partially clears algae");
            float paused = a.Cleaned;
            yield return new WaitForSeconds(0.3f);
            Require(Mathf.Approximately(paused, a.Cleaned), "Releasing mouse pauses cleaning");
            Click(true);
            for (int frame = 0; frame < 900 && !a.Complete; frame++) yield return null;
            Click(false); yield return new WaitForSeconds(0.1f);
            Require(a.Complete && !a.dirtVisual.enabled, "Brush completes algae removal");
            Require(debris.EmittedCount == 0, "Algae brushing emits no coral chips");

            AimTool(workshop.scraper); yield return new WaitForSeconds(0.3f);
            Keys(Key.G); yield return new WaitForSeconds(0.15f); Keys(); yield return new WaitForSeconds(0.2f);
            Require(pickup.Carrying && pickup.ActiveTool == workshop.scraper && !workshop.brush.Held, "Switching tools returns brush");
            a.ResetDirt(); Aim(a); Click(true); yield return new WaitForSeconds(0.5f); Click(false);
            Require(a.Cleaned == 0 && hud.Status.Contains("brush"), "Scraper cannot clear algae; HUD names brush");
            Require(debris.EmittedCount == 0, "Scraping algae emits no coral chips");
            Aim(f); Click(true); yield return new WaitForSeconds(0.7f); Click(false); yield return new WaitForSeconds(0.2f);
            Require(f.Cleaned > 0 && !f.Complete, "Scraper produces real partial contact progress");
            Require(debris.EmittedCount > 0 && debris.AliveCount > 0, "Actual desktop scraping emits coral chips");
            var chips = new ParticleSystem.Particle[96];
            int chipCount = debris.Particles.GetParticles(chips);
            Require(chips.Take(chipCount).All(p => Vector3.Distance(p.position, f.transform.position) < 0.6f),
                "World-space chips originate at the actual scraped fixture");
            Require(chips.Take(chipCount).Any(p => p.GetCurrentColor(debris.Particles).a < 170),
                "Living coral chips lose opacity over time");
            int pausedEmission = debris.EmittedCount;
            yield return new WaitForSeconds(0.25f);
            Require(debris.EmittedCount == pausedEmission, "Releasing mouse stops chip emission immediately");
            Click(true);
            for (int frame = 0; frame < 900 && !f.Complete; frame++) yield return null;
            Click(false); yield return new WaitForSeconds(0.2f);
            Require(f.Complete && !f.dirtVisual.enabled && f.growth.All(t => !t.gameObject.activeSelf), "Fire coral visuals clear progressively to completion");
            Require(hud.DisplayedProgress > 0.999f, "HUD follows the scraper's target");
            Keys(Key.H); yield return new WaitForSeconds(0.2f); Keys();
            Require(!pickup.ToolHeld && Vector3.Distance(workshop.scraper.transform.position, workshop.scraperHome.position) < 0.02f, "H returns scraper");
            int returnedEmission = debris.EmittedCount;
            yield return new WaitForSeconds(1.15f);
            Require(debris.AliveCount == 0 && debris.EmittedCount == returnedEmission, "Returning file leaves no lingering or newly emitted debris");

            // Exercise the same XRI-selected contact sweep used by a tracked controller, with no mouse animation.
            f.ResetDirt(); f.requiredTravel = 2f;
            pickup.scraper.owner = null;
            var tracked = new GameObject("Tracked test hand").AddComponent<ReefPickupInteractor>();
            tracked.attachTransform = tracked.transform; tracked.interactionManager = pickup.hand.interactionManager;
            tracked.keepSelectedTargetValid = true; tracked.Requested = pickup.scraper;
            tracked.transform.rotation = Quaternion.identity;
            Vector3 tipLocal = workshop.scraper.transform.InverseTransformPoint(workshop.scraper.bristles.position);
            Vector3 center = f.transform.position + Vector3.up * 0.025f;
            tracked.transform.position = center - tipLocal;
            tracked.StartManualInteraction((IXRSelectInteractable)pickup.scraper);
            yield return new WaitForSeconds(0.4f);
            int stationaryEmission = debris.EmittedCount;
            f.ResetDirt(); yield return new WaitForSeconds(0.3f);
            Require(f.Cleaned == 0, "Held stationary scraper does not clean");
            Require(debris.EmittedCount == stationaryEmission, "Stationary tracked file emits no debris");
            for (int frame = 0; frame < 30; frame++)
            { tracked.transform.position = center - tipLocal + Vector3.right * (Mathf.Sin(frame * 0.2f) * 0.04f); yield return null; }
            Require(f.Cleaned < 0.001f, "Sideways movement along the blade does not scrape");
            Require(debris.EmittedCount == stationaryEmission, "Rejected sideways strokes emit no debris");
            for (int frame = 0; frame < 40; frame++)
            { tracked.transform.position = center - tipLocal + Vector3.forward * (Mathf.Sin(frame * 0.2f) * 0.065f); yield return null; }
            Require(f.Cleaned > 0.03f, "Forward/backward tracked strokes scrape fire coral");
            Require(debris.EmittedCount > stationaryEmission && debris.AliveCount > 0, "Tracked file strokes emit coral chips");
            foreach (var sample in workshop.patches.Where(p => p.surfaceKind == ReefSurfaceKind.FireCoral))
            {
                var pose = sample.transform.rotation;
                var contact = sample.transform.position + sample.transform.up * 0.025f;
                tracked.transform.SetPositionAndRotation(contact - pose * tipLocal, pose);
                yield return new WaitForSeconds(0.3f);
                sample.ResetDirt();
                int beforeSampleEmission = debris.EmittedCount;
                for (int frame = 0; frame < 45; frame++)
                {
                    tracked.transform.position = contact - pose * tipLocal + sample.transform.forward * (Mathf.Sin(frame * 0.2f) * 0.04f);
                    yield return null;
                }
                Require(sample.Cleaned > 0.01f, sample.name + " receives tracked file contact at its surface angle");
                Require(debris.EmittedCount > beforeSampleEmission, sample.name + " emits debris during tracked contact");
            }
            tracked.EndManualInteraction(); tracked.Requested = null; Destroy(tracked.gameObject);
            pickup.scraper.owner = pickup;

            var shop = FindFirstObjectByType<CoralShop>();
            var stock = shop.stock.First(c => CoralRules.CanPlant(CoralRules.Species(c.gameObject), fireSocket.grade));
            var seed = Instantiate(stock); seed.name = CoralRules.Species(stock.gameObject);
            var fragment = CoralSeating.MakePickable(seed, pickup.corals, shop.outline, pickup.hand.interactionManager);
            pickup.corals.Give(fragment); yield return null;
            Require(planting.Plant(fireSocket) == null && pickup.corals.Held == fragment, "Fire coral blocks a compatible held coral without consuming it");
            fire.ApplyStroke(100f, CleaningToolKind.Scraper);
            Require(!fireSocket.Mossy, "Scraping clears the planting gate");
            var planted = planting.Plant(fireSocket);
            Require(planted && fireSocket.occupant == seed && !pickup.corals.Held, "Cleared fire coral socket accepts planting");
            var nurture = new GameObject("Fire coral timer test").AddComponent<CoralNurture>();
            var growth = nurture.gameObject.AddComponent<CoralGrowth>();
            nurture.coral = growth; nurture.socket = fireSocket; nurture.secondsPerStage = 1000;
            yield return new WaitForSeconds(0.2f);
            Require(fire.Complete, "Algae regrowth timer does not respawn fire coral");
            algae.ApplyStroke(100f, CleaningToolKind.Brush);
            nurture.socket = algaeSocket;
            yield return new WaitForSeconds(0.2f);
            Require(!algae.Complete, "Algae still returns on its care timer");
            Destroy(nurture.gameObject);
            workshop.ResetWorkshop(); yield return null;
            Require(!pickup.ToolHeld && fire.Cleaned == 0 && algae.Cleaned == 0 && fire.dirtVisual.enabled, "Reset restores both surface types and tools");
            Require(debris.AliveCount == 0, "Nursery reset clears remaining debris");
            Require(menu.Owns(CleaningToolKind.Brush) && menu.Owns(CleaningToolKind.Scraper), "Reset retains tools acquired this session");
            Keys(Key.Tab); yield return null; Keys(); yield return new WaitForSeconds(.35f);
            Require(menu.IsOpen && Cursor.lockState == CursorLockMode.None, "Tab opens the menu and releases the mouse");
            var slotScreen = RectTransformUtility.WorldToScreenPoint(null, menu.SlotRect(0).position);
            mouse.MakeCurrent(); InputSystem.QueueStateEvent(mouse,new MouseState { position=slotScreen }); yield return null;
            InputSystem.QueueStateEvent(mouse,new MouseState { position=slotScreen, buttons=1 }); yield return new WaitForSeconds(.1f);
            Require(!menu.IsOpen && pickup.Carrying && pickup.ActiveTool == workshop.brush, "Clicking a pebble equips the brush and closes the menu");
            Require(ReefToolMenu.BlocksWorldInput, "The selection click remains consumed until released");
            Click(false); yield return new WaitForSeconds(.1f);
            Require(!ReefToolMenu.BlocksWorldInput, "Releasing the selection click restores world input");
            menu.SetOpen(true); yield return new WaitForSeconds(.3f);
            Require(workshop.brush.GetComponentsInChildren<Renderer>().All(r=>r.forceRenderingOff), "Opening the menu hides the held tool without dropping it");
            Aim(algae); var beforeMenuClean=algae.Cleaned;
            Click(true); yield return new WaitForSeconds(.4f); Click(false); yield return null;
            Require(algae.Cleaned == beforeMenuClean && !pickup.IsScrubbing, "Mouse clicks while the menu is open do not scrub the world");
            Require(menu.TryEquip(1), "An acquired scraper can be selected from the menu");
            yield return new WaitForSeconds(.2f);
            Require(pickup.ActiveTool == workshop.scraper && pickup.Carrying && !workshop.brush.Held, "Menu swapping retains exactly one held tool");
            Require(workshop.brush.GetComponentsInChildren<Renderer>().All(r=>!r.forceRenderingOff), "Swapping restores the old tool's rendering state");
            menu.SetOpen(true); yield return null;
            Keys(Key.Escape); yield return null; Keys(); yield return new WaitForSeconds(.1f);
            Require(!menu.IsOpen && workshop.scraper.GetComponentsInChildren<Renderer>().All(r=>!r.forceRenderingOff), "Escape closes the menu and restores the selected tool");
            menu.SetOpen(true); yield return null;
            var motor=workshop.desktopPlayer.GetComponent<DiveMotor>(); motor.Automated=false;
            var beforeSwim=motor.transform.position;
            Keys(Key.W); yield return new WaitForSeconds(.4f); Keys(); motor.Automated=true;
            Require(Vector3.Distance(beforeSwim,motor.transform.position)>.01f && menu.IsOpen, "Swimming continues while the tool menu is open");
            menu.SetOpen(false); yield return new WaitForSeconds(.1f);
            var simulator=workshop.xrGroup.GetComponentInChildren<UnityEngine.XR.Interaction.Toolkit.Inputs.Simulation.XRInteractionSimulator>(true);
            Require(simulator.cycleDevicesInput.inputActionPerformed.bindings.Any(b=>b.effectivePath=="<Keyboard>/backquote"), "Simulator device cycling uses backquote instead of Tab");
            workshop.ReturnBrush();

            // Exercise the real action bindings and tracked-pose driver without physical hardware.
            workshop.desktopPlayer.SetActive(false); workshop.xrGroup.SetActive(true);
            yield return null;
            simulator.enabled=false;
            rightController=InputSystem.AddDevice<XRSimulatedController>("Tool menu test right hand");
            InputSystem.SetDeviceUsage(rightController,UnityEngine.InputSystem.CommonUsages.RightHand);
            var controllerState=new XRSimulatedControllerState { isTracked=true,trackingState=3,
                devicePosition=new Vector3(.2f,1.1f,.4f),deviceRotation=Quaternion.identity };
            InputSystem.QueueStateEvent(rightController,controllerState); yield return new WaitForSeconds(.2f);
            Require(!pickup.CenterMode,"Controller quick selection uses tracked mode");
            InputSystem.QueueStateEvent(rightController,controllerState.WithButton(ControllerButton.PrimaryButton));
            yield return new WaitForSeconds(.3f);
            Require(menu.IsOpen,"Right A opens the tool menu through its XR action binding");
            controllerState.buttons=0;InputSystem.QueueStateEvent(rightController,controllerState);yield return null;
            slotScreen=RectTransformUtility.WorldToScreenPoint(null,menu.SlotRect(1).position);
            InputSystem.QueueStateEvent(mouse,new MouseState {position=slotScreen});yield return null;
            InputSystem.QueueStateEvent(rightController,controllerState.WithButton(ControllerButton.TriggerButton));
            yield return new WaitForSeconds(.2f);
            Require(!menu.IsOpen && pickup.Carrying && pickup.ActiveTool==workshop.scraper,"Controller trigger selects a pointed slot and equips its tool");
            controllerState.buttons=0;InputSystem.QueueStateEvent(rightController,controllerState);yield return new WaitForSeconds(.15f);
            var toolBefore=workshop.scraper.transform.position;
            controllerState.devicePosition+=Vector3.right*.15f;
            InputSystem.QueueStateEvent(rightController,controllerState);yield return new WaitForSeconds(.2f);
            Require(pickup.Carrying && Vector3.Distance(toolBefore,workshop.scraper.transform.position)>.1f,
                "Quick-equipped tool follows tracked controller motion without holding Grip");
            InputSystem.QueueStateEvent(rightController,controllerState.WithButton(ControllerButton.PrimaryButton));yield return new WaitForSeconds(.2f);
            Require(menu.IsOpen && workshop.scraper.GetComponentsInChildren<Renderer>().All(r=>r.forceRenderingOff),"Controller menu hides the tracked tool");
            controllerState.buttons=0;InputSystem.QueueStateEvent(rightController,controllerState);yield return null;
            InputSystem.QueueStateEvent(rightController,controllerState.WithButton(ControllerButton.SecondaryButton));yield return new WaitForSeconds(.2f);
            Require(!menu.IsOpen && pickup.Carrying && workshop.scraper.GetComponentsInChildren<Renderer>().All(r=>!r.forceRenderingOff),"Right B closes the menu and restores the tracked tool");
            controllerState.buttons=0;InputSystem.QueueStateEvent(rightController,controllerState);yield return null;
            workshop.ReturnBrush();
            Destroy(fixture);
        }

        void Aim(RackDirtPatch patch)
        {
            pickup.View.transform.position = patch.transform.position + new Vector3(0, 0.35f, -0.8f);
            pickup.View.transform.LookAt(patch.transform.position + Vector3.up * 0.025f); Physics.SyncTransforms();
        }
        void AimTool(ReefBrush tool)
        {
            var at = tool.transform.TransformPoint(new Vector3(0, 0, 0.18f));
            pickup.View.transform.position = at + new Vector3(0, 0.25f, -0.8f);
            pickup.View.transform.LookAt(at); Physics.SyncTransforms();
        }
        void Keys(params Key[] keys) { keyboard.MakeCurrent(); InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys)); }
        void Click(bool down) { mouse.MakeCurrent(); InputSystem.QueueStateEvent(mouse, new MouseState { buttons = (ushort)(down ? 1 : 0) }); }
        void Require(bool valid, string message)
        {
            if (!valid) throw new InvalidOperationException("V3: " + message);
            checks.Add(message); Debug.Log("V3_CHECK: " + message);
        }
        void OnDestroy() => RestoreTestSettings();
        void RestoreTestSettings()
        {
            if (!settingsCaptured) return;
            settingsCaptured = false;
            if (keyboard != null && keyboard.added) InputSystem.RemoveDevice(keyboard);
            if (mouse != null && mouse.added) InputSystem.RemoveDevice(mouse);
            if (rightController != null && rightController.added) InputSystem.RemoveDevice(rightController);
            InputSystem.settings = originalInputSettings;
            Time.captureDeltaTime = originalCaptureDeltaTime;
            if (testInputSettings) Destroy(testInputSettings);
        }
        void Finish(int code)
        {
            RestoreTestSettings();
#if UNITY_EDITOR
            if (Application.isBatchMode) UnityEditor.EditorApplication.Exit(code);
            else UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit(code);
#endif
        }
    }
}
