// Config/Toml/Units/Properties/EngageDistancePatch.cs
//
// PURPOSE: The distance at which an idle ranged unit notices an enemy (EngageRange, and AttackRange when
//          EngageRange is -1), applied by rewriting the game's own constants (2.9.1, GitLab #7 / GitHub #4).
//
// WHY NOT THE SCRIPT EXTENDER'S SetEngageRange (SE 2.10.0 - 2.14.0, SE issue #195):
//   - SE hooks only the compares its walker reaches from the start of each unit update. The idle state's
//     compares (Archer RVA 0x14135C: "wake if the nearest enemy is closer than 400") sit behind the state jump
//     table, so idle units kept waking at 400 world units (50 tiles): measured 48 tiles in every phase.
//   - The hook stub re-executes the native compare with r13-r15 overwritten, so an override crashes the
//     Crossbowman and Bedouin Heavy Camel (r13) and makes the Arabian Ballista compare wild memory (r14).
//
// WHAT THIS DOES:
//   Field +0x8FE of a unit is the distance to its nearest enemy (world units, 8 per tile; filled by RVA 0x18C160).
//   Each ranged unit's update compares it ("cmp word [unit+8FEh], reg16") against a constant loaded a few
//   instructions earlier ("mov reg32, imm32"). Game 2.8.2 constants: Archer, Crossbowman, Arabian Bow, Slinger,
//   Grenadier, Skirmisher 400 (state 0 and idle) plus 432 (or 160 for the Grenadier) before target acquisition;
//   Horse Archer and Bedouin Heavy Camel 432; Ballista 680; Arabian Ballista 680 and 432.
//   The update function is decoded from CrusaderDE.dll ON DISK (SE's hooks have replaced some compares in memory),
//   every feeding mov is checked to be unchanged in memory, and its immediate is rewritten through RedBird's
//   ManagedAssemblyImmediate (the primitive SE itself uses for code constants; restore = ClearOverrides).
//   The constant before the first compare (state 0) is the unit's engage distance D. A target distance E scales
//   every constant c of that unit to round(c * E / D), keeping the game's gaps between states.
//   E = EngageRange x 8 when set; otherwise D x AttackRange / game AttackRange when AttackRange is set (so a
//   longer AttackRange also wakes idle units farther away); otherwise the game constants.
//
// IMPORTANT FOR AI AGENTS:
// - Each unit type has its own update function (SE log "Hooking unit update function", 13 distinct addresses),
//   so a patch never leaks into another type. Scan() refuses a function shared by two types.
// - Never call SE's SetEngageRange for these units again: it is the crash above.
// - Units with no such compare (Catapult, Trebuchet, Mangonel) get no EngageRange.
//
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using Iced.Intel;
using RedBird.X64.Assembly.Stateful;
using SHCDESE.GameGlobals;
using SHCDESE.Interop;

namespace CrusaderDETweaker.Config.Toml.Units.Properties
{
    internal static class EngageDistancePatch
    {
        /// <summary>Unit field: distance to the nearest enemy in world units (r_WorldDistanceToNearestEnemy).</summary>
        private const uint NearestEnemyOffset = 0x8FE;

        /// <summary>The distance field is capped at 32000 by the game (0x18C160); a larger constant means "always".</summary>
        internal const int MaxWorld = 32000;

        private sealed class Site
        {
            internal ulong MovRva, CompareRva;
            internal int Native;
            internal ManagedAssemblyImmediate<int> Immediate;
        }

        private sealed class UnitSites
        {
            internal ulong FunctionRva;
            internal int Base;
            internal readonly List<Site> Sites = new List<Site>();
            internal int EngageTiles;                 // 0 = no EngageRange
            internal int AttackTiles, GameAttackTiles; // 0 = no AttackRange override
            internal int Applied;                     // world units now in effect for the base constant
        }

