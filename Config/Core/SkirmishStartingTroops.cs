// Config/Core/SkirmishStartingTroops.cs
//
// PURPOSE: GameplaySettings ["Skirmish Starting Troops"] (GitHub #2): the soldiers each player receives at the start
//          of a custom skirmish, per start option (Normal / Crusader / Deathmatch), for human players and, with
//          ApplyToAI = true, for AI lords.
//
// HOW THE GAME DOES IT (CrusaderDE.dll, game 2.8.2; the Script Extender calls RVA 0x94350 its skirmish start handler):
//   - 0x94350 places every player's keep, then rebuilds a per-player troop queue (loop 0x96290-0x965AA): 28 int32
//     slots per player at RVA 0x382D354 + player * 0x70, players 1-8, every slot written for every player.
//   - Human players (int32 at 0x8574BCC + player * 4 is not -1) get a row of the static table at RVA 0x2D4FA0 (0x70
//     bytes per row): row 90 + start option - 1, or 93 / 96 + start option - 1 for an Arabian / Bedouin lord (lord
//     type at 0x366A0CC, faction function 0x1CE40), scaled by the AI advantage percentage (table 0x2D4F70). AI lords
//     (the Script Extender's AI line-up, 0x8574C44 + player * 4, not 0) get their AIC starting troops, scaled by the
//     inverse percentage. Start option = int32 0x87ECA04 (the Script Extender's GetCurrentSkirmishMode: 1 Normal,
//     2 Crusader, 3 Deathmatch, as the game's briefing names them).
//   - Delivery (0x119190-0x119394, the multiplayer / skirmish branch of 0x119050): every 200 ticks the first non-zero
//     slot of each human or AI player spawns up to 9 of that unit (0x117D20) and is decremented in place.
//   - Slot -> unit (jump table 0x1199EC): see Slots. Slots 7, 18 and 19 are never read.
//   The Script Extender's Get/SetPlayerSkirmishDefaultUnitsAmount does not reach this queue (it writes the AIV buffer
//   at 0x34A9F70, four unit types per int32 slot), so it is not used.
//
// WHAT THIS DOES:
//   OnStartMap Pre: if the file sets any count, writes a canary into slot 7 of players 1-8.
//   OnStartMap Post: all canaries overwritten = 0x94350 just rebuilt the queue. If it is also a custom skirmish (the
//   Script Extender's GetCurrentSkirmishGameMode() == SKIRMISH_GAME_CUSTOM: trails and their missions are left
//   alone), every slot the start option's table sets (>= 0) replaces that slot's count for each human player, and for
//   each AI lord when ApplyToAI = true. -1 keeps the game's count. Counts are exact (the AI advantage setting does not
//   scale them). Otherwise the canaries are put back and nothing is written.
//   Addresses: queue and static table from one code pattern (0x962F5), cross-checked with a second pattern (0x96284),
//   the human-player array from the faction function (0x1CE40); the static table rows 90-98 must hold the game's
//   values (signature) or the feature switches itself off with a warning.
//
// IMPORTANT FOR AI AGENTS:
// - Session state like the rest of GameplaySettings: read from ConfigPaths.Globals at each start (host sync aware).
//   Nothing to restore: the game rebuilds the queue at every skirmish start. Loaded saves are never touched.
// - Every multiplayer peer writes the same values in its own OnStartMap Post, so everyone needs the same file
//   (host config sync sends GameplaySettings).
// - Not measured in game: the editor map never runs 0x94350 and its delivery returns at once (0x11906C: game type 1),
//   so the self-test checks the addresses, the table signature and a queue write / read-back only.
//
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using R3;
using SHCDESE.API;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.MapLoader;
using SHCDESE.Interop;
using Tomlyn.Model;

namespace CrusaderDETweaker.Config.Core
{
    internal static class SkirmishStartingTroops
    {
        internal const string Section = "Skirmish Starting Troops";
        internal const string ApplyToAIKey = "ApplyToAI";

        /// <summary>Start options in game order (start level 1, 2, 3).</summary>
        internal static readonly string[] Levels = { "Normal", "Crusader", "Deathmatch" };

