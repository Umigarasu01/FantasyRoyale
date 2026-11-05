using System;
using System.Collections.Generic;
using System.Linq;
using FantasyRoyale.MapAuthoringKit.OverheadStudyA;
using NUnit.Framework;
using UnityEngine;

namespace FantasyRoyale.MapAuthoringKit.Tests.Editor
{
    // Pure planning tests. No scene, GameObject, Collider or Physics query is created.
    public sealed class StudyANativeFlexibleConnectionPlanTests
    {
        private const string CdKey = "native-rgba:4dcf54b828755fea2d96e8c9751ecc2da2c6a2399df0014a21775046aedf94a1";
        private const int Ppu = 64;
        private const int ConnectorHeight = 384;

        [TestCase(256)]
        [TestCase(384)]
        [TestCase(512)]
        public void LengthChangesActualPlacementWhilePreservingTheThreeNativeCores(int length)
        {
            var catalog = AdmittedCatalog();
            string before = SourceSignature(catalog);
            var plan = StudyANativeFlexibleConnectionPlan.Build(catalog, length, 20261008);
            Assert.That(SourceSignature(catalog), Is.EqualTo(before), "planning must not edit the admitted native sources");
            Assert.That(catalog.Locations.Sum(d => d.SourceOwnerIds.Length), Is.EqualTo(16));
            Assert.That(catalog.Locations.Sum(d => d.FootUnits.Length), Is.EqualTo(12));
            Assert.That(plan.SourceOwnerIds, Is.EqualTo(catalog.Locations.SelectMany(d => d.SourceOwnerIds).ToArray()),
                "the selected source owner order is part of the registry contract");
            Assert.That(plan.FootUnits.Select(FootSignature), Is.EqualTo(catalog.Locations.SelectMany(d => d.FootUnits).Select(FootSignature)),
                "source foot paths, order and authored ties must remain exact");
            Assert.That(plan.Assembly.Blocks.Select(b => b.LocationId), Is.EqualTo(new[] { "B", "J", "D", "C" }));
            Assert.That(plan.Assembly.Blocks.Select(b => b.InstanceId).Distinct().Count(), Is.EqualTo(4));
            AssertBlock(plan, "B", new Vector2Int(0, 384), new Vector2Int(384, 640));
            AssertBlock(plan, "J", new Vector2Int(384, 384), new Vector2Int(length, 384));
            AssertBlock(plan, "D", new Vector2Int(384 + length, 384), new Vector2Int(640, 512));
            AssertBlock(plan, "C", new Vector2Int(384 + length, 0), new Vector2Int(512, 384));
            Assert.That(plan.ConnectorOriginPx, Is.EqualTo(new Vector2Int(384, 384)));
            Assert.That(plan.ConnectorExtentPx, Is.EqualTo(new Vector2Int(length, 384)));
            Assert.That(plan.Geometry.BoundsPx, Is.EqualTo(new RectInt(0, 0, length + 1024, 1024)));
            Assert.That(plan.Assembly.NormalizedBoundsPx, Is.EqualTo(plan.Geometry.BoundsPx));
            Assert.That(plan.Assembly.Connections.Select(c => LinkSignature(plan, c)), Is.EqualTo(new[]
            {
                "B:B.E.road>J:J.W.road", "J:J.E.road>D:D.W.road", "D:D.S.road>C:C.N.road"
            }));
            Assert.That(plan.Assembly.Connections.Count, Is.EqualTo(3));
        }

