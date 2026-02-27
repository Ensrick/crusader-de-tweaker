// Data/RangedArmorMultipliers.cs
//
// PURPOSE: Data structure for ranged armor multipliers (one per projectile type).
//
// USAGE:
// - Used by RangedArmorMultipliersProperty to store armor values for Arrow, Bolt, Slinger, Javelin
// - TOML format: RangedArmorMultipliers = { Arrow = 0.075, Bolt = 0.250, Slinger = 0.075, Javelin = 0.050 }
//
// IMPORTANT FOR AI AGENTS:
// - Wrapper around generic ArmorMultipliers<ProjectileType> for TOML compatibility
// - Maintains explicit properties (Arrow, Bolt, etc.) for Tomlyn serialization
// - Lower multiplier = better armor (takes less damage)
// - See ArmorMultipliers.cs for generic implementation
//
using System;
using System.Collections.Generic;

namespace CrusaderDETweaker.Data
{
    /// <summary>
    /// Represents ranged armor multipliers for all projectile types.
    ///
    /// Each unit has different armor against each projectile type (armor-piercing mechanics).
    /// Formula: Damage_Taken = Base_Projectile_Damage � ArmorMultiplier[ProjectileType]
    ///
    /// Base projectile damage (from Engineer):
    /// - Arrow: 2000
    /// - Bolt: 10000
    /// - Slinger: 2000
    /// - Javelin: 4000
    ///
    /// Example (Knight):
    /// - Arrow: 0.075x (takes only 150 damage from 2000 base)
    /// - Bolt: 0.250x (armor-piercing, takes 2500 damage from 10000 base)
    /// - Slinger: 0.075x (takes only 150 damage from 2000 base)
    /// - Javelin: 0.050x (best armor, takes only 200 damage from 4000 base)
    ///
    /// Internally delegates to ArmorMultipliers&lt;ProjectileType&gt; for implementation reuse.
    /// </summary>
    internal struct RangedArmorMultipliers : IEquatable<RangedArmorMultipliers>
    {
        private ArmorMultipliers<ProjectileType> _inner;
        /// <summary>
        /// Armor multiplier against arrows (from Archers, Arab Archers, etc.)
        /// </summary>
        public float Arrow
        {
            get => _inner.GetMultiplier(ProjectileType.Arrow);
            set => _inner.SetMultiplier(ProjectileType.Arrow, value);
        }

        /// <summary>
        /// Armor multiplier against crossbow bolts (from Crossbowmen)
        /// Typically higher than Arrow for heavy armor units (armor-piercing effect)
        /// </summary>
        public float Bolt
        {
            get => _inner.GetMultiplier(ProjectileType.Bolt);
            set => _inner.SetMultiplier(ProjectileType.Bolt, value);
        }

        /// <summary>
        /// Armor multiplier against slinger stones (from Arab Slingers)
        /// </summary>
        public float Slinger
        {
            get => _inner.GetMultiplier(ProjectileType.Slinger);
            set => _inner.SetMultiplier(ProjectileType.Slinger, value);
        }

        /// <summary>
        /// Armor multiplier against javelins (from Bedouin Skirmishers, etc.)
        /// </summary>
        public float Javelin
        {
            get => _inner.GetMultiplier(ProjectileType.Javelin);
            set => _inner.SetMultiplier(ProjectileType.Javelin, value);
        }

        /// <summary>
        /// Create ranged armor multipliers with specified values.
        /// </summary>
        public RangedArmorMultipliers(float arrow, float bolt, float slinger, float javelin)
        {
            _inner = new ArmorMultipliers<ProjectileType>(new Dictionary<ProjectileType, float>
            {
                { ProjectileType.Arrow, arrow },
                { ProjectileType.Bolt, bolt },
                { ProjectileType.Slinger, slinger },
                { ProjectileType.Javelin, javelin }
            });
        }

        /// <summary>
        /// Get armor multiplier for a specific projectile type.
        /// </summary>
        public float GetMultiplier(ProjectileType projectileType)
        {
            return _inner.GetMultiplier(projectileType);
        }

        /// <summary>
        /// Set armor multiplier for a specific projectile type.
        /// </summary>
        public void SetMultiplier(ProjectileType projectileType, float value)
        {
            _inner.SetMultiplier(projectileType, value);
        }

        /// <summary>
        /// Default armor multipliers (no armor, takes full damage).
        /// </summary>
        public static RangedArmorMultipliers Default => new RangedArmorMultipliers(1.0f, 1.0f, 1.0f, 1.0f);

        // Equality for TOML comparison
        public bool Equals(RangedArmorMultipliers other)
        {
            return Arrow == other.Arrow &&
                   Bolt == other.Bolt &&
                   Slinger == other.Slinger &&
                   Javelin == other.Javelin;
        }

        public override bool Equals(object obj)
        {
            return obj is RangedArmorMultipliers other && Equals(other);
        }

        public override int GetHashCode()
        {
            return _inner.GetHashCode();
        }

        public override string ToString()
        {
            return $"{{ Arrow = {Arrow:F3}, Bolt = {Bolt:F3}, Slinger = {Slinger:F3}, Javelin = {Javelin:F3} }}";
        }
    }
}
