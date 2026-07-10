using System;
using FantasyRoyale.Core.Map;
using UnityEngine;

namespace FantasyRoyale.Unity.Exploration
{
    /// <summary>
    /// Tilemapで使う固定マップのサイズ、地形領域、ランダム配置スロットをまとめるScriptableObject。
    /// Coreへ渡すときはUnity型をFR_型へ変換する。
    /// </summary>
    [CreateAssetMenu(menuName = "FantasyRoyale/Map Definition")]
    public sealed class MapDefinitionAsset : ScriptableObject
    {
        [SerializeField] private string mapId = "milestone1_exploration";
        [SerializeField] private Vector2Int gridSize = new Vector2Int(96, 72);
        [SerializeField] private MapTileArea[] tileAreas = Array.Empty<MapTileArea>();
        [SerializeField] private MapSlotDefinition[] slots = Array.Empty<MapSlotDefinition>();

        public string MapId => mapId;
        public Vector2Int GridSize => gridSize;
        public MapTileArea[] TileAreas => tileAreas;
        public MapSlotDefinition[] Slots => slots;

        public FR_MapDefinition ToCoreDefinition()
        {
            var coreSlots = new FR_MapSlot[slots.Length];
            for (var i = 0; i < slots.Length; i++)
            {
                coreSlots[i] = slots[i].ToCoreSlot();
            }

            return new FR_MapDefinition(mapId, new FR_IntVector2(gridSize.x, gridSize.y), coreSlots);
        }

        public void Configure(string newMapId, Vector2Int newGridSize, MapTileArea[] newTileAreas, MapSlotDefinition[] newSlots)
        {
            mapId = newMapId;
            gridSize = newGridSize;
            tileAreas = newTileAreas ?? Array.Empty<MapTileArea>();
            slots = newSlots ?? Array.Empty<MapSlotDefinition>();
        }
    }

    /// <summary>
    /// 固定マップ上で同じ地形タイルを塗る矩形領域。
    /// </summary>
    [Serializable]
    public struct MapTileArea
    {
        public string areaId;
        public FR_MapTileKind tileKind;
        public RectInt rect;
        public bool blocksMovement;
        public int detailEvery;
    }

    /// <summary>
    /// Unity Inspectorで編集するスロット定義。
    /// 配置抽選時はCoreのFR_MapSlotへ変換する。
    /// </summary>
    [Serializable]
    public struct MapSlotDefinition
    {
        public string slotId;
        public Vector2Int gridPosition;
        public Vector2Int gridSize;
        public FR_MapObjectSizeCategory sizeCategory;
        public FR_MapBiome biome;
        public FR_MapSlotCategory category;
        public FR_MapObjectTag tags;
        public int maxSpawnCount;

        public FR_MapSlot ToCoreSlot()
        {
            return new FR_MapSlot(
                slotId,
                new FR_IntVector2(gridPosition.x, gridPosition.y),
                new FR_IntVector2(gridSize.x, gridSize.y),
                sizeCategory,
                biome,
                category,
                tags,
                maxSpawnCount);
        }
    }
}
