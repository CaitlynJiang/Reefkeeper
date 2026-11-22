using System;

namespace Reefkeeper.Photos
{
    /// <summary>
    /// Every photographable species, in photo-wall order (board slots S1_R1, S1_R2, ... S6_R2).
    /// The board has 16 photos; cards past the end of this list are hidden.
    /// Ids match the scene: fish model names, the crab, and coral species ids (CoralNurture.species).
    /// </summary>
    public static class ReefSpecies
    {
        public enum Kind { Animal, Coral }

        public static readonly (string id, string name, Kind kind)[] All =
        {
            ("Fish1", "Reef Shoal Fish", Kind.Animal),
            ("Fish2", "Blue Reef Fish", Kind.Animal),
            ("Fish3", "Striped Reef Fish", Kind.Animal),
            ("Manta ray", "Manta Ray", Kind.Animal),
            // Lucky Cat took the coral crab's place; the crab ("CoralCrab", "Coral Crab") is off the wall for now.
            ("LuckyCat", "Lucky Cat", Kind.Animal),
            ("BarrelSponge", "Barrel Sponge", Kind.Coral),
            ("BranchCoral", "Branch Coral", Kind.Coral),
            ("DiscCoral", "Mushroom Coral", Kind.Coral),
            ("FingerCoral", "Finger Coral", Kind.Coral),
            ("PolypPillar_Coral", "Coral Polyp Pillar", Kind.Coral),
            ("PolypPillar_Pink", "Pink Polyp Pillar", Kind.Coral),
            ("RedCoral", "Red Coral", Kind.Coral),
            ("TendrilCoral", "Tendril Coral", Kind.Coral),
            ("TubeSponge_Blossom", "Blossom Tube Sponge", Kind.Coral),
            ("TubeSponge_Lemon", "Lemon Tube Sponge", Kind.Coral),
            ("TubeSponge_Magenta", "Magenta Tube Sponge", Kind.Coral),
        };

        public static int Count => All.Length;
        public static int IndexOf(string id) => Array.FindIndex(All, s => s.id == id);
        public static bool Known(string id) => IndexOf(id) >= 0;
        public static string DisplayName(string id) { int i = IndexOf(id); return i >= 0 ? All[i].name : id; }

        /// <summary>Species id from a scene object name: "Fish2 (3)" -> "Fish2", "DiscCoral(Clone)" -> "DiscCoral".</summary>
        public static string FromObjectName(string name)
        {
            int cut = name.IndexOfAny(new[] { '(', '@', '/' });
            return (cut > 0 ? name.Substring(0, cut) : name).Trim();
        }
    }
}
