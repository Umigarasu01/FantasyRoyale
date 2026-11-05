using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using FantasyRoyale.MapAuthoringKit.OverheadStudyA;
using UnityEngine;

namespace FantasyRoyale.MapAuthoringKit.Editor
{
    [Serializable]
    public sealed class StudyABridgeRiverContourPolygon
    {
        public string BlockId;
        public Vector2Int Origin, Extent;
        public int PixelStep = 8;
        public Vector2[] Points;
    }

    public sealed class StudyABridgeRiverFinePlan
    {
        public StudyABlockAssembly Assembly;
        public Vector2Int Origin;
        public int Width, Height, PixelsPerUnit = 64;
        // Bottom-origin native Texture2D order; all masks contain only0 or255.
        public byte[] VisibleWaterMask, WaterCollisionMask, FineFreeWalkMask;
        public StudyABridgeRiverContourPolygon[] Polygons;
        public Rect[] DeckBounds, ColliderRectangles;
        public int BridgeStructureVersion;
        public byte[] BridgeBodyCollisionMask;
        public Rect[] BridgeBodyRectangles;
        public string BridgeBodyCollisionSha256;
        public Vector2Int[] RetiredMacroWaterCells, RetainedSolidCells;
        public string VisibleWaterSha256, WaterCollisionSha256, FineFreeWalkSha256;
        public Rect WorldBounds => new Rect(Origin.x, Origin.y, Width / (float)PixelsPerUnit, Height / (float)PixelsPerUnit);
        public bool WaterIntersectsRect(Rect rect) => Intersects(VisibleWaterMask, rect);
        public bool CollisionIntersectsRect(Rect rect) => Intersects(WaterCollisionMask, rect);
        public bool IsFreeRect(Rect rect)
        {
            if (!Range(rect, out var x0, out var y0, out var x1, out var y1)) return false;
            for (var y = y0; y < y1; y++) for (var x = x0; x < x1; x++) if (FineFreeWalkMask[y * Width + x] != 255) return false;
            return true;
        }
        private bool Intersects(byte[] mask, Rect rect)
        {
            if (!Range(rect, out var x0, out var y0, out var x1, out var y1)) return true;
            for (var y = y0; y < y1; y++) for (var x = x0; x < x1; x++) if (mask[y * Width + x] != 0) return true;
            return false;
        }
        private bool Range(Rect r, out int x0, out int y0, out int x1, out int y1)
        {
            x0 = Mathf.FloorToInt((r.xMin - Origin.x) * PixelsPerUnit + .00001f);
            y0 = Mathf.FloorToInt((r.yMin - Origin.y) * PixelsPerUnit + .00001f);
            x1 = Mathf.CeilToInt((r.xMax - Origin.x) * PixelsPerUnit - .00001f);
            y1 = Mathf.CeilToInt((r.yMax - Origin.y) * PixelsPerUnit - .00001f);
            return r.width > 0 && r.height > 0 && x0 >= 0 && y0 >= 0 && x1 <= Width && y1 <= Height;
        }
    }

    public sealed class StudyABridgeRiverFineBinding
    {
        public GameObject Root;
        public Rigidbody2D Body;
        public CompositeCollider2D Composite;
        public BoxCollider2D[] SourceBoxes;
    }

    /// <summary>One binary closed water raster drives visible water and water collision minus the full deck aperture.</summary>
    public static class StudyABridgeRiverFineGeometry
    {
        public const int PixelsPerUnit = 64;
        public const string GeometryMode = "FineWaterSharedRenderAndCollision";
        public const string ColliderRootName = "FineWaterCollision";
        private const float PortDepth = .75f;

