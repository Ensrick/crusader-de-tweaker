// Config/Toml/Units/Properties/EngineerSpeedPatch.cs
//
// PURPOSE: Makes the Units-file Speed of CHIMP_TYPE_ENGINEER reach Engineers while they work (Nexus report 2026-11-06,
//          comment 179239047; GitHub #6, GitLab #28: "it doesnt work for engineers"). Before this, Speed only set the
//          speed an Engineer was created with; the Engineer's own code then wrote the game's constants over it.
//
// GAME FACTS (CrusaderDE.dll, game 2.8.2; every RVA below read from the DLL on disk, 2026-11-26):
//   - c_game_unit_init (RVA 0x19A240) copies the speed table into the unit's base speed +0x9A4 (Script Extender
//     r_CurrentSpeed) and +0x9A2 (r_CurrentSpeed2). Each step the speed function (RVA 0x19B2C0) sets +0x9A2 from +0x9A4
//     plus slope / terrain, and the movement step waits that many ticks (smaller = faster).
//   - The Engineer's update (unit function table entry 30, RVA 0x14AED0) writes +0x9A4 with constants in three places
//     (the game table's Engineer value is 1):
//       A. 0x14AF4B, every update before the state switch: 2 for an Engineer of a computer player whose +0xA82
//          (r_AITribeRole) is 10 ("mov word [unit+9A4h], r12w", r12 = 2 for the whole function).
//       B. 0x14B5C4, r_AIState 0x65: 1 (0x14B5AB "mov eax, 1; movzx r12d, ax"), or 2 when it still carries something
//          (+0x9E0 != 0 and +0xAA4 == 0: 0x14B595 jumps to 0x14B6F0, which sets the animation 0x1F5 and jumps back to
//          0x14B5B7 with r12 = 2).
//       C. 0x14B806, r_AIState 0x7A: "setne al; inc ax" at 0x14B7F5 = 1, or 2 while +0x9E0 != 0.
//     Nothing else writes an Engineer's +0x9A4 (every other writer is in another unit type's update).
//   - Measured before this fix (Tests/HeadlessSelfTest.EngineerSpeed.cs, selftest-20261108-002232 / -002443): on a
//     move order (states 0-2) Speed works; writing r_AIState 0x65 / 0x7A on a Speed 6 Engineer gave speed 1 for good.
//     Which of these an AI siege Engineer walks in is [unverified] (no AI siege run).
//
// WHAT THIS DOES (only while the Engineer's Speed is not 1, the game's value; at 1 the game's bytes are put back):
//   every constant becomes "game value - 1 + Speed": walking = Speed, carrying and the AI role-10 case = Speed + 1,
//   as the game's 1 / 2. Same-length rewrites, no new code anywhere else:
//       A (21 bytes at 0x14AF3F): lea rax, [rcx + r8 + 0A13h]; cmp word [rax + 6Fh], 0Ah; jne; mov word [rax - 6Fh], Speed+1
//          (the same compare and store through rax, which is not read again before the next call returns).
//       B (34 bytes at 0x14B5AB): mov ecx, 1; mov r12d, ecx; add r12d, Speed-1; mov [unit+660h], ecx; mov edx, ebx;
//          mov rcx, rbp; mov [unit+9A4h], r12w; nop. The animation value moves from r15d to ecx (rcx is reloaded
//          before the call). The carrying branch (11 bytes at 0x14B6F0) becomes "mov ecx, 1F5h; jmp <the add>; nop",
//          so it arrives with r12 = 2 like before and stores Speed + 1. r12 and r15 are not read again after the
//          store on either path (every path ends at the function's exit, which restores both).
//       C (3 bytes at 0x14B7F8): "inc ax" becomes "add eax, Speed" (the low 16 bits are the same as the game's at 1).
//   Checked on the file: no jump lands inside A, B (other than the carrying branch, retargeted) or C, anywhere in
//   .text; the flags these instructions leave are not read before the next call.
//
// IMPORTANT FOR AI AGENTS:
// - Applied from SpeedProperty.SetToAPI for the Engineer (template apply path: launch, map unload, session start, host
//   sync), when no Engineer update runs. The TemplateBaseline restore of the Engineer's Speed calls Restore().
// - Every site is found by pattern in CrusaderDE.dll ON DISK, must lie in the function the unit function table holds for
//   the Engineer (a Script Extender AIStateTracker clone or another mod's hook there: not patched, warning), and must
//   hold the game's bytes in memory or a previous form of ours before anything is written.
// - Speed is at most SpeedProperty.MaxSpeed (30), so Speed + 1 fits every immediate here (A 16-bit, B and C signed 8-bit).
//
using System;
using System.Runtime.InteropServices;
using SHCDESE.GameGlobals;
using SHCDESE.Interop;
using CrusaderDETweaker.Config.Core;

