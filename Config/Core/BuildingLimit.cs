// Config/Core/BuildingLimit.cs
//
// PURPOSE: The ["Building Limit"] BuildingLimit setting of the GameplaySettings file (GitLab #9): the building total
//          the game shares out among the players when it decides whether the human player may place one more
//          building in skirmish, trails and multiplayer.
//
// GAME FACTS (CrusaderDE.dll, game 2.8.2; traced 2026-10-10):
//   - Buildings live in one fixed table of 4000 records in the building manager (Script Extender
//     GameBuildingManagerVA, RVA 0x64CCBB0; records from +0x5C, 0x32C bytes each; SE NUM_PREALLOC_BUILDINGS = 4000).
//     The table is a static block, followed by the unit table, so it cannot grow.
//   - The allocator (RVA 0xB47E0, SE "building spawn") searches records 1..3999 with constants in the code
//     (0xB48A6 cmp 0xF9F, 0xB48B4 cmp 0xFA0) and returns 0 when none is free.
//   - Every tick RVA 0xC60F0 (from 0xCDE60, DLL_RunTick) counts the live records: manager +0 = all players,
//     +4 + 4 * player = per player (0xC62BB / 0xC62D4), and +0x54 = 4000 - all (0xC6777 / 0xC6798).
//   - The human player's placement (DLL_MapAction -> 0x8AAF0 -> 0x90CD0) refuses with message 333
//     (BHELP_TEXT_MAX_LIMIT, the "limit reached" text) at 0x91BF4 when:
//       a) free records (+0x54) < 3, or < 20 for everything except siege tents (11..19 still allowed) and the
//          tunnel (3..19 allowed): the map-wide limit, all players together, every game mode;
//       b) only when the game mode (RVA 0x8574B90) is not 0, i.e. skirmish, trails (0x63) and multiplayer:
//          4000 / players - own buildings < 2 (0x91AEA-0x91BD7). Players = the occupied lobby slots (at least 2).
//          So the human player's own share is 4000 / players - 1 buildings: 1999 with 2 players, 499 with 8.
//          Mode 0 (campaign, free build, editor) skips b) (0x91AF0 loads 1000 and jumps past it).
//     Only this human placement path reads the patched constant. Other building code checks free records >= 20
//     (e.g. 0x50E31, 0x575D5) and one function weighs 4000 / players - own buildings > 100 (0x5992A); those callers
//     were not identified (probably the AI [unverified]) and are not changed.
//
// WHAT THIS DOES: BuildingLimit replaces the 4000 in b), the immediate of "mov eax, 4000" at RVA 0x91BAF, through
//   RedBird's ManagedAssemblyImmediate (as EngageDistancePatch does; restore = clear the override). The code is
//   found by pattern in CrusaderDE.dll on disk and must be unchanged in memory. The map-wide limit a) and the table
//   are not touched, so no setting can create more than 3999 buildings: a raised value only lets the human player
//   use more of the shared table. Campaign / free build: no effect (no per-player share there).
//
// RULES (as UnitLimit):
//   - Applied from the session Post hooks (ApplyAllGlobalConfigs, OnPostLoad); -1 and every OnUnloadMap Post put
//     the game's code back.
//   - Multiplayer: only the clicking player's own game runs this check, so a different value cannot desync, but host
//     config sync gives everyone the host's value anyway.
//
// IMPORTANT FOR AI AGENTS:
// - Tests/HeadlessSelfTest.Pools.cs places buildings through the game's own click path (EngineInterface.PlaceMapperItem)
//   to measure both limits; keep the RVAs above in sync with it.
//
using System;
using RedBird.X64.Assembly.Stateful;
using Tomlyn.Model;

namespace CrusaderDETweaker.Config.Core
{
    internal static class BuildingLimit
    {
        internal const string Section = "Building Limit";
        internal const string Key = "BuildingLimit";
        internal const int GameValue = 4000, TableSize = 4000;
        // 32000 = 8 players x the whole table: with any number of players the share then reaches the map-wide limit.
        internal const int Minimum = 1000, Maximum = 32000;

        // movsxd rcx, [local player]; lea r15, [building manager]; cmp r9d, ebp; mov eax, 4000; cmovb r9d, ebp;
        // xor edx, edx; div r9d; sub eax, [r15 + rcx*4 + 4]   (RVA 0x91B9E on game 2.8.2)
        internal const string SharePattern = "48 63 0D ? ? ? ? 4C 8D 3D ? ? ? ? 44 3B CD B8 ? ? ? ? 44 0F 42 CD 33 D2 41 F7 F1 41 2B 44 8F 04";
        private const int ManagerLeaOffset = 7, MovOffset = 0x11;

        private static readonly object Owner = new object();
        private static ManagedAssemblyImmediate<int> _immediate;
        private static bool _searched;
        private static string _problem;
        private static int _applied = -1;

        /// <summary>RVA of the "mov eax, 4000" (0 until found).</summary>
        internal static uint MovRva { get; private set; }

