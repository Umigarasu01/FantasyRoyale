using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace FantasyRoyale.MapAuthoringKit.OverheadStudyA
{
    public enum StudyAPortDirection { North, East, South, West }
    public enum StudyATerrain { Grass, Soil, Stone, Bridge, Water }
    public enum StudyABlockKind { Plaza, ForestPath, BridgeBank }

    [Serializable]
    public sealed class StudyAVariationRegion
    {
        public string Id;
        public RectInt Bounds;
        public float Density;
        public string[] Roles;
        public int Variation;
    }

    [Serializable]
    public sealed class StudyABlockPort
    {
        public string Id;
        // 境界上の帯の最小Cell。東西の幅は+y、南北の幅は+xへ伸びる。
        public Vector2Int Position;
        public StudyAPortDirection Direction;
        public int Width = 3;
        public StudyATerrain Terrain = StudyATerrain.Soil;
        // 接続互換性の宣言値。runtime elevation/多層Physicsを実装しない。
        public int Height;
        public bool IsWalkable => Terrain != StudyATerrain.Water;
        public Vector2Int Offset => DirectionOffset(Direction);
        public IEnumerable<Vector2Int> Cells
        {
            get { for (var i = 0; i < Width; i++) yield return Position + (Direction == StudyAPortDirection.East || Direction == StudyAPortDirection.West ? Vector2Int.up : Vector2Int.right) * i; }
        }
        public static Vector2Int DirectionOffset(StudyAPortDirection direction)
        {
            switch (direction)
            {
                case StudyAPortDirection.North: return Vector2Int.up;
                case StudyAPortDirection.East: return Vector2Int.right;
                case StudyAPortDirection.South: return Vector2Int.down;
                case StudyAPortDirection.West: return Vector2Int.left;
                default: throw new ArgumentOutOfRangeException(nameof(direction));
            }
        }
    }

    /// <summary>固定Scene authoring用の区画データ。SpriteBoundsと歩行/占有を分離する。</summary>
    [Serializable]
    public sealed class StudyABlockDefinition
    {
        public string Id;
        public StudyABlockKind Kind;
        public Vector2Int Extent;
        public int Variation;
        public List<StudyABlockPort> Ports = new List<StudyABlockPort>();
        public List<Vector2Int> WalkCells = new List<Vector2Int>();
        public List<Vector2Int> SoilCells = new List<Vector2Int>();
        public List<Vector2Int> StoneCells = new List<Vector2Int>();
        public List<Vector2Int> RoadCells = new List<Vector2Int>();
        public List<Vector2Int> WaterCells = new List<Vector2Int>();
        public List<Vector2Int> BridgeCells = new List<Vector2Int>();
        // 水上のBridgeCellsと、岸に重なる表示/歩行床を別に記録する。
        public List<Vector2Int> BridgeFloorCells = new List<Vector2Int>();
        public List<Vector2Int> SolidCells = new List<Vector2Int>();
        // 樹冠等のvisual領域候補。歩行Colliderに変換しない。
        public List<Vector2Int> OcclusionCells = new List<Vector2Int>();
        public List<StudyAVariationRegion> VariationRegions = new List<StudyAVariationRegion>();
        public List<Vector2> MainRoute = new List<Vector2>();
        public Vector2 Spawn;
        public RectInt Bounds => new RectInt(Vector2Int.zero, Extent);

        public List<string> Validate()
        {
            var errors = new List<string>();
            if (string.IsNullOrWhiteSpace(Id)) errors.Add("block Id is missing");
            if (Extent.x <= 0 || Extent.y <= 0) { errors.Add("extent must be positive"); return errors; }
            var sets = new[] { WalkCells, SoilCells, StoneCells, RoadCells, WaterCells, BridgeCells, BridgeFloorCells, SolidCells, OcclusionCells };
            if (sets.Any(c => c == null) || Ports == null || MainRoute == null || VariationRegions == null) { errors.Add("block collections must not be null"); return errors; }
            foreach (var cells in sets)
                if (cells.Any(c => !Bounds.Contains(c)) || cells.Distinct().Count() != cells.Count) errors.Add("cell outside extent or duplicate cell");
            var walk = new HashSet<Vector2Int>(WalkCells); var water = new HashSet<Vector2Int>(WaterCells); var bridge = new HashSet<Vector2Int>(BridgeCells);
            if (walk.Count == 0 || !Connected(walk)) errors.Add("walk cells must form one connected region");
            if (walk.Overlaps(SolidCells) || walk.Intersect(water).Any(c => !bridge.Contains(c))) errors.Add("walk overlaps solid or unbridged water");
            if (bridge.Any(c => !water.Contains(c) || !walk.Contains(c))) errors.Add("bridge must be water and walkable");
            if (StoneCells.Intersect(SoilCells).Any() || StoneCells.Intersect(WaterCells).Any()) errors.Add("stone terrain overlaps another terrain");
            if (BridgeFloorCells.Any(c => !walk.Contains(c)) || bridge.Any(c => !BridgeFloorCells.Contains(c))) errors.Add("visible bridge floor must cover the water crossing and be walkable");
            if (RoadCells.Any(c => !walk.Contains(c))) errors.Add("road must be walkable");
            if (Ports.Count == 0 || Ports.Any(p => p == null) || Ports.Where(p => p != null).Select(p => p.Id).Distinct().Count() != Ports.Count) errors.Add("ports are missing, null, or duplicate");
            foreach (var port in Ports.Where(p => p != null))
            {
                if (string.IsNullOrWhiteSpace(port.Id) || !Enum.IsDefined(typeof(StudyAPortDirection), port.Direction) || !Enum.IsDefined(typeof(StudyATerrain), port.Terrain) || port.Width <= 0 || port.Width > Math.Max(Extent.x, Extent.y))
                { errors.Add("invalid port contract"); continue; }
                foreach (var cell in port.Cells)
                {
                    if (!Bounds.Contains(cell) || Bounds.Contains(cell + port.Offset)) errors.Add("port must face outward at extent boundary");
                    else if (port.IsWalkable && (!walk.Contains(cell) || !walk.Contains(cell - port.Offset))) errors.Add("full port width and inward approach must be walkable");
                    else if (!port.IsWalkable && (!water.Contains(cell) || walk.Contains(cell) || bridge.Contains(cell))) errors.Add("water port must be nonwalking water");
                    if (port.Terrain == StudyATerrain.Soil && !SoilCells.Contains(cell)) errors.Add("soil port terrain mismatch");
                    if (port.Terrain == StudyATerrain.Grass && (SoilCells.Contains(cell) || StoneCells.Contains(cell) || water.Contains(cell))) errors.Add("grass port terrain mismatch");
                    if (port.Terrain == StudyATerrain.Stone && !StoneCells.Contains(cell)) errors.Add("stone port terrain mismatch");
                    if (port.Terrain == StudyATerrain.Bridge && !bridge.Contains(cell)) errors.Add("bridge port terrain mismatch");
                }
            }
            if (MainRoute.Count == 0 || MainRoute.Any(p => !Finite(p) || !walk.Contains(Vector2Int.FloorToInt(p)))) errors.Add("main route must be inside walking cells");
            if (!Finite(Spawn) || !walk.Contains(Vector2Int.FloorToInt(Spawn))) errors.Add("spawn must be inside walking cells");
            foreach (var region in VariationRegions)
                if (region == null || string.IsNullOrWhiteSpace(region.Id) || region.Bounds.width <= 0 || region.Bounds.height <= 0 || !Bounds.Contains(region.Bounds.min) ||
                    !Bounds.Contains(region.Bounds.max - Vector2Int.one) || float.IsNaN(region.Density) || float.IsInfinity(region.Density) || region.Density < 0f || region.Density > 1f ||
                    region.Roles == null || region.Roles.Length == 0 || region.Roles.Any(string.IsNullOrWhiteSpace)) errors.Add("invalid variation region");
            return errors.Distinct().ToList();
        }

        internal static bool Connected(HashSet<Vector2Int> cells)
        {
            if (cells.Count == 0) return false;
            var pending = new Queue<Vector2Int>(); var seen = new HashSet<Vector2Int>();
            var first = cells.OrderBy(c => c.y).ThenBy(c => c.x).First(); pending.Enqueue(first); seen.Add(first);
            while (pending.Count > 0)
            {
                var cell = pending.Dequeue();
                foreach (var d in new[] { Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left })
                    if (cells.Contains(cell + d) && seen.Add(cell + d)) pending.Enqueue(cell + d);
            }
            return seen.Count == cells.Count;
        }
        private static bool Finite(Vector2 p) => !float.IsNaN(p.x) && !float.IsNaN(p.y) && !float.IsInfinity(p.x) && !float.IsInfinity(p.y);
    }
}
