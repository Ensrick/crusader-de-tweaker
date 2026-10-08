// Config/Sync/LobbyMaxCounts.cs
//
// PURPOSE: MaxCount (unit / building caps) set in the lobby tab (GitLab #5), on top of the MaxCount
//          values of the Units and Structures files. One lobby value per type:
//            not set  = use the file's MaxCount
//            -1       = unlimited in this lobby (even if the file has a cap)
//            0        = disabled (no unit / no placement)
//            N > 0    = at most N alive / placed at once
//
// HOW: the lobby tab (ConfigSyncLobbySettings) keeps all set values in ONE [SyncHostOnly] string,
//   "CHIMP_TYPE_KNIGHT=10;STRUCT_STABLES=2" (sorted, so the same values always give the same text). SE
//   sends it to every player in the lobby and persists the host's own value (LobbyModSettings\*.msgpack
//   in the plugin folder). ConfigLoader.LoadUnitCaps / LoadBuildingCaps hand over the file caps after
//   every (re-)apply; this class keeps them and writes file caps + lobby values into the SAME dictionaries
//   that UnitCapHandler / MakeTroopRecruitHook / BuildingCapHandler read live. Enforcement is unchanged:
//   each game enforces its own (local, human) player's caps.
//
// IMPORTANT FOR AI AGENTS:
// - Parse / Encode / Overlay are pure (Tests/LobbyMaxCountsTest.cs). Keep game access out of them.
// - The lists of types shown in the tab are UnitTypes / BuildingTypes; a key outside them is ignored.
//
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using CrusaderDETweaker.Data;
using SHCDESE.Interop;

namespace CrusaderDETweaker.Config.Sync
{
    internal static class LobbyMaxCounts
    {
        private const string Tag = "[LobbyMaxCounts]";
        internal const int MaxValue = 100000;

        private static readonly eChimps[] SiegeEngines =
        {
            eChimps.CHIMP_TYPE_CATAPULT, eChimps.CHIMP_TYPE_TREBUCHET, eChimps.CHIMP_TYPE_MANGONEL, eChimps.CHIMP_TYPE_BALLISTA,
            eChimps.CHIMP_TYPE_ARAB_BALLISTA, eChimps.CHIMP_TYPE_SIEGE_TOWER, eChimps.CHIMP_TYPE_BATTERING_RAM, eChimps.CHIMP_TYPE_PORTABLE_SHIELD,
        };

        /// <summary>Units in the tab: every recruitable type, then the siege engines (enum order).</summary>
        internal static readonly eChimps[] UnitTypes =
            Enum.GetValues(typeof(eChimps)).Cast<eChimps>().Where(UnitCategories.IsRecruitable)
                .Concat(SiegeEngines).Distinct().ToArray();

        /// <summary>Buildings in the tab: every type of the Structures file except the destroyed-tower variants.</summary>
        internal static readonly eStructs[] BuildingTypes =
            Enum.GetValues(typeof(eStructs)).Cast<eStructs>()
                .Where(s => !StructureCategories.IsNonModifiable(s) && !s.ToString().EndsWith("_DESTROYED", StringComparison.Ordinal))
                .Distinct().ToArray();

        private static readonly HashSet<string> KnownKeys =
            new HashSet<string>(UnitTypes.Select(u => u.ToString()).Concat(BuildingTypes.Select(b => b.ToString())), StringComparer.Ordinal);

        private static readonly object _lock = new object();
        private static Dictionary<string, int> _lobby = new Dictionary<string, int>(StringComparer.Ordinal);
        private static Dictionary<eChimps, int> _unitTable, _unitFile = new Dictionary<eChimps, int>();
        private static Dictionary<eStructs, int> _buildingTable, _buildingFile = new Dictionary<eStructs, int>();