        public static StudyABridgeRiverContourPolygon DefaultPolygon(StudyABlockDefinition definition, Vector2Int origin)
        {
            RequireBridgeDefinition(definition);
            var south = WaterPort(definition, StudyAPortDirection.South).Position.x + 1f;
            var north = WaterPort(definition, StudyAPortDirection.North).Position.x + 1f;
            // Nominal horizontal width2. The two banks share a gentle centerline; this is not normal-width2.
            // Retain the boundary solid row around each opening; bends begin beyond its full1u depth.
            var ys = new[] { 0f, .75f, 1.125f, 1.75f, 2.25f, 3f, 3.75f, 4f, 7f, 7.5f, 8.25f, 9f, 9.75f, 10.5f, 10.875f, 11.25f, 12f };
            var xs = new[] { south, south, south, south - .25f, south - .125f, south + .375f, 5.875f, 6f, 6f, 6.125f, 6.25f, 5.875f, 5.375f, north + .125f, north, north, north };
            var points = new List<Vector2>();
            for (var i = 0; i < ys.Length; i++) points.Add(new Vector2(xs[i] - 1f, ys[i]));
            for (var i = ys.Length - 1; i >= 0; i--) points.Add(new Vector2(xs[i] + 1f, ys[i]));
            return new StudyABridgeRiverContourPolygon { BlockId = definition.Id, Origin = origin, Extent = definition.Extent, PixelStep = 8, Points = points.ToArray() };
        }

        public static StudyABridgeRiverContour CreateDefaultContour(GameObject nativeRoot, StudyABlockDefinition definition)
        {
            RequireUnitTransform(nativeRoot.transform);
            if (nativeRoot.GetComponentsInChildren<StudyABridgeRiverContour>(true).Length != 0) throw new InvalidOperationException("Native river controls already exist; refusing replacement.");
            var polygon = DefaultPolygon(definition, Vector2Int.zero);
            var contour = nativeRoot.AddComponent<StudyABridgeRiverContour>(); contour.BlockId = polygon.BlockId;
            contour.Extent = polygon.Extent; contour.PixelStep = polygon.PixelStep; contour.Points = polygon.Points.ToList(); return contour;
        }
        public static StudyABridgeRiverContourPolygon ReadPolygon(StudyABlockDefinition definition, Vector2Int origin, StudyABridgeRiverContour contour)
        {
            RequireBridgeDefinition(definition);
            if (contour == null || contour.Points == null || contour.BlockId != definition.Id || contour.Extent != definition.Extent)
                throw new InvalidDataException("Explicit native river contour/identity/extent is required.");
            RequireUnitTransform(contour.transform);
            return new StudyABridgeRiverContourPolygon { BlockId = definition.Id, Origin = origin, Extent = definition.Extent,
                PixelStep = contour.PixelStep, Points = contour.Points.ToArray() };
        }
        public static StudyABridgeRiverContourPolygon[] ReadPolygons(StudyABlockAssembly assembly, IEnumerable<GameObject> nativePrefabs)
        {
            var prefabs = (nativePrefabs ?? Array.Empty<GameObject>()).Where(p => p != null).Distinct().ToArray();
            return assembly.Blocks.Where(b => b.Definition.Kind == StudyABlockKind.BridgeBank).Select(block =>
            {
                var prefab = prefabs.SingleOrDefault(p => p.GetComponent<StudyABlockPrefabData>()?.Definition.Kind == StudyABlockKind.BridgeBank);
                if (prefab == null) return DefaultPolygon(block.Definition, block.Origin); // caller has no existing native source for this fresh default
                var contours = prefab.GetComponentsInChildren<StudyABridgeRiverContour>(true);
                if (contours.Length != 1 || contours[0].transform != prefab.transform) throw new InvalidDataException("Existing native source requires exactly one root river contour; no silent default.");
                return ReadPolygon(block.Definition, block.Origin, contours[0]);
            }).ToArray();
        }
        public static StudyABridgeRiverContourPolygon[] ReadScenePolygons(Transform mapRoot)
        {
            RequireUnitTransform(mapRoot);
            return mapRoot.GetComponentsInChildren<StudyABridgeRiverContour>(true).Select(c =>
            {
                RequireUnitTransform(c.transform); var position = (Vector2)c.transform.position; var origin = Vector2Int.RoundToInt(position);
                if (Vector2.Distance(position, origin) > .00001f) throw new InvalidDataException("Saved river control origin is not an integer translation.");
                return new StudyABridgeRiverContourPolygon { BlockId = c.BlockId, Origin = origin, Extent = c.Extent, PixelStep = c.PixelStep, Points = c.Points.ToArray() };
            }).OrderBy(p => p.BlockId, StringComparer.Ordinal).ToArray();
        }

