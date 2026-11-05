using System;
using System.Collections.Generic;
using System.Linq;
using FantasyRoyale.MapAuthoringKit.OverheadStudyA;
using NUnit.Framework;
using UnityEngine;

namespace FantasyRoyale.MapAuthoringKit.Tests.Editor
{
    public sealed class StudyANativeLocationGeneratorTests
    {
        [TestCase(20261003)]
        [TestCase(20261006)]
        [TestCase(0)]
        [TestCase(-1)]
        [TestCase(int.MinValue)]
        [TestCase(int.MaxValue)]
        public void IdenticalSeedAndCatalogReproduceTheActualPixelPlacement(int seed)
        {
            var catalog = Catalog();
            var before = JsonUtility.ToJson(catalog);
            var first = StudyABlockGenerator.GenerateNativeAssembly(catalog, seed);
            var second = StudyABlockGenerator.GenerateNativeAssembly(catalog, seed);
            Assert.That(StudyABlockGenerator.ValidateNativeAssembly(catalog, first), Is.Empty);
            Assert.That(LayoutSignature(first), Is.EqualTo(LayoutSignature(second)));
            Assert.That(ConnectionSignature(first), Is.EqualTo(ConnectionSignature(second)));
            Assert.That(first.ContentHash, Is.EqualTo(second.ContentHash));
            Assert.That(first.ContentHash, Does.Match("^[0-9a-f]{64}$"));
            Assert.That(first.Seed, Is.EqualTo(seed));
            Assert.That(JsonUtility.ToJson(catalog), Is.EqualTo(before), "generation must not edit the source catalog");
        }

        [Test]
        public void FullCatalogHasOneNormalizedGeometryAndItsHashDoesNotDependOnSeed()
        {
            var catalog = Catalog();
            var layouts = new HashSet<string>();
            var hashes = new HashSet<string>();
            foreach (var seed in Enumerable.Range(0, 96).Concat(new[] { int.MinValue, int.MaxValue, 20261006 }))
            {
                var assembly = StudyABlockGenerator.GenerateNativeAssembly(catalog, seed, 2, 4, 4);
                Assert.That(StudyABlockGenerator.ValidateNativeAssembly(catalog, assembly), Is.Empty);
                Assert.That(assembly.Blocks.Select(b => b.LocationId).OrderBy(id => id), Is.EqualTo(new[] { "A", "B", "C", "D" }));
                Assert.That(assembly.Blocks.Select(b => b.InstanceId).Distinct().Count(), Is.EqualTo(4));
                Assert.That(assembly.Connections.Count, Is.EqualTo(3));
                Assert.That(assembly.NormalizedBoundsPx, Is.EqualTo(new RectInt(0, 0, 1792, 1024)));
                Assert.That(Block(assembly, "A").OriginPx, Is.EqualTo(new Vector2Int(0, 0)));
                Assert.That(Block(assembly, "B").OriginPx, Is.EqualTo(new Vector2Int(768, 0)));
                Assert.That(Block(assembly, "C").OriginPx, Is.EqualTo(new Vector2Int(1152, 128)));
                Assert.That(Block(assembly, "D").OriginPx, Is.EqualTo(new Vector2Int(1152, 512)));
                Assert.That(assembly.FixedFullCatalogLayoutQualification, Is.Not.Null.And.Not.Empty,
                    "the four-input catalog must disclose that it permits one full layout");
                layouts.Add(LayoutSignature(assembly));
                hashes.Add(assembly.ContentHash);
            }
            Assert.That(layouts.Count, Is.EqualTo(1));
            Assert.That(hashes.Count, Is.EqualTo(1), "changing only Seed must not create a new content hash");
        }

        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        public void RequestedCountSelectsOnlyConnectedSubsetsAndReachesEveryAvailableSubset(int count)
        {
            var catalog = Catalog();
            var seen = new HashSet<string>();
            var hashBySubset = new Dictionary<string, string>();
            for (var seed = 0; seed < 256; seed++)
            {
                var assembly = StudyABlockGenerator.GenerateNativeAssembly(catalog, seed, 2, 4, count);
                Assert.That(assembly.Blocks.Count, Is.EqualTo(count));
                Assert.That(assembly.Blocks.Select(b => b.LocationId).Distinct().Count(), Is.EqualTo(count));
                Assert.That(StudyABlockGenerator.ValidateNativeAssembly(catalog, assembly), Is.Empty);
                var subset = Subset(assembly);
                seen.Add(subset);
                if (hashBySubset.TryGetValue(subset, out var expectedHash))
                    Assert.That(assembly.ContentHash, Is.EqualTo(expectedHash), "one subset has one normalized geometry: " + subset);
                else hashBySubset.Add(subset, assembly.ContentHash);
            }
            var expected = count == 2 ? new[] { "AB", "BC", "CD" } : count == 3 ? new[] { "ABC", "BCD" } : new[] { "ABCD" };
            Assert.That(seen.OrderBy(s => s), Is.EqualTo(expected));
        }