        [TestCase(256)]
        [TestCase(384)]
        [TestCase(512)]
        public void ExactFloorRunsAndAllPixelMasksProtectBothNativeCollars(int length)
        {
            var plan = StudyANativeFlexibleConnectionPlan.Build(AdmittedCatalog(), length);
            Assert.That(plan.FloorMaskBL, Has.Length.EqualTo(length * ConnectorHeight));
            Assert.That(plan.SoilMaskBL, Has.Length.EqualTo(length * ConnectorHeight));
            Assert.That(plan.SoilCentreYByXBL, Has.Length.EqualTo(length));
            Assert.That(plan.FloorMaskBL.All(v => v == 0 || v == 1), Is.True);
            Assert.That(plan.SoilMaskBL.All(v => v == 0 || v == 1), Is.True);
            AssertDisjointPositiveRuns(plan.JFloorRunsLocalPx, "connector floor runs");
            var runs = Rasterize(plan.JFloorRunsLocalPx, new RectInt(0, 0, length, ConnectorHeight));
            for (int y = 0; y < ConnectorHeight; y++)
            {
                for (int x = 0; x < length; x++)
                {
                    int i = x + y * length;
                    if (runs[i] != (plan.FloorMaskBL[i] != 0))
                        Assert.Fail("floor mask/run coverage differs at BL ({0},{1})", x, y);
                    if (plan.SoilMaskBL[i] != 0 && plan.FloorMaskBL[i] == 0)
                        Assert.Fail("soil has no supporting floor at BL ({0},{1})", x, y);
                    if (x < 32 || x >= length - 32)
                    {
                        if (plan.FloorMaskBL[i] != 1)
                            Assert.Fail("384px floor collar was trimmed at BL ({0},{1})", x, y);
                        bool mouth = x < 32 ? y >= 265 && y < 329 : y >= 137 && y < 201;
                        if ((plan.SoilMaskBL[i] != 0) != mouth)
                            Assert.Fail("native 64px soil mouth changed at BL ({0},{1})", x, y);
                    }
                }
            }
            Assert.That(plan.FloorMaskBL.Count(v => v != 0), Is.LessThan(length * ConnectorHeight),
                "this path must change the walking contour, not merely name a rectangular floor differently");
            // MergeMask emits horizontal maximal runs and extends only exact
            // intervals across rows. Every output rectangle starts at one such
            // run, so its count cannot exceed the independently counted starts.
            // Rounded column contours can make more than two runs on one row;
            // 768 is a resource ceiling checked for all three admitted lengths,
            // not a claim that every row has at most two runs.
            int rowRunStarts = CountMaximalHorizontalRuns(plan.FloorMaskBL, length);
            Assert.That(plan.JFloorRunsLocalPx.Length, Is.LessThanOrEqualTo(rowRunStarts),
                "merged rectangles cannot outnumber the source mask's maximal horizontal runs");
            Assert.That(rowRunStarts, Is.LessThanOrEqualTo(2 * ConnectorHeight),
                "all admitted v1 shapes must stay within the explicit 768 source-run budget");
            for (int x = 0; x < 32; x++)
                Assert.That(plan.SoilCentreYByXBL[x], Is.EqualTo(297f));
            for (int x = length - 32; x < length; x++)
                Assert.That(plan.SoilCentreYByXBL[x], Is.EqualTo(169f));
            for (int x = 0; x < length; x++)
            {
                float centre = plan.SoilCentreYByXBL[x];
                Assert.That(float.IsNaN(centre) || float.IsInfinity(centre), Is.False);
                Assert.That(centre, Is.InRange(169f, 297f));
                int row = Mathf.FloorToInt(centre);
                Assert.That(plan.FloorMaskBL[x + row * length], Is.EqualTo(1), "every road centre has support");
                Assert.That(plan.SoilMaskBL[x + row * length], Is.EqualTo(1), "every road centre lies on soil");
                if (x > 0)
                    Assert.That(centre, Is.LessThanOrEqualTo(plan.SoilCentreYByXBL[x - 1] + 0.0001f));
            }
        }

