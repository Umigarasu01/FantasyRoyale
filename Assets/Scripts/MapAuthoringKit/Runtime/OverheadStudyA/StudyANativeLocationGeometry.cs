using System;
using System.Collections.Generic;
using UnityEngine;

namespace FantasyRoyale.MapAuthoringKit.OverheadStudyA
{
    /// <summary>
    /// Derives exact native-pixel floor, perimeter and void geometry from the
    /// locations that are present. This creates data only, with no scene objects.
    /// </summary>
    public static class StudyANativeLocationGeometry
    {
        public struct Plan
        {
            public readonly RectInt BoundsPx;
            public readonly RectInt[] FloorsPx;
            public readonly RectInt[] PhysicalPerimeterPx;
            public readonly RectInt[] VoidInsideFramePx;
            public readonly int PixelsPerUnit;

            internal Plan(RectInt bounds, RectInt[] floors, RectInt[] perimeter,
                RectInt[] voids, int pixelsPerUnit)
            {
                BoundsPx = bounds;
                FloorsPx = floors;
                PhysicalPerimeterPx = perimeter;
                VoidInsideFramePx = voids;
                PixelsPerUnit = pixelsPerUnit;
            }
        }

        /// <summary>
        /// The physical perimeter is union(expand(floor, PPU)) minus union(floor).
        /// Expansion uses the Chebyshev metric, so its corners are covered.
        /// Frame voids are navigation data only, not additional physical slabs.
        /// Bounds are derived from Blocks; cached NormalizedBoundsPx is not used.
        /// Origins remain exact integers, including unnormalized/negative input.
        /// </summary>
        public static Plan MethodPlan(StudyANativeLocationAssembly assembly, int pixelsPerUnit)
        {
            if (assembly == null)
                throw new ArgumentNullException(nameof(assembly));
            if (pixelsPerUnit <= 0)
                throw new ArgumentOutOfRangeException(nameof(pixelsPerUnit));
            if (assembly.Blocks == null || assembly.Blocks.Count == 0)
                throw new ArgumentException("At least one location floor is required.", nameof(assembly));

            var floors = new List<RectInt>(assembly.Blocks.Count);
            var expanded = new List<RectInt>(assembly.Blocks.Count);
            var xs = new SortedSet<int>();
            var ys = new SortedSet<int>();
            int minX = int.MaxValue, minY = int.MaxValue;
            int maxX = int.MinValue, maxY = int.MinValue;

            foreach (var block in assembly.Blocks)
            {
                if (ReferenceEquals(block, null))
                    throw new ArgumentException("A location floor cannot be null.", nameof(assembly));
                if (block.ExtentPx.x <= 0 || block.ExtentPx.y <= 0)
                    throw new ArgumentException("Location extents must be positive native pixels.", nameof(assembly));

                int x0 = block.OriginPx.x, y0 = block.OriginPx.y;
                int x1 = checked(x0 + block.ExtentPx.x);
                int y1 = checked(y0 + block.ExtentPx.y);
                var floor = new RectInt(x0, y0, block.ExtentPx.x, block.ExtentPx.y);
                floors.Add(floor);
                minX = Math.Min(minX, x0); minY = Math.Min(minY, y0);
                maxX = Math.Max(maxX, x1); maxY = Math.Max(maxY, y1);

                int ex0 = checked(x0 - pixelsPerUnit), ey0 = checked(y0 - pixelsPerUnit);
                int ex1 = checked(x1 + pixelsPerUnit), ey1 = checked(y1 + pixelsPerUnit);
                expanded.Add(new RectInt(ex0, ey0, checked(ex1 - ex0), checked(ey1 - ey0)));
                xs.Add(x0); xs.Add(x1); xs.Add(ex0); xs.Add(ex1);
                ys.Add(y0); ys.Add(y1); ys.Add(ey0); ys.Add(ey1);
            }

            var bounds = new RectInt(minX, minY, checked(maxX - minX), checked(maxY - minY));
            floors.Sort(CompareRects);
            var xEdges = new int[xs.Count]; xs.CopyTo(xEdges);
            var yEdges = new int[ys.Count]; ys.CopyTo(yEdges);
            var perimeterCells = new bool[yEdges.Length - 1, xEdges.Length - 1];
            var voidCells = new bool[yEdges.Length - 1, xEdges.Length - 1];

            // All floor and expanded-floor edges are included. Membership is
            // constant inside each compressed cell; no raster size or rounding
            // is involved, even when a world origin is fractional at native PPU.
            for (int row = 0; row < yEdges.Length - 1; row++)
            {
                int y0 = yEdges[row], y1 = yEdges[row + 1];
                for (int col = 0; col < xEdges.Length - 1; col++)
                {
                    int x0 = xEdges[col], x1 = xEdges[col + 1];
                    bool onFloor = InsideAny(floors, x0, y0, x1, y1);
                    if (onFloor)
                        continue;
                    perimeterCells[row, col] = InsideAny(expanded, x0, y0, x1, y1);
                    voidCells[row, col] = ContainsCell(bounds, x0, y0, x1, y1);
                }
            }

            return new Plan(bounds, floors.ToArray(),
                MergeRowRuns(perimeterCells, xEdges, yEdges),
                MergeRowRuns(voidCells, xEdges, yEdges), pixelsPerUnit);
        }

