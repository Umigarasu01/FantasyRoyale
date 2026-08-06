using System;
using System.Collections.Generic;
using System.IO;
using FantasyRoyale.MapAuthoringKit;
using FantasyRoyale.MapAuthoringKit.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

namespace FantasyRoyale.MapAuthoringKit.Tests.Editor
{
    /// <summary>
    /// Map Authoring Kit v4.3の単一Atlas、地面・道端差分、自由配置表示、道路連結、判定分離、Sample配置を確認する。
    /// </summary>
    public sealed class MapAuthoringAssetContractTests
    {
        private const string ProductionRoot = "Assets/Art/Map/Production/Forest";
        private const string ProductionAtlasPath =
            "Assets/Art/Map/Production/Forest/forest-gba-production-atlas.png";
        private const string ProductionCatalogPath =
            "Assets/Art/Map/Production/Forest/forest-gba-production-atlas.json";
        private const string TileRoot = "Assets/Data/MapAuthoring/Tiles/Forest";
        private const string RuleRoot = "Assets/Data/MapAuthoring/Tiles/Forest/Rules";
        private const string PaletteRoot = "Assets/Data/MapAuthoring/Palettes/Forest";
        private const string BrushRoot = "Assets/Data/MapAuthoring/Brushes";
        private const string PrefabRoot = "Assets/Prefabs/MapAuthoring/Forest";
        private const string Renderer2DDataPath = "Assets/Settings/Renderer2D.asset";
        private const string SampleScenePath = "Assets/Scenes/MapAuthoring/GbaForestMapSample.unity";
        private const string ReferenceScenePath =
            "Assets/Scenes/MapAuthoring/GbaForestBattleRoyaleReference.unity";
        private const int RoadSocketStart = 6;
        private const int RoadSocketEnd = 26;
        private const int RoadSocketCollarDepth = 2;
        private const int NorthMask = 1;
        private const int EastMask = 2;
        private const int SouthMask = 4;
        private const int WestMask = 8;

        private static readonly Vector3Int[] CardinalDirections =
        {
            Vector3Int.up,
            Vector3Int.right,
            Vector3Int.down,
            Vector3Int.left
        };

        /// <summary>
        /// v4.3 Catalogが正しいFamily内訳を持ち、Unityへ205枚のSub-SpriteとしてImportされているかを調べる。
        /// </summary>
        [Test]
        public void ProductionAtlas_DeclaresV43FamiliesAndCompleteSpriteSet()
        {
            Assert.That(File.Exists(ProductionAtlasPath), Is.True, ProductionAtlasPath);
            Assert.That(File.Exists(ProductionCatalogPath), Is.True, ProductionCatalogPath);
            Assert.That(
                GbaMapSpritePostprocessor.TryLoadCatalog(out var catalog, out var catalogError),
                Is.True,
                catalogError);

            Assert.That(catalog.schema, Is.EqualTo("fantasyroyale.map-authoring-atlas.v4.3"));
            Assert.That(catalog.atlasPath.Replace('\\', '/'), Is.EqualTo(ProductionAtlasPath));
            Assert.That(catalog.atlasWidth, Is.EqualTo(1024));
            Assert.That(catalog.atlasHeight, Is.EqualTo(1024));
            Assert.That(catalog.tileSize, Is.EqualTo(32));
            Assert.That(catalog.sprites, Has.Length.EqualTo(205));

            var expectedFamilyCounts = new Dictionary<string, int>(StringComparer.Ordinal)
            {
                { "grass", 8 },
                { "dirt", 64 },
                { "stone", 64 },
                { "water", 50 },
                { "detail", 4 },
                { "props", 10 },
                { "obstacle_visuals", 5 }
            };
            var actualFamilyCounts = new Dictionary<string, int>(StringComparer.Ordinal);
            var catalogNames = new HashSet<string>(StringComparer.Ordinal);
            for (var index = 0; index < catalog.sprites.Length; index++)
            {
                var entry = catalog.sprites[index];
                Assert.That(catalogNames.Add(entry.name), Is.True, $"Catalog Sprite名が重複しています: {entry.name}");
                actualFamilyCounts.TryGetValue(entry.family, out var currentCount);
                actualFamilyCounts[entry.family] = currentCount + 1;
            }

            Assert.That(actualFamilyCounts, Has.Count.EqualTo(expectedFamilyCounts.Count));
            foreach (var expected in expectedFamilyCounts)
            {
                Assert.That(actualFamilyCounts.ContainsKey(expected.Key), Is.True, $"Familyがありません: {expected.Key}");
                Assert.That(actualFamilyCounts[expected.Key], Is.EqualTo(expected.Value), expected.Key);
            }

            var importedSprites = LoadAtlasSpritesByName();
            Assert.That(importedSprites, Has.Count.EqualTo(205));
            foreach (var spriteName in catalogNames)
            {
                Assert.That(importedSprites.ContainsKey(spriteName), Is.True, $"Sub-Spriteがありません: {spriteName}");
            }

            // Production配下を単一Textureへ固定し、旧loose PNGが再混入する回帰を検出する。
            var productionTextureGuids = AssetDatabase.FindAssets("t:Texture2D", new[] { ProductionRoot });
            Assert.That(productionTextureGuids, Has.Length.EqualTo(1));
            Assert.That(
                AssetDatabase.GUIDToAssetPath(productionTextureGuids[0]).Replace('\\', '/'),
                Is.EqualTo(ProductionAtlasPath));
        }

        /// <summary>
        /// 初期の泉定義AssetがCatalogへ登録され、List保存からID検索できる状態を確認する。
        /// </summary>
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