        [TestCase(256)]
        [TestCase(384)]
        [TestCase(512)]
        public void UnionPerimeterAndNavVoidMatchAnIndependentNativePixelOracle(int length)
        {
            var plan = StudyANativeFlexibleConnectionPlan.Build(AdmittedCatalog(), length);
            Assert.That(plan.Geometry.PixelsPerUnit, Is.EqualTo(Ppu));
            AssertDisjointPositiveRuns(plan.Geometry.FloorsPx, "all floor runs");
            AssertDisjointPositiveRuns(plan.Geometry.PhysicalPerimeterPx, "physical perimeter runs");
            AssertDisjointPositiveRuns(plan.Geometry.VoidInsideFramePx, "navigation void runs");

            // A 65px margin includes one complete pixel beyond the required 64px
            // expansion. The oracle uses two independent one-dimensional sliding
            // windows, rather than production's two-dimensional summed-area table.
            var grid = new RectInt(-65, -65, length + 1154, 1154);
            bool[] expectedFloor = ExpectedFloor(plan, length, grid);
            bool[] actualFloor = Rasterize(plan.Geometry.FloorsPx, grid);
            bool[] actualPerimeter = Rasterize(plan.Geometry.PhysicalPerimeterPx, grid);
            bool[] actualVoid = Rasterize(plan.Geometry.VoidInsideFramePx, grid);
            bool[] expandedFloor = ExpandWithSeparableWindows(expectedFloor, grid.width, grid.height);
            for (int gy = 0; gy < grid.height; gy++)
            {
                for (int gx = 0; gx < grid.width; gx++)
                {
                    int i = gx + gy * grid.width;
                    int x = gx + grid.xMin, y = gy + grid.yMin;
                    bool floor = expectedFloor[i];
                    bool expanded = expandedFloor[i];
                    bool perimeter = expanded && !floor;
                    bool inFrame = plan.Geometry.BoundsPx.Contains(new Vector2Int(x, y));
                    bool frameVoid = inFrame && !floor;
                    if (actualFloor[i] != floor || actualPerimeter[i] != perimeter || actualVoid[i] != frameVoid)
                        Assert.Fail("union mismatch at BL ({0},{1}); floor={2}/{3}, perimeter={4}/{5}, void={6}/{7}",
                            x, y, actualFloor[i], floor, actualPerimeter[i], perimeter, actualVoid[i], frameVoid);
                    if (inFrame && ((actualPerimeter[i] || actualVoid[i]) == floor))
                        Assert.Fail("floor/navigation complement mismatch at BL ({0},{1})", x, y);
                }
            }
            // This point is far inside the southwest frame gap. It is navigation
            // void but must not acquire a large physical slab.
            Assert.That(Contains(plan.Geometry.VoidInsideFramePx, 128, 128), Is.True);
            Assert.That(Contains(plan.Geometry.PhysicalPerimeterPx, 128, 128), Is.False);
            Assert.That(Contains(plan.Geometry.FloorsPx, 128, 128), Is.False);
            Assert.That(Contains(plan.Geometry.PhysicalPerimeterPx, -64, 384), Is.True);
            Assert.That(Contains(plan.Geometry.PhysicalPerimeterPx, -65, 384), Is.False);
            Assert.That(Contains(plan.Geometry.PhysicalPerimeterPx, -64, 320), Is.True,
                "Chebyshev expansion covers the diagonal corner, not only axial strips");
            // Road and non-road grass points on the complete admitted shared
            // edges remain walking floor, rather than narrow mouth-only gates.
            AssertWalkingPixel(plan, 383, 681); AssertWalkingPixel(plan, 384, 681);
            AssertWalkingPixel(plan, 383, 400); AssertWalkingPixel(plan, 384, 400);
            AssertWalkingPixel(plan, 383 + length, 553); AssertWalkingPixel(plan, 384 + length, 553);
            AssertWalkingPixel(plan, 383 + length, 400); AssertWalkingPixel(plan, 384 + length, 400);
            AssertWalkingPixel(plan, 768 + length, 383); AssertWalkingPixel(plan, 768 + length, 384);
        }