        [Test]
        public void DefaultCountProducesSixConnectedSubsetLayoutsRatherThanSixNamesForFixedABCD()
        {
            var catalog = Catalog();
            var subsets = new HashSet<string>();
            var hashes = new HashSet<string>();
            for (var seed = 0; seed < 384; seed++)
            {
                var assembly = StudyABlockGenerator.GenerateNativeAssembly(catalog, seed);
                Assert.That(assembly.Blocks.Count, Is.InRange(2, 4));
                Assert.That(StudyABlockGenerator.ValidateNativeAssembly(catalog, assembly), Is.Empty);
                subsets.Add(Subset(assembly));
                hashes.Add(assembly.ContentHash);
            }
            Assert.That(subsets.OrderBy(s => s), Is.EqualTo(new[] { "AB", "ABC", "ABCD", "BC", "BCD", "CD" }));
            Assert.That(hashes.Count, Is.EqualTo(6));
        }

        [TestCase("direction")]
        [TestCase("key")]
        [TestCase("terrain")]
        [TestCase("width")]
        [TestCase("height")]
        public void IncompatibleNativePortsAreRejectedWithoutMovingExistingBlocks(string mismatch)
        {
            var catalog = Catalog();
            var existing = FindSubset(catalog, "AB");
            var candidate = Port(catalog, "C", "west");
            switch (mismatch)
            {
                case "direction": candidate.Direction = StudyAPortDirection.North; candidate.AnchorPx = new Vector2Int(64, 384); break;
                case "key": candidate.CompatibilityKey = "different-source-mouth"; break;
                case "terrain": candidate.Terrain = StudyATerrain.Grass; break;
                case "width": candidate.MouthWidthPx = 48; break;
                case "height": candidate.Height = 1; break;
            }
            Assert.That(StudyABlockGenerator.ValidateNativeAssembly(catalog, existing), Is.Empty);
            AssertRejectedWithoutMutation(catalog, existing, Index(existing, "B"), "east", "C", "west");
        }

        [Test]
        public void UsedNativePortCannotBeConsumedASecondTime()
        {
            var catalog = Catalog();
            var existing = FindSubset(catalog, "AB");
            AssertRejectedWithoutMutation(catalog, existing, Index(existing, "A"), "east", "C", "west");
        }

        [Test]
        public void NativeAlignmentKeepsThe128PixelBCStepWithoutRoundingToWorldCells()
        {
            var catalog = Catalog();
            var existing = FindSubset(catalog, "AB");
            Assert.That(StudyABlockGenerator.TryConnectNative(catalog, existing, Index(existing, "B"), "east", "C", "west",
                out var placement, out var reason), Is.True, reason);
            Assert.That(placement.OriginPx - Block(existing, "B").OriginPx, Is.EqualTo(new Vector2Int(384, 128)));
            Assert.That(placement.ExtentPx, Is.EqualTo(new Vector2Int(512, 384)));
            Assert.That(Port(catalog, "B", "east").AnchorPx.y, Is.EqualTo(297));
            Assert.That(Port(catalog, "C", "west").AnchorPx.y, Is.EqualTo(169));
        }

        [Test]
        public void OnePixelAnchorShiftCannotSilentlyChangeTheApprovedFullEdgeMatch()
        {
            var catalog = Catalog();
            var existing = FindSubset(catalog, "AB");
            Port(catalog, "C", "west").AnchorPx += Vector2Int.up;
            Assert.That(StudyABlockGenerator.ValidateNativeAssembly(catalog, existing), Is.Empty);
            AssertRejectedWithoutMutation(catalog, existing, Index(existing, "B"), "east", "C", "west");
        }

