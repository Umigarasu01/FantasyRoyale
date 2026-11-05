using System.Collections.Generic;
using System.IO;
using System.Linq;
using FantasyRoyale.MapAuthoringKit.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

namespace FantasyRoyale.MapAuthoringKit.Tests.Editor
{
    /// <summary>追加地形の実Pixelと、Planから生成Sceneへ渡した表示・物理境界を検査する。</summary>
    public sealed class ForestCompositionAssetTests
    {
        [Test]
        public void CompositionSprites_KeepNativeDimensionsBinaryAlphaAndPixelImports()
        {
            foreach (var name in ForestCompositionVisuals.ExpectedSpriteNames())
            {
                var path = ForestCompositionVisuals.SpriteRoot + "/" + name + ".png";
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                Assert.That(sprite, Is.Not.Null, path);
                Assert.That(importer, Is.Not.Null, path);
                Assert.That(importer.filterMode, Is.EqualTo(FilterMode.Point), name);
                Assert.That(importer.spritePixelsPerUnit, Is.EqualTo(32f), name);
                Assert.That(importer.textureCompression, Is.EqualTo(TextureImporterCompression.Uncompressed), name);
                Assert.That(importer.mipmapEnabled, Is.False, name);
                Assert.That(importer.spriteImportMode, Is.EqualTo(SpriteImportMode.Single), name);
                var settings = new TextureImporterSettings(); importer.ReadTextureSettings(settings);
                Assert.That(settings.spriteMeshType, Is.EqualTo(SpriteMeshType.FullRect), name);
                var dimensions = ForestCompositionVisuals.SpriteDimensions(name);
                var pixels = ReadPixels(name, out var width, out var height);
                Assert.That(new Vector2Int(width, height), Is.EqualTo(dimensions), name);
                Assert.That(pixels.All(pixel => pixel.a == 0 || pixel.a == 255), Is.True, name);
                Assert.That(pixels.Count(pixel => pixel.a == 255), Is.GreaterThan(width * height / 10), name);
                var expectedPivot = ForestCompositionVisuals.SpritePivot(name);
                Assert.That(sprite.pivot.x, Is.EqualTo(width * expectedPivot.x).Within(0.001f), name);
                Assert.That(sprite.pivot.y, Is.EqualTo(height * expectedPivot.y).Within(0.001f), name);
            }
        }

        [Test]
        public void WaterVariants_StayOpaqueAndShareOpposingBoundaryPixels()
        {
            var variants = Enumerable.Range(0, ForestCompositionVisuals.WaterVariantCount)
                .Select(index => ReadPixels($"water_{index:00}", out _, out _)).ToArray();
            foreach (var variant in variants)
            {
                Assert.That(variant.All(pixel => pixel.a == 255), Is.True);
                for (var i = 0; i < 32; i++)
                {
                    Assert.That(variant[i * 32], Is.EqualTo(variants[0][i * 32 + 31]));
                    Assert.That(variant[i], Is.EqualTo(variants[0][31 * 32 + i]));
                }
            }
            var tile = ForestCompositionVisuals.WaterTile();
            Assert.That(tile.VariantCount, Is.EqualTo(ForestCompositionVisuals.WaterVariantCount));
        }

        [Test]
        public void CliffSlices_PreserveSourceColumnsWithoutScalingOrRecoloring()
        {
            var source = ReadPixels("cliff", out var sourceWidth, out var height);
            for (var cells = 1; cells <= 6; cells++)
            {
                var slice = ReadPixels($"cliff_{cells:00}", out var width, out var sliceHeight);
                Assert.That(width, Is.EqualTo(cells * 32)); Assert.That(sliceHeight, Is.EqualTo(height));
                var left = (sourceWidth - width) / 2;
                for (var y = 0; y < height; y++) for (var x = 0; x < width; x++)
                    Assert.That(slice[y * width + x], Is.EqualTo(source[y * sourceWidth + left + x]));
            }
        }