        [TestCase(256)]
        [TestCase(384)]
        [TestCase(512)]
        public void SeedIsReproductionMetadataAndAllActualContentHashesIgnoreIt(int length)
        {
            var catalog = AdmittedCatalog();
            var first = StudyANativeFlexibleConnectionPlan.Build(catalog, length, 0);
            Assert.That(first.ContentHash, Does.Match("^[0-9a-f]{64}$"));
            Assert.That(first.GeometryHash, Does.Match("^[0-9a-f]{64}$"));
            foreach (int seed in new[] { 0, 1, -1, int.MinValue, int.MaxValue, 20261008 })
            {
                var next = StudyANativeFlexibleConnectionPlan.Build(catalog, length, seed);
                Assert.That(next.Assembly.Seed, Is.EqualTo(seed));
                Assert.That(next.ContentHash, Is.EqualTo(first.ContentHash));
                Assert.That(next.GeometryHash, Is.EqualTo(first.GeometryHash));
                Assert.That(PlacementSignature(next), Is.EqualTo(PlacementSignature(first)));
                Assert.That(next.FloorMaskBL, Is.EqualTo(first.FloorMaskBL));
                Assert.That(next.SoilMaskBL, Is.EqualTo(first.SoilMaskBL));
                Assert.That(next.SoilCentreYByXBL, Is.EqualTo(first.SoilCentreYByXBL));
            }
            var alteredOrder = AdmittedCatalog();
            Array.Reverse(alteredOrder.Locations);
            var reordered = StudyANativeFlexibleConnectionPlan.Build(alteredOrder, length, 0);
            Assert.That(reordered.ContentHash, Is.EqualTo(first.ContentHash), "source collection order is not geometry");
            Assert.That(reordered.GeometryHash, Is.EqualTo(first.GeometryHash));
        }

        [Test]
        public void ThreeLengthsHaveThreeGeometryAndContentHashes()
        {
            var plans = new[] { 256, 384, 512 }.Select(l => StudyANativeFlexibleConnectionPlan.Build(AdmittedCatalog(), l)).ToArray();
            Assert.That(plans.Select(p => p.GeometryHash).Distinct().Count(), Is.EqualTo(3));
            Assert.That(plans.Select(p => p.ContentHash).Distinct().Count(), Is.EqualTo(3));
            Assert.That(plans.Select(p => p.FloorMaskBL.Count(v => v != 0)).Distinct().Count(), Is.EqualTo(3));
        }

        [TestCase(256)]
        [TestCase(384)]
        [TestCase(512)]
        public void ConnectorFloorHasOneFourNeighbourComponentJoiningBothCompleteCollars(int length)
        {
            var plan = StudyANativeFlexibleConnectionPlan.Build(AdmittedCatalog(), length);
            var seen = new bool[plan.FloorMaskBL.Length];
            var queue = new Queue<int>();
            seen[0] = true; queue.Enqueue(0);
            int reached = 0;
            while (queue.Count != 0)
            {
                int at = queue.Dequeue(); reached++;
                int x = at % length, y = at / length;
                if (x > 0) EnqueueFloor(at - 1, plan.FloorMaskBL, seen, queue);
                if (x + 1 < length) EnqueueFloor(at + 1, plan.FloorMaskBL, seen, queue);
                if (y > 0) EnqueueFloor(at - length, plan.FloorMaskBL, seen, queue);
                if (y + 1 < ConnectorHeight) EnqueueFloor(at + length, plan.FloorMaskBL, seen, queue);
            }
            Assert.That(reached, Is.EqualTo(plan.FloorMaskBL.Count(v => v != 0)), "no isolated walking island or diagonal-only contact");
            for (int y = 0; y < ConnectorHeight; y++)
            {
                Assert.That(seen[y * length], Is.True);
                Assert.That(seen[length - 1 + y * length], Is.True);
            }
        }

