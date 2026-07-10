using System;

namespace FantasyRoyale.Core.Map
{
    /// <summary>
    /// 固定マップ内の大まかなロケーション種別。
    /// スロット抽選や見た目の切り替え条件に使う。
    /// </summary>
    public enum FR_MapBiome
    {
        Any,
        Grassland,
        Forest,
        WaterSide,
        Snowfield,
        Volcano,
        Ruins
    }

    /// <summary>
    /// マップスロットが何を置くための候補地かを表す。
    /// </summary>
    public enum FR_MapSlotCategory
    {
        Decoration,
        Landmark,
        EnemySpawn,
        Loot,
        Merchant,
        DungeonEntrance,
        Hazard,
        PlayerStart,
        CpuStart
    }

    /// <summary>
    /// 配置物の大まかなサイズ分類。
    /// 抽選候補の粗い絞り込みに使い、実占有はFootprintSizeで判断する。
    /// </summary>
    public enum FR_MapObjectSizeCategory
    {
        Small,
        Medium,
        Large,
        Huge
    }

    /// <summary>
    /// スロットや配置物の性質を表すタグ。
    /// ビルド時に「森用」「高リスク」「水辺」などの条件を合わせる。
    /// </summary>
    [Flags]
    public enum FR_MapObjectTag
    {
        None = 0,
        Forest = 1 << 0,
        Grassland = 1 << 1,
        WaterSide = 1 << 2,
        Snowfield = 1 << 3,
        Volcano = 1 << 4,
        Ruins = 1 << 5,
        NearRoad = 1 << 6,
        SafeStart = 1 << 7,
        HighRisk = 1 << 8,
        BlocksMovement = 1 << 9,
        WalkableDecoration = 1 << 10
    }

    /// <summary>
    /// Tilemapへ描く基本タイルの種類。
    /// Core側でも領域の意図を持てるよう、UnityのTileBaseには依存させない。
    /// </summary>
    public enum FR_MapTileKind
    {
        Grass,
        TallGrass,
        Road,
        StonePath,
        Water,
        Snow,
        Ice,
        Lava,
        Ash,
        Cliff,
        Dirt
    }
}