        public static List<string> ValidatePolygon(StudyABridgeRiverContourPolygon polygon)
        {
            var errors = new List<string>();
            if (polygon == null) { errors.Add("River polygon is missing."); return errors; }
            errors.AddRange(StudyASoilContourRenderer.ValidatePolygon(new StudyASoilContourPolygon
            { BlockId = polygon.BlockId, Origin = polygon.Origin, Extent = polygon.Extent, PixelStep = polygon.PixelStep, Points = polygon.Points }));
            if (string.IsNullOrWhiteSpace(polygon.BlockId)) errors.Add("River block identity is missing.");
            return errors;
        }
        // Even/odd half-open scanline fill: left boundary included/right excluded; no double-counted2u rows.
        public static bool ContainsPoint(Vector2[] points, double x, double y)
        {
            var inside = false;
            for (int i = 0, j = points.Length - 1; i < points.Length; j = i++)
            {
                var a = points[i]; var b = points[j];
                if ((a.y > y) != (b.y > y) && x < (b.x - (double)a.x) * (y - a.y) / (b.y - (double)a.y) + a.x) inside = !inside;
            }
            return inside;
        }
        public static byte[] Rasterize(IEnumerable<StudyABridgeRiverContourPolygon> controls, Vector2Int origin, int worldWidth, int worldHeight)
        {
            if (worldWidth <= 0 || worldHeight <= 0 || worldWidth > 64 || worldHeight > 64) throw new ArgumentException("Fine river frame must be positive and at most64 world units per axis.");
            var width = checked(worldWidth * PixelsPerUnit); var height = checked(worldHeight * PixelsPerUnit); var bytes = new byte[checked(width * height)];
            foreach (var polygon in controls)
            {
                var errors = ValidatePolygon(polygon); if (errors.Count > 0) throw new InvalidDataException(string.Join("; ", errors));
                var startX = checked((polygon.Origin.x - origin.x) * PixelsPerUnit); var startY = checked((polygon.Origin.y - origin.y) * PixelsPerUnit);
                var endX = checked(startX + polygon.Extent.x * PixelsPerUnit); var endY = checked(startY + polygon.Extent.y * PixelsPerUnit);
                if (startX < 0 || startY < 0 || endX > width || endY > height) throw new InvalidDataException("River native domain escaped the output frame.");
                var step = polygon.PixelStep;
                for (var y = startY; y < endY; y += step) for (var x = startX; x < endX; x += step)
                {
                    if (!ContainsPoint(polygon.Points, (x - startX + step * .5) / PixelsPerUnit, (y - startY + step * .5) / PixelsPerUnit)) continue;
                    for (var dy = 0; dy < step; dy++) for (var dx = 0; dx < step; dx++) bytes[(y + dy) * width + x + dx] = 255;
                }
            }
            return bytes;
        }

