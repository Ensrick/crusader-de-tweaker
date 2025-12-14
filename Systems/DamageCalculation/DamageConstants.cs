// Systems/DamageCalculation/DamageConstants.cs
namespace CrusaderDETweaker.Systems.DamageCalculation
{
    /// <summary>
    /// Constants used in damage calculations.
    /// Centralizes magic numbers for easier maintenance and testing.
    /// </summary>
    internal static class DamageConstants
    {
        /// <summary>
        /// Minimum damage floor - all attacks deal at least this much damage.
        /// </summary>
        public const int MinimumDamage = 2;

        /// <summary>
        /// Weak attacker threshold - attackers with base damage at or below this
        /// deal flat damage, ignoring ALL modifiers.
        /// </summary>
        public const int WeakAttackerThreshold = 2;

        /// <summary>
        /// Weak sword unit threshold for SiegeDefense exception.
        /// Units with base damage <= this and Ranged_Bow or Cavalry tags deal minimum damage vs SiegeDefense.
        /// </summary>
        public const int WeakSwordSiegeDefenseThreshold = 20;

        /// <summary>
        /// Large beast threshold - beasts with base damage >= this ignore armor.
        /// </summary>
        public const int LargeBeastThreshold = 50;

        /// <summary>
        /// Small beast threshold - beasts with base damage <= this use armor with caps.
        /// </summary>
        public const int SmallBeastThreshold = 10;

        /// <summary>
        /// Medium-strength mace unit threshold - units with base damage in this range
        /// deal flat base damage vs Heavy armor.
        /// </summary>
        public const int MediumMaceHeavyThresholdMin = 20;
        public const int MediumMaceHeavyThresholdMax = 75;

        /// <summary>
        /// Assassin damage multipliers.
        /// </summary>
        public static class Assassin
        {
            public const float UnarmoredMultiplier = 3.125f;
            public const float LightArmorMultiplier = 2.5f; // Average of varying values
        }

        /// <summary>
        /// Ranged melee damage multipliers.
        /// </summary>
        public static class RangedMelee
        {
            public const float HeavyArmorMultiplier = 3.0f;
            public const float LightArmorRangedDefenderMultiplier = 1.33f;
        }

        /// <summary>
        /// Damage cap multipliers for various weapon types.
        /// </summary>
        public static class DamageCaps
        {
            // Sword caps
            public const float SwordUnarmoredCap = 1.5f;
            public const float SwordLightCap = 1.25f;
            public const float SwordCavalryUnarmoredCap = 1.25f;

            // Mace caps
            public const float MaceUnarmoredWeakCap = 1.5f;
            public const float MaceUnarmoredMediumCap = 1.0f;
            public const float MaceLightWeakCap = 1.25f;
            public const float MaceLightMediumCap = 1.0f;

            // Lance caps
            public const float LanceCavalryUnarmoredCap = 1.0f; // For medium-strength units

            // Axe caps
            public const float AxeUnarmoredMediumCap = 1.6f;
            public const float AxeLightWeakCap = 1.5f;
            public const float AxeLightMediumCap = 1.2f;

            // Ranged caps
            public const float RangedUnarmoredSmallCap = 2.0f;
            public const float RangedUnarmoredLargeCap = 1.25f;
            public const float RangedLightSmallCap = 2.0f;
            public const float RangedLightLargeCap = 1.25f;
            public const float RangedHunterUnarmoredCap = 2.0f;
            public const float RangedHunterLightCap = 1.5f;
        }

        /// <summary>
        /// Siege defense multipliers for strong melee weapons.
        /// </summary>
        public static class SiegeDefense
        {
            public const float SwordMultiplier = 0.4f;
            public const float MaceMultiplier = 1.0f;
            public const float LanceMultiplier = 0.375f;
            public const float AxeMultiplier = 1.0f;
            public const float PolearmMultiplier = 1.0f;
        }
    }
}

