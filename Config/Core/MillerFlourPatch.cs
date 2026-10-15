// Config/Core/MillerFlourPatch.cs
//
// PURPOSE: Lets the Miller deliver the flour it carries, so its GoodYieldMultiplier (Config/Core/GoodYield.cs) reaches
//          the Stockpile (Nexus report 2026-10-13: Miller at 50, still 1 flour per trip).
//
// HOW THE GAME DOES IT (CrusaderDE.dll, game 2.8.2, Miller update RVA 0x1377C0; traced 2026-10-14):
//   - Taking wheat from the Stockpile (0x137CF6): carried (unit +0x9E0) = yield(miller, 1, no bonus flag), the yield
//     function the Script Extender hooks (GoodYield scales it there).
//   - Taking the flour at the mill, state 7 -> 8 (0x138162): "mov word [unit + 0x9E0], r13w" with r13 = 1, so the
//     count from the yield is overwritten with 1.
//   - Drop-off, state 8 (0x13827D): the goods add (RVA 0xB6100) with amount 1 (flour, good 0x10), then
//     "mov word [unit + 0x9E0], si" with si = 0 (0x138290). Every other worker decrements the count here instead
//     (Woodcutter 0x12CA32 "dec word [unit + 0x9E0]"), and state 8 delivers again on the next tick while the count is
//     above 0 (0x1383B9), checking the Stockpile's room before every bag (0xBD490) and walking to another Stockpile
//     when it is full (0x1382E4).
//
// WHAT THIS DOES (only while the Miller's GoodYieldMultiplier is not 1, so the game's own behaviour is untouched
//   otherwise): 0x138162 becomes a 9-byte NOP (the yield count is kept) and 0x138290 becomes
//   "dec word [rcx + r15 + 0x9E0]" (same length), so the Miller delivers its count one bag per tick like the other
//   workers. Both sites are found by pattern in the DLL file and must hold the game's bytes in memory (or ours)
//   before anything is written. The restore writes the game's bytes back. Side effect while active: the game's
//   productivity bonus reaches the Miller too (the game itself always delivered exactly 1).
//
// IMPORTANT FOR AI AGENTS:
// - The Script Extender hooks the drop-off at 0x13824F (OnMillerDropOffFlour) and the flour pickup at 0x138134; the
//   patched bytes are outside both hooks. Called from GoodYield.Set, i.e. the template apply path (launch, map
//   unload, session start), when no Miller update runs.
// - Tests/HeadlessSelfTest.Economy.cs measures a real Miller trip with and without the patch.
//
using System;
using System.Runtime.InteropServices;

namespace CrusaderDETweaker.Config.Core
{
    internal static class MillerFlourPatch
    {
        private sealed class Site
        {
            internal string Name, Pattern;
            internal int Offset;
            internal byte[] Game, Patched;
            internal uint Rva;
        }

        // B8 08 00 00 00 = mov eax, 8 (state 8); mov word [rcx+r15+0x918], ax; mov word [rcx+r15+0x9E0], r13w; mov byte [rcx+r15+0xAA6], sil
        private static readonly Site Pickup = new Site
        {
            Name = "flour pickup (carried = 1)",
            Pattern = "B8 08 00 00 00 66 42 89 84 39 18 09 00 00 66 46 89 AC 39 E0 09 00 00 42 88 B4 39 A6 0A 00 00",
            Offset = 14,
            Game = new byte[] { 0x66, 0x46, 0x89, 0xAC, 0x39, 0xE0, 0x09, 0x00, 0x00 },
            Patched = new byte[] { 0x66, 0x0F, 0x1F, 0x84, 0x00, 0x00, 0x00, 0x00, 0x00 },   // nop word [rax + rax + 0]
        };

        // call goods add; movsxd rdi, [current unit]; imul rcx, rdi, 0x490; mov word [rcx+r15+0x9E0], si; cmp byte [rcx+r15+0xAA6], sil
        private static readonly Site DropOff = new Site
        {
            Name = "flour drop-off (carried = 0)",
            Pattern = "E8 ? ? ? ? 48 63 3D ? ? ? ? 48 69 CF 90 04 00 00 66 42 89 B4 39 E0 09 00 00 42 38 B4 39 A6 0A 00 00",
            Offset = 19,
            Game = new byte[] { 0x66, 0x42, 0x89, 0xB4, 0x39, 0xE0, 0x09, 0x00, 0x00 },
            Patched = new byte[] { 0x66, 0x42, 0xFF, 0x8C, 0x39, 0xE0, 0x09, 0x00, 0x00 },   // dec word [rcx + r15 + 0x9E0]
        };

