// Config/BepInEx/Systems/StructureMultipliersConfig.cs
using System;
using System.Linq;
using BepInEx.Configuration;
using CrusaderDETweaker.Config.BepInEx.Core;
using CrusaderDETweaker.Data;
using R3;
using SHCDESE.API;
using SHCDESE.EventAPI;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using UnityEngine;

namespace CrusaderDETweaker.Config.BepInEx.Systems
{
    /// <summary>
    /// Manages structure-related damage multipliers (global, wall, tower, civil structures).
    /// Uses real-time event hooks to apply multipliers as structures take damage.
    /// </summary>
    internal class StructureMultipliersConfig : IBepInExConfigSystem
    {
        public string Name => "Structure Multipliers";

        public bool IsInitialized { get; private set; }

        public ConfigEntry<float> GlobalDamageTakenMultiplier { get; private set; }
        public ConfigEntry<float> WallDamageTakenMultiplier { get; private set; }
        public ConfigEntry<float> TowerDamageTakenMultiplier { get; private set; }
        public ConfigEntry<float> CivilStructureDamageTakenMultiplier { get; private set; }

        public void Initialize(ConfigFile config)
        {
            GlobalDamageTakenMultiplier = config.Bind(
                "Multipliers",
                "StructureDamageTakenMultiplier",
                1.0f,
                "Global multiplier for damage taken by all structures"
            );

            WallDamageTakenMultiplier = config.Bind(
                "Multipliers",
                "WallDamageTakenMultiplier",
                1.0f,
                "Multiplier for damage taken by walls (stone, crenel walls)"
            );

            TowerDamageTakenMultiplier = config.Bind(
                "Multipliers",
                "TowerDamageTakenMultiplier",
                1.0f,
                "Multiplier for damage taken by towers (tower levels 1-5)"
            );

            CivilStructureDamageTakenMultiplier = config.Bind(
                "Multipliers",
                "CivilStructureDamageTakenMultiplier",
                1.0f,
                "Multiplier for damage taken by civilian structures (non-towers, non-gatehouses)"
            );

            ValidateMultipliers();
            IsInitialized = true;
        }

        public void Apply()
        {
            ApplyStructureDamageMultipliers();
        }

        /// <summary>
        /// Validates all structure multiplier values and logs warnings for invalid values.
        /// </summary>
        private void ValidateMultipliers()
        {
            ValidateMultiplier("StructureDamageTakenMultiplier", GlobalDamageTakenMultiplier.Value);
            ValidateMultiplier("WallDamageTakenMultiplier", WallDamageTakenMultiplier.Value);
            ValidateMultiplier("TowerDamageTakenMultiplier", TowerDamageTakenMultiplier.Value);
            ValidateMultiplier("CivilStructureDamageTakenMultiplier", CivilStructureDamageTakenMultiplier.Value);
        }

        /// <summary>
        /// Validates a single multiplier value and logs a warning if invalid.
        /// </summary>
        private void ValidateMultiplier(string name, float value)
        {
            if (value < 0.0f)
            {
                Plugin.Logger.LogWarning($"{name} is negative ({value}). Negative multipliers may cause unexpected behavior. Consider using a positive value.");
            }
            else if (float.IsNaN(value) || float.IsInfinity(value))
            {
                Plugin.Logger.LogError($"{name} is invalid ({value}). Using default value of 1.0.");
            }
        }

