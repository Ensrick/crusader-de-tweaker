using System;
using CrusaderDETweaker.Config.DamageMatrix.Core;
using CrusaderDETweaker.Config.Toml.Core;
using CrusaderDETweaker.Data;
using SHCDESE.Interop;

namespace CrusaderDETweaker.Config.Toml.Projectiles.Properties
{
    /// <summary>
    /// Handles the BaseDamage property for projectiles.
    /// 
    /// Base damage is determined by the damage dealt TO the Engineer unit,
    /// since the Engineer has 1.0 armor from all sources and serves as the reference target.
    /// </summary>
    internal class ProjectileBaseDamageProperty : PropertyHandler<ProjectileType, int>
    {
        private const eChimps ReferenceUnit = eChimps.CHIMP_TYPE_ENGINEER;

        public ProjectileBaseDamageProperty() : base("BaseDamage")
        {
        }

        protected override bool TryGetFromAPI(ProjectileType projectile, out int value)
        {
            value = ProjectileApiHelper.GetRangedDamage(projectile, ReferenceUnit);
            return value >= 0;
        }

        protected override void SetToAPI(ProjectileType projectile, int newBaseDamage)
        {
            try
            {
                ProjectileApiHelper.SetRangedDamage(projectile, ReferenceUnit, newBaseDamage);
                Plugin.Logger.LogDebug($"Set {projectile} base damage to {newBaseDamage}");
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogError($"Failed to set {projectile} base damage: {ex.Message}");
            }
        }

        internal override bool ValidateValue(int value)
        {
            return value >= 0 && value <= 10000;
        }

        internal override bool CanApplyTo(ProjectileType projectile)
        {
            return true;
        }

        protected override bool TryGetOriginalValue(ProjectileType projectile, out int defaultValue)
        {
            defaultValue = CsvMatrixReader.GetOriginalRangedDamage(projectile, ReferenceUnit);
            return defaultValue >= 0;
        }
    }
}