        public static StudyABridgeRiverFinePlan MakePlan(StudyABlockAssembly assembly, IEnumerable<StudyABridgeRiverContourPolygon> controls, int worldWidth = 24, int worldHeight = 16, int bridgeStructureVersion = 0)
        {
            if (bridgeStructureVersion < 0 || bridgeStructureVersion > 1) throw new ArgumentException("Unsupported bridge structure snapshot.");
            if (!SupportsAssembly(assembly, worldWidth, worldHeight))
                throw new ArgumentException("Fine water supports one BridgeBank alone, paired with one ForestPath, or the approved32x16 Plaza/ForestPath/BridgeBank chain.");
            var polygons = controls?.Select(Clone).ToArray();
            var assemblyErrors = assembly.Validate();
            if (assemblyErrors.Count > 0) throw new InvalidDataException(string.Join("; ", assemblyErrors));
            if (polygons == null || polygons.Length != 1) throw new InvalidDataException("Exactly one explicit BridgeBank closed water outline is required.");
            var bridge = assembly.Blocks.Single(b => b.Definition.Kind == StudyABlockKind.BridgeBank); RequireBridgeDefinition(bridge.Definition);
            var p = polygons[0]; if (p.BlockId != bridge.Definition.Id || p.Origin != bridge.Origin || p.Extent != bridge.Definition.Extent) throw new InvalidDataException("River controls differ from the assembled native block.");
            if (assembly.SolidCells.Overlaps(assembly.WaterCells)) throw new InvalidDataException("Native Solid intersects old Water; authored occupancy cannot be silently retired as water.");
            if (assembly.Blocks.Any(b => b != bridge && (b.Definition.WaterCells.Count != 0 || b.Definition.BridgeFloorCells.Count != 0))) throw new InvalidDataException("Non-BridgeBank authored water is outside this fine mode.");
            var visible = Rasterize(polygons, Vector2Int.zero, worldWidth, worldHeight);
            RequireConnected(visible, worldWidth * PixelsPerUnit, worldHeight * PixelsPerUnit);
            var plan = new StudyABridgeRiverFinePlan { Assembly = assembly, Origin = Vector2Int.zero, Width = worldWidth * PixelsPerUnit, Height = worldHeight * PixelsPerUnit,
                Polygons = polygons, VisibleWaterMask = visible, WaterCollisionMask = (byte[])visible.Clone(), FineFreeWalkMask = new byte[visible.Length],
                // The approved32x16 three-block composition needs one native north deck pixel
                // of physical clearance for the unchanged offset foot Capsule. Logical floor cells stay4x3.
                BridgeStructureVersion = bridgeStructureVersion,
                DeckBounds = bridgeStructureVersion == 1 ? new[] { new Rect(bridge.Origin.x + 4, bridge.Origin.y + 4.75f, 4, 1.5f) } :
                    new[] { new Rect(bridge.Origin.x + 4, bridge.Origin.y + 4, 4, assembly.Blocks.Count == 3 ? 3 + 1f / PixelsPerUnit : 3) },
                RetiredMacroWaterCells = assembly.WaterCells.Except(assembly.BridgeCells).OrderBy(c => c.y).ThenBy(c => c.x).ToArray(),
                RetainedSolidCells = assembly.SolidCells.OrderBy(c => c.y).ThenBy(c => c.x).ToArray() };
            foreach (var block in assembly.Blocks) FillRect(plan.FineFreeWalkMask, plan, new Rect(block.Origin.x, block.Origin.y, block.Definition.Extent.x, block.Definition.Extent.y), 255);
            foreach (var deck in plan.DeckBounds) FillRect(plan.WaterCollisionMask, plan, deck, 0);
            foreach (var cell in plan.RetainedSolidCells)
            {
                var r = new Rect(cell.x, cell.y, 1, 1);
                if (plan.WaterIntersectsRect(r)) throw new InvalidDataException("Fine water intersects preserved solid occupancy.");
                FillRect(plan.FineFreeWalkMask, plan, r, 0);
            }
            for (var i = 0; i < visible.Length; i++) if (plan.WaterCollisionMask[i] != 0) plan.FineFreeWalkMask[i] = 0;
            plan.ColliderRectangles = Rectangles(plan.WaterCollisionMask, plan.Width, plan.Height, plan.Origin);
            plan.BridgeBodyCollisionMask = bridgeStructureVersion == 1 ? StudyABridge01Structure.MakeBodyMask(plan) : new byte[visible.Length];
            plan.BridgeBodyRectangles = Rectangles(plan.BridgeBodyCollisionMask, plan.Width, plan.Height, plan.Origin);
            plan.BridgeBodyCollisionSha256 = Hash(plan.BridgeBodyCollisionMask);
            for (var i = 0; i < visible.Length; i++) if (plan.BridgeBodyCollisionMask[i] != 0) plan.FineFreeWalkMask[i] = 0;
            RequirePorts(plan, bridge); RequireRoutes(plan);
            plan.VisibleWaterSha256 = Hash(plan.VisibleWaterMask); plan.WaterCollisionSha256 = Hash(plan.WaterCollisionMask); plan.FineFreeWalkSha256 = Hash(plan.FineFreeWalkMask);
            return plan;
        }
        private static bool SupportsAssembly(StudyABlockAssembly assembly, int worldWidth, int worldHeight)
        {
            if (assembly == null || assembly.Blocks.Any(b => b == null || b.Definition == null)) return false;
            if (assembly.Blocks.Count <= 2)
                return assembly.Blocks.Count(b => b.Definition.Kind == StudyABlockKind.BridgeBank) == 1 &&
                    assembly.Blocks.All(b => b.Definition.Kind == StudyABlockKind.BridgeBank || b.Definition.Kind == StudyABlockKind.ForestPath);
            // This is one approved layout, not general admission of larger assemblies.
            if (worldWidth != 32 || worldHeight != 16 || assembly.Blocks.Count != 3 || assembly.Connections.Count != 2) return false;
            var kinds = new[] { StudyABlockKind.Plaza, StudyABlockKind.ForestPath, StudyABlockKind.BridgeBank };
            var extents = new[] { new Vector2Int(12, 12), new Vector2Int(8, 12), new Vector2Int(12, 12) };
            var origins = new[] { new Vector2Int(0, 2), new Vector2Int(12, 2), new Vector2Int(20, 2) };
            for (var i = 0; i < 3; i++)
                if (assembly.Blocks[i].Definition.Kind != kinds[i] || assembly.Blocks[i].Definition.Extent != extents[i] || assembly.Blocks[i].Origin != origins[i]) return false;
            for (var i = 0; i < 2; i++)
            {
                var link = assembly.Connections.FirstOrDefault(c => c != null && c.FromBlock == i && c.ToBlock == i + 1);
                if (link == null || link.FromPort < 0 || link.ToPort < 0 || link.FromPort >= assembly.Blocks[i].Definition.Ports.Count || link.ToPort >= assembly.Blocks[i + 1].Definition.Ports.Count) return false;
                var from = assembly.Blocks[i].Definition.Ports[link.FromPort]; var to = assembly.Blocks[i + 1].Definition.Ports[link.ToPort];
                if (from.Direction != StudyAPortDirection.East || to.Direction != StudyAPortDirection.West || from.Width != 3 || to.Width != 3 || !link.IsWalkable) return false;
            }
            // MakePlan calls assembly.Validate next, including exact port bands, terrain, height, centers and graph connectivity.
            return true;
        }
        private static StudyABridgeRiverContourPolygon Clone(StudyABridgeRiverContourPolygon p) => p == null ? null : new StudyABridgeRiverContourPolygon
        { BlockId = p.BlockId, Origin = p.Origin, Extent = p.Extent, PixelStep = p.PixelStep, Points = p.Points?.ToArray() };
        private static void RequireConnected(byte[] visible, int width, int height)
        {
            // Every fill is a multiple of8px. A four-neighbour traversal on that same minimum grid rejects rasterised dry gaps.
            const int step = 8; var columns = width / step; var rows = height / step;
            var filled = new HashSet<int>();
            for (var y = 0; y < rows; y++) for (var x = 0; x < columns; x++)
                if (visible[(y * step + step / 2) * width + x * step + step / 2] == 255) filled.Add(y * columns + x);
            if (filled.Count == 0) throw new InvalidDataException("River raster is empty.");
            var first = filled.First(); var seen = new HashSet<int> { first }; var queue = new Queue<int>(); queue.Enqueue(first);
            while (queue.Count > 0)
            {
                var cell = queue.Dequeue(); var x = cell % columns; var y = cell / columns;
                foreach (var next in new[] { x > 0 ? cell - 1 : -1, x + 1 < columns ? cell + 1 : -1, y > 0 ? cell - columns : -1, y + 1 < rows ? cell + columns : -1 })
                    if (filled.Contains(next) && seen.Add(next)) queue.Enqueue(next);
            }
            if (seen.Count != filled.Count) throw new InvalidDataException("River raster is disconnected at its authored sampling grid.");
        }
        private static void RequireBridgeDefinition(StudyABlockDefinition d)
        {
            if (d == null || d.Kind != StudyABlockKind.BridgeBank || d.Extent != new Vector2Int(12, 12) ||
                !new HashSet<Vector2Int>(d.BridgeFloorCells).SetEquals(from y in Enumerable.Range(4, 3) from x in Enumerable.Range(4, 4) select new Vector2Int(x, y)))
                throw new ArgumentException("Fine river requires native12x12 BridgeBank and exact deck[4,8]x[4,7].");
            WaterPort(d, StudyAPortDirection.North); WaterPort(d, StudyAPortDirection.South);
        }
        private static StudyABlockPort WaterPort(StudyABlockDefinition d, StudyAPortDirection direction)
        {
            var p = d.Ports.SingleOrDefault(port => port != null && port.Direction == direction && port.Terrain == StudyATerrain.Water);
            if (p == null || p.Width != 2 || p.IsWalkable || p.Position.y != (direction == StudyAPortDirection.South ? 0 : 11)) throw new ArgumentException("North/south native water ports must be nonwalking width2 at the boundary.");
            return p;
        }
        private static void RequirePorts(StudyABridgeRiverFinePlan plan, StudyAPlacedBlock bridge)
        {
            foreach (var dir in new[] { StudyAPortDirection.South, StudyAPortDirection.North })
            {
                var p = WaterPort(bridge.Definition, dir); var left = (bridge.Origin.x + p.Position.x - plan.Origin.x) * PixelsPerUnit;
                var startY = (bridge.Origin.y - plan.Origin.y) * PixelsPerUnit + (dir == StudyAPortDirection.South ? 0 : 12 * PixelsPerUnit - (int)(PortDepth * PixelsPerUnit));
                for (var y = startY; y < startY + PortDepth * PixelsPerUnit; y++) for (var x = bridge.Origin.x * PixelsPerUnit; x < (bridge.Origin.x + 12) * PixelsPerUnit; x++)
                {
                    var expected = x >= left && x < left + 2 * PixelsPerUnit ? 255 : 0;
                    if (plan.VisibleWaterMask[y * plan.Width + x] != expected || plan.WaterCollisionMask[y * plan.Width + x] != expected)
                        throw new InvalidDataException("Fine north/south water port is not exact2u through its first.75u.");
                }
            }
        }
        private static void RequireRoutes(StudyABridgeRiverFinePlan plan)
        {
            var blocked = plan.ColliderRectangles.Concat(plan.BridgeBodyRectangles ?? Array.Empty<Rect>()).Concat(plan.RetainedSolidCells.Select(c => new Rect(c.x, c.y, 1, 1))).ToArray();
            foreach (var route in plan.Assembly.MainRoutes)
            {
                if (route.Count == 0) throw new InvalidDataException("Actual Actor route is missing.");
                foreach (var point in route) if (!plan.IsFreeRect(ActorRect(point))) throw new InvalidDataException("Actor route/spawn footprint leaves authoritative fine free-walk mask.");
                for (var i = 1; i < route.Count; i++) RequireSweep(route[i - 1], route[i], blocked);
            }
            if (!plan.IsFreeRect(ActorRect(plan.Assembly.Spawn))) throw new InvalidDataException("Spawn Actor footprint is not fine free-walk.");
            foreach (var block in plan.Assembly.Blocks) foreach (var port in block.Definition.Ports.Where(q => q.IsWalkable && (q.Direction == StudyAPortDirection.West || q.Direction == StudyAPortDirection.East)))
            {
                if (port.Width != 3) throw new InvalidDataException("Fine mode preserves three-lane walking ports.");
                var x = port.Direction == StudyAPortDirection.West ? .5f : block.Definition.Extent.x - .5f;
                var innerX = port.Direction == StudyAPortDirection.West ? PortDepth : block.Definition.Extent.x - PortDepth;
                for (var lane = 0; lane < 3; lane++) RequireSweep(new Vector2(x, port.Position.y + lane + .5f) + (Vector2)block.Origin,
                    new Vector2(innerX, port.Position.y + lane + .5f) + (Vector2)block.Origin, blocked);
            }
            foreach (var connection in plan.Assembly.Connections.Where(c => c.IsWalkable))
                for (var lane = -1; lane <= 1; lane++) RequireSweep(connection.Center + new Vector2(-.5f, lane), connection.Center + new Vector2(.5f, lane), blocked);
        }
        private static Rect ActorRect(Vector2 foot) => new Rect(foot.x - .24f, foot.y + .22f - .275f, .48f, .55f);
        private static void RequireSweep(Vector2 a, Vector2 b, Rect[] blocked)
        {
            foreach (var r in blocked)
            {
                // Exact segment-versus-rect Minkowski test for the complete Actor box; not sampled points.
                var expanded = new Rect(r.xMin - .24f, r.yMin - .22f - .275f, r.width + .48f, r.height + .55f);
                if (SegmentRect(a, b, expanded)) throw new InvalidDataException("Continuous Actor body intersects fine water/retained solid at route or a three-lane aperture.");
            }
        }
        private static bool SegmentRect(Vector2 a, Vector2 b, Rect r)
        {
            double lo = 0, hi = 1; var dx = (double)b.x - a.x; var dy = (double)b.y - a.y;
            return Clip(-dx, a.x - (double)r.xMin, ref lo, ref hi) && Clip(dx, (double)r.xMax - a.x, ref lo, ref hi) &&
                Clip(-dy, a.y - (double)r.yMin, ref lo, ref hi) && Clip(dy, (double)r.yMax - a.y, ref lo, ref hi);
        }
        private static bool Clip(double p, double q, ref double lo, ref double hi)
        {
            if (p == 0) return q >= 0; var t = q / p;
            if (p < 0) { if (t > hi) return false; lo = Math.Max(lo, t); }
            else { if (t < lo) return false; hi = Math.Min(hi, t); } return true;
        }
        private static void FillRect(byte[] mask, StudyABridgeRiverFinePlan plan, Rect r, byte value)
        {
            var x0 = Mathf.RoundToInt((r.xMin - plan.Origin.x) * PixelsPerUnit); var y0 = Mathf.RoundToInt((r.yMin - plan.Origin.y) * PixelsPerUnit);
            var x1 = Mathf.RoundToInt((r.xMax - plan.Origin.x) * PixelsPerUnit); var y1 = Mathf.RoundToInt((r.yMax - plan.Origin.y) * PixelsPerUnit);
            if (x0 < 0 || y0 < 0 || x1 > plan.Width || y1 > plan.Height) throw new InvalidDataException("Physics/domain rectangle escaped output frame.");
            for (var y = y0; y < y1; y++) for (var x = x0; x < x1; x++) mask[y * plan.Width + x] = value;
        }
        // Coalesce identical horizontal pixel runs vertically. Their axis-aligned union is exactly the binary mask.
        public static Rect[] Rectangles(byte[] mask, int width, int height, Vector2Int origin)
        {
            if (mask == null || mask.Length != checked(width * height) || mask.Any(v => v != 0 && v != 255)) throw new ArgumentException("Collider requires the exact binary raster.");
            var output = new List<Rect>(); var active = new Dictionary<(int, int), (int y, int rows)>();
            for (var y = 0; y <= height; y++)
            {
                var row = new HashSet<(int, int)>();
                if (y < height) for (var x = 0; x < width;)
                {
                    if (mask[y * width + x] == 0) { x++; continue; } var start = x;
                    while (x < width && mask[y * width + x] == 255) x++; row.Add((start, x));
                }
                foreach (var key in active.Keys.Where(k => !row.Contains(k)).ToArray())
                {
                    var r = active[key]; output.Add(new Rect(origin.x + key.Item1 / (float)PixelsPerUnit, origin.y + r.y / (float)PixelsPerUnit,
                        (key.Item2 - key.Item1) / (float)PixelsPerUnit, r.rows / (float)PixelsPerUnit)); active.Remove(key);
                }
                foreach (var key in row) active[key] = active.TryGetValue(key, out var r) ? (r.y, r.rows + 1) : (y, 1);
            }
            return output.OrderBy(r => r.yMin).ThenBy(r => r.xMin).ToArray();
        }

