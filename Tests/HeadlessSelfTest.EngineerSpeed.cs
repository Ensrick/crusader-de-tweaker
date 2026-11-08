// Tests/HeadlessSelfTest.EngineerSpeed.cs
//
// PURPOSE: Self-test stage for the Nexus report of 2026-11-06 (comment 179239047, "it doesnt work for engineers"): does
//          the Units-file Speed reach a walking Engineer? Runs alone with "-cdt-selftest-only speed"
//          (test_headless.ps1 -Only speed); the runner writes Speed = EngineerSpeed for CHIMP_TYPE_ENGINEER and
//          ArcherSpeed for the Archer.
//
// WHAT IT DOES (editor map, player 1):
//   Spawns an Engineer while the speed table says EngineerSpeed (from the file), then sets the table to 1 and spawns a
//   second one, plus an Archer created at ArcherSpeed as the control (the Archer's update keeps the table value). All
//   three are ordered WalkTiles tiles east. Every frame it samples each unit's speed fields (+0x9A4 r_CurrentSpeed,
//   +0x9A2 r_CurrentSpeed2: the movement step at RVA 0x184203 waits more than +0x9A2 + bonus + terrain ticks),
//   r_AIState (+0x918) and the carry count (+0x9E0), and counts the ticks to arrival. Then two more Engineers created at
//   EngineerSpeed get r_AIState 0x65 / 0x7A written once and are sampled for ForcedTicks (notes, not checks).
//   Measured 2026-11-08 (selftest-20261108-002232): on a move order the Engineer stays in states 0-2 and Speed works
//   (Speed 8: 684 ticks, Speed 1: 152, Archer Speed 8: 684). Forced states (selftest-20261108-002443): both Engineers
//   (created at 6: the table restore goes through SE's SetDefaultSpeed, which clamps 8 to 6) had r_CurrentSpeed 1 within
//   3 samples, then r_CurrentSpeed2 1 for the rest of the 100 ticks (state 0x7A passed through 0x65 first).
//
// WHY (CrusaderDE.dll, game 2.8.2, Ghidra 2026-11-08): c_game_unit_init (RVA 0x19A240) copies the table (RVA 0x3230C0,
//   Engineer 1) into +0x9A2 and +0x9A4. The Engineer's update (vtable entry 30, RVA 0x14AED0) then writes +0x9A4 with
//   constants: 1, or 2 while +0x9E0 != 0, in states 0x65 (RVA 0x14B5C4) and 0x7A (0x14B806); 2 when +0xA82
//   (r_AITribeRole) is 10 under an AI condition (0x14AF4B); state 0x69 copies +0x9A4 into +0x9A2 (0x14B454).
//
// IMPORTANT FOR AI AGENTS:
// - Game record offsets are converted to the Script Extender's struct pointer (unit record + 0x65C), like the other stages.
// - The check states what the Speed setting promises (Speed 8 walks clearly slower than Speed 1). A FAIL here is the
//   reported bug, measured; it is not a broken test.
//
using System;
using System.Collections.Generic;
using System.Linq;
using SHCDESE.API;
using SHCDESE.Interop;

namespace CrusaderDETweaker.Tests
{
    internal sealed partial class HeadlessSelfTest
    {
        // Written into the Units file by scripts/test_headless.ps1 (-Only speed).
        internal const int EngineerSpeed = 8, FastEngineerSpeed = 1;
        private const int EngineerSpeedStage = 200, EngineerWalkLimitTicks = 1500;
        private const int StructToRecord = 0x65C;   // SE GameUnit* = game unit record + 0x65C
        private const int AIStateOffset = 0x918 - StructToRecord, CarryOffset = 0x9E0 - StructToRecord;

        private sealed class Walker
        {
            internal string Name;
            internal int Id, TargetX, TargetY, Ticks = -1, CreatedSpeed;
            internal readonly SortedDictionary<string, int> Seen = new SortedDictionary<string, int>();
        }

        private readonly List<Walker> _walkers = new List<Walker>();
        private int _walkStart;

        private void StartEngineerSpeed(float now)
        {
            var units = GameUnitManagerAPI.Instance;
            int fileSpeed = units.GetDefaultSpeed(eChimps.CHIMP_TYPE_ENGINEER);
            Check("Speed table Engineer", fileSpeed == EngineerSpeed, $"GetDefaultSpeed={fileSpeed} (config {EngineerSpeed}, game 1)");
            Check("Speed table Archer", units.GetDefaultSpeed(eChimps.CHIMP_TYPE_ARCHER) == ArcherSpeed, $"GetDefaultSpeed={units.GetDefaultSpeed(eChimps.CHIMP_TYPE_ARCHER)} (config {ArcherSpeed})");

            AddWalker($"Engineer created at Speed {fileSpeed}", eChimps.CHIMP_TYPE_ENGINEER, 400, 412);
            AddWalker($"Archer created at Speed {ArcherSpeed} (control)", eChimps.CHIMP_TYPE_ARCHER, 400, 428);
            units.SetDefaultSpeed(eChimps.CHIMP_TYPE_ENGINEER, FastEngineerSpeed);
            AddWalker($"Engineer created at Speed {FastEngineerSpeed}", eChimps.CHIMP_TYPE_ENGINEER, 400, 420);
            units.SetDefaultSpeed(eChimps.CHIMP_TYPE_ENGINEER, (ushort)fileSpeed);

            foreach (var w in _walkers) units.MoveToTile(w.Id, w.TargetX, w.TargetY);
            _walkStart = global::Director.instance.getSimTickCount();
            Note($"Walkers ordered {WalkTiles} tiles east at tick {_walkStart}: " + string.Join("; ", _walkers.Select(w => $"{w.Name} = unit {w.Id}, speed at creation {w.CreatedSpeed}")));
            _stage = EngineerSpeedStage; _deadline = now + 600;
            _watchTick = _walkStart; _watchTime = now; _lastProgressNote = now;
        }

