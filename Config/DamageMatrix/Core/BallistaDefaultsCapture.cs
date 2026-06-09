using System;
using System.Collections.Generic;
using CrusaderDETweaker.Config.DamageMatrix.Ballista;

namespace CrusaderDETweaker.Config.DamageMatrix.Core
{
    internal static class BallistaDefaultsCapture
    {
        private static bool _isCaptured = false;
        private static readonly Dictionary<BallistaDamageTarget, int> _originalDefaults = new Dictionary<BallistaDamageTarget, int>();

        public static void Capture()
        {
            if (_isCaptured) return;

            var targets = (BallistaDamageTarget[])Enum.GetValues(typeof(BallistaDamageTarget));
            var mappings = BallistaDamageHelper.GetMappings();

            foreach (var target in targets)
            {
                if (mappings.TryGetValue(target, out var prop) && prop != null)
                {
                    try
                    {
                        int val = prop.GetValue();
                        _originalDefaults[target] = val;
                    }
                    catch (Exception ex)
                    {
                        Plugin.Logger.LogDebug($"Could not capture original Ballista damage for {target}: {ex.Message}");
                    }
                }
            }

            _isCaptured = true;
        }

        public static int Get(BallistaDamageTarget target)
        {
            if (_originalDefaults.TryGetValue(target, out int damage))
                return damage;
            
            return -1;
        }
    }
}