        /// <summary>The config value: -1 = game value; null when the key is missing or not a number.</summary>
        internal static long? ReadConfigured(TomlTable model)
        {
            if (model == null || !model.TryGetValue(Section, out var sectionObj) || !(sectionObj is TomlTable section)) return null;
            if (!section.TryGetValue(Key, out var raw)) return null;
            if (raw is long value) return value;
            Plugin.Logger.LogWarning($"[BuildingLimit] {Key} = {raw} is not a whole number; the game value is used.");
            return null;
        }

        /// <summary>Applies the ["Building Limit"] value from a parsed GameplaySettings file (missing = game value).</summary>
        internal static void ApplyFromConfig(TomlTable model, string reason) => Apply(ReadConfigured(model) ?? -1, reason);

        /// <summary>Writes the configured total (clamped to Minimum..Maximum), or puts the game's code back for a negative value.</summary>
        internal static void Apply(long configured, string reason)
        {
            if (configured < 0)
            {
                RestoreGameValue(reason);
                return;
            }
            if (!EnsureFound())
            {
                Plugin.Logger.LogWarning($"[BuildingLimit] Not applied ({reason}): {_problem}");
                return;
            }
            int target = (int)Math.Max(Minimum, Math.Min(Maximum, configured));
            if (target != configured)
                Plugin.Logger.LogWarning($"[BuildingLimit] {Key} = {configured} is outside {Minimum}-{Maximum}; using {target}.");
            if (target == _applied && Current() == target) return;

            if (target == GameValue) _immediate.ClearOverrides(Owner);
            else _immediate.SetOverride(Owner, target, 0, Key);
            int readBack = Current();
            _applied = readBack == target ? target : -1;
            if (_applied == target)
                Plugin.Logger.LogInfo($"[BuildingLimit] Building limit {target} shared among the players (game {GameValue}) ({reason}); read back {readBack}. " +
                                      $"Your own limit in skirmish, trails and multiplayer: {target} / players - 1 (2 players: {Share(target, 2)}, 8 players: {Share(target, 8)}); " +
                                      $"all players together still stop at about {TableSize - 20} (the game's table has {TableSize} places).");
            else
                Plugin.Logger.LogWarning($"[BuildingLimit] Wrote {target} ({reason}) but read back {readBack}: not applied.");
        }

        /// <summary>Puts the game's code back (map unload, -1).</summary>
        internal static void RestoreGameValue(string reason)
        {
            if (_immediate == null || _applied < 0) return;
            _immediate.ClearOverrides(Owner);
            _applied = -1;
            Plugin.Logger.LogInfo($"[BuildingLimit] Game building limit {GameValue} restored ({reason}); read back {Current()}.");
        }

        /// <summary>The human player's largest building count the check allows with <paramref name="players"/> players.</summary>
        internal static int Share(int total, int players) => total / Math.Max(2, players) - 1;

        /// <summary>The value the game code uses now (-1 when the code was not found).</summary>
        internal static unsafe int Current()
        {
            if (!EnsureFound()) return -1;
            return *(int*)((long)GameCode.ModuleBase + MovRva + 1);
        }

        /// <summary>Finds and checks the code once; false (with the reason in the log) when it does not match.</summary>
        internal static unsafe bool EnsureFound()
        {
            if (_searched) return _immediate != null;
            _searched = true;
            byte[] file;
            try { file = GameCode.ReadModuleFile(); }
            catch (Exception ex) { _problem = $"{GameCode.ModuleName} could not be read ({ex.Message})"; return false; }
            if (!GameCode.TryFindUnique(file, SharePattern, out uint rva, out _problem)) return false;

            long module = (long)GameCode.ModuleBase;
            uint movRva = rva + MovOffset;
            int fileValue = GameCode.ReadFileInt32(file, movRva + 1);
            int disp = GameCode.ReadFileInt32(file, rva + ManagerLeaOffset + 3);
            ulong manager = (ulong)(module + rva + ManagerLeaOffset + 7 + disp);
            ulong seManager = Plugin.GlobalsApi?.GameBuildingManagerVA ?? 0;
            if (fileValue != GameValue || (seManager != 0 && manager != seManager))
            {
                _problem = $"the code at RVA 0x{movRva:X} is not the expected building share (constant {fileValue}, manager 0x{manager:X}, Script Extender 0x{seManager:X}); a game update?";
                return false;
            }
            // The bytes in memory must still be the file's (no other mod or hook changed this code).
            byte* live = (byte*)(module + rva);
            for (int k = 0; k < SharePattern.Split(' ').Length; k++)
            {
                if (live[k] == FileByte(file, rva + (uint)k)) continue;
                _problem = $"the game code at RVA 0x{rva + k:X} was changed by another mod; the building limit is left alone";
                return false;
            }
            MovRva = movRva;
            _immediate = new ManagedAssemblyImmediate<int>((IntPtr)(module + movRva), 1, 0L, null, $"building share 0x{movRva:X}");
            Plugin.Logger.LogInfo($"[BuildingLimit] Building share check found at RVA 0x{movRva:X} (constant {fileValue}).");
            return true;
        }

        private static byte FileByte(byte[] file, uint rva) => (byte)(GameCode.ReadFileInt32(file, rva) & 0xFF);
    }
}
