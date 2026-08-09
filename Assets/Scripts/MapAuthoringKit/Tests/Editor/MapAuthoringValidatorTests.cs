using System;
using System.Collections.Generic;
using System.Reflection;
using FantasyRoyale.MapAuthoringKit;
using FantasyRoyale.MapAuthoringKit.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using Object = UnityEngine.Object;

namespace FantasyRoyale.MapAuthoringKit.Tests.Editor
{
    /// <summary>
    /// Map Authoring Kitの標準階層とSocket幾何判定が、完成Sceneの設定漏れを検出できることを確認する。
    /// </summary>
    public sealed class MapAuthoringValidatorTests
    {
        private sealed class SceneFixture
        {
            public GameObject MapRoot;
            public Transform GridRoot;
            public Transform DecorationsRoot;
            public Transform ObstacleVisualsRoot;
            public Transform PropsRoot;
            public Transform SocketsRoot;
            public Tilemap GroundTilemap;
            public Tilemap CollisionTilemap;
        }

        private Scene previewScene;
        private readonly List<Object> transientAssets = new List<Object>();

        /// <summary>
        /// 各Testを保存対象外のPreview Sceneで隔離して実行する。
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            previewScene = EditorSceneManager.NewPreviewScene();
        }

        /// <summary>
        /// Testで生成したTileとPreview Sceneを破棄し、Projectや開いているSceneへ状態を残さない。
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            for (var i = transientAssets.Count - 1; i >= 0; i--)
            {
                if (transientAssets[i] != null)
                {
                    Object.DestroyImmediate(transientAssets[i]);
                }
            }