        /// <summary>
        /// Subscribes to building tile damage event hook to apply structure damage multipliers in real-time.
        /// </summary>
        private void ApplyStructureDamageMultipliers()
        {
            Plugin.Logger.LogInfo("Subscribing to OnBuildingTileTakeDamage event hook...");
            BuildingR3EventHooks.OnBuildingTileTakeDamage.Observable
                .Where(args => args.Phase == EventHookPhase.Pre)
                .Subscribe(args =>
                {
                    try
                    {
                        // Log EVERY hook call to see if it's firing at all
                        Plugin.Logger.LogInfo($"[WALL DEBUG] OnBuildingTileTakeDamage hook FIRED: TileId={args.TileId}, TileX={args.TileX}, TileY={args.TileY}, Damage={args.Damage}");
                        
                        // Get building ID from tile ID
                        ushort buildingId = GameTileManagerAPI.Instance.GetTileBuildingId(args.TileId);
                        Plugin.Logger.LogInfo($"[WALL DEBUG] GetTileBuildingId returned: {buildingId} for TileId={args.TileId}");
                        
                        eStructs structureType = default(eStructs);
                        bool isWall = false;
                        bool isTower = false;
                        bool isCivilStructure = false;

                        // Walls don't have building IDs - they're identified by tile property flags
                        if (buildingId == 0)
                        {
                            // Check if this is a wall tile using tile property flags
                            TilePropertyFlag tileFlags = GameTileManagerAPI.Instance.GetTilePropertyFlag(args.TileId);
                            Plugin.Logger.LogInfo($"[WALL DEBUG] Tile property flags: {tileFlags} (0x{((int)tileFlags):X8})");
                            
                            // Check if this tile is a wall
                            if ((tileFlags & TilePropertyFlag.IsWall) == TilePropertyFlag.IsWall)
                            {
                                isWall = true;
                                // Determine wall type based on crenelation flags
                                bool hasCrenelation = (tileFlags & TilePropertyFlag.CrenelationComponent) == TilePropertyFlag.CrenelationComponent &&
                                                      (tileFlags & TilePropertyFlag.CrenelationModifier) == TilePropertyFlag.CrenelationModifier;
                                
                                if (hasCrenelation)
                                {
                                    structureType = eStructs.STRUCT_CRENAL_WALL;
                                    Plugin.Logger.LogInfo($"[WALL DEBUG] Detected CRENEL WALL from tile flags");
                                }
                                else
                                {
                                    structureType = eStructs.STRUCT_STONE_WALL;
                                    Plugin.Logger.LogInfo($"[WALL DEBUG] Detected STONE WALL from tile flags");
                                }
                            }
                            else
                            {
                                // Not a wall and no building ID - skip this tile
                                Plugin.Logger.LogInfo($"[WALL DEBUG] No building on tile {args.TileId} and not a wall, returning early");
                                return;
                            }
                        }
                        else
                        {
                            // Regular building - get structure type from building API
                            structureType = Plugin.BuildingApi.GetType(buildingId);
                            Plugin.Logger.LogInfo($"[WALL DEBUG] BuildingId={buildingId} has StructureType={structureType}");

                            // Check for walls, towers, and civil structures
                            isWall = StructureCategories.IsWall(structureType);
                            isTower = StructureCategories.IsTower(structureType);
                            isCivilStructure = StructureCategories.IsCivilStructure(structureType);

                            Plugin.Logger.LogInfo($"[WALL DEBUG] Structure detection: IsWall={isWall}, IsTower={isTower}, IsCivil={isCivilStructure}, OriginalDamage={args.Damage}");
                            Plugin.Logger.LogInfo($"[WALL DEBUG] Checking NonModable: Contains={StructureCategories.NonModable.Contains(structureType)}");

                            // Skip non-modifiable structures (except walls, which can take damage)
                            if (!isWall && StructureCategories.NonModable.Contains(structureType))
                            {
                                Plugin.Logger.LogInfo($"[WALL DEBUG] Skipping non-modifiable structure: {structureType}");
                                return;
                            }
                        }

                        float damageMultiplier = 1.0f;
                        string multiplierBreakdown = "1.0";

                        // Apply specific multipliers first (most specific to least specific)
                        if (isWall)
                        {
                            Plugin.Logger.LogInfo($"[WALL DEBUG] WALL DETECTED! Applying WallDamageTakenMultiplier={WallDamageTakenMultiplier.Value}");
                            damageMultiplier *= WallDamageTakenMultiplier.Value;
                            multiplierBreakdown += $" * Wall({WallDamageTakenMultiplier.Value})";
                        }
                        else if (isTower)
                        {
                            damageMultiplier *= TowerDamageTakenMultiplier.Value;
                            multiplierBreakdown += $" * Tower({TowerDamageTakenMultiplier.Value})";
                        }

                        if (isCivilStructure)
                        {
                            damageMultiplier *= CivilStructureDamageTakenMultiplier.Value;
                            multiplierBreakdown += $" * Civil({CivilStructureDamageTakenMultiplier.Value})";
                        }

                        // Apply general structure multiplier last
                        damageMultiplier *= GlobalDamageTakenMultiplier.Value;
                        multiplierBreakdown += $" * Global({GlobalDamageTakenMultiplier.Value})";

                        Plugin.Logger.LogInfo($"[WALL DEBUG] Structure damage calculation: {structureType} | Original={args.Damage} | {multiplierBreakdown} = {damageMultiplier}");

                        // Always apply multiplier (even if 1.0) to ensure we log and handle edge cases
                        // IMPORTANT: Minimum damage must be 1. If damage is 0, the game detects this and uses
                        // a default value from the damage matrix instead of our modified value.
                        float damageBeforeClamp = (float)args.Damage * damageMultiplier;
                        var modified = (int)Mathf.Clamp(damageBeforeClamp, 1, int.MaxValue);
                        
                        Plugin.Logger.LogInfo($"[WALL DEBUG] Structure damage modification: {structureType} | Original={args.Damage} | Multiplier={damageMultiplier} | BeforeClamp={damageBeforeClamp} | Modified={modified} | (Floor of 1 applied)");
                        Plugin.Logger.LogInfo($"[WALL DEBUG] Setting args.Damage from {args.Damage} to {modified}");
                        args.Damage = modified;
                        Plugin.Logger.LogInfo($"[WALL DEBUG] After assignment: args.Damage={args.Damage}");
                    }
                    catch (Exception ex)
                    {
                        Plugin.Logger.LogError($"Failed to apply structure damage multiplier: {ex.Message}\n{ex.StackTrace}");
                    }
                });
            Plugin.Logger.LogInfo("Successfully subscribed to OnBuildingTileTakeDamage event hook");
        }
    }
}

