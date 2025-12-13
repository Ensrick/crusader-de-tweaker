// Config/Toml/ProjectileDamage/ProjectileDamageConfig.cs
namespace CrusaderDETweaker.Config.Toml.ProjectileDamage
{
    /// <summary>
    /// Base projectile damage constant.
    /// All projectile types (Bow, Crossbow, Sling, Javelin) use the same base damage.
    /// Differences between projectile types are handled by armor modifiers in ArmorConfig.
    /// </summary>
    internal static class ProjectileDamageConfig
    {
        /// <summary>
        /// Base damage for all projectile types (before armor modifiers).
        /// Formula: FinalDamage = BaseProjectileDamage × UnitArmorValue × RangedArmorModifier
        /// </summary>
        public const int BaseProjectileDamage = 2500;
    }
}