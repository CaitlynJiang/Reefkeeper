using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace Reefkeeper
{
    public sealed class ReefWorkshop : MonoBehaviour
    {
        public GameObject desktopPlayer, xrGroup;
        public ReefBrush brush;
        public ReefBrush scraper;
        public RackDirtPatch[] patches;
        public Transform brushHome;
        public Transform scraperHome;
        public float Progress
        {
            get { float total = 0; foreach (var patch in patches) total += patch.Cleaned; return patches.Length > 0 ? total / patches.Length : 0; }
        }
        void Awake()
        {
            // Existing desktop checks can still run on the same scene.
            var args = System.Environment.GetCommandLineArgs();
            bool desktopSmoke = System.Array.IndexOf(args, "-reef-smoke") >= 0 || System.Array.IndexOf(args, "-reef-pickup-smoke") >= 0 || System.Array.IndexOf(args, "-reef-cleaning-smoke") >= 0 || System.Array.IndexOf(args, "-reef-v3-smoke") >= 0;
            xrGroup.SetActive(!desktopSmoke); desktopPlayer.SetActive(desktopSmoke);
        }
        void Update()
        {
            var k = Keyboard.current;
            if (k == null) return;
            if (k.f2Key.wasPressedThisFrame)
            {
                ReturnBrush();
                bool xr = !xrGroup.activeSelf;
                xrGroup.SetActive(xr); desktopPlayer.SetActive(!xr);
                Cursor.lockState = xr ? CursorLockMode.None : CursorLockMode.Locked;
                Cursor.visible = xr;
            }
            if (k.f5Key.wasPressedThisFrame) ResetWorkshop();
        }
        public void ResetWorkshop()
        {
            if (scraper && scraper.scrapeDebris) scraper.scrapeDebris.Clear();
            foreach (var patch in patches) patch.ResetDirt();
            ReturnBrush();
            var corals = GetComponent<ReefCoralPickup>();
            if (corals) corals.ResetCorals();
            var planting = GetComponent<Corals.CoralPlanting>();
            if (planting) planting.ResetAll();
        }
        public void ReturnBrush()
        {
            var pickup = GetComponent<ReefBrushPickup>();
            if (pickup) pickup.Release();
            ReturnTool(brush);
            if (scraper) ReturnTool(scraper);
        }
        public void ReturnTool(ReefBrush tool)
        {
            if (!tool) return;
            var home = tool == scraper ? scraperHome : brushHome;
            var grab = tool.GetComponent<XRGrabInteractable>();
            if (grab.isSelected) grab.interactionManager.CancelInteractableSelection((IXRSelectInteractable)grab);
            var body = tool.GetComponent<Rigidbody>();
            body.linearVelocity = Vector3.zero; body.angularVelocity = Vector3.zero;
            tool.transform.SetPositionAndRotation(home.position, home.rotation);
            body.position = home.position; body.rotation = home.rotation;
            Physics.SyncTransforms();
        }
    }
}