        internal const int SlotCount = 28, PlayerStride = 0x70, MaxPlayer = 8, MaxCount = 1000;
        internal const int CanarySlot = 7;
        internal const int Canary = 0x43445453;   // never a troop count; slot 7 is never read by the delivery

        internal sealed class TroopSlot
        {
            internal readonly string Key;
            internal readonly int Slot;
            internal readonly eChimps Unit;
            internal TroopSlot(string key, int slot, eChimps unit) { Key = key; Slot = slot; Unit = unit; }
        }

        /// <summary>The queue slots the delivery reads (jump table RVA 0x1199EC), with their config keys.</summary>
        internal static readonly TroopSlot[] Slots =
        {
            new TroopSlot("Archer", 0, eChimps.CHIMP_TYPE_ARCHER),
            new TroopSlot("Crossbowman", 1, eChimps.CHIMP_TYPE_XBOWMAN),
            new TroopSlot("Spearman", 2, eChimps.CHIMP_TYPE_SPEARMAN),
            new TroopSlot("Pikeman", 3, eChimps.CHIMP_TYPE_PIKEMAN),
            new TroopSlot("Maceman", 4, eChimps.CHIMP_TYPE_MACEMAN),
            new TroopSlot("Swordsman", 5, eChimps.CHIMP_TYPE_SWORDSMAN),
            new TroopSlot("Knight", 6, eChimps.CHIMP_TYPE_KNIGHT),
            new TroopSlot("Engineer", 8, eChimps.CHIMP_TYPE_ENGINEER),
            new TroopSlot("Monk", 9, eChimps.CHIMP_TYPE_MONK),
            new TroopSlot("ArabianBow", 10, eChimps.CHIMP_TYPE_ARAB_BOW),
            new TroopSlot("Slave", 11, eChimps.CHIMP_TYPE_ARAB_SLAVE),
            new TroopSlot("Slinger", 12, eChimps.CHIMP_TYPE_ARAB_SLINGER),
            new TroopSlot("Assassin", 13, eChimps.CHIMP_TYPE_ARAB_ASSASIN),
            new TroopSlot("HorseArcher", 14, eChimps.CHIMP_TYPE_ARAB_HORSEMAN),
            new TroopSlot("ArabianSwordsman", 15, eChimps.CHIMP_TYPE_ARAB_SWORDSMAN),
            new TroopSlot("FireThrower", 16, eChimps.CHIMP_TYPE_ARAB_GRENADIER),
            new TroopSlot("ArabianBallista", 17, eChimps.CHIMP_TYPE_ARAB_BALLISTA),
            new TroopSlot("CamelLancer", 20, eChimps.CHIMP_TYPE_BEDOUIN_CAMEL_LANCER),
            new TroopSlot("BedouinHealer", 21, eChimps.CHIMP_TYPE_BEDOUIN_HEALER),
            new TroopSlot("Eunuch", 22, eChimps.CHIMP_TYPE_BEDOUIN_EUNUCH),
            new TroopSlot("Ambusher", 23, eChimps.CHIMP_TYPE_BEDOUIN_AMBUSHER),
            new TroopSlot("Skirmisher", 24, eChimps.CHIMP_TYPE_BEDOUIN_SKIRMISHER),
            new TroopSlot("HeavyCamel", 25, eChimps.CHIMP_TYPE_BEDOUIN_HEAVY_CAMEL),
            new TroopSlot("Sapper", 26, eChimps.CHIMP_TYPE_BEDOUIN_SAPPER),
            new TroopSlot("Demolisher", 27, eChimps.CHIMP_TYPE_BEDOUIN_DEMOLISHER),
        };

        /// <summary>First static table row per lord faction (European, Arabian, Bedouin); add start level - 1.</summary>
        internal static readonly int[] FactionRows = { 90, 93, 96 };
        internal static readonly string[] FactionNames = { "European lord", "Arabian lord", "Bedouin lord" };

