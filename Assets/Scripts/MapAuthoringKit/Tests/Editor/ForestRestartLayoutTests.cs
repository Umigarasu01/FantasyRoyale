using System.Collections.Generic;
using System.Linq;
using FantasyRoyale.MapAuthoringKit.Editor;
using NUnit.Framework;
using UnityEngine;

namespace FantasyRoyale.MapAuthoringKit.Tests.Editor
{
    /// <summary>
    /// 自動配置の再現性と通行・占有条件を検査し、Seed差分や不正Planを画像確認から独立して判断する。
    /// </summary>
    public sealed class ForestRestartLayoutTests
    {
        /// <summary>
        /// 同じSeedの二回の生成で、道・広場・表示Anchor・判定を含め全配置が一致することを確かめる。
        /// </summary>
        [Test]
        public void Generate_SameSeed_ReproducesWholeLayout()
        {
            var first = ForestRestartLayout.Generate(20261003);
            var second = ForestRestartLayout.Generate(20261003);

            Assert.That(ForestRestartLayout.ComputeLayoutHash(second),
                Is.EqualTo(ForestRestartLayout.ComputeLayoutHash(first)));
            Assert.That(second.RoadCells.SetEquals(first.RoadCells), Is.True);
            Assert.That(second.Clearings, Is.EqualTo(first.Clearings));
            Assert.That(second.Obstacles.Count, Is.EqualTo(first.Obstacles.Count));
        }

        /// <summary>
        /// Seedを変えると装飾だけでなく道路と広場と森林Anchorも変わることを確認する。
        /// </summary>
        [Test]
        public void Generate_DifferentSeeds_ChangesTerrainAndForestPlacement()
        {
            var plans = Enumerable.Range(0, 8).Select(ForestRestartLayout.Generate).ToList();
            for (var index = 1; index < plans.Count; index++)
            {
                Assert.That(plans[index].RoadCells.SetEquals(plans[0].RoadCells), Is.False);
                Assert.That(plans[index].Clearings, Is.Not.EqualTo(plans[0].Clearings));
                Assert.That(plans[index].Obstacles.Select(item => item.Position),
                    Is.Not.EqualTo(plans[0].Obstacles.Select(item => item.Position)));
            }
        }

        /// <summary>
        /// 正負と端値を含む複数Seedで、全歩行地と目的地の連結、境界、Footprint保護を検査する。
        /// </summary>
        [TestCase(0)]
        [TestCase(1)]
        [TestCase(7)]
        [TestCase(42)]
        [TestCase(97)]
        [TestCase(20261003)]
        [TestCase(-41)]
        [TestCase(int.MinValue)]
        [TestCase(int.MaxValue)]
        public void Generate_MultipleSeeds_ProducesReachableProtectedLayout(int seed)
        {
            var plan = ForestRestartLayout.Generate(seed);
            var report = ForestRestartLayout.Validate(plan);

            Assert.That(report.Errors, Is.Empty);
            Assert.That(report.ReachableCellCount,
                Is.EqualTo(plan.GroundCells.Count - plan.BlockedCells.Count));
            Assert.That(plan.EventDestinations.Count, Is.EqualTo(6));
            Assert.That(plan.Entrances.Count, Is.EqualTo(4));
            Assert.That(plan.Obstacles.Select(item => item.AssetKey).Distinct(),
                Is.EquivalentTo(new[] { "oak", "grove", "undergrowth" }));
            foreach (var obstacle in plan.Obstacles)
            {
                Assert.That(ForestRestartLayout.CellsIntersecting(obstacle.VisualFootprint)
                    .Any(plan.ProtectedCells.Contains), Is.False);
                Assert.That(obstacle.CollisionRect.width, Is.LessThan(obstacle.VisualFootprint.width));
                Assert.That(obstacle.CollisionRect.height, Is.LessThan(obstacle.VisualFootprint.height));
                Assert.That(obstacle.VisualBounds.xMin * ForestRestartLayout.PixelsPerUnit,
                    Is.EqualTo(Mathf.Round(obstacle.VisualBounds.xMin * ForestRestartLayout.PixelsPerUnit)));
                Assert.That(obstacle.VisualBounds.yMin * ForestRestartLayout.PixelsPerUnit,
                    Is.EqualTo(Mathf.Round(obstacle.VisualBounds.yMin * ForestRestartLayout.PixelsPerUnit)));
            }
        }

