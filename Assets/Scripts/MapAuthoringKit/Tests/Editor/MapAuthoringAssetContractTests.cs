using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FantasyRoyale.MapAuthoringKit;
using FantasyRoyale.MapAuthoringKit.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

namespace FantasyRoyale.MapAuthoringKit.Tests.Editor
{
    /// <summary>再制作した素材の実Pixel・Import・Scene契約と既存の汎用生成基盤を確認する。</summary>
    public sealed class MapAuthoringAssetContractTests
    {
        private const string ReferenceScenePath = ForestRestartAssetBuilder.ReferenceScenePath;
        private const string Renderer2DDataPath = "Assets/Settings/Renderer2D.asset";

        [Test]
        public void RestartSprites_ExistWithPointUncompressedImports()
        {
            var names = ForestRestartAssetBuilder.ExpectedSpriteNames().ToArray();
            Assert.That(names, Has.Length.EqualTo(76));
            foreach (var name in names)
            {
                var path = ForestRestartAssetBuilder.SpriteRoot + "/" + name + ".png";
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                Assert.That(sprite, Is.Not.Null, path);
                Assert.That(importer, Is.Not.Null, path);
                Assert.That(importer.spritePixelsPerUnit, Is.EqualTo(32f), path);
                Assert.That(importer.spriteImportMode, Is.EqualTo(SpriteImportMode.Single), path);
                Assert.That(importer.filterMode, Is.EqualTo(FilterMode.Point), path);
                Assert.That(importer.mipmapEnabled, Is.False, path);
                Assert.That(importer.textureCompression, Is.EqualTo(TextureImporterCompression.Uncompressed), path);
            }
        }

        [Test]
        public void GrassActualPixels_HaveMatchingEdgesAcrossAllVariants()
        {
            var variants = Enumerable.Range(0, 8).Select(i => ReadPixels($"grass_{i:00}", out _, out _)).ToArray();
            foreach (var first in variants) foreach (var second in variants)
                for (var offset = 0; offset < 32; offset++)
                {
                    Assert.That(first[offset * 32 + 31], Is.EqualTo(second[offset * 32]), $"水平接続 pixel={offset}");
                    Assert.That(first[31 * 32 + offset], Is.EqualTo(second[offset]), $"垂直接続 pixel={offset}");
                }
        }

        [Test]
        public void RoadActualPixels_KeepConnectionSocketsOpaqueAndFullInteriorSolid()
        {
            for (var mask = 0; mask < 16; mask++) for (var variant = 0; variant < 4; variant++)
            {
                var name = $"road_{mask:x2}_{variant:00}";
                var pixels = ReadPixels(name, out var width, out var height);
                Assert.That(width, Is.EqualTo(32)); Assert.That(height, Is.EqualTo(32));
                for (var offset = 6; offset < 26; offset++)
                {
                    if ((mask & 1) != 0) Assert.That(pixels[31 * 32 + offset].a, Is.EqualTo(255), name);
                    if ((mask & 2) != 0) Assert.That(pixels[offset * 32 + 31].a, Is.EqualTo(255), name);
                    if ((mask & 4) != 0) Assert.That(pixels[offset].a, Is.EqualTo(255), name);
                    if ((mask & 8) != 0) Assert.That(pixels[offset * 32].a, Is.EqualTo(255), name);
                }
                if (mask == 15) Assert.That(pixels.Select(pixel => pixel.a), Has.All.EqualTo((byte)255), name);
            }
        }

        [Test]
        public void ObjectsActualPixels_UseBinaryAlphaAndFootPivots()
        {
            foreach (var name in new[] { "oak", "grove", "undergrowth", "hero" })
            {
                var pixels = ReadPixels(name, out var width, out var height);
                Assert.That(pixels.All(pixel => pixel.a == 0 || pixel.a == 255), Is.True, name);
                Assert.That(pixels.Count(pixel => pixel.a == 255), Is.GreaterThan(width * height / 10), name);
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(ForestRestartAssetBuilder.SpriteRoot + "/" + name + ".png");
                Assert.That(sprite.pivot.x, Is.EqualTo(width * 0.5f).Within(0.001f), name);
                Assert.That(sprite.pivot.y, Is.EqualTo(height * ForestRestartAssetBuilder.GetFootPivot(name)).Within(0.001f), name);
            }
        }