namespace CrusaderDETweaker.Config.Toml.Units.Properties
{
    internal static class EngineerSpeedPatch
    {
        /// <summary>The Engineer's walking speed in the game's code (and its speed table entry).</summary>
        internal const int GameSpeed = 1;

        private sealed class Site
        {
            internal string Name;
            internal uint Rva;
            internal byte[] Game;
            internal Func<int, byte[]> Patched;   // bytes for a Speed
            internal int[] SpeedBytes;            // offsets that differ between two Speeds (ignored when recognising ours)
        }

        // A: cmp dword [r9+rbp*4+?], -1; jne; cmp dword [r9+rbp*4+?], 0; je; then the role-10 compare and the store.
        private const string PatternA = "41 83 BC A9 ? ? ? ? FF 75 20 41 83 BC A9 ? ? ? ? 00 74 15 66 42 83 BC 01 82 0A 00 00 0A 75 09 66 46 89 A4 01 A4 09 00 00";
        private const int OffsetA = 22;

        // B: cmp word [unit+0AA4h], di; je <carrying branch>; clear 0AA4h and 9E0h; then the 34 bytes; call.
        private const string PatternB = "66 39 BC 2E A4 0A 00 00 0F 84 ? ? ? ? 66 89 BC 2E A4 0A 00 00 66 89 BC 2E E0 09 00 00 B8 01 00 00 00 44 0F B7 E0 44 8B F8 44 89 BC 2E 60 06 00 00 8B D3 48 8B CD 66 44 89 A4 2E A4 09 00 00 E8";
        private const int OffsetJeB = 8, OffsetB = 30;

        // C: mov r15d, 1F5h; cmove r15d, r8d; setne al; inc ax; mov [unit+660h], r15d; mov edx, r14d; mov [unit+9A4h], ax.
        private const string PatternC = "41 BF F5 01 00 00 45 0F 44 F8 0F 95 C0 66 FF C0 46 89 BC 16 60 06 00 00 41 8B D6 66 42 89 84 16 A4 09 00 00";
        private const int OffsetC = 13;

        private static readonly byte[] GameA = { 0x66, 0x42, 0x83, 0xBC, 0x01, 0x82, 0x0A, 0x00, 0x00, 0x0A, 0x75, 0x09, 0x66, 0x46, 0x89, 0xA4, 0x01, 0xA4, 0x09, 0x00, 0x00 };
        private static readonly byte[] GameB =
        {
            0xB8, 0x01, 0x00, 0x00, 0x00, 0x44, 0x0F, 0xB7, 0xE0, 0x44, 0x8B, 0xF8, 0x44, 0x89, 0xBC, 0x2E, 0x60, 0x06, 0x00, 0x00,
            0x8B, 0xD3, 0x48, 0x8B, 0xCD, 0x66, 0x44, 0x89, 0xA4, 0x2E, 0xA4, 0x09, 0x00, 0x00
        };
        private static readonly byte[] GameCarry = { 0x41, 0xBF, 0xF5, 0x01, 0x00, 0x00, 0xE9 };   // + rel32 to B + 12
        private static readonly byte[] GameC = { 0x66, 0xFF, 0xC0 };

        private const int AddOffsetInB = 8;      // "add r12d, Speed-1" (the carrying branch's new target)
        private const int GameJoinInB = 12;      // the game's carrying branch target (mov [unit+660h], r15d)

        private static Site _a, _b, _carry, _c;
        private static bool _searched, _found;
        private static string _problem;
        private static int _applied = GameSpeed, _warned = -1;

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool VirtualProtect(IntPtr address, UIntPtr size, uint protect, out uint old);

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetCurrentProcess();

        [DllImport("kernel32.dll")]
        private static extern bool FlushInstructionCache(IntPtr process, IntPtr address, UIntPtr size);

        [StructLayout(LayoutKind.Sequential)]
        private struct RuntimeFunction { internal uint Begin, End, Unwind; }

        [DllImport("kernel32.dll")]
        private static extern IntPtr RtlLookupFunctionEntry(ulong controlPc, out ulong imageBase, IntPtr historyTable);

        /// <summary>The Speed the Engineer's code uses now (1 = the game's code).</summary>
        internal static int Applied => _applied;