        /// <summary>
        /// The game's rows 90-98 (game 2.8.2, read from CrusaderDE.dll): slot -> count, the rest 0. Checked before
        /// anything is written, so a game update that moves or changes the table switches the feature off.
        /// </summary>
        internal static readonly Dictionary<int, Dictionary<int, int>> ExpectedRows = new Dictionary<int, Dictionary<int, int>>
        {
            { 90, new Dictionary<int, int> { { 0, 5 }, { 2, 7 } } },
            { 91, new Dictionary<int, int> { { 0, 40 }, { 5, 10 }, { 6, 4 } } },
            { 92, new Dictionary<int, int> { { 0, 5 }, { 2, 7 } } },
            { 93, new Dictionary<int, int> { { 10, 6 }, { 11, 6 } } },
            { 94, new Dictionary<int, int> { { 10, 50 }, { 15, 10 } } },
            { 95, new Dictionary<int, int> { { 10, 6 }, { 11, 6 } } },
            { 96, new Dictionary<int, int> { { 24, 10 } } },
            { 97, new Dictionary<int, int> { { 24, 30 }, { 25, 20 } } },
            { 98, new Dictionary<int, int> { { 24, 10 } } },
        };

        // lea r11, [rip+queue]; sub rax, r12; lea r10, [r9 + table]; add rax, rsi   (RVA 0x962F5)
        internal const string QueuePattern = "4C 8D 1D ? ? ? ? 49 2B C4 4D 8D 91 ? ? ? ? 48 03 C6";
        // lea r14, [rip+queue+0x74] (player 1, slot 0, + 4); nop; mov eax, [start level]   (RVA 0x96284)
        internal const string QueueCrossCheckPattern = "4C 8D 35 ? ? ? ? 0F 1F 44 00 00 8B 05";
        // faction function: movsxd rax, edx; lea rdx, [rip+base]; cmp [rdx+rax*4+humans], -1; jne; imul rax, rax, 583Ch
        internal const string HumanArrayPattern = "48 63 C2 48 8D 15 ? ? ? ? 83 BC 82 ? ? ? ? FF 75 ? 48 69 C0 3C 58 00 00";

        // ===================================================
        // Settings (pure; unit-tested in Tests/EconomyTest.cs)
        // ===================================================

        internal sealed class Settings
        {
            internal bool ApplyToAI;
            /// <summary>[level index 0-2][slot] = count, -1 = keep the game's count.</summary>
            internal readonly int[][] Counts = Enumerable.Range(0, 3).Select(_ => Enumerable.Repeat(-1, SlotCount).ToArray()).ToArray();
            internal bool Any => Counts.Any(row => row.Any(c => c >= 0));
        }

        /// <summary>Reads the section from a parsed GameplaySettings file. Problems are reported, never thrown.</summary>
        internal static Settings Parse(TomlTable model, List<string> problems)
        {
            var settings = new Settings();
            if (model == null || !model.TryGetValue(Section, out var obj) || !(obj is TomlTable section)) return settings;

            if (section.TryGetValue(ApplyToAIKey, out var ai))
            {
                if (ai is bool b) settings.ApplyToAI = b;
                else problems.Add($"{ApplyToAIKey} = {ai}: not true or false; AI lords keep the game's troops");
            }

            for (int level = 0; level < Levels.Length; level++)
            {
                if (!section.TryGetValue(Levels[level], out var t) || !(t is TomlTable table)) continue;
                foreach (var kv in table)
                {
                    var slot = Slots.FirstOrDefault(s => s.Key == kv.Key);
                    if (slot == null) { problems.Add($"{Levels[level]}.{kv.Key}: unknown unit (known: {string.Join(", ", Slots.Select(s => s.Key))})"); continue; }
                    if (!(kv.Value is long v)) { problems.Add($"{Levels[level]}.{kv.Key} = {kv.Value}: not a whole number; the game's count is kept"); continue; }
                    if (v < 0) continue;   // -1 (or any negative) = the game's count
                    if (v > MaxCount) { problems.Add($"{Levels[level]}.{kv.Key} = {v} is above {MaxCount}; clamped to {MaxCount}"); v = MaxCount; }
                    settings.Counts[level][slot.Slot] = (int)v;
                }
            }
            return settings;
        }