        /// <summary>
        /// Reference Mapの6配置が配置固有Socket IDを保ちつつ、同じ泉定義Assetを共有することを確認する。
        /// </summary>
        [Test]
        public void ReferenceScene_EventSocketsShareHealingDefinitionByPlacementId()
        {
            var definition = AssetDatabase.LoadAssetAtPath<HealingFountainEventDefinition>(
                "Assets/Data/MapAuthoring/Events/HealingFountainBasic.asset");
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
                    Assert.That(marker.EventDefinition, Is.SameAs(definition), marker.SocketId);
                    Assert.That(marker.EventDefinitionId, Is.EqualTo("healing-fountain-basic"), marker.SocketId);
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

        /// <summary>
        /// 道2種は全Mask、水面はmask ffだけ4差分を使い、接続と表示差分の責務が混ざっていないかを調べる。
        /// </summary>
        [Test]
        public void RuleFamilies_HaveExpectedTopologiesAndInteriorVariants()
        {
            var dirt = RequireRoadRuleTile($"{RuleRoot}/DirtRuleTile.asset");
            var stone = RequireRoadRuleTile($"{RuleRoot}/StoneRuleTile.asset");
            AssertRuleFamily(dirt, "dirt", 16, false, true, -1);
            AssertRuleFamily(stone, "stone", 16, false, true, -1);
            AssertRuleFamily(
                RequireRuleTile($"{RuleRoot}/WaterRuleTile.asset"),
                "water",
                47,
                true,
                false,
                0xff);
            Assert.That(dirt.ConnectionGroup, Is.EqualTo("road"));
            Assert.That(stone.ConnectionGroup, Is.EqualTo("road"));
            Assert.That(dirt.CanConnectTo(stone), Is.True, "DirtからStoneを同一道路として接続できません。");
            Assert.That(stone.CanConnectTo(dirt), Is.True, "StoneからDirtを同一道路として接続できません。");
            Assert.That(AssetDatabase.LoadMainAssetAtPath($"{RuleRoot}/ForestWallRuleTile.asset"), Is.Null);
            Assert.That(AssetDatabase.LoadMainAssetAtPath($"{RuleRoot}/CliffRuleTile.asset"), Is.Null);
        }

        /// <summary>
        /// 道路差分が負座標を含む各4セル区間で全4種を一度ずつ使い、同じ輪郭を3セル連続させないことを確認する。
        /// </summary>
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

        /// <summary>
        /// Dirt / StoneのAtlas実Pixelが中央20px Socketだけを直線接続し、Cell境界へ規則的な柱を作らないことを確認する。
        /// </summary>
        [Test]
        public void RoadAtlasSprites_UseTwentyPixelTopologyBoundedSockets()
        {
            Assert.That(
                GbaMapSpritePostprocessor.TryLoadCatalog(out var catalog, out var catalogError),
                Is.True,
                catalogError);

            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                Assert.That(
                    ImageConversion.LoadImage(texture, File.ReadAllBytes(ProductionAtlasPath), false),
                    Is.True,
                    ProductionAtlasPath);
                Assert.That(texture.width, Is.EqualTo(catalog.atlasWidth));
                Assert.That(texture.height, Is.EqualTo(catalog.atlasHeight));

                var pixels = texture.GetPixels32();
                var roadSpriteCount = 0;
                foreach (var entry in catalog.sprites)
                {
                    if (entry.family != "dirt" && entry.family != "stone")
                    {
                        continue;
                    }

                    roadSpriteCount++;
                    Assert.That(entry.width, Is.EqualTo(32), entry.name);
                    Assert.That(entry.height, Is.EqualTo(32), entry.name);
                    foreach (var direction in new[] { NorthMask, EastMask, SouthMask, WestMask })
                    {
                        for (var depth = 0; depth < RoadSocketCollarDepth; depth++)
                        {
                            for (var coordinate = 0; coordinate < 32; coordinate++)
                            {
                                var actualOpaque = GetRoadAtlasEdgeAlpha(
                                    pixels,
                                    texture.width,
                                    entry,
                                    direction,
                                    depth,
                                    coordinate) > 0;
                                var expectedOpaque = ExpectedRoadSocketOpaque(
                                    entry.mask,
                                    direction,
                                    coordinate);
                                if (actualOpaque != expectedOpaque)
                                {
                                    Assert.Fail(
                                        $"道路Socket Alphaが契約と異なります: sprite={entry.name}, "
                                        + $"mask={entry.mask:x2}, direction={direction}, depth={depth}, "
                                        + $"coordinate={coordinate}, expected={expectedOpaque}, actual={actualOpaque}");
                                }
                            }
                        }
                    }
                }

                Assert.That(roadSpriteCount, Is.EqualTo(128));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        /// <summary>
        /// 地面差分を1つのGroundVariationTileへ集約し、旧Grass Tileへ退行していないかを確認する。
        /// </summary>
        [Test]
        public void GroundVariationTile_UsesEightAtlasSpritesAndReplacesLegacyGrassTiles()
        {
            var variationPath = $"{TileRoot}/Ground/GrassVariation.asset";
            var variationTile = RequireGroundVariationTile(variationPath);

            Assert.That(variationTile.VariantCount, Is.EqualTo(8), variationPath);
            Assert.That(variationTile.Variants, Has.Count.EqualTo(8), variationPath);
            for (var index = 0; index < variationTile.Variants.Count; index++)
            {
                var sprite = variationTile.Variants[index];
                Assert.That(sprite.name, Is.EqualTo($"ground_grass_{index + 1:00}"), variationPath);
                AssertSpriteUsesProductionAtlas(sprite, $"{variationPath}: variant {index}");
            }

            for (var index = 1; index <= 4; index++)
            {
                var legacyPath = $"{TileRoot}/Ground/Grass{index:00}.asset";
                Assert.That(AssetDatabase.LoadMainAssetAtPath(legacyPath), Is.Null, legacyPath);
            }
        }

        /// <summary>
        /// 通常Tile、全RuleTile、全表示Prefabが単一Atlasだけを参照し、Collision Tileは不可視かを調べる。
        /// </summary>
        [Test]
        public void GeneratedVisualAssets_ReferenceOnlyProductionAtlasAndCollisionIsInvisible()
        {
            var collisionPath = $"{TileRoot}/Utility/CollisionTile.asset";
            var groundVariationPath = $"{TileRoot}/Ground/GrassVariation.asset";
            var collisionTile = AssetDatabase.LoadAssetAtPath<Tile>(collisionPath);
            Assert.That(collisionTile, Is.Not.Null, collisionPath);
            Assert.That(collisionTile.sprite, Is.Null, "Collision Tileは表示Spriteを持たない契約です。");

            var tileGuids = AssetDatabase.FindAssets("t:Tile", new[] { TileRoot });
            var visibleTileCount = 0;
            for (var index = 0; index < tileGuids.Length; index++)
            {
                var path = AssetDatabase.GUIDToAssetPath(tileGuids[index]);
                var tile = AssetDatabase.LoadAssetAtPath<Tile>(path);
                var normalizedPath = path.Replace('\\', '/');
                if (tile == null
                    || string.Equals(normalizedPath, collisionPath, StringComparison.Ordinal)
                    || string.Equals(normalizedPath, groundVariationPath, StringComparison.Ordinal))
                {
                    continue;
                }

                AssertSpriteUsesProductionAtlas(tile.sprite, path);
                visibleTileCount++;
            }

            Assert.That(visibleTileCount, Is.EqualTo(1));
            Assert.That(
                AssetDatabase.IsValidFolder($"{TileRoot}/Detail"),
                Is.False,
                "DetailはTileではなく自由配置Prefabとして扱います。");
            Assert.That(
                AssetDatabase.LoadMainAssetAtPath($"{PaletteRoot}/ForestDetailPalette.prefab"),
                Is.Null,
                "Detail Paletteはv4.3へ持ち込みません。");
            var paletteGuids = AssetDatabase.FindAssets("t:Prefab", new[] { PaletteRoot });
            Assert.That(paletteGuids, Has.Length.EqualTo(2), "PaletteはGroundとCollisionの2点だけです。");

            var ruleGuids = AssetDatabase.FindAssets("t:RuleTile", new[] { RuleRoot });
            Assert.That(ruleGuids, Has.Length.EqualTo(3));
            for (var index = 0; index < ruleGuids.Length; index++)
            {
                var path = AssetDatabase.GUIDToAssetPath(ruleGuids[index]);
                var ruleTile = AssetDatabase.LoadAssetAtPath<RuleTile>(path);
                Assert.That(ruleTile, Is.Not.Null, path);
                AssertSpriteUsesProductionAtlas(ruleTile.m_DefaultSprite, $"{path}: default");
                for (var ruleIndex = 0; ruleIndex < ruleTile.m_TilingRules.Count; ruleIndex++)
                {
                    var rule = ruleTile.m_TilingRules[ruleIndex];
                    Assert.That(rule.m_Sprites, Is.Not.Empty, $"{path}: rule {ruleIndex}");
                    for (var spriteIndex = 0; spriteIndex < rule.m_Sprites.Length; spriteIndex++)
                    {
                        AssertSpriteUsesProductionAtlas(
                            rule.m_Sprites[spriteIndex],
                            $"{path}: rule {ruleIndex}/sprite {spriteIndex}");
                    }
                }
            }

            var prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { PrefabRoot });
            Assert.That(prefabGuids, Has.Length.EqualTo(19));
            var obstacleMarkerCount = 0;
            var collisionBodyCount = 0;
            var tilemapFootprintCount = 0;
            var mapCollisionLayer = LayerMask.NameToLayer("MapCollision");
            Assert.That(mapCollisionLayer, Is.GreaterThanOrEqualTo(0));
            for (var index = 0; index < prefabGuids.Length; index++)
            {
                var path = AssetDatabase.GUIDToAssetPath(prefabGuids[index]);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                Assert.That(prefab, Is.Not.Null, path);
                Assert.That(prefab.layer, Is.EqualTo(0), path);
                var transforms = prefab.GetComponentsInChildren<Transform>(true);
                for (var transformIndex = 0; transformIndex < transforms.Length; transformIndex++)
                {
                    var current = transforms[transformIndex];
                    var expectedLayer = current != prefab.transform
                                        && current.name == MapObstacleVisualMarker.CollisionBodyName
                        ? mapCollisionLayer
                        : 0;
                    Assert.That(current.gameObject.layer, Is.EqualTo(expectedLayer), $"{path}/{current.name}");
                }

                var renderers = prefab.GetComponentsInChildren<SpriteRenderer>(true);
                Assert.That(renderers, Is.Not.Empty, path);
                for (var rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
                {
                    AssertSpriteUsesProductionAtlas(renderers[rendererIndex].sprite, path);
                }

                Assert.That(prefab.GetComponentsInChildren<Rigidbody2D>(true), Is.Empty, path);
                var marker = prefab.GetComponent<MapObstacleVisualMarker>();
                if (marker == null)
                {
                    Assert.That(prefab.GetComponentsInChildren<Collider2D>(true), Is.Empty, path);
                    continue;
                }

                obstacleMarkerCount++;
                if (marker.CollisionMode == MapObstacleCollisionMode.CollisionBody)
                {
                    collisionBodyCount++;
                    AssertPreciseCollisionBody(prefab, marker, prefab.name);
                }
                else
                {
                    tilemapFootprintCount++;
                    Assert.That(prefab.GetComponentsInChildren<Collider2D>(true), Is.Empty, path);
                }
            }

            Assert.That(obstacleMarkerCount, Is.EqualTo(12));
            Assert.That(collisionBodyCount, Is.EqualTo(10));
            Assert.That(tilemapFootprintCount, Is.EqualTo(2));
        }

        /// <summary>
        /// URP 2D RendererがWorld Y軸を実効ソート設定として使うことを検査する。
        /// </summary>
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

        /// <summary>
        /// 立体表示を共通WorldObjects層へ揃え、花などの地面装飾だけを背面へ残す契約を検査する。
        /// </summary>
        [Test]
        public void VisualPrefabs_UseFootPivotAndSharedWorldYSorting()
        {
            var groundDetails = new HashSet<string>(StringComparer.Ordinal)
            {
                "FlowerPatch",
                "Wildflowers"
            };
            var prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { PrefabRoot });
            Assert.That(prefabGuids, Has.Length.EqualTo(19));

            for (var index = 0; index < prefabGuids.Length; index++)
            {
                var prefabPath = AssetDatabase.GUIDToAssetPath(prefabGuids[index]);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                var renderer = prefab != null ? prefab.GetComponent<SpriteRenderer>() : null;
                Assert.That(renderer, Is.Not.Null, prefabPath);

                var expectedLayer = groundDetails.Contains(prefab.name)
                    ? "MapDetail"
                    : "WorldObjects";
                Assert.That(renderer.sortingLayerName, Is.EqualTo(expectedLayer), prefabPath);
                Assert.That(renderer.sortingOrder, Is.EqualTo(0), prefabPath);
                Assert.That(renderer.spriteSortPoint, Is.EqualTo(SpriteSortPoint.Pivot), prefabPath);
            }
        }

        /// <summary>
        /// 4種の通行可能装飾がTileではなく、物理責務を持たない独立Prefabとして生成されるかを調べる。
        /// </summary>
        [Test]
        public void DecorationPrefabs_AreColliderFreeVisualsWithoutObstacleMarkers()
        {
            var expectedDecorations = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                { "TallGrass", "detail_tall_grass" },
                { "FlowerPatch", "detail_flower_patch" },
                { "Wildflowers", "detail_wildflowers" },
                { "Reeds", "detail_reeds" }
            };

            foreach (var expected in expectedDecorations)
            {
                var prefabPath = $"{PrefabRoot}/{expected.Key}.prefab";
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                Assert.That(prefab, Is.Not.Null, prefabPath);
                Assert.That(prefab.layer, Is.EqualTo(0), prefabPath);
                Assert.That(prefab.GetComponent<Grid>(), Is.Null, prefabPath);
                Assert.That(prefab.GetComponent<Tilemap>(), Is.Null, prefabPath);
                Assert.That(prefab.GetComponent<MapObstacleVisualMarker>(), Is.Null, prefabPath);
                Assert.That(prefab.GetComponentsInChildren<Collider2D>(true), Is.Empty, prefabPath);
                Assert.That(prefab.GetComponentsInChildren<Rigidbody2D>(true), Is.Empty, prefabPath);

                var renderer = prefab.GetComponent<SpriteRenderer>();
                Assert.That(renderer, Is.Not.Null, prefabPath);
                Assert.That(renderer.sprite.name, Is.EqualTo(expected.Value), prefabPath);
                AssertSpriteUsesProductionAtlas(renderer.sprite, prefabPath);
            }
        }

