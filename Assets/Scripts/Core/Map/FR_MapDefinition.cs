using System;

namespace FantasyRoyale.Core.Map
{
    /// <summary>
    /// Core層で扱う固定マップ定義。
    /// 地形描画ではなく、サイズとランダム配置スロットの解決に必要な情報だけを持つ。
    /// </summary>
    [Serializable]
    public sealed class FR_MapDefinition
    {
        public string MapId { get; }
        public FR_IntVector2 GridSize { get; }
        public FR_MapSlot[] Slots { get; }

        public FR_MapDefinition(string mapId, FR_IntVector2 gridSize, FR_MapSlot[] slots)
        {
            MapId = mapId;
            GridSize = gridSize;
            Slots = slots ?? Array.Empty<FR_MapSlot>();
        }
    }
}