        private void AddWalker(string name, eChimps type, int x, int y)
        {
            int id = Spawn(type, x, y);
            var at = GameUnitManagerAPI.Instance.GetCurrentLocalTilePosition(id);
            _walkers.Add(new Walker { Name = name, Id = id, TargetX = at.X + WalkTiles, TargetY = at.Y, CreatedSpeed = GameUnitManagerAPI.Instance.GetSpeed(id) });
        }

        // Forced-state phase: Engineers created at EngineerSpeed get r_AIState 0x65 / 0x7A written once (the two states
        // whose Engineer code writes +0x9A4 with a constant), then are sampled for ForcedTicks.
        private static readonly ushort[] ForcedStates = { 0x65, 0x7A };
        private const int ForcedTicks = 100;
        private readonly List<Walker> _forced = new List<Walker>();
        private int _forcedStart = -1;

        /// <summary>Samples every walker each frame; true when the walk and the forced-state phase are done.</summary>
        private unsafe bool EngineerSpeedTick()
        {
            int tick = global::Director.instance.getSimTickCount();
            if (_forcedStart < 0)
            {
                foreach (var w in _walkers.Where(x => x.Ticks < 0))
                {
                    Sample(w);
                    if (At(w.Id, w.TargetX, w.TargetY)) w.Ticks = tick - _walkStart;
                }
                if (!_walkers.All(w => w.Ticks >= 0) && tick - _walkStart <= EngineerWalkLimitTicks) return false;
                for (int i = 0; i < ForcedStates.Length; i++)
                {
                    var w = new Walker { Name = $"Engineer created at Speed {GameUnitManagerAPI.Instance.GetDefaultSpeed(eChimps.CHIMP_TYPE_ENGINEER)}, r_AIState set to 0x{ForcedStates[i]:X}" };
                    w.Id = Spawn(eChimps.CHIMP_TYPE_ENGINEER, 380, 440 + 6 * i);
                    w.CreatedSpeed = GameUnitManagerAPI.Instance.GetSpeed(w.Id);
                    if (GameUnitManagerAPI.Instance.TryGetUnitById(w.Id, out GameUnit* u)) *(ushort*)((byte*)u + AIStateOffset) = ForcedStates[i];
                    _forced.Add(w);
                }
                _forcedStart = tick;
                Note($"Forced-state phase at tick {tick}: " + string.Join("; ", _forced.Select(w => $"{w.Name} = unit {w.Id}, speed at creation {w.CreatedSpeed}")));
                return false;
            }
            foreach (var w in _forced) Sample(w);
            return tick - _forcedStart >= ForcedTicks;
        }

        private static unsafe void Sample(Walker w)
        {
            if (!GameUnitManagerAPI.Instance.TryGetUnitById(w.Id, out GameUnit* u)) return;
            byte* b = (byte*)u;
            string key = $"speed {u->r_CurrentSpeed}/{u->r_CurrentSpeed2} state 0x{*(ushort*)(b + AIStateOffset):X} carry {*(short*)(b + CarryOffset)}";
            w.Seen.TryGetValue(key, out int n);
            w.Seen[key] = n + 1;
        }

        private void ReportEngineerSpeed()
        {
            foreach (var w in _walkers)
                Note($"{w.Name}: {(w.Ticks >= 0 ? $"arrived after {w.Ticks} ticks" : $"not there after {EngineerWalkLimitTicks} ticks (at {Pos(w.Id)})")}; " +
                     $"r_CurrentSpeed/r_CurrentSpeed2, r_AIState, carry (samples): {string.Join(", ", w.Seen.Select(kv => $"{kv.Key} x{kv.Value}"))}");
            Walker slow = _walkers[0], archer = _walkers[1], fast = _walkers[2];
            Check("Archer control walks at its Speed", archer.Ticks > 0 && fast.Ticks > 0 && archer.Ticks > fast.Ticks * 2.5,
                $"{WalkTiles} tiles: Archer at Speed {ArcherSpeed} {archer.Ticks} ticks, Engineer at Speed {FastEngineerSpeed} {fast.Ticks} ticks");
            Check($"Engineer Speed {EngineerSpeed} walks slower than Speed {FastEngineerSpeed}", slow.Ticks > 0 && fast.Ticks > 0 && slow.Ticks > fast.Ticks * 2.5,
                $"{WalkTiles} tiles: Speed {EngineerSpeed} took {slow.Ticks} ticks, Speed {FastEngineerSpeed} took {fast.Ticks} ticks (ratio {(fast.Ticks > 0 ? (double)slow.Ticks / fast.Ticks : 0):0.00})");
            foreach (var w in _forced)
                Note($"{w.Name}: r_CurrentSpeed/r_CurrentSpeed2, r_AIState, carry over {ForcedTicks} ticks (samples): {string.Join(", ", w.Seen.Select(kv => $"{kv.Key} x{kv.Value}"))}");
            foreach (var w in _walkers.Concat(_forced)) if (w.Id > 0) GameUnitManagerAPI.Instance.DeleteUnitSafe(w.Id);
        }
    }
}
