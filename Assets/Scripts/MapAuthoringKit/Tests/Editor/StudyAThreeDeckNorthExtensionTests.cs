using System;
using System.IO;
using System.Linq;
using System.Reflection;
using FantasyRoyale.MapAuthoringKit.Editor;
using FantasyRoyale.MapAuthoringKit.OverheadStudyA;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace FantasyRoyale.MapAuthoringKit.Tests.Editor
{
    public sealed class StudyAThreeDeckNorthExtensionTests
    {
        [TestCase(8)]
        [TestCase(16)]
        public void ThreeDeckNorthRowSharesVisibleAndCollisionApertureAndKeepsAllTwelveFootRectsFree(int step)
        {
            const int seed = 20261003;
            var plaza = StudyABlockPresets.Plaza(seed); var path = StudyABlockPresets.ForestPath(seed); var bank = StudyABlockPresets.BridgeBank(seed);
            var assembly = StudyABlockGenerator.AddConnected(StudyABlockGenerator.Single(plaza, new Vector2Int(0, 2), seed), 0, 1, path, 0);
            assembly = StudyABlockGenerator.AddConnected(assembly, 1, 1, bank, 0);
            var polygon = StudyABridgeRiverFineGeometry.DefaultPolygon(bank, new Vector2Int(20, 2)); polygon.PixelStep = step;
            var plan = StudyABridgeRiverFineGeometry.MakePlan(assembly, new[] { polygon }, 32, 16);
            Assert.That(assembly.Validate(), Is.Empty);
            Assert.That(assembly.BridgeFloorCells.Count, Is.EqualTo(12));
            Assert.That(plan.DeckBounds.Single(), Is.EqualTo(new Rect(24, 6, 4, 3 + 1f / 64)));
            // Source raster stays unchanged beneath both the original deck and its copied opaque top row.
            Assert.That(plan.VisibleWaterMask, Is.EqualTo(StudyABridgeRiverFineGeometry.Rasterize(new[] { polygon }, Vector2Int.zero, 32, 16)));
            var removedNorthPixels = 0; var mismatch = 0;
            for (var y = 0; y < plan.Height; y++) for (var x = 0; x < plan.Width; x++)
            {
                var i = y * plan.Width + x; var point = new Vector2((x + .5f) / 64, (y + .5f) / 64);
                var expected = plan.DeckBounds[0].Contains(point) ? (byte)0 : plan.VisibleWaterMask[i];
                if (plan.WaterCollisionMask[i] != expected) mismatch++;
                if (y == 9 * 64 && x >= 24 * 64 && x < 28 * 64 && plan.VisibleWaterMask[i] == 255) removedNorthPixels++;
            }
            Assert.That(mismatch, Is.Zero, "Collision must be exactly visible water minus the visible deck.");
            Assert.That(removedNorthPixels, Is.EqualTo(128));
            foreach (var cell in assembly.BridgeFloorCells)
            {
                var body = (Vector2)cell + new Vector2(.5f, .5f);
                var foot = new Rect(body.x - .24f, body.y + .22f - .275f, .48f, .55f);
                Assert.That(plan.IsFreeRect(foot), Is.True, "Unchanged source-equivalent full foot bounds must remain free.");
            }
        }

        [Test]
        public void ExtendedDeckPreservesEveryNativeRgbaAndCopiesOnlyItsOpaqueNorthRow()
        {
            var source = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                Assert.That(source.LoadImage(File.ReadAllBytes("Assets/Art/Map/OverheadStudyA/DerivedBridgeRiverV1/bridge_deck.png"), false), Is.True);
                Assert.That(new Vector2Int(source.width, source.height), Is.EqualTo(new Vector2Int(256, 192)));
                var native = source.GetPixels32();
                var method = typeof(StudyABridgeRiverRenderer).GetMethod("ComposeNorthExtendedDeck", BindingFlags.NonPublic | BindingFlags.Static);
                Assert.That(method, Is.Not.Null);
                var extended = (Color32[])method.Invoke(null, new object[] { native });
                Assert.That(extended.Length, Is.EqualTo(256 * 193));
                Assert.That(extended.Take(native.Length), Is.EqualTo(native));
                Assert.That(extended.Skip(native.Length), Is.EqualTo(native.Skip(256 * 191)));
                Assert.That(extended.Skip(native.Length).All(p => p.a == 255), Is.True);
                var transparent = (Color32[])native.Clone(); transparent[256 * 191].a = 0;
                var failure = Assert.Throws<TargetInvocationException>(() => method.Invoke(null, new object[] { transparent }));
                Assert.That(failure.InnerException, Is.TypeOf<InvalidDataException>());
            }
            finally { Object.DestroyImmediate(source); }
        }

        [TestCase(false, 8, "41fad173fdb29ac3b83fb5297c8e59b6ba2dcf8ecb834550304fdf4c4269db40", "2e22970790d06d45c17cd330742409ff7912c7d23c3f7b1152eb5be1f88219a8", "6cf39f302c667c914020b421f7a3628d1177a1299f400ed40f951aae8d05503f")]
        [TestCase(false, 16, "cdf07fcbe7ac97d67740d748f5bb52a4986ac9256b12c03d52e7ffa7e15a2058", "f5825ce39bac2682bfcc15c0ccffb0895fd1004b4aea9bcbd3bc2ad8adc48931", "cf39521995066747bd5fb910aaf7209bc78361f0ce9942e6887e4b9670ae8edc")]
        [TestCase(true, 8, "42d08c173164aca3b9a111836b0691b5ccd35e4aea87f354e42c6838cb55686e", "07857ac584de491b56b347b1a4176a85283f7070dfcaab30390f8d1ca35ab8e1", "6707f455edb9964c4f4b33927578d4cbc0b9586d375a5e282e59622d2d8f2e8c")]
        [TestCase(true, 16, "8c534d59e7472ba762d6c37ea4a24135320ab073cc187398324a66cba8debd4f", "735f5bc2c56dae3134f30b94e91c1c57c99982c186f6681f8f5dab5f766b5615", "71664574bab9f01f19cec349a011a66b8b02c408b162495192b4b994f89f2d89")]
        public void SingleAndPairKeepPreviouslyReviewedMaskHashes(bool paired, int step, string visibleHash, string collisionHash, string freeHash)
        {
            var bank = StudyABlockPresets.BridgeBank(0);
            var assembly = paired ? StudyABlockGenerator.Pair(StudyABlockPresets.ForestPath(0), 1, bank, 0, 0).Translate(new Vector2Int(2, 2))
                : StudyABlockGenerator.Single(bank, new Vector2Int(6, 2), 0);
            var placed = assembly.Blocks.Single(b => b.Definition.Kind == StudyABlockKind.BridgeBank);
            var polygon = StudyABridgeRiverFineGeometry.DefaultPolygon(bank, placed.Origin); polygon.PixelStep = step;
            var plan = StudyABridgeRiverFineGeometry.MakePlan(assembly, new[] { polygon }, 24, 16);
            Assert.That(plan.DeckBounds.Single().height, Is.EqualTo(3));
            Assert.That(plan.VisibleWaterSha256, Is.EqualTo(visibleHash));
            Assert.That(plan.WaterCollisionSha256, Is.EqualTo(collisionHash));
            Assert.That(plan.FineFreeWalkSha256, Is.EqualTo(freeHash));
        }
    }
}