        private static readonly Site[] Sites = { Pickup, DropOff };
        private static bool _searched, _found, _active;
        private static string _problem;

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool VirtualProtect(IntPtr address, UIntPtr size, uint protect, out uint old);

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetCurrentProcess();

        [DllImport("kernel32.dll")]
        private static extern bool FlushInstructionCache(IntPtr process, IntPtr address, UIntPtr size);

        /// <summary>True while the Miller delivers its whole count (the game code is changed).</summary>
        internal static bool Active => _active;

        /// <summary>The two RVAs (self-test), 0 until found.</summary>
        internal static uint PickupRva => Pickup.Rva;
        internal static uint DropOffRva => DropOff.Rva;

        /// <summary>Changes the game code (on = true) or puts the game's bytes back; false with a log line when not possible.</summary>
        internal static bool Apply(bool on)
        {
            if (on == _active) return true;
            if (!EnsureFound())
            {
                if (on) Plugin.Logger.LogWarning($"[GoodYield] CHIMP_TYPE_MILLER: GoodYieldMultiplier cannot reach the Stockpile: {_problem}. The Miller still delivers 1 flour per trip.");
                return false;
            }
            foreach (Site s in Sites) Write(s, on ? s.Patched : s.Game);
            _active = on;
            Plugin.Logger.LogInfo(on
                ? $"[GoodYield] CHIMP_TYPE_MILLER: the Miller now delivers all the flour it carries, one bag per tick (game code 0x{Pickup.Rva:X} and 0x{DropOff.Rva:X} changed; the game always delivered 1)."
                : $"[GoodYield] CHIMP_TYPE_MILLER: game code 0x{Pickup.Rva:X} and 0x{DropOff.Rva:X} restored (1 flour per trip, the game's behaviour).");
            return true;
        }

        /// <summary>"game", "patched" or "other" for each site (self-test).</summary>
        internal static string Describe()
        {
            if (!EnsureFound()) return "not found: " + _problem;
            var parts = new string[Sites.Length];
            for (int i = 0; i < Sites.Length; i++)
                parts[i] = $"0x{Sites[i].Rva:X} {(Holds(Sites[i], Sites[i].Game) ? "game" : Holds(Sites[i], Sites[i].Patched) ? "patched" : "other")}";
            return string.Join(", ", parts);
        }

        private static bool EnsureFound()
        {
            if (_searched) return _found;
            _searched = true;
            byte[] file;
            try { file = GameCode.ReadModuleFile(); }
            catch (Exception ex) { _problem = $"{GameCode.ModuleName} could not be read ({ex.Message})"; return false; }
            foreach (Site s in Sites)
            {
                if (!GameCode.TryFindUnique(file, s.Pattern, out uint rva, out _problem)) return false;
                s.Rva = rva + (uint)s.Offset;
                if (!Holds(s, s.Game) && !Holds(s, s.Patched))
                {
                    _problem = $"the game code at RVA 0x{s.Rva:X} ({s.Name}) was changed by another mod";
                    return false;
                }
            }
            _found = true;
            return true;
        }

        private static unsafe bool Holds(Site s, byte[] bytes)
        {
            byte* live = (byte*)((long)GameCode.ModuleBase + s.Rva);
            for (int i = 0; i < bytes.Length; i++) if (live[i] != bytes[i]) return false;
            return true;
        }

        private static unsafe void Write(Site s, byte[] bytes)
        {
            var address = (IntPtr)((long)GameCode.ModuleBase + s.Rva);
            var size = (UIntPtr)bytes.Length;
            if (!VirtualProtect(address, size, 0x40, out uint old))   // PAGE_EXECUTE_READWRITE
                throw new InvalidOperationException($"VirtualProtect failed at RVA 0x{s.Rva:X} (error {Marshal.GetLastWin32Error()})");
            try { Marshal.Copy(bytes, 0, address, bytes.Length); }
            finally
            {
                VirtualProtect(address, size, old, out _);
                FlushInstructionCache(GetCurrentProcess(), address, size);
            }
        }
    }
}
