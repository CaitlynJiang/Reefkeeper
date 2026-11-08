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

namespace Reefkeeper
{
    /// <summary>Opt-in Play Mode integration check; absent from normal gameplay.</summary>
    public sealed class ReefV3SmokeCheck : MonoBehaviour
    {
        Keyboard keyboard;
        Mouse mouse;
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
            Aim(f); Click(true); yield return new WaitForSeconds(0.6f); Click(false);
            Require(f.Cleaned == 0 && !pickup.IsScrubbing && hud.Status.Contains("scraper"), "Brush cannot scrape fire coral; HUD names correct tool");
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

            AimTool(workshop.scraper); yield return new WaitForSeconds(0.3f);
            Keys(Key.G); yield return new WaitForSeconds(0.15f); Keys(); yield return new WaitForSeconds(0.2f);
            Require(pickup.Carrying && pickup.ActiveTool == workshop.scraper && !workshop.brush.Held, "Switching tools returns brush");
            a.ResetDirt(); Aim(a); Click(true); yield return new WaitForSeconds(0.5f); Click(false);
            Require(a.Cleaned == 0 && hud.Status.Contains("brush"), "Scraper cannot clear algae; HUD names brush");
            Aim(f); Click(true); yield return new WaitForSeconds(0.7f); Click(false); yield return new WaitForSeconds(0.2f);
            Require(f.Cleaned > 0 && !f.Complete, "Scraper produces real partial contact progress");
            Click(true);
            for (int frame = 0; frame < 900 && !f.Complete; frame++) yield return null;
            Click(false); yield return new WaitForSeconds(0.2f);
            Require(f.Complete && !f.dirtVisual.enabled && f.growth.All(t => !t.gameObject.activeSelf), "Fire coral visuals clear progressively to completion");
            Require(hud.DisplayedProgress > 0.999f, "HUD follows the scraper's target");
            Keys(Key.H); yield return new WaitForSeconds(0.2f); Keys();
            Require(!pickup.ToolHeld && Vector3.Distance(workshop.scraper.transform.position, workshop.scraperHome.position) < 0.02f, "H returns scraper");

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
            f.ResetDirt(); yield return new WaitForSeconds(0.3f);
            Require(f.Cleaned == 0, "Held stationary scraper does not clean");
            for (int frame = 0; frame < 30; frame++)
            { tracked.transform.position = center - tipLocal + Vector3.right * (Mathf.Sin(frame * 0.2f) * 0.04f); yield return null; }
            Require(f.Cleaned < 0.001f, "Sideways movement along the blade does not scrape");
            for (int frame = 0; frame < 40; frame++)
            { tracked.transform.position = center - tipLocal + Vector3.forward * (Mathf.Sin(frame * 0.2f) * 0.065f); yield return null; }
            Require(f.Cleaned > 0.03f, "Forward/backward tracked strokes scrape fire coral");
            foreach (var sample in workshop.patches.Where(p => p.surfaceKind == ReefSurfaceKind.FireCoral))
            {
                var pose = sample.transform.rotation;
                var contact = sample.transform.position + sample.transform.up * 0.025f;
                tracked.transform.SetPositionAndRotation(contact - pose * tipLocal, pose);
                yield return new WaitForSeconds(0.3f);
                sample.ResetDirt();
                for (int frame = 0; frame < 45; frame++)
                {
                    tracked.transform.position = contact - pose * tipLocal + sample.transform.forward * (Mathf.Sin(frame * 0.2f) * 0.04f);
                    yield return null;
                }
                Require(sample.Cleaned > 0.01f, sample.name + " receives tracked file contact at its surface angle");
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
