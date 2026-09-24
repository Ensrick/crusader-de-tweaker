// Config/DamageMatrix/DamageMatrixConfigSystem.cs
using CrusaderDETweaker.Config.Core;

namespace CrusaderDETweaker.Config.DamageMatrix
{
    /// <summary>
    /// Wrapper that implements IConfigSystem for damage matrix configurations.
    /// </summary>
    internal class DamageMatrixConfigSystem : IConfigSystem
    {
        public string Name => "Damage Matrix";

        public bool IsLoaded { get; private set; }

        public void GenerateDefaults()
        {
            DamageMatrixManager.GenerateDefaults();
        }

        public void Load()
        {
            // Applied by ConfigLoader.ReapplyTemplateConfigs("launch") once the multipliers are bound (Plugin.cs):
            // the one template write path (v2.7.0). Loading here only marks the system ready.
            IsLoaded = true;
        }

        /// <summary>
        /// Validate the damage matrix system configuration.
        /// </summary>
        public bool Validate()
        {
            // Validation removed - damage matrix system is now reference-only
            return true;
        }
    }
}

