// Config/Core/TeamColors.cs
//
// PURPOSE: The ["Team Colors"] section of the GameplaySettings file (v2.9.0): one RGB value per player
//          colour slot (Red, Blue, Orange, Yellow, Purple, Grey, LightBlue, Green), applied to
//          - the sprite palettes the game's team-colour shader multiplies over unit cloth, lords and
//            flags (spriteLoader.defaultColours / lordLadyColours / jesterColours and every other palette
//            in spriteLoader.gmColors that holds the game's colour for that slot), and
//          - the interface colour tables (OnScreenText.MPTeamColours: player names, allies / merit /
//            score / ping panels; HUD_MPChatMessages.MPTeamColours: chat).
//
// HOW (decompiled Assembly-CSharp, game 2.8.x):
//   - Colour slot 1..8 is what a player picked in the lobby; SpriteMapping.remapColours maps player -> slot.
//     Slot 0 (neutral) and 9 are not touched.
//   - SpriteMapping.SetBodySprite -> spriteLoader.GetGMMaterial reads gmColors[file][slot] on every sprite
//     update, and gmColors[file] holds a REFERENCE to one of the named palette arrays, so writing the array
//     elements in place recolours everything that uses that palette, live. The knight-horse, animal and
//     foliage palettes hold other colours in those slots and are left alone (match rule below).
//   - The UI tables are read whenever a brush is built (chat line, panel refresh), so they also apply live.
//   - The minimap (native, DLL_SetMPRadarColours) and the lobby's colour shield images are not reachable.
//
// RULES:
//   - Restore-then-apply through a private BaselineJournal (first write remembers the game's value), so
//     applying N times equals applying once and a key set back to -1 really returns the game's colour,
//     also after the lobby host's file stops being used (host config sync).
//   - A palette entry is recoloured only while it still holds the game's default-palette colour for that
//     slot (checked after the restore), so palettes with deliberately different colours (jester yellow,
//     knight horses) and mod atlases with their own palette keep them.
//   - Only managed arrays are written; no native memory and no Unity API call, so it is safe from any
//     hook and any thread. Unity objects are null-checked with (object) casts (no native liveness check).
//
// IMPORTANT FOR AI AGENTS:
// - The parse / apply core (TryParse, ApplyToPalettes, ApplyToTable) is pure and covered by
//   Tests/TeamColorsTest.cs; keep game access in ApplyToGame.
// - The "# default:" comments come from Slots (decompiled constants); the apply path never uses them.
//
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using Tomlyn.Model;

namespace CrusaderDETweaker.Config.Core
{
    /// <summary>An 8-bit RGB colour as written in the config.</summary>
    internal struct Rgb
    {
        internal readonly byte R, G, B;

        internal Rgb(byte r, byte g, byte b) { R = r; G = g; B = b; }

        public override string ToString() => $"[{R}, {G}, {B}]";
    }

    /// <summary>One player colour slot: its config key, game index and the game's colours (for comments).</summary>
    internal sealed class TeamColorSlot
    {
        internal readonly string Key;
        internal readonly int Index;
        internal readonly Rgb UnitDefault;
        internal readonly Rgb InterfaceDefault;

        internal TeamColorSlot(string key, int index, Rgb unitDefault, Rgb interfaceDefault)
        {
            Key = key; Index = index; UnitDefault = unitDefault; InterfaceDefault = interfaceDefault;
        }
    }

    internal static class TeamColors
    {
        internal const string Section = "Team Colors";

        /// <summary>
        /// Slots 1..8 in game order. Unit defaults = spriteLoader.defaultColours x 255 (rounded),
        /// interface defaults = HUD_MPChatMessages.MPTeamColours (decompiled, game 2.8.x).
        /// </summary>
        internal static readonly TeamColorSlot[] Slots =
        {
            new TeamColorSlot("Red",       1, new Rgb(204,  28,  56), new Rgb(196,   2,   2)),
            new TeamColorSlot("Blue",      2, new Rgb( 15, 146, 212), new Rgb( 70,  70, 200)),
            new TeamColorSlot("Orange",    3, new Rgb(241, 122,  31), new Rgb(200,  97,   6)),
            new TeamColorSlot("Yellow",    4, new Rgb(255, 210,  35), new Rgb(198, 195,   0)),
            new TeamColorSlot("Purple",    5, new Rgb(167,  40, 195), new Rgb(144,   0, 144)),
            new TeamColorSlot("Grey",      6, new Rgb(111, 111, 111), new Rgb(128, 128, 128)),
            new TeamColorSlot("LightBlue", 7, new Rgb( 70, 253, 251), new Rgb(  9, 193, 191)),
            new TeamColorSlot("Green",     8, new Rgb( 77, 212,  33), new Rgb(  2, 200,   2)),
        };

        private static readonly BaselineJournal _journal = new BaselineJournal();

        // ===================================================
        // Parsing (pure)
        // ===================================================