        /// <summary>
        /// The decision at OnStartMap Post: apply only when the queue was just rebuilt (every canary overwritten) by a
        /// custom skirmish start with a known start option (1-3). Pure.
        /// </summary>
        internal static bool ShouldApply(bool queueRebuilt, eSkirmishGameMode mode, int level, out string reason)
        {
            if (!queueRebuilt) { reason = "not a skirmish start (the game did not rebuild the troop queue)"; return false; }
            if (mode != eSkirmishGameMode.SKIRMISH_GAME_CUSTOM) { reason = $"not a custom skirmish ({mode}); trails keep the game's troops"; return false; }
            if (level < 1 || level > Levels.Length) { reason = $"unknown start option {level}"; return false; }
            reason = null;
            return true;
        }

        // ===================================================
        // Game addresses
        // ===================================================

        private static bool _resolved;
        private static string _unavailable;
        private static uint _queueRva, _tableRva, _humansRva;

        internal static uint QueueRva => _queueRva;
        internal static uint TableRva => _tableRva;
        internal static uint HumansRva => _humansRva;

        /// <summary>Finds the queue, the static table and the human-player array, and checks the table rows (once).</summary>
        internal static bool TryResolve(out string problem)
        {
            if (!_resolved)
            {
                _resolved = true;
                _unavailable = Resolve();
            }
            problem = _unavailable;
            return problem == null;
        }

        private static string Resolve()
        {
            byte[] file;
            try { file = GameCode.ReadModuleFile(); }
            catch (Exception ex) { return $"{GameCode.ModuleName} could not be read ({ex.Message})"; }

            if (!GameCode.TryFindUnique(file, QueuePattern, out uint at, out string problem)) return problem;
            uint queue = (uint)(at + 7 + GameCode.ReadFileInt32(file, at + 3));
            uint table = (uint)GameCode.ReadFileInt32(file, at + 13);

            if (!GameCode.TryFindUnique(file, QueueCrossCheckPattern, out uint at2, out problem)) return problem;
            uint player1 = (uint)(at2 + 7 + GameCode.ReadFileInt32(file, at2 + 3)) - 4;
            if (player1 != queue + PlayerStride)
                return $"the two troop queue references disagree (0x{queue:X} + 0x{PlayerStride:X} vs 0x{player1:X})";

            if (!GameCode.TryFindUnique(file, HumanArrayPattern, out uint at3, out problem)) return problem;
            uint humans = (uint)GameCode.ReadFileInt32(file, at3 + 13);

            _queueRva = queue;
            _tableRva = table;
            _humansRva = humans;
            foreach (var row in ExpectedRows)
            {
                int[] actual = ReadRow(row.Key);
                for (int slot = 0; slot < SlotCount; slot++)
                {
                    row.Value.TryGetValue(slot, out int expected);
                    if (actual[slot] != expected)
                        return $"the game's starting troop table (0x{table:X}) row {row.Key} slot {slot} is {actual[slot]}, expected {expected} (a game update?)";
                }
            }
            return null;
        }

        /// <summary>A row of the game's static starting troop table (28 counts).</summary>
        internal static unsafe int[] ReadRow(int row)
        {
            int* p = (int*)((byte*)GameCode.ModuleBase + _tableRva + row * PlayerStride);
            var counts = new int[SlotCount];
            for (int i = 0; i < SlotCount; i++) counts[i] = p[i];
            return counts;
        }

        /// <summary>Pointer to a player's queue slot (players 0-8). Only after TryResolve succeeded.</summary>
        internal static unsafe int* QueueSlot(int player, int slot) =>
            (int*)((byte*)GameCode.ModuleBase + _queueRva + player * PlayerStride) + slot;

        /// <summary>The game's human-player test (0x8574BCC + player * 4 is not -1), as the queue rebuild uses it.</summary>
        internal static unsafe bool IsHuman(int player) =>
            *(int*)((byte*)GameCode.ModuleBase + _humansRva + player * 4) != -1;

        // ===================================================
        // Session hooks
        // ===================================================

        private static bool _subscribed;
        private static Settings _pending;
        private static readonly int[] _savedCanarySlots = new int[MaxPlayer + 1];

