using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace FantasyRoyale.MapAuthoringKit.Editor
{
    /// <summary>Aの一画面検証用。既存ForestRestartの固定Bounds・素材・生成処理から独立する。</summary>
    public sealed class OverheadStudyAPlan
    {
        public sealed class Art
        {
            public string Key, Role;
            public Vector2 WorldSize, Pivot;
            public Rect Footprint, Collider, WalkFloor;
            public Rect VisualBounds(Vector2 anchor) => new Rect(anchor - Vector2.Scale(WorldSize, Pivot), WorldSize);
        }

        public sealed class Placement
        {
            public Art Asset;
            public Vector2 Anchor;
            public bool GroundDetail;
            public Rect VisualBounds => Asset.VisualBounds(Anchor);
            public Rect Footprint => Translate(Asset.Footprint, Anchor);
            public Rect Collider => Translate(Asset.Collider, Anchor);
            public bool BlocksWalking => Asset.Role != "bridge" && Asset.Role != "hero" && Asset.Role != "flower"
                && Asset.Collider.width > 0f && Asset.Collider.height > 0f;
        }

        public const int Width = 24, Height = 16;
        public readonly int Seed;
        public readonly RectInt Bounds = new RectInt(0, 0, Width, Height);
        public readonly HashSet<Vector2Int> GroundCells = new HashSet<Vector2Int>();
        public readonly HashSet<Vector2Int> RoadCells = new HashSet<Vector2Int>();
        public readonly HashSet<Vector2Int> SoilCells = new HashSet<Vector2Int>();
        public readonly HashSet<Vector2Int> WaterCells = new HashSet<Vector2Int>();
        public readonly HashSet<Vector2Int> BridgeCells = new HashSet<Vector2Int>();
        public readonly HashSet<Vector2Int> TerrainBlockedCells = new HashSet<Vector2Int>();
        public readonly HashSet<Vector2Int> BlockedCells = new HashSet<Vector2Int>();
        public readonly List<Vector2> MainRoute = new List<Vector2>();
        public readonly List<Vector2> RiverCenterline = new List<Vector2>();
        public readonly List<Vector2> Destinations = new List<Vector2>();
        public readonly List<Placement> Placements = new List<Placement>();
        public readonly Vector2 Spawn = new Vector2(12.5f, 7.5f);
        public Rect Clearing, BridgeWalkRect;
        private readonly List<Rect> visibleReservations = new List<Rect>();
        private static readonly Vector2Int[] Directions = { Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left };

        private OverheadStudyAPlan(int seed) { Seed = seed; }

        public static OverheadStudyAPlan Generate(int seed, IEnumerable<Art> definitions)
        {
            if (definitions == null) throw new ArgumentNullException(nameof(definitions));
            var arts = definitions.OrderBy(item => item.Key, StringComparer.Ordinal).ToArray();
            foreach (var role in new[] { "grass", "soil", "water", "tree", "shrub", "rock", "flower", "hero", "fountain", "shop", "bridge" })
                if (!arts.Any(item => item.Role == role)) throw new ArgumentException("A素材role不足: " + role);
            var plan = new OverheadStudyAPlan(seed);
            var random = new System.Random(seed);
            foreach (var cell in plan.Bounds.allPositionsWithin) plan.GroundCells.Add(cell);
            var phase = (float)random.NextDouble() * 0.6f - 0.3f;
            for (var y = 0; y < Height; y++)
            {
                var center = y >= 7 && y <= 9 ? 6f : 6f + Mathf.Sin((y - 8) * 0.35f + phase) * 0.8f;
                plan.RiverCenterline.Add(new Vector2(center, y + 0.5f));
                for (var x = Mathf.FloorToInt(center - 1f); x < Mathf.CeilToInt(center + 1f); x++)
                    plan.WaterCells.Add(new Vector2Int(x, y));
            }
            plan.AddRoute(new Vector2Int(0, 8), new Vector2Int(9, 8), new Vector2(4.5f, 8.5f), true);
            plan.AddRoute(new Vector2Int(9, 8), new Vector2Int(23, 7), new Vector2(16f, 5.5f + (float)random.NextDouble()), true);
            plan.Clearing = new Rect(10.5f, 6.25f, 6.5f + (float)random.NextDouble() * 0.5f, 4.25f);
            foreach (var cell in plan.GroundCells)
            {
                var point = new Vector2(cell.x + 0.5f, cell.y + 0.5f);
                var d = point - plan.Clearing.center;
                var ellipse = d.x * d.x / Mathf.Pow(plan.Clearing.width * 0.5f, 2f)
                    + d.y * d.y / Mathf.Pow(plan.Clearing.height * 0.5f, 2f);
                if (ellipse < 1f + Mathf.Sin(point.x * 1.7f + point.y + phase) * 0.12f && !plan.WaterCells.Contains(cell))
                    plan.SoilCells.Add(cell);
            }
            plan.SoilCells.UnionWith(plan.RoadCells);
            plan.BridgeCells.UnionWith(plan.WaterCells.Where(cell => cell.y == 8));
            var bridgeArt = arts.First(item => item.Role == "bridge");
            plan.BridgeWalkRect = new Rect(5f, 8f, 2f, 1f);
            if (bridgeArt.WalkFloor.width < 2f || bridgeArt.WalkFloor.height < 1f)
                throw new ArgumentException("橋素材の確認済みwalkFloorは幅2u・歩行1u以上必要です。全canvas寸法で代用しません。");
            var bridge = new Placement { Asset = bridgeArt, Anchor = plan.BridgeWalkRect.center - bridgeArt.WalkFloor.center, GroundDetail = true };
            if (!plan.IsInside(bridge.VisualBounds))
                throw new ArgumentException("確認済み橋の全表示範囲が24x16の画面に収まりません。素材寸法/Pivotを再確認してください。");
            plan.Placements.Add(bridge);
            plan.visibleReservations.Add(Expand(bridge.VisualBounds, 0.125f));
            plan.visibleReservations.Add(new Rect(plan.Spawn - new Vector2(0.75f, 0.3f), new Vector2(1.5f, 2f)));
            foreach (var point in plan.MainRoute) plan.visibleReservations.Add(new Rect(point - Vector2.one * 0.5f, Vector2.one));

            plan.AddLandmark(arts.First(item => item.Role == "fountain"), new Vector2(15.5f, 10.75f));
            plan.AddLandmark(arts.First(item => item.Role == "shop"), new Vector2(18.5f, 12.25f));
            plan.Placements.Add(new Placement { Asset = arts.First(item => item.Role == "hero"), Anchor = plan.Spawn });
            var clusters = new[] { new Vector2(2f, 2f), new Vector2(2f, 12.25f), new Vector2(10.5f, 13.25f),
                new Vector2(22f, 12.25f), new Vector2(21.5f, 2.25f), new Vector2(11f, 1.5f) };
            foreach (var cluster in clusters) plan.AddCluster(arts.Where(item => item.Role == "tree").ToArray(), cluster, 6, 2.6f, random);
            for (var i = 0; i < clusters.Length; i++)
            {
                plan.AddCluster(arts.Where(item => item.Role == "shrub").ToArray(), clusters[i], 3, 3.2f, random);
                plan.AddCluster(arts.Where(item => item.Role == "rock").ToArray(), clusters[i], 2, 3.5f, random);
            }
            var flowers = arts.Where(item => item.Role == "flower").ToArray();
            foreach (var cell in plan.GroundCells.OrderBy(cell => cell.y).ThenBy(cell => cell.x))
            {
                if (plan.WaterCells.Contains(cell) || plan.RoadCells.Contains(cell) || random.Next(4) != 0) continue;
                if (!Directions.Any(direction => plan.SoilCells.Contains(cell + direction) || plan.WaterCells.Contains(cell + direction))) continue;
                var item = new Placement { Asset = flowers[random.Next(flowers.Length)], Anchor = new Vector2(cell.x + 0.5f, cell.y + 0.5f) };
                if (plan.IsInside(item.VisualBounds) && !plan.visibleReservations.Take(2).Any(rect => rect.Overlaps(item.VisualBounds))) plan.Placements.Add(item);
            }
            plan.TerrainBlockedCells.UnionWith(plan.WaterCells.Except(plan.BridgeCells));
            plan.TerrainBlockedCells.UnionWith(plan.GroundCells.Where(cell => plan.IsBoundary(cell) && !plan.RoadCells.Contains(cell)));
            plan.BlockedCells.UnionWith(plan.TerrainBlockedCells);
            foreach (var item in plan.Placements.Where(item => item.BlocksWalking)) plan.BlockedCells.UnionWith(Cells(item.Collider));
            return plan;
        }

        private void AddLandmark(Art art, Vector2 preferred)
        {
            Placement selected = null;
            foreach (var shift in new[] { Vector2.zero, Vector2.left, Vector2.right, Vector2.up, Vector2.up + Vector2.left })
            {
                var candidate = new Placement { Asset = art, Anchor = preferred + shift * 0.5f };
                if (!IsInside(candidate.VisualBounds) || !IsInside(candidate.Footprint) || !IsInside(candidate.Collider)
                    || Cells(candidate.Collider).Any(cell => WaterCells.Contains(cell) || RoadCells.Contains(cell))
                    || Placements.Where(item => item.BlocksWalking).Any(item => item.Collider.Overlaps(candidate.Collider))) continue;
                selected = candidate; break;
            }
            if (selected == null) throw new ArgumentException("確認済み素材の寸法/足元が小区画のPOI配置に収まりません: " + art.Key);
            Placements.Add(selected);
            var approach = new Vector2(selected.Collider.center.x, selected.Collider.yMin - 0.75f);
            Destinations.Add(approach);
            AddRoute(Vector2Int.FloorToInt(Spawn), Vector2Int.FloorToInt(approach), (Spawn + approach) * 0.5f, false);
            visibleReservations.Add(selected.VisualBounds);
            visibleReservations.Add(new Rect(approach - new Vector2(0.5f, 0.1f), new Vector2(1f, 1.5f)));
        }

        private void AddCluster(Art[] arts, Vector2 center, int target, float radius, System.Random random)
        {
            var placed = 0;
            for (var attempt = 0; attempt < target * 80 && placed < target; attempt++)
            {
                var angle = (float)random.NextDouble() * Mathf.PI * 2f;
                var distance = Mathf.Sqrt((float)random.NextDouble()) * radius;
                var anchor = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance;
                anchor = new Vector2(Mathf.Round(anchor.x * 64f) / 64f, Mathf.Round(anchor.y * 64f) / 64f);
                var item = new Placement { Asset = arts[random.Next(arts.Length)], Anchor = anchor };
                // 森の樹冠は画面端で自然に切れてよい。足元/衝突は必ず区画内に置く。
                if (!IsInside(item.Footprint) || !IsInside(item.Collider)
                    || Cells(item.Footprint).Any(cell => WaterCells.Contains(cell) || RoadCells.Contains(cell) || IsBoundary(cell))
                    || visibleReservations.Any(rect => rect.Overlaps(item.VisualBounds))
                    || Placements.Where(other => other.BlocksWalking).Any(other => Expand(other.Collider, 0.12f).Overlaps(item.Collider))) continue;
                Placements.Add(item); placed++;
            }
        }

        private void AddRoute(Vector2Int start, Vector2Int end, Vector2 control, bool main)
        {
            var previous = start;
            var steps = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(start, end) * 8f));
            for (var i = 0; i <= steps; i++)
            {
                var t = (float)i / steps;
                var target = Vector2Int.RoundToInt((1f - t) * (1f - t) * (Vector2)start + 2f * (1f - t) * t * control + t * t * (Vector2)end);
                while (previous != target)
                {
                    var delta = target - previous;
                    previous += Mathf.Abs(delta.x) >= Mathf.Abs(delta.y) ? new Vector2Int(Math.Sign(delta.x), 0) : new Vector2Int(0, Math.Sign(delta.y));
                    PaintRoute(previous, main);
                }
                PaintRoute(previous, main);
            }
        }

        private void PaintRoute(Vector2Int cell, bool main)
        {
            if (main) MainRoute.Add(new Vector2(cell.x + 0.5f, cell.y + 0.5f));
            if (Bounds.Contains(cell)) { RoadCells.Add(cell); SoilCells.Add(cell); }
        }

        public List<string> Validate()
        {
            var errors = new List<string>();
            if (GroundCells.Count != Width * Height) errors.Add("Groundは24x16全Cell必要です。");
            if (!RoadCells.Contains(Vector2Int.FloorToInt(Spawn))) errors.Add("Spawnが主導線にありません。");
            if (!WaterCells.Intersect(RoadCells).ToHashSet().SetEquals(BridgeCells)) errors.Add("水路横断は一つの橋の1歩行段だけに限定します。");
            if (BridgeCells.Count != 2 || BridgeCells.Any(cell => cell.y != 8)) errors.Add("初回Aは2Cellの川を1歩行段で渡ります。");
            foreach (var point in MainRoute.Concat(BridgeCells.Select(cell => new Vector2(cell.x + 0.5f, cell.y + 0.5f))).Concat(Destinations).Append(Spawn))
            {
                var foot = new Rect(point + new Vector2(-0.24f, -0.055f), new Vector2(0.48f, 0.55f));
                if (Cells(foot).Any(cell => TerrainBlockedCells.Contains(cell)) || Placements.Any(item => item.BlocksWalking && item.Collider.Overlaps(foot)))
                    errors.Add("Actor足元の保守的Boundsが主導線/橋頭で遮断されます: " + point);
            }
            var reachable = new HashSet<Vector2Int>(); var queue = new Queue<Vector2Int>();
            var start = Vector2Int.FloorToInt(Spawn); reachable.Add(start); queue.Enqueue(start);
            while (queue.Count > 0)
            {
                var cell = queue.Dequeue();
                foreach (var direction in Directions)
                {
                    var next = cell + direction;
                    if (Bounds.Contains(next) && !BlockedCells.Contains(next) && reachable.Add(next)) queue.Enqueue(next);
                }
            }
            foreach (var destination in Destinations.Concat(BridgeCells.Select(cell => new Vector2(cell.x + 0.5f, cell.y + 0.5f))).Concat(new[] { new Vector2(0.5f, 8.5f), new Vector2(23.5f, 7.5f) }))
                if (!reachable.Contains(Vector2Int.FloorToInt(destination))) errors.Add("到達不可: " + destination);
            var bridge = Placements.Single(item => item.Asset.Role == "bridge");
            foreach (var cell in BridgeCells)
                if (!Translate(bridge.Asset.WalkFloor, bridge.Anchor).Contains(new Vector2(cell.x + 0.5f, cell.y + 0.5f))) errors.Add("確認済み橋床の表示範囲不足: " + cell);
            if (Placements.Count(item => item.Asset.Role == "tree") < 12) errors.Add("樹冠群が小区画に収まりません。素材Footprint/Boundsを確認してください。");
            return errors.Distinct().ToList();
        }

        public string StableHash()
        {
            var text = new StringBuilder().Append(Seed).Append('|');
            foreach (var cells in new[] { RoadCells, SoilCells, WaterCells, BridgeCells, BlockedCells })
            { foreach (var cell in cells.OrderBy(cell => cell.y).ThenBy(cell => cell.x)) text.Append(cell.x).Append(',').Append(cell.y).Append(';'); text.Append('|'); }
            foreach (var item in Placements)
                text.Append(item.Asset.Key).Append(':').Append(item.Anchor.x.ToString("R", CultureInfo.InvariantCulture)).Append(',')
                    .Append(item.Anchor.y.ToString("R", CultureInfo.InvariantCulture)).Append(';');
            using (var sha = SHA256.Create()) return string.Concat(sha.ComputeHash(Encoding.UTF8.GetBytes(text.ToString())).Select(value => value.ToString("x2")));
        }

        private bool IsBoundary(Vector2Int cell) => cell.x == 0 || cell.y == 0 || cell.x == Width - 1 || cell.y == Height - 1;
        private bool IsInside(Rect rect) => rect.width > 0f && rect.height > 0f && rect.xMin >= 0f && rect.yMin >= 0f && rect.xMax <= Width && rect.yMax <= Height;
        private static Rect Translate(Rect rect, Vector2 point) => new Rect(rect.position + point, rect.size);
        private static Rect Expand(Rect rect, float margin) => new Rect(rect.position - Vector2.one * margin, rect.size + Vector2.one * margin * 2f);
        private static IEnumerable<Vector2Int> Cells(Rect rect)
        {
            for (var y = Mathf.FloorToInt(rect.yMin); y < Mathf.CeilToInt(rect.yMax); y++)
                for (var x = Mathf.FloorToInt(rect.xMin); x < Mathf.CeilToInt(rect.xMax); x++) yield return new Vector2Int(x, y);
        }
    }
}