        [Test]
        public void RoadActualPixels_ExposeIrregularEdgesWithoutChangingConnectionCollars()
        {
            var depths = new HashSet<int>();
            for (var variant = 0; variant < 4; variant++)
            {
                var pixels = ReadPixels($"road_05_{variant:00}", out _, out _);
                var variantDepths = new HashSet<int>();
                for (var y = 6; y < 26; y++)
                {
                    var firstOpaque = Enumerable.Range(0, 32).First(x => pixels[y * 32 + x].a == 255);
                    Assert.That(firstOpaque, Is.InRange(4, 8), $"variant={variant}, row={y}");
                    variantDepths.Add(firstOpaque); depths.Add(firstOpaque);
                }
                Assert.That(variantDepths.Count, Is.GreaterThan(1), $"variant={variant}の露出辺が直線です。");
                // 南北を接続する直線の境界は変形させず、隣のSpriteへ確実につながる。
                for (var x = 0; x < 32; x++)
                {
                    var expectedAlpha = x >= 6 && x < 26 ? 255 : 0;
                    Assert.That(pixels[x].a, Is.EqualTo(expectedAlpha));
                    Assert.That(pixels[31 * 32 + x].a, Is.EqualTo(expectedAlpha));
                }
            }
            Assert.That(depths.Max() - depths.Min(), Is.InRange(2, 4));
        }

        [Test]
        public void RoadActualPixels_FillEveryCornerBetweenTwoConnectedEdges()
        {
            for (var mask = 0; mask < 16; mask++) for (var variant = 0; variant < 4; variant++)
            {
                var name = $"road_{mask:x2}_{variant:00}";
                var pixels = ReadPixels(name, out _, out _);
                // N/E、E/S、S/W、W/Nの二辺が接続する角はCollarも内部も草穴を残さない。
                foreach (var corner in new[] { new Vector3Int(26, 26, 3), new Vector3Int(26, 0, 6),
                    new Vector3Int(0, 0, 12), new Vector3Int(0, 26, 9) })
                {
                    if ((mask & corner.z) != corner.z) continue;
                    for (var y = corner.y; y < corner.y + 6; y++)
                        for (var x = corner.x; x < corner.x + 6; x++)
                            Assert.That(pixels[y * 32 + x].a, Is.EqualTo(255), $"{name}: connected corner ({x},{y})");
                }
            }
        }

        [Test]
        public void RestartTileAssets_HaveEightGroundAndSixteenRoadRules()
        {
            var grass = AssetDatabase.LoadAssetAtPath<GroundVariationTile>(ForestRestartAssetBuilder.TileRoot + "/GrassVariation.asset");
            var road = AssetDatabase.LoadAssetAtPath<RoadConnectionRuleTile>(ForestRestartAssetBuilder.TileRoot + "/DirtRoad.asset");
            var collision = AssetDatabase.LoadAssetAtPath<Tile>(ForestRestartAssetBuilder.TileRoot + "/Collision.asset");
            Assert.That(grass, Is.Not.Null); Assert.That(road, Is.Not.Null); Assert.That(collision, Is.Not.Null);
            Assert.That(grass.VariantCount, Is.EqualTo(8));
            Assert.That(road.m_TilingRules, Has.Count.EqualTo(16));
            foreach (var rule in road.m_TilingRules)
            {
                Assert.That(rule.m_Sprites, Has.Length.EqualTo(4));
                Assert.That(rule.m_NeighborPositions, Has.Count.EqualTo(4));
                Assert.That(rule.m_RuleTransform, Is.EqualTo(RuleTile.TilingRuleOutput.Transform.Fixed));
                Assert.That(rule.m_RandomTransform, Is.EqualTo(RuleTile.TilingRuleOutput.Transform.Fixed));
            }
            Assert.That(collision.sprite, Is.Null); Assert.That(collision.colliderType, Is.EqualTo(Tile.ColliderType.Grid));
        }