        /// <summary>
        /// 単木3差分が別Prefabを持ち、樹冠ではなく幹へ寄せた専用判定を持つか調べる。
        /// </summary>
        [Test]
        public void TreeVariants_HaveDistinctTrunkCollisionPrefabs()
        {
            var prefabGuids = new HashSet<string>();
            for (var variant = 1; variant <= 3; variant++)
            {
                var suffix = variant.ToString("00");
                var prefabPath = $"{PrefabRoot}/TreeMedium{suffix}.prefab";
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);

                Assert.That(prefab, Is.Not.Null, prefabPath);
                var marker = prefab.GetComponent<MapObstacleVisualMarker>();
                Assert.That(marker, Is.Not.Null, prefabPath);
                Assert.That(marker.CollisionMode, Is.EqualTo(MapObstacleCollisionMode.CollisionBody), prefabPath);
                AssertPreciseCollisionBody(prefab, marker, prefab.name);
                prefabGuids.Add(AssetDatabase.AssetPathToGUID(prefabPath));
            }

            Assert.That(prefabGuids, Has.Count.EqualTo(3));
            Assert.That(AssetDatabase.IsValidFolder(BrushRoot), Is.False, "v4ではGameObject Brushを使用しません。");
        }

        /// <summary>
        /// 表示Markerが無効Sizeを補正し、Anchorから決定的なCollision Cellを列挙するか確認する。
        /// </summary>
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

        /// <summary>
        /// 水平区間をRasterizeしたとき、進行方向に関係なく始点と終点を含むことを確認する。
        /// </summary>
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

        /// <summary>
        /// Radius 1の正方形Brushが中心線を3Cell幅へ膨張し、両端にも同じ幅を適用することを確認する。
        /// </summary>
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