        [Test]
        public void MatchingOppositeMouthsStillRejectOverlapWithAnEarlierFloor()
        {
            var catalog = Catalog();
            var existing = FindSubset(catalog, "ABC");
            // This remains a valid south mouth. Its left shift would place D over B.
            Port(catalog, "D", "south").AnchorPx = new Vector2Int(512, 0);
            catalog.SharedEdgeMatches.Single(m => m.FromLocationId == "C" && m.ToLocationId == "D").ToAlongMinPx = 128;
            Assert.That(StudyABlockGenerator.ValidateNativeAssembly(catalog, existing), Is.Empty);
            AssertRejectedWithoutMutation(catalog, existing, Index(existing, "C"), "north", "D", "south");
        }

        [Test]
        public void SecondaryBDTouchNeedsAnExplicitFullSharedEdgeMatch()
        {
            var catalog = Catalog();
            var full = StudyABlockGenerator.GenerateNativeAssembly(catalog, 20261006, 2, 4, 4);
            var existing = FindSubset(catalog, "ABC");
            // B/D share open grass; they have no road-mouth connection to stand in for this rule.
            catalog.SharedEdgeMatches = catalog.SharedEdgeMatches.Where(m => !(m.FromLocationId == "B" && m.ToLocationId == "D")).ToArray();
            Assert.That(StudyABlockGenerator.ValidateNativeAssembly(catalog, existing), Is.Empty);
            AssertRejectedWithoutMutation(catalog, existing, Index(existing, "C"), "north", "D", "south");
            Assert.That(StudyABlockGenerator.ValidateNativeAssembly(catalog, full), Is.Not.Empty);
            Assert.That(() => StudyABlockGenerator.GenerateNativeAssembly(catalog, 20261006, 2, 4, 4), Throws.InstanceOf<ArgumentException>());
        }

        [Test]
        public void UnreachableRequestedCountFailsInsteadOfReturningAFewerBlockFallback()
        {
            var catalog = Catalog();
            Port(catalog, "D", "south").CompatibilityKey = "not-a-C-exit";
            Assert.That(() => StudyABlockGenerator.GenerateNativeAssembly(catalog, 17, 2, 4, 4), Throws.InstanceOf<ArgumentException>());
            var pair = StudyABlockGenerator.GenerateNativeAssembly(catalog, 17, 2, 4, 2);
            Assert.That(pair.Blocks.Count, Is.EqualTo(2));
            Assert.That(StudyABlockGenerator.ValidateNativeAssembly(catalog, pair), Is.Empty);
        }

        [TestCase("ppu")]
        [TestCase("zeroExtent")]
        [TestCase("negativeExtent")]
        [TestCase("fractionalWorldWidth")]
        [TestCase("fractionalWorldHeight")]
        [TestCase("offBoundaryAnchor")]
        public void InvalidNativePixelContractsFailBeforeGeneration(string fault)
        {
            var catalog = Catalog();
            var c = catalog.Locations.Single(l => l.Id == "C");
            switch (fault)
            {
                case "ppu": catalog.PixelsPerUnit = 32; break;
                case "zeroExtent": c.ExtentPx = new Vector2Int(0, 384); break;
                case "negativeExtent": c.ExtentPx = new Vector2Int(512, -64); break;
                case "fractionalWorldWidth": c.ExtentPx = new Vector2Int(513, 384); break;
                case "fractionalWorldHeight": c.ExtentPx = new Vector2Int(512, 385); break;
                case "offBoundaryAnchor": Port(catalog, "C", "west").AnchorPx += Vector2Int.right; break;
            }
            Assert.That(() => StudyABlockGenerator.GenerateNativeAssembly(catalog, 20261006, 2, 4, 4), Throws.InstanceOf<ArgumentException>());
        }

        [TestCase("outsideExtent")]
        [TestCase("notFinite")]
        public void InvalidNativeSpawnIsRejectedBeforeGeneration(string fault)
        {
            var catalog = Catalog();
            var a = catalog.Locations.Single(l => l.Id == "A");
            a.SpawnPx = fault == "outsideExtent"
                ? new Vector2(a.ExtentPx.x, 297f)
                : new Vector2(float.NaN, 297f);
            Assert.That(() => StudyABlockGenerator.GenerateNativeAssembly(catalog, 20261006, 2, 4, 4),
                Throws.InstanceOf<ArgumentException>());
        }

