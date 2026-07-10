using System;

namespace FantasyRoyale.Core.Map
{
    /// <summary>
    /// 固定マップ上に用意するランダム配置候補地。
    /// 何を直接置くかではなく、置けるカテゴリ、サイズ、タグ条件を持つ。
    /// </summary>
    [Serializable]
    public sealed class FR_MapSlot
    {
        public string SlotId { get; }
        public FR_IntVector2 GridPosition { get; }
        public FR_IntVector2 GridSize { get; }
        public FR_MapObjectSizeCategory SizeCategory { get; }
        public FR_MapBiome Biome { get; }
        public FR_MapSlotCategory Category { get; }
        public FR_MapObjectTag Tags { get; }
        public int MaxSpawnCount { get; }

        public FR_MapSlot(
            string slotId,
            FR_IntVector2 gridPosition,
            FR_IntVector2 gridSize,
            FR_MapObjectSizeCategory sizeCategory,
            FR_MapBiome biome,
            FR_MapSlotCategory category,
            FR_MapObjectTag tags,
            int maxSpawnCount)
        {
            SlotId = slotId;
            GridPosition = gridPosition;
            GridSize = gridSize;
            SizeCategory = sizeCategory;
            Biome = biome;
            Category = category;
            Tags = tags;
            MaxSpawnCount = Math.Max(1, maxSpawnCount);
        }

        public bool ContainsTag(FR_MapObjectTag tag)
        {
            return tag == FR_MapObjectTag.None || (Tags & tag) == tag;
        }
    }
}
