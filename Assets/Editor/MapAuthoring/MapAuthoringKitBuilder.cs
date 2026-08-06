using System;
using System.Collections.Generic;
using System.IO;
using FantasyRoyale.MapAuthoringKit;
using FantasyRoyale.MapAuthoringKit.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.SceneTemplate;
using UnityEditor.Tilemaps;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using Object = UnityEngine.Object;

namespace FantasyRoyale.Editor.MapAuthoring
{
    /// <summary>
    /// 単一Production AtlasのSub-Spriteから、Unity標準編集で使うTile、Palette、Prefab、Scene Templateを再構築する。
    /// 日常のマップ編集UIは提供せず、Git管理Assetの生成規約だけを一つの入口へ固定する。
    /// </summary>
    public static class MapAuthoringKitBuilder
    {
        public const string ProductionRoot = "Assets/Art/Map/Production/Forest";
        public const string ProductionAtlasPath =
            "Assets/Art/Map/Production/Forest/forest-gba-production-atlas.png";
        public const string ProductionCatalogPath =
            "Assets/Art/Map/Production/Forest/forest-gba-production-atlas.json";
        public const string TileAssetRoot = "Assets/Data/MapAuthoring/Tiles/Forest";
        public const string PaletteRoot = "Assets/Data/MapAuthoring/Palettes/Forest";
        private const string ObsoleteBrushRoot = "Assets/Data/MapAuthoring/Brushes";
        public const string PrefabRoot = "Assets/Prefabs/MapAuthoring/Forest";
        public const string SceneTemplatePath =
            "Assets/Scenes/MapAuthoring/GbaForestMapAuthoringTemplate.scenetemplate";
        public const string SampleScenePath =
            "Assets/Scenes/MapAuthoring/GbaForestMapSample.unity";
        public const string SamplePreviewPath =
            "Assets/Art/Generated/MapAuthoring/QA/gba-forest-sample-unity.png";
        public const string Renderer2DDataPath = "Assets/Settings/Renderer2D.asset";

        private const int North = 1;
        private const int East = 2;
        private const int South = 4;
        private const int West = 8;

        private const int BlobNorth = 1;
        private const int BlobNorthEast = 2;
        private const int BlobEast = 4;
        private const int BlobSouthEast = 8;
        private const int BlobSouth = 16;
        private const int BlobSouthWest = 32;
        private const int BlobWest = 64;
        private const int BlobNorthWest = 128;

        private static readonly Vector3Int[] CardinalDirections =
        {
            Vector3Int.up,
            Vector3Int.right,
            Vector3Int.down,
            Vector3Int.left
        };

        private static readonly int[] CardinalBits =
        {
            North,
            East,
            South,
            West
        };

        private static readonly Vector3Int[] BlobDirections =
        {
            Vector3Int.up,
            new Vector3Int(1, 1, 0),
            Vector3Int.right,
            new Vector3Int(1, -1, 0),
            Vector3Int.down,
            new Vector3Int(-1, -1, 0),
            Vector3Int.left,
            new Vector3Int(-1, 1, 0)
        };

        private static readonly int[] BlobBits =
        {
            BlobNorth,
            BlobNorthEast,
            BlobEast,
            BlobSouthEast,
            BlobSouth,
            BlobSouthWest,
            BlobWest,
            BlobNorthWest
        };

        private static readonly int[] CanonicalBlobMasks = BuildCanonicalBlobMasks();
        private static Dictionary<string, Sprite> atlasSpritesByName;

        /// <summary>
        /// 一回のKit再生成で作ったAsset参照を保持し、後続のPalette、Prefab、Sample生成へ渡す。
        /// </summary>
        private sealed class BuiltAssets
        {
            public GroundVariationTile GrassTile;
            public Tile StoneVariant;
            public RoadConnectionRuleTile DirtRuleTile;
            public RoadConnectionRuleTile StoneRuleTile;
            public RuleTile WaterRuleTile;
            public Tile CollisionTile;
            public readonly Dictionary<string, GameObject> VisualPrefabs = new Dictionary<string, GameObject>();
        }

        /// <summary>
        /// 表示Prefabを自由配置先と描画責務で分類し、装飾・障害物・操作物を混在させない。
        /// </summary>
        private enum VisualPlacementKind
        {
            Decoration,
            Obstacle,
            Prop
        }

        /// <summary>
        /// 地面へ固定する表示と、足元のWorld Yで前後関係を決める表示を分類する。
        /// </summary>
        private enum VisualDepthKind
        {
            GroundDetail,
            WorldYSorted
        }

        /// <summary>
        /// 表示PrefabのSprite、生成計画用Footprint、足元Collision形状を一箇所で定義する。
        /// </summary>
        private sealed class VisualDefinition
        {
            public VisualDefinition(
                string name,
                string spriteName,
                VisualPlacementKind placementKind,
                VisualDepthKind depthKind,
                Vector2Int footprintSize,
                Vector2Int footprintOffset)
                : this(
                    name,
                    spriteName,
                    placementKind,
                    depthKind,
                    footprintSize,
                    footprintOffset,
                    MapObstacleCollisionMode.TilemapFootprint,
                    MapObstacleCollisionShape.Box,
                    Vector2.zero,
                    Vector2.zero)
            {
            }

            public VisualDefinition(
                string name,
                string spriteName,
                VisualPlacementKind placementKind,
                VisualDepthKind depthKind,
                Vector2Int footprintSize,
                Vector2Int footprintOffset,
                MapObstacleCollisionMode collisionMode,
                MapObstacleCollisionShape collisionShape,
                Vector2 collisionSize,
                Vector2 collisionOffset,
                float collisionRotation = 0f,
                CapsuleDirection2D collisionDirection = CapsuleDirection2D.Horizontal)
            {
                Name = name;
                SpriteName = spriteName;
                PlacementKind = placementKind;
                DepthKind = depthKind;
                FootprintSize = footprintSize;
                FootprintOffset = footprintOffset;
                CollisionMode = collisionMode;
                CollisionShape = collisionShape;
                CollisionSize = collisionSize;
                CollisionOffset = collisionOffset;
                CollisionRotation = collisionRotation;
                CollisionDirection = collisionDirection;
            }

            public string Name { get; }
            public string SpriteName { get; }
            public VisualPlacementKind PlacementKind { get; }
            public VisualDepthKind DepthKind { get; }
            public bool Obstacle => PlacementKind == VisualPlacementKind.Obstacle;
            public Vector2Int FootprintSize { get; }
            public Vector2Int FootprintOffset { get; }
            public MapObstacleCollisionMode CollisionMode { get; }
            public MapObstacleCollisionShape CollisionShape { get; }
            public Vector2 CollisionSize { get; }
            public Vector2 CollisionOffset { get; }
            public float CollisionRotation { get; }
            public CapsuleDirection2D CollisionDirection { get; }
        }

