using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace FantasyRoyale.MapAuthoringKit.Editor
{
    /// <summary>名前は画像aliasではなく、grassが接する方角を示す。NorthはUnity +Y。</summary>
    public enum StudyASoilTileShape
    {
        Interior,
        GrassNorth, GrassEast, GrassSouth, GrassWest,
        ConvexGrassNorthWest, ConvexGrassNorthEast, ConvexGrassSouthEast, ConvexGrassSouthWest,
        ConcaveGrassNorthWest, ConcaveGrassNorthEast, ConcaveGrassSouthEast, ConcaveGrassSouthWest
    }

    /// <summary>中心+8近傍から土overlayの形を選ぶ。Pair後のglobal soil集合を渡す。</summary>
    public static class StudyATerrainTileSelector
    {
        public static bool TrySelect(ISet<Vector2Int> soil, Vector2Int cell,
            out StudyASoilTileShape shape, out string error)
        {
            if (soil == null) throw new ArgumentNullException(nameof(soil));
            shape = StudyASoilTileShape.Interior;
            if (!soil.Contains(cell)) { error = "The center cell is not soil: " + cell; return false; }
            return TrySelect(
                soil.Contains(cell + Vector2Int.up), soil.Contains(cell + Vector2Int.right),
                soil.Contains(cell + Vector2Int.down), soil.Contains(cell + Vector2Int.left),
                soil.Contains(cell + new Vector2Int(-1, 1)), soil.Contains(cell + new Vector2Int(1, 1)),
                soil.Contains(cell + new Vector2Int(1, -1)), soil.Contains(cell + new Vector2Int(-1, -1)),
                out shape, out error);
        }

        /// <summary>boolは当該近傍がsoilならtrue。露出辺に隣接する対角は境界形に影響しない。</summary>
        public static bool TrySelect(bool north, bool east, bool south, bool west,
            bool northWest, bool northEast, bool southEast, bool southWest,
            out StudyASoilTileShape shape, out string error)
        {
            shape = StudyASoilTileShape.Interior;
            error = null;
            var exposed = (north ? 0 : 1) + (east ? 0 : 1) + (south ? 0 : 1) + (west ? 0 : 1);
            var innerNW = north && west && !northWest;
            var innerNE = north && east && !northEast;
            var innerSE = south && east && !southEast;
            var innerSW = south && west && !southWest;
            var innerCount = (innerNW ? 1 : 0) + (innerNE ? 1 : 0) + (innerSE ? 1 : 0) + (innerSW ? 1 : 0);
            if (exposed >= 3 || (exposed == 2 && ((!north && !south) || (!east && !west))))
            {
                error = "The approved 12 boundary shapes cannot represent an isolated or one-cell-wide soil region.";
                return false;
            }
            if (innerCount > 1 || (exposed != 0 && innerCount != 0))
            {
                error = "The approved 12 boundary shapes cannot represent multiple corners in one soil cell.";
                return false;
            }
            if (exposed == 0)
            {
                if (innerNW) shape = StudyASoilTileShape.ConcaveGrassNorthWest;
                else if (innerNE) shape = StudyASoilTileShape.ConcaveGrassNorthEast;
                else if (innerSE) shape = StudyASoilTileShape.ConcaveGrassSouthEast;
                else if (innerSW) shape = StudyASoilTileShape.ConcaveGrassSouthWest;
                return true;
            }
            if (exposed == 1)
            {
                shape = !north ? StudyASoilTileShape.GrassNorth : !east ? StudyASoilTileShape.GrassEast :
                    !south ? StudyASoilTileShape.GrassSouth : StudyASoilTileShape.GrassWest;
                return true;
            }
            shape = !north && !west ? StudyASoilTileShape.ConvexGrassNorthWest :
                !north && !east ? StudyASoilTileShape.ConvexGrassNorthEast :
                !south && !east ? StudyASoilTileShape.ConvexGrassSouthEast : StudyASoilTileShape.ConvexGrassSouthWest;
            return true;
        }

        /// <summary>全セルを先に検証する。非対応形を無言でInteriorへ落とさない。</summary>
        public static List<string> ValidateLayout(ISet<Vector2Int> soil)
        {
            if (soil == null) throw new ArgumentNullException(nameof(soil));
            var errors = new List<string>();
            foreach (var cell in soil.OrderBy(c => c.y).ThenBy(c => c.x))
                if (!TrySelect(soil, cell, out _, out var error)) errors.Add(cell + ": " + error);
            return errors;
        }

        /// <summary>UnityEngine.Random状態と列挙順序に依存しない地表variant。</summary>
        public static int SelectVariant(int seed, Vector2Int cell, int count)
        {
            if (count <= 0) throw new ArgumentOutOfRangeException(nameof(count));
            unchecked
            {
                uint value = (uint)seed ^ (uint)cell.x * 0x9e3779b9u ^ (uint)cell.y * 0x85ebca6bu;
                value ^= value >> 16; value *= 0x7feb352du; value ^= value >> 15;
                value *= 0x846ca68bu; value ^= value >> 16;
                return (int)(value % (uint)count);
            }
        }
    }
}