        /// <summary>
        /// 連続40Seedでも素材3種を配置でき、全歩行地を接続したまま生成できることを確認する。
        /// </summary>
        [Test]
        public void Generate_FortySeeds_RetainsAllWalkingAndAssetContracts()
        {
            for (var seed = 100; seed < 140; seed++)
            {
                var plan = ForestRestartLayout.Generate(seed);
                var report = ForestRestartLayout.Validate(plan);
                Assert.That(report.IsValid, Is.True, $"seed={seed}: {string.Join("; ", report.Errors)}");
                Assert.That(plan.Obstacles.Select(item => item.AssetKey).Distinct(),
                    Is.EquivalentTo(new[] { "oak", "grove", "undergrowth" }), $"seed={seed}");
            }
        }

        /// <summary>通常24x16unit画面を土だけで埋めない7x5前後の主広場。戦闘最低面積の仕様ではない。</summary>
        [TestCase(20261003)]
        [TestCase(20261004)]
        [TestCase(42)]
        public void Generate_CompositionHasCompactIrregularCentralPlaza(int seed)
        {
            var plan = ForestRestartLayout.Generate(seed);
            var central = plan.Clearings[0];
            Assert.That(central.Contains(plan.Spawn), Is.True);
            Assert.That(central.width, Is.InRange(6, 8));
            Assert.That(central.height, Is.InRange(5, 6));
            Assert.That(central.width * central.height,
                Is.GreaterThan(plan.Clearings.Skip(1).Max(item => item.width * item.height)));
            var pavedCells = 0;
            foreach (var cell in central.allPositionsWithin)
                if (plan.RoadCells.Contains(cell)) pavedCells++;
            Assert.That(pavedCells, Is.GreaterThan(central.width * central.height / 2));
            Assert.That(pavedCells, Is.LessThan(central.width * central.height * 0.95f));
            Assert.That(plan.ProtectedCells.Count - plan.RoadCells.Count, Is.LessThan(50),
                "通路全体へ大きな無装飾の保護帯を追加しない。");
        }

        /// <summary>川を任意の場所で渡れず、一つの橋が閉じても別の橋から迂回できる。</summary>
        [TestCase(20261003)]
        [TestCase(20261004)]
        [TestCase(42)]
        public void Generate_RiverHasTwoWideCrossingsAndAlternativeBankRoute(int seed)
        {
            var plan = ForestRestartLayout.Generate(seed);
            var crossings = ConnectedComponents(plan.BridgeCells);
            Assert.That(crossings, Has.Count.EqualTo(2));
            Assert.That(plan.WaterCells.Except(plan.BridgeCells).All(plan.BlockedCells.Contains), Is.True);
            Assert.That(plan.RoadCells.Intersect(plan.WaterCells), Is.EquivalentTo(plan.BridgeCells));
            Assert.That(plan.CliffCells.All(plan.BlockedCells.Contains), Is.True);
            Assert.That(plan.CliffFaceCells, Is.EquivalentTo(plan.CliffCells.Where(cell =>
                !plan.CliffCells.Contains(cell + Vector2Int.down))));
            var west = plan.Entrances.Single(cell => cell.x == plan.Bounds.xMin);
            var walkable = new HashSet<Vector2Int>(plan.GroundCells.Except(plan.BlockedCells));
            foreach (var crossing in crossings)
            {
                var crossingRows = new HashSet<int>(crossing.Select(cell => cell.y));
                Assert.That(crossingRows, Has.Count.EqualTo(3));
                Assert.That(crossing.Max(cell => cell.x) - crossing.Min(cell => cell.x) + 1,
                    Is.InRange(3, 5), "蛇行する幅3〜4の水域を短い橋で渡る。");
                Assert.That(plan.WaterCells.Where(cell => crossingRows.Contains(cell.y)),
                    Is.EquivalentTo(crossing), "橋の三列すべてが川幅全体を覆う必要があります。");
                var oneBridgeClosed = new HashSet<Vector2Int>(walkable.Except(crossing));
                Assert.That(Flood(oneBridgeClosed, west).Contains(plan.Spawn), Is.True,
                    "もう一方の橋へ回る道が必要です。");
            }
            var bothBridgesClosed = new HashSet<Vector2Int>(walkable.Except(plan.BridgeCells));
            Assert.That(Flood(bothBridgesClosed, west).Contains(plan.Spawn), Is.False,
                "川岸の隙間から橋を使わず渡れてはいけません。");
        }