        [TestCase(0)]
        [TestCase(128)]
        [TestCase(320)]
        [TestCase(-256)]
        [TestCase(int.MaxValue)]
        public void UncertifiedLengthIsRejectedWithoutEditingSources(int length)
        {
            var catalog = AdmittedCatalog();
            string before = SourceSignature(catalog);
            Assert.That(() => StudyANativeFlexibleConnectionPlan.Build(catalog, length), Throws.ArgumentException);
            Assert.That(SourceSignature(catalog), Is.EqualTo(before));
        }

        [TestCase("extent")]
        [TestCase("anchor")]
        [TestCase("direction")]
        [TestCase("width")]
        [TestCase("offset")]
        [TestCase("terrain")]
        [TestCase("height")]
        [TestCase("key")]
        [TestCase("duplicate-owner")]
        [TestCase("foreign-foot")]
        [TestCase("cd-key")]
        [TestCase("cd-match")]
        public void ChangedNativeSourceContractIsRejectedWithoutFurtherMutation(string field)
        {
            var catalog = AdmittedCatalog();
            var b = catalog.Locations.Single(d => d.Id == "B");
            var d = catalog.Locations.Single(l => l.Id == "D");
            var mouth = b.Ports.Single(p => p.Id == "B.E.road");
            switch (field)
            {
                case "extent": b.ExtentPx = new Vector2Int(448, 640); break;
                case "anchor": mouth.AnchorPx = new Vector2Int(384, 298); break;
                case "direction": mouth.Direction = StudyAPortDirection.North; break;
                case "width": mouth.MouthWidthPx = 96; break;
                case "offset": mouth.AlongMinOffsetPx = -31; break;
                case "terrain": mouth.Terrain = StudyATerrain.Stone; break;
                case "height": mouth.Height = 1; break;
                case "key": mouth.CompatibilityKey = "uncertified"; break;
                case "duplicate-owner": d.SourceOwnerIds[0] = b.SourceOwnerIds[0]; break;
                case "foreign-foot": d.FootUnits[0].OwnerId = "missing-owner"; break;
                case "cd-key":
                    catalog.Locations.Single(l => l.Id == "C").Ports.Single(p => p.Id == "C.N.road").CompatibilityKey = "uncertified-CD";
                    d.Ports.Single(p => p.Id == "D.S.road").CompatibilityKey = "uncertified-CD";
                    break;
                case "cd-match": catalog.SharedEdgeMatches[0].LengthPx = 256; break;
            }
            string before = SourceSignature(catalog);
            Assert.That(() => StudyANativeFlexibleConnectionPlan.Build(catalog, 256), Throws.ArgumentException);
            Assert.That(SourceSignature(catalog), Is.EqualTo(before));
        }

        [Test]
        public void ReturnedMutableArraysAndPlacementAreNotSharedBetweenBuilds()
        {
            var catalog = AdmittedCatalog();
            string before = SourceSignature(catalog);
            var first = StudyANativeFlexibleConnectionPlan.Build(catalog, 256);
            first.FloorMaskBL[0] = 0;
            first.SoilMaskBL[265 * 256] = 0;
            first.Assembly.Blocks[0].OriginPx = new Vector2Int(900, 900);
            first.FootUnits[0].TieOrder = 999;
            first.ConnectorPorts[0].AnchorPx = Vector2Int.zero;
            first.SourceOwnerIds[0] = "result-only-owner";
            var next = StudyANativeFlexibleConnectionPlan.Build(catalog, 256);
            Assert.That(next.FloorMaskBL[0], Is.EqualTo(1));
            Assert.That(next.SoilMaskBL[265 * 256], Is.EqualTo(1));
            Assert.That(next.Assembly.Blocks[0].OriginPx, Is.EqualTo(new Vector2Int(0, 384)));
            Assert.That(next.FootUnits[0].TieOrder, Is.EqualTo(0));
            Assert.That(next.ConnectorPorts[0].AnchorPx, Is.EqualTo(new Vector2Int(0, 297)));
            Assert.That(next.SourceOwnerIds[0], Is.EqualTo(catalog.Locations[0].SourceOwnerIds[0]));
            Assert.That(SourceSignature(catalog), Is.EqualTo(before));
        }