        /// <summary>
        /// Parse one config value. Returns false when the value is unusable (problem says why).
        /// On success, value == null means "keep the game's colour" (-1 or ""); problem may still carry a
        /// clamp warning. Accepted: [R, G, B] with 0-255 numbers, "#RRGGBB" / "RRGGBB", -1, "".
        /// </summary>
        internal static bool TryParse(object raw, out Rgb? value, out string problem)
        {
            value = null;
            problem = null;

            switch (raw)
            {
                case long l when l == -1:
                    return true;
                case long l:
                    problem = $"{l} is not a colour; use [R, G, B] (0-255 each), \"#RRGGBB\" or -1";
                    return false;
                case string s:
                {
                    string hex = s.Trim();
                    if (hex.Length == 0) return true;
                    if (hex.StartsWith("#")) hex = hex.Substring(1);
                    if (hex.Length == 6
                        && int.TryParse(hex, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out int rgb))
                    {
                        value = new Rgb((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb);
                        return true;
                    }
                    problem = $"\"{s}\" is not a hex colour; use \"#RRGGBB\" (e.g. \"#FF0000\") or [R, G, B]";
                    return false;
                }
                case TomlArray array:
                {
                    if (array.Count != 3)
                    {
                        problem = $"needs exactly 3 numbers [R, G, B], found {array.Count}";
                        return false;
                    }
                    var channels = new byte[3];
                    var clamped = new List<string>();
                    for (int i = 0; i < 3; i++)
                    {
                        double d;
                        if (array[i] is long li) d = li;
                        else if (array[i] is double di) d = di;
                        else
                        {
                            problem = $"[R, G, B] must hold numbers, found '{array[i]}'";
                            return false;
                        }
                        double c = Math.Round(d);
                        if (c < 0 || c > 255)
                        {
                            clamped.Add(d.ToString(CultureInfo.InvariantCulture));
                            c = c < 0 ? 0 : 255;
                        }
                        channels[i] = (byte)c;
                    }
                    value = new Rgb(channels[0], channels[1], channels[2]);
                    if (clamped.Count > 0)
                        problem = $"{string.Join(", ", clamped)} outside 0-255, clamped to {value.Value}";
                    return true;
                }
                default:
                    problem = $"'{raw}' is not a colour; use [R, G, B] (0-255 each), \"#RRGGBB\" or -1";
                    return false;
            }
        }

        /// <summary>
        /// The TOML text for an existing value during file migration: the user's value as written
        /// (array, string or number), or "-1" when there is none / it is of another type.
        /// Invalid values are kept so the edit stays visible; the loader warns about them.
        /// </summary>
        internal static string FormatForToml(object raw)
        {
            switch (raw)
            {
                case long l:
                    return l.ToString(CultureInfo.InvariantCulture);
                case string s:
                    return "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
                case TomlArray array:
                {
                    var parts = new List<string>();
                    foreach (var item in array)
                    {
                        if (item is long li) parts.Add(li.ToString(CultureInfo.InvariantCulture));
                        else if (item is double di) parts.Add(di.ToString("R", CultureInfo.InvariantCulture));
                        else return "-1";
                    }
                    return "[" + string.Join(", ", parts) + "]";
                }
                default:
                    return "-1";
            }
        }

        /// <summary>Read the section into slot index -> colour, logging unusable values. Null section = none.</summary>
        internal static Dictionary<int, Rgb> ReadOverrides(TomlTable section, Action<string> warn)
        {
            var overrides = new Dictionary<int, Rgb>();
            if (section == null) return overrides;

            foreach (var slot in Slots)
            {
                if (!section.TryGetValue(slot.Key, out var raw)) continue;
                bool ok = TryParse(raw, out var value, out var problem);
                if (problem != null)
                    warn($"[TeamColors] {slot.Key}: {problem}{(ok ? "" : "; keeping the game's colour")}.");
                if (ok && value.HasValue) overrides[slot.Index] = value.Value;
            }
            foreach (var key in section.Keys)
            {
                if (Array.Find(Slots, s => s.Key == key) == null)
                    warn($"[TeamColors] Unknown key '{key}' ignored; the keys are {string.Join(", ", Array.ConvertAll(Slots, s => s.Key))}.");
            }
            return overrides;
        }

        // ===================================================
        // Apply (pure over arrays)
        // ===================================================

        /// <summary>
        /// Recolour every palette entry that holds the reference palette's colour for an overridden slot.
        /// <paramref name="reference"/> must hold the game's colours (call after the journal restore).
        /// Returns the number of entries written.
        /// </summary>
        internal static int ApplyToPalettes(BaselineJournal journal, IList<UnityEngine.Color[]> palettes,
            UnityEngine.Color[] reference, IDictionary<int, Rgb> overrides)
        {
            int written = 0;
            var refCopy = (UnityEngine.Color[])reference.Clone();
            foreach (var kvp in overrides)
            {
                int index = kvp.Key;
                if (index >= refCopy.Length) continue;
                foreach (var palette in palettes)
                {
                    if (palette == null || index >= palette.Length || !SameColor(palette[index], refCopy[index])) continue;
                    RecordOriginal(journal, palette, index);
                    var c = palette[index];
                    palette[index] = new UnityEngine.Color(kvp.Value.R / 255f, kvp.Value.G / 255f, kvp.Value.B / 255f, c.a);
                    written++;
                }
            }
            return written;
        }

        /// <summary>Recolour the overridden slots of one interface colour table. Returns the entries written.</summary>
        internal static int ApplyToTable<T>(BaselineJournal journal, T[] table, IDictionary<int, Rgb> overrides, Func<T, Rgb, T> recolour)
        {
            if (table == null) return 0;
            int written = 0;
            foreach (var kvp in overrides)
            {
                if (kvp.Key >= table.Length) continue;
                RecordOriginal(journal, table, kvp.Key);
                table[kvp.Key] = recolour(table[kvp.Key], kvp.Value);
                written++;
            }
            return written;
        }

        private static bool SameColor(UnityEngine.Color a, UnityEngine.Color b) =>
            Math.Abs(a.r - b.r) < 1e-4f && Math.Abs(a.g - b.g) < 1e-4f && Math.Abs(a.b - b.b) < 1e-4f;

        private static void RecordOriginal<T>(BaselineJournal journal, T[] array, int index)
        {
            // One key per (array instance, index): the same array can sit in many gmColors slots.
            string key = $"{typeof(T).Name}@{ArrayId(array)}[{index}]";
            journal.BeforeWrite(key, () =>
            {
                T original = array[index];
                return () => array[index] = original;
            });
        }

        // Stable ids for array instances (RuntimeHelpers hash codes can collide).
        private static readonly ConditionalWeakTable<object, object> _ids = new ConditionalWeakTable<object, object>();
        private static int _nextId;
        private static int ArrayId(object array) => (int)_ids.GetValue(array, _ => ++_nextId);

        // ===================================================
        // Game binding
        // ===================================================

        private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
        private static readonly string[] NamedPalettes = { "defaultColours", "lordLadyColours", "jesterColours" };

        /// <summary>
        /// Put back every colour this mod changed, then apply <paramref name="section"/> (null = none).
        /// Safe to call at any time; a game object that does not exist yet is skipped and logged.
        /// </summary>
        internal static void ApplyToGame(TomlTable section, string reason, bool quiet)
        {
            var overrides = ReadOverrides(section, quiet ? (Action<string>)(_ => { }) : msg => Plugin.Logger.LogWarning(msg));
            var (restored, failed) = _journal.RestoreAll();
            if (overrides.Count == 0)
            {
                if (restored > 0 && !quiet)
                    Plugin.Logger.LogInfo($"[TeamColors] No colour set ({reason}): {restored} entries back to the game's colours.");
                return;
            }

            int unitWrites = 0, uiWrites = 0;
            var missing = new List<string>();

            var loader = global::spriteLoader.instance;
            if ((object)loader == null)
            {
                missing.Add("unit sprites (not loaded yet)");
            }
            else
            {
                var palettes = CollectPalettes(loader, out var reference);
                if (reference == null) missing.Add("unit sprites (palette not found)");
                else unitWrites = ApplyToPalettes(_journal, palettes, reference, overrides);
            }

            var screenText = global::OnScreenText.Instance;
            if (screenText == null) missing.Add("player names / panels (not created yet)");
            else uiWrites += ApplyToTable(_journal, screenText.MPTeamColours, overrides,
                (c, v) => new UnityEngine.Color(v.R / 255f, v.G / 255f, v.B / 255f, c.a));

            uiWrites += ApplyToTable(_journal, global::CrusaderDE.HUD_MPChatMessages.MPTeamColours, overrides,
                (c, v) => Noesis.Color.FromArgb(c.A, v.R, v.G, v.B));

            if (quiet) return;
            var applied = new StringBuilder();
            foreach (var slot in Slots)
                if (overrides.TryGetValue(slot.Index, out var v)) applied.Append(applied.Length > 0 ? ", " : "").Append($"{slot.Key}={v}");
            Plugin.Logger.LogInfo($"[TeamColors] Applied ({reason}): {applied}; {unitWrites} sprite palette entries, {uiWrites} interface entries"
                + (failed > 0 ? $", {failed} restores FAILED" : "")
                + (missing.Count > 0 ? $". Not reached yet: {string.Join(", ", missing)} - applied again at the next session start." : "."));
        }

        /// <summary>Every distinct sprite palette: the named ones plus every array in gmColors.</summary>
        private static List<UnityEngine.Color[]> CollectPalettes(global::spriteLoader loader, out UnityEngine.Color[] reference)
        {
            var type = typeof(global::spriteLoader);
            var result = new List<UnityEngine.Color[]>();
            var seen = new HashSet<int>();
            void Add(UnityEngine.Color[] p)
            {
                if (p != null && seen.Add(ArrayId(p))) result.Add(p);
            }

            reference = type.GetField("defaultColours", Instance)?.GetValue(loader) as UnityEngine.Color[];
            foreach (var name in NamedPalettes)
                Add(type.GetField(name, Instance)?.GetValue(loader) as UnityEngine.Color[]);
            if (type.GetField("gmColors", Instance)?.GetValue(loader) is UnityEngine.Color[][] gm)
                foreach (var p in gm) Add(p);
            return result;
        }
    }
}