        /// <summary>
        /// Unit types whose update is scanned: the ranged types (AttackRange) plus the Bedouin Heavy Camel, which has
        /// engage compares but no attack-range table. Types without compares (Catapult, ...) simply find none.
        /// </summary>
        internal static IEnumerable<eChimps> Candidates =>
            UnitRanges.AttackRangeUnits.Concat(new[] { eChimps.CHIMP_TYPE_BEDOUIN_HEAVY_CAMEL });

        /// <summary>The unit types that have engage constants (for logs and the self-test).</summary>
        internal static IEnumerable<eChimps> SupportedUnits
        {
            get { EnsureScanned(); return _units.Keys.OrderBy(u => (int)u).ToList(); }
        }

        private static readonly object Owner = new object();
        private static readonly object Gate = new object();
        private static Dictionary<eChimps, UnitSites> _units;

        /// <summary>True when the unit's update has engage constants this mod can rewrite.</summary>
        internal static bool Supports(eChimps unit)
        {
            EnsureScanned();
            return _units.ContainsKey(unit);
        }

        /// <summary>The game's engage distance in tiles (the state-0 constant / 8).</summary>
        internal static bool TryGetGameTiles(eChimps unit, out int tiles)
        {
            EnsureScanned();
            UnitSites u;
            tiles = _units.TryGetValue(unit, out u) ? u.Base / UnitRanges.WorldUnitsPerTile : 0;
            return tiles > 0;
        }

        /// <summary>The engage distance now in effect, in world units (the game's when nothing is set).</summary>
        internal static bool TryGetCurrentWorld(eChimps unit, out int world)
        {
            EnsureScanned();
            UnitSites u;
            world = _units.TryGetValue(unit, out u) ? u.Applied : 0;
            return world > 0;
        }

        /// <summary>The rewritten constants of a unit as "RVA 0x...: game -> now" (for logs and the self-test).</summary>
        internal static string Describe(eChimps unit)
        {
            EnsureScanned();
            UnitSites u;
            if (!_units.TryGetValue(unit, out u)) return "no engage constants";
            return string.Join(", ", u.Sites.Select(s => $"0x{s.MovRva:X} {s.Native} -> {s.Immediate.GetValue()}"));
        }

        internal static void SetEngageTiles(eChimps unit, int tiles) => Update(unit, u => u.EngageTiles = tiles, "EngageRange");

        internal static void ClearEngage(eChimps unit) => Update(unit, u => u.EngageTiles = 0, "EngageRange");

        internal static void SetAttackTiles(eChimps unit, int tiles, int gameTiles) =>
            Update(unit, u => { u.AttackTiles = tiles; u.GameAttackTiles = gameTiles; }, "AttackRange");

        internal static void ClearAttack(eChimps unit) => Update(unit, u => u.AttackTiles = 0, "AttackRange");

        private static void Update(eChimps unit, Action<UnitSites> change, string property)
        {
            EnsureScanned();
            UnitSites u;
            if (!_units.TryGetValue(unit, out u)) return;
            lock (Gate)
            {
                int before = u.Applied;
                change(u);
                int target = u.Base;
                string source = "game value";
                if (u.EngageTiles > 0)
                {
                    target = u.EngageTiles * UnitRanges.WorldUnitsPerTile;
                    source = $"EngageRange {u.EngageTiles}";
                }
                else if (u.AttackTiles > 0 && u.GameAttackTiles > 0 && u.AttackTiles != u.GameAttackTiles)
                {
                    target = (int)Math.Round(u.Base * (double)u.AttackTiles / u.GameAttackTiles);
                    source = $"AttackRange {u.AttackTiles} (game {u.GameAttackTiles}), EngageRange -1";
                }
                target = Math.Max(1, Math.Min(MaxWorld, target));

                foreach (Site s in u.Sites)
                {
                    if (target == u.Base) s.Immediate.ClearOverrides(Owner);
                    else s.Immediate.SetOverride(Owner, Math.Max(1, Math.Min(MaxWorld, (int)Math.Round(s.Native * (double)target / u.Base))), 0, property);
                }
                u.Applied = target;
                if (target != before)
                    Plugin.Logger.LogInfo($"[{property}] {unit}: idle units notice enemies within {target / (double)UnitRanges.WorldUnitsPerTile:0.#} tiles " +
                                          $"(game {u.Base / (double)UnitRanges.WorldUnitsPerTile:0.#}; from {source}); game code constants {Describe(unit)}.");
            }
        }