        private static void AssertBlock(StudyANativeFlexibleConnectionPlan.Plan plan, string id, Vector2Int origin, Vector2Int extent)
        {
            var b = plan.Assembly.Blocks.Single(p => p.LocationId == id);
            Assert.That(b.OriginPx, Is.EqualTo(origin), id + " origin");
            Assert.That(b.ExtentPx, Is.EqualTo(extent), id + " native extent");
        }

        private static string LinkSignature(StudyANativeFlexibleConnectionPlan.Plan plan, StudyANativeLocationConnection link) =>
            plan.Assembly.Blocks[link.FromBlock].LocationId + ":" + link.FromPortId + ">" +
            plan.Assembly.Blocks[link.ToBlock].LocationId + ":" + link.ToPortId;

        private static string PlacementSignature(StudyANativeFlexibleConnectionPlan.Plan plan) => string.Join("|",
            plan.Assembly.Blocks.Select(b => b.LocationId + ":" + b.OriginPx.x + "," + b.OriginPx.y + "/" + b.ExtentPx.x + "," + b.ExtentPx.y)
                .Concat(plan.Assembly.Connections.Select(c => LinkSignature(plan, c))));
        private static string FootSignature(StudyANativeFootUnit foot) => foot.OwnerId + "/" + foot.PartPath + "/" + foot.TieOrder;

        private static bool[] ExpectedFloor(StudyANativeFlexibleConnectionPlan.Plan plan, int length, RectInt grid)
        {
            // These three source rectangles and J's fixed origin are independent
            // literals from the admitted B/J/D/C layout, not result block data.
            bool[] floor = Rasterize(new[] { new RectInt(0,384,384,640), new RectInt(384+length,384,640,512),
                new RectInt(384+length,0,512,384) }, grid);
            for (int y = 0; y < ConnectorHeight; y++)
                for (int x = 0; x < length; x++)
                    if (plan.FloorMaskBL[x + y * length] != 0)
                        floor[(x + 384 - grid.xMin) + (y + 384 - grid.yMin) * grid.width] = true;
            return floor;
        }

        private static bool[] Rasterize(IEnumerable<RectInt> rectangles, RectInt grid)
        {
            var raster = new bool[checked(grid.width * grid.height)];
            foreach (RectInt r in rectangles)
            {
                Assert.That(r.xMin, Is.GreaterThanOrEqualTo(grid.xMin));
                Assert.That(r.yMin, Is.GreaterThanOrEqualTo(grid.yMin));
                Assert.That(r.xMax, Is.LessThanOrEqualTo(grid.xMax));
                Assert.That(r.yMax, Is.LessThanOrEqualTo(grid.yMax));
                for (int y = r.yMin; y < r.yMax; y++)
                    for (int x = r.xMin; x < r.xMax; x++)
                        raster[x - grid.xMin + (y - grid.yMin) * grid.width] = true;
            }
            return raster;
        }

        private static bool[] ExpandWithSeparableWindows(bool[] cells, int width, int height)
        {
            var horizontal = new bool[cells.Length];
            var result = new bool[cells.Length];
            for (int y = 0; y < height; y++)
            {
                int count = 0;
                for (int x = 0; x <= Math.Min(Ppu, width - 1); x++)
                    if (cells[x + y * width]) count++;
                for (int x = 0; x < width; x++)
                {
                    horizontal[x + y * width] = count != 0;
                    if (x - Ppu >= 0 && cells[x - Ppu + y * width]) count--;
                    if (x + Ppu + 1 < width && cells[x + Ppu + 1 + y * width]) count++;
                }
            }
            for (int x = 0; x < width; x++)
            {
                int count = 0;
                for (int y = 0; y <= Math.Min(Ppu, height - 1); y++)
                    if (horizontal[x + y * width]) count++;
                for (int y = 0; y < height; y++)
                {
                    result[x + y * width] = count != 0;
                    if (y - Ppu >= 0 && horizontal[x + (y - Ppu) * width]) count--;
                    if (y + Ppu + 1 < height && horizontal[x + (y + Ppu + 1) * width]) count++;
                }
            }
            return result;
        }

