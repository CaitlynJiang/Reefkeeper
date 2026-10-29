using UnityEngine;

namespace Reefkeeper.Corals
{
    /// <summary>
    /// A planting point on a rack base. Table sockets take a table coral (cradles, stone tops);
    /// hang sockets take a hung fragment (hooks on the hanging nursery).
    /// Only a marker for now; the planting system will read these.
    /// </summary>
    public sealed class RackSocket : MonoBehaviour
    {
        public enum Kind { Table, Hang }

        public Kind kind;
        [Tooltip("What is planted here, if anything.")] public CoralGrowth occupant;

        public bool IsFree => !occupant;

        void OnDrawGizmos()
        {
            Gizmos.color = kind == Kind.Table ? new Color(1f, 0.75f, 0.3f) : new Color(0.4f, 0.9f, 0.8f);
            Gizmos.DrawWireSphere(transform.position, 0.04f);
        }
    }
}
