// Data/ArmorMultipliers.cs
//
// PURPOSE: Generic data structure for armor multipliers (melee or ranged).
//
// USAGE:
// - ArmorMultipliers<MeleeDamageType>: Melee armor (Type1, Type2, Type3, Type4)
// - ArmorMultipliers<ProjectileType>: Ranged armor (Arrow, Bolt, Slinger, Javelin)
// - TOML format: MeleeArmorMultipliers = { Type1 = 1.0, Type2 = 0.5, Type3 = 1.5, Type4 = 0.75 }
//
// IMPORTANT FOR AI AGENTS:
// - Generic struct eliminates duplication between MeleeArmorMultipliers and RangedArmorMultipliers
// - Type parameter T must be an enum (MeleeDamageType or ProjectileType)
// - Uses Dictionary internally for flexibility (supports any enum)
// - Lower multiplier = better armor (takes less damage)
//
using System;
using System.Collections.Generic;
using System.Linq;

namespace CrusaderDETweaker.Data
{
    /// <summary>
    /// Generic armor multipliers for any damage type enum.
    ///
    /// Supports both melee and ranged armor systems:
    /// - ArmorMultipliers&lt;MeleeDamageType&gt;: Melee weapon vs armor
    /// - ArmorMultipliers&lt;ProjectileType&gt;: Ranged projectile vs armor
    ///
    /// Formula: Damage_Taken = Base_Damage × ArmorMultipliers[DamageType]
    ///
    /// Example (Knight melee armor):
    /// - Type1 attackers: 0.50x → Knight takes 50% damage
    /// - Type2 attackers: 1.00x → Knight takes full damage
    /// </summary>
    /// <typeparam name="T">Damage type enum (MeleeDamageType or ProjectileType)</typeparam>
    internal struct ArmorMultipliers<T> : IEquatable<ArmorMultipliers<T>> where T : Enum
    {
        private Dictionary<T, float> _multipliers;

        /// <summary>
        /// Create armor multipliers with specified values for each damage type.
        /// </summary>
        public ArmorMultipliers(Dictionary<T, float> multipliers)
        {
            _multipliers = new Dictionary<T, float>(multipliers);
        }

        /// <summary>
        /// Get armor multiplier for a specific damage type.
        /// </summary>
        public float GetMultiplier(T damageType)
        {
            if (_multipliers == null)
                InitializeToDefault();

            if (_multipliers.TryGetValue(damageType, out float value))
                return value;

            // Default to 1.0 (no armor) if not found
            return 1.0f;
        }

        /// <summary>
        /// Set armor multiplier for a specific damage type.
        /// </summary>
        public void SetMultiplier(T damageType, float value)
        {
            if (_multipliers == null)
                InitializeToDefault();

            _multipliers[damageType] = value;
        }

        /// <summary>
        /// Get all multipliers as a dictionary.
        /// </summary>
        public Dictionary<T, float> GetAll()
        {
            if (_multipliers == null)
                InitializeToDefault();

            return new Dictionary<T, float>(_multipliers);
        }

        /// <summary>
        /// Initialize to default values (1.0 for all types).
        /// </summary>
        private void InitializeToDefault()
        {
            _multipliers = new Dictionary<T, float>();
            foreach (T damageType in Enum.GetValues(typeof(T)))
            {
                _multipliers[damageType] = 1.0f;
            }
        }

        /// <summary>
        /// Default armor multipliers (no armor, takes full damage from all types).
        /// </summary>
        public static ArmorMultipliers<T> Default
        {
            get
            {
                var multipliers = new Dictionary<T, float>();
                foreach (T damageType in Enum.GetValues(typeof(T)))
                {
                    multipliers[damageType] = 1.0f;
                }
                return new ArmorMultipliers<T>(multipliers);
            }
        }

        // Equality for TOML comparison
        public bool Equals(ArmorMultipliers<T> other)
        {
            if (_multipliers == null && other._multipliers == null)
                return true;
            if (_multipliers == null || other._multipliers == null)
                return false;
            if (_multipliers.Count != other._multipliers.Count)
                return false;

            foreach (var kvp in _multipliers)
            {
                if (!other._multipliers.TryGetValue(kvp.Key, out float otherValue))
                    return false;
                if (Math.Abs(kvp.Value - otherValue) > 0.0001f)
                    return false;
            }
            return true;
        }

        public override bool Equals(object obj)
        {
            return obj is ArmorMultipliers<T> other && Equals(other);
        }

        public override int GetHashCode()
        {
            if (_multipliers == null)
                return 0;

            unchecked
            {
                int hash = 17;
                foreach (var kvp in _multipliers.OrderBy(x => x.Key.ToString()))
                {
                    hash = hash * 31 + kvp.Key.GetHashCode();
                    hash = hash * 31 + kvp.Value.GetHashCode();
                }
                return hash;
            }
        }

        public override string ToString()
        {
            if (_multipliers == null)
                InitializeToDefault();

            var items = _multipliers
                .OrderBy(x => x.Key.ToString())
                .Select(kvp => $"{kvp.Key} = {kvp.Value:F3}");
            return $"{{ {string.Join(", ", items)} }}";
        }
    }
}