        private static readonly VisualDefinition[] VisualDefinitions =
        {
            new VisualDefinition("tall_grass", "detail_tall_grass", VisualPlacementKind.Decoration, VisualDepthKind.WorldYSorted, Vector2Int.zero, Vector2Int.zero),
            new VisualDefinition("flower_patch", "detail_flower_patch", VisualPlacementKind.Decoration, VisualDepthKind.GroundDetail, Vector2Int.zero, Vector2Int.zero),
            new VisualDefinition("wildflowers", "detail_wildflowers", VisualPlacementKind.Decoration, VisualDepthKind.GroundDetail, Vector2Int.zero, Vector2Int.zero),
            new VisualDefinition("reeds", "detail_reeds", VisualPlacementKind.Decoration, VisualDepthKind.WorldYSorted, Vector2Int.zero, Vector2Int.zero),
            new VisualDefinition(
                "forest_mass_wide", "obstacle_forest_mass_wide", VisualPlacementKind.Obstacle,
                VisualDepthKind.WorldYSorted, new Vector2Int(6, 2), new Vector2Int(-3, 0),
                MapObstacleCollisionMode.CollisionBody, MapObstacleCollisionShape.Box,
                new Vector2(5f, 0.4375f), new Vector2(0f, 0.34375f)),
            new VisualDefinition(
                "forest_mass_deep", "obstacle_forest_mass_deep", VisualPlacementKind.Obstacle,
                VisualDepthKind.WorldYSorted, new Vector2Int(5, 3), new Vector2Int(-2, 0),
                MapObstacleCollisionMode.CollisionBody, MapObstacleCollisionShape.Box,
                new Vector2(4.25f, 0.5f), new Vector2(0f, 0.40625f)),
            new VisualDefinition(
                "forest_front_strip", "obstacle_forest_front_strip", VisualPlacementKind.Obstacle,
                VisualDepthKind.WorldYSorted, new Vector2Int(7, 1), new Vector2Int(-3, 0),
                MapObstacleCollisionMode.CollisionBody, MapObstacleCollisionShape.Box,
                new Vector2(6.5f, 0.4375f), new Vector2(-0.03125f, 0.3125f)),
            new VisualDefinition("cliff_straight_wide", "obstacle_cliff_straight_wide", VisualPlacementKind.Obstacle, VisualDepthKind.WorldYSorted, new Vector2Int(6, 2), new Vector2Int(-3, -1)),
            new VisualDefinition("cliff_outer_corner", "obstacle_cliff_outer_corner", VisualPlacementKind.Obstacle, VisualDepthKind.WorldYSorted, new Vector2Int(4, 4), new Vector2Int(-2, -2)),
            new VisualDefinition(
                "tree_medium_01", "prop_tree_medium_01", VisualPlacementKind.Obstacle,
                VisualDepthKind.WorldYSorted, Vector2Int.one, Vector2Int.zero,
                MapObstacleCollisionMode.CollisionBody, MapObstacleCollisionShape.Capsule,
                new Vector2(0.625f, 0.3125f), new Vector2(-0.03125f, 0.28125f)),
            new VisualDefinition(
                "tree_medium_02", "prop_tree_medium_02", VisualPlacementKind.Obstacle,
                VisualDepthKind.WorldYSorted, Vector2Int.one, Vector2Int.zero,
                MapObstacleCollisionMode.CollisionBody, MapObstacleCollisionShape.Capsule,
                new Vector2(0.75f, 0.3125f), new Vector2(0f, 0.28125f)),
            new VisualDefinition(
                "tree_medium_03", "prop_tree_medium_03", VisualPlacementKind.Obstacle,
                VisualDepthKind.WorldYSorted, Vector2Int.one, Vector2Int.zero,
                MapObstacleCollisionMode.CollisionBody, MapObstacleCollisionShape.Capsule,
                new Vector2(0.5625f, 0.3125f), new Vector2(0f, 0.25f)),
            new VisualDefinition(
                "blocking_bush", "prop_blocking_bush", VisualPlacementKind.Obstacle,
                VisualDepthKind.WorldYSorted, Vector2Int.one, Vector2Int.zero,
                MapObstacleCollisionMode.CollisionBody, MapObstacleCollisionShape.Capsule,
                new Vector2(0.75f, 0.3125f), new Vector2(0f, 0.25f)),
            new VisualDefinition(
                "mossy_rock", "prop_mossy_rock", VisualPlacementKind.Obstacle,
                VisualDepthKind.WorldYSorted, Vector2Int.one, Vector2Int.zero,
                MapObstacleCollisionMode.CollisionBody, MapObstacleCollisionShape.Capsule,
                new Vector2(0.875f, 0.4375f), new Vector2(0f, 0.28125f)),
            new VisualDefinition(
                "stump", "prop_stump", VisualPlacementKind.Obstacle,
                VisualDepthKind.WorldYSorted, Vector2Int.one, Vector2Int.zero,
                MapObstacleCollisionMode.CollisionBody, MapObstacleCollisionShape.Capsule,
                new Vector2(0.625f, 0.375f), new Vector2(0f, 0.25f)),
            new VisualDefinition(
                "fallen_log", "prop_fallen_log", VisualPlacementKind.Obstacle,
                VisualDepthKind.WorldYSorted, new Vector2Int(2, 1), new Vector2Int(-1, 0),
                MapObstacleCollisionMode.CollisionBody, MapObstacleCollisionShape.Capsule,
                new Vector2(0.9375f, 0.375f), new Vector2(-0.015625f, 0.4375f),
                -35f, CapsuleDirection2D.Horizontal),
            new VisualDefinition("mushroom_patch", "prop_mushroom_patch", VisualPlacementKind.Prop, VisualDepthKind.WorldYSorted, Vector2Int.zero, Vector2Int.zero),
            new VisualDefinition("chest", "prop_chest", VisualPlacementKind.Prop, VisualDepthKind.WorldYSorted, Vector2Int.zero, Vector2Int.zero),
            new VisualDefinition("signpost", "prop_signpost", VisualPlacementKind.Prop, VisualDepthKind.WorldYSorted, Vector2Int.zero, Vector2Int.zero)
        };

        /// <summary>
        /// Production Spriteを再Importし、編集に必要な全Assetと動作確認用Sceneを一括再構築する。
        /// </summary>
        [MenuItem("FantasyRoyale/Map Authoring/Rebuild Complete Kit")]
        public static void RebuildCompleteKit()
        {
            EnsureFolder(TileAssetRoot);
            EnsureFolder(PaletteRoot);
            EnsureFolder(PrefabRoot);
            EnsureFolder("Assets/Scenes/MapAuthoring");
            EnsureFolder("Assets/Art/Generated/MapAuthoring/QA");
            ConfigureWorldYTransparencySorting();

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            ForceImportProductionSprites();
            DeleteObsoleteAssets();

            var assets = BuildTileAssets();
            BuildVisualPrefabs(assets);
            ValidateBuiltAssetsUseAtlas(assets);
            BuildTilePalettes(assets);

            GbaForestMapAuthoringSceneBuilder.BuildOrRefreshTemplate();
            BuildFormalSceneTemplate();
            BuildSampleScene(assets);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            ValidateGeneratedSample();
            TryCaptureSamplePreview();
            Debug.Log("Map Authoring Kit rebuilt successfully.");
        }

        /// <summary>
        /// URP 2D Rendererの透明描画をWorld Y軸へ固定し、下側の足元を後から描画させる。
        /// </summary>
        private static void ConfigureWorldYTransparencySorting()
        {
            var rendererData = AssetDatabase.LoadMainAssetAtPath(Renderer2DDataPath);
            if (rendererData == null)
            {
                throw new FileNotFoundException(
                    "URP 2D Renderer Dataがありません。",
                    Renderer2DDataPath);
            }

            var serializedRendererData = new SerializedObject(rendererData);
            var sortMode = serializedRendererData.FindProperty("m_TransparencySortMode");
            var sortAxis = serializedRendererData.FindProperty("m_TransparencySortAxis");
            if (sortMode == null || sortAxis == null)
            {
                throw new InvalidOperationException(
                    $"URP 2D Renderer Dataに透明描画設定がありません: {Renderer2DDataPath}");
            }

            sortMode.intValue = (int)TransparencySortMode.CustomAxis;
            sortAxis.vector3Value = Vector3.up;
            serializedRendererData.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(rendererData);
        }

