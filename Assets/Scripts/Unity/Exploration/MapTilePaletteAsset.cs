using System;
using FantasyRoyale.Core.Map;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace FantasyRoyale.Unity.Exploration
{
    /// <summary>
    /// CoreのFR_MapTileKindとTilemap用TileBaseの対応表。
    /// 仮素材から正式素材へ差し替えるときはこのAssetを中心に更新する。
    /// </summary>
    [CreateAssetMenu(menuName = "FantasyRoyale/Map Tile Palette")]
    public sealed class MapTilePaletteAsset : ScriptableObject
    {
        [SerializeField] private TilePaletteEntry[] entries = Array.Empty<TilePaletteEntry>();

        public TileBase Find(FR_MapTileKind kind)
        {
            for (var i = 0; i < entries.Length; i++)
            {
                if (entries[i].kind == kind)
                {
                    return entries[i].tile;
                }
            }

            return null;
        }

        public void Configure(TilePaletteEntry[] newEntries)
        {
            entries = newEntries ?? Array.Empty<TilePaletteEntry>();
        }
    }

    /// <summary>
    /// 1種類の地形とTileAssetの対応。
    /// </summary>
    [Serializable]
    public struct TilePaletteEntry
    {
        public FR_MapTileKind kind;
        public TileBase tile;
    }
}
