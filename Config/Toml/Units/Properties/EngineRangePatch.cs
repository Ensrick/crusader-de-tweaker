// Config/Toml/Units/Properties/EngineRangePatch.cs
//
// PURPOSE: AttackRange for the siege engines (Catapult, Trebuchet, Mangonel, Ballista, Arabian Ballista), GitHub #5.
//          The Script Extender's AttackRange alone does not move the distance an engine fires from.
//
// GAME FACTS (CrusaderDE.dll, game 2.8.2; traced 2026-10-11, every RVA below read from the DLL on disk):
//   - An engine's update (unit function table, SE GameUnitFunctionsVTable) runs a small state machine (unit +0x918):
//     state 0 picks a target through RVA 0x18E9A0 (the target acquisition SE hooks for AttackRange; it range-checks
//     UNIT targets only, a building or ground target passes unchecked), state 8 checks the distance and turns the
//     engine, state 2 fires. The archers' fire check (RVA 0x19B630, also hooked by SE) is only called by the
//     Arabian Ballista among the engines.
//   - The state-8 check is "dx^2 + dy^2 <= (R + half the building size)^2" in tiles (0 for a unit or ground target),
//     where R is the 8-bit displacement of "lea eax, [reg + R]; imul eax, eax" in the engine's own code:
//     Catapult 75 (RVA 0x152C6E), Trebuchet 85 (0x153EA7), Mangonel 70 (0x155150), Ballista 85 (0x1625AE),
//     Arabian Ballista 54 (0x1729EB). Out of range: the engine waits 40 ticks and drops a building target.
//   - So with the Script Extender's AttackRange only, an engine never fires beyond R (a raised value only makes it
//     pick targets it then gives up on) and a lowered value does nothing against buildings.
//   - The Mangonel looks for enemies only while the nearest one is within 680 world units (85 tiles), for both
//     human and AI owners ("mov eax, 680" at RVA 0x154908, compared with unit +0x8FE as a register at 0x154913 /
//     0x154A0A; EngageDistancePatch does not see this form). An AttackRange above 85 moves it in proportion.
//
// WHAT THIS DOES: rewrites R (and the Mangonel's 680) to follow AttackRange through RedBird's
//   ManagedAssemblyDisplacement / ManagedAssemblyImmediate (the primitives EngageDistancePatch uses; restore =
//   ClearOverrides). Each site is found by decoding the engine's update (all of its .pdata chunks) from CrusaderDE.dll
//   ON DISK: the one "lea r32, [r64 + disp8]" followed by "imul r32, r32" and a compare, whose displacement equals the
//   engine's game AttackRange (its projectile range table entry); the bytes in memory must still be the file's.
//   R is an 8-bit displacement: AttackRange above 127 tiles is clamped to 127 for engines (warning).
//
// MEASURED (Tests/HeadlessSelfTest.Engines.cs, -Only engines, 2026-10-11): first shot Catapult 75 -> 90 / 70, Trebuchet
//   85 -> 100, Mangonel 70 -> 85, Ballista 85 -> 100, Arabian Ballista 54 -> 77 tiles; with only the Script Extender's
//   AttackRange (2.10.0) every engine still fired from its game range.
//
// NOT CHANGED: AI-owned Catapults and Trebuchets choose targets in RVA 0x18FC00 (search radius 73 / 83 tiles) and go
//   straight to state 2 without the state-8 check [unverified in game: an AI-owned Catapult never fired in the editor map].
//
// IMPORTANT FOR AI AGENTS:
// - Applied only through UnitRanges.ApplyAttackRange / ResetAttackRange (TemplateBaseline restore = Reset).
// - Each engine type has its own update function, so a patch never leaks into another type.
// - Tests/HeadlessSelfTest.Engines.cs measures the first-shot distance with and without this patch.
//
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using CrusaderDETweaker.Config.Core;
using Iced.Intel;
using RedBird.X64.Assembly.Stateful;
using SHCDESE.GameGlobals;
using SHCDESE.Interop;

namespace CrusaderDETweaker.Config.Toml.Units.Properties
{
    internal static class EngineRangePatch
    {
        /// <summary>The range is an 8-bit displacement in the game's code.</summary>
        internal const int MaxTiles = sbyte.MaxValue;

