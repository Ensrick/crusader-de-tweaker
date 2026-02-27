using System.Collections.Generic;
using SHCDESE.Interop;

namespace CrusaderDETweaker.Data
{
    internal static class UnitMeleeDamageTypeRegistry
    {
        private static readonly Dictionary<eChimps, MeleeDamageType> _types = new Dictionary<eChimps, MeleeDamageType>();

        internal static MeleeDamageType Get(eChimps unit)
        {
            return _types.TryGetValue(unit, out var type) ? type : MeleeDamageType.Type1;
        }

        internal static void Set(eChimps unit, MeleeDamageType type)
        {
            _types[unit] = type;
        }
    }
}