        [TestCase("prefab")]
        [TestCase("ground")]
        public void ChangedSourceGuardChangesContentHashWithoutChangingPlacement(string sourceKind)
        {
            var catalog = Catalog();
            var a = catalog.Locations.Single(l => l.Id == "A");
            a.PrefabSHA256 = new string('1', 64);
            a.GroundSHA256 = new string('2', 64);
            var before = StudyABlockGenerator.GenerateNativeAssembly(catalog, 20261006, 2, 4, 4);
            if (sourceKind == "prefab") a.PrefabSHA256 = new string('3', 64);
            else a.GroundSHA256 = new string('3', 64);
            var after = StudyABlockGenerator.GenerateNativeAssembly(catalog, 20261006, 2, 4, 4);
            Assert.That(StudyABlockGenerator.ValidateNativeAssembly(catalog, after), Is.Empty);
            Assert.That(LayoutSignature(after), Is.EqualTo(LayoutSignature(before)));
            Assert.That(ConnectionSignature(after), Is.EqualTo(ConnectionSignature(before)));
            Assert.That(after.Seed, Is.EqualTo(before.Seed));
            Assert.That(after.ContentHash, Is.Not.EqualTo(before.ContentHash),
                "the source guard must invalidate prior content even when the geometric result is unchanged");
        }

        [TestCase(1)]
        [TestCase(5)]
        public void RequestedCountOutsideTheConfiguredRangeFails(int requested)
        {
            Assert.That(() => StudyABlockGenerator.GenerateNativeAssembly(Catalog(), 0, 2, 4, requested), Throws.InstanceOf<ArgumentException>());
        }

        [Test]
        public void ValidationChecksActualCoordinatesEvenWhenDerivedHashAndBoundsAreAbsent()
        {
            var catalog = Catalog();
            var full = StudyABlockGenerator.GenerateNativeAssembly(catalog, 7, 2, 4, 4);
            var decoded = JsonUtility.FromJson<StudyANativeLocationAssembly>(JsonUtility.ToJson(full));
            decoded.ContentHash = string.Empty;
            decoded.NormalizedBoundsPx = default(RectInt);
            Assert.That(StudyABlockGenerator.ValidateNativeAssembly(catalog, decoded), Is.Empty);
            Block(decoded, "C").OriginPx += Vector2Int.up;
            Assert.That(StudyABlockGenerator.ValidateNativeAssembly(catalog, decoded), Is.Not.Empty);
        }

        [Test]
        public void CatalogSerializationRetainsFractionalWorldAnchorsAndApprovedContacts()
        {
            var original = Catalog();
            var decoded = JsonUtility.FromJson<StudyANativeLocationCatalog>(JsonUtility.ToJson(original));
            Assert.That(Port(decoded, "B", "east").AnchorPx, Is.EqualTo(new Vector2Int(384, 297)));
            Assert.That(Port(decoded, "C", "west").AnchorPx, Is.EqualTo(new Vector2Int(0, 169)));
            var decodedSpawn = decoded.Locations.Single(l => l.Id == "A").SpawnPx;
            Assert.That(decodedSpawn.x, Is.EqualTo(41.6f), "JSON must retain the fractional pixel spawn without integer rounding");
            Assert.That(decodedSpawn.y, Is.EqualTo(297f));
            Assert.That(decodedSpawn.x / decoded.PixelsPerUnit, Is.EqualTo(0.65f));
            Assert.That(decoded.SharedEdgeMatches.Length, Is.EqualTo(4));
            var before = StudyABlockGenerator.GenerateNativeAssembly(original, 11, 2, 4, 4);
            var after = StudyABlockGenerator.GenerateNativeAssembly(decoded, 11, 2, 4, 4);
            Assert.That(LayoutSignature(after), Is.EqualTo(LayoutSignature(before)));
            Assert.That(after.ContentHash, Is.EqualTo(before.ContentHash));
        }