        internal static readonly eChimps[] Engines =
        {
            eChimps.CHIMP_TYPE_CATAPULT, eChimps.CHIMP_TYPE_TREBUCHET, eChimps.CHIMP_TYPE_MANGONEL,
            eChimps.CHIMP_TYPE_BALLISTA, eChimps.CHIMP_TYPE_ARAB_BALLISTA
        };

        private const uint NearestEnemyOffset = 0x8FE;
        private const int MangonelNotice = 680;   // world units (85 tiles)

        private sealed class Site
        {
            internal ulong Rva, NoticeRva;
            internal int Native, Applied;
            internal int Engage, EngageWarned, NoticeLogged = MangonelNotice;   // EngageRange in tiles, 0 = -1 (Mangonel only)
            internal ManagedAssemblyDisplacement<int> Range;
            internal ManagedAssemblyImmediate<int> Notice;   // Mangonel only
        }

        private static readonly object Owner = new object();
        private static readonly object Gate = new object();
        private static Dictionary<eChimps, Site> _sites;

        internal static bool Supports(eChimps unit)
        {
            EnsureScanned();
            return _sites.ContainsKey(unit);
        }

        /// <summary>The engine's range in the game's code (tiles), or false when the site was not found.</summary>
        internal static bool TryGetGameTiles(eChimps unit, out int tiles)
        {
            EnsureScanned();
            tiles = _sites.TryGetValue(unit, out Site s) ? s.Native : 0;
            return tiles > 0;
        }

        /// <summary>The range in effect in the game's code now (tiles), or 0 when the site was not found.</summary>
        internal static int CurrentTiles(eChimps unit)
        {
            EnsureScanned();
            return _sites.TryGetValue(unit, out Site s) ? s.Range.GetValue() : 0;
        }

        /// <summary>"RVA 0x...: game -> now" for logs and the self-test.</summary>
        internal static string Describe(eChimps unit)
        {
            EnsureScanned();
            if (!_sites.TryGetValue(unit, out Site s)) return "no engine range site";
            string text = $"0x{s.Rva:X} {s.Native} -> {s.Range.GetValue()} tiles";
            if (s.Notice != null) text += $", notice 0x{s.NoticeRva:X} {MangonelNotice} -> {s.Notice.GetValue()} world units";
            return text;
        }

        /// <summary>Makes the engine fire from <paramref name="tiles"/> (clamped to 1..127).</summary>
        internal static void SetAttackTiles(eChimps unit, int tiles)
        {
            EnsureScanned();
            if (!_sites.TryGetValue(unit, out Site s)) return;
            lock (Gate)
            {
                int value = Math.Max(1, tiles);
                if (value > MaxTiles)
                {
                    Plugin.Logger.LogWarning($"[AttackRange] {unit}: {tiles} tiles is above the {MaxTiles} a siege engine's code can hold; using {MaxTiles}.");
                    value = MaxTiles;
                }
                if (value == s.Native) s.Range.ClearOverrides(Owner);
                else s.Range.SetOverride(Owner, value, 0, "AttackRange");
                int before = s.Applied;
                s.Applied = value;
                ApplyNotice(s);
                if (value != before)
                    Plugin.Logger.LogInfo($"[AttackRange] {unit}: the engine fires from up to {value} tiles (game {s.Native}); game code {Describe(unit)}.");
            }
        }

        /// <summary>True for the engines whose own look-out EngageRange sets (the Mangonel; the Ballistas go through EngageDistancePatch).</summary>
        internal static bool SupportsEngage(eChimps unit)
        {
            EnsureScanned();
            return _sites.TryGetValue(unit, out Site s) && s.Notice != null;
        }

        /// <summary>The look-out in the game's code in tiles (the Mangonel's 85), or false.</summary>
        internal static bool TryGetNoticeTiles(eChimps unit, bool current, out int tiles)
        {
            EnsureScanned();
            tiles = _sites.TryGetValue(unit, out Site s) && s.Notice != null
                ? (current ? s.Notice.GetValue() : MangonelNotice) / UnitRanges.WorldUnitsPerTile : 0;
            return tiles > 0;
        }

