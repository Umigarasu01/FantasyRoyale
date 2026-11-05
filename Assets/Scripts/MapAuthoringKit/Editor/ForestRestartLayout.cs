using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace FantasyRoyale.MapAuthoringKit.Editor
{
    /// <summary>
    /// 少量の森林素材の接続・自動配置を検証するEditor内の一時Plan。保存先の正本はUnity Sceneとする。
    /// </summary>
    public sealed class ForestRestartPlan
    {
        public readonly int Seed;
        public readonly RectInt Bounds;
        public readonly HashSet<Vector2Int> GroundCells = new HashSet<Vector2Int>();
        public readonly HashSet<Vector2Int> RoadCells = new HashSet<Vector2Int>();
        public readonly HashSet<Vector2Int> WaterCells = new HashSet<Vector2Int>();
        public readonly HashSet<Vector2Int> BridgeCells = new HashSet<Vector2Int>();
        public readonly HashSet<Vector2Int> CliffCells = new HashSet<Vector2Int>();
        public readonly HashSet<Vector2Int> CliffFaceCells = new HashSet<Vector2Int>();
        // 橋床・出入口・崖面を隠さないための表示用予約域。通行Collisionには転用しない。
        public readonly HashSet<Vector2Int> VisualClearanceCells = new HashSet<Vector2Int>();
        public readonly HashSet<Vector2Int> ProtectedCells = new HashSet<Vector2Int>();
        public readonly HashSet<Vector2Int> BlockedCells = new HashSet<Vector2Int>();
        public readonly List<RectInt> Clearings = new List<RectInt>();
        public readonly List<Vector2Int> Entrances = new List<Vector2Int>();
        public readonly List<Vector2Int> EventDestinations = new List<Vector2Int>();
        public readonly List<ForestRestartObstaclePlacement> Obstacles =
            new List<ForestRestartObstaclePlacement>();
        public readonly List<ForestRestartObstaclePlacement> Landmarks =
            new List<ForestRestartObstaclePlacement>();
        public Vector2Int Spawn;

        public ForestRestartPlan(int seed, RectInt bounds)
        {
            Seed = seed;
            Bounds = bounds;
        }
    }

    /// <summary>
    /// 足元Anchor、配置用Footprint、表示Bounds、接地Collisionを分け、枝葉の通路への張り出しを許容する。
    /// </summary>
    public sealed class ForestRestartObstaclePlacement
    {
        public readonly string AssetKey;
        public readonly Vector2 Position;
        public readonly Rect Footprint;
        public readonly Rect VisualBounds;
        public readonly Rect CollisionRect;
        public Rect VisualFootprint => Footprint;

        public ForestRestartObstaclePlacement(
            string assetKey, Vector2 position, Rect visualFootprint, Rect collisionRect)
        {
            AssetKey = assetKey;
            Position = position;
            Footprint = visualFootprint;
            var visualSize = GetVisualSize(assetKey);
            var footOffset = Mathf.Round(visualSize.y * ForestRestartLayout.PixelsPerUnit * 0.16f)
                / ForestRestartLayout.PixelsPerUnit;
            VisualBounds = new Rect(position - new Vector2(visualSize.x * 0.5f, footOffset),
                visualSize);
            CollisionRect = collisionRect;
        }

        public static Vector2 GetVisualSize(string assetKey) =>
            assetKey == "grove" ? new Vector2(6f, 4f)
            : assetKey == "undergrowth" ? new Vector2(3f, 3f)
            : assetKey == "fountain" ? new Vector2(3f, 4f) : new Vector2(4f, 4f);
    }

    /// <summary>
    /// Sceneへ書き込む前の配置・通行検査結果。描画やUnity Physicsの検証はScene側で別途行う。
    /// </summary>
    public sealed class ForestRestartValidation
    {
        public IReadOnlyList<string> Errors { get; }
        public int ReachableCellCount { get; }
        public bool IsValid => Errors.Count == 0;

        internal ForestRestartValidation(List<string> errors, int reachableCellCount)
        {
            Errors = errors;
            ReachableCellCount = reachableCellCount;
        }
    }

    /// <summary>
    /// Seedから広場・幅3Cellの曲がる道路・森林を生成する検証用Plan。正式な試合地形仕様ではない。
    /// </summary>
    public static class ForestRestartLayout
    {
        public const int Width = 48;
        public const int Height = 32;
        public const int PixelsPerUnit = 32;

        private static readonly Vector2Int[] Directions =
        {
            Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left
        };

        /// <summary>
        /// 地形と目的地を先に保護し、全歩行Cellの連結を保てる森林候補だけ採用する。
        /// </summary>
        public static ForestRestartPlan Generate(int seed) => Generate(seed, true);

        public static ForestRestartPlan Generate(int seed, bool includeLandmarks)
        {
            var random = new System.Random(seed);
            var plan = new ForestRestartPlan(seed, new RectInt(0, 0, Width, Height));
            foreach (var cell in plan.Bounds.allPositionsWithin)
            {
                plan.GroundCells.Add(cell);
            }

            AddClearings(plan, random);
            AddNaturalTerrain(plan, random);
            if (includeLandmarks) AddLandmarks(plan);
            AddRoadsAndDestinations(plan, random);
            AddVisualClearance(plan);
            foreach (var cell in plan.RoadCells)
            {
                plan.ProtectedCells.Add(cell);
            }

            plan.BlockedCells.UnionWith(EnvironmentBlockedCells(plan));
            foreach (var landmark in plan.Landmarks)
                plan.BlockedCells.UnionWith(CellsIntersecting(landmark.CollisionRect));
            // 森の輪郭を先に作り、道・岸・広場の縁へ中小の部品を重ねる。
            var clusters = CreateForestClusters(plan, random);
            ScatterObstacles(plan, random, clusters, "grove", new Vector2(6f, 4f), new Vector2(3.5f, 1.25f),
                new Rect(-1.5f, -0.3125f, 3f, 0.9375f), 20, 1800);
            ScatterObstacles(plan, random, clusters, "oak", new Vector2(4f, 4f), new Vector2(1.75f, 1.125f),
                new Rect(-0.625f, -0.25f, 1.25f, 0.625f), 78, 4500);
            ScatterObstacles(plan, random, clusters, "undergrowth", new Vector2(3f, 3f), new Vector2(1.875f, 0.875f),
                new Rect(-0.8125f, -0.1875f, 1.625f, 0.5f), 56, 3000);

            var validation = Validate(plan);
            if (!validation.IsValid)
            {
                throw new InvalidOperationException(
                    "森林検証Planを生成できませんでした: " + string.Join("; ", validation.Errors));
            }

            return plan;
        }

        public static ForestRestartPlan Create(int seed) => Generate(seed);

        /// <summary>
        /// 通常24x16unit画面へ地形層を残す小広場。寸法は戦闘の最低面積を定めるものではない。
        /// </summary>
        private static void AddClearings(ForestRestartPlan plan, System.Random random)
        {
            var central = new Vector2Int(plan.Bounds.xMin + plan.Bounds.width / 2 + random.Next(-1, 2),
                plan.Bounds.yMin + Mathf.RoundToInt(plan.Bounds.height * 0.44f) + random.Next(-1, 2));
            plan.Spawn = central;
            var centers = new[]
            {
                central,
                central + new Vector2Int(Mathf.RoundToInt(plan.Bounds.width * 0.21f) + random.Next(-1, 2),
                    Mathf.RoundToInt(plan.Bounds.height * 0.25f)),
                new Vector2Int(RiverCenterX(plan) - 5, central.y + random.Next(0, 3)),
                central + new Vector2Int(Mathf.RoundToInt(plan.Bounds.width * 0.17f) + random.Next(-1, 2),
                    -Mathf.RoundToInt(plan.Bounds.height * 0.19f))
            };
            var count = random.Next(3, 5);
            for (var index = 0; index < count; index++)
            {
                var center = centers[index];
                var width = index == 0 ? 7 : 5;
                var height = index == 0 || index == 2 ? 5 : 3;
                var clearing = new RectInt(center.x - width / 2, center.y - height / 2, width, height);
                plan.Clearings.Add(clearing);
                var phase = random.NextDouble() * Math.PI * 2;
                foreach (var cell in clearing.allPositionsWithin)
                {
                    var dx = (cell.x - center.x) / (width / 2f);
                    var dy = (cell.y - center.y) / (height / 2f);
                    var angle = Math.Atan2(dy, dx);
                    var edge = 0.86 + 0.11 * Math.Sin(angle * 3 + phase)
                        + 0.07 * Math.Sin(angle * 5 - phase);
                    if (dx * dx + dy * dy <= edge)
                    {
                        plan.RoadCells.Add(cell);
                    }
                }
            }
        }

        /// <summary>
        /// Spawn側の画面に見える川・小段丘と、離れた外周段丘を役割から配置する。
        /// </summary>
        private static void AddNaturalTerrain(ForestRestartPlan plan, System.Random random)
        {
            var phase = random.NextDouble() * Math.PI * 2;
            for (var y = 0; y < Height; y++)
            {
                var center = RiverCenterX(plan)
                    + Mathf.RoundToInt((float)(1.25 * Math.Sin((y - plan.Spawn.y) * 0.23 + phase)));
                var width = y % 7 < 3 ? 4 : 3;
                for (var x = center - 1; x < center - 1 + width; x++)
                    plan.WaterCells.Add(new Vector2Int(x, y));
            }

            var outerWest = plan.Bounds.xMin + Mathf.RoundToInt(plan.Bounds.width * 0.06f);
            for (var x = outerWest; x < outerWest + 7; x++)
            {
                var front = plan.Bounds.yMax - 4 + (x == outerWest || x == outerWest + 6 ? 1 : 0);
                for (var y = front; y < Height; y++) plan.CliffCells.Add(new Vector2Int(x, y));
            }
            var outerEast = plan.Bounds.xMax - Mathf.RoundToInt(plan.Bounds.width * 0.25f);
            for (var x = outerEast; x < outerEast + 9; x++)
            {
                var front = plan.Bounds.yMin + 2 + (x == outerEast || x == outerEast + 8 ? 1 : 0);
                var back = plan.Bounds.yMin + 4;
                for (var y = front; y <= back; y++) plan.CliffCells.Add(new Vector2Int(x, y));
            }
            var nearFront = plan.Spawn + new Vector2Int(Mathf.RoundToInt(plan.Bounds.width * 0.15f) - 3,
                Mathf.RoundToInt(plan.Bounds.height * 0.125f) - 1);
            for (var x = nearFront.x; x < nearFront.x + 6; x++)
                for (var y = nearFront.y; y < nearFront.y + 3; y++)
                    plan.CliffCells.Add(new Vector2Int(x, y));
            foreach (var cell in plan.CliffCells)
                if (!plan.CliffCells.Contains(cell + Vector2Int.down)) plan.CliffFaceCells.Add(cell);
        }

        /// <summary>
        /// 店と泉は道路外の足元を占有する。後続の道路・森林生成はこの領域を避ける。
        /// </summary>
        private static void AddLandmarks(ForestRestartPlan plan)
        {
            var market = CenterCell(plan.Clearings[1]);
            var central = CenterCell(plan.Clearings[0]);
            AddLandmark(plan, "shop", new Vector2(market.x + 1, market.y + 3f),
                new Vector2(3.5f, 1.25f), new Rect(-1.5f, -0.25f, 3f, 0.8125f));
            AddLandmark(plan, "fountain", new Vector2(central.x + 6, central.y + 1),
                new Vector2(1.875f, 1.125f), new Rect(-0.75f, -0.25f, 1.5f, 0.625f));
        }

        private static void AddLandmark(ForestRestartPlan plan, string key, Vector2 position,
            Vector2 footprintSize, Rect localCollision)
        {
            var footprint = new Rect(position - new Vector2(footprintSize.x * 0.5f, 0.375f), footprintSize);
            plan.Landmarks.Add(new ForestRestartObstaclePlacement(key, position, footprint,
                new Rect(position + localCollision.position, localCollision.size)));
        }

        /// <summary>
        /// 主広場を中心に東側のLoopと二橋の西側Loopを作り、入口は短い曲線枝で接続する。
        /// </summary>
        private static void AddRoadsAndDestinations(ForestRestartPlan plan, System.Random random)
        {
            var centers = plan.Clearings.Select(CenterCell).ToList();
            var central = centers[0]; var market = centers[1]; var crossing = centers[2];
            var northBend = central + new Vector2Int(1, Mathf.RoundToInt(plan.Bounds.height * 0.25f));
            var eastBend = central + new Vector2Int(Mathf.RoundToInt(plan.Bounds.width * 0.29f), 3);
            var northPort = new Vector2Int(central.x, plan.Clearings[0].yMax - 1);
            var southPort = new Vector2Int(central.x, plan.Clearings[0].yMin);
            var westPort = new Vector2Int(plan.Clearings[0].xMin, central.y);
            var northJunction = central + new Vector2Int(0, plan.Bounds.height / 6);
            var southJunction = central + new Vector2Int(0, -plan.Bounds.height / 5);
            var westJunction = central + new Vector2Int(-plan.Bounds.width / 10, 0);
            var southBend = central + new Vector2Int(Mathf.RoundToInt(plan.Bounds.width * 0.25f),
                -plan.Bounds.height / 5);
            // 広場へ四本の長い直線を突き刺さず、段丘の外側を巡る細い主導線にする。
            AddMeanderingRoad(plan, northPort, northJunction, random, 0.5f);
            AddMeanderingRoad(plan, northJunction, northBend, random, 0.5f);
            AddMeanderingRoad(plan, northBend, market, random, 1f);
            AddMeanderingRoad(plan, market, eastBend, random, 1f);
            AddMeanderingRoad(plan, eastBend, southBend, random, 1.25f);
            AddMeanderingRoad(plan, southBend, southJunction, random, 0.75f);
            AddMeanderingRoad(plan, southJunction, southPort, random, 0.5f);
            AddMeanderingRoad(plan, westJunction, westPort, random, 0.3f);
            var rows = BridgeRows(plan).ToArray();
            foreach (var y in rows)
            {
                var channel = plan.WaterCells.Where(cell => Math.Abs(cell.y - y) <= 1).ToArray();
                var westEnd = new Vector2Int(channel.Min(cell => cell.x) - 2, y);
                var eastEnd = new Vector2Int(channel.Max(cell => cell.x) + 2, y);
                AddCardinalRoad(plan, westEnd, eastEnd);
                AddCardinalRoad(plan, westEnd, new Vector2Int(crossing.x, y));
                AddMeanderingRoad(plan, eastEnd, westJunction, random, 0.5f);
            }
            AddMeanderingRoad(plan, new Vector2Int(crossing.x, rows[0]), crossing, random, 0.75f);
            AddMeanderingRoad(plan, crossing, new Vector2Int(crossing.x, rows[1]), random, 0.75f);
            if (centers.Count == 4)
            {
                // 南東の小広場も既存の周回路上で接続し、中央へ別の斜線を増やさない。
                AddMeanderingRoad(plan, southBend, centers[3], random, 0.3f);
            }
            plan.Entrances.Add(new Vector2Int(plan.Bounds.xMin, crossing.y));
            plan.Entrances.Add(new Vector2Int(plan.Bounds.xMax - 1, central.y + 1 + random.Next(-1, 2)));
            plan.Entrances.Add(new Vector2Int(northBend.x, plan.Bounds.yMax - 1));
            plan.Entrances.Add(new Vector2Int(central.x - 2, plan.Bounds.yMin));
            AddMeanderingRoad(plan, plan.Entrances[0], crossing, random, 2f);
            AddMeanderingRoad(plan, eastBend, plan.Entrances[1], random, 1f);
            AddCardinalRoad(plan, plan.Entrances[2], northBend);
            AddMeanderingRoad(plan, plan.Entrances[3], southJunction, random, 0.75f);
            AddProtectedBrush(plan, plan.Spawn, 2);
            plan.EventDestinations.AddRange(new[]
            {
                central + new Vector2Int(4, 1), market,
                crossing + Vector2Int.right, central + Vector2Int.left * 2,
                centers.Count == 4 ? centers[3] : crossing + Vector2Int.left,
                market + Vector2Int.left * 2
            });
            for (var index = 0; index < plan.EventDestinations.Count; index++)
            {
                var destination = plan.EventDestinations[index];
                AddMeanderingRoad(plan, centers.OrderBy(center =>
                    (center - destination).sqrMagnitude).First(), destination, random, 0.3f);
                AddProtectedBrush(plan, destination, index == 0 ? 0 : 1);
            }
            plan.BridgeCells.UnionWith(plan.WaterCells.Where(plan.RoadCells.Contains));
        }

        private static int RiverCenterX(ForestRestartPlan plan) =>
            plan.Spawn.x - Mathf.RoundToInt(plan.Bounds.width * 0.19f);

        private static IEnumerable<int> BridgeRows(ForestRestartPlan plan)
        {
            var spacing = plan.Bounds.height / 6;
            yield return plan.Spawn.y - spacing;
            yield return plan.Spawn.y + spacing;
        }

        private static void AddVisualClearance(ForestRestartPlan plan)
        {
            foreach (var y in BridgeRows(plan))
            {
                var channel = plan.BridgeCells.Where(cell => Math.Abs(cell.y - y) <= 1).ToArray();
                for (var cy = y - 2; cy <= y + 2; cy++)
                    for (var x = channel.Min(cell => cell.x) - 2; x <= channel.Max(cell => cell.x) + 2; x++)
                        if (plan.Bounds.Contains(new Vector2Int(x, cy)))
                            plan.VisualClearanceCells.Add(new Vector2Int(x, cy));
            }
            foreach (var face in plan.CliffFaceCells)
                for (var dy = -2; dy <= 0; dy++)
                {
                    var cell = face + new Vector2Int(0, dy);
                    if (plan.Bounds.Contains(cell)) plan.VisualClearanceCells.Add(cell);
                }
        }

        private static void AddCardinalRoad(ForestRestartPlan plan, Vector2Int start, Vector2Int end)
        {
            foreach (var cell in MapRoadPathRasterizer.RasterizePath(new[]
                { new Vector3Int(start.x, start.y, 0), new Vector3Int(end.x, end.y, 0) }, 1))
                AddRoadCell(plan, new Vector2Int(cell.x, cell.y));
        }

        /// <summary>
        /// 二次曲線を1Cell未満の間隔で採取し、各対角Stepを4近傍の二歩へ変換して膨張する。
        /// </summary>
        private static void AddMeanderingRoad(ForestRestartPlan plan, Vector2Int start, Vector2Int end,
            System.Random random, float bendAmount)
        {
            var delta = (Vector2)(end - start);
            var length = delta.magnitude;
            var normal = length > 0f ? new Vector2(-delta.y, delta.x) / length : Vector2.zero;
            var control = ((Vector2)start + (Vector2)end) * 0.5f
                + normal * bendAmount * (random.Next(2) == 0 ? -1f : 1f);
            var steps = Mathf.Max(1, Mathf.CeilToInt(length * 3f));
            var previous = start;
            AddRoadBrush(plan, previous);
            for (var step = 1; step <= steps; step++)
            {
                var t = step / (float)steps;
                var point = Vector2Int.RoundToInt((1f - t) * (1f - t) * (Vector2)start
                    + 2f * (1f - t) * t * control + t * t * (Vector2)end);
                while (previous != point)
                {
                    var remaining = point - previous;
                    var moveX = remaining.x != 0 && (remaining.y == 0
                        || Math.Abs(remaining.x) >= Math.Abs(remaining.y));
                    previous += moveX ? new Vector2Int(Math.Sign(remaining.x), 0)
                        : new Vector2Int(0, Math.Sign(remaining.y));
                    AddRoadBrush(plan, previous);
                }
            }
        }

        private static void AddRoadBrush(ForestRestartPlan plan, Vector2Int center)
        {
            for (var y = -1; y <= 1; y++) for (var x = -1; x <= 1; x++)
                AddRoadCell(plan, center + new Vector2Int(x, y));
        }

        private static void AddRoadCell(ForestRestartPlan plan, Vector2Int cell)
        {
            if (!plan.Bounds.Contains(cell) || plan.CliffCells.Contains(cell)
                || plan.Landmarks.Any(item => CellsIntersecting(item.Footprint).Contains(cell))) return;
            // 河岸の歩行路は橋の三列だけ水へ進入できる。
            if (plan.WaterCells.Contains(cell) && BridgeRows(plan).All(y => Math.Abs(cell.y - y) > 1)) return;
            plan.RoadCells.Add(cell);
        }

        private static HashSet<Vector2Int> EnvironmentBlockedCells(ForestRestartPlan plan)
        {
            var cells = new HashSet<Vector2Int>(plan.GroundCells.Where(cell =>
                IsBoundary(plan.Bounds, cell) && !plan.RoadCells.Contains(cell)));
            cells.UnionWith(plan.WaterCells.Except(plan.BridgeCells));
            cells.UnionWith(plan.CliffCells);
            return cells;
        }

        private static List<Vector2> CreateForestClusters(ForestRestartPlan plan, System.Random random)
        {
            var clusters = new List<Vector2>
            {
                new Vector2(plan.Bounds.xMin + 3, plan.Bounds.yMin + 5),
                new Vector2(plan.Bounds.xMin + 3, plan.Bounds.yMax - 5),
                new Vector2(plan.Bounds.xMax - 4, plan.Bounds.yMax - 5),
                new Vector2(plan.Bounds.xMax - 4, plan.Bounds.yMin + 5),
                plan.Spawn + new Vector2(-5, -7), plan.Spawn + new Vector2(7, 5)
            };
            for (var i = 0; i < 4; i++)
                clusters.Add(new Vector2(RiverCenterX(plan) + 7 + random.Next(16),
                    plan.Bounds.yMin + 4 + random.Next(plan.Bounds.height - 8)));
            return clusters;
        }

        private static float RegionDensity(ForestRestartPlan plan, Vector2 position, string assetKey)
        {
            var outerDistance = Mathf.Min(position.x - plan.Bounds.xMin, plan.Bounds.xMax - position.x,
                position.y - plan.Bounds.yMin, plan.Bounds.yMax - position.y);
            var nearClearing = plan.Clearings.Any(clearing =>
                position.x >= clearing.xMin - 3 && position.x <= clearing.xMax + 3
                && position.y >= clearing.yMin - 2 && position.y <= clearing.yMax + 2);
            var nearCoast = Mathf.Abs(position.x - RiverCenterX(plan)) < 6f;
            var onTerrace = plan.CliffCells.Contains(Vector2Int.FloorToInt(position));
            if (assetKey == "grove") return onTerrace || outerDistance < 6f ? 0.9f
                : nearClearing || nearCoast ? 0.12f : 0.7f;
            if (assetKey == "undergrowth") return nearClearing || nearCoast ? 0.95f : 0.35f;
            return outerDistance < 6f || onTerrace ? 0.85f : nearClearing || nearCoast ? 0.65f : 0.5f;
        }

        private static bool ObscuresTerrain(ForestRestartPlan plan, ForestRestartObstaclePlacement placement)
        {
            var cells = CellsIntersecting(placement.VisualBounds);
            var isCanopy = placement.AssetKey == "oak" || placement.AssetKey == "grove";
            return isCanopy ? cells.Any(plan.VisualClearanceCells.Contains)
                : cells.Any(cell => plan.BridgeCells.Contains(cell) || plan.CliffFaceCells.Contains(cell));
        }

        private static bool ObscuresLandmark(ForestRestartPlan plan, Rect visualBounds) =>
            plan.Landmarks.Any(landmark => visualBounds.Overlaps(new Rect(
                landmark.Position.x - (landmark.AssetKey == "shop" ? 1.5f : 1f),
                landmark.Position.y - 0.375f, landmark.AssetKey == "shop" ? 3f : 2f,
                landmark.AssetKey == "shop" ? 2.75f : 2.25f)));

        /// <summary>
        /// 表示範囲で間隔を制約し、接地判定を加えても全地表の到達性を失わない候補を選ぶ。
        /// </summary>
        private static void ScatterObstacles(
            ForestRestartPlan plan, System.Random random, List<Vector2> clusters, string assetKey,
            Vector2 visualSize, Vector2 footprintSize, Rect localCollision, int targetCount, int attempts)
        {
            var added = 0;
            for (var attempt = 0; attempt < attempts && added < targetCount; attempt++)
            {
                var footOffset = Mathf.Round(visualSize.y * PixelsPerUnit * 0.16f) / PixelsPerUnit;
                var position = new Vector2(
                    visualSize.x * 0.5f + (float)random.NextDouble() * (Width - visualSize.x),
                    footOffset + (float)random.NextDouble() * (Height - visualSize.y));
                if (attempt % 3 != 0)
                {
                    var cluster = clusters[random.Next(clusters.Count)];
                    position = cluster + new Vector2((float)(random.NextDouble() - 0.5) * 12f,
                        (float)(random.NextDouble() - 0.5) * 8f);
                    position.x = Mathf.Clamp(position.x, visualSize.x * 0.5f, Width - visualSize.x * 0.5f);
                    position.y = Mathf.Clamp(position.y, footOffset, Height - visualSize.y + footOffset);
                }
                // 小さい岩・下草は広場と岸へ寄せ、大きい森塊と同じ散布領域へ均等に埋めない。
                if (assetKey == "undergrowth" && attempt % 2 == 0)
                {
                    var clearing = plan.Clearings[random.Next(plan.Clearings.Count)];
                    var angle = random.NextDouble() * Math.PI * 2;
                    position = (Vector2)CenterCell(clearing) + new Vector2(
                        (float)Math.Cos(angle) * (clearing.width * 0.5f + 2f),
                        (float)Math.Sin(angle) * (clearing.height * 0.5f + 1.5f));
                    position.x = Mathf.Clamp(position.x, visualSize.x * 0.5f, Width - visualSize.x * 0.5f);
                    position.y = Mathf.Clamp(position.y, footOffset, Height - visualSize.y + footOffset);
                }
                position = new Vector2(Snap(position.x), Snap(position.y));
                if (random.NextDouble() > RegionDensity(plan, position, assetKey)) continue;
                var footprint = new Rect(
                    position.x - footprintSize.x * 0.5f, position.y - 0.375f,
                    footprintSize.x, footprintSize.y);
                var collision = new Rect(position + localCollision.position, localCollision.size);
                var placement = new ForestRestartObstaclePlacement(assetKey, position, footprint, collision);
                if (CellsIntersecting(footprint).Any(cell => plan.ProtectedCells.Contains(cell))
                    || CellsIntersecting(footprint).Any(cell => plan.WaterCells.Contains(cell))
                    || plan.Obstacles.Concat(plan.Landmarks).Any(obstacle => footprint.Overlaps(obstacle.Footprint))
                    || ObscuresTerrain(plan, placement) || ObscuresLandmark(plan, placement.VisualBounds))
                {
                    continue;
                }

                var collisionCells = CellsIntersecting(collision).ToList();
                if (collisionCells.Any(cell => !plan.Bounds.Contains(cell)
                    || plan.ProtectedCells.Contains(cell)))
                {
                    continue;
                }

                var addedCells = collisionCells.Where(cell => !plan.BlockedCells.Contains(cell)).ToList();
                plan.BlockedCells.UnionWith(addedCells);

                if (FloodWalkable(plan).Count != plan.GroundCells.Count - plan.BlockedCells.Count)
                {
                    foreach (var cell in addedCells)
                    {
                        plan.BlockedCells.Remove(cell);
                    }

                    continue;
                }

                plan.Obstacles.Add(placement);
                added++;
            }
        }

        /// <summary>
        /// 境界、保護域、表示と判定の分離、目的地、全歩行Cellの4近傍接続を検査する。
        /// </summary>
        public static ForestRestartValidation Validate(ForestRestartPlan plan)
        {
            if (plan == null)
            {
                throw new ArgumentNullException(nameof(plan));
            }

            var errors = new List<string>();
            if (plan.Bounds != new RectInt(0, 0, Width, Height))
            {
                errors.Add("BOUNDS: 検証用Mapは48x32Cellです。");
            }

            if (plan.GroundCells.Count != plan.Bounds.width * plan.Bounds.height
                || CellsWithin(plan.Bounds).Any(cell => !plan.GroundCells.Contains(cell)))
            {
                errors.Add("GROUND: 全Cellの地表が必要です。");
            }

            foreach (var cells in new[]
                { plan.GroundCells, plan.RoadCells, plan.ProtectedCells, plan.BlockedCells,
                    plan.WaterCells, plan.BridgeCells, plan.CliffCells, plan.CliffFaceCells,
                    plan.VisualClearanceCells })
            {
                if (cells.Any(cell => !plan.Bounds.Contains(cell)))
                {
                    errors.Add("CELL_BOUNDS: Map外にCellがあります。");
                }
            }

            if (plan.Clearings.Count < 3 || plan.Clearings.Count > 4
                || plan.Clearings.Any(clearing => !ContainsRect(plan.Bounds, clearing)))
            {
                errors.Add("CLEARINGS: Map内に3または4箇所の広場が必要です。");
            }

            if (plan.RoadCells.Count == 0 || !IsConnected(plan.RoadCells))
            {
                errors.Add("ROAD_CONNECTION: 道路と広場は単一の接続成分である必要があります。");
            }

            if (!plan.RoadCells.IsSubsetOf(plan.ProtectedCells)
                || plan.ProtectedCells.Overlaps(plan.BlockedCells))
            {
                errors.Add("PROTECTED: 道路・Spawn・目的地の保護域に障害物を置けません。");
            }

            if (plan.WaterCells.Count == 0 || !IsConnected(plan.WaterCells)
                || !plan.WaterCells.Any(cell => cell.y == 0)
                || !plan.WaterCells.Any(cell => cell.y == Height - 1))
                errors.Add("WATER_CHANNEL: 川は上下の境界へ達する単一成分である必要があります。");
            if (!plan.BridgeCells.IsSubsetOf(plan.WaterCells)
                || !plan.BridgeCells.IsSubsetOf(plan.RoadCells)
                || plan.RoadCells.Intersect(plan.WaterCells).Any(cell => !plan.BridgeCells.Contains(cell))
                || plan.BridgeCells.Overlaps(plan.BlockedCells)
                || CountComponents(plan.BridgeCells) != 2)
                errors.Add("BRIDGE_CROSSINGS: 道路の渡河は二つの歩行可能な橋へ限定します。");
            if (plan.CliffCells.Count == 0 || plan.CliffCells.Overlaps(plan.RoadCells)
                || !plan.CliffFaceCells.SetEquals(plan.CliffCells.Where(cell =>
                    !plan.CliffCells.Contains(cell + Vector2Int.down)))
                || !plan.WaterCells.Except(plan.BridgeCells).Concat(plan.CliffCells)
                    .All(plan.BlockedCells.Contains))
                errors.Add("TERRAIN_COLLISION: 深い水と段丘は通行不能で、崖面は段丘の南縁に置きます。");
            if (!plan.BridgeCells.IsSubsetOf(plan.VisualClearanceCells)
                || !plan.CliffFaceCells.IsSubsetOf(plan.VisualClearanceCells))
                errors.Add("TERRAIN_VISUAL_CLEARANCE: 橋床と崖面は樹冠に隠れない予約域が必要です。");

            var expectedBlocked = EnvironmentBlockedCells(plan);
            var placements = plan.Obstacles.Concat(plan.Landmarks).ToList();
            for (var index = 0; index < placements.Count; index++)
            {
                var obstacle = placements[index];
                if (obstacle == null)
                {
                    errors.Add("OBSTACLE_NULL: 配置がnullです。");
                    continue;
                }

                if (obstacle.AssetKey != "oak" && obstacle.AssetKey != "grove"
                    && obstacle.AssetKey != "undergrowth" && obstacle.AssetKey != "shop"
                    && obstacle.AssetKey != "fountain")
                {
                    errors.Add("ASSET_KEY: 未定義の森林素材です。");
                }

                if (!IsSnapped(obstacle.Position.x) || !IsSnapped(obstacle.Position.y))
                {
                    errors.Add("PIXEL_SNAP: Anchorを1/32unitへ合わせてください。");
                }

                if (!ContainsRect(plan.Bounds, obstacle.VisualBounds)
                    || !ContainsRect(obstacle.VisualBounds, obstacle.Footprint)
                    || !ContainsRect(obstacle.Footprint, obstacle.CollisionRect)
                    || obstacle.CollisionRect.width <= 0f || obstacle.CollisionRect.height <= 0f
                    || obstacle.CollisionRect.width >= obstacle.Footprint.width
                    || obstacle.CollisionRect.height >= obstacle.Footprint.height)
                {
                    errors.Add("OBSTACLE_BOUNDS: 表示範囲内に小さい接地判定が必要です。");
                }

                if (CellsIntersecting(obstacle.Footprint).Any(cell => plan.ProtectedCells.Contains(cell)))
                {
                    errors.Add("VISUAL_PROTECTED: 配置用Footprintが道路・目的地の保護域に重なっています。");
                }
                if (plan.Obstacles.Contains(obstacle)
                    && (ObscuresTerrain(plan, obstacle) || ObscuresLandmark(plan, obstacle.VisualBounds)))
                    errors.Add("TERRAIN_VISUAL_OCCLUSION: 森林表示で橋床・出入口・崖面・景観の中心を隠せません。");

                for (var otherIndex = 0; otherIndex < index; otherIndex++)
                {
                    var other = placements[otherIndex];
                    if (other != null && obstacle.Footprint.Overlaps(other.Footprint))
                    {
                        errors.Add("VISUAL_OVERLAP: 森林部品の配置用Footprintが重なっています。");
                    }
                }

                expectedBlocked.UnionWith(CellsIntersecting(obstacle.CollisionRect));
            }

            if (!expectedBlocked.SetEquals(plan.BlockedCells))
            {
                errors.Add("COLLISION_CELLS: 外周と接地判定から導く移動不可Cellと一致していません。");
            }

            var reached = FloodWalkable(plan);
            if (!plan.GroundCells.Contains(plan.Spawn) || !plan.ProtectedCells.Contains(plan.Spawn)
                || plan.BlockedCells.Contains(plan.Spawn))
            {
                errors.Add("SPAWN: Spawnは保護された歩行地に置いてください。");
            }

            if (plan.Entrances.Count != 4 || plan.Entrances.Distinct().Count() != 4
                || !HasFourBoundarySides(plan))
            {
                errors.Add("ENTRANCES: 四辺に一つずつ入口が必要です。");
            }

            if (plan.EventDestinations.Count != 6 || plan.EventDestinations.Distinct().Count() != 6
                || plan.EventDestinations.Contains(plan.Spawn))
            {
                errors.Add("EVENT_COUNT: Spawnと異なる6箇所の目的地が必要です。");
            }

            if (plan.Entrances.Concat(plan.EventDestinations).Any(cell => !reached.Contains(cell))
                || plan.EventDestinations.Any(cell => !plan.ProtectedCells.Contains(cell)))
            {
                errors.Add("DESTINATION_REACHABILITY: 全入口と目的地へ到達できる必要があります。");
            }

            if (reached.Count != plan.GroundCells.Count - plan.BlockedCells.Count)
            {
                errors.Add("WALKABLE_CONNECTION: 全歩行地が単一の接続成分である必要があります。");
            }

            return new ForestRestartValidation(errors, reached.Count);
        }

        /// <summary>
        /// Scene生成前後の再現性記録用Hash。Map保存形式ではなく、集合を座標順へ正規化した検証値。
        /// </summary>
        public static string ComputeLayoutHash(ForestRestartPlan plan)
        {
            if (plan == null)
            {
                throw new ArgumentNullException(nameof(plan));
            }

            var text = new StringBuilder();
            text.Append(plan.Seed).Append('|').Append(plan.Bounds).Append('|').Append(plan.Spawn).Append('|');
            foreach (var cells in new[]
                { plan.GroundCells, plan.RoadCells, plan.ProtectedCells, plan.BlockedCells,
                    plan.WaterCells, plan.BridgeCells, plan.CliffCells, plan.CliffFaceCells,
                    plan.VisualClearanceCells,
                    new HashSet<Vector2Int>(plan.Entrances), new HashSet<Vector2Int>(plan.EventDestinations) })
            {
                foreach (var cell in cells.OrderBy(cell => cell.y).ThenBy(cell => cell.x))
                {
                    text.Append(cell.x).Append(',').Append(cell.y).Append(';');
                }

                text.Append('|');
            }

            foreach (var clearing in plan.Clearings)
            {
                text.Append(clearing).Append('|');
            }

            foreach (var obstacle in plan.Obstacles.Concat(plan.Landmarks))
            {
                text.Append(obstacle.AssetKey).Append(':');
                foreach (var value in new[] { obstacle.Position.x, obstacle.Position.y,
                    obstacle.Footprint.x, obstacle.Footprint.y,
                    obstacle.Footprint.width, obstacle.Footprint.height,
                    obstacle.VisualBounds.x, obstacle.VisualBounds.y,
                    obstacle.VisualBounds.width, obstacle.VisualBounds.height,
                    obstacle.CollisionRect.x, obstacle.CollisionRect.y,
                    obstacle.CollisionRect.width, obstacle.CollisionRect.height })
                {
                    text.Append(value.ToString("R", CultureInfo.InvariantCulture)).Append(',');
                }

                text.Append('|');
            }

            using (var hash = SHA256.Create())
            {
                return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(text.ToString())))
                    .Replace("-", string.Empty);
            }
        }

        /// <summary>
        /// world Rectが正の面積で交差するCellを列挙し、上端・右端だけの接触を除く。
        /// </summary>
        public static IEnumerable<Vector2Int> CellsIntersecting(Rect rect)
        {
            for (var y = Mathf.FloorToInt(rect.yMin); y < Mathf.CeilToInt(rect.yMax); y++)
            {
                for (var x = Mathf.FloorToInt(rect.xMin); x < Mathf.CeilToInt(rect.xMax); x++)
                {
                    yield return new Vector2Int(x, y);
                }
            }
        }

        private static IEnumerable<Vector2Int> CellsWithin(RectInt rect)
        {
            foreach (var cell in rect.allPositionsWithin)
            {
                yield return cell;
            }
        }

        private static Vector2Int CenterCell(RectInt rect) =>
            new Vector2Int(rect.xMin + rect.width / 2, rect.yMin + rect.height / 2);

        private static float Snap(float value) => Mathf.Round(value * PixelsPerUnit) / PixelsPerUnit;

        private static bool IsSnapped(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value)
            && Mathf.Abs(value * PixelsPerUnit - Mathf.Round(value * PixelsPerUnit)) < 0.0001f;

        private static bool IsBoundary(RectInt bounds, Vector2Int cell) =>
            cell.x == bounds.xMin || cell.x == bounds.xMax - 1
            || cell.y == bounds.yMin || cell.y == bounds.yMax - 1;

        private static bool ContainsRect(RectInt outer, RectInt inner) =>
            inner.width > 0 && inner.height > 0 && inner.xMin >= outer.xMin && inner.yMin >= outer.yMin
            && inner.xMax <= outer.xMax && inner.yMax <= outer.yMax;

        private static bool ContainsRect(RectInt outer, Rect inner) =>
            ContainsRect(new Rect(outer.x, outer.y, outer.width, outer.height), inner);

        private static bool ContainsRect(Rect outer, Rect inner) =>
            inner.width > 0f && inner.height > 0f && inner.xMin >= outer.xMin && inner.yMin >= outer.yMin
            && inner.xMax <= outer.xMax && inner.yMax <= outer.yMax;

        private static bool HasFourBoundarySides(ForestRestartPlan plan) =>
            plan.Entrances.All(cell => plan.Bounds.Contains(cell) && plan.RoadCells.Contains(cell))
            && plan.Entrances.Any(cell => cell.x == plan.Bounds.xMin)
            && plan.Entrances.Any(cell => cell.x == plan.Bounds.xMax - 1)
            && plan.Entrances.Any(cell => cell.y == plan.Bounds.yMin)
            && plan.Entrances.Any(cell => cell.y == plan.Bounds.yMax - 1);

        private static void AddProtectedBrush(ForestRestartPlan plan, Vector2Int center, int radius)
        {
            for (var y = -radius; y <= radius; y++)
            {
                for (var x = -radius; x <= radius; x++)
                {
                    var cell = center + new Vector2Int(x, y);
                    if (plan.Bounds.Contains(cell) && (!IsBoundary(plan.Bounds, cell)
                        || plan.RoadCells.Contains(cell)))
                    {
                        plan.ProtectedCells.Add(cell);
                    }
                }
            }
        }

        /// <summary>
        /// 障害物に囲まれた地表も検出するため、道路だけでなく全GroundをSpawnから探索する。
        /// </summary>
        private static HashSet<Vector2Int> FloodWalkable(ForestRestartPlan plan)
        {
            var walkable = new HashSet<Vector2Int>(plan.GroundCells);
            walkable.ExceptWith(plan.BlockedCells);
            return FloodCells(walkable, plan.Spawn);
        }

        private static bool IsConnected(HashSet<Vector2Int> cells) =>
            cells.Count > 0 && FloodCells(cells, cells.First()).Count == cells.Count;

        private static int CountComponents(HashSet<Vector2Int> cells)
        {
            var remaining = new HashSet<Vector2Int>(cells);
            var count = 0;
            while (remaining.Count > 0)
            {
                remaining.ExceptWith(FloodCells(remaining, remaining.First()));
                count++;
            }
            return count;
        }

        private static HashSet<Vector2Int> FloodCells(HashSet<Vector2Int> cells, Vector2Int start)
        {
            var visited = new HashSet<Vector2Int>();
            if (!cells.Contains(start))
            {
                return visited;
            }

            var queue = new Queue<Vector2Int>();
            visited.Add(start);
            queue.Enqueue(start);
            while (queue.Count > 0)
            {
                var cell = queue.Dequeue();
                foreach (var direction in Directions)
                {
                    var next = cell + direction;
                    if (cells.Contains(next) && visited.Add(next))
                    {
                        queue.Enqueue(next);
                    }
                }
            }

            return visited;
        }
    }
}
