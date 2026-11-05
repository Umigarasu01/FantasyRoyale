using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace FantasyRoyale.MapAuthoringKit.OverheadStudyA
{
    public sealed class StudyAPlacedBlock
    {
        public readonly StudyABlockDefinition Definition;
        public readonly Vector2Int Origin;
        public RectInt Bounds => new RectInt(Origin, Definition.Extent);
        public StudyAPlacedBlock(StudyABlockDefinition definition, Vector2Int origin) { Definition = definition; Origin = origin; }
    }

    public sealed class StudyABlockConnection
    {
        public int FromBlock, FromPort, ToBlock, ToPort;
        public Vector2 Center;
        public bool IsWalkable;
    }

    public sealed class StudyABlockAssembly
    {
        public readonly int Seed;
        public readonly List<StudyAPlacedBlock> Blocks;
        public readonly List<StudyABlockConnection> Connections;
        public readonly HashSet<Vector2Int> WalkCells = new HashSet<Vector2Int>();
        public readonly HashSet<Vector2Int> SoilCells = new HashSet<Vector2Int>();
        public readonly HashSet<Vector2Int> StoneCells = new HashSet<Vector2Int>();
        public readonly HashSet<Vector2Int> RoadCells = new HashSet<Vector2Int>();
        public readonly HashSet<Vector2Int> WaterCells = new HashSet<Vector2Int>();
        public readonly HashSet<Vector2Int> BridgeCells = new HashSet<Vector2Int>();
        public readonly HashSet<Vector2Int> BridgeFloorCells = new HashSet<Vector2Int>();
        public readonly HashSet<Vector2Int> SolidCells = new HashSet<Vector2Int>();
        public readonly HashSet<Vector2Int> OcclusionCells = new HashSet<Vector2Int>();
        public readonly List<List<Vector2>> MainRoutes = new List<List<Vector2>>();
        public IEnumerable<Vector2> ConnectionCenters => Connections.Where(c => c.IsWalkable).Select(c => c.Center);
        public Vector2 Spawn => (Vector2)Blocks[0].Origin + Blocks[0].Definition.Spawn;
        public RectInt Bounds { get; private set; }

        internal StudyABlockAssembly(int seed, IEnumerable<StudyAPlacedBlock> blocks, IEnumerable<StudyABlockConnection> connections)
        {
            Seed = seed; Blocks = blocks.ToList(); Connections = connections.ToList();
            var targets = new[] { WalkCells, SoilCells, StoneCells, RoadCells, WaterCells, BridgeCells, BridgeFloorCells, SolidCells, OcclusionCells };
            foreach (var block in Blocks)
            {
                var sources = new[] { block.Definition.WalkCells, block.Definition.SoilCells, block.Definition.StoneCells, block.Definition.RoadCells, block.Definition.WaterCells, block.Definition.BridgeCells, block.Definition.BridgeFloorCells, block.Definition.SolidCells, block.Definition.OcclusionCells };
                for (var i = 0; i < sources.Length; i++) targets[i].UnionWith(sources[i].Select(c => c + block.Origin));
                MainRoutes.Add(block.Definition.MainRoute.Select(p => p + (Vector2)block.Origin).ToList());
            }
            Bounds = new RectInt(Blocks.Min(b => b.Bounds.xMin), Blocks.Min(b => b.Bounds.yMin), Blocks.Max(b => b.Bounds.xMax) - Blocks.Min(b => b.Bounds.xMin), Blocks.Max(b => b.Bounds.yMax) - Blocks.Min(b => b.Bounds.yMin));
        }

        public StudyABlockAssembly Translate(Vector2Int offset)
        {
            return new StudyABlockAssembly(Seed, Blocks.Select(b => new StudyAPlacedBlock(b.Definition, CheckedAdd(b.Origin, offset))), Connections.Select(c => new StudyABlockConnection { FromBlock = c.FromBlock, FromPort = c.FromPort, ToBlock = c.ToBlock, ToPort = c.ToPort, Center = c.Center + (Vector2)offset, IsWalkable = c.IsWalkable }));
        }

        public List<string> Validate()
        {
            if (Blocks.Count == 0 || Blocks.Any(b => b == null || b.Definition == null)) return new List<string> { "placed block is missing" };
            var errors = Blocks.SelectMany(b => b.Definition.Validate()).ToList();
            if (errors.Count != 0) return errors.Distinct().ToList();
            for (var i = 0; i < Blocks.Count; i++) for (var j = i + 1; j < Blocks.Count; j++)
                if (Blocks[i].Bounds.Overlaps(Blocks[j].Bounds)) errors.Add("block extents overlap");
            var usedPorts = new HashSet<string>();
            foreach (var connection in Connections)
            {
                if (connection == null || connection.FromBlock < 0 || connection.ToBlock < 0 || connection.FromBlock >= Blocks.Count || connection.ToBlock >= Blocks.Count || connection.FromBlock == connection.ToBlock)
                { errors.Add("invalid connection block index"); continue; }
                var from = Blocks[connection.FromBlock]; var to = Blocks[connection.ToBlock];
                if (connection.FromPort < 0 || connection.ToPort < 0 || connection.FromPort >= from.Definition.Ports.Count || connection.ToPort >= to.Definition.Ports.Count)
                { errors.Add("invalid connection port index"); continue; }
                var a = from.Definition.Ports[connection.FromPort]; var b = to.Definition.Ports[connection.ToPort];
                if (!usedPorts.Add(connection.FromBlock + ":" + connection.FromPort) || !usedPorts.Add(connection.ToBlock + ":" + connection.ToPort)) errors.Add("port is connected more than once");
                if (a.Offset != -b.Offset || a.Width != b.Width || a.Terrain != b.Terrain || a.Height != b.Height || connection.IsWalkable != a.IsWalkable ||
                    a.Cells.Any(c => !b.Cells.Any(other => other + to.Origin == c + from.Origin + a.Offset))) errors.Add("connection contract mismatch");
                var expected = a.Cells.Select(c => (Vector2)(c + from.Origin) + Vector2.one * 0.5f).Aggregate(Vector2.zero, (sum, p) => sum + p) / a.Width + (Vector2)a.Offset * 0.5f;
                if (Vector2.Distance(expected, connection.Center) > 0.0001f) errors.Add("connection center mismatch");
            }
            var connectedBlocks = new HashSet<int> { 0 };
            for (var i = 0; i < Blocks.Count; i++)
                foreach (var c in Connections.Where(c => c != null))
                { if (connectedBlocks.Contains(c.FromBlock)) connectedBlocks.Add(c.ToBlock); if (connectedBlocks.Contains(c.ToBlock)) connectedBlocks.Add(c.FromBlock); }
            if (connectedBlocks.Count != Blocks.Count) errors.Add("declared connection graph is disconnected");
            if (!StudyABlockDefinition.Connected(WalkCells)) errors.Add("assembled walk cells are disconnected");
            return errors.Distinct().ToList();
        }

        public string StableHash()
        {
            var text = new StringBuilder().Append(Seed).Append('|');
            foreach (var block in Blocks)
            {
                var d = block.Definition; text.Append(d.Id).Append(':').Append((int)d.Kind).Append(':').Append(d.Variation).Append(':').Append(block.Origin.x).Append(',').Append(block.Origin.y).Append(':').Append(d.Extent.x).Append(',').Append(d.Extent.y).Append('|');
                foreach (var p in d.Ports) text.Append(p.Id).Append(':').Append(p.Position.x).Append(',').Append(p.Position.y).Append(':').Append((int)p.Direction).Append(':').Append(p.Width).Append(':').Append((int)p.Terrain).Append(':').Append(p.Height).Append(';');
                foreach (var point in d.MainRoute) AppendPoint(text, point);
                AppendPoint(text, d.Spawn); text.Append('|');
                foreach (var region in d.VariationRegions) text.Append(region.Id).Append(':').Append(region.Bounds.x).Append(',').Append(region.Bounds.y).Append(',').Append(region.Bounds.width).Append(',').Append(region.Bounds.height).Append(':').Append(region.Density.ToString("R", CultureInfo.InvariantCulture)).Append(':').Append(region.Variation).Append(':').Append(string.Join(",", region.Roles)).Append(';');
            }
            foreach (var set in new[] { WalkCells, SoilCells, StoneCells, RoadCells, WaterCells, BridgeCells, BridgeFloorCells, SolidCells, OcclusionCells })
            { foreach (var c in set.OrderBy(c => c.y).ThenBy(c => c.x)) text.Append(c.x).Append(',').Append(c.y).Append(';'); text.Append('|'); }
            foreach (var c in Connections) { text.Append(c.FromBlock).Append(',').Append(c.FromPort).Append('>').Append(c.ToBlock).Append(',').Append(c.ToPort).Append(':').Append(c.IsWalkable); AppendPoint(text, c.Center); }
            using (var sha = SHA256.Create()) return string.Concat(sha.ComputeHash(Encoding.UTF8.GetBytes(text.ToString())).Select(b => b.ToString("x2")));
        }
        private static void AppendPoint(StringBuilder text, Vector2 p) => text.Append(p.x.ToString("R", CultureInfo.InvariantCulture)).Append(',').Append(p.y.ToString("R", CultureInfo.InvariantCulture)).Append(';');
        internal static Vector2Int CheckedAdd(Vector2Int a, Vector2Int b) => new Vector2Int(checked(a.x + b.x), checked(a.y + b.y));
    }

    /// <summary>Editorが固定Sceneを組むための純粋な接続計算。回転・反転・runtime地形変更なし。</summary>
    public static partial class StudyABlockGenerator
    {
        public static StudyABlockAssembly Single(StudyABlockDefinition definition, Vector2Int origin, int seed)
        {
            RequireValid(definition); CheckBounds(origin, definition.Extent);
            return new StudyABlockAssembly(seed, new[] { new StudyAPlacedBlock(definition, origin) }, Array.Empty<StudyABlockConnection>());
        }
        public static StudyABlockAssembly Pair(StudyABlockDefinition first, int firstPortIndex, StudyABlockDefinition second, int secondPortIndex, int seed)
            => AddConnected(Single(first, Vector2Int.zero, seed), 0, firstPortIndex, second, secondPortIndex);

        public static bool TryConnect(StudyABlockAssembly existing, int existingBlockIndex, int existingPortIndex, StudyABlockDefinition candidate, int candidatePortIndex, out StudyAPlacedBlock placement, out string reason)
        {
            placement = null; reason = null;
            if (existing == null || candidate == null) { reason = "assembly/candidate is missing"; return false; }
            if (existing.Validate().Count != 0 || candidate.Validate().Count != 0) { reason = "invalid block or assembly contract"; return false; }
            if (existingBlockIndex < 0 || existingBlockIndex >= existing.Blocks.Count) { reason = "block index outside assembly"; return false; }
            var source = existing.Blocks[existingBlockIndex];
            if (existingPortIndex < 0 || existingPortIndex >= source.Definition.Ports.Count || candidatePortIndex < 0 || candidatePortIndex >= candidate.Ports.Count) { reason = "port index outside definition"; return false; }
            var a = source.Definition.Ports[existingPortIndex]; var b = candidate.Ports[candidatePortIndex];
            if (a.Offset != -b.Offset) { reason = "port directions must face opposite ways; rotation/flip is not applied"; return false; }
            if (a.Width != b.Width) { reason = "port width mismatch"; return false; }
            if (a.Terrain != b.Terrain) { reason = "port terrain mismatch"; return false; }
            if (a.Height != b.Height) { reason = "port height mismatch"; return false; }
            if (existing.Connections.Any(c => (c.FromBlock == existingBlockIndex && c.FromPort == existingPortIndex) || (c.ToBlock == existingBlockIndex && c.ToPort == existingPortIndex))) { reason = "port is already connected"; return false; }
            try
            {
                var origin = StudyABlockAssembly.CheckedAdd(source.Origin, StudyABlockAssembly.CheckedAdd(a.Position + a.Offset, -b.Position));
                CheckBounds(origin, candidate.Extent);
                var proposed = new StudyAPlacedBlock(candidate, origin);
                if (existing.Blocks.Any(block => block.Bounds.Overlaps(proposed.Bounds))) { reason = "block extents overlap"; return false; }
                foreach (var cell in a.Cells)
                {
                    var target = cell + source.Origin + a.Offset;
                    if (!b.Cells.Any(c => c + origin == target)) { reason = "full port bands do not align"; return false; }
                }
                placement = proposed; return true;
            }
            catch (OverflowException) { reason = "block coordinates overflow"; return false; }
        }

        public static StudyABlockAssembly AddConnected(StudyABlockAssembly existing, int existingBlockIndex, int existingPortIndex, StudyABlockDefinition candidate, int candidatePortIndex)
        {
            if (!TryConnect(existing, existingBlockIndex, existingPortIndex, candidate, candidatePortIndex, out var placement, out var reason)) throw new ArgumentException(reason);
            var port = existing.Blocks[existingBlockIndex].Definition.Ports[existingPortIndex];
            var band = port.Cells.Select(c => (Vector2)(c + existing.Blocks[existingBlockIndex].Origin) + Vector2.one * 0.5f).ToArray();
            var center = band.Aggregate(Vector2.zero, (sum, p) => sum + p) / band.Length + (Vector2)port.Offset * 0.5f;
            var connections = existing.Connections.Concat(new[] { new StudyABlockConnection { FromBlock = existingBlockIndex, FromPort = existingPortIndex, ToBlock = existing.Blocks.Count, ToPort = candidatePortIndex, Center = center, IsWalkable = port.IsWalkable } });
            var result = new StudyABlockAssembly(existing.Seed, existing.Blocks.Concat(new[] { placement }), connections);
            var errors = result.Validate(); if (errors.Count != 0) throw new ArgumentException(string.Join("; ", errors));
            return result;
        }
        private static void RequireValid(StudyABlockDefinition definition)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            var errors = definition.Validate(); if (errors.Count != 0) throw new ArgumentException(string.Join("; ", errors));
        }
        private static void CheckBounds(Vector2Int origin, Vector2Int extent) { checked { var x = origin.x + extent.x; var y = origin.y + extent.y; } }
    }

    public static class StudyABlockPresets
    {
        public static StudyABlockDefinition Plaza(int seed) => Create(StudyABlockKind.Plaza, seed);
        public static StudyABlockDefinition ForestPath(int seed) => Create(StudyABlockKind.ForestPath, seed);
        public static StudyABlockDefinition BridgeBank(int seed) => Create(StudyABlockKind.BridgeBank, seed);

        private static StudyABlockDefinition Create(StudyABlockKind kind, int seed)
        {
            var variation = (int)(Mix(unchecked((uint)seed) ^ ((uint)kind + 1) * 0x9E3779B9u) % 3);
            var path = kind == StudyABlockKind.ForestPath;
            var d = new StudyABlockDefinition { Id = kind.ToString(), Kind = kind, Variation = variation, Extent = path ? new Vector2Int(8, 12) : new Vector2Int(12, 12) };
            const int laneY = 4;
            d.Ports.Add(new StudyABlockPort { Id = "west", Position = new Vector2Int(0, laneY), Direction = StudyAPortDirection.West });
            d.Ports.Add(new StudyABlockPort { Id = "east", Position = new Vector2Int(d.Extent.x - 1, laneY), Direction = StudyAPortDirection.East });
            if (path)
            {
                // Soil shoulders follow staggered banks; the protected route rises, falls, then rises.
                // Only the two port columns prescribe a three-unit road. Interior protection follows the actor foot sweep.
                var soilMin = new[] { 4, 4, 5, 4, 3, 3, 4, 4 };
                var soilMax = new[] { 6, 7, 7, variation == 2 ? 6 : 7, 6, variation == 1 ? 6 : 5, 6, 6 };
                var roadMin = new[] { 4, 5, 5, 5, 4, 4, 4, 4 };
                var roadMax = new[] { 6, 6, 6, 6, 5, 5, 5, 6 };
                var routeY = new[] { 5.5f, 6f, 6.5f, 5.5f, 5f, 4.5f, 5f, 5.5f };
                for (var x = 0; x < d.Extent.x; x++)
                {
                    for (var y = soilMin[x]; y <= soilMax[x]; y++) d.SoilCells.Add(new Vector2Int(x, y));
                    for (var y = roadMin[x]; y <= roadMax[x]; y++) d.RoadCells.Add(new Vector2Int(x, y));
                    d.MainRoute.Add(new Vector2(x + 0.5f, routeY[x]));
                }
            }
            else
            {
                for (var x = 0; x < d.Extent.x; x++)
                {
                    for (var y = laneY; y < laneY + 3; y++) { var cell = new Vector2Int(x, y); d.RoadCells.Add(cell); d.SoilCells.Add(cell); }
                    d.MainRoute.Add(new Vector2(x + 0.5f, laneY + 1.5f));
                }
            }
            if (kind == StudyABlockKind.Plaza)
            {
                // Soil is the authored clearing outline, separate from the protected three-unit road.
                // The left/front clearing joins the fountain and shop without a square terrace behind the roof.
                var rows = new[] { new[] { 2, 3, 7 }, new[] { 3, 2, 8 }, new[] { 7, 2, 10 }, new[] { 8, 3, 10 }, new[] { 9, 4, 6 } };
                foreach (var row in rows) for (var x = row[1]; x <= row[2]; x++)
                { var cell = new Vector2Int(x, row[0]); if (!d.SoilCells.Contains(cell)) d.SoilCells.Add(cell); }
            }
            if (kind == StudyABlockKind.BridgeBank)
            {
                for (var y = 0; y < d.Extent.y; y++)
                {
                    var left = y >= 4 && y <= 7 ? 5 : 4 + ((y + variation) / 3 % 2);
                    for (var x = left; x < left + 2; x++) d.WaterCells.Add(new Vector2Int(x, y));
                }
                d.BridgeCells.AddRange(d.WaterCells.Where(c => c.y >= 4 && c.y <= 6));
                for (var y = 4; y <= 6; y++) for (var x = 4; x <= 7; x++) d.BridgeFloorCells.Add(new Vector2Int(x, y));
                d.SoilCells.RemoveAll(c => d.WaterCells.Contains(c));
                d.Ports.Add(new StudyABlockPort { Id = "river-north", Position = new Vector2Int(d.WaterCells.Where(c => c.y == 11).Min(c => c.x), 11), Direction = StudyAPortDirection.North, Terrain = StudyATerrain.Water, Width = 2 });
                d.Ports.Add(new StudyABlockPort { Id = "river-south", Position = new Vector2Int(d.WaterCells.Where(c => c.y == 0).Min(c => c.x), 0), Direction = StudyAPortDirection.South, Terrain = StudyATerrain.Water, Width = 2 });
            }
            var openings = new HashSet<Vector2Int>(d.Ports.SelectMany(p => p.Cells));
            foreach (var cell in d.Bounds.allPositionsWithin)
            {
                var edge = cell.x == 0 || cell.y == 0 || cell.x == d.Extent.x - 1 || cell.y == d.Extent.y - 1;
                if (edge && !openings.Contains(cell) && !d.WaterCells.Contains(cell)) d.SolidCells.Add(cell);
                if (!d.SolidCells.Contains(cell) && (!d.WaterCells.Contains(cell) || d.BridgeCells.Contains(cell))) d.WalkCells.Add(cell);
                if (!d.WaterCells.Contains(cell) && !d.SoilCells.Contains(cell) && (cell.y < 3 || cell.y >= d.Extent.y - 3)) d.OcclusionCells.Add(cell);
            }
            d.Spawn = d.MainRoute[d.MainRoute.Count / 2];
            d.VariationRegions.Add(new StudyAVariationRegion { Id = "forest-north", Bounds = new RectInt(1, d.Extent.y - 3, d.Extent.x - 2, 3), Density = 0.75f + variation * 0.05f, Roles = new[] { "tree", "shrub", "rock" }, Variation = variation });
            d.VariationRegions.Add(new StudyAVariationRegion { Id = "verge-south", Bounds = new RectInt(1, 0, d.Extent.x - 2, 2), Density = 0.3f + variation * 0.05f, Roles = new[] { "shrub", "rock", "flower" }, Variation = (variation + 1) % 3 });
            var errors = d.Validate(); if (errors.Count != 0) throw new InvalidOperationException(string.Join("; ", errors));
            return d;
        }
        private static uint Mix(uint value) { unchecked { value ^= value >> 16; value *= 0x7FEB352Du; value ^= value >> 15; value *= 0x846CA68Bu; return value ^ (value >> 16); } }
    }
}