        [TestCase("null-owner-array")]
        [TestCase("null-foot-array")]
        [TestCase("null-foot-entry")]
        [TestCase("null-owner-id")]
        [TestCase("empty-owner-id")]
        [TestCase("duplicate-owner-within-location")]
        [TestCase("duplicate-owner-in-unselected-location")]
        [TestCase("unknown-foot-owner")]
        [TestCase("null-foot-owner")]
        [TestCase("empty-foot-owner")]
        [TestCase("null-foot-path")]
        [TestCase("empty-foot-path")]
        [TestCase("absolute-foot-path")]
        [TestCase("duplicate-foot-pair")]
        public void InvalidOwnerFootMetadataAnywhereInCatalogRejectsPairGeneration(string fault)
        {
            var catalog = Catalog();
            foreach (var location in catalog.Locations)
            {
                location.SourceOwnerIds = new[] { "owner_" + location.Id };
                location.FootUnits = new[] { new StudyANativeFootUnit { OwnerId = "owner_" + location.Id,
                    PartPath = "PresentOwned/VisualsFoot", TieOrder = 0 } };
            }
            var a = catalog.Locations.Single(l => l.Id == "A");
            var d = catalog.Locations.Single(l => l.Id == "D");
            switch (fault)
            {
                case "null-owner-array": d.SourceOwnerIds = null; break;
                case "null-foot-array": d.FootUnits = null; break;
                case "null-foot-entry": d.FootUnits[0] = null; break;
                case "null-owner-id": d.SourceOwnerIds[0] = null; break;
                case "empty-owner-id": d.SourceOwnerIds[0] = string.Empty; break;
                case "duplicate-owner-within-location": d.SourceOwnerIds = new[] { "owner_D", "owner_D" }; break;
                case "duplicate-owner-in-unselected-location": d.SourceOwnerIds[0] = a.SourceOwnerIds[0]; d.FootUnits[0].OwnerId = a.SourceOwnerIds[0]; break;
                case "unknown-foot-owner": d.FootUnits[0].OwnerId = "not_an_owned_source"; break;
                case "null-foot-owner": d.FootUnits[0].OwnerId = null; break;
                case "empty-foot-owner": d.FootUnits[0].OwnerId = string.Empty; break;
                case "null-foot-path": d.FootUnits[0].PartPath = null; break;
                case "empty-foot-path": d.FootUnits[0].PartPath = string.Empty; break;
                case "absolute-foot-path": d.FootUnits[0].PartPath = "/PresentOwned/VisualsFoot"; break;
                case "duplicate-foot-pair": d.FootUnits = new[] { d.FootUnits[0], new StudyANativeFootUnit
                    { OwnerId = "owner_D", PartPath = "PresentOwned/VisualsFoot", TieOrder = 0 } }; break;
            }
            // Even a target of two cannot bypass invalid metadata in the unselected catalog entries.
            Assert.That(() => StudyABlockGenerator.GenerateNativeAssembly(catalog, 17, 2, 4, 2), Throws.InstanceOf<ArgumentException>());
        }

        [Test]
        public void CatalogAllowsEmptyMetadataAndDistinctFeetForOneOwnedSource()
        {
            var empty = Catalog();
            Assert.That(StudyABlockGenerator.GenerateNativeAssembly(empty, 17, 2, 4, 4).Blocks.Count, Is.EqualTo(4));
            var a = empty.Locations.Single(l => l.Id == "A");
            a.SourceOwnerIds = new[] { "owner_A" };
            a.FootUnits = new[] {
                new StudyANativeFootUnit { OwnerId = "owner_A", PartPath = "PresentOwned/BridgeBackFoot" },
                new StudyANativeFootUnit { OwnerId = "owner_A", PartPath = "PresentOwned/BridgeFrontFoot" }
            };
            var assembly = StudyABlockGenerator.GenerateNativeAssembly(empty, 17, 2, 4, 4);
            Assert.That(StudyABlockGenerator.ValidateNativeAssembly(empty, assembly), Is.Empty);
        }

        private static void AssertRejectedWithoutMutation(StudyANativeLocationCatalog catalog, StudyANativeLocationAssembly existing,
            int existingBlockIndex, string existingPortId, string candidateLocationId, string candidatePortId)
        {
            var beforeAssembly = JsonUtility.ToJson(existing);
            var beforeCatalog = JsonUtility.ToJson(catalog);
            Assert.That(StudyABlockGenerator.TryConnectNative(catalog, existing, existingBlockIndex, existingPortId,
                candidateLocationId, candidatePortId, out var placement, out var reason), Is.False);
            Assert.That(placement, Is.Null);
            Assert.That(reason, Is.Not.Null.And.Not.Empty);
            Assert.That(JsonUtility.ToJson(existing), Is.EqualTo(beforeAssembly));
            Assert.That(JsonUtility.ToJson(catalog), Is.EqualTo(beforeCatalog));
        }

        private static StudyANativeLocationAssembly FindSubset(StudyANativeLocationCatalog catalog, string expected)
        {
            for (var seed = 0; seed < 256; seed++)
            {
                var assembly = StudyABlockGenerator.GenerateNativeAssembly(catalog, seed, 2, 4, expected.Length);
                if (Subset(assembly) == expected) return assembly;
            }
            Assert.Fail("Seed sample did not produce the expected connected subset: " + expected);
            return null;
        }

