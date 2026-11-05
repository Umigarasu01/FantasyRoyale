using System;
using System.Collections.Generic;
using System.Linq;
using FantasyRoyale.MapAuthoringKit.OverheadStudyA;
using NUnit.Framework;
using UnityEngine;

namespace FantasyRoyale.MapAuthoringKit.Tests.Editor
{
    public sealed class StudyABlockGeneratorTests
    {
        [Test]
        public void PresetsAndAllWalkPairsStayConnectedAcrossRepresentativeAndExtremeSeeds()
        {
            var seeds = Enumerable.Range(0, 48).Concat(new[] { 20261003, 20261004, 20261005, 20261006, int.MinValue, int.MaxValue }).ToArray();
            foreach (var seed in seeds)
            {
                var blocks = new[] { StudyABlockPresets.Plaza(seed), StudyABlockPresets.ForestPath(seed), StudyABlockPresets.BridgeBank(seed) };
                foreach (var block in blocks)
                {
                    Assert.That(block.Validate(), Is.Empty, block.Id + ":" + seed);
                    Assert.That(block.Ports.Take(2).All(p => p.Width == 3 && p.IsWalkable), Is.True);
                    foreach (var next in blocks)
                    {
                        var pair = StudyABlockGenerator.Pair(block, 1, next, 0, seed);
                        Assert.That(pair.Validate(), Is.Empty, block.Id + "+" + next.Id + ":" + seed);
                        Assert.That(pair.Bounds.width, Is.LessThanOrEqualTo(24));
                        Assert.That(pair.Bounds.height, Is.LessThanOrEqualTo(16));
                        Assert.That(pair.Connections.Count, Is.EqualTo(1));
                        var a = pair.Blocks[0]; var b = pair.Blocks[1];
                        foreach (var cell in a.Definition.Ports[1].Cells)
                        {
                            var at = cell + a.Origin;
                            Assert.That(pair.WalkCells.Contains(at) && pair.WalkCells.Contains(at + Vector2Int.right), Is.True, "whole opening band must cross the seam");
                            Assert.That(pair.WalkCells.Contains(at - Vector2Int.right) && pair.WalkCells.Contains(at + Vector2Int.right * 2), Is.True, "both inward approaches must remain open");
                        }
                    }
                }
            }
        }

        [TestCase("direction")]
        [TestCase("width")]
        [TestCase("terrain")]
        [TestCase("height")]
        public void IncompatibleValidPortsAreRejectedWithoutRotatingOrMutatingInput(string mismatch)
        {
            var first = StudyABlockPresets.Plaza(17); var next = StudyABlockPresets.ForestPath(17);
            var nextPort = mismatch == "direction" ? 1 : 0;
            if (mismatch == "width") next.Ports[0].Width = 2;
            if (mismatch == "height") next.Ports[0].Height = 1;
            if (mismatch == "terrain")
            {
                first.Ports[1].Terrain = StudyATerrain.Grass;
                first.SoilCells.RemoveAll(c => first.Ports[1].Cells.Contains(c));
            }
            Assert.That(first.Validate(), Is.Empty); Assert.That(next.Validate(), Is.Empty);
            var existing = StudyABlockGenerator.Single(first, Vector2Int.zero, 17); var before = existing.StableHash();
            Assert.That(StudyABlockGenerator.TryConnect(existing, 0, 1, next, nextPort, out var placement, out var reason), Is.False);
            Assert.That(placement, Is.Null); StringAssert.Contains(mismatch, reason);
            Assert.That(existing.StableHash(), Is.EqualTo(before)); Assert.That(existing.Blocks.Count, Is.EqualTo(1));
        }

        [Test]
        public void OccupiedPortCannotBeConnectedAgain()
        {
            var pair = StudyABlockGenerator.Pair(StudyABlockPresets.Plaza(4), 1, StudyABlockPresets.ForestPath(4), 0, 4);
            Assert.That(StudyABlockGenerator.TryConnect(pair, 0, 1, StudyABlockPresets.ForestPath(4), 0, out _, out var reason), Is.False);
            StringAssert.Contains("already connected", reason);
        }

        [Test]
        public void ChangedHeightOnAnExistingConnectionInvalidatesTheAssembly()
        {
            var pair = StudyABlockGenerator.Pair(StudyABlockPresets.Plaza(4), 1, StudyABlockPresets.ForestPath(4), 0, 4);
            pair.Blocks[1].Definition.Ports[0].Height = 2;
            Assert.That(pair.Validate().Any(e => e.Contains("connection contract")), Is.True);
            Assert.That(StudyABlockGenerator.TryConnect(pair, 1, 1, StudyABlockPresets.Plaza(4), 0, out _, out _), Is.False);
        }

        [Test]
        public void ValidOppositePortsRejectFootprintOverlapWithAnotherPlacedBlock()
        {
            var a = OpenRectangle("a", 12, 12, true); var b = OpenRectangle("b", 12, 12, true); var c = OpenRectangle("c", 12, 12, false);
            var assembly = StudyABlockGenerator.Single(a, Vector2Int.zero, 99);
            assembly = StudyABlockGenerator.AddConnected(assembly, 0, 2, b, 3);
            assembly = StudyABlockGenerator.AddConnected(assembly, 1, 1, c, 0);
            var tall = OpenRectangle("tall", 12, 20, false);
            Assert.That(tall.Validate(), Is.Empty);
            Assert.That(StudyABlockGenerator.TryConnect(assembly, 0, 1, tall, 0, out _, out var reason), Is.False);
            StringAssert.Contains("extents overlap", reason);
        }