        [Test]
        public void RepresentativeScene_BakesTerrainCollisionAndKeepsBridgesBelowActors()
        {
            var seed = ForestRestartAssetBuilder.DefaultSeed;
            var qaPath = ForestRestartAssetBuilder.QaRoot + $"/composition-seed-{seed}-validation.json";
            var qa = JsonUtility.FromJson<ForestRestartAssetBuilder.KitQa>(File.ReadAllText(qaPath));
            var row = qa.seeds.Single();
            var plan = ForestRestartLayout.Generate(seed, row.includesLandmarkCandidates);
            var scene = SceneManager.GetSceneByPath(row.scene);
            var wasLoaded = scene.IsValid() && scene.isLoaded;
            if (!wasLoaded) scene = EditorSceneManager.OpenScene(row.scene, OpenSceneMode.Additive);
            try
            {
                var root = scene.GetRootGameObjects().Single(item => item.name == "MapRoot");
                var water = root.transform.Find("Grid/WaterTilemap").GetComponent<Tilemap>();
                var terrain = root.transform.Find("Grid/TerrainTilemap").GetComponent<Tilemap>();
                var collision = root.transform.Find("Grid/CollisionTilemap").GetComponent<Tilemap>();
                var waterCells = OccupiedCells(water); Assert.That(waterCells.SetEquals(plan.WaterCells), Is.True);
                var expected = new HashSet<Vector2Int>(plan.WaterCells); expected.ExceptWith(plan.BridgeCells);
                expected.UnionWith(plan.CliffCells);
                expected.UnionWith(plan.BlockedCells.Where(cell => cell.x == plan.Bounds.xMin || cell.x == plan.Bounds.xMax - 1
                    || cell.y == plan.Bounds.yMin || cell.y == plan.Bounds.yMax - 1));
                Assert.That(OccupiedCells(collision).SetEquals(expected), Is.True,
                    "木と候補建物のFootprintをTilemapへ重ねず、外周・水域・崖だけを焼く。");
                foreach (var cell in plan.BridgeCells)
                {
                    Assert.That(collision.HasTile(new Vector3Int(cell.x, cell.y, 0)), Is.False);
                    Assert.That(terrain.HasTile(new Vector3Int(cell.x, cell.y, 0)), Is.False);
                }
                var bridges = root.GetComponentsInChildren<Transform>(true)
                    .Where(item => item.name.StartsWith("bridge_crossing_")).ToArray();
                Assert.That(bridges, Has.Length.EqualTo(2));
                var bridgeRenderers = bridges.SelectMany(item => item.GetComponentsInChildren<SpriteRenderer>(true)).ToArray();
                Assert.That(bridgeRenderers.Any(renderer => renderer.sprite.name == "bridge"), Is.False,
                    "巨大な橋全体Spriteを残さず、床・欄干・柱をnative部品で配置する。");
                var floors = bridgeRenderers.Where(renderer => renderer.sprite.name == "bridge_floor").ToArray();
                Assert.That(floors, Is.Not.Empty);
                Assert.That(ReadPixels("bridge_floor", out _, out _).All(pixel => pixel.a == 255), Is.True,
                    "床のboundsに含まれる歩行Cellを透明画素で欠かさない。");
                foreach (var floor in floors)
                {
                    Assert.That(floor.sortingLayerName, Is.EqualTo("MapDetail"), floor.name);
                    Assert.That(floor.sortingOrder, Is.EqualTo(0), floor.name);
                    Assert.That(floor.spriteSortPoint, Is.EqualTo(SpriteSortPoint.Pivot), floor.name);
                }
                foreach (var cell in plan.BridgeCells)
                {
                    var center = water.GetCellCenterWorld(new Vector3Int(cell.x, cell.y, 0));
                    Assert.That(floors.Any(floor => floor.bounds.Contains(center)), Is.True,
                        $"通行可能な橋Cell {cell} の中心を床Spriteが覆う。");
                }
                foreach (var bridge in bridges)
                {
                    var renderers = bridge.GetComponentsInChildren<SpriteRenderer>(true);
                    Assert.That(renderers.Any(renderer => renderer.sprite.name == "bridge_floor"), Is.True, bridge.name);
                    var rails = renderers.Where(renderer => renderer.sprite.name.StartsWith("bridge_rail_")).ToArray();
                    var posts = renderers.Where(renderer => renderer.sprite.name.StartsWith("bridge_post_")).ToArray();
                    Assert.That(rails, Is.Not.Empty, bridge.name);
                    Assert.That(posts, Has.Length.EqualTo(4), bridge.name);
                    foreach (var frame in rails.Concat(posts))
                    {
                        Assert.That(frame.sortingLayerName, Is.EqualTo("WorldObjects"), frame.name);
                        Assert.That(frame.sortingOrder, Is.EqualTo(0), frame.name);
                        Assert.That(frame.spriteSortPoint, Is.EqualTo(SpriteSortPoint.Pivot), frame.name);
                    }
                }
                foreach (var renderer in root.GetComponentsInChildren<SpriteRenderer>(true))
                {
                    Assert.That(renderer.transform.localScale, Is.EqualTo(Vector3.one), renderer.name);
                    Assert.That(renderer.transform.localRotation, Is.EqualTo(Quaternion.identity), renderer.name);
                    var position = renderer.transform.position;
                    Assert.That(position.x * 32f, Is.EqualTo(Mathf.Round(position.x * 32f)).Within(0.0001f), renderer.name);
                    Assert.That(position.y * 32f, Is.EqualTo(Mathf.Round(position.y * 32f)).Within(0.0001f), renderer.name);
                    if (renderer.sprite.name.StartsWith("cliff_"))
                    {
                        Assert.That(renderer.bounds.min.x, Is.GreaterThanOrEqualTo(plan.Bounds.xMin - 0.0001f), renderer.name);
                        Assert.That(renderer.bounds.max.x, Is.LessThanOrEqualTo(plan.Bounds.xMax + 0.0001f), renderer.name);
                    }
                }
                var report = MapAuthoringValidator.ValidateScene(scene, true);
                Assert.That(report.HasErrors, Is.False, string.Join("; ", report.Issues.Select(issue => issue.Code + ": " + issue.Message)));
            }
            finally { if (!wasLoaded) EditorSceneManager.CloseScene(scene, true); }
        }

        private static HashSet<Vector2Int> OccupiedCells(Tilemap tilemap)
        {
            var cells = new HashSet<Vector2Int>();
            foreach (var cell in tilemap.cellBounds.allPositionsWithin)
                if (tilemap.HasTile(cell)) cells.Add(new Vector2Int(cell.x, cell.y));
            return cells;
        }

        private static Color32[] ReadPixels(string name, out int width, out int height)
        {
            var image = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                var path = ForestCompositionVisuals.SpriteRoot + "/" + name + ".png";
                Assert.That(image.LoadImage(File.ReadAllBytes(path)), Is.True, path);
                width = image.width; height = image.height; return image.GetPixels32();
            }
            finally { Object.DestroyImmediate(image); }
        }
    }
}
