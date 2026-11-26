// Config/Core/AutoTrade.cs
//
// PURPOSE: GameplaySettings ["Auto Trade".<good>] (2.11.4, GitLab #11, plan finding FWP-E-02).
//
// RULES:
//   - Each key is "no override" by default: Enabled = -1 leaves the good's in-game auto-trade alone (the whole entry
//     is skipped); BuyLevel / SellLevel = -1 keep the in-game level. Only an entry with Enabled = true / false is
//     written. Before 2.11.4 every generated entry said Enabled = false, BuyLevel = 0, SellLevel = 0 and was written
//     at every session start, save load and Marketplace spawn, so auto-trade set in the market was switched off.
//     The generator migrates exactly that old default (false / 0 / 0) to -1 / -1 / -1; any other value is kept.
//   - Single player only. In a multiplayer match nothing is written: a write here changes only this PC's game (the
//     market's own buttons send game commands to everyone), so it would desync. The log says so once per session.
//   - Written by this class, not SE's GamePlayerManagerAPI.SetAutoTrade (SE 2.14.6): that call has no player
//     argument (it writes index eGoods.Count + good, the slot of player 1) and stores the one-byte Enabled flag as
//     16 bits, which also clears the next good's flag. Here: the same three tables SE uses (player manager +0x22D9B5
//     bytes, +0x22DA96 / +0x22DC58 16-bit levels), index player * eGoods.Count + good for the LOCAL player
//     (player 1 gives SE's index; the per-player stride is read from SE's offsets [unverified for other players]),
//     the flag as one byte.
//
// IMPORTANT FOR AI AGENTS:
//   - Parse / Generated* are pure (Tests/EconomyTest.cs). Keep game access in Apply / Write only.
//   - Session state (GameplaySettings): applied from ConfigLoader's session Post hooks and the local Marketplace
//     spawn, never during LibraryLoaded.
//
using System;
using System.Collections.Generic;
using System.Globalization;
using SHCDESE.API;
using SHCDESE.Interop;
using Tomlyn.Model;

namespace CrusaderDETweaker.Config.Core
{
    internal static class AutoTrade
    {
        internal const string Section = "Auto Trade";
        private const string Tag = "[Auto Trade]";

        private const int EnabledOffset = 0x22D9B5;
        private const int BuyOffset = 0x22DA96;
        private const int SellOffset = 0x22DC58;
        private const int MaxPlayer = 8;

        /// <summary>One configured good: Enabled always set; Buy / Sell null = keep the in-game level.</summary>
        internal struct Entry
        {
            public eGoods Good;
            public bool Enabled;
            public ushort? BuyLevel;
            public ushort? SellLevel;
        }

        // ------------------------------------------------------------------
        // Pure parts
        // ------------------------------------------------------------------

        /// <summary>
        /// The entries the file overrides. A good with Enabled = -1 (or no Enabled) is not listed; a level of -1 (or
        /// none) is null. Problems (unknown good, bad type, out of range) are reported and that key or entry skipped.
        /// </summary>
        internal static List<Entry> Parse(TomlTable section, Action<string> warn)
        {
            var result = new List<Entry>();
            if (section == null) return result;
            foreach (var kv in section)
            {
                if (!(kv.Value is TomlTable good)) continue;
                if (!Enum.TryParse(kv.Key, out eGoods goodType) || !kv.Key.StartsWith("STORED_", StringComparison.Ordinal))
                {
                    warn?.Invoke($"{Tag} Unknown goods type: '{kv.Key}'");
                    continue;
                }
                if (!good.TryGetValue("Enabled", out object enabledRaw)) continue;
                if (!(enabledRaw is bool enabled))
                {
                    if (!(enabledRaw is long l && l == -1))
                        warn?.Invoke($"{Tag} {kv.Key}.Enabled = {Text(enabledRaw)} is not true, false or -1; the good is left alone.");
                    continue;
                }
                result.Add(new Entry
                {
                    Good = goodType,
                    Enabled = enabled,
                    BuyLevel = Level(good, "BuyLevel", kv.Key, warn),
                    SellLevel = Level(good, "SellLevel", kv.Key, warn),
                });
            }
            return result;
        }