        private static void EnqueueFloor(int at, byte[] mask, bool[] seen, Queue<int> queue)
        {
            if (mask[at] == 0 || seen[at]) return;
            seen[at] = true; queue.Enqueue(at);
        }

        private static int CountMaximalHorizontalRuns(byte[] mask, int width)
        {
            int count = 0;
            for (int y = 0; y < ConnectorHeight; y++)
                for (int x = 0; x < width; x++)
                    if (mask[x + y * width] != 0 && (x == 0 || mask[x - 1 + y * width] == 0)) count++;
            return count;
        }

        private static void AssertDisjointPositiveRuns(RectInt[] runs, string name)
        {
            Assert.That(runs, Is.Not.Null.And.Not.Empty, name);
            for (int i = 0; i < runs.Length; i++)
            {
                Assert.That(runs[i].width, Is.GreaterThan(0), name);
                Assert.That(runs[i].height, Is.GreaterThan(0), name);
                for (int j = 0; j < i; j++)
                    Assert.That(runs[i].xMin < runs[j].xMax && runs[j].xMin < runs[i].xMax &&
                        runs[i].yMin < runs[j].yMax && runs[j].yMin < runs[i].yMax, Is.False, name + " has overlapping interiors");
            }
        }

        private static bool Contains(IEnumerable<RectInt> runs, int x, int y) => runs.Any(r => r.Contains(new Vector2Int(x, y)));
        private static void AssertWalkingPixel(StudyANativeFlexibleConnectionPlan.Plan plan, int x, int y)
        {
            Assert.That(Contains(plan.Geometry.FloorsPx, x, y), Is.True, "walking floor at " + x + "," + y);
            Assert.That(Contains(plan.Geometry.PhysicalPerimeterPx, x, y), Is.False);
            Assert.That(Contains(plan.Geometry.VoidInsideFramePx, x, y), Is.False);
        }

        private static string SourceSignature(StudyANativeLocationCatalog catalog) => catalog.Id + "|" + catalog.Version + "|" + catalog.PixelsPerUnit + "|" +
            string.Join("|", catalog.Locations.Select(d => d.Id + ":" + d.PrefabPath + ":" + d.GroundPath + ":" + d.PrefabSHA256 + ":" + d.GroundSHA256 +
                ":" + d.ExtentPx.x + "," + d.ExtentPx.y + ":" + d.SpawnPx.x.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + "," + d.SpawnPx.y.ToString("R", System.Globalization.CultureInfo.InvariantCulture) +
                ":" + string.Join(",", d.SourceOwnerIds) + ":" + string.Join(",", d.FootUnits.Select(f => f.OwnerId + "/" + f.PartPath + "/" + f.TieOrder)) +
                ":" + string.Join(",", d.Ports.Select(p => p.Id + "/" + p.Direction + "/" + p.AnchorPx.x + "/" + p.AnchorPx.y + "/" + p.AlongMinOffsetPx + "/" + p.MouthWidthPx + "/" + p.Terrain + "/" + p.CompatibilityKey + "/" + p.Height)))) +
            "|" + string.Join("|", catalog.SharedEdgeMatches.Select(m => m.FromLocationId + "/" + m.ToLocationId + "/" + m.FromDirection + "/" + m.FromAlongMinPx + "/" + m.ToAlongMinPx + "/" + m.LengthPx));

