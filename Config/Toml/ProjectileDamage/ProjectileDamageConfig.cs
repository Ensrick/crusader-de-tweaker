// Config/Toml/ProjectileDamage/ProjectileDamageConfig.cs
using CrusaderDETweaker;

namespace CrusaderDETweaker.Config.Toml.ProjectileDamage
{
    /// <summary>
    /// Base projectile damage constant.
    /// All projectile types (Bow, Crossbow, Sling, Javelin) use the same base damage.
    /// Differences between projectile types are handled by ranged armor modifiers in Tags config (Ranged_Bow vs Armor_Heavy, etc.).
    /// </summary>
    internal static class ProjectileDamageConfig
    {
        /// <summary>
        /// Base damage for all projectile types (before armor modifiers and BepInEx multiplier).
        /// Formula: FinalDamage = BaseProjectileDamage × UnitRangedDamageTakenMultiplier × UnitArmorValue × RangedArmorModifier
        /// </summary>
        public const int BaseProjectileDamage = 2500;

        /// <summary>
        /// Gets the effective base projectile damage after applying the BepInEx multiplier.
        /// </summary>
        public static float GetEffectiveBaseProjectileDamage()
        {
#pragma warning disable CS0618 // Type or member is obsolete
            if (ConfigManagerBepinex.UnitRangedDamageTakenMultiplier == null)
                return BaseProjectileDamage;

            return BaseProjectileDamage * ConfigManagerBepinex.UnitRangedDamageTakenMultiplier.Value;
#pragma warning restore CS0618 // Type or member is obsolete
        }
    }
}