        /// <summary>店と泉は任意の景観候補であり、外しても同じ試合基盤と通行条件を保つ。</summary>
        [Test]
        public void Generate_OptionalSceneryCanBeDisabledAndParticipatesInLayoutHash()
        {
            var withScenery = ForestRestartLayout.Generate(20261003);
            var withoutScenery = ForestRestartLayout.Generate(20261003, false);
            Assert.That(withScenery.Landmarks.Select(item => item.AssetKey),
                Is.EquivalentTo(new[] { "shop", "fountain" }));
            Assert.That(withoutScenery.Landmarks, Is.Empty);
            Assert.That(ForestRestartLayout.Validate(withoutScenery).Errors, Is.Empty);
            Assert.That(withoutScenery.EventDestinations.Count, Is.EqualTo(6));
            Assert.That(ForestRestartLayout.ComputeLayoutHash(withoutScenery),
                Is.Not.EqualTo(ForestRestartLayout.ComputeLayoutHash(withScenery)));
            foreach (var landmark in withScenery.Landmarks)
            {
                Assert.That(ForestRestartLayout.CellsIntersecting(landmark.Footprint)
                    .Any(withScenery.ProtectedCells.Contains), Is.False);
                Assert.That(ForestRestartLayout.CellsIntersecting(landmark.CollisionRect)
                    .All(withScenery.BlockedCells.Contains), Is.True);
            }
        }

        /// <summary>描画だけの川にしてPhysicsを付け忘れる変更を拒否する。</summary>
        [Test]
        public void Validate_UnblockedDeepWater_ReportsTerrainCollisionFailure()
        {
            var plan = ForestRestartLayout.Generate(42);
            var water = plan.WaterCells.First(cell => !plan.BridgeCells.Contains(cell));
            plan.BlockedCells.Remove(water);
            var report = ForestRestartLayout.Validate(plan);
            Assert.That(report.Errors.Any(error => error.StartsWith("TERRAIN_COLLISION:")), Is.True);
            Assert.That(report.Errors.Any(error => error.StartsWith("COLLISION_CELLS:")), Is.True);
        }

        /// <summary>広場の円弧を除いても道の縁が繰り返し曲がり、長い直交Loopだけへ戻らない。</summary>
        [TestCase(20261003)]
        [TestCase(20261004)]
        [TestCase(42)]
        public void Generate_RoadEdgesChangeDirectionOutsidePlazas(int seed)
        {
            var plan = ForestRestartLayout.Generate(seed);
            var directions = new[] { Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left };
            var corners = plan.RoadCells.Where(cell => !plan.Clearings.Any(rect => rect.Contains(cell))
                && !plan.BridgeCells.Contains(cell) && cell.x > 1 && cell.x < ForestRestartLayout.Width - 2
                && cell.y > 1 && cell.y < ForestRestartLayout.Height - 2)
                .Sum(cell => Enumerable.Range(0, 4).Count(index =>
                    !plan.RoadCells.Contains(cell + directions[index])
                    && !plan.RoadCells.Contains(cell + directions[(index + 1) % 4])));
            Assert.That(corners, Is.GreaterThanOrEqualTo(12));
        }

