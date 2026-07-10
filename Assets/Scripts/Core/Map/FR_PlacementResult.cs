using System;

namespace FantasyRoyale.Core.Map
{
    /// <summary>
    /// 配置抽選で決定した1件分の結果。
    /// Unity側はこの結果をPrefab配置へ変換する。
    /// </summary>
    [Serializable]
    public readonly struct FR_PlacementResult
    {
        public readonly string SlotId;
        public readonly string ObjectId;
        public readonly FR_IntVector2 GridPosition;
        public readonly FR_IntVector2 FootprintSize;

        public FR_PlacementResult(string slotId, string objectId, FR_IntVector2 gridPosition, FR_IntVector2 footprintSize)
        {
            SlotId = slotId;
            ObjectId = objectId;
            GridPosition = gridPosition;
            FootprintSize = footprintSize;
        }
    }
}
