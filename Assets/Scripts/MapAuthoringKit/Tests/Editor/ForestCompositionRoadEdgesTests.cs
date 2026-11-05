using System.Collections.Generic;
using System.IO;
using System.Linq;
using FantasyRoyale.MapAuthoringKit.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace FantasyRoyale.MapAuthoringKit.Tests.Editor
{
    /// <summary>保存された道路Atlasの接続、角の表示と既存路面色の保持を実Pixelで検査する。</summary>
    public sealed class ForestCompositionRoadEdgesTests
    {
        private const int CellSize = 32;
        private const int Masks = 512;
        private const int AtlasColumns = 16;
        private const int AtlasWidth = AtlasColumns * CellSize;
        private const int AtlasHeight = Masks / AtlasColumns * CellSize;
        private const int CenterBit = 1 << 4;
        private Color32[][] atlasPixels;
        private Color32[][] sourcePixels;

        [OneTimeSetUp]
        public void ReadGeneratedAtlasAndOriginalRoadPixels()
        {
            atlasPixels = new Color32[4][];
            sourcePixels = new Color32[4][];
            for (var variant = 0; variant < 4; variant++)
            {
                atlasPixels[variant] = ReadPixels(AtlasPath(variant), out var width, out var height);
                Assert.That(width, Is.EqualTo(AtlasWidth), AtlasPath(variant));
                Assert.That(height, Is.EqualTo(AtlasHeight), AtlasPath(variant));
                var sourcePath = ForestRestartAssetBuilder.ArtRoot + $"/Sprites/road_0f_{variant:00}.png";
                sourcePixels[variant] = ReadPixels(sourcePath, out width, out height);
                Assert.That(new Vector2Int(width, height), Is.EqualTo(new Vector2Int(CellSize, CellSize)), sourcePath);
                Assert.That(sourcePixels[variant].All(pixel => pixel.a == 255), Is.True,
                    "完全な路面を切り出すため、元road_0fは不透明である必要がある。");
            }
        }

        [Test]
        public void AllMasks_KeepRoadCentersAndGrassCentersAccordingToCenterOccupancy()
        {
            for (var variant = 0; variant < 4; variant++)
            {
                Assert.That(atlasPixels[variant].All(pixel => pixel.a == 0 || pixel.a == 255), Is.True,
                    $"variant {variant} must keep binary alpha");
                for (var mask = 0; mask < Masks; mask++)
                {
                    var expectedAlpha = (mask & CenterBit) != 0 ? 255 : 0;
                    // 偶数寸法の中心を囲む4pxを確認し、隣接道路が草Cell中心へ侵入しないことも保証する。
                    for (var y = 15; y <= 16; y++) for (var x = 15; x <= 16; x++)
                        Assert.That(Pixel(variant, mask, x, y).a, Is.EqualTo(expectedAlpha),
                            $"variant {variant}, mask {mask:x3}, center ({x},{y})");
                }
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void AdjacentMasks_ShareEveryBoundaryAlphaPixelForAllOverlappingNeighborhoods(bool vertical)
        {
            // 4x3/3x4の占有Gridには共有6bitと独立した外側3bitずつがある。
            // 全4096通りを調べるため、共有情報が同じなら外側の道路形状が異なっても接続を保つ。
            var gridWidth = vertical ? 3 : 4;
            var firstEdges = new byte[4][][];
            var secondEdges = new byte[4][][];
            for (var variant = 0; variant < 4; variant++)
            {
                firstEdges[variant] = new byte[Masks][];
                secondEdges[variant] = new byte[Masks][];
                for (var mask = 0; mask < Masks; mask++)
                {
                    firstEdges[variant][mask] = BoundaryAlpha(variant, mask, vertical, true);
                    secondEdges[variant][mask] = BoundaryAlpha(variant, mask, vertical, false);
                }
            }
            for (var occupancy = 0; occupancy < 1 << 12; occupancy++)
            {
                var firstMask = MaskFromWindow(occupancy, gridWidth, 0, 0);
                var secondMask = MaskFromWindow(occupancy, gridWidth, vertical ? 0 : 1, vertical ? 1 : 0);
                for (var variant = 0; variant < 4; variant++)
                {
                    var first = firstEdges[variant][firstMask];
                    var second = secondEdges[variant][secondMask];
                    var mismatch = -1;
                    for (var pixel = 0; pixel < CellSize; pixel++)
                        if (first[pixel] != second[pixel]) { mismatch = pixel; break; }
                    Assert.That(mismatch, Is.EqualTo(-1),
                        $"{(vertical ? "vertical" : "horizontal")} shared edge, variant {variant}, occupancy {occupancy:x3}, masks {firstMask:x3}/{secondMask:x3}");
                }
            }
        }

        [Test]
        public void RoundedCorners_FillDiagonalGrassPatchButPreserveRoadCenterAndClipExposedRoadCorners()
        {
            // 中心は草、東・北・北東は道。道路の曲がり角の外側に丸め表示の斜めPatchが必要。
            const int grassBesideNorthEastRoads = (1 << 5) | (1 << 7) | (1 << 8);
            for (var variant = 0; variant < 4; variant++)
            {
                Assert.That(Pixel(variant, grassBesideNorthEastRoads, 31, 31).a, Is.EqualTo(255));
                Assert.That(Pixel(variant, grassBesideNorthEastRoads, 28, 28).a, Is.EqualTo(255));
                Assert.That(Pixel(variant, grassBesideNorthEastRoads, 16, 16).a, Is.EqualTo(0));
                Assert.That(Pixel(variant, grassBesideNorthEastRoads, 0, 0).a, Is.EqualTo(0));
                Assert.That(Pixel(variant, CenterBit, 16, 16).a, Is.EqualTo(255));
                Assert.That(Pixel(variant, CenterBit, 8, 8).a, Is.EqualTo(255));
                foreach (var x in new[] { 0, 31 }) foreach (var y in new[] { 0, 31 })
                    Assert.That(Pixel(variant, CenterBit, x, y).a, Is.EqualTo(0),
                        "隣接する道がない角を切り落としても、道の中心は残す。");
            }
        }

        [Test]
        public void RepresentativeMasks_OnlyClipAlphaAndKeepEveryOriginalRoadRgbPixel()
        {
            var representativeMasks = new[] { 0, CenterBit, (1 << 3) | CenterBit | (1 << 5),
                (1 << 5) | (1 << 7) | (1 << 8), Masks - 1 };
            for (var variant = 0; variant < 4; variant++)
            {
                foreach (var mask in representativeMasks)
                {
                    var changedRgbPixel = -1;
                    for (var y = 0; y < CellSize; y++) for (var x = 0; x < CellSize; x++)
                    {
                        var source = sourcePixels[variant][y * CellSize + x];
                        var actual = Pixel(variant, mask, x, y);
                        if (source.r != actual.r || source.g != actual.g || source.b != actual.b)
                            changedRgbPixel = y * CellSize + x;
                        if (mask == 0) Assert.That(actual.a, Is.EqualTo(0));
                        if (mask == Masks - 1) Assert.That(actual, Is.EqualTo(source));
                    }
                    Assert.That(changedRgbPixel, Is.EqualTo(-1),
                        $"variant {variant}, mask {mask:x3}: RGBは透明になった画素も含めて元路面から変更しない。");
                }
            }
        }

        [Test]
        public void FourAtlases_ImportAllNativeFullRectSpritesWithoutFilteringOrCompression()
        {
            for (var variant = 0; variant < 4; variant++)
            {
                var path = AtlasPath(variant);
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                Assert.That(importer, Is.Not.Null, path);
                Assert.That(importer.textureType, Is.EqualTo(TextureImporterType.Sprite), path);
                Assert.That(importer.spriteImportMode, Is.EqualTo(SpriteImportMode.Multiple), path);
                Assert.That(importer.filterMode, Is.EqualTo(FilterMode.Point), path);
                Assert.That(importer.spritePixelsPerUnit, Is.EqualTo(32f), path);
                Assert.That(importer.textureCompression, Is.EqualTo(TextureImporterCompression.Uncompressed), path);
                Assert.That(importer.crunchedCompression, Is.False, path);
                Assert.That(importer.mipmapEnabled, Is.False, path);
                Assert.That(importer.npotScale, Is.EqualTo(TextureImporterNPOTScale.None), path);
                Assert.That(importer.maxTextureSize, Is.GreaterThanOrEqualTo(AtlasHeight), path);
                var settings = new TextureImporterSettings(); importer.ReadTextureSettings(settings);
                Assert.That(settings.spriteMeshType, Is.EqualTo(SpriteMeshType.FullRect), path);
                var sprites = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().ToArray();
                Assert.That(sprites, Has.Length.EqualTo(Masks), path);
                var byName = sprites.ToDictionary(sprite => sprite.name);
                for (var mask = 0; mask < Masks; mask++)
                {
                    var name = $"road_edge_{mask:x3}_{variant:00}";
                    Assert.That(byName.ContainsKey(name), Is.True, name);
                    var sprite = byName[name];
                    Assert.That(sprite.rect, Is.EqualTo(new Rect(mask % AtlasColumns * CellSize,
                        mask / AtlasColumns * CellSize, CellSize, CellSize)), name);
                    Assert.That(sprite.pivot, Is.EqualTo(new Vector2(16f, 16f)), name);
                    Assert.That(sprite.pixelsPerUnit, Is.EqualTo(32f), name);
                }
            }
        }

        private Color32 Pixel(int variant, int mask, int x, int y)
        {
            var left = mask % AtlasColumns * CellSize;
            var bottom = mask / AtlasColumns * CellSize;
            return atlasPixels[variant][(bottom + y) * AtlasWidth + left + x];
        }

        private byte[] BoundaryAlpha(int variant, int mask, bool vertical, bool upperOrRight)
        {
            var result = new byte[CellSize];
            for (var pixel = 0; pixel < CellSize; pixel++)
                result[pixel] = vertical
                    ? Pixel(variant, mask, pixel, upperOrRight ? CellSize - 1 : 0).a
                    : Pixel(variant, mask, upperOrRight ? CellSize - 1 : 0, pixel).a;
            return result;
        }

        private static int MaskFromWindow(int occupancy, int gridWidth, int left, int bottom)
        {
            var result = 0;
            for (var y = 0; y < 3; y++) for (var x = 0; x < 3; x++)
                if ((occupancy & (1 << ((bottom + y) * gridWidth + left + x))) != 0)
                    result |= 1 << (y * 3 + x);
            return result;
        }

        private static string AtlasPath(int variant) => ForestCompositionRoadEdges.Root + $"/road-edges-{variant:00}.png";

        private static Color32[] ReadPixels(string path, out int width, out int height)
        {
            Assert.That(File.Exists(path), Is.True, path);
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                Assert.That(texture.LoadImage(File.ReadAllBytes(path)), Is.True, path);
                width = texture.width; height = texture.height;
                return texture.GetPixels32();
            }
            finally { Object.DestroyImmediate(texture); }
        }
    }
}