        internal static void Subscribe()
        {
            if (_subscribed) return;
            _subscribed = true;
            MapLoaderR3EventHooks.OnStartMap.Observable
                .Where(args => args.Phase == EventHookPhase.Pre)
                .Subscribe(_ => Guard("start (Pre)", OnStartPre));
            MapLoaderR3EventHooks.OnStartMap.Observable
                .Where(args => args.Phase == EventHookPhase.Post)
                .Subscribe(_ => Guard("start (Post)", OnStartPost));
        }

        private static void Guard(string where, Action action)
        {
            try { action(); }
            catch (Exception ex) { _pending = null; Plugin.Logger.LogError($"[{Section}] {where}: {ex}"); }
        }

        internal static unsafe void OnStartPre()
        {
            _pending = null;
            var settings = LoadFromFile();
            if (settings == null || !settings.Any) return;
            if (!TryResolve(out string problem))
            {
                Plugin.Logger.LogWarning($"[{Section}] Not applied: {problem}. The game's starting troops are used.");
                return;
            }
            for (int p = 1; p <= MaxPlayer; p++)
            {
                _savedCanarySlots[p] = *QueueSlot(p, CanarySlot);
                *QueueSlot(p, CanarySlot) = Canary;
            }
            _pending = settings;
        }

        internal static unsafe void OnStartPost()
        {
            var settings = _pending;
            _pending = null;
            if (settings == null) return;

            bool rebuilt = true;
            for (int p = 1; p <= MaxPlayer; p++)
                if (*QueueSlot(p, CanarySlot) == Canary) { rebuilt = false; *QueueSlot(p, CanarySlot) = _savedCanarySlots[p]; }

            var players = GamePlayerManagerAPI.Instance;
            eSkirmishGameMode mode = players.GetCurrentSkirmishGameMode();
            int level = (int)players.GetCurrentSkirmishMode();
            if (!ShouldApply(rebuilt, mode, level, out string reason))
            {
                Plugin.Logger.LogInfo($"[{Section}] Not applied: {reason}.");
                return;
            }
            Plugin.Logger.LogInfo($"[{Section}] {Levels[level - 1]} start: " + Apply(settings, level, players.IsAIPlayer));
        }

        /// <summary>
        /// Writes the configured counts of a start level (1-3) into the queue of every human player, and every AI lord
        /// when ApplyToAI is set. Returns a log line. Only after TryResolve succeeded.
        /// </summary>
        internal static unsafe string Apply(Settings settings, int level, Func<int, bool> isAI)
        {
            int[] counts = settings.Counts[level - 1];
            var log = new StringBuilder();
            for (int p = 1; p <= MaxPlayer; p++)
            {
                // The game's order (0x962A0): a human player first, else an AI lord, else an empty slot (no delivery).
                bool human = IsHuman(p), ai = !human && isAI(p);
                if (!ai && !human) continue;
                if (log.Length > 0) log.Append("; ");
                log.Append($"player {p} ({(ai ? "AI" : "human")})");
                if (ai && !settings.ApplyToAI) { log.Append(" unchanged (ApplyToAI = false)"); continue; }
                var changes = new List<string>();
                foreach (var slot in Slots)
                {
                    int count = counts[slot.Slot];
                    if (count < 0) continue;
                    int* cell = QueueSlot(p, slot.Slot);
                    if (*cell != count) changes.Add($"{slot.Key} {*cell} -> {count}");
                    *cell = count;
                }
                log.Append(changes.Count > 0 ? ": " + string.Join(", ", changes) : ": already as configured");
            }
            return log.Length > 0 ? log.ToString() : "no human or AI players found";
        }

        private static Settings LoadFromFile()
        {
            if (!Toml.Core.ConfigFileHelper.ConfigFileExists(Toml.ConfigPaths.Globals)) return null;
            TomlTable model;
            try { model = Tomlyn.Toml.ToModel(Toml.Core.ConfigFileHelper.ReadConfigFile(Toml.ConfigPaths.Globals)); }
            catch (Exception) { return null; }   // the session-start apply reports the syntax error
            var problems = new List<string>();
            var settings = Parse(model, problems);
            foreach (string problem in problems) Plugin.Logger.LogWarning($"[{Section}] {problem}");
            return settings;
        }
    }
}
