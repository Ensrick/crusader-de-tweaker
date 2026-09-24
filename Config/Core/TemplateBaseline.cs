// Config/Core/TemplateBaseline.cs
//
// PURPOSE: Remembers the game's own value of every unit/building template cell this mod writes, so
//          ConfigLoader.ReapplyTemplateConfigs can put the tables back before every re-apply (v2.6.8).
//
// HOW:
//   - Every template writer calls TemplateBaseline.BeforeWrite(key, capture) right before it writes.
//   - The FIRST time a key is seen, capture() reads the cell's current value and returns an action
//     that writes it back. The first time is at launch, before this mod touched the cell, so what is
//     remembered is the game's own value. Later writes of the same key keep that first value.
//   - RestoreAll() runs the stored actions newest-first. The entries stay, so restore can run again.
//
// WHY: the template tables are re-applied many times per game (launch, after every SE map-unload reset,
//   before every session start), sometimes with no SE reset in between. SE does not reset every cell
//   (wall-cost multipliers, ballista / run-speed immediates), and the fire / heal / wall-cost multipliers
//   scale the CURRENT value, so a re-apply without a restore would scale them twice. Restore-then-apply
//   makes every re-apply equal the first (tested: TemplateBaselineTest.Reapply_NTimesEqualsOnce).
//   Since 2.7.0 the multiplayer host config sync relies on the same restore: switching between the
//   host's files and the player's own is just a re-apply from another folder (docs/HOST_SYNC_DESIGN.md).
//
// IMPORTANT FOR AI AGENTS:
// - A new template writer (PropertyHandler subclass, matrix loader, init-time multiplier) must report
//   its cell here with the SAME key as any other writer of that cell (see MatrixLoader.BaselineKey and
//   FireAndHealMultipliersConfig), or the second writer would capture an already-modified value.
// - Session state (GameplaySettings: globals, gameplay options, trade prices) is NOT recorded: it is
//   only written in a running session and cannot be restored from the menu.
// - capture() returns null when the value cannot be read; that cell is then not restorable (counted).
// - BaselineJournal is the testable core; TemplateBaseline is the one game-wide instance.
//
using System;
using System.Collections.Generic;

namespace CrusaderDETweaker.Config.Core
{
    /// <summary>First-write-wins journal of restore actions (pure; unit-tested).</summary>
    internal sealed class BaselineJournal
    {
        private readonly Dictionary<string, Action> _restoreByKey = new Dictionary<string, Action>(StringComparer.Ordinal);
        private readonly List<string> _order = new List<string>();
        private readonly HashSet<string> _unreadable = new HashSet<string>(StringComparer.Ordinal);

        /// <summary>Number of cells with a remembered game value.</summary>
        internal int Count => _order.Count;

        /// <summary>Number of written cells whose game value could not be read (not restorable).</summary>
        internal int UnreadableCount => _unreadable.Count;

        internal void BeforeWrite(string key, Func<Action> capture)
        {
            if (key == null || capture == null) return;
            if (_restoreByKey.ContainsKey(key) || _unreadable.Contains(key)) return;

            Action restore = null;
            try
            {
                restore = capture();
            }
            catch (Exception ex)
            {
                Plugin.Logger?.LogDebug($"[TemplateBaseline] Could not read {key}: {ex.Message}");
            }

            if (restore == null)
            {
                _unreadable.Add(key);
                return;
            }

            _restoreByKey[key] = restore;
            _order.Add(key);
        }

        /// <summary>Write every remembered value back, newest first. Returns (restored, failed).</summary>
        internal (int restored, int failed) RestoreAll()
        {
            int restored = 0, failed = 0;
            for (int i = _order.Count - 1; i >= 0; i--)
            {
                try
                {
                    _restoreByKey[_order[i]]();
                    restored++;
                }
                catch (Exception ex)
                {
                    failed++;
                    Plugin.Logger?.LogWarning($"[TemplateBaseline] Restoring {_order[i]} failed: {ex.Message}");
                }
            }
            return (restored, failed);
        }

        /// <summary>
        /// The one template write sequence: restore every remembered value, then run the apply steps in
        /// order. Running it N times leaves the same state as running it once (tested).
        /// </summary>
        internal (int restored, int failed) Reapply(IList<Action> steps)
        {
            var result = RestoreAll();
            foreach (var step in steps) step();
            return result;
        }
    }

    /// <summary>The game-wide baseline every template writer reports to.</summary>
    internal static class TemplateBaseline
    {
        private static readonly BaselineJournal _game = new BaselineJournal();

        internal static int Count => _game.Count;
        internal static int UnreadableCount => _game.UnreadableCount;

        /// <summary>
        /// False only while CoreTestRunner runs its suites: their mock handlers go through the same
        /// PropertyHandler.TryLoad and must not enter the game baseline.
        /// </summary>
        internal static bool Recording = true;

        /// <summary>
        /// Report a template cell about to be written. On the first report of <paramref name="key"/>,
        /// <paramref name="capture"/> reads the current value and returns the action restoring it.
        /// </summary>
        internal static void BeforeWrite(string key, Func<Action> capture)
        {
            if (Recording) _game.BeforeWrite(key, capture);
        }

        /// <summary>Write every remembered game value back, newest first.</summary>
        internal static (int restored, int failed) RestoreAll() => _game.RestoreAll();

        /// <summary>Restore, then run the apply steps (used only by ConfigLoader.ReapplyTemplateConfigs).</summary>
        internal static (int restored, int failed) Reapply(IList<Action> steps) => _game.Reapply(steps);
    }
}