        /// <summary>通常ortho8画面で、水・二橋・近い崖面・景観・植生が地形そのものとして存在する。</summary>
        [TestCase(20261003)]
        [TestCase(20261004)]
        [TestCase(20261005)]
        [TestCase(20261006)]
        [TestCase(int.MinValue)]
        [TestCase(int.MaxValue)]
        public void Generate_NormalGameplayViewRetainsNearbyTerrainLayers(int seed)
        {
            var plan = ForestRestartLayout.Generate(seed);
            var view = new RectInt(plan.Spawn.x - 12, plan.Spawn.y - 8, 24, 16);
            var visualView = new Rect(view.x, view.y, view.width, view.height);
            Assert.That(plan.WaterCells.Count(view.Contains), Is.GreaterThan(20));
            Assert.That(ConnectedComponents(plan.BridgeCells).All(crossing => crossing.All(view.Contains)), Is.True);
            Assert.That(plan.CliffFaceCells.Count(view.Contains), Is.GreaterThanOrEqualTo(4));
            Assert.That(plan.Landmarks.Any(item => item.VisualBounds.Overlaps(visualView)), Is.True);
            Assert.That(plan.Obstacles.Where(item => item.VisualBounds.Overlaps(visualView))
                .Select(item => item.AssetKey).Distinct().Count(), Is.GreaterThanOrEqualTo(2));
            foreach (var item in plan.Obstacles)
            {
                var visualCells = ForestRestartLayout.CellsIntersecting(item.VisualBounds).ToList();
                Assert.That(visualCells.Any(cell => plan.BridgeCells.Contains(cell)
                    || plan.CliffFaceCells.Contains(cell)), Is.False);
                if (item.AssetKey == "oak" || item.AssetKey == "grove")
                    Assert.That(visualCells.Any(plan.VisualClearanceCells.Contains), Is.False,
                        "樹冠全体が橋の出入口・崖面の手前を隠してはいけません。");
            }
        }

        /// <summary>表示予約域を外して樹冠の通行保護だけに戻す変更は検出し、再現Hashも変える。</summary>
        [Test]
        public void Validate_MissingVisualClearance_ReportsFailureAndChangesHash()
        {
            var plan = ForestRestartLayout.Generate(42);
            var originalHash = ForestRestartLayout.ComputeLayoutHash(plan);
            plan.VisualClearanceCells.Clear();
            var report = ForestRestartLayout.Validate(plan);
            Assert.That(report.Errors.Any(error => error.StartsWith("TERRAIN_VISUAL_CLEARANCE:")), Is.True);
            Assert.That(ForestRestartLayout.ComputeLayoutHash(plan), Is.Not.EqualTo(originalHash));
        }

        /// <summary>
        /// 通常24x16unitで巨大な土面の合流へ戻らず、北・南・西の枝は3Cell幅で広場へ入る。
        /// 55%は景観作成Presetの上限であり、戦闘面積やゲーム内道路の新しい仕様ではない。
        /// </summary>
        [TestCase(20261003)]
        [TestCase(20261004)]
        [TestCase(20261005)]
        [TestCase(20261006)]
        [TestCase(42)]
        [TestCase(int.MinValue)]
        [TestCase(int.MaxValue)]
        public void Generate_ExteriorApproachesStayNarrowAndMergeBeforePlaza(int seed)
        {
            var plan = ForestRestartLayout.Generate(seed);
            var central = plan.Clearings[0];
            var view = new RectInt(plan.Spawn.x - 12, plan.Spawn.y - 8, 24, 16);
            Assert.That(plan.RoadCells.Count(view.Contains), Is.LessThanOrEqualTo(view.width * view.height * 0.55f),
                "近接する斜線を一本の枝へまとめ、通常画面に草・岸・植生が残ること。");
            var broadMerges = plan.RoadCells.Where(cell => view.Contains(cell) && !central.Contains(cell))
                .Count(cell => Enumerable.Range(-2, 5).All(y => Enumerable.Range(-2, 5)
                    .All(x => plan.RoadCells.Contains(cell + new Vector2Int(x, y)))));
            Assert.That(broadMerges, Is.Zero, "広場外に完全な5x5Cellの土面を作る枝の重なりを避ける。");
            var north = new Vector2Int(plan.Spawn.x, central.yMax + 1);
            var south = new Vector2Int(plan.Spawn.x, central.yMin - 1);
            var west = new Vector2Int(central.xMin - 1, plan.Spawn.y);
            for (var offset = -1; offset <= 1; offset++)
            {
                Assert.That(plan.RoadCells.Contains(north + Vector2Int.right * offset), Is.True);
                Assert.That(plan.RoadCells.Contains(south + Vector2Int.right * offset), Is.True);
                Assert.That(plan.RoadCells.Contains(west + Vector2Int.up * offset), Is.True);
            }
        }