        /// <summary>
        /// 単一Production Atlasだけを同期Importし、PostprocessorのMultiple Sprite契約を先に確定させる。
        /// </summary>
        private static void ForceImportProductionSprites()
        {
            if (!File.Exists(ProductionAtlasPath))
            {
                throw new FileNotFoundException("Production Atlasがありません。", ProductionAtlasPath);
            }

            if (!File.Exists(ProductionCatalogPath))
            {
                throw new FileNotFoundException("Production Atlas catalogがありません。", ProductionCatalogPath);
            }

            atlasSpritesByName = null;
            AssetDatabase.ImportAsset(
                ProductionAtlasPath,
                ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
        }

        /// <summary>
        /// 自由配置表示へ置換した旧RuleTile、Detail Tilemap用Asset、Brushを削除する。
        /// </summary>
        private static void DeleteObsoleteAssets()
        {
            var obsoletePaths = new[]
            {
                $"{TileAssetRoot}/Rules/ForestWallRuleTile.asset",
                $"{TileAssetRoot}/Rules/CliffRuleTile.asset",
                $"{TileAssetRoot}/Detail",
                $"{TileAssetRoot}/Ground/Grass01.asset",
                $"{TileAssetRoot}/Ground/Grass02.asset",
                $"{TileAssetRoot}/Ground/Grass03.asset",
                $"{TileAssetRoot}/Ground/Grass04.asset",
                $"{PaletteRoot}/ForestObstaclePalette.prefab",
                $"{PaletteRoot}/ForestDetailPalette.prefab",
                ObsoleteBrushRoot
            };

            for (var i = 0; i < obsoletePaths.Length; i++)
            {
                if (AssetDatabase.LoadMainAssetAtPath(obsoletePaths[i]) != null)
                {
                    AssetDatabase.DeleteAsset(obsoletePaths[i]);
                }
            }
        }

        /// <summary>
        /// Atlas内の静的Spriteと接続Mask SpriteからTile・RuleTileを作り、既存Asset参照を維持して更新する。
        /// </summary>
        private static BuiltAssets BuildTileAssets()
        {
            var assets = new BuiltAssets();
            assets.GrassTile = CreateOrUpdateGroundVariationTile();

            assets.StoneVariant = CreateOrUpdateTile(
                $"{TileAssetRoot}/Ground/StoneVariant.asset",
                "stone_0f",
                Tile.ColliderType.None);
            assets.DirtRuleTile = CreateOrUpdateCardinalRuleTile("Dirt", "dirt");
            assets.StoneRuleTile = CreateOrUpdateCardinalRuleTile("Stone", "stone");
            assets.DirtRuleTile.Configure("road");
            assets.StoneRuleTile.Configure("road");
            EditorUtility.SetDirty(assets.DirtRuleTile);
            EditorUtility.SetDirty(assets.StoneRuleTile);
            assets.WaterRuleTile = CreateOrUpdateBlobRuleTile("Water", "water");
            assets.CollisionTile = CreateOrUpdateTile(
                $"{TileAssetRoot}/Utility/CollisionTile.asset",
                null,
                Tile.ColliderType.Grid);
            return assets;
        }

        /// <summary>
        /// 8枚の低コントラスト芝Spriteを一つの座標Hash Tileへ設定し、塗り結果を単一Assetとして保存する。
        /// </summary>
        private static GroundVariationTile CreateOrUpdateGroundVariationTile()
        {
            var assetPath = $"{TileAssetRoot}/Ground/GrassVariation.asset";
            EnsureFolder($"{TileAssetRoot}/Ground");
            var tile = AssetDatabase.LoadAssetAtPath<GroundVariationTile>(assetPath);
            if (tile == null)
            {
                tile = ScriptableObject.CreateInstance<GroundVariationTile>();
                tile.name = "GrassVariation";
                AssetDatabase.CreateAsset(tile, assetPath);
            }

            var sprites = new Sprite[GroundVariationTile.RequiredVariantCount];
            for (var index = 0; index < sprites.Length; index++)
            {
                sprites[index] = RequireAtlasSprite($"ground_grass_{index + 1:00}");
            }

            tile.Configure(sprites, variationSeed: 0);
            EditorUtility.SetDirty(tile);
            return tile;
        }

        /// <summary>
        /// Sprite一枚を参照する標準Tileを既存GUIDのまま更新し、Collider用途だけを明示する。
        /// </summary>
        private static Tile CreateOrUpdateTile(
            string assetPath,
            string spriteName,
            Tile.ColliderType colliderType)
        {
            EnsureFolder(Path.GetDirectoryName(assetPath)?.Replace('\\', '/'));
            var tile = AssetDatabase.LoadAssetAtPath<Tile>(assetPath);
            if (tile == null)
            {
                tile = ScriptableObject.CreateInstance<Tile>();
                tile.name = Path.GetFileNameWithoutExtension(assetPath);
                AssetDatabase.CreateAsset(tile, assetPath);
            }

            tile.sprite = string.IsNullOrEmpty(spriteName) ? null : RequireAtlasSprite(spriteName);
            tile.color = Color.white;
            tile.transform = Matrix4x4.identity;
            tile.gameObject = null;
            tile.flags = TileFlags.LockColor | TileFlags.LockTransform;
            tile.colliderType = colliderType;
            EditorUtility.SetDirty(tile);
            return tile;
        }

        /// <summary>
        /// N/E/S/Wの同種隣接を全て明示した16Ruleを作り、全Maskの輪郭・質感4差分をRandom出力する。
        /// </summary>
        private static RoadConnectionRuleTile CreateOrUpdateCardinalRuleTile(
            string displayName,
            string spritePrefix)
        {
            var ruleTile = CreateOrLoadRoadRuleTile(displayName);
            ruleTile.m_DefaultSprite = RequireAtlasSprite($"{spritePrefix}_0f");
            ruleTile.m_TilingRules.Clear();

            for (var mask = 0; mask < 16; mask++)
            {
                var sprites = RequireRoadVariationSprites(spritePrefix, mask);
                var rule = new RuleTile.TilingRule
                {
                    m_Id = mask,
                    m_NeighborPositions = new List<Vector3Int>(),
                    m_Neighbors = new List<int>(),
                    m_Sprites = sprites,
                    m_Output = sprites.Length > 1
                        ? RuleTile.TilingRuleOutput.OutputSprite.Random
                        : RuleTile.TilingRuleOutput.OutputSprite.Single,
                    m_PerlinScale = 0.31f,
                    m_ColliderType = Tile.ColliderType.None,
                    m_RuleTransform = RuleTile.TilingRuleOutput.Transform.Fixed,
                    m_RandomTransform = RuleTile.TilingRuleOutput.Transform.Fixed
                };

                for (var directionIndex = 0; directionIndex < CardinalDirections.Length; directionIndex++)
                {
                    rule.m_NeighborPositions.Add(CardinalDirections[directionIndex]);
                    rule.m_Neighbors.Add(
                        (mask & CardinalBits[directionIndex]) != 0
                            ? RuleTile.TilingRuleOutput.Neighbor.This
                            : RuleTile.TilingRuleOutput.Neighbor.NotThis);
                }

                ruleTile.m_TilingRules.Add(rule);
            }

            ruleTile.UpdateNeighborPositions();
            EditorUtility.SetDirty(ruleTile);
            return ruleTile;
        }

        /// <summary>
        /// 道路用RuleTileを既存Assetから取得し、旧標準RuleTileなら型契約を更新して作り直す。
        /// </summary>
        private static RoadConnectionRuleTile CreateOrLoadRoadRuleTile(string displayName)
        {
            var assetPath = $"{TileAssetRoot}/Rules/{displayName}RuleTile.asset";
            EnsureFolder($"{TileAssetRoot}/Rules");
            var existing = AssetDatabase.LoadMainAssetAtPath(assetPath);
            if (existing != null && !(existing is RoadConnectionRuleTile))
            {
                AssetDatabase.DeleteAsset(assetPath);
            }

            var ruleTile = AssetDatabase.LoadAssetAtPath<RoadConnectionRuleTile>(assetPath);
            if (ruleTile == null)
            {
                ruleTile = ScriptableObject.CreateInstance<RoadConnectionRuleTile>();
                ruleTile.name = $"{displayName}RuleTile";
                AssetDatabase.CreateAsset(ruleTile, assetPath);
            }

            ruleTile.m_DefaultGameObject = null;
            ruleTile.m_DefaultColliderType = Tile.ColliderType.None;
            return ruleTile;
        }

        /// <summary>
        /// 8近傍のうち意味のある対角だけを評価する47 Blob Ruleを作り、全面水だけ質感差分をRandom出力する。
        /// </summary>
        private static RuleTile CreateOrUpdateBlobRuleTile(
            string displayName,
            string spritePrefix)
        {
            var ruleTile = CreateOrLoadRuleTile(displayName);
            ruleTile.m_DefaultSprite = RequireAtlasSprite($"{spritePrefix}_ff");
            ruleTile.m_TilingRules.Clear();

            for (var maskIndex = 0; maskIndex < CanonicalBlobMasks.Length; maskIndex++)
            {
                var mask = CanonicalBlobMasks[maskIndex];
                var sprites = RequireSurfaceVariationSprites(spritePrefix, mask, 0xFF);
                var rule = new RuleTile.TilingRule
                {
                    m_Id = mask,
                    m_NeighborPositions = new List<Vector3Int>(),
                    m_Neighbors = new List<int>(),
                    m_Sprites = sprites,
                    m_Output = sprites.Length > 1
                        ? RuleTile.TilingRuleOutput.OutputSprite.Random
                        : RuleTile.TilingRuleOutput.OutputSprite.Single,
                    m_PerlinScale = 0.37f,
                    m_ColliderType = Tile.ColliderType.None,
                    m_RuleTransform = RuleTile.TilingRuleOutput.Transform.Fixed,
                    m_RandomTransform = RuleTile.TilingRuleOutput.Transform.Fixed
                };

                for (var directionIndex = 0; directionIndex < BlobDirections.Length; directionIndex++)
                {
                    var bit = BlobBits[directionIndex];
                    if (IsDiagonalBit(bit) && !IsRelevantDiagonal(mask, bit))
                    {
                        continue;
                    }

                    rule.m_NeighborPositions.Add(BlobDirections[directionIndex]);
                    rule.m_Neighbors.Add(
                        (mask & bit) != 0
                            ? RuleTile.TilingRuleOutput.Neighbor.This
                            : RuleTile.TilingRuleOutput.Neighbor.NotThis);
                }

                ruleTile.m_TilingRules.Add(rule);
            }

            ruleTile.UpdateNeighborPositions();
            EditorUtility.SetDirty(ruleTile);
            return ruleTile;
        }

        /// <summary>
        /// RuleTile Assetを既存GUIDのまま再利用し、共通Default設定を初期化する。
        /// </summary>
        private static RuleTile CreateOrLoadRuleTile(string displayName)
        {
            var assetPath = $"{TileAssetRoot}/Rules/{displayName}RuleTile.asset";
            EnsureFolder($"{TileAssetRoot}/Rules");
            var ruleTile = AssetDatabase.LoadAssetAtPath<RuleTile>(assetPath);
            if (ruleTile == null)
            {
                ruleTile = ScriptableObject.CreateInstance<RuleTile>();
                ruleTile.name = $"{displayName}RuleTile";
                AssetDatabase.CreateAsset(ruleTile, assetPath);
            }

            ruleTile.m_DefaultGameObject = null;
            ruleTile.m_DefaultColliderType = Tile.ColliderType.None;
            return ruleTile;
        }

        /// <summary>
        /// 4 Cardinalの全組合せへ、両隣が接続した角だけ対角bitを追加して47 Maskを生成する。
        /// </summary>
        private static int[] BuildCanonicalBlobMasks()
        {
            var masks = new HashSet<int>();
            for (var cardinalMask = 0; cardinalMask < 16; cardinalMask++)
            {
                var baseMask = 0;
                if ((cardinalMask & North) != 0) baseMask |= BlobNorth;
                if ((cardinalMask & East) != 0) baseMask |= BlobEast;
                if ((cardinalMask & South) != 0) baseMask |= BlobSouth;
                if ((cardinalMask & West) != 0) baseMask |= BlobWest;

                var optionalDiagonals = new List<int>();
                if ((cardinalMask & (North | East)) == (North | East)) optionalDiagonals.Add(BlobNorthEast);
                if ((cardinalMask & (East | South)) == (East | South)) optionalDiagonals.Add(BlobSouthEast);
                if ((cardinalMask & (South | West)) == (South | West)) optionalDiagonals.Add(BlobSouthWest);
                if ((cardinalMask & (West | North)) == (West | North)) optionalDiagonals.Add(BlobNorthWest);

                for (var selection = 0; selection < 1 << optionalDiagonals.Count; selection++)
                {
                    var mask = baseMask;
                    for (var diagonalIndex = 0; diagonalIndex < optionalDiagonals.Count; diagonalIndex++)
                    {
                        if ((selection & (1 << diagonalIndex)) != 0)
                        {
                            mask |= optionalDiagonals[diagonalIndex];
                        }
                    }

                    masks.Add(mask);
                }
            }

            var result = new List<int>(masks);
            result.Sort();
            if (result.Count != 47)
            {
                throw new InvalidOperationException($"Blob Ruleは47 Mask必要です: {result.Count}");
            }

            return result.ToArray();
        }

        /// <summary>
        /// 対角bitかを判定し、Cardinalと同じ必須条件として誤登録しないようにする。
        /// </summary>
        private static bool IsDiagonalBit(int bit)
        {
            return bit == BlobNorthEast
                || bit == BlobSouthEast
                || bit == BlobSouthWest
                || bit == BlobNorthWest;
        }

        /// <summary>
        /// 対角の両隣が接続中の場合だけ、その対角をThis/NotThis判定へ含める。
        /// </summary>
        private static bool IsRelevantDiagonal(int mask, int diagonalBit)
        {
            switch (diagonalBit)
            {
                case BlobNorthEast:
                    return (mask & (BlobNorth | BlobEast)) == (BlobNorth | BlobEast);
                case BlobSouthEast:
                    return (mask & (BlobEast | BlobSouth)) == (BlobEast | BlobSouth);
                case BlobSouthWest:
                    return (mask & (BlobSouth | BlobWest)) == (BlobSouth | BlobWest);
                case BlobNorthWest:
                    return (mask & (BlobWest | BlobNorth)) == (BlobWest | BlobNorth);
                default:
                    return false;
            }
        }

        /// <summary>
        /// 表示Prefabを作り、障害物には生成計画用Markerと足元へ寄せた独立CollisionBodyを設定する。
        /// </summary>
        private static void BuildVisualPrefabs(BuiltAssets assets)
        {
            for (var i = 0; i < VisualDefinitions.Length; i++)
            {
                var definition = VisualDefinitions[i];
                var prefabPath = $"{PrefabRoot}/{ToPascalCase(definition.Name)}.prefab";
                var temporary = new GameObject(ToPascalCase(definition.Name));
                try
                {
                    temporary.layer = ResolveLayer("Default");

                    var renderer = temporary.AddComponent<SpriteRenderer>();
                    renderer.sprite = RequireAtlasSprite(definition.SpriteName);
                    renderer.sortingLayerName = ResolveSortingLayer(
                        definition.DepthKind == VisualDepthKind.WorldYSorted
                            ? "WorldObjects"
                            : "MapDetail");
                    renderer.sortingOrder = 0;
                    renderer.spriteSortPoint = SpriteSortPoint.Pivot;

                    if (definition.Obstacle)
                    {
                        var marker = temporary.AddComponent<MapObstacleVisualMarker>();
                        marker.Configure(
                            definition.FootprintSize,
                            definition.FootprintOffset,
                            definition.CollisionMode,
                            definition.CollisionShape,
                            definition.CollisionSize,
                            definition.CollisionOffset,
                            definition.CollisionRotation,
                            definition.CollisionDirection);
                        if (definition.CollisionMode == MapObstacleCollisionMode.CollisionBody)
                        {
                            CreateCollisionBody(temporary.transform, definition);
                        }
                    }

                    EnsureFolder(PrefabRoot);
                    var prefab = PrefabUtility.SaveAsPrefabAsset(temporary, prefabPath);
                    if (prefab == null)
                    {
                        throw new InvalidOperationException($"Prefabを保存できません: {prefabPath}");
                    }

                    assets.VisualPrefabs[definition.Name] = prefab;
                }
                finally
                {
                    Object.DestroyImmediate(temporary);
                }
            }
        }

        /// <summary>
        /// 見た目とは別の子Objectへ一つだけColliderを置き、足元形状がPrefab移動へ確実に追従するようにする。
        /// </summary>
        private static void CreateCollisionBody(Transform parent, VisualDefinition definition)
        {
            var body = new GameObject(MapObstacleVisualMarker.CollisionBodyName);
            body.layer = ResolveLayer("MapCollision");
            body.transform.SetParent(parent, false);
            body.transform.localPosition = new Vector3(
                definition.CollisionOffset.x,
                definition.CollisionOffset.y,
                0f);
            body.transform.localRotation = Quaternion.Euler(0f, 0f, definition.CollisionRotation);
            body.transform.localScale = Vector3.one;

            Collider2D collider;
            switch (definition.CollisionShape)
            {
                case MapObstacleCollisionShape.Box:
                    var box = body.AddComponent<BoxCollider2D>();
                    box.size = definition.CollisionSize;
                    collider = box;
                    break;
                case MapObstacleCollisionShape.Capsule:
                    var capsule = body.AddComponent<CapsuleCollider2D>();
                    capsule.size = definition.CollisionSize;
                    capsule.direction = definition.CollisionDirection;
                    collider = capsule;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(definition.CollisionShape),
                        definition.CollisionShape,
                        "未対応のCollision形状です。");
            }

            collider.offset = Vector2.zero;
            collider.isTrigger = false;
        }

