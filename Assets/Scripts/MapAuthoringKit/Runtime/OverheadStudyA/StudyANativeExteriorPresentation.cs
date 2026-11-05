using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace FantasyRoyale.MapAuthoringKit.OverheadStudyA
{
    [Serializable]
    public sealed class StudyANativeExteriorPresentationProfile
    {
        public string Schema = "FantasyRoyale.NativeExteriorPresentation.v1";
        public string Id;
        public int Version = 1, PixelsPerUnit = 64, PresentationMarginPx = 128;
        public string SourceTexturePath, SourceTextureSHA256, SourceTextureMetaSHA256;
        public Vector2Int SourceExtentPx;
        // All source and world rectangles use native bottom-left pixel coordinates.
        public RectInt SourceRectPx;
    }

    [Serializable]
    public sealed class StudyANativeExteriorPresentationPlan
    {
        public bool Applied, SourcePixelsNative1To1, FloorNavigationGeometryUnchanged, OrthogonalCornersPreserved;
        public string ProfileId, RecipeId;
        public int PixelsPerUnit = 64, PresentationMarginPx;
        public RectInt FloorFramePx, PresentationFramePx;
        public int ExposedBoundaryLengthPx, RiverExcludedLengthPx, PresentedBoundaryLengthPx;
        public int ConvexCornerCount, ConcaveCornerCount;
        public StudyANativeExteriorSegment[] Segments = Array.Empty<StudyANativeExteriorSegment>();
        public StudyANativeExteriorPlacement[] Placements = Array.Empty<StudyANativeExteriorPlacement>();
    }

    [Serializable]
    public sealed class StudyANativeExteriorSegment
    {
        public string LocationId, Side, Axis;
        public int FixedPx, AlongMinPx, AlongMaxPx;
    }

    [Serializable]
    public sealed class StudyANativeExteriorPlacement
    {
        public string SourceTexturePath, SourceTextureSHA256, SourceTextureMetaSHA256;
        public Vector2Int SourceExtentPx;
        public RectInt SourceRectPx, TargetRectWorldPx;
        public Vector2 WorldCenterPx;
        public int RotationDegrees, SegmentIndex, AlongStartPx, SortingOrder;
        public string LocationId, Side;
    }

    /// <summary>
    /// Pure source-UV plan for the small vertical earth face outside the existing
    /// S0/E floor. It changes no source assets, floor, water, navigation, owners,
    /// foot units or physical wall. Presentation padding is a separate domain.
    /// </summary>
    public static class StudyANativeExteriorPresentation
    {
        public const string ApprovedSourcePath = "Assets/Art/Map/OverheadStudyA/IndependentPrefabStudies/IsolatedStyleFamily-v2-20261004-215942621/Sources/bank_west_style.png";
        public const string ApprovedSourceSHA256 = "b975b0da6456582c76ce2831dd4b820b7c6c87077484abb1f4f1ed0a5118161a";
        public const string ApprovedSourceMetaSHA256 = "149834344ab296fedc0aa14d1e0859e1d76e9948f04767252884e2f1cd2652c8";
        private const int PPU = 64, FaceWidth = 31, Margin = 128;

        public static StudyANativeExteriorPresentationPlan BuildPlan(StudyANativeExteriorPresentationProfile profile,
            StudyANativeLocationAssembly assembly, string recipeId, StudyANativeLocationGeometry.Plan geometry)
        {
            Need(profile != null && assembly != null && assembly.Blocks != null && assembly.Connections != null,
                "Exterior presentation requires an explicit profile and generated assembly.");
            Need(profile.Schema == "FantasyRoyale.NativeExteriorPresentation.v1" && profile.Version == 1 &&
                !string.IsNullOrWhiteSpace(profile.Id) && profile.PixelsPerUnit == PPU && profile.PresentationMarginPx == Margin,
                "Only the reviewed exterior v1 profile at64PPU with128px presentation padding is supported.");
            Need(profile.SourceTexturePath == ApprovedSourcePath && profile.SourceTextureSHA256 == ApprovedSourceSHA256 &&
                profile.SourceTextureMetaSHA256 == ApprovedSourceMetaSHA256 &&
                profile.SourceExtentPx.x == 768 && profile.SourceExtentPx.y == 640 &&
                Same(profile.SourceRectPx, new RectInt(389, 384, FaceWidth, PPU)),
                "Exterior presentation must pin the unchanged original bank texture/meta and reviewed31x64 source slice.");
            ValidateAssembly(assembly, recipeId);
            var actual = StudyANativeLocationGeometry.MethodPlan(assembly, PPU);
            Need(geometry.PixelsPerUnit == PPU && Same(geometry.BoundsPx, actual.BoundsPx) &&
                Same(geometry.FloorsPx, actual.FloorsPx) && Same(geometry.PhysicalPerimeterPx, actual.PhysicalPerimeterPx) &&
                Same(geometry.VoidInsideFramePx, actual.VoidInsideFramePx),
                "Exterior presentation requires the exact unchanged geometry derived from the actual assembly.");

            var segments = new List<StudyANativeExteriorSegment>();
            foreach (var block in assembly.Blocks.OrderBy(b => b.LocationId, StringComparer.Ordinal))
            {
                int x = block.OriginPx.x, y = block.OriginPx.y;
                int x1 = checked(x + block.ExtentPx.x), y1 = checked(y + block.ExtentPx.y);
                Exposed(segments, block.LocationId, "W", x, y, y1, actual.FloorsPx);
                Exposed(segments, block.LocationId, "E", x1, y, y1, actual.FloorsPx);
                Exposed(segments, block.LocationId, "S", y, x, x1, actual.FloorsPx);
                Exposed(segments, block.LocationId, "N", y1, x, x1, actual.FloorsPx);
            }
            var placements = new List<StudyANativeExteriorPlacement>();
            int length = 0, excluded = 0;
            for (var index = 0; index < segments.Count; index++)
            {
                var segment = segments[index];
                length = checked(length + segment.AlongMaxPx - segment.AlongMinPx);
                bool riverEdge = segment.LocationId == "A" && (segment.Side == "N" || segment.Side == "S");
                var intervals = new List<Interval>();
                if (riverEdge)
                {
                    // Both source-native river exits remain unchanged and nontraversable.
                    int low = Math.Max(segment.AlongMinPx, 352), high = Math.Min(segment.AlongMaxPx, 608);
                    if (high > low)
                    {
                        if (segment.AlongMinPx < low) intervals.Add(new Interval(segment.AlongMinPx, low));
                        if (high < segment.AlongMaxPx) intervals.Add(new Interval(high, segment.AlongMaxPx));
                        excluded = checked(excluded + high - low);
                    }
                    else intervals.Add(new Interval(segment.AlongMinPx, segment.AlongMaxPx));
                }
                else intervals.Add(new Interval(segment.AlongMinPx, segment.AlongMaxPx));
                foreach (var interval in intervals)
                {
                    int tile = segment.AlongMinPx + ((interval.Min - segment.AlongMinPx) / PPU) * PPU;
                    for (; tile < interval.Max; tile = checked(tile + PPU))
                    {
                        int start = Math.Max(interval.Min, tile), end = Math.Min(interval.Max, checked(tile + PPU));
                        if (end <= start) continue;
                        var placement = Place(profile, segment, index, start, end - start, start - tile);
                        Need(!actual.FloorsPx.Any(f => Overlap(f, placement.TargetRectWorldPx)) &&
                            CoveredBy(placement.TargetRectWorldPx, actual.PhysicalPerimeterPx),
                            "Every exterior source-UV rectangle must stay wholly outside all original floors and within the actual physical wall.");
                        Need(placement.SortingOrder >= 2 && placement.SortingOrder <= 4,
                            "Exterior MapGround orders must remain below the existing water, owner shadows and contact vegetation.");
                        placements.Add(placement);
                    }
                }
            }
            CountCorners(segments, out var convex, out var concave);
            var frame = actual.BoundsPx;
            var result = new StudyANativeExteriorPresentationPlan {
                Applied = true, ProfileId = profile.Id, RecipeId = recipeId, PixelsPerUnit = PPU,
                SourcePixelsNative1To1 = true, FloorNavigationGeometryUnchanged = true, OrthogonalCornersPreserved = true,
                PresentationMarginPx = Margin, FloorFramePx = frame,
                PresentationFramePx = new RectInt(checked(frame.x - Margin), checked(frame.y - Margin),
                    checked(frame.width + Margin * 2), checked(frame.height + Margin * 2)),
                Segments = segments.ToArray(), Placements = placements.ToArray(),
                ExposedBoundaryLengthPx = length, RiverExcludedLengthPx = excluded, PresentedBoundaryLengthPx = length - excluded,
                ConvexCornerCount = convex, ConcaveCornerCount = concave
            };
            Need(excluded == 512 && (recipeId == "S0" ? segments.Count == 12 && length == 5632 &&
                placements.Count == 82 && convex == 7 && concave == 3 : segments.Count == 11 && length == 5376 &&
                placements.Count == 78 && convex == 6 && concave == 2),
                "Exterior plan must retain the reviewed complete S0/E boundary and exact two256px river apertures.");
            return result;
        }

        private static void ValidateAssembly(StudyANativeLocationAssembly assembly, string recipe)
        {
            Need(recipe == "S0" || recipe == "E", "Exterior presentation supports only the recognized S0/E recipes.");
            Need(assembly.Blocks.Count == 4 && assembly.Blocks.All(b => b != null) &&
                assembly.Blocks.Select(b => b.LocationId).OrderBy(id => id, StringComparer.Ordinal).SequenceEqual(new[] { "A", "B", "C", "D" }),
                "Exterior presentation requires exactly one unchanged A/B/C/D location.");
            var blocks = assembly.Blocks.ToDictionary(b => b.LocationId, StringComparer.Ordinal);
            Need(Block(blocks["A"], 0, recipe == "E" ? 256 : 0, 768, 640) &&
                Block(blocks["B"], 768, recipe == "E" ? 256 : 0, 384, 640) &&
                Block(blocks["C"], 1152, recipe == "E" ? 0 : 128, 512, 384) &&
                Block(blocks["D"], 1152, recipe == "E" ? 384 : 512, 640, 512),
                "Only the reviewed normalized S0/E origins and unchanged source extents are supported.");
            Need(assembly.Connections.Count == 3 && assembly.Connections.All(c => c != null &&
                c.FromBlock >= 0 && c.FromBlock < 4 && c.ToBlock >= 0 && c.ToBlock < 4) &&
                Link(assembly, "A", "A.E.road", "B", "B.W.road") &&
                (recipe == "E" ? Link(assembly, "B", "B.E.road", "D", "D.W.road") &&
                    Link(assembly, "D", "D.S.road", "C", "C.N.road") :
                    Link(assembly, "B", "B.E.road", "C", "C.W.road") &&
                    Link(assembly, "C", "C.N.road", "D", "D.S.road")),
                "Exterior presentation requires the existing recognized port graph, with all contacts preserved.");
        }

        private static bool Block(StudyANativePlacedLocation b, int x, int y, int w, int h) =>
            b.OriginPx.x == x && b.OriginPx.y == y && b.ExtentPx.x == w && b.ExtentPx.y == h;

        private static bool Link(StudyANativeLocationAssembly a, string from, string fromPort, string to, string toPort) =>
            a.Connections.Any(c => (a.Blocks[c.FromBlock].LocationId == from && c.FromPortId == fromPort &&
                a.Blocks[c.ToBlock].LocationId == to && c.ToPortId == toPort) ||
                (a.Blocks[c.ToBlock].LocationId == from && c.ToPortId == fromPort &&
                a.Blocks[c.FromBlock].LocationId == to && c.FromPortId == toPort));

        private struct Interval
        {
            public readonly int Min, Max;
            public Interval(int min, int max) { Min = min; Max = max; }
        }

        private static void Exposed(List<StudyANativeExteriorSegment> output, string id, string side,
            int fixedPixel, int min, int max, RectInt[] floors)
        {
            var covered = new List<Interval>();
            foreach (var floor in floors)
            {
                bool touches = side == "W" ? floor.xMax == fixedPixel : side == "E" ? floor.xMin == fixedPixel :
                    side == "S" ? floor.yMax == fixedPixel : floor.yMin == fixedPixel;
                if (!touches) continue;
                bool vertical = side == "W" || side == "E";
                int low = Math.Max(min, vertical ? floor.yMin : floor.xMin), high = Math.Min(max, vertical ? floor.yMax : floor.xMax);
                if (high > low) covered.Add(new Interval(low, high));
            }
            int cursor = min;
            foreach (var interval in covered.OrderBy(i => i.Min).ThenBy(i => i.Max))
            {
                if (interval.Min > cursor) Segment(output, id, side, fixedPixel, cursor, interval.Min);
                cursor = Math.Max(cursor, interval.Max);
            }
            if (cursor < max) Segment(output, id, side, fixedPixel, cursor, max);
        }

        private static void Segment(List<StudyANativeExteriorSegment> output, string id, string side, int fixedPixel, int min, int max) =>
            output.Add(new StudyANativeExteriorSegment { LocationId = id, Side = side,
                Axis = side == "W" || side == "E" ? "y" : "x", FixedPx = fixedPixel, AlongMinPx = min, AlongMaxPx = max });

        private static StudyANativeExteriorPlacement Place(StudyANativeExteriorPresentationProfile profile,
            StudyANativeExteriorSegment segment, int index, int start, int length, int delta)
        {
            bool forward = segment.Side == "E" || segment.Side == "S";
            var source = new RectInt(profile.SourceRectPx.x, profile.SourceRectPx.y + (forward ? delta : PPU - delta - length), FaceWidth, length);
            var target = segment.Side == "E" ? new RectInt(segment.FixedPx, start, FaceWidth, length) :
                segment.Side == "W" ? new RectInt(segment.FixedPx - FaceWidth, start, FaceWidth, length) :
                segment.Side == "S" ? new RectInt(start, segment.FixedPx - FaceWidth, length, FaceWidth) :
                new RectInt(start, segment.FixedPx, length, FaceWidth);
            return new StudyANativeExteriorPlacement {
                SourceTexturePath = profile.SourceTexturePath, SourceTextureSHA256 = profile.SourceTextureSHA256,
                SourceTextureMetaSHA256 = profile.SourceTextureMetaSHA256, SourceExtentPx = profile.SourceExtentPx,
                SourceRectPx = source, TargetRectWorldPx = target,
                WorldCenterPx = new Vector2(target.x + target.width * .5f, target.y + target.height * .5f),
                RotationDegrees = segment.Side == "E" ? 0 : segment.Side == "S" ? 270 : segment.Side == "W" ? 180 : 90,
                SegmentIndex = index, AlongStartPx = start, LocationId = segment.LocationId, Side = segment.Side,
                // Perpendicular faces need a deterministic corner order, but
                // all remain below the original MapGround water(5+) and owned
                // shadow/contact layers(9+), including their floor overhang.
                SortingOrder = segment.Side == "S" ? 3 : segment.Side == "N" ? 4 : 2
            };
        }

        private static void CountCorners(List<StudyANativeExteriorSegment> segments, out int convex, out int concave)
        {
            convex = 0; concave = 0;
            foreach (var segment in segments)
            {
                Endpoints(segment, out var start, out var end, out var direction);
                StudyANativeExteriorSegment next = null;
                foreach (var candidate in segments)
                {
                    Endpoints(candidate, out var candidateStart, out var candidateEnd, out var candidateDirection);
                    if (candidateStart.x == end.x && candidateStart.y == end.y)
                    {
                        Need(next == null, "Exterior boundary branches at a source endpoint.");
                        next = candidate;
                    }
                }
                Need(next != null, "Exterior boundary must remain a closed orthogonal loop.");
                Endpoints(next, out var nextStart, out var nextEnd, out var nextDirection);
                int cross = direction.x * nextDirection.y - direction.y * nextDirection.x;
                if (cross > 0) convex++; else if (cross < 0) concave++;
            }
            Need(convex - concave == 4, "The recognized connected exterior must have one closed outer boundary and no holes.");
        }

        private static void Endpoints(StudyANativeExteriorSegment s, out Vector2Int start, out Vector2Int end, out Vector2Int direction)
        {
            if (s.Side == "S") { start = new Vector2Int(s.AlongMinPx, s.FixedPx); end = new Vector2Int(s.AlongMaxPx, s.FixedPx); direction = new Vector2Int(1, 0); }
            else if (s.Side == "E") { start = new Vector2Int(s.FixedPx, s.AlongMinPx); end = new Vector2Int(s.FixedPx, s.AlongMaxPx); direction = new Vector2Int(0, 1); }
            else if (s.Side == "N") { start = new Vector2Int(s.AlongMaxPx, s.FixedPx); end = new Vector2Int(s.AlongMinPx, s.FixedPx); direction = new Vector2Int(-1, 0); }
            else { start = new Vector2Int(s.FixedPx, s.AlongMaxPx); end = new Vector2Int(s.FixedPx, s.AlongMinPx); direction = new Vector2Int(0, -1); }
        }

        private static bool CoveredBy(RectInt target, RectInt[] disjointRects)
        {
            long area = 0;
            foreach (var rect in disjointRects)
            {
                int w = Math.Max(0, Math.Min(target.xMax, rect.xMax) - Math.Max(target.xMin, rect.xMin));
                int h = Math.Max(0, Math.Min(target.yMax, rect.yMax) - Math.Max(target.yMin, rect.yMin));
                area += (long)w * h;
            }
            return area == (long)target.width * target.height;
        }

        private static bool Overlap(RectInt a, RectInt b) => a.xMin < b.xMax && b.xMin < a.xMax && a.yMin < b.yMax && b.yMin < a.yMax;
        private static bool Same(RectInt a, RectInt b) => a.x == b.x && a.y == b.y && a.width == b.width && a.height == b.height;
        private static bool Same(RectInt[] a, RectInt[] b) => a != null && b != null && a.Length == b.Length && a.Where((r, i) => !Same(r, b[i])).Count() == 0;
        private static void Need(bool valid, string message) { if (!valid) throw new ArgumentException(message); }
    }
}
