// Config/Core/UnitLimit.cs
//
// PURPOSE: The ["Army Size"] UnitLimit setting of the GameplaySettings file (GitHub #3): the game's total
//          unit pool, i.e. how many unit slots exist for everyone (troops, workers, animals). The per-player
//          troop limits are derived from it every tick, so raising the pool raises them too.
//
// GAME FACTS (CrusaderDE.dll, game 2.8.2; RE report 2026-10-08):
//   - The pool is the int at SE's GameGlobalsManager.LocalPlayerUnitLimitVA (RVA 0x3668E34, address only, no
//     SE API). The unit slot allocator 0x17FEF0 searches slots 1..pool-1 and fails when none is free
//     (0x17FF1F-0x17FF25 -> 0x180015). No other code iterates units up to the pool, so lowering it below the
//     slots in use only blocks new units (readers listed by an xref scan of the RVA).
//   - Game value: 3000, set at 0x87D3F by 0x87D10, which every DLL_PreInitMap_* and DLL_LoadSaveGame call.
//     Extreme troops sets 10000 (DLL_ApplyMultiplayerSetupData 0x26B7E, map load 0x96E09 / 0x8B0FF). A save
//     stores the value (written 0x593C, read back 0x9141).
//   - Every tick 0xCBD90 recomputes the per-player limits from the pool: humans 0x37EF954, AI 0x37EF950.
//     Mode 0 (campaign, editor, invasion, free build): both = pool. Mode 0x63 (skirmish, trails) and online
//     multiplayer: (pool - 100) / players - 40, with a coop case (0xCBE16-0xCBE5B). Only the local player's
//     recruiting checks the human limit (barracks 0xD69FB, engineers / monks 0xD6066, tunnelers 0xD6868,
//     mercenaries 0xD6B81, Bedouin 0xD6AF2); the AI only meets the shared pool outside online multiplayer.
//   - The hard ceiling is 10000: the unit table holds 10000 records (SE GameUnitManager.NativeUnitSlotCount).
//   - Side effects of a pool above 3000 (the game's own Extreme troops test): DLL_GetMultiplayerSetupData
//     reports Extreme troops (0x20873), the per-player counter at player +0x3950 advances every tick instead of
//     every third (0xCDD97; probably the Extreme power bar [unverified]), outposts take their Extreme branch
//     (0xAC4F4), and the skirmish start handler 0x94350 forces 10000 if it sees > 3000 (0x948A9). The value is
//     also hashed at 0x2257B (probably the multiplayer sync check [unverified]).
//
// RULES:
//   - Written only from the session Post hooks (OnStartMap / OnLoadMap / OnLoadSave Post, OnPostLoad), never
//     in a Pre phase: 0x94350 would read a raised pool as Extreme troops and set 10000 itself.
//   - The game's value is remembered before the first write of a session (and again whenever the game set its
//     own value since); -1 and every map unload put it back, only while the pool still holds our value.
//   - 0x3668E38 (3000, 6000 with Extreme) caps another pool [unverified]; it is left alone.
//
// IMPORTANT FOR AI AGENTS:
// - Keep writes single aligned 32-bit stores (the simulation thread reads the pool every tick).
// - Tests/HeadlessSelfTest.cs measures this in game (read-back, per-player limit, spawning past a small pool).
//
using System;
using Tomlyn.Model;

namespace CrusaderDETweaker.Config.Core
{
    internal static class UnitLimit
    {
        internal const string Section = "Army Size";
        internal const string Key = "UnitLimit";
        internal const int GameValue = 3000, ExtremeTroopsValue = 10000;
        internal const int Minimum = 1000, Maximum = 10000;

        private static bool _written;
        private static int _writtenValue, _gameValue;

        /// <summary>The config value: -1 = game value; null when the key is missing or not a number.</summary>
        internal static long? ReadConfigured(TomlTable model)
        {
            if (model == null || !model.TryGetValue(Section, out var sectionObj) || !(sectionObj is TomlTable section)) return null;
            if (!section.TryGetValue(Key, out var raw)) return null;
            if (raw is long value) return value;
            Plugin.Logger.LogWarning($"[UnitLimit] {Key} = {raw} is not a whole number; the game value is used.");
            return null;
        }

        /// <summary>Applies the ["Army Size"] value from a parsed GameplaySettings file (missing = game value).</summary>
        internal static void ApplyFromConfig(TomlTable model, string reason) => Apply(ReadConfigured(model) ?? -1, reason);

        /// <summary>Writes the configured pool (clamped to Minimum..Maximum), or puts the game's value back for a negative value.</summary>
        internal static void Apply(long configured, string reason)
        {
            if (!TryRead(out int current))
            {
                if (configured >= 0) Plugin.Logger.LogWarning($"[UnitLimit] Not applied ({reason}): the Script Extender did not find the game's unit limit.");
                return;
            }
            // The game set its own value since our last write (new map, save load): that is the value to restore.
            if (!_written || current != _writtenValue) { _gameValue = current; _written = false; }

            if (configured < 0)
            {
                RestoreGameValue(reason);
                return;
            }

            int target = (int)Math.Max(Minimum, Math.Min(Maximum, configured));
            if (target != configured)
                Plugin.Logger.LogWarning($"[UnitLimit] {Key} = {configured} is outside {Minimum}-{Maximum}; using {target}.");
            if (current == target && _written) return;

            TryWrite(target);
            TryRead(out int readBack);
            _written = readBack == target;
            _writtenValue = target;
            if (_written)
                Plugin.Logger.LogInfo($"[UnitLimit] Unit limit {target} (game {_gameValue}) ({reason}); read back {readBack}.{(target > GameValue ? " Above 3000 the game also applies some Extreme troops rules." : "")}");
            else
                Plugin.Logger.LogWarning($"[UnitLimit] Wrote {target} ({reason}) but read back {readBack}: not applied.");
        }

        /// <summary>Puts the game's value back if the pool still holds the value this mod wrote (map unload, -1).</summary>
        internal static void RestoreGameValue(string reason)
        {
            if (!_written) return;
            _written = false;
            if (!TryRead(out int current) || current != _writtenValue) return;
            TryWrite(_gameValue);
            TryRead(out int readBack);
            Plugin.Logger.LogInfo($"[UnitLimit] Game unit limit {_gameValue} restored ({reason}); read back {readBack}.");
        }

        internal static unsafe bool TryRead(out int value)
        {
            value = 0;
            ulong va = Plugin.GlobalsApi?.LocalPlayerUnitLimitVA ?? 0;
            if (va == 0) return false;
            value = *(int*)va;
            return true;
        }

        internal static unsafe bool TryWrite(int value)
        {
            ulong va = Plugin.GlobalsApi?.LocalPlayerUnitLimitVA ?? 0;
            if (va == 0) return false;
            *(int*)va = value;
            return true;
        }
    }
}