        [Test]
        public void BridgeFloorIncludesBankApproachesWhileWaterPortsRemainNonwalking()
        {
            var bank = StudyABlockPresets.BridgeBank(20261003);
            Assert.That(bank.BridgeCells.Count, Is.EqualTo(6)); Assert.That(bank.BridgeFloorCells.Count, Is.EqualTo(12));
            Assert.That(bank.BridgeCells.All(c => bank.WaterCells.Contains(c) && bank.WalkCells.Contains(c)), Is.True);
            Assert.That(bank.BridgeFloorCells.All(bank.WalkCells.Contains), Is.True);
            Assert.That(bank.WaterCells.Except(bank.BridgeCells).Any(bank.WalkCells.Contains), Is.False);
            foreach (var port in bank.Ports.Skip(2))
            { Assert.That(port.IsWalkable, Is.False); Assert.That(port.Width, Is.EqualTo(2)); Assert.That(port.Cells.Any(bank.WalkCells.Contains), Is.False); }
        }

        [Test]
        public void MissingCellInPortBandOrInwardApproachIsRejected()
        {
            var first = StudyABlockPresets.Plaza(9);
            first.WalkCells.Remove(first.Ports[1].Cells.First() - Vector2Int.right);
            Assert.That(first.Validate().Any(e => e.Contains("inward approach")), Is.True);
        }

        [Test]
        public void MislabeledGrassAndStonePortsAreRejected()
        {
            var block = StudyABlockPresets.Plaza(0);
            block.Ports[0].Terrain = StudyATerrain.Grass;
            Assert.That(block.Validate().Any(e => e.Contains("grass port terrain")), Is.True);
            block.Ports[0].Terrain = StudyATerrain.Stone;
            Assert.That(block.Validate().Any(e => e.Contains("stone port terrain")), Is.True);
        }

        [TestCase(20261003)]
        [TestCase(0)]
        [TestCase(-1)]
        [TestCase(int.MinValue)]
        [TestCase(int.MaxValue)]
        public void SeedAndHashAreDeterministicAndTranslationIsPure(int seed)
        {
            var first = StudyABlockGenerator.Pair(StudyABlockPresets.Plaza(seed), 1, StudyABlockPresets.ForestPath(seed), 0, seed);
            var second = StudyABlockGenerator.Pair(StudyABlockPresets.Plaza(seed), 1, StudyABlockPresets.ForestPath(seed), 0, seed);
            Assert.That(first.StableHash(), Is.EqualTo(second.StableHash()));
            var originalHash = first.StableHash(); var moved = first.Translate(new Vector2Int(2, 2));
            Assert.That(first.StableHash(), Is.EqualTo(originalHash)); Assert.That(moved.StableHash(), Is.Not.EqualTo(originalHash));
            Assert.That(moved.WalkCells.SetEquals(first.WalkCells.Select(c => c + new Vector2Int(2, 2))), Is.True);
            Assert.That(moved.Bounds, Is.EqualTo(new RectInt(2, 2, 20, 12))); Assert.That(moved.Validate(), Is.Empty);
        }

        [Test]
        public void SerializationRoundTripPreservesAuthoringData()
        {
            var original = StudyABlockPresets.BridgeBank(31);
            var decoded = JsonUtility.FromJson<StudyABlockDefinition>(JsonUtility.ToJson(original));
            Assert.That(decoded.Validate(), Is.Empty);
            Assert.That(decoded.Ports.Count, Is.EqualTo(original.Ports.Count));
            Assert.That(decoded.VariationRegions.Count, Is.EqualTo(original.VariationRegions.Count));
            Assert.That(StudyABlockGenerator.Single(decoded, Vector2Int.zero, 31).StableHash(), Is.EqualTo(StudyABlockGenerator.Single(original, Vector2Int.zero, 31).StableHash()));
        }

        private static StudyABlockDefinition OpenRectangle(string id, int width, int height, bool fourPorts)
        {
            var block = new StudyABlockDefinition { Id = id, Extent = new Vector2Int(width, height), Spawn = new Vector2(5.5f, 5.5f) };
            block.Ports.Add(new StudyABlockPort { Id = "west", Position = new Vector2Int(0, 4), Direction = StudyAPortDirection.West });
            block.Ports.Add(new StudyABlockPort { Id = "east", Position = new Vector2Int(width - 1, 4), Direction = StudyAPortDirection.East });
            if (fourPorts)
            {
                block.Ports.Add(new StudyABlockPort { Id = "north", Position = new Vector2Int(4, height - 1), Direction = StudyAPortDirection.North });
                block.Ports.Add(new StudyABlockPort { Id = "south", Position = new Vector2Int(4, 0), Direction = StudyAPortDirection.South });
            }
            var openings = new HashSet<Vector2Int>(block.Ports.SelectMany(p => p.Cells));
            foreach (var cell in block.Bounds.allPositionsWithin)
            {
                if ((cell.x == 0 || cell.y == 0 || cell.x == width - 1 || cell.y == height - 1) && !openings.Contains(cell)) block.SolidCells.Add(cell);
                else block.WalkCells.Add(cell);
                if (openings.Contains(cell)) block.SoilCells.Add(cell);
            }
            block.MainRoute.Add(block.Spawn); return block;
        }
    }
}