        /// <summary>
        /// 再構築したTile・RuleTile・Prefabが単一Atlasだけを参照し、Collision Tileが不可視であることを即時検証する。
        /// </summary>
        private static void ValidateBuiltAssetsUseAtlas(BuiltAssets assets)
        {
            for (var i = 0; i < assets.GrassTile.Variants.Count; i++)
            {
                EnsureSpriteComesFromAtlas(
                    assets.GrassTile.Variants[i],
                    $"{assets.GrassTile.name}.Variant[{i}]");
            }

            EnsureSpriteComesFromAtlas(assets.StoneVariant.sprite, assets.StoneVariant.name);

            ValidateRuleTileSprites(assets.DirtRuleTile);
            ValidateRuleTileSprites(assets.StoneRuleTile);
            ValidateRuleTileSprites(assets.WaterRuleTile);

            if (assets.CollisionTile.sprite != null)
            {
                throw new InvalidOperationException("CollisionTileは表示Spriteを参照してはいけません。");
            }

            foreach (var pair in assets.VisualPrefabs)
            {
                var renderers = pair.Value.GetComponentsInChildren<SpriteRenderer>(true);
                if (renderers.Length == 0)
                {
                    throw new InvalidOperationException($"表示PrefabにSpriteRendererがありません: {pair.Key}");
                }

                for (var rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
                {
                    EnsureSpriteComesFromAtlas(renderers[rendererIndex].sprite, pair.Value.name);
                }

                if (pair.Value.GetComponentInChildren<Rigidbody2D>(true) != null)
                {
                    throw new InvalidOperationException($"表示PrefabにRigidbody2Dが残っています: {pair.Key}");
                }

                ValidatePrefabCollisionBody(pair.Key, pair.Value);
            }
        }

        /// <summary>
        /// CollisionBody方式だけに正規の子Collider一つを許可し、表示側への物理Component混入を拒否する。
        /// </summary>
        private static void ValidatePrefabCollisionBody(string prefabKey, GameObject prefab)
        {
            var marker = prefab.GetComponent<MapObstacleVisualMarker>();
            var colliders = prefab.GetComponentsInChildren<Collider2D>(true);
            var collisionBody = prefab.transform.Find(MapObstacleVisualMarker.CollisionBodyName);
            if (marker == null || marker.CollisionMode == MapObstacleCollisionMode.TilemapFootprint)
            {
                if (colliders.Length != 0 || collisionBody != null)
                {
                    throw new InvalidOperationException(
                        $"Tilemap Footprint方式のPrefabにCollisionBodyが残っています: {prefabKey}");
                }

                return;
            }

            if (collisionBody == null || colliders.Length != 1 || colliders[0].transform != collisionBody)
            {
                throw new InvalidOperationException(
                    $"CollisionBody方式のPrefabは直下のCollisionBodyへColliderを一つだけ持たせてください: {prefabKey}");
            }

            var collisionLayer = ResolveLayer("MapCollision");
            var collider = colliders[0];
            if (collisionBody.gameObject.layer != collisionLayer
                || collider.isTrigger
                || collisionBody.GetComponent<Renderer>() != null
                || collisionBody.GetComponent<Rigidbody2D>() != null
                || collisionBody.childCount != 0)
            {
                throw new InvalidOperationException(
                    $"CollisionBodyのLayerまたはComponent構成が不正です: {prefabKey}");
            }

            if ((collisionBody.localPosition - new Vector3(
                    marker.CollisionOffset.x,
                    marker.CollisionOffset.y,
                    0f)).sqrMagnitude > 0.000001f
                || Quaternion.Angle(
                    collisionBody.localRotation,
                    Quaternion.Euler(0f, 0f, marker.CollisionRotationDegrees)) > 0.001f
                || (collisionBody.localScale - Vector3.one).sqrMagnitude > 0.000001f
                || collider.offset != Vector2.zero)
            {
                throw new InvalidOperationException(
                    $"CollisionBodyのTransformまたはCollider OffsetがMarker設定と一致しません: {prefabKey}");
            }

            switch (marker.CollisionShape)
            {
                case MapObstacleCollisionShape.Box when collider is BoxCollider2D box:
                    if (box.size != marker.CollisionSize)
                    {
                        throw new InvalidOperationException($"Box Collision Sizeが一致しません: {prefabKey}");
                    }

                    break;
                case MapObstacleCollisionShape.Capsule when collider is CapsuleCollider2D capsule:
                    if (capsule.size != marker.CollisionSize
                        || capsule.direction != marker.CapsuleDirection)
                    {
                        throw new InvalidOperationException($"Capsule Collision設定が一致しません: {prefabKey}");
                    }

                    break;
                default:
                    throw new InvalidOperationException(
                        $"CollisionBodyのCollider型がMarker設定と一致しません: {prefabKey}");
            }
        }

        /// <summary>
        /// RuleTileのDefaultと全Rule Spriteを走査し、旧分割PNG参照が残っていないことを確認する。
        /// </summary>
        private static void ValidateRuleTileSprites(RuleTile ruleTile)
        {
            EnsureSpriteComesFromAtlas(ruleTile.m_DefaultSprite, $"{ruleTile.name}.Default");
            for (var ruleIndex = 0; ruleIndex < ruleTile.m_TilingRules.Count; ruleIndex++)
            {
                var sprites = ruleTile.m_TilingRules[ruleIndex].m_Sprites;
                for (var spriteIndex = 0; spriteIndex < sprites.Length; spriteIndex++)
                {
                    EnsureSpriteComesFromAtlas(
                        sprites[spriteIndex],
                        $"{ruleTile.name}.Rule[{ruleIndex}].Sprite[{spriteIndex}]");
                }
            }
        }

        /// <summary>
        /// SpriteのAsset pathを単一Production Atlasと照合し、参照元の分散を生成時点で止める。
        /// </summary>
        private static void EnsureSpriteComesFromAtlas(Sprite sprite, string context)
        {
            if (sprite == null)
            {
                throw new InvalidOperationException($"Sprite参照がありません: {context}");
            }

            var spritePath = AssetDatabase.GetAssetPath(sprite).Replace('\\', '/');
            if (!string.Equals(spritePath, ProductionAtlasPath, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"単一Production Atlas以外を参照しています: {context} -> {spritePath}");
            }
        }

        /// <summary>
        /// Tile PaletteをGround/Collisionへ分け、自由配置装飾と判定Tileを混在させない。
        /// </summary>
        private static void BuildTilePalettes(BuiltAssets assets)
        {
            CreateOrUpdatePalette(
                "ForestGroundPalette",
                new TileBase[]
                {
                    assets.GrassTile,
                    assets.DirtRuleTile,
                    assets.StoneRuleTile,
                    assets.WaterRuleTile,
                    assets.StoneVariant
                });
            CreateOrUpdatePalette(
                "ForestCollisionPalette",
                new TileBase[]
                {
                    assets.CollisionTile
                });
        }

        /// <summary>
        /// 既存Palette Prefabを再利用し、Tile配置だけを決定的な一列へ更新する。
        /// </summary>
        private static void CreateOrUpdatePalette(string paletteName, IReadOnlyList<TileBase> tiles)
        {
            EnsureFolder(PaletteRoot);
            var palettePath = $"{PaletteRoot}/{paletteName}.prefab";
            var palette = AssetDatabase.LoadAssetAtPath<GameObject>(palettePath);
            if (palette == null)
            {
                palette = GridPaletteUtility.CreateNewPalette(
                    PaletteRoot,
                    paletteName,
                    GridLayout.CellLayout.Rectangle,
                    GridPalette.CellSizing.Manual,
                    Vector3.one,
                    GridLayout.CellSwizzle.XYZ);
            }

            if (palette == null)
            {
                throw new InvalidOperationException($"Tile Paletteを作成できません: {palettePath}");
            }

            var paletteTilemap = palette.GetComponentInChildren<Tilemap>(true);
            if (paletteTilemap == null)
            {
                throw new InvalidOperationException($"Palette内にTilemapがありません: {palettePath}");
            }

            paletteTilemap.ClearAllTiles();
            for (var i = 0; i < tiles.Count; i++)
            {
                paletteTilemap.SetTile(new Vector3Int(i, 0, 0), tiles[i]);
            }

            paletteTilemap.CompressBounds();
            EditorUtility.SetDirty(paletteTilemap);
            EditorUtility.SetDirty(palette);
            PrefabUtility.SavePrefabAsset(palette);
        }

        /// <summary>
        /// 空SceneをUnityの正式なScene Template Assetとして登録し、New Sceneから選択可能にする。
        /// </summary>
        private static void BuildFormalSceneTemplate()
        {
            var sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(
                GbaForestMapAuthoringSceneBuilder.ScenePath);
            if (sceneAsset == null)
            {
                throw new InvalidOperationException(
                    $"Scene Template元Sceneがありません: {GbaForestMapAuthoringSceneBuilder.ScenePath}");
            }

            var template = AssetDatabase.LoadAssetAtPath<SceneTemplateAsset>(SceneTemplatePath);
            if (template == null)
            {
                template = SceneTemplateService.CreateTemplateFromScene(sceneAsset, SceneTemplatePath);
            }

            if (template == null)
            {
                throw new InvalidOperationException($"Scene Templateを作成できません: {SceneTemplatePath}");
            }

            template.templateName = "GBA Forest Map";
            template.description =
                "FantasyRoyaleのGBA風森林マップ用。標準Tilemap階層、描画順、Collision、Socketsを含みます。";
            template.addToDefaults = true;
            EditorUtility.SetDirty(template);
        }

        /// <summary>
        /// 空Templateを複製し、全Layer、RuleTile、Prefab、Socketを目視確認できる小規模Mapを生成する。
        /// </summary>
        private static void BuildSampleScene(BuiltAssets assets)
        {
            var previousActiveScene = SceneManager.GetActiveScene();
            var templateWasLoaded = TryGetLoadedScene(
                GbaForestMapAuthoringSceneBuilder.ScenePath,
                out var templateScene);
            if (!templateWasLoaded)
            {
                templateScene = EditorSceneManager.OpenScene(
                    GbaForestMapAuthoringSceneBuilder.ScenePath,
                    OpenSceneMode.Additive);
            }

            var sampleAssetExists = AssetDatabase.LoadAssetAtPath<SceneAsset>(SampleScenePath) != null;
            var sampleWasLoaded = TryGetLoadedScene(SampleScenePath, out var sampleScene);
            if (!sampleWasLoaded)
            {
                sampleScene = sampleAssetExists
                    ? EditorSceneManager.OpenScene(SampleScenePath, OpenSceneMode.Additive)
                    : EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            }

            try
            {
                // Hierarchy契約が変わった場合も旧Rootを残さないよう、専用SampleはTemplateから再複製する。
                ClearScene(sampleScene);
                var templateRoot = FindRoot(templateScene, "MapRoot");
                var mapRoot = Object.Instantiate(templateRoot);
                mapRoot.name = "MapRoot";
                SceneManager.MoveGameObjectToScene(mapRoot, sampleScene);
                SceneManager.SetActiveScene(sampleScene);

                PopulateSampleMap(mapRoot.transform, assets, sampleScene);
                EditorSceneManager.MarkSceneDirty(sampleScene);
                if (!EditorSceneManager.SaveScene(sampleScene, SampleScenePath))
                {
                    throw new InvalidOperationException($"Sample Sceneを保存できません: {SampleScenePath}");
                }
            }
            finally
            {
                if (previousActiveScene.IsValid() && previousActiveScene.isLoaded)
                {
                    SceneManager.SetActiveScene(previousActiveScene);
                }

                if (!sampleWasLoaded && sampleScene.IsValid() && sampleScene.isLoaded)
                {
                    EditorSceneManager.CloseScene(sampleScene, true);
                }

                if (!templateWasLoaded && templateScene.IsValid() && templateScene.isLoaded)
                {
                    EditorSceneManager.CloseScene(templateScene, true);
                }
            }
        }

        /// <summary>
        /// 30x20の森Mapへ地面、接続地形、境界、装飾、Prefab、Socketを順に配置する。
        /// </summary>
        private static void PopulateSampleMap(Transform mapRoot, BuiltAssets assets, Scene sampleScene)
        {
            var ground = RequireTilemap(mapRoot, "Grid/GroundTilemap");
            var terrain = RequireTilemap(mapRoot, "Grid/TerrainTilemap");
            var collision = RequireTilemap(mapRoot, "Grid/CollisionTilemap");
            var foreground = RequireTilemap(mapRoot, "Grid/ForegroundTilemap");
            var decorationsRoot = RequireChild(mapRoot, "Decorations");
            var obstacleVisualsRoot = RequireChild(mapRoot, "ObstacleVisuals");
            var propsRoot = RequireChild(mapRoot, "Props");

            // 既存Tilemap Componentを再利用してfileIDを保ち、再生成によるScene差分を抑える。
            ground.ClearAllTiles();
            terrain.ClearAllTiles();
            collision.ClearAllTiles();
            foreground.ClearAllTiles();
            RemoveUnexpectedChildren(decorationsRoot);
            RemoveUnexpectedChildren(obstacleVisualsRoot);
            RemoveUnexpectedChildren(propsRoot);

            const int xMin = -15;
            const int xMax = 14;
            const int yMin = -10;
            const int yMax = 9;

            for (var y = yMin; y <= yMax; y++)
            {
                for (var x = xMin; x <= xMax; x++)
                {
                    // Sprite選択はGroundVariationTileが座標Hashから行う。Sceneへは単一Tileだけを保存し、
                    // 16Cell周期の手書きmod式や、装飾入りTileの再混入を避ける。
                    ground.SetTile(new Vector3Int(x, y, 0), assets.GrassTile);
                }
            }

            // 水域を先に論理Cellとして確定し、後から道が上書きされて途切れないようにする。
            var pondRows = new[]
            {
                (Y: 7, MinX: 6, MaxX: 11),
                (Y: 6, MinX: 5, MaxX: 13),
                (Y: 5, MinX: 5, MaxX: 13),
                (Y: 4, MinX: 5, MaxX: 13),
                (Y: 3, MinX: 6, MaxX: 13),
                (Y: 2, MinX: 7, MaxX: 12),
                (Y: 1, MinX: 8, MaxX: 11),
                (Y: 0, MinX: 7, MaxX: 10),
                (Y: -1, MinX: 8, MaxX: 10),
                (Y: -2, MinX: 9, MaxX: 10),
                (Y: -3, MinX: 9, MaxX: 11)
            };
            var islandCells = new HashSet<Vector3Int>
            {
                new Vector3Int(9, 4, 0),
                new Vector3Int(10, 4, 0),
                new Vector3Int(9, 5, 0),
                new Vector3Int(10, 5, 0)
            };
            var waterCells = new HashSet<Vector3Int>();
            for (var rowIndex = 0; rowIndex < pondRows.Length; rowIndex++)
            {
                var row = pondRows[rowIndex];
                for (var x = row.MinX; x <= row.MaxX; x++)
                {
                    var cell = new Vector3Int(x, row.Y, 0);
                    if (!islandCells.Contains(cell))
                    {
                        waterCells.Add(cell);
                    }
                }
            }

            // Waypointから3Cell幅の形状を先に作り、曲がり角・分岐・材質境界を同じ連結集合にする。
            var roadCells = MapRoadPathRasterizer.RasterizePaths(
                new IEnumerable<Vector3Int>[]
                {
                    new[]
                    {
                        new Vector3Int(-14, -7, 0),
                        new Vector3Int(-10, -7, 0),
                        new Vector3Int(-10, -5, 0),
                        new Vector3Int(-6, -5, 0),
                        new Vector3Int(-6, -3, 0),
                        new Vector3Int(1, -3, 0),
                        new Vector3Int(1, 0, 0),
                        new Vector3Int(5, 0, 0)
                    },
                    new[]
                    {
                        new Vector3Int(-2, -3, 0),
                        new Vector3Int(-2, 5, 0)
                    }
                },
                squareBrushRadius: 1);

            // 森の祠前の石畳も道路形状へ統合し、Dirtとの境界を切れ目ではなく材質変更として扱う。
            var stoneCells = new HashSet<Vector3Int>();
            for (var y = 4; y <= 6; y++)
            {
                for (var x = -4; x <= 0; x++)
                {
                    var cell = new Vector3Int(x, y, 0);
                    stoneCells.Add(cell);
                    roadCells.Add(cell);
                }
            }

            if (!MapRoadPathRasterizer.IsSingleCardinalComponent(roadCells))
            {
                throw new InvalidOperationException("Sample道路が4近傍で分断されています。");
            }

            foreach (var cell in roadCells)
            {
                if (cell.x < xMin || cell.x > xMax || cell.y < yMin || cell.y > yMax)
                {
                    throw new InvalidOperationException($"Sample道路がMap範囲外です: {cell}");
                }

                if (waterCells.Contains(cell))
                {
                    throw new InvalidOperationException($"Sample道路と水域が重複しています: {cell}");
                }
            }

            // 形状検証後に材質を割り当てる。両Tileは同じroad接続Groupなので境界でも草縁を閉じない。
            foreach (var cell in roadCells)
            {
                terrain.SetTile(
                    cell,
                    stoneCells.Contains(cell)
                        ? assets.StoneRuleTile
                        : assets.DirtRuleTile);
            }

            foreach (var cell in waterCells)
            {
                terrain.SetTile(cell, assets.WaterRuleTile);
                collision.SetTile(cell, assets.CollisionTile);
            }

            // 装飾は地面Tileへ焼き込まず、1px単位の自由配置Prefabとして余白と水際を整える。
            PlaceVisual(assets, "reeds", "Reeds01", decorationsRoot, sampleScene, new Vector3(4.15625f, 6.0625f, 0f));
            PlaceVisual(assets, "reeds", "Reeds02", decorationsRoot, sampleScene, new Vector3(5.1875f, 1.125f, 0f));
            PlaceVisual(assets, "reeds", "Reeds03", decorationsRoot, sampleScene, new Vector3(6.21875f, -0.90625f, 0f));
            PlaceVisual(assets, "reeds", "Reeds04", decorationsRoot, sampleScene, new Vector3(7.125f, -2.09375f, 0f));
            PlaceVisual(assets, "reeds", "Reeds05", decorationsRoot, sampleScene, new Vector3(8.125f, -3.0625f, 0f));
            PlaceVisual(assets, "reeds", "Reeds06", decorationsRoot, sampleScene, new Vector3(13.15625f, 1.9375f, 0f));
            PlaceVisual(assets, "tall_grass", "TallGrass01", decorationsRoot, sampleScene, new Vector3(-7.84375f, 2.09375f, 0f));
            PlaceVisual(assets, "tall_grass", "TallGrass02", decorationsRoot, sampleScene, new Vector3(2.15625f, 3.0625f, 0f));
            PlaceVisual(assets, "tall_grass", "TallGrass03", decorationsRoot, sampleScene, new Vector3(3.1875f, -5.90625f, 0f));
            PlaceVisual(assets, "tall_grass", "TallGrass04", decorationsRoot, sampleScene, new Vector3(-13.15625f, 0.90625f, 0f));
            PlaceVisual(assets, "flower_patch", "FlowerPatch01", decorationsRoot, sampleScene, new Vector3(-12.15625f, -2.90625f, 0f));
            PlaceVisual(assets, "flower_patch", "FlowerPatch02", decorationsRoot, sampleScene, new Vector3(3.21875f, 4.09375f, 0f));
            PlaceVisual(assets, "flower_patch", "FlowerPatch03", decorationsRoot, sampleScene, new Vector3(3.09375f, -4.875f, 0f));
            PlaceVisual(assets, "wildflowers", "Wildflowers01", decorationsRoot, sampleScene, new Vector3(-5.15625f, 6.09375f, 0f));
            PlaceVisual(assets, "wildflowers", "Wildflowers02", decorationsRoot, sampleScene, new Vector3(4.1875f, 3.125f, 0f));
            PlaceVisual(assets, "wildflowers", "Wildflowers03", decorationsRoot, sampleScene, new Vector3(-0.84375f, -6.90625f, 0f));
            RemoveUnexpectedChildren(
                decorationsRoot,
                "Reeds01",
                "Reeds02",
                "Reeds03",
                "Reeds04",
                "Reeds05",
                "Reeds06",
                "TallGrass01",
                "TallGrass02",
                "TallGrass03",
                "TallGrass04",
                "FlowerPatch01",
                "FlowerPatch02",
                "FlowerPatch03",
                "Wildflowers01",
                "Wildflowers02",
                "Wildflowers03");

            // 大型Stampは互いに重ね、32px境界ではなく輪郭同士で森林と崖を構成する。
            PlaceObstacleVisual(assets, "forest_mass_wide", "ForestWestWide", obstacleVisualsRoot, sampleScene, new Vector3(-11f, 6f, 0f), collision);
            PlaceObstacleVisual(assets, "forest_mass_deep", "ForestWestDeep", obstacleVisualsRoot, sampleScene, new Vector3(-5.96875f, 6f, 0f), collision);
            PlaceObstacleVisual(assets, "forest_front_strip", "ForestNorthStrip", obstacleVisualsRoot, sampleScene, new Vector3(5f, 7f, 0f), collision);
            PlaceObstacleVisual(assets, "forest_mass_deep", "ForestNorthEastDeep", obstacleVisualsRoot, sampleScene, new Vector3(12f, 6f, 0f), collision);
            PlaceObstacleVisual(assets, "cliff_straight_wide", "CliffSouthEastStraight", obstacleVisualsRoot, sampleScene, new Vector3(8f, -5f, 0f), collision);
            PlaceObstacleVisual(assets, "cliff_outer_corner", "CliffSouthEastCorner", obstacleVisualsRoot, sampleScene, new Vector3(13f, -7f, 0f), collision);

            PlaceObstacleVisual(assets, "tree_medium_01", "TreeWest01", obstacleVisualsRoot, sampleScene, new Vector3(-13f, 3f, 0f), collision);
            PlaceObstacleVisual(assets, "tree_medium_02", "TreeWest02", obstacleVisualsRoot, sampleScene, new Vector3(-10f, 3f, 0f), collision);
            PlaceObstacleVisual(assets, "tree_medium_03", "TreeCenter03", obstacleVisualsRoot, sampleScene, new Vector3(-7f, 2f, 0f), collision);
            PlaceObstacleVisual(assets, "tree_medium_01", "TreeEast01", obstacleVisualsRoot, sampleScene, new Vector3(13f, 0f, 0f), collision);
            PlaceObstacleVisual(assets, "blocking_bush", "BlockingBush", obstacleVisualsRoot, sampleScene, new Vector3(-5f, 3f, 0f), collision);
            PlaceObstacleVisual(assets, "mossy_rock", "MossyRock", obstacleVisualsRoot, sampleScene, new Vector3(5f, -4f, 0f), collision);
            PlaceObstacleVisual(assets, "stump", "Stump", obstacleVisualsRoot, sampleScene, new Vector3(-4f, -1f, 0f), collision);
            PlaceObstacleVisual(assets, "fallen_log", "FallenLog", obstacleVisualsRoot, sampleScene, new Vector3(1f, -7f, 0f), collision);

            PlaceVisual(assets, "mushroom_patch", "MushroomPatch", propsRoot, sampleScene, new Vector3(9f, 4f, 0f));
            PlaceVisual(assets, "chest", "Chest", propsRoot, sampleScene, new Vector3(-2f, 5f, 0f));
            PlaceVisual(assets, "signpost", "Signpost", propsRoot, sampleScene, new Vector3(-12f, -7f, 0f));
            RemoveUnexpectedChildren(
                obstacleVisualsRoot,
                "ForestWestWide",
                "ForestWestDeep",
                "ForestNorthStrip",
                "ForestNorthEastDeep",
                "CliffSouthEastStraight",
                "CliffSouthEastCorner",
                "TreeWest01",
                "TreeWest02",
                "TreeCenter03",
                "TreeEast01",
                "BlockingBush",
                "MossyRock",
                "Stump",
                "FallenLog");
            RemoveUnexpectedChildren(
                propsRoot,
                "MushroomPatch",
                "Chest",
                "Signpost");

            var socketsRoot = mapRoot.Find("Sockets");
            CreateSocket(socketsRoot, "PlayerStart", new Vector3(-13f, -7f, 0f), MapSocketKind.PlayerStart, "south-west");
            CreateSocket(socketsRoot, "EnemyCamp", new Vector3(3f, -4f, 0f), MapSocketKind.Enemy, "camp-a");
            CreateSocket(socketsRoot, "LootPoint", new Vector3(-2f, 5f, 0f), MapSocketKind.Loot, "chest-a");
            RemoveUnexpectedChildren(socketsRoot, "PlayerStart", "EnemyCamp", "LootPoint");

            var camera = mapRoot.GetComponentInChildren<Camera>(true);
            if (camera != null)
            {
                camera.transform.position = new Vector3(-0.5f, -0.25f, -10f);
                camera.transform.rotation = Quaternion.identity;
                camera.orthographicSize = 10.6f;
                camera.orthographic = true;
                camera.nearClipPlane = 0.1f;
                camera.farClipPlane = 100f;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color32(20, 32, 28, 255);
                camera.transparencySortMode = TransparencySortMode.CustomAxis;
                camera.transparencySortAxis = Vector3.up;
            }

            ground.CompressBounds();
            terrain.CompressBounds();
            collision.CompressBounds();
            foreground.CompressBounds();
        }

        /// <summary>
        /// Prefab接続を保った表示InstanceをSample Sceneの指定Rootへ配置する。
        /// </summary>
        private static GameObject PlaceVisual(
            BuiltAssets assets,
            string prefabName,
            string instanceName,
            Transform parent,
            Scene scene,
            Vector3 position)
        {
            var prefab = assets.VisualPrefabs[prefabName];
            var instanceTransform = FindDirectChild(parent, instanceName);
            var instance = instanceTransform != null ? instanceTransform.gameObject : null;
            if (instance != null && PrefabUtility.GetCorrespondingObjectFromSource(instance) != prefab)
            {
                Object.DestroyImmediate(instance);
                instance = null;
            }

            if (instance == null)
            {
                instance = PrefabUtility.InstantiatePrefab(prefab, scene) as GameObject;
            }

            if (instance == null)
            {
                throw new InvalidOperationException($"Prop Prefabを配置できません: {prefabName} / {instanceName}");
            }

            instance.name = instanceName;
            instance.transform.SetParent(parent, true);
            instance.transform.position = position;
            instance.transform.rotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one;
            return instance;
        }

        /// <summary>
        /// 障害物表示を配置し、Tilemap Footprint方式だけを不可視CollisionTilemapへ焼き込む。
        /// </summary>
        private static void PlaceObstacleVisual(
            BuiltAssets assets,
            string prefabName,
            string instanceName,
            Transform parent,
            Scene scene,
            Vector3 position,
            Tilemap collision)
        {
            var instance = PlaceVisual(assets, prefabName, instanceName, parent, scene, position);
            var marker = instance.GetComponent<MapObstacleVisualMarker>();
            if (marker == null)
            {
                throw new InvalidOperationException($"障害物表示にFootprint Markerがありません: {prefabName}");
            }

            if (marker.CollisionMode != MapObstacleCollisionMode.TilemapFootprint)
            {
                return;
            }

            // 崖など連続地形だけは、表示の1px自由配置から最寄りの論理Cellへ判定を保存する。
            var anchorCell = new Vector3Int(
                Mathf.RoundToInt(position.x),
                Mathf.RoundToInt(position.y),
                0);
            foreach (var cell in marker.GetFootprintCells(anchorCell))
            {
                collision.SetTile(cell, assets.CollisionTile);
            }
        }

        /// <summary>
        /// ゲーム実装へ依存しない制作MarkerをSockets Root配下へ生成する。
        /// </summary>
        private static void CreateSocket(
            Transform parent,
            string name,
            Vector3 position,
            MapSocketKind kind,
            string tag)
        {
            var existing = FindDirectChild(parent, name);
            var socketObject = existing != null ? existing.gameObject : new GameObject(name);
            socketObject.transform.SetParent(parent, false);
            socketObject.transform.position = position;
            socketObject.transform.rotation = Quaternion.identity;
            socketObject.transform.localScale = Vector3.one;
            socketObject.layer = ResolveLayer("MapInteraction");
            var marker = socketObject.GetComponent<MapSocketMarker>();
            if (marker == null)
            {
                marker = socketObject.AddComponent<MapSocketMarker>();
            }

            marker.Configure(
                kind,
                new Vector2(0.8f, 0.8f),
                MapSocketFacing.Any,
                tag,
                name);
        }

        /// <summary>
        /// 生成SampleをValidatorへ通し、構造やImport設定にErrorが残る場合はbuildを失敗させる。
        /// </summary>
        private static void ValidateGeneratedSample()
        {
            var wasLoaded = TryGetLoadedScene(SampleScenePath, out var sampleScene);
            if (!wasLoaded)
            {
                sampleScene = EditorSceneManager.OpenScene(SampleScenePath, OpenSceneMode.Additive);
            }

            try
            {
                // 完成Kitの再生成ではScene構造だけでなく、全Production SpriteのImport契約も同時に確認する。
                var report = MapAuthoringValidator.ValidateScene(sampleScene, true);
                if (!report.HasErrors)
                {
                    return;
                }

                var errorCodes = new List<string>();
                for (var i = 0; i < report.Issues.Count; i++)
                {
                    if (report.Issues[i].Severity == MapAuthoringIssueSeverity.Error)
                    {
                        errorCodes.Add(report.Issues[i].Code);
                    }
                }

                throw new InvalidOperationException(
                    $"生成SampleのMap Authoring検証に失敗しました: {string.Join(", ", errorCodes)}");
            }
            finally
            {
                if (!wasLoaded && sampleScene.IsValid() && sampleScene.isLoaded)
                {
                    EditorSceneManager.CloseScene(sampleScene, true);
                }
            }
        }

        /// <summary>
        /// Unity CameraからSample PNGを生成する。描画Deviceがない環境ではKit buildを止めず警告だけを残す。
        /// </summary>
        private static void TryCaptureSamplePreview()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
            {
                Debug.Log("Null Graphics DeviceのためSample preview取得を省略しました。");
                return;
            }

            try
            {
                CaptureSamplePreview();
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Sample previewの取得を省略しました: {exception.Message}");
            }
        }

