using System.Linq;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace Reefkeeper.Corals
{
    /// <summary>
    /// Puts corals onto sockets and turns them into things the player can carry.
    /// Shared by the editor rack tools and the runtime planting/shop code.
    /// </summary>
    public static class CoralSeating
    {
        public const float HangGap = 0.06f;   // space between hook and coral top, filled by a line
        const float LineWidth = 0.006f;

        /// <summary>
        /// Parents the coral to its socket at world scale 1: sitting on a table socket, or hanging
        /// just below a hook on a short line. Reuses <paramref name="line"/> when given; returns the
        /// line in use (null for table sockets).
        /// </summary>
        public static GameObject Seat(CoralGrowth coral, RackSocket socket, GameObject line, Material rope)
        {
            var t = coral.transform;
            var yaw = Quaternion.Euler(0, t.eulerAngles.y, 0);
            t.SetParent(socket.transform, false);
            t.rotation = yaw;
            t.localScale = Divide(Vector3.one, socket.transform.lossyScale);
            t.position = socket.transform.position;

            if (socket.kind == RackSocket.Kind.Table)
            {
                if (line) DestroyAny(line);
                return null;
            }

            float height = coral.Current.GetComponentsInChildren<Renderer>().Max(r => r.bounds.max.y) - t.position.y;
            t.position = socket.transform.position + Vector3.down * (height + HangGap);
            if (!line)
            {
                line = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                DestroyAny(line.GetComponent<Collider>());
                line.name = "Coral line";
                if (rope) line.GetComponent<Renderer>().sharedMaterial = rope;
            }
            line.transform.SetParent(socket.transform, false);
            line.transform.SetPositionAndRotation(socket.transform.position + Vector3.down * (HangGap * 0.5f), Quaternion.identity);
            line.transform.localScale = Divide(new Vector3(LineWidth, HangGap * 0.5f, LineWidth), socket.transform.lossyScale);
            return line;
        }

        /// <summary>
        /// Wraps a growth coral so the pickup flow can carry it. The object is briefly disabled so the
        /// grab component wakes up with its collider, outline and manager already set.
        /// </summary>
        public static ReefCoralFragment MakePickable(CoralGrowth coral, ReefCoralPickup pickup, Material outline, XRInteractionManager manager)
        {
            var go = coral.gameObject;
            bool wasActive = go.activeSelf;
            go.SetActive(false);
            var bounds = new Bounds(go.transform.position, Vector3.zero);
            foreach (var r in coral.Current.GetComponentsInChildren<Renderer>(true)) bounds.Encapsulate(r.bounds);
            var box = go.AddComponent<BoxCollider>();
            box.center = go.transform.InverseTransformPoint(bounds.center);
            var size = go.transform.InverseTransformVector(bounds.size);
            box.size = Vector3.Max(new Vector3(Mathf.Abs(size.x), Mathf.Abs(size.y), Mathf.Abs(size.z)), Vector3.one * 0.12f);

            var body = go.GetComponent<Rigidbody>();
            if (!body) body = go.AddComponent<Rigidbody>();
            body.useGravity = false; body.isKinematic = true; body.mass = 0.04f;

            var fragment = go.AddComponent<ReefCoralFragment>();
            fragment.owner = pickup;
            fragment.outlineMaterial = outline;
            fragment.interactionManager = manager;
            fragment.movementType = XRBaseInteractable.MovementType.Instantaneous;
            fragment.throwOnDetach = false;
            fragment.useDynamicAttach = false;
            fragment.attachEaseInTime = 0.12f;
            fragment.selectMode = InteractableSelectMode.Single;
            fragment.colliders.Clear();
            fragment.colliders.Add(box);
            var grip = new GameObject("Grip pose").transform;
            grip.SetParent(go.transform, false);
            grip.position = bounds.center;
            fragment.attachTransform = grip;
            go.SetActive(wasActive);
            return fragment;
        }

        static void DestroyAny(Object o)
        {
            if (!o) return;
            if (Application.isPlaying) Object.Destroy(o); else Object.DestroyImmediate(o);
        }

        static Vector3 Divide(Vector3 a, Vector3 b) => new Vector3(a.x / b.x, a.y / b.y, a.z / b.z);
    }
}
