// Data/ProjectileType.cs
//
// PURPOSE: Enum for ranged projectile types used in ranged damage matrix.
//
// USAGE:
// - Used by RangedDamageMatrixGenerator to generate CSV headers
// - Used by RangedDamageMatrixLoader to parse CSV headers
// - Used by ProjectileApiHelper to map projectile types to API calls
//
// IMPORTANT FOR AI AGENTS:
// - This enum represents the 4 projectile types in the game (Arrow, Bolt, Slinger, Javelin)
// - Used exclusively for ranged damage matrix configuration
// - NOT related to unit types - these are projectile types fired BY units
//
namespace CrusaderDETweaker.Data
{
    /// <summary>
    /// Represents the different types of ranged projectiles in the game.
    /// 
    /// Used by the ranged damage matrix system to configure damage values.
    /// Each projectile type corresponds to a different ranged weapon:
    /// - Arrow: Standard bows (Archers, Arab Archers, etc.)
    /// - Bolt: Crossbows (Crossbowmen)
    /// - Slinger: Sling stones (Arab Slingers)
    /// - Javelin: Javelins (Bedouin Skirmishers, Camel Lancers)
    /// </summary>
    internal enum ProjectileType
    {
        /// <summary>
        /// Standard bow arrows (from Archers, Arab Archers, etc.)
        /// </summary>
        Arrow,

        /// <summary>
        /// Crossbow bolts (from Crossbowmen)
        /// </summary>
        Bolt,

        /// <summary>
        /// Slinger stones (from Arab Slingers)
        /// </summary>
        Slinger,

        /// <summary>
        /// Javelins
        /// </summary>
        Javelin
    }
}