        private static List<HashSet<Vector2Int>> ConnectedComponents(IEnumerable<Vector2Int> cells)
        {
            var remaining = new HashSet<Vector2Int>(cells);
            var components = new List<HashSet<Vector2Int>>();
            while (remaining.Count > 0)
            {
                var component = Flood(remaining, remaining.First());
                components.Add(component);
                remaining.ExceptWith(component);
            }
            return components;
        }

        private static HashSet<Vector2Int> Flood(HashSet<Vector2Int> cells, Vector2Int start)
        {
            var visited = new HashSet<Vector2Int>();
            if (!cells.Contains(start)) return visited;
            var pending = new Queue<Vector2Int>();
            visited.Add(start); pending.Enqueue(start);
            while (pending.Count > 0)
            {
                var current = pending.Dequeue();
                foreach (var direction in new[] { Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left })
                {
                    var next = current + direction;
                    if (cells.Contains(next) && visited.Add(next)) pending.Enqueue(next);
                }
            }
            return visited;
        }

        /// <summary>
        /// 目的地を障害物へ変更した場合に、到達性と保護違反を検出する。
        /// </summary>
        [Test]
        public void Validate_BlockedEventDestination_ReportsReachabilityFailure()
        {
            var plan = ForestRestartLayout.Generate(42);
            plan.BlockedCells.Add(plan.EventDestinations[0]);

            var report = ForestRestartLayout.Validate(plan);

            Assert.That(report.IsValid, Is.False);
            Assert.That(report.Errors.Any(error => error.StartsWith("DESTINATION_REACHABILITY:")), Is.True);
            Assert.That(report.Errors.Any(error => error.StartsWith("PROTECTED:")), Is.True);
        }

        /// <summary>
        /// 本来到達できた草地だけを孤立させても、全歩行地の探索で検出できることを確認する。
        /// </summary>
        [Test]
        public void Validate_IsolatedWalkableCell_ReportsDisconnectedGround()
        {
            var plan = ForestRestartLayout.Generate(42);
            var directions = new[] { Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left };
            var target = plan.GroundCells.First(cell => !plan.BlockedCells.Contains(cell)
                && !plan.ProtectedCells.Contains(cell)
                && directions.All(direction => plan.Bounds.Contains(cell + direction)
                    && !plan.ProtectedCells.Contains(cell + direction)));
            foreach (var direction in directions)
            {
                plan.BlockedCells.Add(target + direction);
            }

            Assert.That(ForestRestartLayout.Validate(plan).Errors
                .Any(error => error.StartsWith("WALKABLE_CONNECTION:")), Is.True);
        }

        /// <summary>
        /// 表示矩形全体をCollisionへ転用した部品や重複部品を、Scene書込み前に拒否する。
        /// </summary>
        [Test]
        public void Validate_DuplicateVisualAndFullSpriteCollision_ReportsOccupancyFailure()
        {
            var plan = ForestRestartLayout.Generate(42);
            var first = plan.Obstacles[0];
            plan.Obstacles.Add(new ForestRestartObstaclePlacement(
                first.AssetKey, first.Position, first.VisualFootprint, first.VisualFootprint));

            var report = ForestRestartLayout.Validate(plan);

            Assert.That(report.Errors.Any(error => error.StartsWith("OBSTACLE_BOUNDS:")), Is.True);
            Assert.That(report.Errors.Any(error => error.StartsWith("VISUAL_OVERLAP:")), Is.True);
        }

        /// <summary>
        /// Map外配置、Pixel未整列Anchor、未知素材名を明示的なErrorとして返す。
        /// </summary>
        [Test]
        public void Validate_InvalidAssetAndAnchor_ReportsBoundsAndPixelFailure()
        {
            var plan = ForestRestartLayout.Generate(42);
            plan.Obstacles.Add(new ForestRestartObstaclePlacement(
                "invalid", new Vector2(-0.01f, 0f), new Rect(-2f, 0f, 2f, 2f),
                new Rect(-1.5f, 0f, 0.5f, 0.5f)));

            var report = ForestRestartLayout.Validate(plan);

            Assert.That(report.Errors.Any(error => error.StartsWith("ASSET_KEY:")), Is.True);
            Assert.That(report.Errors.Any(error => error.StartsWith("PIXEL_SNAP:")), Is.True);
            Assert.That(report.Errors.Any(error => error.StartsWith("OBSTACLE_BOUNDS:")), Is.True);
        }
    }
}