        // ------------------------------------------------------------------
        // Scan
        // ------------------------------------------------------------------

        private static void EnsureScanned()
        {
            if (_units != null) return;
            lock (Gate)
            {
                if (_units != null) return;
                var found = new Dictionary<eChimps, UnitSites>();
                try { Scan(found); }
                catch (Exception ex) { Plugin.Logger.LogError($"[EngageRange] Could not read the game's engage constants, so EngageRange is unavailable: {ex.GetType().Name}: {ex.Message}"); found.Clear(); }
                _units = found;
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RuntimeFunction { internal uint Begin, End, Unwind; }

        [DllImport("kernel32.dll")]
        private static extern IntPtr RtlLookupFunctionEntry(ulong controlPc, out ulong imageBase, IntPtr historyTable);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        private static extern uint GetModuleFileNameW(IntPtr module, StringBuilder fileName, int size);

        private static unsafe void Scan(Dictionary<eChimps, UnitSites> found)
        {
            ulong* vtable = (ulong*)GameGlobalsManager.Instance.GameUnitFunctionsVTable.Pointer;
            if (vtable == null) throw new InvalidOperationException("the Script Extender has no unit function table");

            byte[] file = null;
            ulong imageBase = 0;
            var owners = new Dictionary<ulong, eChimps>();
            foreach (eChimps unit in Candidates)
            {
                ulong fn = vtable[(int)unit];
                ulong moduleBase;
                IntPtr entry = RtlLookupFunctionEntry(fn, out moduleBase, IntPtr.Zero);
                if (entry == IntPtr.Zero) { Plugin.Logger.LogWarning($"[EngageRange] {unit}: update function 0x{fn:X} has no unwind entry; skipped."); continue; }
                var rf = (RuntimeFunction)Marshal.PtrToStructure(entry, typeof(RuntimeFunction));
                eChimps other;
                if (owners.TryGetValue(rf.Begin, out other))
                {
                    Plugin.Logger.LogWarning($"[EngageRange] {unit} shares its update function with {other}; EngageRange is disabled for both.");
                    found.Remove(other);
                    continue;
                }
                owners[rf.Begin] = unit;

                if (file == null)
                {
                    imageBase = moduleBase;
                    var path = new StringBuilder(1024);
                    if (GetModuleFileNameW((IntPtr)(long)moduleBase, path, path.Capacity) == 0) throw new IOException("game module path not found");
                    file = File.ReadAllBytes(path.ToString());
                }
                else if (moduleBase != imageBase) continue;

                var u = new UnitSites { FunctionRva = rf.Begin };
                FindSites(file, imageBase, rf.Begin, rf.End, u);
                if (u.Sites.Count == 0) continue;
                u.Applied = u.Base;
                found[unit] = u;
                Plugin.Logger.LogDebug($"[EngageRange] {unit}: update 0x{rf.Begin:X}, engage {u.Base} world units, constants {string.Join(", ", u.Sites.Select(s => $"0x{s.MovRva:X}={s.Native}"))}");
            }
        }

        /// <summary>
        /// Decodes [begin, end) from the DLL file and records every "mov reg32, imm32" that feeds a
        /// "cmp word [..+8FEh], reg16". The base constant is the one before the first compare.
        /// </summary>
        private static unsafe void FindSites(byte[] file, ulong imageBase, uint begin, uint end, UnitSites u)
        {
            int offset = FileOffset(file, begin);
            var decoder = Iced.Intel.Decoder.Create(64, new ByteArrayCodeReader(file, offset, (int)(end - begin)), imageBase + begin);
            var list = new List<Instruction>();
            while (decoder.IP < imageBase + end)
            {
                decoder.Decode(out Instruction ins);
                list.Add(ins);
            }

            var info = new InstructionInfoFactory();
            ulong firstCompare = ulong.MaxValue;
            var byMov = new Dictionary<ulong, Site>();
            for (int i = 0; i < list.Count; i++)
            {
                Instruction cmp = list[i];
                if (cmp.Mnemonic != Mnemonic.Cmp || cmp.Op0Kind != OpKind.Memory || cmp.MemoryDisplacement32 != NearestEnemyOffset ||
                    cmp.Op1Kind != OpKind.Register || cmp.MemorySize.GetSize() != 2) continue;
                Register full = cmp.Op1Register.GetFullRegister();
                for (int j = i - 1; j >= 0 && j >= i - 14; j--)
                {
                    Instruction p = list[j];
                    if (p.FlowControl != FlowControl.Next && p.FlowControl != FlowControl.ConditionalBranch) break;
                    if (p.Mnemonic == Mnemonic.Mov && p.Op0Kind == OpKind.Register && p.Op0Register.GetFullRegister() == full &&
                        p.Op0Register.GetSize() == 4 && p.Op1Kind == OpKind.Immediate32)
                    {
                        int value = (int)p.Immediate32;
                        if (value < 0x40 || value > 0x1000) break;   // not a distance
                        ulong movRva = p.IP - imageBase;
                        if (!byMov.ContainsKey(movRva))
                        {
                            // The bytes in memory must still be the file's (nobody hooked this mov).
                            byte* live = (byte*)(imageBase + movRva);
                            int fo = FileOffset(file, (uint)movRva);
                            bool same = true;
                            for (int k = 0; k < p.Length; k++) same &= live[k] == file[fo + k];
                            if (!same) { Plugin.Logger.LogWarning($"[EngageRange] game code at 0x{movRva:X} was changed by another mod; that constant is left alone."); break; }
                            byMov[movRva] = new Site
                            {
                                MovRva = movRva, CompareRva = cmp.IP - imageBase, Native = value,
                                Immediate = new ManagedAssemblyImmediate<int>((IntPtr)(long)(imageBase + movRva), 1, 0L, null, $"engage distance 0x{movRva:X}")
                            };
                        }
                        if (cmp.IP < firstCompare) { firstCompare = cmp.IP; u.Base = value; }
                        break;
                    }
                    bool writes = info.GetInfo(p).GetUsedRegisters().Any(r => r.Register.GetFullRegister() == full &&
                        r.Access != OpAccess.Read && r.Access != OpAccess.CondRead && r.Access != OpAccess.None);
                    if (writes) break;
                }
            }
            u.Sites.AddRange(byMov.Values.OrderBy(s => s.MovRva));
        }

        private static int FileOffset(byte[] file, uint rva)
        {
            int pe = BitConverter.ToInt32(file, 0x3C);
            int sections = BitConverter.ToUInt16(file, pe + 6);
            int optional = BitConverter.ToUInt16(file, pe + 20);
            for (int i = 0; i < sections; i++)
            {
                int o = pe + 24 + optional + i * 40;
                uint vsize = BitConverter.ToUInt32(file, o + 8), va = BitConverter.ToUInt32(file, o + 12);
                uint rawSize = BitConverter.ToUInt32(file, o + 16), raw = BitConverter.ToUInt32(file, o + 20);
                if (rva >= va && rva < va + Math.Max(vsize, rawSize)) return (int)(rva - va + raw);
            }
            throw new InvalidDataException($"RVA 0x{rva:X} is in no section of the game DLL");
        }
    }
}