        public static StudyABridgeRiverFineBinding Bind(StudyABridgeRiverFinePlan plan, Transform parent)
        {
            RequireUnitTransform(parent);
            if (parent.position != Vector3.zero || parent.Find(ColliderRootName) != null || parent.GetComponentsInChildren<StudyABridgeRiverContour>(true).Length != 0)
                throw new InvalidOperationException("Fine water requires identity map root and no pre-existing generated collider/contour; refusing duplicate or replacement.");
            // Reconstruct from saved controls; callers cannot swap masks/rectangles after preflight.
            var checkedPlan = MakePlan(plan.Assembly, plan.Polygons, plan.Width / PixelsPerUnit, plan.Height / PixelsPerUnit, plan.BridgeStructureVersion);
            if (Hash(plan.VisibleWaterMask) != checkedPlan.VisibleWaterSha256 || Hash(plan.WaterCollisionMask) != checkedPlan.WaterCollisionSha256 ||
                Hash(plan.FineFreeWalkMask) != checkedPlan.FineFreeWalkSha256 || plan.ColliderRectangles == null || !plan.ColliderRectangles.SequenceEqual(checkedPlan.ColliderRectangles) ||
                plan.DeckBounds == null || !plan.DeckBounds.SequenceEqual(checkedPlan.DeckBounds) || plan.RetainedSolidCells == null ||
                !plan.RetainedSolidCells.SequenceEqual(checkedPlan.RetainedSolidCells)) throw new InvalidDataException("Fine water masks/rectangle union changed after preflight.");
            if (plan.BridgeBodyCollisionMask == null || Hash(plan.BridgeBodyCollisionMask) != checkedPlan.BridgeBodyCollisionSha256 ||
                plan.BridgeBodyRectangles == null || !plan.BridgeBodyRectangles.SequenceEqual(checkedPlan.BridgeBodyRectangles))
                throw new InvalidDataException("Bridge pillar body changed after preflight.");
            var collisionLayer = LayerMask.NameToLayer("MapCollision");
            if (collisionLayer < 0) throw new InvalidDataException("Required MapCollision physics layer is missing.");
            var root = new GameObject(ColliderRootName); root.transform.SetParent(parent, false); root.layer = collisionLayer;
            var body = root.AddComponent<Rigidbody2D>(); body.bodyType = RigidbodyType2D.Static;
            var composite = root.AddComponent<CompositeCollider2D>(); composite.geometryType = CompositeCollider2D.GeometryType.Polygons;
            composite.generationType = CompositeCollider2D.GenerationType.Manual; composite.edgeRadius = 0; composite.vertexDistance = .00001f;
            composite.offsetDistance = 0; composite.offset = Vector2.zero; composite.isTrigger = false;
            var boxes = new List<BoxCollider2D>();
            foreach (var rect in checkedPlan.ColliderRectangles)
            {
                var box = root.AddComponent<BoxCollider2D>(); box.offset = rect.center; box.size = rect.size; box.edgeRadius = 0;
                box.compositeOperation = Collider2D.CompositeOperation.Merge; boxes.Add(box);
            }
            composite.GenerateGeometry();
            foreach (var polygon in checkedPlan.Polygons)
            {
                var controls = new GameObject("FineWaterOutline_" + polygon.BlockId); controls.transform.SetParent(parent, false);
                controls.transform.localPosition = new Vector3(polygon.Origin.x, polygon.Origin.y, 0);
                var component = controls.AddComponent<StudyABridgeRiverContour>(); component.BlockId = polygon.BlockId; component.Extent = polygon.Extent;
                component.PixelStep = polygon.PixelStep; component.Points = polygon.Points.ToList();
            }
            if (checkedPlan.BridgeStructureVersion == 1) StudyABridge01Structure.BindBodies(checkedPlan, parent);
            return new StudyABridgeRiverFineBinding { Root = root, Body = body, Composite = composite, SourceBoxes = boxes.ToArray() };
        }
        private static void RequireUnitTransform(Transform transform)
        {
            if (transform == null || transform.rotation != Quaternion.identity || transform.lossyScale != Vector3.one)
                throw new InvalidDataException("Fine river controls/binding require unrotated native unit scale.");
        }
        public static string Hash(byte[] bytes)
        {
            using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
        }
    }
}