        private static StudyANativeLocationCatalog AdmittedCatalog()
        {
            // Owner IDs and foot registration order below are the native source
            // metadata, read from the project catalog. Fixture paths/hashes are
            // intentionally synthetic; this test does not claim art admission.
            var bOwners = new[] { "056a7f9bbef449af99b2fd0df03d017e", "0b1c89b9003b467e999e960df2d018b7", "11be76f41a174f1da3bb6a1f5ad9a43c", "4fe337fa894245fea6581bdd6f1e1ca6", "69dcc2791c594fc2b88c0f18bce3d26e", "8b5ac9fe52cf4ebcaa8926daafa8ec11", "eae44c72b2a1432fa12dc172f83a0fe5" };
            var cOwners = new[] { "c_grass_east02", "c_grass_southwest01", "c_rock_southwest01", "c_tree_northwest_anchor01", "c_tree_northwest_broad02", "c_tree_south_broad03", "c_tree_southeast_upright04" };
            var dOwners = new[] { "d_tree_northeast_upright02", "d_tree_northwest_anchor01" };
            return new StudyANativeLocationCatalog
            {
                Id = "native-admitted-flexible-test-only", Version = 1, PixelsPerUnit = Ppu,
                Locations = new[] {
                    Location("B",384,640,new Vector2(336,297),bOwners,new[] { bOwners[5],bOwners[2],bOwners[4],bOwners[0],bOwners[6] },
                        Port("B.W.road",StudyAPortDirection.West,0,297,64,-32,"native-source-AB"),
                        Port("B.E.road",StudyAPortDirection.East,384,297,64,-32,StudyABlockGenerator.NativeEPortCompatibilityKey)),
                    Location("C",512,384,new Vector2(32,169),cOwners,new[] { cOwners[4],cOwners[3],cOwners[2],cOwners[5],cOwners[6] },
                        Port("C.W.road",StudyAPortDirection.West,0,169,64,-32,"native-source-BC"),
                        Port("C.N.road",StudyAPortDirection.North,384,384,96,-48,CdKey)),
                    Location("D",640,512,new Vector2(384,16),dOwners,dOwners,
                        Port("D.S.road",StudyAPortDirection.South,384,0,96,-48,CdKey),
                        Port("D.W.road",StudyAPortDirection.West,0,169,64,-32,StudyABlockGenerator.NativeEPortCompatibilityKey))
                },
                SharedEdgeMatches = new[] { new StudyANativeSharedEdgeMatch { FromLocationId="C",ToLocationId="D",FromDirection=StudyAPortDirection.North,FromAlongMinPx=0,ToAlongMinPx=0,LengthPx=512 } }
            };
        }

        private static StudyANativeLocationDefinition Location(string id,int w,int h,Vector2 spawn,string[] owners,string[] feet,params StudyANativeLocationPort[] ports) =>
            new StudyANativeLocationDefinition { Id=id,ExtentPx=new Vector2Int(w,h),SpawnPx=spawn,SourceOwnerIds=owners,Ports=ports,
                FootUnits=feet.Select(o => new StudyANativeFootUnit { OwnerId=o,PartPath="PresentOwned/VisualsFoot",TieOrder=0 }).ToArray(),
                PrefabPath="Assets/TestOnly/"+id+".prefab",GroundPath="Assets/TestOnly/"+id+".png",PrefabSHA256=new string(id[0],64).ToLowerInvariant(),GroundSHA256=new string(id[0],64).ToLowerInvariant() };
        private static StudyANativeLocationPort Port(string id,StudyAPortDirection direction,int x,int y,int width,int offset,string key) =>
            new StudyANativeLocationPort { Id=id,Direction=direction,AnchorPx=new Vector2Int(x,y),MouthWidthPx=width,AlongMinOffsetPx=offset,Terrain=StudyATerrain.Soil,CompatibilityKey=key,Height=0 };
    }
}
