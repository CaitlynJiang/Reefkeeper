using UnityEngine;

namespace Reefkeeper.Corals
{
    /// <summary>
    /// Which coral may be planted on which socket grade, plus the player-facing names.
    /// A: only DiscCoral (mushroom coral). B: any coral that needs a flat base (all but
    /// FingerCoral). C: corals that hang from a point (FingerCoral).
    /// </summary>
    public static class CoralRules
    {
        /// <summary>Stages the player sees (the models may have more; only the first three are used).</summary>
        public const int StageCount = 3;

        public const string Disc = "DiscCoral";
        public const string Finger = "FingerCoral";

        /// <summary>Species id from a coral object's name, e.g. "DiscCoral(Clone)" -> "DiscCoral".</summary>
        public static string Species(GameObject coral)
        {
            var name = coral.name;
            int cut = name.IndexOfAny(new[] { '(', ' ', '@' });
            return cut > 0 ? name.Substring(0, cut) : name;
        }

        public static bool CanPlant(string species, RackSocket.Grade grade) => grade switch
        {
            RackSocket.Grade.A => species == Disc,
            RackSocket.Grade.B => species != Finger,
            RackSocket.Grade.C => species == Finger,
            _ => false,
        };

        public static string DisplayName(string species) => species switch
        {
            "BarrelSponge" => "Barrel Sponge",
            "BranchCoral" => "Branch Coral",
            "DiscCoral" => "Mushroom Coral",
            "FingerCoral" => "Finger Coral",
            "PolypPillar_Coral" => "Coral Polyp Pillar",
            "PolypPillar_Pink" => "Pink Polyp Pillar",
            "TendrilCoral" => "Tendril Coral",
            "TubeSponge_Blossom" => "Blossom Tube Sponge",
            "TubeSponge_Lemon" => "Lemon Tube Sponge",
            "TubeSponge_Magenta" => "Magenta Tube Sponge",
            _ => species,
        };

        /// <summary>Where this coral can go, as a short hint.</summary>
        public static string Needs(string species) => species switch
        {
            Disc => "Plant on a stone cairn or a flat cradle",
            Finger => "Hang it on a hook",
            _ => "Plant on a flat cradle",
        };

        public static string GradeName(RackSocket.Grade grade) => grade switch
        {
            RackSocket.Grade.A => "stone cairn",
            RackSocket.Grade.B => "flat cradle",
            _ => "hanging hook",
        };
    }
}