        private static StudyANativePlacedLocation Block(StudyANativeLocationAssembly assembly, string id)
            => assembly.Blocks.Single(b => b.LocationId == id);
        private static int Index(StudyANativeLocationAssembly assembly, string id)
            => assembly.Blocks.FindIndex(b => b.LocationId == id);
        private static StudyANativeLocationPort Port(StudyANativeLocationCatalog catalog, string locationId, string portId)
            => catalog.Locations.Single(l => l.Id == locationId).Ports.Single(p => p.Id == portId);
        private static string Subset(StudyANativeLocationAssembly assembly)
            => string.Concat(assembly.Blocks.Select(b => b.LocationId).OrderBy(id => id, StringComparer.Ordinal));
        private static string LayoutSignature(StudyANativeLocationAssembly assembly)
            => string.Join(";", assembly.Blocks.OrderBy(b => b.LocationId, StringComparer.Ordinal).Select(b =>
                b.LocationId + "@" + b.OriginPx.x + "," + b.OriginPx.y + ":" + b.ExtentPx.x + "," + b.ExtentPx.y));
        private static string ConnectionSignature(StudyANativeLocationAssembly assembly)
            => string.Join(";", assembly.Connections.Select(c =>
            {
                var a = assembly.Blocks[c.FromBlock].LocationId + "/" + c.FromPortId;
                var b = assembly.Blocks[c.ToBlock].LocationId + "/" + c.ToPortId;
                return StringComparer.Ordinal.Compare(a, b) < 0 ? a + "-" + b : b + "-" + a;
            }).OrderBy(s => s, StringComparer.Ordinal));

        private static StudyANativeLocationCatalog Catalog()
        {
            // Geometry-only fixture paths: no assets, sprite pixels, colliders or Unity scenes are created.
            return new StudyANativeLocationCatalog
            {
                Version = 1,
                PixelsPerUnit = 64,
                Locations = new[]
                {
                    Location("A", 768, 640, NativePort("east", StudyAPortDirection.East, 768, 297, 64, -32)),
                    Location("B", 384, 640, NativePort("west", StudyAPortDirection.West, 0, 297, 64, -32), NativePort("east", StudyAPortDirection.East, 384, 297, 64, -32)),
                    Location("C", 512, 384, NativePort("west", StudyAPortDirection.West, 0, 169, 64, -32), NativePort("north", StudyAPortDirection.North, 384, 384, 96, -48)),
                    Location("D", 640, 512, NativePort("south", StudyAPortDirection.South, 384, 0, 96, -48))
                },
                SharedEdgeMatches = new[]
                {
                    Match("A", "B", StudyAPortDirection.East, 0, 0, 640),
                    Match("B", "C", StudyAPortDirection.East, 128, 0, 384),
                    Match("C", "D", StudyAPortDirection.North, 0, 0, 512),
                    Match("B", "D", StudyAPortDirection.East, 512, 0, 128)
                }
            };
        }

        private static StudyANativeLocationDefinition Location(string id, int width, int height, params StudyANativeLocationPort[] ports)
            => new StudyANativeLocationDefinition { Id = id, PrefabPath = "Assets/NativeLocationTestOnly/" + id + ".prefab", ExtentPx = new Vector2Int(width, height),
                SpawnPx = id == "A" ? new Vector2(41.6f, 297f) : Vector2.zero, Ports = ports };
        private static StudyANativeLocationPort NativePort(string id, StudyAPortDirection direction, int x, int y, int width, int alongMinOffset)
            => new StudyANativeLocationPort { Id = id, Direction = direction, AnchorPx = new Vector2Int(x, y), AlongMinOffsetPx = alongMinOffset,
                MouthWidthPx = width, Terrain = StudyATerrain.Soil, CompatibilityKey = "soil-mouth-" + width, Height = 0 };
        private static StudyANativeSharedEdgeMatch Match(string from, string to, StudyAPortDirection direction, int fromAlongMin, int toAlongMin, int length)
            => new StudyANativeSharedEdgeMatch { FromLocationId = from, ToLocationId = to, FromDirection = direction,
                FromAlongMinPx = fromAlongMin, ToAlongMinPx = toAlongMin, LengthPx = length };
    }
}