        /// <summary>
        /// 太いL字経路の内角がBrushの重なりで埋まり、斜めの欠落を残さないことを確認する。
        /// </summary>
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

        /// <summary>
        /// 複数Waypoint列が交点を共有すると、一つの分岐道路として重複なく統合されることを確認する。
        /// </summary>
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

        /// <summary>
        /// 斜めWaypointを暗黙補間せず、設計誤りとして明示的に拒否することを確認する。
        /// </summary>
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

        /// <summary>
        /// 4近傍で連結した集合だけを単一Componentとし、空集合と分断集合を拒否することを確認する。
        /// </summary>
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

        /// <summary>
        /// SampleのGround全600 Cellが同じ差分Tileを保存し、座標差分が目立つ周期や長い連続を作らないかを確認する。
        /// </summary>
        [Test]
        public void SampleGround_UsesSingleVariationTileAndAvoidsVisibleRepetition()
        {
            var wasLoaded = TryGetLoadedScene(SampleScenePath, out var scene);
            if (!wasLoaded)
            {
                scene = EditorSceneManager.OpenScene(SampleScenePath, OpenSceneMode.Additive);
            }

            try
            {
                var mapRoot = FindRoot(scene, "MapRoot");
                var groundTransform = mapRoot.transform.Find("Grid/GroundTilemap");
                Assert.That(groundTransform, Is.Not.Null);

                var ground = groundTransform.GetComponent<Tilemap>();
                var variationTile = RequireGroundVariationTile($"{TileRoot}/Ground/GrassVariation.asset");
                AssertGroundVariationDistribution(ground, variationTile);
            }
            finally
            {
                if (!wasLoaded && scene.IsValid() && scene.isLoaded)
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
        }

        /// <summary>
        /// Sample Sceneが自由配置表示と不可視Collisionを分離し、異素材を含む道路も一続きかを確認する。
        /// </summary>
        [Test]
        public void SampleScene_SeparatesFreeVisualsAndKeepsRoadConnected()
        {
            var wasLoaded = TryGetLoadedScene(SampleScenePath, out var scene);
            if (!wasLoaded)
            {
                scene = EditorSceneManager.OpenScene(SampleScenePath, OpenSceneMode.Additive);
            }

            try
            {
                var mapRoot = FindRoot(scene, "MapRoot");
                var grid = mapRoot.transform.Find("Grid");
                var decorations = mapRoot.transform.Find("Decorations");
                var obstacleVisuals = mapRoot.transform.Find("ObstacleVisuals");
                var props = mapRoot.transform.Find("Props");
                Assert.That(grid, Is.Not.Null);
                Assert.That(decorations, Is.Not.Null);
                Assert.That(obstacleVisuals, Is.Not.Null);
                Assert.That(props, Is.Not.Null);
                Assert.That(grid.GetComponentsInChildren<Tilemap>(true), Has.Length.EqualTo(4));
                Assert.That(grid.Find("GroundTilemap"), Is.Not.Null);
                Assert.That(grid.Find("TerrainTilemap"), Is.Not.Null);
                Assert.That(grid.Find("CollisionTilemap"), Is.Not.Null);
                Assert.That(grid.Find("ForegroundTilemap"), Is.Not.Null);
                Assert.That(grid.Find("DetailTilemap"), Is.Null);
                Assert.That(grid.Find("ObstacleTilemap"), Is.Null);
                Assert.That(decorations.GetComponent<Grid>(), Is.Null);
                Assert.That(decorations.GetComponent<Tilemap>(), Is.Null);
                Assert.That(decorations.GetComponent<TilemapRenderer>(), Is.Null);
                Assert.That(obstacleVisuals.GetComponent<Grid>(), Is.Null);
                Assert.That(props.GetComponent<Grid>(), Is.Null);
                Assert.That(decorations.GetComponentsInChildren<Collider2D>(true), Is.Empty);
                Assert.That(decorations.GetComponentsInChildren<Rigidbody2D>(true), Is.Empty);
                Assert.That(obstacleVisuals.GetComponentsInChildren<Rigidbody2D>(true), Is.Empty);
                Assert.That(props.GetComponentsInChildren<Collider2D>(true), Is.Empty);
                Assert.That(props.GetComponentsInChildren<Rigidbody2D>(true), Is.Empty);

                var decorationPrefabNames = new HashSet<string>(StringComparer.Ordinal);
                var hasDecorationPixelOffset = false;
                for (var childIndex = 0; childIndex < decorations.childCount; childIndex++)
                {
                    var decoration = decorations.GetChild(childIndex);
                    var source = PrefabUtility.GetCorrespondingObjectFromSource(decoration.gameObject);
                    Assert.That(source, Is.Not.Null, decoration.name);
                    decorationPrefabNames.Add(source.name);
                    Assert.That(decoration.GetComponent<MapObstacleVisualMarker>(), Is.Null, decoration.name);
                    AssertPixelSnappedTransform(decoration);
                    hasDecorationPixelOffset |= !IsCellAligned(decoration.position);
                }

                Assert.That(
                    decorationPrefabNames,
                    Is.SupersetOf(new[] { "TallGrass", "FlowerPatch", "Wildflowers", "Reeds" }));
                Assert.That(
                    hasDecorationPixelOffset,
                    Is.True,
                    "Decorationには1/32 unit単位の非Cell位置を最低一つ含めてください。");

                var firstTree = obstacleVisuals.Find("TreeWest01");
                var repeatedTree = obstacleVisuals.Find("TreeEast01");
                Assert.That(firstTree, Is.Not.Null);
                Assert.That(repeatedTree, Is.Not.Null);
                Assert.That(firstTree.gameObject, Is.Not.SameAs(repeatedTree.gameObject));
                Assert.That(
                    PrefabUtility.GetCorrespondingObjectFromSource(firstTree.gameObject),
                    Is.EqualTo(PrefabUtility.GetCorrespondingObjectFromSource(repeatedTree.gameObject)));

                var terrain = grid.Find("TerrainTilemap").GetComponent<Tilemap>();
                var collision = grid.Find("CollisionTilemap").GetComponent<Tilemap>();
                var dirtRuleTile = RequireRoadRuleTile($"{RuleRoot}/DirtRuleTile.asset");
                var stoneRuleTile = RequireRoadRuleTile($"{RuleRoot}/StoneRuleTile.asset");
                Assert.That(TilemapUsesTile(terrain, dirtRuleTile), Is.True);
                Assert.That(TilemapUsesTile(terrain, stoneRuleTile), Is.True);
                var waterRuleTile = RequireRuleTile($"{RuleRoot}/WaterRuleTile.asset");
                Assert.That(TilemapUsesTile(terrain, waterRuleTile), Is.True);
                AssertRoadCellsFormSingleMixedMaterialNetwork(terrain, dirtRuleTile, stoneRuleTile);
                var collisionTile = AssetDatabase.LoadAssetAtPath<Tile>($"{TileRoot}/Utility/CollisionTile.asset");
                Assert.That(TilemapUsesTile(collision, collisionTile), Is.True);
                AssertWaterCellsHaveCollision(terrain, waterRuleTile, collision, collisionTile);
                AssertObstacleCollisionSeparation(obstacleVisuals, collision, collisionTile);

                var forestWide = obstacleVisuals.Find("ForestWestWide");
                var cliffStraight = obstacleVisuals.Find("CliffSouthEastStraight");
                Assert.That(forestWide, Is.Not.Null);
                Assert.That(cliffStraight, Is.Not.Null);
                Assert.That(forestWide.GetComponent<SpriteRenderer>().sprite.rect.width, Is.GreaterThan(32f));
                Assert.That(cliffStraight.GetComponent<SpriteRenderer>().sprite.rect.width, Is.GreaterThan(32f));

                var hasPixelOffsetPlacement = false;
                for (var childIndex = 0; childIndex < obstacleVisuals.childCount; childIndex++)
                {
                    var obstacle = obstacleVisuals.GetChild(childIndex);
                    AssertPixelSnappedTransform(obstacle);
                    hasPixelOffsetPlacement |= !IsCellAligned(obstacle.position);
                }

                Assert.That(hasPixelOffsetPlacement, Is.True, "自由配置を示す1px単位の非Cell位置が必要です。");
            }
            finally
            {
                if (!wasLoaded && scene.IsValid() && scene.isLoaded)
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
        }

        /// <summary>
        /// RuleTileを必須Assetとして読み、欠落時は対象Pathを含むFailureにする。
        /// </summary>
        private static RuleTile RequireRuleTile(string path)
        {
            var tile = AssetDatabase.LoadAssetAtPath<RuleTile>(path);
            Assert.That(tile, Is.Not.Null, path);
            return tile;
        }

        /// <summary>
        /// 道路接続RuleTileを必須Assetとして読み、通常RuleTileへ退行した場合も型違反として失敗させる。
        /// </summary>
        private static RoadConnectionRuleTile RequireRoadRuleTile(string path)
        {
            var tile = AssetDatabase.LoadAssetAtPath<RoadConnectionRuleTile>(path);
            Assert.That(tile, Is.Not.Null, path);
            return tile;
        }

        /// <summary>
        /// Catalog座標の道路Spriteから、指定方向・深さ・辺座標にある実PixelのAlphaを返す。
        /// </summary>
        private static byte GetRoadAtlasEdgeAlpha(
            Color32[] pixels,
            int atlasWidth,
            GbaMapSpritePostprocessor.AtlasSpriteEntry entry,
            int direction,
            int depth,
            int coordinate)
        {
            int localX;
            int localY;
            switch (direction)
            {
                case NorthMask:
                    localX = coordinate;
                    localY = entry.height - 1 - depth;
                    break;
                case EastMask:
                    localX = entry.width - 1 - depth;
                    localY = entry.height - 1 - coordinate;
                    break;
                case SouthMask:
                    localX = coordinate;
                    localY = depth;
                    break;
                case WestMask:
                    localX = depth;
                    localY = entry.height - 1 - coordinate;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(direction));
            }

            return pixels[(entry.y + localY) * atlasWidth + entry.x + localX].a;
        }

        /// <summary>
        /// 中央20pxと、直交する二方向がともに接続する角だけを不透明とする期待値を返す。
        /// </summary>
        private static bool ExpectedRoadSocketOpaque(int mask, int direction, int coordinate)
        {
            if ((mask & direction) == 0)
            {
                return false;
            }

            if (coordinate >= RoadSocketStart && coordinate < RoadSocketEnd)
            {
                return true;
            }

            var lowCornerDirection = direction == NorthMask || direction == SouthMask
                ? WestMask
                : NorthMask;
            var highCornerDirection = direction == NorthMask || direction == SouthMask
                ? EastMask
                : SouthMask;
            return coordinate < RoadSocketStart
                ? (mask & lowCornerDirection) != 0
                : (mask & highCornerDirection) != 0;
        }

        /// <summary>
        /// 地面差分Tileを必須Assetとして読み、通常Tileへ退行した場合も型違反として失敗させる。
        /// </summary>
        private static GroundVariationTile RequireGroundVariationTile(string path)
        {
            var tile = AssetDatabase.LoadAssetAtPath<GroundVariationTile>(path);
            Assert.That(tile, Is.Not.Null, path);
            return tile;
        }

        /// <summary>
        /// Rule数、頻出Maskだけの4差分出力、固定変換、近傍Topology、Atlas参照をFamily単位で検査する。
        /// </summary>
        private static void AssertRuleFamily(
            RuleTile tile,
            string spritePrefix,
            int expectedRuleCount,
            bool expectsDiagonalRule,
            bool everyRuleUsesVariants,
            int randomRuleId)
        {
            Assert.That(tile.m_TilingRules, Has.Count.EqualTo(expectedRuleCount));
            AssertSpriteUsesProductionAtlas(tile.m_DefaultSprite, $"{tile.name}: default");

            var ids = new HashSet<int>();
            var hasEightNeighbors = false;
            for (var index = 0; index < tile.m_TilingRules.Count; index++)
            {
                var rule = tile.m_TilingRules[index];
                Assert.That(ids.Add(rule.m_Id), Is.True, $"Rule IDが重複しています: {tile.name}/{rule.m_Id}");
                var usesVariants = everyRuleUsesVariants || rule.m_Id == randomRuleId;
                var expectedSpriteCount = usesVariants ? 4 : 1;
                var expectedOutput = usesVariants
                    ? RuleTile.TilingRuleOutput.OutputSprite.Random
                    : RuleTile.TilingRuleOutput.OutputSprite.Single;

                Assert.That(rule.m_Sprites, Has.Length.EqualTo(expectedSpriteCount), $"{tile.name}: rule {rule.m_Id:x2}");
                Assert.That(rule.m_Output, Is.EqualTo(expectedOutput), $"{tile.name}: rule {rule.m_Id:x2}");
                Assert.That(rule.m_RuleTransform, Is.EqualTo(RuleTile.TilingRuleOutput.Transform.Fixed));
                Assert.That(rule.m_RandomTransform, Is.EqualTo(RuleTile.TilingRuleOutput.Transform.Fixed));
                for (var spriteIndex = 0; spriteIndex < rule.m_Sprites.Length; spriteIndex++)
                {
                    var expectedName = spriteIndex == 0
                        ? $"{spritePrefix}_{rule.m_Id:x2}"
                        : $"{spritePrefix}_{rule.m_Id:x2}_v{spriteIndex + 1:00}";
                    Assert.That(rule.m_Sprites[spriteIndex].name, Is.EqualTo(expectedName));
                    AssertSpriteUsesProductionAtlas(
                        rule.m_Sprites[spriteIndex],
                        $"{tile.name}: rule {rule.m_Id:x2}/sprite {spriteIndex}");
                }

                hasEightNeighbors |= rule.m_NeighborPositions.Count == 8;
            }

            Assert.That(ids, Has.Count.EqualTo(expectedRuleCount));
            Assert.That(hasEightNeighbors, Is.EqualTo(expectsDiagonalRule));
        }

        /// <summary>
        /// Sampleの保存Tileと差分Indexを収集し、使用数、隣接、連続、固定Offset、一様行列の各回帰条件をまとめて検査する。
        /// </summary>
        private static void AssertGroundVariationDistribution(Tilemap ground, GroundVariationTile variationTile)
        {
            const int expectedCellCount = 600;
            const int expectedWidth = 30;
            const int expectedHeight = 20;
            var variantByCell = new Dictionary<Vector3Int, int>();
            var usedVariants = new HashSet<int>();
            var minX = int.MaxValue;
            var minY = int.MaxValue;
            var maxX = int.MinValue;
            var maxY = int.MinValue;

            foreach (var cell in ground.cellBounds.allPositionsWithin)
            {
                var savedTile = ground.GetTile(cell);
                if (savedTile == null)
                {
                    continue;
                }

                Assert.That(savedTile, Is.SameAs(variationTile), $"Groundに別Tileが保存されています: {cell}");
                var variantIndex = variationTile.GetVariantIndex(cell);
                Assert.That(variantIndex, Is.InRange(0, variationTile.VariantCount - 1), cell.ToString());
                variantByCell.Add(cell, variantIndex);
                usedVariants.Add(variantIndex);
                minX = Math.Min(minX, cell.x);
                minY = Math.Min(minY, cell.y);
                maxX = Math.Max(maxX, cell.x);
                maxY = Math.Max(maxY, cell.y);
            }

            Assert.That(variantByCell, Has.Count.EqualTo(expectedCellCount));
            Assert.That(ground.GetUsedTilesCount(), Is.EqualTo(1), "GroundにはGrassVariationだけを保存します。");
            Assert.That(maxX - minX + 1, Is.EqualTo(expectedWidth));
            Assert.That(maxY - minY + 1, Is.EqualTo(expectedHeight));
            Assert.That(usedVariants, Is.EquivalentTo(new[] { 0, 1, 2, 3, 4, 5, 6, 7 }));

            var sampleBounds = new BoundsInt(minX, minY, 0, expectedWidth, expectedHeight, 1);
            foreach (var cell in sampleBounds.allPositionsWithin)
            {
                Assert.That(variantByCell.ContainsKey(cell), Is.True, $"Ground Cellが欠けています: {cell}");
            }

            AssertGroundCardinalAdjacencyRate(variantByCell);
            Assert.That(
                GetMaximumGroundRun(variantByCell, sampleBounds),
                Is.LessThanOrEqualTo(4),
                "同じGrass差分がCardinal方向へ5 Cell以上連続しています。");
            AssertGroundOffsetMatchRate(variantByCell, sampleBounds, 4);
            AssertGroundOffsetMatchRate(variantByCell, sampleBounds, 8);
            AssertGroundOffsetMatchRate(variantByCell, sampleBounds, 16);
            AssertGroundHasNoUniformRowsOrColumns(variantByCell, sampleBounds);
        }

        /// <summary>
        /// 右・上の隣接Pairを一度ずつ数え、同一差分が局所的に固まりすぎていないかを検査する。
        /// </summary>
        private static void AssertGroundCardinalAdjacencyRate(IReadOnlyDictionary<Vector3Int, int> variantByCell)
        {
            var pairCount = 0;
            var matchCount = 0;
            foreach (var entry in variantByCell)
            {
                var right = entry.Key + Vector3Int.right;
                var up = entry.Key + Vector3Int.up;
                if (variantByCell.TryGetValue(right, out var rightVariant))
                {
                    pairCount++;
                    matchCount += rightVariant == entry.Value ? 1 : 0;
                }

                if (variantByCell.TryGetValue(up, out var upVariant))
                {
                    pairCount++;
                    matchCount += upVariant == entry.Value ? 1 : 0;
                }
            }

            Assert.That(pairCount, Is.GreaterThan(0));
            var matchRate = (double)matchCount / pairCount;
            Assert.That(matchRate, Is.LessThan(0.2d), $"同一Grass差分のCardinal隣接率が高すぎます: {matchRate:P2}");
        }

        /// <summary>
        /// 全行・全列を走査し、同一差分が連続する最長Cell数を返す。
        /// </summary>
        private static int GetMaximumGroundRun(
            IReadOnlyDictionary<Vector3Int, int> variantByCell,
            BoundsInt bounds)
        {
            var maximumRun = 0;
            for (var y = bounds.yMin; y < bounds.yMax; y++)
            {
                maximumRun = Math.Max(maximumRun, GetGroundRun(variantByCell, bounds.xMin, bounds.xMax, y, true));
            }

            for (var x = bounds.xMin; x < bounds.xMax; x++)
            {
                maximumRun = Math.Max(maximumRun, GetGroundRun(variantByCell, bounds.yMin, bounds.yMax, x, false));
            }

            return maximumRun;
        }

        /// <summary>
        /// 指定した1行または1列について、同一差分の最長連続数を数える。
        /// </summary>
        private static int GetGroundRun(
            IReadOnlyDictionary<Vector3Int, int> variantByCell,
            int start,
            int end,
            int fixedCoordinate,
            bool horizontal)
        {
            var maximumRun = 0;
            var currentRun = 0;
            var previousVariant = -1;
            for (var coordinate = start; coordinate < end; coordinate++)
            {
                var cell = horizontal
                    ? new Vector3Int(coordinate, fixedCoordinate, 0)
                    : new Vector3Int(fixedCoordinate, coordinate, 0);
                var variant = variantByCell[cell];
                currentRun = variant == previousVariant ? currentRun + 1 : 1;
                previousVariant = variant;
                maximumRun = Math.Max(maximumRun, currentRun);
            }

            return maximumRun;
        }

        /// <summary>
        /// 指定Cell間隔で同じ差分へ戻る割合を縦横で数え、短周期の反復を検出する。
        /// </summary>
        private static void AssertGroundOffsetMatchRate(
            IReadOnlyDictionary<Vector3Int, int> variantByCell,
            BoundsInt bounds,
            int offset)
        {
            var pairCount = 0;
            var matchCount = 0;
            foreach (var cell in bounds.allPositionsWithin)
            {
                var horizontal = cell + new Vector3Int(offset, 0, 0);
                if (horizontal.x < bounds.xMax)
                {
                    pairCount++;
                    matchCount += variantByCell[cell] == variantByCell[horizontal] ? 1 : 0;
                }

                var vertical = cell + new Vector3Int(0, offset, 0);
                if (vertical.y < bounds.yMax)
                {
                    pairCount++;
                    matchCount += variantByCell[cell] == variantByCell[vertical] ? 1 : 0;
                }
            }

            Assert.That(pairCount, Is.GreaterThan(0), $"offset {offset}");
            var matchRate = (double)matchCount / pairCount;
            Assert.That(matchRate, Is.LessThan(0.3d), $"offset {offset}のGrass一致率が高すぎます: {matchRate:P2}");
        }

        /// <summary>
        /// 行または列が単一差分だけで埋まる一様な帯を禁止し、32px Gridを強調する回帰を検出する。
        /// </summary>
        private static void AssertGroundHasNoUniformRowsOrColumns(
            IReadOnlyDictionary<Vector3Int, int> variantByCell,
            BoundsInt bounds)
        {
            for (var y = bounds.yMin; y < bounds.yMax; y++)
            {
                var variants = new HashSet<int>();
                for (var x = bounds.xMin; x < bounds.xMax; x++)
                {
                    variants.Add(variantByCell[new Vector3Int(x, y, 0)]);
                }

                Assert.That(variants, Has.Count.GreaterThan(1), $"Ground row {y}が単一差分だけで埋まっています。");
            }

            for (var x = bounds.xMin; x < bounds.xMax; x++)
            {
                var variants = new HashSet<int>();
                for (var y = bounds.yMin; y < bounds.yMax; y++)
                {
                    variants.Add(variantByCell[new Vector3Int(x, y, 0)]);
                }

                Assert.That(variants, Has.Count.GreaterThan(1), $"Ground column {x}が単一差分だけで埋まっています。");
            }
        }

        /// <summary>
        /// Sample内のDirtとStoneを同じ道路集合として列挙し、全Cellの4近傍連結と異素材接続を確認する。
        /// </summary>
        private static void AssertRoadCellsFormSingleMixedMaterialNetwork(
            Tilemap terrain,
            RoadConnectionRuleTile dirt,
            RoadConnectionRuleTile stone)
        {
            var roadCells = new HashSet<Vector3Int>();
            var dirtCellCount = 0;
            var stoneCellCount = 0;
            foreach (var cell in terrain.cellBounds.allPositionsWithin)
            {
                var tile = terrain.GetTile(cell);
                if (tile == dirt)
                {
                    roadCells.Add(cell);
                    dirtCellCount++;
                }
                else if (tile == stone)
                {
                    roadCells.Add(cell);
                    stoneCellCount++;
                }
            }

            Assert.That(dirtCellCount, Is.GreaterThan(0));
            Assert.That(stoneCellCount, Is.GreaterThan(0));
            Assert.That(
                MapRoadPathRasterizer.IsSingleCardinalComponent(roadCells),
                Is.True,
                "SampleのDirtとStoneを合わせた道路網が4近傍で分断されています。");

            var hasMixedMaterialAdjacency = false;
            foreach (var cell in roadCells)
            {
                var tile = terrain.GetTile(cell);
                for (var directionIndex = 0; directionIndex < CardinalDirections.Length; directionIndex++)
                {
                    var neighbor = terrain.GetTile(cell + CardinalDirections[directionIndex]);
                    if (tile == dirt && neighbor == stone || tile == stone && neighbor == dirt)
                    {
                        hasMixedMaterialAdjacency = true;
                        break;
                    }
                }

                if (hasMixedMaterialAdjacency)
                {
                    break;
                }
            }

            Assert.That(
                hasMixedMaterialAdjacency,
                Is.True,
                "Sample道路にDirtとStoneの相互材質接続がありません。");
        }

        /// <summary>
        /// 自由配置Instanceが1Pixel境界、無回転、等倍という共通Transform契約を満たすか確認する。
        /// </summary>
        private static void AssertPixelSnappedTransform(Transform instance)
        {
            const float tolerance = 0.001f;
            var position = instance.position;
            var pixelsPerUnit = GbaMapSpritePostprocessor.RequiredPixelsPerUnit;
            Assert.That(position.x * pixelsPerUnit, Is.EqualTo(Mathf.Round(position.x * pixelsPerUnit)).Within(tolerance), instance.name);
            Assert.That(position.y * pixelsPerUnit, Is.EqualTo(Mathf.Round(position.y * pixelsPerUnit)).Within(tolerance), instance.name);
            Assert.That(position.z * pixelsPerUnit, Is.EqualTo(Mathf.Round(position.z * pixelsPerUnit)).Within(tolerance), instance.name);
            Assert.That(Quaternion.Angle(instance.rotation, Quaternion.identity), Is.LessThanOrEqualTo(tolerance), instance.name);
            Assert.That((instance.localScale - Vector3.one).sqrMagnitude, Is.LessThanOrEqualTo(tolerance * tolerance), instance.name);
        }

        /// <summary>
        /// 1Pixel Snap済み位置がさらに1Cell整数境界へ一致しているかを、自由配置の回帰確認用に判定する。
        /// </summary>
        private static bool IsCellAligned(Vector3 position)
        {
            const float tolerance = 0.001f;
            return Mathf.Abs(position.x - Mathf.Round(position.x)) <= tolerance
                && Mathf.Abs(position.y - Mathf.Round(position.y)) <= tolerance;
        }

        /// <summary>
        /// AtlasからImportされたSub-Spriteを名前で索引化し、重複名をその場で失敗させる。
        /// </summary>
        private static Dictionary<string, Sprite> LoadAtlasSpritesByName()
        {
            var result = new Dictionary<string, Sprite>(StringComparer.Ordinal);
            var assets = AssetDatabase.LoadAllAssetsAtPath(ProductionAtlasPath);
            for (var index = 0; index < assets.Length; index++)
            {
                if (!(assets[index] is Sprite sprite))
                {
                    continue;
                }

                Assert.That(result.ContainsKey(sprite.name), Is.False, $"Sub-Sprite名が重複しています: {sprite.name}");
                result.Add(sprite.name, sprite);
            }

            return result;
        }

        /// <summary>
        /// 表示Spriteが欠落せず、唯一のProduction Atlasを参照しているかを確認する。
        /// </summary>
        private static void AssertSpriteUsesProductionAtlas(Sprite sprite, string context)
        {
            Assert.That(sprite, Is.Not.Null, $"Sprite参照がありません: {context}");
            Assert.That(
                AssetDatabase.GetAssetPath(sprite).Replace('\\', '/'),
                Is.EqualTo(ProductionAtlasPath),
                context);
        }

        /// <summary>
        /// Sampleの水面Cellを列挙し、見た目だけでなく不可視Collisionも同じ座標へ置かれているか確認する。
        /// </summary>
        private static void AssertWaterCellsHaveCollision(
            Tilemap terrain,
            RuleTile waterRuleTile,
            Tilemap collision,
            Tile collisionTile)
        {
            var waterCellCount = 0;
            foreach (var cell in terrain.cellBounds.allPositionsWithin)
            {
                if (terrain.GetTile(cell) != waterRuleTile)
                {
                    continue;
                }

                waterCellCount++;
                Assert.That(collision.GetTile(cell), Is.EqualTo(collisionTile), $"Water Collisionがありません: {cell}");
            }

            Assert.That(waterCellCount, Is.GreaterThan(0));
        }

        /// <summary>
        /// 地形型だけはCollisionTilemap、幹・接地点型はPrefab内CollisionBodyを正本にする分離を確認する。
        /// </summary>
        private static void AssertObstacleCollisionSeparation(
            Transform obstacleVisuals,
            Tilemap collision,
            Tile collisionTile)
        {
            var markerCount = 0;
            for (var childIndex = 0; childIndex < obstacleVisuals.childCount; childIndex++)
            {
                var child = obstacleVisuals.GetChild(childIndex);
                var marker = child.GetComponent<MapObstacleVisualMarker>();
                Assert.That(marker, Is.Not.Null, child.name);
                markerCount++;

                if (marker.CollisionMode == MapObstacleCollisionMode.CollisionBody)
                {
                    var prefab = PrefabUtility.GetCorrespondingObjectFromSource(child.gameObject);
                    Assert.That(prefab, Is.Not.Null, child.name);
                    AssertPreciseCollisionBody(child.gameObject, marker, prefab.name);
                    continue;
                }

                var anchorCell = new Vector3Int(
                    Mathf.RoundToInt(child.position.x),
                    Mathf.RoundToInt(child.position.y),
                    0);
                foreach (var cell in marker.GetFootprintCells(anchorCell))
                {
                    Assert.That(
                        collision.GetTile(cell),
                        Is.EqualTo(collisionTile),
                        $"Obstacle Collisionがありません: {child.name}/{cell}");
                }
            }

            Assert.That(markerCount, Is.GreaterThanOrEqualTo(10));
        }

        /// <summary>
        /// CollisionBodyの階層・Physics責務と、素材ごとに定めた幹・接地点形状を厳密に照合する。
        /// </summary>
        private static void AssertPreciseCollisionBody(
            GameObject obstacle,
            MapObstacleVisualMarker marker,
            string definitionName)
        {
            var body = obstacle.transform.Find(MapObstacleVisualMarker.CollisionBodyName);
            Assert.That(body, Is.Not.Null, definitionName);
            Assert.That(body.parent, Is.SameAs(obstacle.transform), definitionName);
            Assert.That(body.gameObject.layer, Is.EqualTo(LayerMask.NameToLayer("MapCollision")), definitionName);
            Assert.That(body.localPosition, Is.EqualTo((Vector3)marker.CollisionOffset), definitionName);
            Assert.That(body.localScale, Is.EqualTo(Vector3.one), definitionName);
            Assert.That(
                Mathf.DeltaAngle(body.localEulerAngles.z, marker.CollisionRotationDegrees),
                Is.EqualTo(0f).Within(0.001f),
                definitionName);
            Assert.That(body.GetComponentsInChildren<Renderer>(true), Is.Empty, definitionName);
            Assert.That(body.GetComponentsInChildren<Rigidbody2D>(true), Is.Empty, definitionName);

            var colliders = obstacle.GetComponentsInChildren<Collider2D>(true);
            Assert.That(colliders, Has.Length.EqualTo(1), definitionName);
            var collider = colliders[0];
            Assert.That(collider.transform, Is.SameAs(body), definitionName);
            Assert.That(collider.enabled, Is.True, definitionName);
            Assert.That(collider.isTrigger, Is.False, definitionName);
            Assert.That(collider.offset, Is.EqualTo(Vector2.zero), definitionName);

            if (marker.CollisionShape == MapObstacleCollisionShape.Box)
            {
                Assert.That(collider, Is.TypeOf<BoxCollider2D>(), definitionName);
                Assert.That(((BoxCollider2D)collider).size, Is.EqualTo(marker.CollisionSize), definitionName);
            }
            else
            {
                Assert.That(collider, Is.TypeOf<CapsuleCollider2D>(), definitionName);
                var capsule = (CapsuleCollider2D)collider;
                Assert.That(capsule.size, Is.EqualTo(marker.CollisionSize), definitionName);
                Assert.That(capsule.direction, Is.EqualTo(marker.CapsuleDirection), definitionName);
            }

            AssertExpectedCollisionProfile(marker, definitionName);
        }

        /// <summary>
        /// Atlas素材の足元Pixelから決めたCollider profileが不用意に拡大・移動しないことを固定する。
        /// </summary>
        private static void AssertExpectedCollisionProfile(
            MapObstacleVisualMarker marker,
            string definitionName)
        {
            MapObstacleCollisionShape expectedShape;
            Vector2 expectedSize;
            Vector2 expectedOffset;
            var expectedRotation = 0f;
            switch (definitionName)
            {
                case "ForestMassWide":
                    expectedShape = MapObstacleCollisionShape.Box;
                    expectedSize = new Vector2(5f, 0.4375f);
                    expectedOffset = new Vector2(0f, 0.34375f);
                    break;
                case "ForestMassDeep":
                    expectedShape = MapObstacleCollisionShape.Box;
                    expectedSize = new Vector2(4.25f, 0.5f);
                    expectedOffset = new Vector2(0f, 0.40625f);
                    break;
                case "ForestFrontStrip":
                    expectedShape = MapObstacleCollisionShape.Box;
                    expectedSize = new Vector2(6.5f, 0.4375f);
                    expectedOffset = new Vector2(-0.03125f, 0.3125f);
                    break;
                case "TreeMedium01":
                    expectedShape = MapObstacleCollisionShape.Capsule;
                    expectedSize = new Vector2(0.625f, 0.3125f);
                    expectedOffset = new Vector2(-0.03125f, 0.28125f);
                    break;
                case "TreeMedium02":
                    expectedShape = MapObstacleCollisionShape.Capsule;
                    expectedSize = new Vector2(0.75f, 0.3125f);
                    expectedOffset = new Vector2(0f, 0.28125f);
                    break;
                case "TreeMedium03":
                    expectedShape = MapObstacleCollisionShape.Capsule;
                    expectedSize = new Vector2(0.5625f, 0.3125f);
                    expectedOffset = new Vector2(0f, 0.25f);
                    break;
                case "BlockingBush":
                    expectedShape = MapObstacleCollisionShape.Capsule;
                    expectedSize = new Vector2(0.75f, 0.3125f);
                    expectedOffset = new Vector2(0f, 0.25f);
                    break;
                case "MossyRock":
                    expectedShape = MapObstacleCollisionShape.Capsule;
                    expectedSize = new Vector2(0.875f, 0.4375f);
                    expectedOffset = new Vector2(0f, 0.28125f);
                    break;
                case "Stump":
                    expectedShape = MapObstacleCollisionShape.Capsule;
                    expectedSize = new Vector2(0.625f, 0.375f);
                    expectedOffset = new Vector2(0f, 0.25f);
                    break;
                case "FallenLog":
                    expectedShape = MapObstacleCollisionShape.Capsule;
                    expectedSize = new Vector2(0.9375f, 0.375f);
                    expectedOffset = new Vector2(-0.015625f, 0.4375f);
                    expectedRotation = -35f;
                    break;
                default:
                    Assert.Fail($"CollisionBodyを持つ未登録Prefabです: {definitionName}");
                    return;
            }

            Assert.That(marker.CollisionShape, Is.EqualTo(expectedShape), definitionName);
            Assert.That(marker.CollisionSize, Is.EqualTo(expectedSize), definitionName);
            Assert.That(marker.CollisionOffset, Is.EqualTo(expectedOffset), definitionName);
            Assert.That(
                Mathf.DeltaAngle(marker.CollisionRotationDegrees, expectedRotation),
                Is.EqualTo(0f).Within(0.001f),
                definitionName);
            if (expectedShape == MapObstacleCollisionShape.Capsule)
            {
                Assert.That(marker.CapsuleDirection, Is.EqualTo(CapsuleDirection2D.Horizontal), definitionName);
            }
        }

        /// <summary>
        /// 指定Sceneが既に読み込まれているかをPathで検索する。
        /// </summary>
        private static bool TryGetLoadedScene(string path, out Scene scene)
        {
            for (var index = 0; index < SceneManager.sceneCount; index++)
            {
                var candidate = SceneManager.GetSceneAt(index);
                if (candidate.path == path)
                {
                    scene = candidate;
                    return true;
                }
            }

            scene = default;
            return false;
        }

        /// <summary>
        /// Scene直下のRootを名前で検索し、Sample構造の欠落を明示する。
        /// </summary>
        private static GameObject FindRoot(Scene scene, string name)
        {
            var roots = scene.GetRootGameObjects();
            for (var index = 0; index < roots.Length; index++)
            {
                if (roots[index].name == name)
                {
                    return roots[index];
                }
            }

            Assert.Fail($"Scene Rootがありません: {name}");
            return null;
        }

        /// <summary>
        /// Tilemapの使用Tile集合に、指定Tile Assetが含まれているかを調べる。
        /// </summary>
        private static bool TilemapUsesTile(Tilemap tilemap, TileBase expected)
        {
            var tiles = new TileBase[tilemap.GetUsedTilesCount()];
            tilemap.GetUsedTilesNonAlloc(tiles);
            for (var index = 0; index < tiles.Length; index++)
            {
                if (tiles[index] == expected)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
