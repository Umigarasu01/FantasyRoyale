using FantasyRoyale.Core.Map;
using UnityEngine;

namespace FantasyRoyale.Unity.Exploration
{
    /// <summary>
    /// ランダム配置できる設置物のUnity側定義。
    /// Coreに渡す抽選条件と、実際に生成するPrefabを橋渡しする。
    /// </summary>
    [CreateAssetMenu(menuName = "FantasyRoyale/Map Object Definition")]
    public sealed class MapObjectDefinitionAsset : ScriptableObject
    {
        [SerializeField] private string objectId = "object";
        [SerializeField] private GameObject prefab;
        [SerializeField] private FR_MapSlotCategory category;
        [SerializeField] private FR_MapObjectSizeCategory sizeCategory = FR_MapObjectSizeCategory.Small;
        [SerializeField] private Vector2Int footprintSize = Vector2Int.one;
        [SerializeField] private FR_MapBiome biome = FR_MapBiome.Any;
        [SerializeField] private FR_MapObjectTag tags = FR_MapObjectTag.None;
        [SerializeField] private int weight = 10;
        [SerializeField] private bool canShareLargeSlot = true;

        public string ObjectId => objectId;
        public GameObject Prefab => prefab;

        public FR_MapObjectDefinition ToCoreDefinition()
        {
            return new FR_MapObjectDefinition(
                objectId,
                category,
                sizeCategory,
                new FR_IntVector2(footprintSize.x, footprintSize.y),
                biome,
                tags,
                weight,
                canShareLargeSlot);
        }

        public void Configure(
            string newObjectId,
            GameObject newPrefab,
            FR_MapSlotCategory newCategory,
            FR_MapObjectSizeCategory newSizeCategory,
            Vector2Int newFootprintSize,
            FR_MapBiome newBiome,
            FR_MapObjectTag newTags,
            int newWeight,
            bool newCanShareLargeSlot)
        {
            objectId = newObjectId;
            prefab = newPrefab;
            category = newCategory;
            sizeCategory = newSizeCategory;
            footprintSize = newFootprintSize;
            biome = newBiome;
            tags = newTags;
            weight = newWeight;
            canShareLargeSlot = newCanShareLargeSlot;
        }
    }
}