        [Test]
        public void RestartPrefabCollisions_AreSeparatedFromLargerPlacementFootprints()
        {
            foreach (var name in new[] { "oak", "grove", "undergrowth" })
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ForestRestartAssetBuilder.PrefabRoot + "/" + name + ".prefab");
                Assert.That(prefab, Is.Not.Null, name);
                var marker = prefab.GetComponent<MapObstacleVisualMarker>();
                Assert.That(marker, Is.Not.Null, name);
                Assert.That(marker.CollisionMode, Is.EqualTo(MapObstacleCollisionMode.CollisionBody), name);
                Assert.That(prefab.GetComponent<Collider2D>(), Is.Null, name);
                var body = prefab.transform.Find(MapObstacleVisualMarker.CollisionBodyName);
                Assert.That(body, Is.Not.Null, name);
                Assert.That(body.gameObject.layer, Is.EqualTo(LayerMask.NameToLayer("MapCollision")), name);
                var collider = body.GetComponent<BoxCollider2D>();
                Assert.That(collider, Is.Not.Null, name);
                Assert.That(collider.size, Is.EqualTo(marker.CollisionSize), name);
                Assert.That(body.GetComponent<Renderer>(), Is.Null, name);
                Assert.That(marker.CollisionSize.x * marker.CollisionSize.y,
                    Is.LessThan(marker.FootprintSize.x * marker.FootprintSize.y), name);
            }
        }

        [Test]
        public void ReferenceScene_SatisfiesCurrentHierarchyAndAssetContract()
        {
            var wasLoaded = TryGetLoadedScene(ReferenceScenePath, out var scene);
            if (!wasLoaded) scene = EditorSceneManager.OpenScene(ReferenceScenePath, OpenSceneMode.Additive);
            try
            {
                var report = MapAuthoringValidator.ValidateScene(scene, true);
                Assert.That(report.HasErrors, Is.False, string.Join("; ", report.Issues.Select(issue => issue.Code + ": " + issue.Message)));
                var cameras = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Camera>(true)).ToArray();
                Assert.That(cameras, Has.Length.EqualTo(1));
            }
            finally { if (!wasLoaded) EditorSceneManager.CloseScene(scene, true); }
        }

        private static Color32[] ReadPixels(string name, out int width, out int height)
        {
            var path = ForestRestartAssetBuilder.SpriteRoot + "/" + name + ".png";
            var image = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                Assert.That(image.LoadImage(File.ReadAllBytes(path)), Is.True, path);
                width = image.width; height = image.height; return image.GetPixels32();
            }
            finally { UnityEngine.Object.DestroyImmediate(image); }
        }

        [Test]
        public void EventCatalog_ContainsHealingFountainDefinition()
        {
            const string definitionPath = "Assets/Data/MapAuthoring/Events/HealingFountainBasic.asset";
            const string catalogPath = "Assets/Data/MapAuthoring/Events/MapEventCatalog.asset";
            var definition = AssetDatabase.LoadAssetAtPath<HealingFountainEventDefinition>(definitionPath);
            var catalog = AssetDatabase.LoadAssetAtPath<MapEventCatalog>(catalogPath);

            Assert.That(definition, Is.Not.Null, definitionPath);
            Assert.That(catalog, Is.Not.Null, catalogPath);
            Assert.That(definition.EventDefinitionId, Is.EqualTo("healing-fountain-basic"));
            Assert.That(definition.OneShot, Is.True);
            Assert.That(definition.DefaultInteractionRadius, Is.EqualTo(1.25f));
            Assert.That(definition.HealAmount, Is.EqualTo(30));
            Assert.That(catalog.Validate(out var validationError), Is.True, validationError);
            Assert.That(catalog.TryGet("healing-fountain-basic", out var resolved), Is.True);
            Assert.That(resolved, Is.SameAs(definition));
        }

        [Test]
        public void ReferenceScene_EventSocketsArePoolCandidatesByPlacementId()
        {
            var wasLoaded = TryGetLoadedScene(ReferenceScenePath, out var scene);
            if (!wasLoaded)
            {
                scene = EditorSceneManager.OpenScene(ReferenceScenePath, OpenSceneMode.Additive);
            }

            try
            {
                var mapRoot = FindRoot(scene, "MapRoot");
                var markers = mapRoot.GetComponentsInChildren<MapSocketMarker>(true);
                var eventCount = 0;
                var socketIds = new HashSet<string>(StringComparer.Ordinal);
                for (var index = 0; index < markers.Length; index++)
                {
                    var marker = markers[index];
                    if (marker.SocketKind != MapSocketKind.Event)
                    {
                        continue;
                    }

                    eventCount++;
                    Assert.That(socketIds.Add(marker.SocketId), Is.True, marker.SocketId);
                    Assert.That(
                        marker.EventPlacementMode,
                        Is.EqualTo(MapEventPlacementMode.PoolCandidate),
                        marker.SocketId);
                    Assert.That(marker.EventDefinition, Is.Null, marker.SocketId);
                    Assert.That(marker.EventDefinitionId, Is.Empty, marker.SocketId);
                    Assert.That(marker.InteractionRadiusOverride, Is.GreaterThan(0f), marker.SocketId);
                    Assert.That(marker.InteractionRadius, Is.GreaterThan(0f), marker.SocketId);
                }

                Assert.That(eventCount, Is.EqualTo(6));
            }
            finally
            {
                if (!wasLoaded && scene.IsValid() && scene.isLoaded)
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
        }

        [Test]
        public void RoadVariationHash_IsDeterministicAndAvoidsShortPeriodRuns()
        {
            var tile = ScriptableObject.CreateInstance<RoadConnectionRuleTile>();
            try
            {
                tile.Configure("road", 0);
                for (var mask = 0; mask < 16; mask++)
                {
                    var counts = new int[4];
                    var previous = -1;
                    var currentRun = 0;
                    var maximumRun = 0;
                    for (var blockStart = -32; blockStart < 32; blockStart += 4)
                    {
                        var blockVariants = new HashSet<int>();
                        for (var x = blockStart; x < blockStart + 4; x++)
                        {
                            var position = new Vector3Int(x, mask * 3, 0);
                            var index = tile.GetVariantIndex(position, 4, mask);
                            Assert.That(
                                tile.GetVariantIndex(new Vector3Int(x, mask * 3, 9), 4, mask),
                                Is.EqualTo(index),
                                $"Z座標で差分が変わりました: mask={mask:x2}, x={x}");
                            counts[index]++;
                            blockVariants.Add(index);
                            currentRun = index == previous ? currentRun + 1 : 1;
                            maximumRun = Math.Max(maximumRun, currentRun);
                            previous = index;
                        }

                        Assert.That(
                            blockVariants.Count,
                            Is.EqualTo(4),
                            $"4セル区間で差分が重複しました: mask={mask:x2}, block={blockStart}");
                    }

                    Assert.That(counts, Has.All.EqualTo(16), $"mask={mask:x2}");
                    Assert.That(maximumRun, Is.LessThanOrEqualTo(2), $"mask={mask:x2}");
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(tile);
            }
        }

        [Test]
        public void Renderer2D_UsesCustomWorldYAxisSorting()
        {
            var rendererData = AssetDatabase.LoadMainAssetAtPath(Renderer2DDataPath);
            Assert.That(rendererData, Is.Not.Null, Renderer2DDataPath);

            var serializedRendererData = new SerializedObject(rendererData);
            var sortMode = serializedRendererData.FindProperty("m_TransparencySortMode");
            var sortAxis = serializedRendererData.FindProperty("m_TransparencySortAxis");
            Assert.That(sortMode, Is.Not.Null, Renderer2DDataPath);
            Assert.That(sortAxis, Is.Not.Null, Renderer2DDataPath);
            Assert.That(
                sortMode.intValue,
                Is.EqualTo((int)TransparencySortMode.CustomAxis),
                Renderer2DDataPath);
            Assert.That(sortAxis.vector3Value, Is.EqualTo(Vector3.up), Renderer2DDataPath);
        }

        [Test]
        public void ObstacleVisualMarker_ClampsSizeAndEnumeratesFootprintCells()
        {
            var markerObject = new GameObject("MarkerTest");
            try
            {
                var marker = markerObject.AddComponent<MapObstacleVisualMarker>();
                marker.Configure(
                    new Vector2Int(0, 2),
                    new Vector2Int(-1, 1),
                    MapObstacleCollisionMode.CollisionBody,
                    MapObstacleCollisionShape.Capsule,
                    new Vector2(0.5f, 0.25f),
                    new Vector2(0f, 0.25f));

                Assert.That(marker.FootprintSize, Is.EqualTo(new Vector2Int(1, 2)));
                Assert.That(marker.FootprintOffset, Is.EqualTo(new Vector2Int(-1, 1)));
                Assert.That(marker.CollisionMode, Is.EqualTo(MapObstacleCollisionMode.CollisionBody));
                Assert.That(marker.CollisionShape, Is.EqualTo(MapObstacleCollisionShape.Capsule));
                Assert.That(marker.CollisionSize, Is.EqualTo(new Vector2(0.5f, 0.25f)));
                Assert.That(marker.CollisionOffset, Is.EqualTo(new Vector2(0f, 0.25f)));
                Assert.That(
                    new List<Vector3Int>(marker.GetFootprintCells(new Vector3Int(4, 5, 0))),
                    Is.EqualTo(new[]
                    {
                        new Vector3Int(3, 6, 0),
                        new Vector3Int(3, 7, 0)
                    }));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(markerObject);
            }
        }

        [Test]
        public void RoadPathRasterizer_IncludesBothSegmentEndpoints()
        {
            var start = new Vector3Int(3, -2, 0);
            var end = new Vector3Int(-1, -2, 0);

            var cells = MapRoadPathRasterizer.RasterizePath(new[] { start, end });

            Assert.That(cells, Does.Contain(start));
            Assert.That(cells, Does.Contain(end));
            Assert.That(cells, Has.Count.EqualTo(5));
        }

        [Test]
        public void RoadPathRasterizer_AppliesSquareBrushWidthAlongWholeSegment()
        {
            var cells = MapRoadPathRasterizer.RasterizePath(
                new[]
                {
                    new Vector3Int(0, 0, 0),
                    new Vector3Int(0, 2, 0)
                },
                squareBrushRadius: 1);

            Assert.That(cells, Has.Count.EqualTo(15));
            for (var y = -1; y <= 3; y++)
            {
                for (var x = -1; x <= 1; x++)
                {
                    Assert.That(cells, Does.Contain(new Vector3Int(x, y, 0)));
                }
            }
        }

        [Test]
        public void RoadPathRasterizer_FillsThickCornerWithoutDiagonalHole()
        {
            var cells = MapRoadPathRasterizer.RasterizePath(
                new[]
                {
                    new Vector3Int(0, 0, 0),
                    new Vector3Int(2, 0, 0),
                    new Vector3Int(2, 2, 0)
                },
                squareBrushRadius: 1);

            Assert.That(cells, Does.Contain(new Vector3Int(1, 1, 0)));
            Assert.That(cells, Does.Contain(new Vector3Int(3, 1, 0)));
            Assert.That(MapRoadPathRasterizer.IsSingleCardinalComponent(cells), Is.True);
        }

        [Test]
        public void RoadPathRasterizer_MergesBranchPathsIntoOneComponent()
        {
            var cells = MapRoadPathRasterizer.RasterizePaths(
                new IEnumerable<Vector3Int>[]
                {
                    new[]
                    {
                        new Vector3Int(-2, 0, 0),
                        new Vector3Int(2, 0, 0)
                    },
                    new[]
                    {
                        new Vector3Int(0, 0, 0),
                        new Vector3Int(0, 2, 0)
                    }
                });

            Assert.That(cells, Has.Count.EqualTo(7));
            Assert.That(cells, Does.Contain(new Vector3Int(0, 0, 0)));
            Assert.That(cells, Does.Contain(new Vector3Int(0, 2, 0)));
            Assert.That(MapRoadPathRasterizer.IsSingleCardinalComponent(cells), Is.True);
        }

        [Test]
        public void RoadPathRasterizer_RejectsDiagonalSegment()
        {
            Assert.Throws<ArgumentException>(() => MapRoadPathRasterizer.RasterizePath(
                new[]
                {
                    new Vector3Int(0, 0, 0),
                    new Vector3Int(1, 1, 0)
                }));
        }

        [Test]
        public void RoadPathRasterizer_DetectsCardinalConnectivity()
        {
            var connected = new[]
            {
                new Vector3Int(0, 0, 0),
                new Vector3Int(1, 0, 0),
                new Vector3Int(1, 1, 0)
            };
            var disconnected = new[]
            {
                new Vector3Int(0, 0, 0),
                new Vector3Int(1, 1, 0)
            };

            Assert.That(MapRoadPathRasterizer.IsSingleCardinalComponent(connected), Is.True);
            Assert.That(MapRoadPathRasterizer.IsSingleCardinalComponent(disconnected), Is.False);
            Assert.That(MapRoadPathRasterizer.IsSingleCardinalComponent(Array.Empty<Vector3Int>()), Is.False);
        }

        private static bool TryGetLoadedScene(string path, out Scene scene)
        {
            scene = SceneManager.GetSceneByPath(path); return scene.IsValid() && scene.isLoaded;
        }

        private static GameObject FindRoot(Scene scene, string name)
        {
            var root = scene.GetRootGameObjects().FirstOrDefault(candidate => candidate.name == name);
            Assert.That(root, Is.Not.Null, name); return root;
        }
    }
}
