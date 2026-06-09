using System.Collections.Generic;
using SHCDESE.Interop;
using Zhuqiaomon.Assembly.Stateful;

namespace CrusaderDETweaker.Config.DamageMatrix.Ballista
{
    public static class BallistaDamageHelper
    {
        public static Dictionary<BallistaDamageTarget, ManagedAssemblyImmediate<ushort>> GetMappings()
        {
            var p = Plugin.GlobalsApi;
            if (p == null) return new Dictionary<BallistaDamageTarget, ManagedAssemblyImmediate<ushort>>();

            return new Dictionary<BallistaDamageTarget, ManagedAssemblyImmediate<ushort>>
            {
                { BallistaDamageTarget.Default, p.BallistaDamageDefault },
                { BallistaDamageTarget.ArabBallista, p.BallistaDamageToArabBallista },
                { BallistaDamageTarget.Ballista, p.BallistaDamageToBallista },
                { BallistaDamageTarget.BatteringRamAndSiegeTower, p.BallistaDamageToBatteringRamAndSiegeTower },
                { BallistaDamageTarget.Catapult, p.BallistaDamageToCatapult },
                { BallistaDamageTarget.Mangonel, p.BallistaDamageToMangonel },
                { BallistaDamageTarget.PortableShields, p.BallistaDamageToPortableShields },
                { BallistaDamageTarget.Trebutchet, p.BallistaDamageToTrebutchet }
            };
        }
    }
}