        /// <summary>"Arab Bow" from CHIMP_TYPE_ARAB_BOW, "Stables" from STRUCT_STABLES.</summary>
        internal static string Label(string key)
        {
            string name = key.StartsWith("CHIMP_TYPE_", StringComparison.Ordinal) ? key.Substring(11)
                        : key.StartsWith("STRUCT_", StringComparison.Ordinal) ? key.Substring(7) : key;
            var words = name.Split(new[] { '_' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(w => w.Length == 0 ? w : char.ToUpperInvariant(w[0]) + w.Substring(1).ToLowerInvariant());
            return string.Join(" ", words);
        }

        // ===================================================
        // Pure parts
        // ===================================================

        /// <summary>
        /// Text of one box: "" = not set (null), else a whole number from -1 to MaxValue. False = not valid.
        /// </summary>
        internal static bool TryParseValue(string text, out int? value)
        {
            value = null;
            text = (text ?? string.Empty).Trim();
            if (text.Length == 0) return true;
            if (!int.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int v) || v < -1 || v > MaxValue)
                return false;
            value = v;
            return true;
        }

        /// <summary>Reads "KEY=V;KEY=V". Unknown keys and bad values are skipped and reported.</summary>
        internal static Dictionary<string, int> Parse(string encoded, List<string> problems)
        {
            var result = new Dictionary<string, int>(StringComparer.Ordinal);
            if (string.IsNullOrWhiteSpace(encoded)) return result;
            foreach (string part in encoded.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                int eq = part.IndexOf('=');
                string key = eq > 0 ? part.Substring(0, eq).Trim() : part.Trim();
                string text = eq > 0 ? part.Substring(eq + 1) : string.Empty;
                if (!KnownKeys.Contains(key)) { problems?.Add($"unknown type '{key}'"); continue; }
                if (!TryParseValue(text, out int? v) || !v.HasValue) { problems?.Add($"{key}: '{text.Trim()}' is not a number from -1 to {MaxValue}"); continue; }
                result[key] = v.Value;
            }
            return result;
        }

        /// <summary>Sorted "KEY=V;KEY=V" (empty when nothing is set).</summary>
        internal static string Encode(IEnumerable<KeyValuePair<string, int>> values)
        {
            var sb = new StringBuilder();
            foreach (var kv in values.OrderBy(kv => kv.Key, StringComparer.Ordinal))
            {
                if (sb.Length > 0) sb.Append(';');
                sb.Append(kv.Key).Append('=').Append(kv.Value.ToString(CultureInfo.InvariantCulture));
            }
            return sb.ToString();
        }

        /// <summary>File caps with the lobby values on top: -1 removes the cap (unlimited), 0..N sets it.</summary>
        internal static Dictionary<T, int> Overlay<T>(IDictionary<T, int> fileCaps, IDictionary<string, int> lobby, Func<string, T?> parseKey) where T : struct
        {
            var effective = new Dictionary<T, int>(fileCaps);
            foreach (var kv in lobby)
            {
                T? key = parseKey(kv.Key);
                if (!key.HasValue) continue;
                if (kv.Value < 0) effective.Remove(key.Value);
                else effective[key.Value] = kv.Value;
            }
            return effective;
        }

        private static eChimps? UnitKey(string key) =>
            key.StartsWith("CHIMP_", StringComparison.Ordinal) && Enum.TryParse(key, out eChimps u) ? u : (eChimps?)null;

        private static eStructs? BuildingKey(string key) =>
            key.StartsWith("STRUCT_", StringComparison.Ordinal) && Enum.TryParse(key, out eStructs b) ? b : (eStructs?)null;

        // ===================================================
        // Live cap tables
        // ===================================================

        /// <summary>
        /// Called once by ConfigLoader.RegisterSessionHooks with the tables the cap handlers read, so lobby values
        /// apply even before (or without) a Units / Structures file. Keeps the file caps known so far.
        /// </summary>
        internal static void Attach(Dictionary<eChimps, int> unitTable, Dictionary<eStructs, int> buildingTable)
        {
            lock (_lock)
            {
                _unitTable = unitTable;
                _buildingTable = buildingTable;
                WriteUnits();
                WriteBuildings();
            }
        }

        /// <summary>Called by ConfigLoader.LoadUnitCaps after it filled the table from the Units file.</summary>
        internal static void UnitFileCapsLoaded(Dictionary<eChimps, int> table)
        {
            lock (_lock)
            {
                _unitTable = table;
                _unitFile = new Dictionary<eChimps, int>(table);
                WriteUnits();
            }
        }

        /// <summary>Called by ConfigLoader.LoadBuildingCaps after it filled the table from the Structures file.</summary>
        internal static void BuildingFileCapsLoaded(Dictionary<eStructs, int> table)
        {
            lock (_lock)
            {
                _buildingTable = table;
                _buildingFile = new Dictionary<eStructs, int>(table);
                WriteBuildings();
            }
        }

        /// <summary>New lobby values (the tab's string); applied to the cap tables at once.</summary>
        internal static void SetLobbyValues(string encoded, string source)
        {
            var problems = new List<string>();
            var parsed = Parse(encoded, problems);
            lock (_lock)
            {
                bool same = parsed.Count == _lobby.Count && parsed.All(kv => _lobby.TryGetValue(kv.Key, out int v) && v == kv.Value);
                _lobby = parsed;
                WriteUnits();
                WriteBuildings();
                if (same && problems.Count == 0) return;
            }
            foreach (string p in problems) Plugin.Logger?.LogWarning($"{Tag} Ignored lobby value from {source}: {p}.");
            Plugin.Logger?.LogInfo($"{Tag} Lobby MaxCount values ({source}): {(parsed.Count == 0 ? "none (the Units / Structures files apply)" : Encode(parsed).Replace(";", ", "))}. " +
                                   $"Caps in effect: {_unitTable?.Count ?? 0} unit, {_buildingTable?.Count ?? 0} building type(s).");
        }

        /// <summary>Caps in effect now (self-test / log).</summary>
        internal static int? EffectiveCap(eChimps unit)
        {
            lock (_lock) return _unitTable != null && _unitTable.TryGetValue(unit, out int c) ? c : (int?)null;
        }

        internal static int? EffectiveCap(eStructs building)
        {
            lock (_lock) return _buildingTable != null && _buildingTable.TryGetValue(building, out int c) ? c : (int?)null;
        }

        // Rewrites the table the handlers hold (same instance: they read it live).
        private static void WriteUnits()
        {
            if (_unitTable == null) return;
            var effective = Overlay(_unitFile, _lobby, UnitKey);
            _unitTable.Clear();
            foreach (var kv in effective) _unitTable[kv.Key] = kv.Value;
        }

        private static void WriteBuildings()
        {
            if (_buildingTable == null) return;
            var effective = Overlay(_buildingFile, _lobby, BuildingKey);
            _buildingTable.Clear();
            foreach (var kv in effective) _buildingTable[kv.Key] = kv.Value;
        }
    }
}