        private static ushort? Level(TomlTable good, string key, string goodName, Action<string> warn)
        {
            if (!good.TryGetValue(key, out object raw)) return null;
            if (raw is long l)
            {
                if (l == -1) return null;
                if (l >= 0 && l <= ushort.MaxValue) return (ushort)l;
            }
            warn?.Invoke($"{Tag} {goodName}.{key} = {Text(raw)} is not -1 or a whole number from 0 to {ushort.MaxValue}; the in-game level is kept.");
            return null;
        }

        /// <summary>
        /// The three generated values for a good, from the existing file (null = none): -1 for a new entry and for the
        /// pre-2.11.4 default false / 0 / 0; anything else is kept as written.
        /// </summary>
        internal static (string enabled, string buy, string sell) GeneratedValues(object enabledRaw, object buyRaw, object sellRaw)
        {
            bool oldDefault = enabledRaw is bool e && !e && buyRaw is long b && b == 0 && sellRaw is long s && s == 0;
            if (enabledRaw == null || oldDefault) return ("-1", "-1", "-1");
            return (Format(enabledRaw), buyRaw == null ? "-1" : Format(buyRaw), sellRaw == null ? "-1" : Format(sellRaw));
        }

        private static string Format(object raw)
        {
            switch (raw)
            {
                case bool b: return b ? "true" : "false";
                case long l: return l.ToString(CultureInfo.InvariantCulture);
                case double d: return d.ToString("R", CultureInfo.InvariantCulture);
                case string s: return "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
                default: return "-1";
            }
        }

        private static string Text(object raw) => raw == null ? "nothing" : Format(raw);

        // ------------------------------------------------------------------
        // Game
        // ------------------------------------------------------------------

        private static bool _multiplayerNoted;

        /// <summary>True in a multiplayer lobby or match (fails toward true: never write when unsure).</summary>
        internal static bool InMultiplayer()
        {
            try
            {
                return GameNetworkAPI.IsMultiplayerGame() || GameNetworkAPI.IsNetworkedEnvironment();
            }
            catch
            {
                return true;
            }
        }

        /// <summary>Called at map unload: the multiplayer note is logged once per session.</summary>
        internal static void ResetSessionNote() => _multiplayerNoted = false;

        /// <summary>Write the configured goods for the local player (single player only).</summary>
        internal static void Apply(TomlTable globals, string reason)
        {
            var section = globals != null && globals.TryGetValue(Section, out object obj) ? obj as TomlTable : null;
            var entries = Parse(section, msg => Plugin.Logger.LogWarning(msg));
            if (entries.Count == 0) return;

            if (InMultiplayer())
            {
                if (!_multiplayerNoted)
                {
                    _multiplayerNoted = true;
                    Plugin.Logger.LogInfo($"{Tag} {entries.Count} auto-trade setting(s) from the GameplaySettings file are not applied in multiplayer ({reason}): " +
                                          "the file would change this PC's game only. Set auto-trade in the market.");
                }
                return;
            }

            int player = Plugin.PlayerApi?.GetLocalPlayerId() ?? -1;
            if (player < 1 || player > MaxPlayer)
            {
                Plugin.Logger.LogWarning($"{Tag} Not applied ({reason}): local player id {player} is not 1 to {MaxPlayer}.");
                return;
            }

            try
            {
                foreach (var entry in entries) Write(player, entry);
                Plugin.Logger.LogInfo($"{Tag} Applied {entries.Count} auto-trade setting(s) for player {player} ({reason}).");
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogWarning($"{Tag} Apply failed ({reason}): {ex.Message}");
            }
        }

        private static unsafe void Write(int player, Entry entry)
        {
            IntPtr manager = Plugin.PlayerApi.GetPlayerManager();
            if (manager == IntPtr.Zero) throw new InvalidOperationException("no player manager");
            byte* pm = (byte*)manager.ToPointer();
            int index = player * (int)eGoods.Count + (int)entry.Good;
            *(pm + EnabledOffset + index) = (byte)(entry.Enabled ? 1 : 0);
            if (entry.BuyLevel.HasValue) *(ushort*)(pm + BuyOffset + index * 2) = entry.BuyLevel.Value;
            if (entry.SellLevel.HasValue) *(ushort*)(pm + SellOffset + index * 2) = entry.SellLevel.Value;
        }
    }
}