        private static bool InsideAny(List<RectInt> rects, int x0, int y0, int x1, int y1)
        {
            foreach (var rect in rects)
                if (ContainsCell(rect, x0, y0, x1, y1))
                    return true;
            return false;
        }

        private static bool ContainsCell(RectInt rect, int x0, int y0, int x1, int y1)
        {
            return rect.xMin <= x0 && x1 <= rect.xMax && rect.yMin <= y0 && y1 <= rect.yMax;
        }

        private struct RunKey : IEquatable<RunKey>
        {
            private readonly int x0, x1;

            public RunKey(int start, int end) { x0 = start; x1 = end; }
            public bool Equals(RunKey other) { return x0 == other.x0 && x1 == other.x1; }
            public override bool Equals(object other) { return other is RunKey && Equals((RunKey)other); }
            public override int GetHashCode() { unchecked { return (x0 * 397) ^ x1; } }
        }

        // Each row is divided into maximal X runs. A run is extended vertically
        // only when its exact X interval continues on the immediately next row.
        // The resulting rectangles have disjoint interiors and deterministic order.
        private static RectInt[] MergeRowRuns(bool[,] cells, int[] xs, int[] ys)
        {
            var active = new Dictionary<RunKey, RectInt>();
            var completed = new List<RectInt>();
            int columns = xs.Length - 1;

            for (int row = 0; row < ys.Length - 1; row++)
            {
                var next = new Dictionary<RunKey, RectInt>();
                int col = 0;
                while (col < columns)
                {
                    if (!cells[row, col]) { col++; continue; }
                    int begin = col++;
                    while (col < columns && cells[row, col]) col++;
                    int x0 = xs[begin], x1 = xs[col];
                    var key = new RunKey(x0, x1);
                    RectInt previous;
                    if (active.TryGetValue(key, out previous) && previous.yMax == ys[row])
                        next.Add(key, new RectInt(x0, previous.yMin,
                            checked(x1 - x0), checked(ys[row + 1] - previous.yMin)));
                    else
                        next.Add(key, new RectInt(x0, ys[row],
                            checked(x1 - x0), checked(ys[row + 1] - ys[row])));
                }

                foreach (var run in active)
                    if (!next.ContainsKey(run.Key))
                        completed.Add(run.Value);
                active = next;
            }

            completed.AddRange(active.Values);
            completed.Sort(CompareRects);
            return completed.ToArray();
        }

        private static int CompareRects(RectInt a, RectInt b)
        {
            int order = a.yMin.CompareTo(b.yMin);
            if (order != 0) return order;
            order = a.xMin.CompareTo(b.xMin);
            if (order != 0) return order;
            order = a.width.CompareTo(b.width);
            return order != 0 ? order : a.height.CompareTo(b.height);
        }
    }
}
