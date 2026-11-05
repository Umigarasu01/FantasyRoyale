using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace FantasyRoyale.MapAuthoringKit.OverheadStudyA
{
    /// <summary>
    /// Explicit B-J-D / D-C pilot. Original B/C/D rectangles and owner registries
    /// stay intact. J's binary floor, not soil colour or its bounding box, supplies
    /// floor/navigation/perimeter geometry. No scene objects or source edits.
    /// Accepted-E pixel/art preflight remains the caller's responsibility.
    /// </summary>
    public static class StudyANativeFlexibleConnectionPlan
    {
        public const int PixelsPerUnit = 64, ConnectorHeightPx = 384, CollarPx = 32;
        public const string Schema = "FantasyRoyale.NativeFlexibleConnectionPlan.v1";
        private const int FloorFanPx = 80, SoilFanPx = 32;
        private const string GeometrySchema = "FantasyRoyale.NativeFlexibleFloorGeometry.v1";
        private const string CDPortKey = "native-rgba:4dcf54b828755fea2d96e8c9751ecc2da2c6a2399df0014a21775046aedf94a1";

        [Serializable]
        public sealed class Plan
        {
            public string SchemaId = Schema;
            public int Version = 1, Seed, PixelsPerUnit = 64, ConnectorLengthPx, ConnectorCollarPx = 32;
            public Vector2Int ConnectorOriginPx, ConnectorExtentPx;
            public RectInt FramePx;
            // Each mask is x+y*ConnectorExtentPx.x, bottom-left native pixels, 0/1.
            public byte[] FloorMaskBL, SoilMaskBL;
            public float[] SoilCentreYByXBL;
            public RectInt[] JFloorRunsLocalPx, FloorsPx, PhysicalPerimeterPx, VoidInsideFramePx;
            public StudyANativeLocationAssembly Assembly;
            public StudyANativePlacedLocation[] CoreBlocks;
            public StudyANativeLocationPort[] ConnectorPorts;
            public string[] SourceOwnerIds;
            public StudyANativeFootUnit[] FootUnits;
            public string FloorMaskSHA256, SoilMaskSHA256, GeometryHash, ContentHash;
            // FloorPixelCount counts J only; TotalFloorPixelCount includes B/C/D.
            public int FloorPixelCount, TotalFloorPixelCount, JFloorRunCount, UnionScanPixelCount, PerimeterPixelCount, VoidPixelCount;
            public int MinimumInteriorFloorWidthPx, MaximumInteriorFloorWidthPx;
            public string Qualification = "Pure fixed-length pilot only; source art admission and actual Unity/Physics/GPU require separate evidence. Soil mask is a road reservation, never a physics gate. Seed is reproduction metadata; fixed length is the only shape choice in v1.";
            [NonSerialized] public StudyANativeLocationGeometry.Plan Geometry;
        }

        public static Plan Build(StudyANativeLocationCatalog admittedCatalog, int connectorLengthPx, int seed = 0)
        {
            Need(connectorLengthPx == 256 || connectorLengthPx == 384 || connectorLengthPx == 512,
                "Flexible pilot accepts explicit native connector lengths 256/384/512 only.");
            Need(admittedCatalog != null && admittedCatalog.Locations != null, "An admitted native catalog is required.");
            var selected = new[] { "B", "C", "D" };
            var defs = new Dictionary<string, StudyANativeLocationDefinition>(StringComparer.Ordinal);
            foreach (var id in selected)
            {
                var matches = admittedCatalog.Locations.Where(d => d != null && d.Id == id).ToArray();
                Need(matches.Length == 1, "Pilot needs exactly one source definition for " + id);
                defs.Add(id, matches[0]);
            }
            // Keep the production native catalog/owner/foot guards. A one-block
            // probe avoids asking the existing direct-edge generator for a gap.
            var probe = new StudyANativeLocationAssembly { CatalogVersion = admittedCatalog.Version };
            probe.Blocks.Add(Block("B", Vector2Int.zero, defs["B"].ExtentPx));
            var errors = StudyABlockGenerator.ValidateNativeAssembly(admittedCatalog, probe);
            Need(errors.Count == 0, "Admitted native source contract is invalid: " + string.Join("; ", errors));
            Need(defs["B"].ExtentPx == new Vector2Int(384, 640) && defs["C"].ExtentPx == new Vector2Int(512, 384) &&
                defs["D"].ExtentPx == new Vector2Int(640, 512), "Pilot preserves original B/C/D native extents.");
            foreach (var d in defs.Values)
                Need(IsSHA(d.PrefabSHA256) && IsSHA(d.GroundSHA256), "Explicit source prefab/ground SHA256 is required: " + d.Id);
            var be = RequirePort(defs["B"], "B.E.road", StudyAPortDirection.East, new Vector2Int(384, 297), 64, -32);
            var dw = RequirePort(defs["D"], "D.W.road", StudyAPortDirection.West, new Vector2Int(0, 169), 64, -32);
            var cn = RequirePort(defs["C"], "C.N.road", StudyAPortDirection.North, new Vector2Int(384, 384), 96, -48);
            var ds = RequirePort(defs["D"], "D.S.road", StudyAPortDirection.South, new Vector2Int(384, 0), 96, -48);
            Need(be.CompatibilityKey == StudyABlockGenerator.NativeEPortCompatibilityKey && dw.CompatibilityKey == be.CompatibilityKey,
                "B_E and D_W must retain the admitted E port profile.");
            Need(cn.CompatibilityKey == CDPortKey && ds.CompatibilityKey == CDPortKey, "Preserve the admitted C-N/D-S source profile.");
            Need(admittedCatalog.SharedEdgeMatches.Any(m => m.LengthPx == 512 && m.FromAlongMinPx == 0 && m.ToAlongMinPx == 0 &&
                (m.FromLocationId == "C" && m.ToLocationId == "D" && m.FromDirection == StudyAPortDirection.North ||
                 m.FromLocationId == "D" && m.ToLocationId == "C" && m.FromDirection == StudyAPortDirection.South)),
                "Preserve the certified full512px C-N/D-S shared edge.");

            int length = connectorLengthPx;
            var b = Block("B", new Vector2Int(0, 384), defs["B"].ExtentPx);
            var j = Block("J", new Vector2Int(384, 384), new Vector2Int(length, ConnectorHeightPx));
            var dBlock = Block("D", new Vector2Int(length + 384, 384), defs["D"].ExtentPx);
            var c = Block("C", new Vector2Int(length + 384, 0), defs["C"].ExtentPx);
            var plan = new Plan { Seed = seed, ConnectorLengthPx = length, ConnectorOriginPx = j.OriginPx,
                ConnectorExtentPx = j.ExtentPx, FramePx = new RectInt(0, 0, length + 1024, 1024),
                FloorMaskBL = new byte[length * ConnectorHeightPx], SoilMaskBL = new byte[length * ConnectorHeightPx],
                SoilCentreYByXBL = new float[length], CoreBlocks = new[] { b, c, dBlock },
                ConnectorPorts = new[] { ClonePort(be, "J.W.road", StudyAPortDirection.West, new Vector2Int(0, 297)),
                    ClonePort(dw, "J.E.road", StudyAPortDirection.East, new Vector2Int(length, 169)) } };
            BuildConnectorMasks(plan);
            plan.JFloorRunsLocalPx = MergeMask(plan.FloorMaskBL, length, ConnectorHeightPx, Vector2Int.zero);
            plan.JFloorRunCount = plan.JFloorRunsLocalPx.Length;
            var floors = new List<RectInt> { Rect(b), Rect(c), Rect(dBlock) };
            floors.AddRange(plan.JFloorRunsLocalPx.Select(r => new RectInt(r.position + j.OriginPx, r.size)));
            plan.FloorsPx = floors.OrderBy(r => r.y).ThenBy(r => r.x).ThenBy(r => r.width).ThenBy(r => r.height).ToArray();
            BuildUnionGeometry(plan);
            plan.Geometry = new StudyANativeLocationGeometry.Plan(plan.FramePx, plan.FloorsPx,
                plan.PhysicalPerimeterPx, plan.VoidInsideFramePx, PixelsPerUnit);
            plan.Assembly = new StudyANativeLocationAssembly { Seed = seed, CatalogVersion = 1,
                Blocks = new List<StudyANativePlacedLocation> { b, j, dBlock, c },
                Connections = new List<StudyANativeLocationConnection> {
                    new StudyANativeLocationConnection { FromBlock = 0, FromPortId = "B.E.road", ToBlock = 1, ToPortId = "J.W.road" },
                    new StudyANativeLocationConnection { FromBlock = 1, FromPortId = "J.E.road", ToBlock = 2, ToPortId = "D.W.road" },
                    new StudyANativeLocationConnection { FromBlock = 2, FromPortId = "D.S.road", ToBlock = 3, ToPortId = "C.N.road" } },
                NormalizedBoundsPx = plan.FramePx,
                FixedFullCatalogLayoutQualification = "Explicit B/J/D/C flexible pilot; J actual floor differs from its placement bounding box. No source rotation/scale, T branch or new direction art." };
            // Preserve the original selected-catalog sequence, never re-sort feet.
            var ordered = admittedCatalog.Locations.Where(x => selected.Contains(x.Id)).ToArray();
            plan.SourceOwnerIds = ordered.SelectMany(x => x.SourceOwnerIds).ToArray();
            plan.FootUnits = ordered.SelectMany(x => x.FootUnits).Select(x => new StudyANativeFootUnit {
                OwnerId = x.OwnerId, PartPath = x.PartPath, TieOrder = x.TieOrder }).ToArray();
            Need(plan.SourceOwnerIds.Distinct(StringComparer.Ordinal).Count() == plan.SourceOwnerIds.Length, "Selected core owner IDs must remain unique.");
            plan.FloorMaskSHA256 = Hash(plan.FloorMaskBL); plan.SoilMaskSHA256 = Hash(plan.SoilMaskBL);
            plan.GeometryHash = HashGeometry(plan);
            plan.ContentHash = HashContent(plan, defs);
            plan.Assembly.ContentHash = plan.ContentHash;
            return plan;
        }

        private static void BuildConnectorMasks(Plan plan)
        {
            int width = plan.ConnectorExtentPx.x, height = plan.ConnectorExtentPx.y;
            int minWidth = int.MaxValue, maxWidth = 0;
            for (int x = 0; x < width; x++)
            {
                float pixelX = x + .5f;
                float t = Mathf.Clamp01((pixelX - CollarPx) / (width - 2f * CollarPx));
                float centre = Mathf.Lerp(297f, 169f, Smooth(t));
                float endDistance = Mathf.Min(pixelX, width - pixelX);
                float floorFan = Smooth(Mathf.Clamp01((endDistance - CollarPx) / FloorFanPx));
                int innerWidth = Mathf.RoundToInt(192f + 64f * (4f * t * (1f - t)));
                int targetLow = Mathf.Clamp(Mathf.RoundToInt(centre - innerWidth * .5f), 0, height - innerWidth);
                int low = Mathf.RoundToInt(targetLow * floorFan);
                int high = Mathf.RoundToInt(Mathf.Lerp(height, targetLow + innerWidth, floorFan));
                if (x < CollarPx || x >= width - CollarPx) { low = 0; high = height; }
                Need(low >= 0 && low < high && high <= height, "Invalid connector floor interval.");
                if (endDistance >= CollarPx + FloorFanPx)
                { minWidth = Math.Min(minWidth, high - low); maxWidth = Math.Max(maxWidth, high - low); }
                float soilFan = Smooth(Mathf.Clamp01((endDistance - CollarPx) / SoilFanPx));
                float soilWidth = Mathf.Lerp(64f, 96f, soilFan);
                plan.SoilCentreYByXBL[x] = centre;
                for (int y = low; y < high; y++)
                {
                    int at = x + y * width; plan.FloorMaskBL[at] = 1; plan.FloorPixelCount++;
                    float pixelY = y + .5f;
                    if (pixelY >= centre - soilWidth * .5f && pixelY < centre + soilWidth * .5f)
                        plan.SoilMaskBL[at] = 1;
                }
            }
            Need(minWidth >= 192 && maxWidth <= 256, "Interior floor ribbon must stay within the explicit 192..256px candidate range.");
            plan.MinimumInteriorFloorWidthPx = minWidth; plan.MaximumInteriorFloorWidthPx = maxWidth;
            for (int y = 0; y < height; y++)
                for (int x = 0; x < CollarPx; x++)
                {
                    Need(plan.FloorMaskBL[x + y * width] == 1 && plan.FloorMaskBL[width - 1 - x + y * width] == 1,
                        "All384px endpoint floor collars must stay intact.");
                    Need(plan.SoilMaskBL[x + y * width] == (y >= 265 && y < 329 ? 1 : 0) &&
                        plan.SoilMaskBL[width - 1 - x + y * width] == (y >= 137 && y < 201 ? 1 : 0),
                        "Endpoint soil reservations must retain64px native mouth bands.");
                }
        }

        private static void BuildUnionGeometry(Plan plan)
        {
            // The bounded pilot scans fewer than two million native cells.
            // Rect count does not multiply cost: raster union + summed-area
            // dilation + row-runs are all O(native area), with no edge cross-product.
            int width = plan.FramePx.width + 2 * PixelsPerUnit, height = plan.FramePx.height + 2 * PixelsPerUnit;
            var origin = new Vector2Int(-PixelsPerUnit, -PixelsPerUnit);
            var floor = new byte[checked(width * height)];
            foreach (var block in plan.CoreBlocks)
            {
                int x0 = block.OriginPx.x - origin.x, y0 = block.OriginPx.y - origin.y;
                for (int y = 0; y < block.ExtentPx.y; y++)
                    for (int x = 0; x < block.ExtentPx.x; x++) floor[x0 + x + (y0 + y) * width] = 1;
            }
            int jw = plan.ConnectorExtentPx.x;
            int jx = plan.ConnectorOriginPx.x - origin.x, jy = plan.ConnectorOriginPx.y - origin.y;
            for (int y = 0; y < ConnectorHeightPx; y++)
                for (int x = 0; x < jw; x++) if (plan.FloorMaskBL[x + y * jw] != 0) floor[jx + x + (jy + y) * width] = 1;

            int stride = width + 1;
            var sum = new int[checked(stride * (height + 1))];
            for (int y = 0; y < height; y++)
            {
                int rowTotal = 0;
                for (int x = 0; x < width; x++)
                { rowTotal += floor[x + y * width]; sum[x + 1 + (y + 1) * stride] = sum[x + 1 + y * stride] + rowTotal; }
            }
            var perimeter = new byte[floor.Length]; var voids = new byte[floor.Length];
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    int at = x + y * width;
                    if (floor[at] != 0) { plan.TotalFloorPixelCount++; continue; }
                    int x0 = Math.Max(0, x - PixelsPerUnit), y0 = Math.Max(0, y - PixelsPerUnit);
                    int x1 = Math.Min(width, x + PixelsPerUnit + 1), y1 = Math.Min(height, y + PixelsPerUnit + 1);
                    int count = sum[x1 + y1 * stride] - sum[x0 + y1 * stride] - sum[x1 + y0 * stride] + sum[x0 + y0 * stride];
                    if (count != 0) { perimeter[at] = 1; plan.PerimeterPixelCount++; }
                    int worldX = x + origin.x, worldY = y + origin.y;
                    if (worldX >= 0 && worldX < plan.FramePx.width && worldY >= 0 && worldY < plan.FramePx.height)
                    { voids[at] = 1; plan.VoidPixelCount++; }
                }
            plan.UnionScanPixelCount = floor.Length;
            plan.PhysicalPerimeterPx = MergeMask(perimeter, width, height, origin);
            plan.VoidInsideFramePx = MergeMask(voids, width, height, origin);
        }

        private struct RunKey : IEquatable<RunKey>
        {
            public readonly int Start, End;
            public RunKey(int start, int end) { Start = start; End = end; }
            public bool Equals(RunKey value) => Start == value.Start && End == value.End;
            public override bool Equals(object value) => value is RunKey key && Equals(key);
            public override int GetHashCode() { unchecked { return (Start * 397) ^ End; } }
        }

        private static RectInt[] MergeMask(byte[] mask, int width, int height, Vector2Int origin)
        {
            var complete = new List<RectInt>(); var active = new Dictionary<RunKey, RectInt>();
            for (int y = 0; y < height; y++)
            {
                var next = new Dictionary<RunKey, RectInt>(); int x = 0;
                while (x < width)
                {
                    if (mask[x + y * width] == 0) { x++; continue; }
                    int start = x++;
                    while (x < width && mask[x + y * width] != 0) x++;
                    var key = new RunKey(start, x);
                    if (active.TryGetValue(key, out var previous))
                        next.Add(key, new RectInt(previous.x, previous.y, previous.width, checked(previous.height + 1)));
                    else next.Add(key, new RectInt(origin.x + start, origin.y + y, x - start, 1));
                }
                foreach (var old in active) if (!next.ContainsKey(old.Key)) complete.Add(old.Value);
                active = next;
            }
            complete.AddRange(active.Values);
            return complete.OrderBy(r => r.y).ThenBy(r => r.x).ThenBy(r => r.width).ThenBy(r => r.height).ToArray();
        }

        private static StudyANativeLocationPort RequirePort(StudyANativeLocationDefinition source, string id,
            StudyAPortDirection direction, Vector2Int anchor, int width, int offset)
        {
            var ports = source.Ports.Where(p => p.Id == id).ToArray();
            Need(ports.Length == 1, "Missing/duplicate admitted port: " + id);
            var port = ports[0];
            Need(port.Direction == direction && port.AnchorPx == anchor && port.MouthWidthPx == width &&
                port.AlongMinOffsetPx == offset && port.Height == 0 && port.Terrain == StudyATerrain.Soil,
                "Original port anchor/band/direction/terrain/height changed: " + id);
            return port;
        }

        private static StudyANativeLocationPort ClonePort(StudyANativeLocationPort source, string id,
            StudyAPortDirection direction, Vector2Int anchor) => new StudyANativeLocationPort { Id = id,
                Direction = direction, AnchorPx = anchor, AlongMinOffsetPx = source.AlongMinOffsetPx,
                MouthWidthPx = source.MouthWidthPx, Terrain = source.Terrain, CompatibilityKey = source.CompatibilityKey, Height = source.Height };
        private static StudyANativePlacedLocation Block(string id, Vector2Int origin, Vector2Int extent) =>
            new StudyANativePlacedLocation { LocationId = id, InstanceId = id, OriginPx = origin, ExtentPx = extent };
        private static RectInt Rect(StudyANativePlacedLocation block) => new RectInt(block.OriginPx, block.ExtentPx);
        private static float Smooth(float t) => t * t * (3f - 2f * t);
        private static bool IsSHA(string value) => value != null && value.Length == 64 && value.All(Uri.IsHexDigit);
        private static void Need(bool condition, string message) { if (!condition) throw new ArgumentException(message); }
        private static string Hash(byte[] bytes) { using (var sha = SHA256.Create()) return string.Concat(sha.ComputeHash(bytes).Select(b => b.ToString("x2", CultureInfo.InvariantCulture))); }
        private static void Field(StringBuilder b, string value) { value = value ?? ""; b.Append(value.Length.ToString(CultureInfo.InvariantCulture)).Append(':').Append(value).Append('|'); }
        private static void Rects(StringBuilder b, RectInt[] rects)
        { foreach (var r in rects) b.Append(r.x.ToString(CultureInfo.InvariantCulture)).Append(',').Append(r.y.ToString(CultureInfo.InvariantCulture)).Append(',').Append(r.width.ToString(CultureInfo.InvariantCulture)).Append(',').Append(r.height.ToString(CultureInfo.InvariantCulture)).Append('|'); }
        private static string HashGeometry(Plan plan)
        {
            var b = new StringBuilder(); Field(b, GeometrySchema); Field(b, plan.FloorMaskSHA256);
            Rects(b, new[] { plan.FramePx }); Rects(b, plan.FloorsPx); Rects(b, plan.PhysicalPerimeterPx); Rects(b, plan.VoidInsideFramePx);
            return Hash(Encoding.UTF8.GetBytes(b.ToString()));
        }
        private static string HashContent(Plan plan, Dictionary<string, StudyANativeLocationDefinition> defs)
        {
            var b = new StringBuilder(); Field(b, Schema); Field(b, plan.GeometryHash); Field(b, plan.SoilMaskSHA256);
            foreach (var block in plan.CoreBlocks.OrderBy(x => x.LocationId, StringComparer.Ordinal))
            {
                var definition = defs[block.LocationId]; Field(b, block.LocationId); Field(b, definition.PrefabSHA256); Field(b, definition.GroundSHA256);
                Rects(b, new[] { Rect(block) });
                Field(b, definition.SpawnPx.x.ToString("R", CultureInfo.InvariantCulture)); Field(b, definition.SpawnPx.y.ToString("R", CultureInfo.InvariantCulture));
                foreach (var owner in definition.SourceOwnerIds) Field(b, owner);
                foreach (var foot in definition.FootUnits)
                { Field(b, foot.OwnerId); Field(b, foot.PartPath); Field(b, foot.TieOrder.ToString(CultureInfo.InvariantCulture)); }
                foreach (var port in definition.Ports.OrderBy(x => x.Id, StringComparer.Ordinal))
                { Field(b, port.Id); Field(b, port.CompatibilityKey); b.Append(((int)port.Direction).ToString(CultureInfo.InvariantCulture)).Append(',').Append(port.AnchorPx.x.ToString(CultureInfo.InvariantCulture)).Append(',').Append(port.AnchorPx.y.ToString(CultureInfo.InvariantCulture)).Append(',').Append(port.MouthWidthPx.ToString(CultureInfo.InvariantCulture)).Append(',').Append(port.AlongMinOffsetPx.ToString(CultureInfo.InvariantCulture)).Append(',').Append(((int)port.Terrain).ToString(CultureInfo.InvariantCulture)).Append(',').Append(port.Height.ToString(CultureInfo.InvariantCulture)).Append('|'); }
            }
            foreach (var link in plan.Assembly.Connections)
            { Field(b, plan.Assembly.Blocks[link.FromBlock].LocationId); Field(b, link.FromPortId); Field(b, plan.Assembly.Blocks[link.ToBlock].LocationId); Field(b, link.ToPortId); }
            return Hash(Encoding.UTF8.GetBytes(b.ToString()));
        }
    }
}