        /// <summary>
        /// Makes the Engineer's code use <paramref name="speed"/> (walking; carrying and the AI case get speed + 1).
        /// 1 puts the game's bytes back. False with a warning when the code was not found or is not the game's.
        /// </summary>
        internal static bool Apply(int speed)
        {
            speed = Math.Max(0, Math.Min(SpeedProperty.MaxSpeed, speed));
            if (speed == _applied && (_searched || speed == GameSpeed)) return true;
            if (!EnsureFound())
            {
                if (speed != GameSpeed && speed != _warned)
                {
                    _warned = speed;
                    Plugin.Logger.LogWarning($"[Set Speed] CHIMP_TYPE_ENGINEER: Speed {speed} applies only to newly created Engineers until they start a task, because the Engineer's code could not be changed: {_problem}.");
                }
                return false;
            }
            // Order matters if a write fails halfway: the carrying branch may only jump to the "add" while B holds our
            // bytes (B + 8 is inside an instruction in the game's B, while B + 12 is an instruction start in both).
            if (speed == GameSpeed) { Write(_carry, _carry.Game); Write(_b, _b.Game); Write(_a, _a.Game); Write(_c, _c.Game); }
            else { Write(_a, _a.Patched(speed)); Write(_c, _c.Patched(speed)); Write(_b, _b.Patched(speed)); Write(_carry, _carry.Patched(speed)); }
            int before = _applied;
            _applied = speed;
            if (speed != before)
                Plugin.Logger.LogInfo(speed == GameSpeed
                    ? $"[Set Speed] CHIMP_TYPE_ENGINEER: Engineer code restored (game speeds 1, and 2 while carrying or in the computer's AI role 10; {Describe()})."
                    : $"[Set Speed] CHIMP_TYPE_ENGINEER: Engineers keep Speed {speed} while they work ({speed + 1} while carrying or in the computer's AI role 10, as the game's 1 / 2; game code {Describe()}).");
            return true;
        }

        /// <summary>Puts the game's bytes back (TemplateBaseline restore).</summary>
        internal static void Restore() => Apply(GameSpeed);

        /// <summary>"0x14AF3F game" style state of every site (logs and the self-test).</summary>
        internal static string Describe()
        {
            if (!EnsureFound()) return "not found: " + _problem;
            Site[] all = Sites();
            var parts = new string[all.Length];
            for (int i = 0; i < all.Length; i++)
                parts[i] = $"{all[i].Name} 0x{all[i].Rva:X} {(Holds(all[i], all[i].Game, null) ? "game" : IsOurs(all[i]) ? "patched" : "other")}";
            return string.Join(", ", parts);
        }

        private static Site[] Sites() => new[] { _a, _b, _carry, _c };

        private static bool IsOurs(Site s) => Holds(s, s.Patched(SpeedProperty.MaxSpeed), s.SpeedBytes);

        private static bool EnsureFound()
        {
            if (_searched) return _found;
            _searched = true;
            try { _found = Find(); }
            catch (Exception ex) { _problem = $"{ex.GetType().Name}: {ex.Message}"; _found = false; }
            return _found;
        }

