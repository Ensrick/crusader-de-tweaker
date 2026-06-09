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
            DamageMatrixManager.LoadAll();
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

