using System;
using System.Collections.Generic;
using UnityEngine;

namespace FantasyRoyale.MapAuthoringKit.Editor
{
    /// <summary>
    /// 直交Waypoint列を道路Cellへ変換し、表示素材の選択より前に連続した道路形状を確定する。
    /// 各中心Cellへ正方形Brushを重ねるため、曲がり角を太くしても斜めの穴を残さない。
    /// </summary>
    public static class MapRoadPathRasterizer
    {
        private static readonly Vector3Int[] CardinalDirections =
        {
            Vector3Int.up,
            Vector3Int.right,
            Vector3Int.down,
            Vector3Int.left
        };

        /// <summary>
        /// 一つの直交Waypoint列を両端込みで1Cellずつ辿り、正方形Brushで膨張した道路Cellを返す。
        /// </summary>
        public static HashSet<Vector3Int> RasterizePath(
            IEnumerable<Vector3Int> waypoints,
            int squareBrushRadius = 0)
        {
            if (waypoints == null)
            {
                throw new ArgumentNullException(nameof(waypoints));
            }

            ValidateBrushRadius(squareBrushRadius);
            var cells = new HashSet<Vector3Int>();
            RasterizePathInto(cells, waypoints, squareBrushRadius, 0);
            return cells;
        }

        /// <summary>
        /// 複数の直交Waypoint列を同じCell集合へRasterizeし、交差や合流を重複なく統合する。
        /// </summary>
        public static HashSet<Vector3Int> RasterizePaths(
            IEnumerable<IEnumerable<Vector3Int>> waypointPaths,
            int squareBrushRadius = 0)
        {
            if (waypointPaths == null)
            {
                throw new ArgumentNullException(nameof(waypointPaths));
            }

            ValidateBrushRadius(squareBrushRadius);
            var cells = new HashSet<Vector3Int>();
            var pathIndex = 0;
            foreach (var waypoints in waypointPaths)
            {
                if (waypoints == null)
                {
                    throw new ArgumentException(
                        $"Waypoint列にnullが含まれています: pathIndex={pathIndex}",
                        nameof(waypointPaths));
                }

                RasterizePathInto(cells, waypoints, squareBrushRadius, pathIndex);
                pathIndex++;
            }

            return cells;
        }

        /// <summary>
        /// 指定Cell集合が同じZ平面上の4近傍で単一Componentを構成しているかを判定する。
        /// 空集合は道路が存在しないためfalse、1Cellだけならtrueとする。
        /// </summary>
        public static bool IsSingleCardinalComponent(IEnumerable<Vector3Int> cells)
        {
            if (cells == null)
            {
                throw new ArgumentNullException(nameof(cells));
            }

            var remaining = new HashSet<Vector3Int>(cells);
            if (remaining.Count == 0)
            {
                return false;
            }

            var queue = new Queue<Vector3Int>();
            using (var enumerator = remaining.GetEnumerator())
            {
                enumerator.MoveNext();
                queue.Enqueue(enumerator.Current);
            }

            var visitedCount = 0;
            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                if (!remaining.Remove(current))
                {
                    continue;
                }

                visitedCount++;
                for (var directionIndex = 0;
                     directionIndex < CardinalDirections.Length;
                     directionIndex++)
                {
                    var neighbor = current + CardinalDirections[directionIndex];
                    if (remaining.Contains(neighbor))
                    {
                        queue.Enqueue(neighbor);
                    }
                }
            }

            return visitedCount > 0 && remaining.Count == 0;
        }

        /// <summary>
        /// 一つのWaypoint列を既存集合へ追加し、各直交区間を両端込みで埋める。
        /// </summary>
        private static void RasterizePathInto(
            ISet<Vector3Int> destination,
            IEnumerable<Vector3Int> waypoints,
            int squareBrushRadius,
            int pathIndex)
        {
            var hasPrevious = false;
            var previous = default(Vector3Int);
            var waypointIndex = 0;

            foreach (var waypoint in waypoints)
            {
                if (!hasPrevious)
                {
                    AddSquareBrush(destination, waypoint, squareBrushRadius);
                    previous = waypoint;
                    hasPrevious = true;
                    waypointIndex++;
                    continue;
                }

                RasterizeSegment(
                    destination,
                    previous,
                    waypoint,
                    squareBrushRadius,
                    pathIndex,
                    waypointIndex - 1);
                previous = waypoint;
                waypointIndex++;
            }
        }

        /// <summary>
        /// 水平または垂直区間だけを1Cell刻みで追加し、斜め区間やZ平面の変更を即座に拒否する。
        /// </summary>
        private static void RasterizeSegment(
            ISet<Vector3Int> destination,
            Vector3Int start,
            Vector3Int end,
            int squareBrushRadius,
            int pathIndex,
            int segmentIndex)
        {
            var changesX = start.x != end.x;
            var changesY = start.y != end.y;
            if ((changesX && changesY) || start.z != end.z)
            {
                throw new ArgumentException(
                    "道路Waypointは同じZ平面の水平・垂直区間で接続してください。"
                    + $" pathIndex={pathIndex}, segmentIndex={segmentIndex}, start={start}, end={end}");
            }

            var step = new Vector3Int(
                Math.Sign(end.x - start.x),
                Math.Sign(end.y - start.y),
                0);
            var distance = Math.Max(
                Math.Abs(end.x - start.x),
                Math.Abs(end.y - start.y));

            // 0から始めることで始点と終点の両方を必ず含み、隣接区間との角を共有する。
            for (var offset = 0; offset <= distance; offset++)
            {
                AddSquareBrush(destination, start + step * offset, squareBrushRadius);
            }
        }

        /// <summary>
        /// 中心Cellの周囲へ一辺2r+1の正方形を追加し、曲がり角でもBrush同士を重ねる。
        /// </summary>
        private static void AddSquareBrush(
            ISet<Vector3Int> destination,
            Vector3Int center,
            int radius)
        {
            for (var y = -radius; y <= radius; y++)
            {
                for (var x = -radius; x <= radius; x++)
                {
                    destination.Add(new Vector3Int(center.x + x, center.y + y, center.z));
                }
            }
        }

        /// <summary>
        /// 負のRadiusによる空集合や逆向きLoopを防ぎ、呼び出し側の設定誤りを明示する。
        /// </summary>
        private static void ValidateBrushRadius(int radius)
        {
            if (radius < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(radius),
                    radius,
                    "正方形BrushのRadiusは0以上で指定してください。");
            }
        }
    }
}