        /// <summary>
        /// EngageRange of an engine with its own look-out (the Mangonel): how far it aims on its own, at most its
        /// AttackRange (it cannot fire farther). 0 = -1: the look-out follows AttackRange as before.
        /// </summary>
        internal static void SetEngageTiles(eChimps unit, int tiles)
        {
            EnsureScanned();
            if (!_sites.TryGetValue(unit, out Site s) || s.Notice == null) return;
            lock (Gate)
            {
                s.Engage = Math.Max(0, tiles);
                ApplyNotice(s, warn: true);
            }
        }

        internal static void ClearEngage(eChimps unit) => SetEngageTiles(unit, 0);

        /// <summary>
        /// The Mangonel's look-out: EngageRange (at most AttackRange) when set; otherwise the game's 680 world units, grown
        /// in proportion when AttackRange is above the game's 70 (an engine aims as far as it fires).
        /// </summary>
        private static void ApplyNotice(Site s, bool warn = false)
        {
            if (s.Notice == null) return;
            int notice;
            string source;
            if (s.Engage > 0)
            {
                int tiles = Math.Min(s.Engage, s.Applied);
                if (warn && tiles < s.Engage && s.Engage != s.EngageWarned)
                    Plugin.Logger.LogWarning($"[EngageRange] CHIMP_TYPE_MANGONEL: EngageRange {s.Engage} is above its AttackRange {s.Applied}; it aims on its own from up to {tiles} tiles (it cannot fire farther).");
                if (warn) s.EngageWarned = s.Engage;
                notice = tiles * UnitRanges.WorldUnitsPerTile;
                source = $"EngageRange {s.Engage}";
            }
            else
            {
                notice = Math.Max(MangonelNotice, (int)Math.Round(MangonelNotice * (double)s.Applied / s.Native));
                source = s.Applied > s.Native ? $"AttackRange {s.Applied}, EngageRange -1" : "game value";
            }
            notice = Math.Min(EngageDistancePatch.MaxWorld, notice);
            if (notice == MangonelNotice) s.Notice.ClearOverrides(Owner);
            else s.Notice.SetOverride(Owner, notice, 0, s.Engage > 0 ? "EngageRange" : "AttackRange");
            if (notice != s.NoticeLogged)
                Plugin.Logger.LogInfo($"[EngageRange] CHIMP_TYPE_MANGONEL: aims on its own at enemies within {notice / (double)UnitRanges.WorldUnitsPerTile:0.#} tiles " +
                                      $"(game {MangonelNotice / UnitRanges.WorldUnitsPerTile}; from {source}); game code 0x{s.NoticeRva:X} {MangonelNotice} -> {notice} world units.");
            s.NoticeLogged = notice;
        }

        /// <summary>Puts the game's code back for this engine.</summary>
        internal static void ClearAttack(eChimps unit)
        {
            EnsureScanned();
            if (!_sites.TryGetValue(unit, out Site s)) return;
            lock (Gate)
            {
                s.Range.ClearOverrides(Owner);
                if (s.Applied != s.Native) Plugin.Logger.LogInfo($"[AttackRange] {unit}: engine range back to the game's {s.Native} tiles.");
                s.Applied = s.Native;
                ApplyNotice(s);   // the Mangonel's look-out: EngageRange if set, else the game's
            }
        }

        // ------------------------------------------------------------------
        // Scan
        // ------------------------------------------------------------------