            transientAssets.Clear();
            if (previewScene.IsValid())
            {
                EditorSceneManager.ClosePreviewScene(previewScene);
            }
        }

        /// <summary>
        /// 必須階層とCollider設定を満たすSceneが構造Errorを出さないことを確認する。
        /// </summary>
        [Test]
        public void ValidateScene_WithCompleteAuthoringHierarchy_HasNoStructuralErrors()
        {
            CreateCompleteScene();

            var report = MapAuthoringValidator.ValidateScene(previewScene, false);

            Assert.That(report.Issues, Has.None.Matches<MapAuthoringValidationIssue>(issue => IsStructuralOrSocketIssue(issue.Code)));
        }

        /// <summary>
        /// 必須Tilemapが一つ欠けた場合に、名前を含む欠落Errorが返ることを確認する。
        /// </summary>
        [Test]
        public void ValidateScene_WhenRequiredTilemapIsMissing_ReportsError()
        {
            var fixture = CreateCompleteScene();
            Object.DestroyImmediate(fixture.GridRoot.Find("TerrainTilemap").gameObject);

            var report = MapAuthoringValidator.ValidateScene(previewScene, false);

            Assert.That(report.Issues, Has.Some.Matches<MapAuthoringValidationIssue>(issue =>
                issue.Code == "TILEMAP_MISSING" && issue.Message.Contains("TerrainTilemap")));
        }

        /// <summary>
        /// 自由配置装飾の標準Rootが欠けた場合に、専用の構造Errorが返ることを確認する。
        /// </summary>
        [Test]
        public void ValidateScene_WhenDecorationsRootIsMissing_ReportsError()
        {
            var fixture = CreateCompleteScene();
            Object.DestroyImmediate(fixture.DecorationsRoot.gameObject);

            var report = MapAuthoringValidator.ValidateScene(previewScene, false);

            Assert.That(report.Issues, Has.Some.Matches<MapAuthoringValidationIssue>(issue =>
                issue.Code == "DECORATIONS_ROOT_MISSING"));
        }

        /// <summary>
        /// 三つの表示専用Rootへ物理Componentや物理用途Layerが混入した場合に、責務ごとのErrorを返すことを確認する。
        /// </summary>
        [TestCase("Decorations")]
        [TestCase("ObstacleVisuals")]
        [TestCase("Props")]
        public void ValidateScene_WhenDisplayObjectOwnsPhysicsResponsibilities_ReportsEachError(string rootName)
        {
            var fixture = CreateCompleteScene();
            var invalidVisual = new GameObject("InvalidVisual", typeof(BoxCollider2D), typeof(Rigidbody2D));
            invalidVisual.transform.SetParent(fixture.MapRoot.transform.Find(rootName), false);
            var nonDefaultLayer = LayerMask.NameToLayer("Ignore Raycast");
            Assert.That(nonDefaultLayer, Is.GreaterThanOrEqualTo(0));
            invalidVisual.layer = nonDefaultLayer;

            var report = MapAuthoringValidator.ValidateScene(previewScene, false);

            Assert.That(report.Issues, Has.Some.Matches<MapAuthoringValidationIssue>(issue =>
                issue.Code == "VISUAL_COLLIDER_FORBIDDEN"));
            Assert.That(report.Issues, Has.Some.Matches<MapAuthoringValidationIssue>(issue =>
                issue.Code == "VISUAL_RIGIDBODY_FORBIDDEN"));
            Assert.That(report.Issues, Has.Some.Matches<MapAuthoringValidationIssue>(issue =>
                issue.Code == "VISUAL_LAYER_INVALID"));
        }

        /// <summary>
        /// 自由配置RootへGrid系Componentが混入した場合に、Component種別ごとのErrorが返ることを確認する。
        /// </summary>
        [Test]
        public void ValidateScene_WhenDecorationsContainGridComponents_ReportsEachError()
        {
            var fixture = CreateCompleteScene();
            var invalidGrid = new GameObject("InvalidGrid", typeof(Grid));
            invalidGrid.transform.SetParent(fixture.DecorationsRoot, false);
            var invalidTilemap = new GameObject("InvalidTilemap", typeof(Tilemap), typeof(TilemapRenderer));
            invalidTilemap.transform.SetParent(fixture.DecorationsRoot, false);

            var report = MapAuthoringValidator.ValidateScene(previewScene, false);

            Assert.That(report.Issues, Has.Some.Matches<MapAuthoringValidationIssue>(issue =>
                issue.Code == "VISUAL_GRID_FORBIDDEN"));
            Assert.That(report.Issues, Has.Some.Matches<MapAuthoringValidationIssue>(issue =>
                issue.Code == "VISUAL_TILEMAP_FORBIDDEN"));
            Assert.That(report.Issues, Has.Some.Matches<MapAuthoringValidationIssue>(issue =>
                issue.Code == "VISUAL_TILEMAP_RENDERER_FORBIDDEN"));
        }

        /// <summary>
        /// 通行可能な装飾へ障害物Markerを付けた分類違反が専用Errorになることを確認する。
        /// </summary>
        [Test]
        public void ValidateScene_WhenDecorationHasObstacleMarker_ReportsClassificationError()
        {
            var fixture = CreateCompleteScene();
            var decoration = new GameObject("MarkedDecoration");
            decoration.transform.SetParent(fixture.DecorationsRoot, false);
            var marker = decoration.AddComponent<MapObstacleVisualMarker>();

            var report = MapAuthoringValidator.ValidateScene(previewScene, false);

            Assert.That(report.Issues, Has.Some.Matches<MapAuthoringValidationIssue>(issue =>
                issue.Code == "DECORATION_OBSTACLE_MARKER_FORBIDDEN" && issue.Context == marker));
        }

        /// <summary>
        /// 旧表示TilemapがGrid直下へ残った場合に、移行漏れErrorとして検出されることを確認する。
        /// </summary>
        [TestCase("DetailTilemap")]
        [TestCase("ObstacleTilemap")]
        public void ValidateScene_WhenLegacyTilemapRemains_ReportsError(string tilemapName)
        {
            var fixture = CreateCompleteScene();
            var legacyTilemap = CreateTilemap(fixture.GridRoot, tilemapName, "MapDetail", 0, false);

            var report = MapAuthoringValidator.ValidateScene(previewScene, false);

            Assert.That(report.Issues, Has.Some.Matches<MapAuthoringValidationIssue>(issue =>
                issue.Code == "LEGACY_TILEMAP_FORBIDDEN"
                && issue.Context == legacyTilemap.gameObject
                && issue.Message.Contains(tilemapName)));
        }

        /// <summary>
        /// 三つの表示専用Rootで、直下InstanceのPixel Snap、回転、Scale違反が独立したErrorになることを確認する。
        /// </summary>
        [TestCase("Decorations")]
        [TestCase("ObstacleVisuals")]
        [TestCase("Props")]
        public void ValidateScene_WhenDisplayTransformsBreakContract_ReportsEachError(string rootName)
        {
            var fixture = CreateCompleteScene();
            var displayRoot = fixture.MapRoot.transform.Find(rootName);
            var positionInvalid = CreateVisibleObstacle(displayRoot, "PositionInvalid");
            positionInvalid.transform.position = new Vector3(1f / 64f, 0f, 0f);
            var rotationInvalid = CreateVisibleObstacle(displayRoot, "RotationInvalid");
            rotationInvalid.transform.rotation = Quaternion.Euler(0f, 0f, 10f);
            var scaleInvalid = CreateVisibleObstacle(displayRoot, "ScaleInvalid");
            scaleInvalid.transform.localScale = new Vector3(1.25f, 1f, 1f);

            var report = MapAuthoringValidator.ValidateScene(previewScene, false);

            Assert.That(report.Issues, Has.Some.Matches<MapAuthoringValidationIssue>(issue =>
                issue.Code == "VISUAL_POSITION_NOT_PIXEL_SNAPPED" && issue.Context == positionInvalid));
            Assert.That(report.Issues, Has.Some.Matches<MapAuthoringValidationIssue>(issue =>
                issue.Code == "VISUAL_ROTATION_INVALID" && issue.Context == rotationInvalid));
            Assert.That(report.Issues, Has.Some.Matches<MapAuthoringValidationIssue>(issue =>
                issue.Code == "VISUAL_SCALE_INVALID" && issue.Context == scaleInvalid));
        }

        /// <summary>
        /// 自由配置Spriteの固定Layer、Order、中心Sortが足元Y契約違反として検出されることを確認する。
        /// </summary>
        [TestCase("Decorations")]
        [TestCase("ObstacleVisuals")]
        [TestCase("Props")]
        public void ValidateScene_WhenWorldObjectSortingBreaksContract_ReportsEachError(
            string rootName)
        {
            var fixture = CreateCompleteScene();
            var displayRoot = fixture.MapRoot.transform.Find(rootName);
            var visual = CreateVisibleObstacle(displayRoot, "WorldYInvalid");
            var renderer = visual.GetComponent<SpriteRenderer>();
            renderer.sortingLayerName = "MapGround";
            renderer.sortingOrder = 12;
            renderer.spriteSortPoint = SpriteSortPoint.Center;

            var report = MapAuthoringValidator.ValidateScene(previewScene, false);

            Assert.That(report.Issues, Has.Some.Matches<MapAuthoringValidationIssue>(issue =>
                issue.Code == "VISUAL_SORTING_LAYER_INVALID" && issue.Context == renderer));
            Assert.That(report.Issues, Has.Some.Matches<MapAuthoringValidationIssue>(issue =>
                issue.Code == "VISUAL_SORTING_ORDER_INVALID" && issue.Context == renderer));
            Assert.That(report.Issues, Has.Some.Matches<MapAuthoringValidationIssue>(issue =>
                issue.Code == "VISUAL_SORT_POINT_INVALID" && issue.Context == renderer));

            renderer.sortingLayerName = "WorldObjects";
            renderer.sortingOrder = 0;
            renderer.spriteSortPoint = SpriteSortPoint.Pivot;
            var correctedReport = MapAuthoringValidator.ValidateScene(previewScene, false);
            Assert.That(correctedReport.Issues, Has.None.Matches<MapAuthoringValidationIssue>(issue =>
                issue.Context == renderer && issue.Code.StartsWith("VISUAL_SORT", StringComparison.Ordinal)));
        }

        /// <summary>
        /// 可視障害物に制作Markerがない場合だけErrorとし、Marker追加後は解消することを確認する。
        /// </summary>
        [Test]
        public void ValidateScene_WhenVisibleObstacleHasNoMarker_ReportsUntilMarkerIsAdded()
        {
            var fixture = CreateCompleteScene();
            var obstacle = CreateVisibleObstacle(fixture.ObstacleVisualsRoot, "ForestMass");

            var missingReport = MapAuthoringValidator.ValidateScene(previewScene, false);
            Assert.That(missingReport.Issues, Has.Some.Matches<MapAuthoringValidationIssue>(issue =>
                issue.Code == "OBSTACLE_VISUAL_MARKER_MISSING" && issue.Context == obstacle));

            obstacle.AddComponent<MapObstacleVisualMarker>();
            fixture.CollisionTilemap.SetTile(Vector3Int.zero, CreateTransientTile());
            var validReport = MapAuthoringValidator.ValidateScene(previewScene, false);
            Assert.That(validReport.Issues, Has.None.Matches<MapAuthoringValidationIssue>(issue =>
                issue.Code == "OBSTACLE_VISUAL_MARKER_MISSING"));
        }

        /// <summary>
        /// MarkerのFootprintに一つでもCollision Tileがなければ、対象Instanceの不足Errorになることを確認する。
        /// </summary>
        [Test]
        public void ValidateScene_WhenObstacleFootprintLacksCollision_ReportsError()
        {
            var fixture = CreateCompleteScene();
            var obstacle = CreateMarkedObstacle(
                fixture.ObstacleVisualsRoot,
                "ForestMassMissingCollision",
                new Vector3(2f, 3f, 0f),
                new Vector2Int(2, 2),
                new Vector2Int(-1, -1));

            var report = MapAuthoringValidator.ValidateScene(previewScene, false);

            Assert.That(report.Issues, Has.Some.Matches<MapAuthoringValidationIssue>(issue =>
                issue.Code == "OBSTACLE_COLLISION_MISSING" && issue.Context == obstacle));
        }

        /// <summary>
        /// 1Pixelずらした表示でも最寄りAnchorからFootprintを求め、全CellにCollisionがあればErrorにならないことを確認する。
        /// </summary>
        [Test]
        public void ValidateScene_WhenObstacleFootprintHasCollision_HasNoError()
        {
            var fixture = CreateCompleteScene();
            var obstacle = CreateMarkedObstacle(
                fixture.ObstacleVisualsRoot,
                "ForestMassWithCollision",
                new Vector3(2f + 1f / 32f, 3f, 0f),
                new Vector2Int(2, 1),
                new Vector2Int(-1, 0));
            var marker = obstacle.GetComponent<MapObstacleVisualMarker>();
            var collisionTile = CreateTransientTile();
            foreach (var cell in marker.GetFootprintCells(new Vector3Int(2, 3, 0)))
            {
                fixture.CollisionTilemap.SetTile(cell, collisionTile);
            }

            var report = MapAuthoringValidator.ValidateScene(previewScene, false);

            Assert.That(report.Issues, Has.None.Matches<MapAuthoringValidationIssue>(issue =>
                issue.Code == "OBSTACLE_COLLISION_MISSING" && issue.Context == obstacle));
            Assert.That(report.Issues, Has.None.Matches<MapAuthoringValidationIssue>(issue =>
                issue.Code.StartsWith("VISUAL_", StringComparison.Ordinal) && issue.Context == obstacle));
        }

        /// <summary>
        /// 精密CollisionBodyがMarkerの足元形状と一致する場合、Tilemap Cellなしでも構造Errorにならないことを確認する。
        /// </summary>
        [Test]
        public void ValidateScene_WhenPreciseCollisionBodyMatchesMarker_HasNoCollisionErrors()
        {
            var fixture = CreateCompleteScene();
            var obstacle = CreateCollisionBodyObstacle(
                fixture.ObstacleVisualsRoot,
                "TreeWithPreciseBody",
                MapObstacleCollisionShape.Box,
                new Vector2(0.5f, 0.375f),
                new Vector2(0f, 0.1875f),
                0f,
                CapsuleDirection2D.Horizontal);

            var report = MapAuthoringValidator.ValidateScene(previewScene, false);

            Assert.That(report.Issues, Has.None.Matches<MapAuthoringValidationIssue>(issue =>
                issue.Code.StartsWith("OBSTACLE_COLLISION_BODY", StringComparison.Ordinal)
                && IsContextInHierarchy(issue.Context, obstacle.transform)));
            Assert.That(report.Issues, Has.None.Matches<MapAuthoringValidationIssue>(issue =>
                (issue.Code == "VISUAL_COLLIDER_FORBIDDEN" || issue.Code == "VISUAL_LAYER_INVALID")
                && IsContextInHierarchy(issue.Context, obstacle.transform)));
            Assert.That(report.Issues, Has.None.Matches<MapAuthoringValidationIssue>(issue =>
                issue.Code == "OBSTACLE_COLLISION_MISSING" && issue.Context == obstacle));
        }

        /// <summary>
        /// CollisionBody ModeのMarkerにBodyがなければ、Tilemap判定へ暗黙Fallbackせず欠落Errorにする。
        /// </summary>
        [Test]
        public void ValidateScene_WhenPreciseCollisionBodyIsMissing_ReportsError()
        {
            var fixture = CreateCompleteScene();
            var obstacle = CreateVisibleObstacle(fixture.ObstacleVisualsRoot, "TreeMissingPreciseBody");
            var marker = obstacle.AddComponent<MapObstacleVisualMarker>();
            marker.Configure(
                Vector2Int.one,
                Vector2Int.zero,
                MapObstacleCollisionMode.CollisionBody,
                MapObstacleCollisionShape.Box,
                new Vector2(0.5f, 0.375f),
                new Vector2(0f, 0.1875f));

            var report = MapAuthoringValidator.ValidateScene(previewScene, false);

            Assert.That(report.Issues, Has.Some.Matches<MapAuthoringValidationIssue>(issue =>
                issue.Code == "OBSTACLE_COLLISION_BODY_MISSING" && issue.Context == obstacle));
        }

        /// <summary>
        /// CollisionBodyのLayer、Transform、物理・表示Component、Collider設定がMarker契約から外れた場合を一括検出する。
        /// </summary>
        [Test]
        public void ValidateScene_WhenPreciseCollisionBodyBreaksContract_ReportsEachError()
        {
            var fixture = CreateCompleteScene();
            var obstacle = CreateCollisionBodyObstacle(
                fixture.ObstacleVisualsRoot,
                "TreeWithInvalidPreciseBody",
                MapObstacleCollisionShape.Box,
                new Vector2(0.5f, 0.375f),
                new Vector2(0f, 0.1875f),
                0f,
                CapsuleDirection2D.Horizontal);
            var body = obstacle.transform.Find(MapObstacleVisualMarker.CollisionBodyName);
            var collider = body.GetComponent<BoxCollider2D>();
            body.gameObject.layer = LayerMask.NameToLayer("Default");
            body.localPosition += new Vector3(0.125f, 0f, 0f);
            body.localRotation = Quaternion.Euler(0f, 0f, 10f);
            body.localScale = new Vector3(1.25f, 1f, 1f);
            body.gameObject.AddComponent<SpriteRenderer>();
            body.gameObject.AddComponent<Rigidbody2D>();
            collider.enabled = false;
            collider.isTrigger = true;
            collider.size = new Vector2(0.75f, 0.5f);

            var report = MapAuthoringValidator.ValidateScene(previewScene, false);
            var codes = new HashSet<string>();
            for (var issueIndex = 0; issueIndex < report.Issues.Count; issueIndex++)
            {
                if (IsContextInHierarchy(report.Issues[issueIndex].Context, obstacle.transform))
                {
                    codes.Add(report.Issues[issueIndex].Code);
                }
            }

            Assert.That(codes, Does.Contain("OBSTACLE_COLLISION_BODY_LAYER_INVALID"));
            Assert.That(codes, Does.Contain("OBSTACLE_COLLISION_BODY_POSITION_MISMATCH"));
            Assert.That(codes, Does.Contain("OBSTACLE_COLLISION_BODY_ROTATION_MISMATCH"));
            Assert.That(codes, Does.Contain("OBSTACLE_COLLISION_BODY_SCALE_INVALID"));
            Assert.That(codes, Does.Contain("OBSTACLE_COLLISION_BODY_RENDERER_FORBIDDEN"));
            Assert.That(codes, Does.Contain("OBSTACLE_COLLISION_BODY_RIGIDBODY_FORBIDDEN"));
            Assert.That(codes, Does.Contain("OBSTACLE_COLLISION_BODY_COLLIDER_DISABLED"));
            Assert.That(codes, Does.Contain("OBSTACLE_COLLISION_BODY_TRIGGER_FORBIDDEN"));
            Assert.That(codes, Does.Contain("OBSTACLE_COLLISION_BODY_SIZE_MISMATCH"));
        }

        /// <summary>
        /// Markerと異なるCollider型、Capsule方向、Collider数、重複Bodyを個別に識別して報告できることを確認する。
        /// </summary>
        [Test]
        public void ValidateScene_WhenPreciseCollisionBodyShapeOrMultiplicityIsInvalid_ReportsEachError()
        {
            var fixture = CreateCompleteScene();

            var wrongShape = CreateCollisionBodyObstacle(
                fixture.ObstacleVisualsRoot,
                "WrongShape",
                MapObstacleCollisionShape.Capsule,
                new Vector2(0.5f, 0.75f),
                new Vector2(0f, 0.375f),
                0f,
                CapsuleDirection2D.Vertical);
            var wrongShapeBody = wrongShape.transform.Find(MapObstacleVisualMarker.CollisionBodyName);
            Object.DestroyImmediate(wrongShapeBody.GetComponent<CapsuleCollider2D>());
            wrongShapeBody.gameObject.AddComponent<BoxCollider2D>().size = new Vector2(0.5f, 0.75f);

            var wrongDirection = CreateCollisionBodyObstacle(
                fixture.ObstacleVisualsRoot,
                "WrongCapsuleDirection",
                MapObstacleCollisionShape.Capsule,
                new Vector2(0.5f, 0.75f),
                new Vector2(0f, 0.375f),
                0f,
                CapsuleDirection2D.Vertical);
            wrongDirection.transform.Find(MapObstacleVisualMarker.CollisionBodyName)
                .GetComponent<CapsuleCollider2D>().direction = CapsuleDirection2D.Horizontal;

            var duplicateCollider = CreateCollisionBodyObstacle(
                fixture.ObstacleVisualsRoot,
                "DuplicateCollider",
                MapObstacleCollisionShape.Box,
                new Vector2(0.5f, 0.375f),
                new Vector2(0f, 0.1875f),
                0f,
                CapsuleDirection2D.Horizontal);
            duplicateCollider.transform.Find(MapObstacleVisualMarker.CollisionBodyName)
                .gameObject.AddComponent<CircleCollider2D>();

            var duplicateBody = CreateCollisionBodyObstacle(
                fixture.ObstacleVisualsRoot,
                "DuplicateBody",
                MapObstacleCollisionShape.Box,
                new Vector2(0.5f, 0.375f),
                new Vector2(0f, 0.1875f),
                0f,
                CapsuleDirection2D.Horizontal);
            var extraBody = new GameObject(MapObstacleVisualMarker.CollisionBodyName);
            extraBody.transform.SetParent(duplicateBody.transform, false);

            var report = MapAuthoringValidator.ValidateScene(previewScene, false);

            Assert.That(report.Issues, Has.Some.Matches<MapAuthoringValidationIssue>(issue =>
                issue.Code == "OBSTACLE_COLLISION_BODY_SHAPE_MISMATCH"
                && IsContextInHierarchy(issue.Context, wrongShape.transform)));
            Assert.That(report.Issues, Has.Some.Matches<MapAuthoringValidationIssue>(issue =>
                issue.Code == "OBSTACLE_COLLISION_BODY_CAPSULE_DIRECTION_MISMATCH"
                && IsContextInHierarchy(issue.Context, wrongDirection.transform)));
            Assert.That(report.Issues, Has.Some.Matches<MapAuthoringValidationIssue>(issue =>
                issue.Code == "OBSTACLE_COLLISION_BODY_COLLIDER_COUNT_INVALID"
                && IsContextInHierarchy(issue.Context, duplicateCollider.transform)));
            Assert.That(report.Issues, Has.Some.Matches<MapAuthoringValidationIssue>(issue =>
                issue.Code == "OBSTACLE_COLLISION_BODY_COUNT_INVALID"
                && IsContextInHierarchy(issue.Context, duplicateBody.transform)));
        }

        /// <summary>
        /// TilemapFootprint ModeへCollisionBodyやColliderを混入させた場合は、従来の分離契約違反として報告する。
        /// </summary>
        [Test]
        public void ValidateScene_WhenTilemapFootprintOwnsCollisionBody_ReportsError()
        {
            var fixture = CreateCompleteScene();
            var obstacle = CreateMarkedObstacle(
                fixture.ObstacleVisualsRoot,
                "TilemapObstacleWithBody",
                Vector3.zero,
                Vector2Int.one,
                Vector2Int.zero);
            fixture.CollisionTilemap.SetTile(Vector3Int.zero, CreateTransientTile());
            var body = new GameObject(MapObstacleVisualMarker.CollisionBodyName, typeof(BoxCollider2D));
            body.transform.SetParent(obstacle.transform, false);

            var report = MapAuthoringValidator.ValidateScene(previewScene, false);

            Assert.That(report.Issues, Has.Some.Matches<MapAuthoringValidationIssue>(issue =>
                issue.Code == "OBSTACLE_COLLISION_BODY_UNEXPECTED" && issue.Context == body));
            Assert.That(report.Issues, Has.Some.Matches<MapAuthoringValidationIssue>(issue =>
                issue.Code == "VISUAL_COLLIDER_FORBIDDEN" && issue.Context == body.GetComponent<BoxCollider2D>()));
        }

        /// <summary>
        /// 面積を持って重なる二つのSocketが重複Errorになることを確認する。
        /// </summary>
        [Test]
        public void ValidateScene_WhenSocketsOverlap_ReportsError()
        {
            var fixture = CreateCompleteScene();
            var position = fixture.GroundTilemap.GetCellCenterWorld(Vector3Int.zero);
            CreateSocket(fixture.SocketsRoot, "EnemySocket", position, new Vector2(0.75f, 0.75f));
            CreateSocket(fixture.SocketsRoot, "LootSocket", position + new Vector3(0.2f, 0f, 0f), new Vector2(0.75f, 0.75f));

            var report = MapAuthoringValidator.ValidateScene(previewScene, false);

            Assert.That(report.Issues, Has.Some.Matches<MapAuthoringValidationIssue>(issue => issue.Code == "SOCKET_OVERLAP"));
        }

        /// <summary>
        /// Event定義をScriptableObjectとして保持し、CatalogがID検索用索引へ変換できることを確認する。
        /// </summary>
        [Test]
        public void MapEventCatalog_BuildsRuntimeLookupFromSerializedDefinitions()
        {
            var fountain = ScriptableObject.CreateInstance<MapEventDefinition>();
            fountain.Configure(
                "healing-fountain-basic",
                "回復の泉",
                "E：泉を使う",
                1.25f,
                true);
            transientAssets.Add(fountain);

            var shrine = ScriptableObject.CreateInstance<MapEventDefinition>();
            shrine.Configure(
                "ancient-shrine-basic",
                "古代の祠",
                "E：調べる",
                1.5f,
                false);
            transientAssets.Add(shrine);

            var catalog = ScriptableObject.CreateInstance<MapEventCatalog>();
            transientAssets.Add(catalog);
            var serializedCatalog = new SerializedObject(catalog);
            var definitions = serializedCatalog.FindProperty("definitions");
            definitions.arraySize = 2;
            definitions.GetArrayElementAtIndex(0).objectReferenceValue = fountain;
            definitions.GetArrayElementAtIndex(1).objectReferenceValue = shrine;
            serializedCatalog.ApplyModifiedPropertiesWithoutUndo();

            Assert.That(catalog.Validate(out var validationError), Is.True, validationError);
            Assert.That(catalog.TryGet("healing-fountain-basic", out var resolved), Is.True);
            Assert.That(resolved, Is.SameAs(fountain));
            Assert.That(catalog.TryGet("ancient-shrine-basic", out resolved), Is.True);
            Assert.That(resolved, Is.SameAs(shrine));
            Assert.That(catalog.TryGet("missing-event", out resolved), Is.False);
        }

        /// <summary>
        /// 同じEventDefinition IDをCatalogへ登録した場合に、保存データの正本を不正として検出する。
        /// </summary>
        [Test]
        public void MapEventCatalog_WhenDefinitionIdsDuplicate_ReportsError()
        {
            var first = ScriptableObject.CreateInstance<MapEventDefinition>();
            first.Configure("duplicate-event", "一つ目", "E：調べる", 1f, true);
            transientAssets.Add(first);
            var second = ScriptableObject.CreateInstance<MapEventDefinition>();
            second.Configure("duplicate-event", "二つ目", "E：調べる", 1f, true);
            transientAssets.Add(second);

            var catalog = ScriptableObject.CreateInstance<MapEventCatalog>();
            transientAssets.Add(catalog);
            var serializedCatalog = new SerializedObject(catalog);
            var definitions = serializedCatalog.FindProperty("definitions");
            definitions.arraySize = 2;
            definitions.GetArrayElementAtIndex(0).objectReferenceValue = first;
            definitions.GetArrayElementAtIndex(1).objectReferenceValue = second;
            serializedCatalog.ApplyModifiedPropertiesWithoutUndo();

            Assert.That(catalog.Validate(out var validationError), Is.False);
            Assert.That(validationError, Does.Contain("duplicate-event"));
        }

        /// <summary>
        /// EventSocketが配置固有ID、定義ID、接近半径を保持し、定義の一度きり設定を共有できることを確認する。
        /// </summary>
        [Test]
        public void MapSocketMarker_EventContractStoresPlacementAndDefinitionReference()
        {
            var definition = ScriptableObject.CreateInstance<MapEventDefinition>();
            definition.Configure("healing-fountain-basic", "回復の泉", "E：泉を使う", 1.25f, true);
            transientAssets.Add(definition);

            var socketObject = new GameObject("FountainSocket");
            SceneManager.MoveGameObjectToScene(socketObject, previewScene);
            socketObject.transform.position = new Vector3(0.21875f, -0.34375f, 0f);
            var marker = socketObject.AddComponent<MapSocketMarker>();
            marker.Configure(
                MapSocketKind.Event,
                Vector2.one,
                MapSocketFacing.Any,
                "fountain-north-01",
                "fountain-north-01",
                definition,
                null,
                1.75f);

            Assert.That(marker.SocketId, Is.EqualTo("fountain-north-01"));
            Assert.That(marker.transform.position.x, Is.EqualTo(0.21875f));
            Assert.That(marker.transform.position.y, Is.EqualTo(-0.34375f));
            Assert.That(marker.EventDefinition, Is.SameAs(definition));
            Assert.That(marker.EventPlacementMode, Is.EqualTo(MapEventPlacementMode.FixedDefinition));
            Assert.That(marker.EventDefinitionId, Is.EqualTo("healing-fountain-basic"));
            Assert.That(marker.InteractionRadius, Is.EqualTo(1.75f));
            marker.SetInteractionRadiusOverride(0f);
            Assert.That(marker.InteractionRadius, Is.EqualTo(1.25f));
            Assert.That(definition.OneShot, Is.True);
        }

        /// <summary>
        /// Pool候補が固定定義を持たず、配置固有の正の接近半径だけを保存することを確認する。
        /// </summary>
        [Test]
        public void MapSocketMarker_PoolCandidateStoresPlacementWithoutFixedDefinition()
        {
            var socketObject = new GameObject("PoolEventSocket");
            SceneManager.MoveGameObjectToScene(socketObject, previewScene);
            var marker = socketObject.AddComponent<MapSocketMarker>();
            marker.Configure(
                MapSocketKind.Event,
                Vector2.one,
                MapSocketFacing.Any,
                "event-pool",
                "event-pool-01",
                null,
                null,
                1.5f,
                MapEventPlacementMode.PoolCandidate);

            Assert.That(marker.EventPlacementMode, Is.EqualTo(MapEventPlacementMode.PoolCandidate));
            Assert.That(marker.EventDefinition, Is.Null);
            Assert.That(marker.EventDefinitionId, Is.Empty);
            Assert.That(marker.InteractionRadius, Is.EqualTo(1.5f));
            Assert.That(marker.TryResolveEventDefinition(null, out _), Is.False);
        }

        /// <summary>
        /// Pool候補に半径がない場合、Event種類の割当前でも制作上の到達範囲を確定できないためErrorにする。
        /// </summary>
        [Test]
        public void ValidateScene_WhenPoolCandidateRadiusIsMissing_ReportsContractError()
        {
            var fixture = CreateCompleteScene();
            var socketObject = new GameObject("PoolEventWithoutRadius");
            socketObject.transform.SetParent(fixture.SocketsRoot, false);
            var marker = socketObject.AddComponent<MapSocketMarker>();
            marker.Configure(
                MapSocketKind.Event,
                Vector2.one,
                MapSocketFacing.Any,
                "event-pool",
                "event-pool-missing-radius",
                null,
                null,
                0f,
                MapEventPlacementMode.PoolCandidate);

            var report = MapAuthoringValidator.ValidateScene(previewScene, false);

            Assert.That(report.Issues, Has.Some.Matches<MapAuthoringValidationIssue>(issue =>
                issue.Code == "SOCKET_EVENT_POOL_RADIUS_REQUIRED" && issue.Context == marker));
        }

        /// <summary>
        /// EventSocketから定義情報が欠けた場合に、近接処理へ曖昧なFallbackを渡さずErrorにする。
        /// </summary>
        [Test]
        public void ValidateScene_WhenEventSocketDefinitionIsMissing_ReportsContractErrors()
        {
            var fixture = CreateCompleteScene();
            var socketObject = new GameObject("EventWithoutDefinition");
            socketObject.transform.SetParent(fixture.SocketsRoot, false);
            socketObject.transform.position = Vector3.zero;
            var marker = socketObject.AddComponent<MapSocketMarker>();
            marker.Configure(MapSocketKind.Event, Vector2.one, MapSocketFacing.Any, "event-missing", "event-missing");

            var report = MapAuthoringValidator.ValidateScene(previewScene, false);

            Assert.That(report.Issues, Has.Some.Matches<MapAuthoringValidationIssue>(issue =>
                issue.Code == "SOCKET_EVENT_DEFINITION_MISSING" && issue.Context == marker));
            Assert.That(report.Issues, Has.Some.Matches<MapAuthoringValidationIssue>(issue =>
                issue.Code == "SOCKET_EVENT_RADIUS_INVALID" && issue.Context == marker));
        }

        /// <summary>
        /// Event中心が障害物上でも、接近円内に立てるGroundがあれば到達不能Errorにしないことを確認する。
        /// </summary>
        [Test]
        public void ValidateScene_WhenEventCenterIsBlockedButRadiusIsReachable_AllowsPlacement()
        {
            var fixture = CreateCompleteScene();
            FillGroundSquare(fixture.GroundTilemap, 2);
            CreateReachabilityBlocker(fixture.MapRoot.transform, Vector2.zero, new Vector2(0.45f, 0.45f));
            var definition = ScriptableObject.CreateInstance<MapEventDefinition>();
            definition.Configure("reachable-event", "到達可能Event", "E：調べる", 1.25f, true);
            transientAssets.Add(definition);
            var socketObject = new GameObject("ReachableEvent");
            socketObject.transform.SetParent(fixture.SocketsRoot, false);
            var marker = socketObject.AddComponent<MapSocketMarker>();
            marker.Configure(
                MapSocketKind.Event,
                Vector2.one,
                MapSocketFacing.Any,
                "reachable-event",
                "reachable-event",
                definition);

            var report = MapAuthoringValidator.ValidateScene(previewScene, false);

            Assert.That(report.Issues, Has.None.Matches<MapAuthoringValidationIssue>(issue =>
                issue.Code == "SOCKET_EVENT_NO_REACHABLE_POINT" && issue.Context == marker));
        }

        /// <summary>
        /// Event円のGround全体がMapCollisionで塞がれている場合に、到達不能Errorを報告する。
        /// </summary>
        [Test]
        public void ValidateScene_WhenEventRadiusHasNoStandingPoint_ReportsError()
        {
            var fixture = CreateCompleteScene();
            FillGroundSquare(fixture.GroundTilemap, 2);
            CreateReachabilityBlocker(fixture.MapRoot.transform, Vector2.zero, new Vector2(4f, 4f));
            var definition = ScriptableObject.CreateInstance<MapEventDefinition>();
            definition.Configure("blocked-event", "到達不能Event", "E：調べる", 1.25f, true);
            transientAssets.Add(definition);
            var socketObject = new GameObject("BlockedEvent");
            socketObject.transform.SetParent(fixture.SocketsRoot, false);
            var marker = socketObject.AddComponent<MapSocketMarker>();
            marker.Configure(
                MapSocketKind.Event,
                Vector2.one,
                MapSocketFacing.Any,
                "blocked-event",
                "blocked-event",
                definition);

            var report = MapAuthoringValidator.ValidateScene(previewScene, false);

            Assert.That(report.Issues, Has.Some.Matches<MapAuthoringValidationIssue>(issue =>
                issue.Code == "SOCKET_EVENT_NO_REACHABLE_POINT" && issue.Context == marker));
        }

        /// <summary>
        /// Socketの四隅がGround範囲外へ出た場合に、範囲外Errorになることを確認する。
        /// </summary>
        [Test]
        public void ValidateScene_WhenSocketLeavesGround_ReportsError()
        {
            var fixture = CreateCompleteScene();
            CreateSocket(fixture.SocketsRoot, "OutsideSocket", new Vector3(10f, 10f, 0f), Vector2.one);

            var report = MapAuthoringValidator.ValidateScene(previewScene, false);

            Assert.That(report.Issues, Has.Some.Matches<MapAuthoringValidationIssue>(issue => issue.Code == "SOCKET_OUT_OF_BOUNDS"));
        }

        /// <summary>
        /// 辺が接するだけのSocketは重複とせず、実際に面積が重なる場合だけを検出することを確認する。
        /// </summary>
        [Test]
        public void SocketsOverlap_WhenOnlyEdgesTouch_ReturnsFalse()
        {
            var fixture = CreateCompleteScene();
            var first = CreateSocket(fixture.SocketsRoot, "First", Vector3.zero, Vector2.one);
            var second = CreateSocket(fixture.SocketsRoot, "Second", Vector3.right, Vector2.one);

            Assert.That(MapAuthoringValidator.SocketsOverlap(first, second), Is.False);

            second.transform.position = new Vector3(0.9f, 0f, 0f);
            Assert.That(MapAuthoringValidator.SocketsOverlap(first, second), Is.True);
        }

        /// <summary>
        /// Postprocessorが唯一のProduction Atlasだけを対象にし、旧loose PNGやSource素材を変更しないことを確認する。
        /// </summary>
        [TestCase("Assets/Art/Map/Production/Forest/forest-gba-production-atlas.png", true)]
        [TestCase("Assets\\Art\\Map\\Production\\Forest\\FOREST-GBA-PRODUCTION-ATLAS.PNG", true)]
        [TestCase("Assets/Art/Map/Production/Forest/grass.png", false)]
        [TestCase("Assets/Art/Map/Source/Forest/forest-gba-design-master.png", false)]
        [TestCase("Assets/Art/Concept/forest.png", false)]
        [TestCase("Assets/Art/Map/Production/Forest/forest-gba-production-atlas.json", false)]
        public void IsProductionMapPng_WithAssetPath_RestrictsProcessingScope(string path, bool expected)
        {
            Assert.That(GbaMapSpritePostprocessor.IsProductionMapPng(path), Is.EqualTo(expected));
        }

        /// <summary>
        /// 実Projectのv4 Atlasを含めて検証し、Catalog、Import設定、Sub-Sprite分割にErrorがないことを確認する。
        /// </summary>
        [Test]
        public void ValidateScene_WithV4ProductionAtlas_HasNoAtlasErrors()
        {
            CreateCompleteScene();

            var report = MapAuthoringValidator.ValidateScene(previewScene, true);

            Assert.That(report.Issues, Has.None.Matches<MapAuthoringValidationIssue>(issue =>
                issue.Code.StartsWith("PRODUCTION_ATLAS", StringComparison.Ordinal)
                || issue.Code.StartsWith("SPRITE_IMPORT", StringComparison.Ordinal)
                || issue.Code.StartsWith("ATLAS_", StringComparison.Ordinal)));
        }

        /// <summary>
        /// Test用に4 Tilemapと自由配置Root、Composite Collision、最小Groundを持つ正しいMapRootを組み立てる。
        /// </summary>
        private SceneFixture CreateCompleteScene()
        {
            var fixture = new SceneFixture();
            fixture.MapRoot = new GameObject("MapRoot");
            SceneManager.MoveGameObjectToScene(fixture.MapRoot, previewScene);

            var gridObject = new GameObject("Grid", typeof(Grid));
            gridObject.transform.SetParent(fixture.MapRoot.transform, false);
            fixture.GridRoot = gridObject.transform;

            fixture.GroundTilemap = CreateTilemap(fixture.GridRoot, "GroundTilemap", "MapGround", 0, false);
            CreateTilemap(fixture.GridRoot, "TerrainTilemap", "MapGround", 10, false);
            fixture.CollisionTilemap = CreateTilemap(fixture.GridRoot, "CollisionTilemap", string.Empty, 0, true);
            CreateTilemap(fixture.GridRoot, "ForegroundTilemap", "MapForeground", 0, false);

            fixture.DecorationsRoot = new GameObject("Decorations").transform;
            fixture.DecorationsRoot.SetParent(fixture.MapRoot.transform, false);
            fixture.ObstacleVisualsRoot = new GameObject("ObstacleVisuals").transform;
            fixture.ObstacleVisualsRoot.SetParent(fixture.MapRoot.transform, false);
            fixture.PropsRoot = new GameObject("Props").transform;
            fixture.PropsRoot.SetParent(fixture.MapRoot.transform, false);
            fixture.SocketsRoot = new GameObject("Sockets").transform;
            fixture.SocketsRoot.SetParent(fixture.MapRoot.transform, false);

            var groundTile = ScriptableObject.CreateInstance<Tile>();
            groundTile.hideFlags = HideFlags.DontSave;
            transientAssets.Add(groundTile);
            fixture.GroundTilemap.SetTile(Vector3Int.zero, groundTile);
            return fixture;
        }

        /// <summary>
        /// Marker必須条件を検証できるよう、実体を持つ一時Spriteを表示する障害物Instanceを作る。
        /// </summary>
        private GameObject CreateVisibleObstacle(Transform parent, string name)
        {
            var texture = new Texture2D(1, 1);
            texture.hideFlags = HideFlags.DontSave;
            texture.SetPixel(0, 0, Color.white);
            texture.Apply();
            transientAssets.Add(texture);

            var sprite = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), Vector2.zero, 1f);
            sprite.hideFlags = HideFlags.DontSave;
            transientAssets.Add(sprite);

            var instance = new GameObject(name, typeof(SpriteRenderer));
            instance.transform.SetParent(parent, false);
            var renderer = instance.GetComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingLayerName = "WorldObjects";
            renderer.sortingOrder = 0;
            renderer.spriteSortPoint = SpriteSortPoint.Pivot;
            return instance;
        }

        /// <summary>
        /// 表示SpriteとFootprint Markerを持つ障害物Instanceを指定位置へ作る。
        /// </summary>
        private GameObject CreateMarkedObstacle(
            Transform parent,
            string name,
            Vector3 position,
            Vector2Int footprintSize,
            Vector2Int footprintOffset)
        {
            var instance = CreateVisibleObstacle(parent, name);
            instance.transform.position = position;
            var marker = instance.AddComponent<MapObstacleVisualMarker>();
            marker.Configure(footprintSize, footprintOffset);
            return instance;
        }

        /// <summary>
        /// Marker設定と一致する精密CollisionBodyを障害物直下へ作り、各異常系Testの正常な起点を用意する。
        /// </summary>
        private GameObject CreateCollisionBodyObstacle(
            Transform parent,
            string name,
            MapObstacleCollisionShape shape,
            Vector2 collisionSize,
            Vector2 collisionOffset,
            float rotationDegrees,
            CapsuleDirection2D capsuleDirection)
        {
            var instance = CreateVisibleObstacle(parent, name);
            var marker = instance.AddComponent<MapObstacleVisualMarker>();
            marker.Configure(
                Vector2Int.one,
                Vector2Int.zero,
                MapObstacleCollisionMode.CollisionBody,
                shape,
                collisionSize,
                collisionOffset,
                rotationDegrees,
                capsuleDirection);

            var body = new GameObject(MapObstacleVisualMarker.CollisionBodyName);
            body.transform.SetParent(instance.transform, false);
            body.transform.localPosition = new Vector3(collisionOffset.x, collisionOffset.y, 0f);
            body.transform.localRotation = Quaternion.Euler(0f, 0f, rotationDegrees);
            body.transform.localScale = Vector3.one;
            var collisionLayer = LayerMask.NameToLayer("MapCollision");
            if (collisionLayer >= 0)
            {
                body.layer = collisionLayer;
            }

            if (shape == MapObstacleCollisionShape.Capsule)
            {
                var capsule = body.AddComponent<CapsuleCollider2D>();
                capsule.size = collisionSize;
                capsule.direction = capsuleDirection;
            }
            else
            {
                body.AddComponent<BoxCollider2D>().size = collisionSize;
            }

            return instance;
        }

        /// <summary>
        /// Issue Contextが指定障害物自身またはその子Component/Objectかを判定する。
        /// </summary>
        private static bool IsContextInHierarchy(Object context, Transform root)
        {
            if (context == null || root == null)
            {
                return false;
            }

            Transform contextTransform;
            if (context is Component component)
            {
                contextTransform = component.transform;
            }
            else if (context is GameObject gameObject)
            {
                contextTransform = gameObject.transform;
            }
            else
            {
                return false;
            }

            return contextTransform == root || contextTransform.IsChildOf(root);
        }

        /// <summary>
        /// Preview SceneのTilemapへ置ける保存対象外Tileを作り、TearDownで確実に破棄する。
        /// </summary>
        private Tile CreateTransientTile()
        {
            var tile = ScriptableObject.CreateInstance<Tile>();
            tile.hideFlags = HideFlags.DontSave;
            transientAssets.Add(tile);
            return tile;
        }

        /// <summary>
        /// 用途別の描画順を設定し、Collision用だけは静的Composite Collider構成を追加する。
        /// </summary>
        private static Tilemap CreateTilemap(
            Transform parent,
            string name,
            string sortingLayer,
            int sortingOrder,
            bool collision)
        {
            var tilemapObject = new GameObject(name, typeof(Tilemap), typeof(TilemapRenderer));
            tilemapObject.transform.SetParent(parent, false);
            var tilemap = tilemapObject.GetComponent<Tilemap>();
            var renderer = tilemapObject.GetComponent<TilemapRenderer>();

            if (!collision)
            {
                renderer.sortingLayerName = sortingLayer;
                renderer.sortingOrder = sortingOrder;
                return tilemap;
            }

            renderer.enabled = false;
            var collisionLayer = LayerMask.NameToLayer("MapCollision");
            if (collisionLayer >= 0)
            {
                tilemapObject.layer = collisionLayer;
            }

            var rigidbody = tilemapObject.AddComponent<Rigidbody2D>();
            rigidbody.bodyType = RigidbodyType2D.Static;
            tilemapObject.AddComponent<CompositeCollider2D>();
            var tilemapCollider = tilemapObject.AddComponent<TilemapCollider2D>();
            EnableCompositeOperation(tilemapCollider);
            return tilemap;
        }

        /// <summary>
        /// Unity Versionに応じたAPIまたはSerialized PropertyでTilemapCollider2DのComposite合成を有効にする。
        /// </summary>
        private static void EnableCompositeOperation(TilemapCollider2D tilemapCollider)
        {
            var property = tilemapCollider.GetType().GetProperty("compositeOperation", BindingFlags.Instance | BindingFlags.Public);
            if (property != null)
            {
                var mergeValue = Enum.Parse(property.PropertyType, "Merge");
                property.SetValue(tilemapCollider, mergeValue, null);
                return;
            }

            var serializedCollider = new SerializedObject(tilemapCollider);
            var operation = serializedCollider.FindProperty("m_CompositeOperation");
            if (operation != null)
            {
                operation.intValue = 1;
                serializedCollider.ApplyModifiedPropertiesWithoutUndo();
                return;
            }

            var legacy = serializedCollider.FindProperty("m_UsedByComposite");
            if (legacy != null)
            {
                legacy.boolValue = true;
                serializedCollider.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        /// <summary>
        /// 指定したWorld範囲のSocketをSockets Root配下へ作成する。
        /// </summary>
        private static MapSocketMarker CreateSocket(Transform parent, string name, Vector3 position, Vector2 size)
        {
            var socketObject = new GameObject(name);
            socketObject.transform.SetParent(parent, false);
            socketObject.transform.position = position;
            var marker = socketObject.AddComponent<MapSocketMarker>();
            marker.Configure(MapSocketKind.Enemy, size, MapSocketFacing.Any, string.Empty, name);
            return marker;
        }

        /// <summary>
        /// Event到達可能性Test用に、原点を囲む正方形範囲へGround Tileを配置する。
        /// </summary>
        private void FillGroundSquare(Tilemap groundTilemap, int extent)
        {
            var tile = CreateTransientTile();
            for (var y = -extent; y <= extent; y++)
            {
                for (var x = -extent; x <= extent; x++)
                {
                    groundTilemap.SetTile(new Vector3Int(x, y, 0), tile);
                }
            }
        }

        /// <summary>
        /// MapCollision Layer上へ表示を持たないBoxを作り、Event中心と接近円を別々に塞ぐ条件を作る。
        /// </summary>
        private static BoxCollider2D CreateReachabilityBlocker(
            Transform parent,
            Vector2 position,
            Vector2 size)
        {
            var blocker = new GameObject("EventReachabilityBlocker", typeof(BoxCollider2D));
            blocker.transform.SetParent(parent, false);
            blocker.transform.position = position;
            blocker.layer = LayerMask.NameToLayer("MapCollision");
            var collider = blocker.GetComponent<BoxCollider2D>();
            collider.size = size;
            return collider;
        }

        /// <summary>
        /// 正常系Testで除外できない構造・描画・Collision・Socket関連Codeかを判定する。
        /// </summary>
        private static bool IsStructuralOrSocketIssue(string code)
        {
            return code.StartsWith("MAP_ROOT", StringComparison.Ordinal)
                || code.StartsWith("GRID", StringComparison.Ordinal)
                || code.StartsWith("DECORATIONS_ROOT", StringComparison.Ordinal)
                || code.StartsWith("DECORATION_", StringComparison.Ordinal)
                || code.StartsWith("OBSTACLE_VISUALS_ROOT", StringComparison.Ordinal)
                || code.StartsWith("OBSTACLE_VISUAL_MARKER", StringComparison.Ordinal)
                || code.StartsWith("OBSTACLE_COLLISION", StringComparison.Ordinal)
                || code.StartsWith("PROPS_ROOT", StringComparison.Ordinal)
                || code.StartsWith("SOCKET", StringComparison.Ordinal)
                || code.StartsWith("TILEMAP", StringComparison.Ordinal)
                || code.StartsWith("LEGACY_TILEMAP", StringComparison.Ordinal)
                || code.StartsWith("COLLISION", StringComparison.Ordinal)
                || code.StartsWith("COMPOSITE", StringComparison.Ordinal)
                || code.StartsWith("STATIC_RIGIDBODY", StringComparison.Ordinal)
                || code.StartsWith("RIGIDBODY", StringComparison.Ordinal)
                || code.StartsWith("VISUAL_", StringComparison.Ordinal)
                || code.StartsWith("SORTING_ORDER", StringComparison.Ordinal);
        }
    }
}
