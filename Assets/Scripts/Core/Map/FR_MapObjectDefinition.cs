using System;

namespace FantasyRoyale.Core.Map
{
    /// <summary>
    /// ランダム配置で出現するオブジェクト候補。
    /// Core層ではPrefabを持たず、配置条件と占有サイズだけを扱う。
    /// </summary>
    [Serializable]
    public sealed class FR_MapObjectDefinition
    {
        public string ObjectId { get; }
        public FR_MapSlotCategory Category { get; }
        public FR_MapObjectSizeCategory SizeCategory { get; }
        public FR_IntVector2 FootprintSize { get; }
        public FR_MapBiome Biome { get; }
        public FR_MapObjectTag Tags { get; }
        public int Weight { get; }
        public bool CanShareLargeSlot { get; }

        public FR_MapObjectDefinition(
            string objectId,
            FR_MapSlotCategory category,
            FR_MapObjectSizeCategory sizeCategory,
            FR_IntVector2 footprintSize,
            FR_MapBiome biome,
            FR_MapObjectTag tags,
            int weight,
            bool canShareLargeSlot)
        {
            ObjectId = objectId;
            Category = category;
            SizeCategory = sizeCategory;
            FootprintSize = footprintSize;
            Biome = biome;
            Tags = tags;
            Weight = Math.Max(0, weight);
            CanShareLargeSlot = canShareLargeSlot;
        }

        public bool ContainsTag(FR_MapObjectTag tag)
        {
            return tag == FR_MapObjectTag.None || (Tags & tag) == tag;
        }
    }
}
