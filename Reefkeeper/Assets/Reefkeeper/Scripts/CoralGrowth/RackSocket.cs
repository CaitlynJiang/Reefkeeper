using UnityEngine;

namespace Reefkeeper.Corals
{
    /// <summary>
    /// A planting point. <see cref="kind"/> says how a coral sits (on a table, or hanging from
    /// a hook); <see cref="grade"/> says which corals are allowed (see <see cref="CoralRules"/>).
    /// A socket can be covered in moss, which must be brushed off before planting and which
    /// pauses growth when it comes back.
    /// </summary>
    public sealed class RackSocket : MonoBehaviour
    {
        public enum Kind { Table, Hang }
        public enum Grade { A, B, C }

        public Kind kind;
        [Tooltip("A: Mushroom (Disc) coral only. B: any flat-based coral. C: hanging corals.")]
        public Grade grade = Grade.B;
        [Tooltip("What is planted here, if anything.")] public CoralGrowth occupant;
        [Tooltip("Moss covering this socket; cleaned with the brush.")] public RackDirtPatch moss;

        public bool IsFree => !occupant;
        public bool Mossy => moss && !moss.Complete;

        void OnDrawGizmos()
        {
            Gizmos.color = grade switch
            {
                Grade.A => new Color(1f, 0.55f, 0.3f),
                Grade.B => new Color(1f, 0.85f, 0.35f),
                _ => new Color(0.4f, 0.9f, 0.8f),
            };
            Gizmos.DrawWireSphere(transform.position, 0.04f);
        }
    }
}