        private static void EnsureScanned()
        {
            if (_sites != null) return;
            lock (Gate)
            {
                if (_sites != null) return;
                var found = new Dictionary<eChimps, Site>();
                try { Scan(found); }
                catch (Exception ex) { Plugin.Logger.LogError($"[AttackRange] Could not read the siege engines' range code, so AttackRange for engines only changes target selection: {ex.GetType().Name}: {ex.Message}"); found.Clear(); }
                _sites = found;
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RuntimeFunction { internal uint Begin, End, Unwind; }

        [DllImport("kernel32.dll")]
        private static extern IntPtr RtlLookupFunctionEntry(ulong controlPc, out ulong imageBase, IntPtr historyTable);

        private static unsafe void Scan(Dictionary<eChimps, Site> found)
        {
            ulong* vtable = (ulong*)GameGlobalsManager.Instance.GameUnitFunctionsVTable.Pointer;
            if (vtable == null) throw new InvalidOperationException("the Script Extender has no unit function table");
            byte[] file = GameCode.ReadModuleFile();
            ulong module = (ulong)(long)GameCode.ModuleBase;
            var functions = ReadFunctionTable(file);

            foreach (eChimps unit in Engines)
            {
                ulong fn = vtable[(int)unit];
                IntPtr entry = RtlLookupFunctionEntry(fn, out ulong moduleBase, IntPtr.Zero);
                if (entry == IntPtr.Zero || moduleBase != module) { Plugin.Logger.LogWarning($"[AttackRange] {unit}: update function 0x{fn:X} is not in the game module; engine range unchanged."); continue; }
                var primary = (RuntimeFunction)Marshal.PtrToStructure(entry, typeof(RuntimeFunction));
                int native = UnitRanges.GameAttackTiles(unit);

                var instructions = new List<Instruction>();
                foreach (var chunk in functions.Where(f => PrimaryOf(file, f) == primary.Begin))
                    instructions.AddRange(Decode(file, module, chunk.Begin, chunk.End));

                var ranges = new List<Instruction>();
                for (int i = 0; i + 3 < instructions.Count; i++)
                {
                    Instruction lea = instructions[i], imul = instructions[i + 1];
                    if (lea.Mnemonic != Mnemonic.Lea || lea.Op0Register.GetSize() != 4 || lea.MemoryIndex != Register.None ||
                        lea.MemoryDisplSize != 1 || (int)lea.MemoryDisplacement64 != native) continue;
                    if (imul.Mnemonic != Mnemonic.Imul || imul.OpCount != 2 || imul.Op0Register != lea.Op0Register || imul.Op1Register != lea.Op0Register) continue;
                    if (!instructions.Skip(i + 2).Take(2).Any(c => c.Mnemonic == Mnemonic.Cmp && c.Op1Kind == OpKind.Register && c.Op1Register == lea.Op0Register)) continue;
                    ranges.Add(lea);
                }
                if (ranges.Count != 1)
                {
                    Plugin.Logger.LogWarning($"[AttackRange] {unit}: found {ranges.Count} range checks for {native} tiles in its update (expected 1, a game update?); engine range unchanged.");
                    continue;
                }
                ulong rva = ranges[0].IP - module;
                if (!SameInMemory(file, module, rva, ranges[0].Length)) { Plugin.Logger.LogWarning($"[AttackRange] {unit}: game code at 0x{rva:X} was changed by another mod; engine range unchanged."); continue; }

                var site = new Site
                {
                    Rva = rva, Native = native, Applied = native,
                    Range = new ManagedAssemblyDisplacement<int>((IntPtr)(long)(module + rva), 1, 0L, null, $"{unit} attack range 0x{rva:X}")
                };
                if (unit == eChimps.CHIMP_TYPE_MANGONEL) FindMangonelNotice(file, module, instructions, site);
                found[unit] = site;
                Plugin.Logger.LogDebug($"[AttackRange] {unit}: update 0x{primary.Begin:X}, {Describe(unit, site)}");
            }
        }

        private static string Describe(eChimps unit, Site s) => $"range 0x{s.Rva:X} = {s.Native} tiles" + (s.Notice != null ? $", notice 0x{s.NoticeRva:X}" : "");

        /// <summary>
        /// "movzx r32, word [unit + 8FEh]" ... "mov eax, 680" ... "cmp r16, ax": the Mangonel's 85-tile look-out, shared
        /// by the human and AI branches (one mov, two compares).
        /// </summary>
        private static void FindMangonelNotice(byte[] file, ulong module, List<Instruction> list, Site site)
        {
            var movs = new HashSet<ulong>();
            for (int i = 0; i < list.Count; i++)
            {
                Instruction cmp = list[i];
                if (cmp.Mnemonic != Mnemonic.Cmp || cmp.Op0Kind != OpKind.Register || cmp.Op1Kind != OpKind.Register || cmp.Op0Register.GetSize() != 2) continue;
                Register distance = cmp.Op0Register.GetFullRegister(), constant = cmp.Op1Register.GetFullRegister();
                bool loaded = false;
                ulong mov = 0;
                for (int j = i - 1; j >= 0 && j >= i - 12; j--)
                {
                    Instruction p = list[j];
                    if (p.Mnemonic == Mnemonic.Movzx && p.Op0Register.GetFullRegister() == distance && p.Op1Kind == OpKind.Memory && p.MemoryDisplacement32 == NearestEnemyOffset) loaded = true;
                    if (mov == 0 && p.Mnemonic == Mnemonic.Mov && p.Op0Kind == OpKind.Register && p.Op0Register.GetFullRegister() == constant &&
                        p.Op1Kind == OpKind.Immediate32 && (int)p.Immediate32 == MangonelNotice) mov = p.IP;
                }
                if (loaded && mov != 0) movs.Add(mov);
            }
            if (movs.Count != 1) { Plugin.Logger.LogWarning($"[AttackRange] CHIMP_TYPE_MANGONEL: its {MangonelNotice} look-out was found {movs.Count} times (expected 1); an AttackRange above 85 tiles only works for enemies within 85."); return; }
            ulong rva = movs.First() - module;
            if (!SameInMemory(file, module, rva, 5)) { Plugin.Logger.LogWarning($"[AttackRange] CHIMP_TYPE_MANGONEL: game code at 0x{rva:X} was changed by another mod; its look-out stays at 85 tiles."); return; }
            site.NoticeRva = rva;
            site.Notice = new ManagedAssemblyImmediate<int>((IntPtr)(long)(module + rva), 1, 0L, null, $"Mangonel look-out 0x{rva:X}");
        }

        private static unsafe bool SameInMemory(byte[] file, ulong module, ulong rva, int length)
        {
            byte* live = (byte*)(module + rva);
            int fo = FileOffset(file, (uint)rva);
            for (int k = 0; k < length; k++) if (live[k] != file[fo + k]) return false;
            return true;
        }

        private static List<Instruction> Decode(byte[] file, ulong module, uint begin, uint end)
        {
            var decoder = Iced.Intel.Decoder.Create(64, new ByteArrayCodeReader(file, FileOffset(file, begin), (int)(end - begin)), module + begin);
            var list = new List<Instruction>();
            while (decoder.IP < module + end)
            {
                decoder.Decode(out Instruction ins);
                list.Add(ins);
            }
            return list;
        }

        /// <summary>The exception directory (.pdata) of the DLL file: one entry per function chunk.</summary>
        private static List<RuntimeFunction> ReadFunctionTable(byte[] file)
        {
            int pe = BitConverter.ToInt32(file, 0x3C);
            int directory = pe + 24 + 112 + 3 * 8;   // PE32+ optional header, data directory 3 = exceptions
            uint rva = BitConverter.ToUInt32(file, directory), size = BitConverter.ToUInt32(file, directory + 4);
            int offset = FileOffset(file, rva);
            var list = new List<RuntimeFunction>();
            for (int i = 0; i + 12 <= size; i += 12)
                list.Add(new RuntimeFunction { Begin = BitConverter.ToUInt32(file, offset + i), End = BitConverter.ToUInt32(file, offset + i + 4), Unwind = BitConverter.ToUInt32(file, offset + i + 8) });
            return list;
        }

        /// <summary>The function a chunk belongs to: follows chained unwind info (UNW_FLAG_CHAININFO) to the primary entry.</summary>
        private static uint PrimaryOf(byte[] file, RuntimeFunction f)
        {
            for (int depth = 0; depth < 16; depth++)
            {
                int u = FileOffset(file, f.Unwind & ~1u);
                int chained;
                if ((f.Unwind & 1) != 0) chained = u;   // the entry points straight at its parent's RUNTIME_FUNCTION
                else
                {
                    if (((file[u] >> 3) & 4) == 0) return f.Begin;
                    int codes = file[u + 2];
                    chained = u + 4 + 2 * ((codes + 1) & ~1);
                }
                f = new RuntimeFunction { Begin = BitConverter.ToUInt32(file, chained), End = BitConverter.ToUInt32(file, chained + 4), Unwind = BitConverter.ToUInt32(file, chained + 8) };
            }
            return 0;
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