        private static unsafe bool Find()
        {
            byte[] file = GameCode.ReadModuleFile();
            if (!GameCode.TryFindUnique(file, PatternA, out uint a, out _problem)) return false;
            if (!GameCode.TryFindUnique(file, PatternB, out uint b, out _problem)) return false;
            if (!GameCode.TryFindUnique(file, PatternC, out uint c, out _problem)) return false;
            uint siteA = a + OffsetA, siteB = b + OffsetB, siteC = c + OffsetC;
            uint carry = (uint)(b + OffsetJeB + 6 + GameCode.ReadFileInt32(file, b + OffsetJeB + 2));

            _a = new Site
            {
                Name = "role-10", Rva = siteA, Game = GameA, SpeedBytes = new[] { 19, 20 },
                Patched = speed =>
                {
                    int v = speed + 1;
                    return new byte[]
                    {
                        0x4A, 0x8D, 0x84, 0x01, 0x13, 0x0A, 0x00, 0x00,   // lea rax, [rcx + r8 + 0A13h]
                        0x66, 0x83, 0x78, 0x6F, 0x0A,                     // cmp word [rax + 6Fh], 0Ah   (unit + 0A82h)
                        0x75, 0x06,                                       // jne past the store
                        0x66, 0xC7, 0x40, 0x91, (byte)v, (byte)(v >> 8)   // mov word [rax - 6Fh], Speed + 1   (unit + 9A4h)
                    };
                }
            };
            _b = new Site
            {
                Name = "state-0x65", Rva = siteB, Game = GameB, SpeedBytes = new[] { 11 },
                Patched = speed => new byte[]
                {
                    0xB9, 0x01, 0x00, 0x00, 0x00,                         // mov ecx, 1          (animation, was r15d)
                    0x41, 0x89, 0xCC,                                     // mov r12d, ecx       (the game's 1)
                    0x41, 0x83, 0xC4, (byte)(sbyte)(speed - 1),           // add r12d, Speed-1   (carrying branch joins here, r12 = 2)
                    0x89, 0x8C, 0x2E, 0x60, 0x06, 0x00, 0x00,             // mov [rsi+rbp+660h], ecx
                    0x8B, 0xD3,                                           // mov edx, ebx
                    0x48, 0x8B, 0xCD,                                     // mov rcx, rbp
                    0x66, 0x44, 0x89, 0xA4, 0x2E, 0xA4, 0x09, 0x00, 0x00, // mov [rsi+rbp+9A4h], r12w
                    0x90
                }
            };
            int gameRel = (int)(siteB + GameJoinInB) - (int)(carry + 11);
            byte[] carryGame = new byte[11];
            Array.Copy(GameCarry, carryGame, GameCarry.Length);
            BitConverter.GetBytes(gameRel).CopyTo(carryGame, 7);
            int ourRel = (int)(siteB + AddOffsetInB) - (int)(carry + 10);
            byte[] carryOurs = { 0xB9, 0xF5, 0x01, 0x00, 0x00, 0xE9, 0, 0, 0, 0, 0x90 };   // mov ecx, 1F5h; jmp <add>; nop
            BitConverter.GetBytes(ourRel).CopyTo(carryOurs, 6);
            _carry = new Site { Name = "state-0x65 carrying", Rva = carry, Game = carryGame, SpeedBytes = new int[0], Patched = _ => carryOurs };
            _c = new Site
            {
                Name = "state-0x7A", Rva = siteC, Game = GameC, SpeedBytes = new[] { 2 },
                Patched = speed => new byte[] { 0x83, 0xC0, (byte)(sbyte)speed }   // add eax, Speed
            };

            // The carrying branch must be the game's (its jmp lands on B + 12) in the file.
            for (int i = 0; i < carryGame.Length; i++)
                if (FileByte(file, carry + (uint)i) != carryGame[i]) { _problem = $"the Engineer's carrying branch at RVA 0x{carry:X} is not the expected code (a game update?)"; return false; }

            // All four sites must be in the function the unit function table runs for the Engineer.
            ulong* vtable = (ulong*)GameGlobalsManager.Instance.GameUnitFunctionsVTable.Pointer;
            if (vtable == null) { _problem = "the Script Extender has no unit function table"; return false; }
            ulong module = (ulong)(long)GameCode.ModuleBase;
            ulong fn = vtable[(int)eChimps.CHIMP_TYPE_ENGINEER];
            IntPtr entry = RtlLookupFunctionEntry(fn, out ulong fnModule, IntPtr.Zero);
            if (entry == IntPtr.Zero || fnModule != module)
            {
                _problem = $"the Engineer's update 0x{fn:X} is not the game's (replaced by the Script Extender or another mod)";
                return false;
            }
            var rf = (RuntimeFunction)Marshal.PtrToStructure(entry, typeof(RuntimeFunction));
            if (fn - module != rf.Begin)
            {
                _problem = $"the Engineer's update 0x{fn - module:X} does not start a game function (hooked?)";
                return false;
            }
            foreach (Site s in Sites())
            {
                if (s.Rva < rf.Begin || s.Rva + s.Game.Length > rf.End)
                {
                    _problem = $"{s.Name} code at RVA 0x{s.Rva:X} is not in the Engineer's update 0x{rf.Begin:X}-0x{rf.End:X} (a game update?)";
                    return false;
                }
                if (!Holds(s, s.Game, null) && !IsOurs(s))
                {
                    _problem = $"the game code at RVA 0x{s.Rva:X} ({s.Name}) was changed by another mod";
                    return false;
                }
            }
            return true;
        }

        private static byte FileByte(byte[] file, uint rva) => (byte)(GameCode.ReadFileInt32(file, rva) & 0xFF);

        /// <summary>True when the live bytes equal <paramref name="bytes"/> (offsets in <paramref name="skip"/> ignored).</summary>
        private static unsafe bool Holds(Site s, byte[] bytes, int[] skip)
        {
            byte* live = (byte*)((long)GameCode.ModuleBase + s.Rva);
            for (int i = 0; i < bytes.Length; i++)
            {
                if (skip != null && Array.IndexOf(skip, i) >= 0) continue;
                if (live[i] != bytes[i]) return false;
            }
            return true;
        }

        private static void Write(Site s, byte[] bytes)
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
