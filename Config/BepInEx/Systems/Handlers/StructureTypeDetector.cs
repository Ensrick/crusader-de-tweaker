// Config/BepInEx/Systems/Handlers/StructureTypeDetector.cs
using System.Linq;
using SHCDESE.API;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using CrusaderDETweaker.Data;

namespace CrusaderDETweaker.Config.BepInEx.Systems.Handlers
{
    /// <summary>
    /// Detects structure types from tile IDs and building IDs.
    /// Extracted from StructureMultipliersConfig to reduce class size.
    /// </summary>
    internal static class StructureTypeDetector
    {
        /// <summary>
        /// Structure type information extracted from damage event.
        /// </summary>
        public struct StructureTypeInfo
        {
            public eStructs StructureType;
            public bool IsWall;
            public bool IsTower;
            public bool IsGatehouse;
            public bool IsCivilStructure;
        }

        /// <summary>
        /// Detects structure type from a tile ID in a damage event.
        /// Returns null if structure cannot be determined or should be skipped.
        /// </summary>
        public static StructureTypeInfo? Detect(int tileId, ushort buildingId)
        {
            eStructs structureType = default(eStructs);
            bool isWall = false;
            bool isTower = false;
            bool isGatehouse = false;
            bool isCivilStructure = false;

            // Walls don't have building IDs - they're identified by tile property flags
            if (buildingId == 0)
            {
                // Check if this is a wall tile using tile property flags
                TilePropertyFlag tileFlags = GameTileManagerAPI.Instance.GetTilePropertyFlag(tileId);
                
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
                    }
                    else
                    {
                        structureType = eStructs.STRUCT_STONE_WALL;
                    }
                }
                else
                {
                    // Not a wall and no building ID - skip this tile
                    return null;
                }
            }
            else
            {
                // Regular building - get structure type from building API
                structureType = Plugin.BuildingApi.GetType(buildingId);

                // Check for walls, towers, gatehouses, and civil structures
                isWall = StructureCategories.IsWall(structureType);
                isTower = StructureCategories.IsTower(structureType);
                isGatehouse = StructureCategories.IsGatehouse(structureType);
                isCivilStructure = StructureCategories.IsCivilStructure(structureType);

                // Skip non-modifiable structures (except walls, which can take damage)
                if (!isWall && StructureCategories.IsNonModifiable(structureType))
                {
                    return null;
                }
            }

            return new StructureTypeInfo
            {
                StructureType = structureType,
                IsWall = isWall,
                IsTower = isTower,
                IsGatehouse = isGatehouse,
                IsCivilStructure = isCivilStructure
            };
        }
    }
}