        /// <summary>
        /// Sample Sceneを960x640で描画し、Unity Import後の見た目をQA画像として保存する。
        /// </summary>
        [MenuItem("FantasyRoyale/Map Authoring/Capture Sample Preview")]
        public static void CaptureSamplePreview()
        {
            var wasLoaded = TryGetLoadedScene(SampleScenePath, out var sampleScene);
            if (!wasLoaded)
            {
                sampleScene = EditorSceneManager.OpenScene(SampleScenePath, OpenSceneMode.Additive);
            }

            RenderTexture renderTexture = null;
            Texture2D capture = null;
            var previousActive = RenderTexture.active;
            try
            {
                var root = FindRoot(sampleScene, "MapRoot");
                var camera = root.GetComponentInChildren<Camera>(true);
                if (camera == null)
                {
                    throw new InvalidOperationException("Sample SceneにCameraがありません。");
                }

                renderTexture = new RenderTexture(960, 640, 24, RenderTextureFormat.ARGB32)
                {
                    filterMode = FilterMode.Point
                };
                renderTexture.Create();
                if (!renderTexture.IsCreated())
                {
                    throw new InvalidOperationException("Sample preview用RenderTextureを作成できません。");
                }

                var tilemaps = root.GetComponentsInChildren<Tilemap>(true);
                for (var i = 0; i < tilemaps.Length; i++)
                {
                    tilemaps[i].RefreshAllTiles();
                }

                camera.targetTexture = renderTexture;
                // TilemapのChunk geometryは最初のRenderで準備される場合があるため、読み戻し前に二度描画する。
                camera.Render();
                camera.Render();
                RenderTexture.active = renderTexture;
                capture = new Texture2D(960, 640, TextureFormat.RGBA32, false);
                capture.ReadPixels(new Rect(0, 0, 960, 640), 0, 0);
                capture.Apply(false, false);

                var projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
                if (string.IsNullOrEmpty(projectRoot))
                {
                    throw new InvalidOperationException("Project Rootを取得できません。");
                }

                var absolutePath = Path.Combine(projectRoot, SamplePreviewPath.Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(Path.GetDirectoryName(absolutePath) ?? projectRoot);
                File.WriteAllBytes(absolutePath, capture.EncodeToPNG());
                AssetDatabase.ImportAsset(
                    SamplePreviewPath,
                    ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
            }
            finally
            {
                var root = sampleScene.IsValid() && sampleScene.isLoaded
                    ? FindRoot(sampleScene, "MapRoot")
                    : null;
                var camera = root != null ? root.GetComponentInChildren<Camera>(true) : null;
                if (camera != null)
                {
                    camera.targetTexture = null;
                }

                RenderTexture.active = previousActive;
                if (capture != null)
                {
                    Object.DestroyImmediate(capture);
                }

                if (renderTexture != null)
                {
                    renderTexture.Release();
                    Object.DestroyImmediate(renderTexture);
                }

                if (!wasLoaded && sampleScene.IsValid() && sampleScene.isLoaded)
                {
                    EditorSceneManager.CloseScene(sampleScene, true);
                }
            }
        }

        /// <summary>
        /// 保存済みSceneが既に開かれているかをpathで判定する。
        /// </summary>
        private static bool TryGetLoadedScene(string path, out Scene scene)
        {
            scene = SceneManager.GetSceneByPath(path);
            return scene.IsValid() && scene.isLoaded;
        }

        /// <summary>
        /// 指定Sceneの全Rootを即時破棄し、生成物が再実行ごとに積み重ならないようにする。
        /// </summary>
        private static void ClearScene(Scene scene)
        {
            var roots = scene.GetRootGameObjects();
            for (var i = roots.Length - 1; i >= 0; i--)
            {
                Object.DestroyImmediate(roots[i]);
            }
        }

        /// <summary>
        /// 親直下から最初の同名Objectだけを取得し、深い階層の偶然の同名Objectを拾わない。
        /// </summary>
        private static Transform FindDirectChild(Transform parent, string childName)
        {
            for (var i = 0; i < parent.childCount; i++)
            {
                var child = parent.GetChild(i);
                if (string.Equals(child.name, childName, StringComparison.Ordinal))
                {
                    return child;
                }
            }

            return null;
        }

        /// <summary>
        /// Sample生成対象外の子と重複名を除去し、既存の正しいInstanceはfileID維持のため再利用する。
        /// </summary>
        private static void RemoveUnexpectedChildren(Transform parent, params string[] allowedNames)
        {
            var allowed = new HashSet<string>(allowedNames, StringComparer.Ordinal);
            var retained = new HashSet<string>(StringComparer.Ordinal);
            var removals = new List<GameObject>();
            for (var i = 0; i < parent.childCount; i++)
            {
                var child = parent.GetChild(i).gameObject;
                if (!allowed.Contains(child.name) || !retained.Add(child.name))
                {
                    removals.Add(child);
                }
            }

            for (var i = removals.Count - 1; i >= 0; i--)
            {
                Object.DestroyImmediate(removals[i]);
            }
        }

        /// <summary>
        /// Scene直下の必須Rootを一意に取得する。
        /// </summary>
        private static GameObject FindRoot(Scene scene, string name)
        {
            var roots = scene.GetRootGameObjects();
            for (var i = 0; i < roots.Length; i++)
            {
                if (string.Equals(roots[i].name, name, StringComparison.Ordinal))
                {
                    return roots[i];
                }
            }

            throw new InvalidOperationException($"{scene.path} に{name}がありません。");
        }

        /// <summary>
        /// MapRootから相対pathで必須の表示Rootを取得する。
        /// </summary>
        private static Transform RequireChild(Transform mapRoot, string relativePath)
        {
            var target = mapRoot.Find(relativePath);
            if (target == null)
            {
                throw new InvalidOperationException($"表示Rootがありません: {relativePath}");
            }

            return target;
        }

        /// <summary>
        /// MapRootから相対pathで必須Tilemapを取得する。
        /// </summary>
        private static Tilemap RequireTilemap(Transform mapRoot, string relativePath)
        {
            var target = mapRoot.Find(relativePath);
            var tilemap = target != null ? target.GetComponent<Tilemap>() : null;
            if (tilemap == null)
            {
                throw new InvalidOperationException($"Tilemapがありません: {relativePath}");
            }

            return tilemap;
        }

        /// <summary>
        /// 単一Atlas内の全Sub-Spriteを名前で索引化し、重複や欠落を参照設定前に検出する。
        /// </summary>
        private static Sprite RequireAtlasSprite(string spriteName)
        {
            if (atlasSpritesByName == null)
            {
                atlasSpritesByName = new Dictionary<string, Sprite>(StringComparer.Ordinal);
                var atlasAssets = AssetDatabase.LoadAllAssetsAtPath(ProductionAtlasPath);
                for (var i = 0; i < atlasAssets.Length; i++)
                {
                    if (!(atlasAssets[i] is Sprite sprite))
                    {
                        continue;
                    }

                    if (atlasSpritesByName.ContainsKey(sprite.name))
                    {
                        throw new InvalidOperationException($"Atlas内のSprite名が重複しています: {sprite.name}");
                    }

                    atlasSpritesByName.Add(sprite.name, sprite);
                }
            }

            if (!atlasSpritesByName.TryGetValue(spriteName, out var result))
            {
                throw new InvalidOperationException(
                    $"Atlas Spriteを読み込めません: {spriteName} ({ProductionAtlasPath})");
            }

            return result;
        }

        /// <summary>
        /// 道路の全Maskについて、接続口を共有する4枚の輪郭・質感差分を返す。
        /// </summary>
        private static Sprite[] RequireRoadVariationSprites(string spritePrefix, int mask)
        {
            var baseName = $"{spritePrefix}_{mask:x2}";
            return new[]
            {
                RequireAtlasSprite(baseName),
                RequireAtlasSprite($"{baseName}_v02"),
                RequireAtlasSprite($"{baseName}_v03"),
                RequireAtlasSprite($"{baseName}_v04")
            };
        }

        /// <summary>
        /// 水面は頻出する全面Maskだけ4枚の質感差分を返し、境界Maskは単一Spriteへ固定する。
        /// </summary>
        private static Sprite[] RequireSurfaceVariationSprites(
            string spritePrefix,
            int mask,
            int fullSurfaceMask)
        {
            var baseName = $"{spritePrefix}_{mask:x2}";
            if (mask != fullSurfaceMask)
            {
                return new[] { RequireAtlasSprite(baseName) };
            }

            return new[]
            {
                RequireAtlasSprite(baseName),
                RequireAtlasSprite($"{baseName}_v02"),
                RequireAtlasSprite($"{baseName}_v03"),
                RequireAtlasSprite($"{baseName}_v04")
            };
        }

        /// <summary>
        /// Project Layerが未登録でも生成処理を継続できるようDefaultへフォールバックする。
        /// </summary>
        private static int ResolveLayer(string layerName)
        {
            var layer = LayerMask.NameToLayer(layerName);
            return layer >= 0 ? layer : 0;
        }

        /// <summary>
        /// Sorting Layerが未登録の場合だけDefaultを返す。
        /// </summary>
        private static string ResolveSortingLayer(string layerName)
        {
            var layers = SortingLayer.layers;
            for (var i = 0; i < layers.Length; i++)
            {
                if (string.Equals(layers[i].name, layerName, StringComparison.Ordinal))
                {
                    return layerName;
                }
            }

            return "Default";
        }

        /// <summary>
        /// snake_caseの素材名をAsset表示用PascalCaseへ変換する。
        /// </summary>
        private static string ToPascalCase(string value)
        {
            var result = string.Empty;
            var capitalize = true;
            for (var i = 0; i < value.Length; i++)
            {
                var character = value[i];
                if (character == '_')
                {
                    capitalize = true;
                    continue;
                }

                result += capitalize ? char.ToUpperInvariant(character) : character;
                capitalize = false;
            }

            return result;
        }

        /// <summary>
        /// AssetDatabase用Folderを親から再帰作成する。
        /// </summary>
        private static void EnsureFolder(string assetFolderPath)
        {
            if (string.IsNullOrWhiteSpace(assetFolderPath) || AssetDatabase.IsValidFolder(assetFolderPath))
            {
                return;
            }

            var separatorIndex = assetFolderPath.LastIndexOf('/');
            if (separatorIndex <= 0)
            {
                throw new ArgumentException($"Invalid asset folder path: {assetFolderPath}", nameof(assetFolderPath));
            }

            var parentPath = assetFolderPath.Substring(0, separatorIndex);
            var folderName = assetFolderPath.Substring(separatorIndex + 1);
            EnsureFolder(parentPath);
            AssetDatabase.CreateFolder(parentPath, folderName);
        }
    }
}
