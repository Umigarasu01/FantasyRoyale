using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using Object = UnityEngine.Object;

namespace FantasyRoyale.MapAuthoringKit.Editor
{
    /// <summary>
    /// Map Authoring検証で報告する問題の重大度。Errorは完成扱いを止め、Warningは確認を促す。
    /// </summary>
    public enum MapAuthoringIssueSeverity
    {
        Warning,
        Error
    }

    /// <summary>
    /// Validatorが検出した一件の問題を、安定したCodeとScene/Asset Context付きで表す。
    /// </summary>
    public sealed class MapAuthoringValidationIssue
    {
        /// <summary>
        /// 検出Code、重大度、説明、選択可能なContextを一件の問題として保持する。
        /// </summary>
        public MapAuthoringValidationIssue(
            string code,
            MapAuthoringIssueSeverity severity,
            string message,
            Object context)
        {
            Code = code;
            Severity = severity;
            Message = message;
            Context = context;
        }

        public string Code { get; }
        public MapAuthoringIssueSeverity Severity { get; }
        public string Message { get; }
        public Object Context { get; }
    }

    /// <summary>
    /// 一回のMap検証結果を集約し、EditorメニューとEditMode Testが同じ判定を利用できるようにする。
    /// </summary>
    public sealed class MapAuthoringValidationReport
    {
        private readonly List<MapAuthoringValidationIssue> issues = new List<MapAuthoringValidationIssue>();

        public IReadOnlyList<MapAuthoringValidationIssue> Issues => issues;

        public bool HasErrors
        {
            get
            {
                for (var i = 0; i < issues.Count; i++)
                {
                    if (issues[i].Severity == MapAuthoringIssueSeverity.Error)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        /// <summary>
        /// 検出箇所のContextを保ったまま、問題をReportへ追加する。
        /// </summary>
        internal void Add(string code, MapAuthoringIssueSeverity severity, string message, Object context = null)
        {
            issues.Add(new MapAuthoringValidationIssue(code, severity, message, context));
        }
    }

    /// <summary>
    /// Active Sceneの標準階層、Tilemap設定、Production Sprite、Socket配置を検証するEditor専用入口。
    /// 自動修正は行わず、制作者の意図を保持したまま設定漏れだけを明示する。
    /// </summary>
    public static class MapAuthoringValidator
    {
        private const float GeometryEpsilon = 0.0001f;
        private const string Renderer2DDataPath = "Assets/Settings/Renderer2D.asset";

        /// <summary>
        /// 標準Tilemap一層に必要な名前、描画順、Collision責務を表す検証定義。
        /// </summary>
        private sealed class TilemapRequirement
        {
            /// <summary>
            /// Tilemap名と期待する描画順、Collision用途かどうかを一つの検証契約にまとめる。
            /// </summary>
            public TilemapRequirement(string name, string sortingLayer, int sortingOrder, bool collision)
            {
                Name = name;
                SortingLayer = sortingLayer;
                SortingOrder = sortingOrder;
                Collision = collision;
            }

            public string Name { get; }
            public string SortingLayer { get; }
            public int SortingOrder { get; }
            public bool Collision { get; }
        }

        private static readonly TilemapRequirement[] RequiredTilemaps =
        {
            new TilemapRequirement("GroundTilemap", "MapGround", 0, false),
            new TilemapRequirement("TerrainTilemap", "MapGround", 10, false),
            new TilemapRequirement("CollisionTilemap", string.Empty, 0, true),
            new TilemapRequirement("ForegroundTilemap", "MapForeground", 0, false)
        };

        private static readonly string[] LegacyTilemapNames =
        {
            "DetailTilemap",
            "ObstacleTilemap"
        };

        private static readonly string[] RequiredSortingLayers =
        {
            "MapGround",
            "MapDetail",
            "WorldObjects",
            "MapForeground",
            "Effects",
            "UI"
        };

        private static readonly HashSet<string> GroundDetailSpriteNames =
            new HashSet<string>(StringComparer.Ordinal)
            {
                "detail_flower_patch",
                "detail_wildflowers"
            };

        private static readonly string[] RequiredPhysicsLayers =
        {
            "MapCollision",
            "MapInteraction"
        };

        private const float EventReachabilitySampleStep = 0.25f;
        private const float EventReachabilityClearance = 0.22f;

        /// <summary>
        /// UnityメニューからActive SceneとProduction Spriteを検証し、Consoleへ結果を出力する。
        /// </summary>
        [MenuItem("FantasyRoyale/Map Authoring/Validate Active Scene")]
        public static void ValidateActiveScene()
        {
            var scene = SceneManager.GetActiveScene();
            var report = ValidateScene(scene, true);
            LogReport(scene, report);
        }

        /// <summary>
        /// 指定Sceneを変更せずに検証する。TestではProduction Asset走査だけを無効化できる。
        /// </summary>
        public static MapAuthoringValidationReport ValidateScene(Scene scene, bool validateProductionSprites = true)
        {
            var report = new MapAuthoringValidationReport();
            ValidateLayerDefinitions(report);
            ValidateWorldYTransparencySorting(report);

            if (!scene.IsValid() || !scene.isLoaded)
            {
                report.Add("SCENE_INVALID", MapAuthoringIssueSeverity.Error, "検証対象Sceneが無効、または未Loadです。");
                return report;
            }

            var mapRoot = FindSingleMapRoot(scene, report);
            if (mapRoot != null)
            {
                ValidateMapRoot(mapRoot.transform, report);
            }

            if (validateProductionSprites)
            {
                ValidateProductionSprites(report);
            }

            return report;
        }

        /// <summary>
        /// 二つのSocket矩形が面積を持って重なるかを、回転とScaleを含むWorld座標で判定する。
        /// 辺が接するだけの場合は配置重複として扱わない。
        /// </summary>
        public static bool SocketsOverlap(MapSocketMarker first, MapSocketMarker second)
        {
            if (first == null || second == null)
            {
                return false;
            }

            var firstCorners = first.GetWorldCorners();
            var secondCorners = second.GetWorldCorners();
            return !HasSeparatingAxis(firstCorners, firstCorners, secondCorners)
                && !HasSeparatingAxis(secondCorners, firstCorners, secondCorners);
        }

        /// <summary>
        /// Scene直下からMapRootを一つだけ取得し、欠落や重複をErrorとして報告する。
        /// </summary>
        private static GameObject FindSingleMapRoot(Scene scene, MapAuthoringValidationReport report)
        {
            GameObject mapRoot = null;
            var roots = scene.GetRootGameObjects();
            for (var i = 0; i < roots.Length; i++)
            {
                if (!string.Equals(roots[i].name, "MapRoot", StringComparison.Ordinal))
                {
                    continue;
                }

                if (mapRoot != null)
                {
                    report.Add(
                        "MAP_ROOT_DUPLICATED",
                        MapAuthoringIssueSeverity.Error,
                        "Scene直下のMapRootは一つだけにしてください。",
                        roots[i]);
                    continue;
                }

                mapRoot = roots[i];
            }

            if (mapRoot == null)
            {
                report.Add("MAP_ROOT_MISSING", MapAuthoringIssueSeverity.Error, "Scene直下にMapRootがありません。");
            }

            return mapRoot;
        }

        /// <summary>
        /// MapRoot直下のGrid、表示専用Root、Socketsと、Grid配下の標準Tilemapを検証する。
        /// </summary>
        private static void ValidateMapRoot(Transform mapRoot, MapAuthoringValidationReport report)
        {
            var decorationsRoot = FindDirectChild(mapRoot, "Decorations");
            if (decorationsRoot == null)
            {
                report.Add(
                    "DECORATIONS_ROOT_MISSING",
                    MapAuthoringIssueSeverity.Error,
                    "MapRoot直下にDecorationsがありません。",
                    mapRoot.gameObject);
            }
            else
            {
                ValidateDisplayOnlyRoot(decorationsRoot, false, report);
                ValidateDirectDisplayInstanceTransforms(decorationsRoot, report);
                ValidateDecorationClassification(decorationsRoot, report);
                ValidateDisplaySorting(decorationsRoot, true, report);
            }

            var obstacleVisualsRoot = FindDirectChild(mapRoot, "ObstacleVisuals");
            if (obstacleVisualsRoot == null)
            {
                report.Add(
                    "OBSTACLE_VISUALS_ROOT_MISSING",
                    MapAuthoringIssueSeverity.Error,
                    "MapRoot直下にObstacleVisualsがありません。",
                    mapRoot.gameObject);
            }
            else
            {
                ValidateDisplayOnlyRoot(obstacleVisualsRoot, true, report);
                ValidateDirectDisplayInstanceTransforms(obstacleVisualsRoot, report);
                ValidateObstacleVisualMarkers(obstacleVisualsRoot, report);
                ValidateDisplaySorting(obstacleVisualsRoot, false, report);
            }

            var propsRoot = FindDirectChild(mapRoot, "Props");
            if (propsRoot == null)
            {
                report.Add("PROPS_ROOT_MISSING", MapAuthoringIssueSeverity.Error, "MapRoot直下にPropsがありません。", mapRoot.gameObject);
            }
            else
            {
                ValidateDisplayOnlyRoot(propsRoot, false, report);
                ValidateDirectDisplayInstanceTransforms(propsRoot, report);
                ValidateDisplaySorting(propsRoot, false, report);
            }

            var socketsRoot = FindDirectChild(mapRoot, "Sockets");
            if (socketsRoot == null)
            {
                report.Add("SOCKETS_ROOT_MISSING", MapAuthoringIssueSeverity.Error, "MapRoot直下にSocketsがありません。", mapRoot.gameObject);
            }

            var gridRoot = FindDirectChild(mapRoot, "Grid");
            if (gridRoot == null)
            {
                report.Add("GRID_MISSING", MapAuthoringIssueSeverity.Error, "MapRoot直下にGridがありません。", mapRoot.gameObject);
                return;
            }

            if (gridRoot.GetComponent<Grid>() == null)
            {
                report.Add("GRID_COMPONENT_MISSING", MapAuthoringIssueSeverity.Error, "GridにGrid componentがありません。", gridRoot.gameObject);
            }

            ValidateLegacyTilemaps(gridRoot, report);

            Tilemap groundTilemap = null;
            Tilemap collisionTilemap = null;
            for (var i = 0; i < RequiredTilemaps.Length; i++)
            {
                var requirement = RequiredTilemaps[i];
                var tilemapTransform = FindDirectChild(gridRoot, requirement.Name);
                if (tilemapTransform == null)
                {
                    report.Add(
                        "TILEMAP_MISSING",
                        MapAuthoringIssueSeverity.Error,
                        $"Grid直下に{requirement.Name}がありません。",
                        gridRoot.gameObject);
                    continue;
                }

                var tilemap = ValidateTilemap(tilemapTransform.gameObject, requirement, report);
                if (string.Equals(requirement.Name, "GroundTilemap", StringComparison.Ordinal))
                {
                    groundTilemap = tilemap;
                }
                else if (string.Equals(requirement.Name, "CollisionTilemap", StringComparison.Ordinal))
                {
                    collisionTilemap = tilemap;
                }
            }

            ValidateObstacleCollisions(obstacleVisualsRoot, collisionTilemap, report);
            ValidateSockets(mapRoot, socketsRoot, groundTilemap, report);
        }

        /// <summary>
        /// 表示専用Root自身と全子孫を走査し、物理Componentや物理用途Layerの混入を報告する。
        /// </summary>
        private static void ValidateDisplayOnlyRoot(
            Transform displayRoot,
            bool allowDeclaredCollisionBodies,
            MapAuthoringValidationReport report)
        {
            var grids = displayRoot.GetComponentsInChildren<Grid>(true);
            for (var i = 0; i < grids.Length; i++)
            {
                report.Add(
                    "VISUAL_GRID_FORBIDDEN",
                    MapAuthoringIssueSeverity.Error,
                    $"表示専用Root {displayRoot.name} 配下にGridを置かないでください。表示物は1px単位で自由配置します。",
                    grids[i]);
            }

            var tilemaps = displayRoot.GetComponentsInChildren<Tilemap>(true);
            for (var i = 0; i < tilemaps.Length; i++)
            {
                report.Add(
                    "VISUAL_TILEMAP_FORBIDDEN",
                    MapAuthoringIssueSeverity.Error,
                    $"表示専用Root {displayRoot.name} 配下にTilemapを置かないでください。地形と判定だけをGrid配下へ分離します。",
                    tilemaps[i]);
            }

            var tilemapRenderers = displayRoot.GetComponentsInChildren<TilemapRenderer>(true);
            for (var i = 0; i < tilemapRenderers.Length; i++)
            {
                report.Add(
                    "VISUAL_TILEMAP_RENDERER_FORBIDDEN",
                    MapAuthoringIssueSeverity.Error,
                    $"表示専用Root {displayRoot.name} 配下にTilemapRendererを置かないでください。装飾と配置物はSpriteRendererで表示します。",
                    tilemapRenderers[i]);
            }

            var colliders = displayRoot.GetComponentsInChildren<Collider2D>(true);
            for (var i = 0; i < colliders.Length; i++)
            {
                if (allowDeclaredCollisionBodies
                    && IsDeclaredCollisionBodyComponent(colliders[i], displayRoot))
                {
                    continue;
                }

                report.Add(
                    "VISUAL_COLLIDER_FORBIDDEN",
                    MapAuthoringIssueSeverity.Error,
                    $"表示専用Root {displayRoot.name} 配下にCollider2Dを置かないでください。判定は専用Layerへ分離します。",
                    colliders[i]);
            }

            var rigidbodies = displayRoot.GetComponentsInChildren<Rigidbody2D>(true);
            for (var i = 0; i < rigidbodies.Length; i++)
            {
                if (allowDeclaredCollisionBodies
                    && IsDeclaredCollisionBodyTransform(rigidbodies[i].transform, displayRoot))
                {
                    continue;
                }

                report.Add(
                    "VISUAL_RIGIDBODY_FORBIDDEN",
                    MapAuthoringIssueSeverity.Error,
                    $"表示専用Root {displayRoot.name} 配下にRigidbody2Dを置かないでください。判定は専用Layerへ分離します。",
                    rigidbodies[i]);
            }

            var defaultLayer = LayerMask.NameToLayer("Default");
            var transforms = displayRoot.GetComponentsInChildren<Transform>(true);
            for (var i = 0; i < transforms.Length; i++)
            {
                var visualObject = transforms[i].gameObject;
                if (allowDeclaredCollisionBodies
                    && IsDeclaredCollisionBodyTransform(transforms[i], displayRoot))
                {
                    continue;
                }

                if (visualObject.layer == defaultLayer)
                {
                    continue;
                }

                report.Add(
                    "VISUAL_LAYER_INVALID",
                    MapAuthoringIssueSeverity.Error,
                    $"表示専用Root {displayRoot.name} 配下のPhysics LayerはDefaultにしてください。",
                    visualObject);
            }
        }

        /// <summary>
        /// 障害物Marker直下で精密判定用として宣言されたCollisionBodyかを判定する。
        /// 名前だけを合わせた任意Objectは許可せず、MarkerのModeとObstacleVisuals直下配置も確認する。
        /// </summary>
        private static bool IsDeclaredCollisionBodyTransform(
            Transform candidate,
            Transform obstacleVisualsRoot)
        {
            if (candidate == null || candidate.parent == null)
            {
                return false;
            }

            var marker = candidate.parent.GetComponent<MapObstacleVisualMarker>();
            if (marker == null
                || marker.CollisionMode != MapObstacleCollisionMode.CollisionBody
                || marker.transform.parent != obstacleVisualsRoot)
            {
                return false;
            }

            return marker.transform.Find(MapObstacleVisualMarker.CollisionBodyName) == candidate;
        }

        /// <summary>
        /// 許可対象のCollisionBody自身に置かれたColliderかを判定する。
        /// 子孫へ紛れたColliderは精密判定契約外として通常の表示Collider違反に残す。
        /// </summary>
        private static bool IsDeclaredCollisionBodyComponent(
            Collider2D collider,
            Transform obstacleVisualsRoot)
        {
            return collider != null
                && IsDeclaredCollisionBodyTransform(collider.transform, obstacleVisualsRoot);
        }

        /// <summary>
        /// 自由配置Spriteが足元Pivotと共通World Y層を使い、カテゴリ固定Orderで前後関係を壊さないか確認する。
        /// </summary>
        private static void ValidateDisplaySorting(
            Transform displayRoot,
            bool allowGroundDetails,
            MapAuthoringValidationReport report)
        {
            var renderers = displayRoot.GetComponentsInChildren<SpriteRenderer>(true);
            for (var i = 0; i < renderers.Length; i++)
            {
                var renderer = renderers[i];
                var isGroundDetail = allowGroundDetails
                                     && renderer.sprite != null
                                     && GroundDetailSpriteNames.Contains(renderer.sprite.name);
                var expectedLayer = isGroundDetail ? "MapDetail" : "WorldObjects";
                if (!string.Equals(
                        renderer.sortingLayerName,
                        expectedLayer,
                        StringComparison.Ordinal))
                {
                    report.Add(
                        "VISUAL_SORTING_LAYER_INVALID",
                        MapAuthoringIssueSeverity.Error,
                        $"{renderer.name}のSorting Layerは{expectedLayer}にしてください。立体表示は足元Yで共通ソートします。",
                        renderer);
                }

                if (renderer.sortingOrder != 0)
                {
                    report.Add(
                        "VISUAL_SORTING_ORDER_INVALID",
                        MapAuthoringIssueSeverity.Error,
                        $"{renderer.name}のOrder in Layerは0にしてください。固定OrderはWorld Y比較より優先されます。",
                        renderer);
                }

                if (renderer.spriteSortPoint != SpriteSortPoint.Pivot)
                {
                    report.Add(
                        "VISUAL_SORT_POINT_INVALID",
                        MapAuthoringIssueSeverity.Error,
                        $"{renderer.name}のSprite Sort PointはPivotにしてください。",
                        renderer);
                }
            }
        }

        /// <summary>
        /// Decorations配下に障害物占有を示すMarkerが混入していないか確認し、表示分類と判定責務の混同を防ぐ。
        /// </summary>
        private static void ValidateDecorationClassification(
            Transform decorationsRoot,
            MapAuthoringValidationReport report)
        {
            var markers = decorationsRoot.GetComponentsInChildren<MapObstacleVisualMarker>(true);
            for (var i = 0; i < markers.Length; i++)
            {
                report.Add(
                    "DECORATION_OBSTACLE_MARKER_FORBIDDEN",
                    MapAuthoringIssueSeverity.Error,
                    $"装飾 {markers[i].name} にMapObstacleVisualMarkerを付けないでください。障害物表示はObstacleVisualsへ分類します。",
                    markers[i]);
            }
        }

        /// <summary>
        /// v4.1で廃止した表示用TilemapがGrid直下へ残っていないか名前で検出する。
        /// </summary>
        private static void ValidateLegacyTilemaps(
            Transform gridRoot,
            MapAuthoringValidationReport report)
        {
            for (var i = 0; i < LegacyTilemapNames.Length; i++)
            {
                var legacyName = LegacyTilemapNames[i];
                var legacyTilemap = FindDirectChild(gridRoot, legacyName);
                if (legacyTilemap == null)
                {
                    continue;
                }

                report.Add(
                    "LEGACY_TILEMAP_FORBIDDEN",
                    MapAuthoringIssueSeverity.Error,
                    $"Grid直下の{legacyName}は廃止済みです。装飾と障害物の表示は自由配置Rootへ移してください。",
                    legacyTilemap.gameObject);
            }
        }

        /// <summary>
        /// 表示専用Root直下のSprite Instanceだけを対象に、Pixel Snap、回転、Scaleの制作規約を検証する。
        /// </summary>
        private static void ValidateDirectDisplayInstanceTransforms(
            Transform displayRoot,
            MapAuthoringValidationReport report)
        {
            for (var childIndex = 0; childIndex < displayRoot.childCount; childIndex++)
            {
                var instance = displayRoot.GetChild(childIndex);
                if (!HasSpriteWithContent(instance, false))
                {
                    continue;
                }

                if (!IsPixelSnapped(instance.position))
                {
                    report.Add(
                        "VISUAL_POSITION_NOT_PIXEL_SNAPPED",
                        MapAuthoringIssueSeverity.Error,
                        $"表示Instance {instance.name} のWorld Positionを1/32 unitへSnapしてください。",
                        instance.gameObject);
                }

                if (Quaternion.Angle(instance.rotation, Quaternion.identity) > GeometryEpsilon)
                {
                    report.Add(
                        "VISUAL_ROTATION_INVALID",
                        MapAuthoringIssueSeverity.Error,
                        $"表示Instance {instance.name} のRotationはIdentityにしてください。",
                        instance.gameObject);
                }

                if ((instance.localScale - Vector3.one).sqrMagnitude > GeometryEpsilon * GeometryEpsilon)
                {
                    report.Add(
                        "VISUAL_SCALE_INVALID",
                        MapAuthoringIssueSeverity.Error,
                        $"表示Instance {instance.name} のLocal ScaleはOneにしてください。",
                        instance.gameObject);
                }
            }
        }

        /// <summary>
        /// World Positionの各軸が32 Pixels Per UnitのPixel境界へ揃っているか判定する。
        /// </summary>
        private static bool IsPixelSnapped(Vector3 position)
        {
            return IsPixelSnapped(position.x)
                && IsPixelSnapped(position.y)
                && IsPixelSnapped(position.z);
        }

        /// <summary>
        /// 一軸のWorld座標を32倍し、最寄り整数との差が許容範囲内か判定する。
        /// </summary>
        private static bool IsPixelSnapped(float value)
        {
            var pixelPosition = value * GbaMapSpritePostprocessor.RequiredPixelsPerUnit;
            return Mathf.Abs(pixelPosition - Mathf.Round(pixelPosition)) <= GeometryEpsilon;
        }

        /// <summary>
        /// ObstacleVisuals直下の可視Instanceに、Collision占有範囲を示す制作Markerがあるか検証する。
        /// </summary>
        private static void ValidateObstacleVisualMarkers(
            Transform obstacleVisualsRoot,
            MapAuthoringValidationReport report)
        {
            for (var childIndex = 0; childIndex < obstacleVisualsRoot.childCount; childIndex++)
            {
                var instance = obstacleVisualsRoot.GetChild(childIndex);
                if (!HasSpriteWithContent(instance, true)
                    || instance.GetComponent<MapObstacleVisualMarker>() != null)
                {
                    continue;
                }

                report.Add(
                    "OBSTACLE_VISUAL_MARKER_MISSING",
                    MapAuthoringIssueSeverity.Error,
                    $"可視障害物 {instance.name} にMapObstacleVisualMarkerがありません。",
                    instance.gameObject);
            }
        }

        /// <summary>
        /// Instance配下に実体Spriteがあり、必要に応じて有効かつScene上で表示可能かを判定する。
        /// </summary>
        private static bool HasSpriteWithContent(Transform instance, bool requireVisible)
        {
            var renderers = instance.GetComponentsInChildren<SpriteRenderer>(true);
            for (var rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
            {
                var renderer = renderers[rendererIndex];
                if (renderer.sprite == null)
                {
                    continue;
                }

                if (!requireVisible || renderer.enabled && renderer.gameObject.activeInHierarchy)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 障害物MarkerのModeごとに、従来のTilemap Footprintまたは足元へ寄せた精密CollisionBodyを検証する。
        /// 表示Prefabを自由配置しても判定がずれないよう、CollisionBodyはMarker直下のLocal座標で照合する。
        /// </summary>
        private static void ValidateObstacleCollisions(
            Transform obstacleVisualsRoot,
            Tilemap collisionTilemap,
            MapAuthoringValidationReport report)
        {
            if (obstacleVisualsRoot == null)
            {
                return;
            }

            for (var childIndex = 0; childIndex < obstacleVisualsRoot.childCount; childIndex++)
            {
                var instance = obstacleVisualsRoot.GetChild(childIndex);
                var marker = instance.GetComponent<MapObstacleVisualMarker>();
                if (marker == null)
                {
                    continue;
                }

                switch (marker.CollisionMode)
                {
                    case MapObstacleCollisionMode.TilemapFootprint:
                        ValidateTilemapFootprintCollision(marker, collisionTilemap, report);
                        break;
                    case MapObstacleCollisionMode.CollisionBody:
                        ValidateCollisionBody(marker, report);
                        break;
                    default:
                        report.Add(
                            "OBSTACLE_COLLISION_MODE_INVALID",
                            MapAuthoringIssueSeverity.Error,
                            $"障害物 {instance.name} のCollision Modeが未対応です。",
                            marker);
                        break;
                }
            }
        }

        /// <summary>
        /// TilemapFootprint ModeではCollisionBodyを持たず、従来どおり全Footprint Cellが保存済みか確認する。
        /// </summary>
        private static void ValidateTilemapFootprintCollision(
            MapObstacleVisualMarker marker,
            Tilemap collisionTilemap,
            MapAuthoringValidationReport report)
        {
            var collisionBody = marker.transform.Find(MapObstacleVisualMarker.CollisionBodyName);
            if (collisionBody != null)
            {
                report.Add(
                    "OBSTACLE_COLLISION_BODY_UNEXPECTED",
                    MapAuthoringIssueSeverity.Error,
                    $"TilemapFootprint障害物 {marker.name} にCollisionBodyを置かないでください。",
                    collisionBody.gameObject);
            }

            if (collisionTilemap == null)
            {
                return;
            }

            var anchorCell = FindNearestCellAnchor(collisionTilemap, marker.transform.position);
            var missingCount = 0;
            var firstMissingCell = default(Vector3Int);
            foreach (var cell in marker.GetFootprintCells(anchorCell))
            {
                if (collisionTilemap.HasTile(cell))
                {
                    continue;
                }

                if (missingCount == 0)
                {
                    firstMissingCell = cell;
                }

                missingCount++;
            }

            if (missingCount == 0)
            {
                return;
            }

            report.Add(
                "OBSTACLE_COLLISION_MISSING",
                MapAuthoringIssueSeverity.Error,
                $"障害物 {marker.name} のFootprintにCollisionが{missingCount} Cell不足しています。先頭: {firstMissingCell}",
                marker.gameObject);
        }

        /// <summary>
        /// CollisionBody Modeの直下Bodyが、Markerに保存された形状、足元Offset、向きを過不足なく表すか検証する。
        /// </summary>
        private static void ValidateCollisionBody(
            MapObstacleVisualMarker marker,
            MapAuthoringValidationReport report)
        {
            var namedBodyCount = 0;
            for (var childIndex = 0; childIndex < marker.transform.childCount; childIndex++)
            {
                if (marker.transform.GetChild(childIndex).name == MapObstacleVisualMarker.CollisionBodyName)
                {
                    namedBodyCount++;
                }
            }

            var collisionBody = marker.transform.Find(MapObstacleVisualMarker.CollisionBodyName);
            if (collisionBody == null)
            {
                report.Add(
                    "OBSTACLE_COLLISION_BODY_MISSING",
                    MapAuthoringIssueSeverity.Error,
                    $"精密判定障害物 {marker.name} にCollisionBodyがありません。",
                    marker.gameObject);
                return;
            }

            if (namedBodyCount != 1)
            {
                report.Add(
                    "OBSTACLE_COLLISION_BODY_COUNT_INVALID",
                    MapAuthoringIssueSeverity.Error,
                    $"精密判定障害物 {marker.name} のCollisionBodyは直下に1つだけ置いてください。現在: {namedBodyCount}",
                    marker.gameObject);
            }

            var collisionLayer = LayerMask.NameToLayer("MapCollision");
            if (collisionLayer >= 0 && collisionBody.gameObject.layer != collisionLayer)
            {
                report.Add(
                    "OBSTACLE_COLLISION_BODY_LAYER_INVALID",
                    MapAuthoringIssueSeverity.Error,
                    $"{marker.name}/CollisionBodyのPhysics LayerはMapCollisionにしてください。",
                    collisionBody.gameObject);
            }

            if ((collisionBody.localScale - Vector3.one).sqrMagnitude > GeometryEpsilon * GeometryEpsilon)
            {
                report.Add(
                    "OBSTACLE_COLLISION_BODY_SCALE_INVALID",
                    MapAuthoringIssueSeverity.Error,
                    $"{marker.name}/CollisionBodyのLocal Scaleは1にしてください。",
                    collisionBody.gameObject);
            }

            var expectedPosition = new Vector3(marker.CollisionOffset.x, marker.CollisionOffset.y, 0f);
            if ((collisionBody.localPosition - expectedPosition).sqrMagnitude > GeometryEpsilon * GeometryEpsilon)
            {
                report.Add(
                    "OBSTACLE_COLLISION_BODY_POSITION_MISMATCH",
                    MapAuthoringIssueSeverity.Error,
                    $"{marker.name}/CollisionBodyのLocal PositionがMarkerのCollision Offsetと一致しません。",
                    collisionBody.gameObject);
            }

            var expectedRotation = Quaternion.Euler(0f, 0f, marker.CollisionRotationDegrees);
            if (Quaternion.Angle(collisionBody.localRotation, expectedRotation) > GeometryEpsilon)
            {
                report.Add(
                    "OBSTACLE_COLLISION_BODY_ROTATION_MISMATCH",
                    MapAuthoringIssueSeverity.Error,
                    $"{marker.name}/CollisionBodyのLocal RotationがMarkerのCollision Rotationと一致しません。",
                    collisionBody.gameObject);
            }

            var renderers = collisionBody.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length > 0)
            {
                report.Add(
                    "OBSTACLE_COLLISION_BODY_RENDERER_FORBIDDEN",
                    MapAuthoringIssueSeverity.Error,
                    $"{marker.name}/CollisionBodyにRendererを置かないでください。",
                    renderers[0]);
            }

            var rigidbodies = collisionBody.GetComponentsInChildren<Rigidbody2D>(true);
            if (rigidbodies.Length > 0)
            {
                report.Add(
                    "OBSTACLE_COLLISION_BODY_RIGIDBODY_FORBIDDEN",
                    MapAuthoringIssueSeverity.Error,
                    $"{marker.name}/CollisionBodyにRigidbody2Dを置かないでください。",
                    rigidbodies[0]);
            }

            var colliders = collisionBody.GetComponentsInChildren<Collider2D>(true);
            if (colliders.Length != 1)
            {
                report.Add(
                    "OBSTACLE_COLLISION_BODY_COLLIDER_COUNT_INVALID",
                    MapAuthoringIssueSeverity.Error,
                    $"{marker.name}/CollisionBodyのCollider2Dは1つだけにしてください。現在: {colliders.Length}",
                    collisionBody.gameObject);
                return;
            }

            var collider = colliders[0];
            if (collider.transform != collisionBody)
            {
                report.Add(
                    "OBSTACLE_COLLISION_BODY_COLLIDER_LOCATION_INVALID",
                    MapAuthoringIssueSeverity.Error,
                    $"{marker.name}のCollider2DはCollisionBody自身に置いてください。",
                    collider);
            }

            if (!collider.enabled)
            {
                report.Add(
                    "OBSTACLE_COLLISION_BODY_COLLIDER_DISABLED",
                    MapAuthoringIssueSeverity.Error,
                    $"{marker.name}/CollisionBodyのCollider2Dを有効にしてください。",
                    collider);
            }

            if (collider.isTrigger)
            {
                report.Add(
                    "OBSTACLE_COLLISION_BODY_TRIGGER_FORBIDDEN",
                    MapAuthoringIssueSeverity.Error,
                    $"{marker.name}/CollisionBodyはTriggerにせず、移動を遮るColliderにしてください。",
                    collider);
            }

            ValidateCollisionBodyShape(marker, collider, report);
        }

        /// <summary>
        /// Markerが選んだBoxまたはCapsuleと実Colliderの型、Size、Capsule方向を照合する。
        /// </summary>
        private static void ValidateCollisionBodyShape(
            MapObstacleVisualMarker marker,
            Collider2D collider,
            MapAuthoringValidationReport report)
        {
            Vector2 actualSize;
            switch (marker.CollisionShape)
            {
                case MapObstacleCollisionShape.Box when collider is BoxCollider2D boxCollider:
                    actualSize = boxCollider.size;
                    break;
                case MapObstacleCollisionShape.Capsule when collider is CapsuleCollider2D capsuleCollider:
                    actualSize = capsuleCollider.size;
                    if (capsuleCollider.direction != marker.CapsuleDirection)
                    {
                        report.Add(
                            "OBSTACLE_COLLISION_BODY_CAPSULE_DIRECTION_MISMATCH",
                            MapAuthoringIssueSeverity.Error,
                            $"{marker.name}/CollisionBodyのCapsule DirectionがMarker設定と一致しません。",
                            capsuleCollider);
                    }

                    break;
                default:
                    report.Add(
                        "OBSTACLE_COLLISION_BODY_SHAPE_MISMATCH",
                        MapAuthoringIssueSeverity.Error,
                        $"{marker.name}/CollisionBodyのCollider型がMarkerのCollision Shapeと一致しません。",
                        collider);
                    return;
            }

            if ((actualSize - marker.CollisionSize).sqrMagnitude <= GeometryEpsilon * GeometryEpsilon)
            {
                return;
            }

            report.Add(
                "OBSTACLE_COLLISION_BODY_SIZE_MISMATCH",
                MapAuthoringIssueSeverity.Error,
                $"{marker.name}/CollisionBodyのSizeがMarkerのCollision Sizeと一致しません。",
                collider);
        }

        /// <summary>
        /// World Position周辺のCell原点を比較し、表示Anchorに最も近い論理Cellを返す。
        /// </summary>
        private static Vector3Int FindNearestCellAnchor(Tilemap tilemap, Vector3 worldPosition)
        {
            var baseCell = tilemap.WorldToCell(worldPosition);
            var nearestCell = baseCell;
            var nearestDistance = (tilemap.CellToWorld(baseCell) - worldPosition).sqrMagnitude;
            for (var yOffset = -1; yOffset <= 1; yOffset++)
            {
                for (var xOffset = -1; xOffset <= 1; xOffset++)
                {
                    var candidate = baseCell + new Vector3Int(xOffset, yOffset, 0);
                    var distance = (tilemap.CellToWorld(candidate) - worldPosition).sqrMagnitude;
                    if (distance + GeometryEpsilon >= nearestDistance)
                    {
                        continue;
                    }

                    nearestCell = candidate;
                    nearestDistance = distance;
                }
            }

            return nearestCell;
        }

        /// <summary>
        /// Tilemap component、Rendererの描画順、Collision専用構成を用途別契約と照合する。
        /// </summary>
        private static Tilemap ValidateTilemap(
            GameObject tilemapObject,
            TilemapRequirement requirement,
            MapAuthoringValidationReport report)
        {
            var tilemap = tilemapObject.GetComponent<Tilemap>();
            if (tilemap == null)
            {
                report.Add("TILEMAP_COMPONENT_MISSING", MapAuthoringIssueSeverity.Error, $"{requirement.Name}にTilemap componentがありません。", tilemapObject);
            }

            var renderer = tilemapObject.GetComponent<TilemapRenderer>();
            if (renderer == null)
            {
                report.Add("TILEMAP_RENDERER_MISSING", MapAuthoringIssueSeverity.Error, $"{requirement.Name}にTilemapRendererがありません。", tilemapObject);
            }
            else if (requirement.Collision)
            {
                if (renderer.enabled)
                {
                    report.Add("COLLISION_RENDERER_VISIBLE", MapAuthoringIssueSeverity.Error, "CollisionTilemapのTilemapRendererは無効にしてください。", tilemapObject);
                }
            }
            else
            {
                if (!string.Equals(renderer.sortingLayerName, requirement.SortingLayer, StringComparison.Ordinal))
                {
                    report.Add(
                        "SORTING_LAYER_INVALID",
                        MapAuthoringIssueSeverity.Error,
                        $"{requirement.Name}のSorting Layerは{requirement.SortingLayer}にしてください。",
                        tilemapObject);
                }

                if (renderer.sortingOrder != requirement.SortingOrder)
                {
                    report.Add(
                        "SORTING_ORDER_INVALID",
                        MapAuthoringIssueSeverity.Error,
                        $"{requirement.Name}のOrder in Layerは{requirement.SortingOrder}にしてください。",
                        tilemapObject);
                }
            }

            if (requirement.Collision)
            {
                ValidateCollisionTilemap(tilemapObject, report);
            }

            return tilemap;
        }

        /// <summary>
        /// CollisionTilemapが静的Composite Colliderとして機能するためのcomponentとPhysics Layerを検証する。
        /// </summary>
        private static void ValidateCollisionTilemap(GameObject collisionObject, MapAuthoringValidationReport report)
        {
            var tilemapCollider = collisionObject.GetComponent<TilemapCollider2D>();
            if (tilemapCollider == null)
            {
                report.Add("TILEMAP_COLLIDER_MISSING", MapAuthoringIssueSeverity.Error, "CollisionTilemapにTilemapCollider2Dがありません。", collisionObject);
            }

            if (collisionObject.GetComponent<CompositeCollider2D>() == null)
            {
                report.Add("COMPOSITE_COLLIDER_MISSING", MapAuthoringIssueSeverity.Error, "CollisionTilemapにCompositeCollider2Dがありません。", collisionObject);
            }

            var rigidbody = collisionObject.GetComponent<Rigidbody2D>();
            if (rigidbody == null)
            {
                report.Add("STATIC_RIGIDBODY_MISSING", MapAuthoringIssueSeverity.Error, "CollisionTilemapにRigidbody2Dがありません。", collisionObject);
            }
            else if (rigidbody.bodyType != RigidbodyType2D.Static)
            {
                report.Add("RIGIDBODY_NOT_STATIC", MapAuthoringIssueSeverity.Error, "CollisionTilemapのRigidbody2DはStaticにしてください。", collisionObject);
            }

            var collisionLayer = LayerMask.NameToLayer("MapCollision");
            if (collisionLayer >= 0 && collisionObject.layer != collisionLayer)
            {
                report.Add("COLLISION_LAYER_INVALID", MapAuthoringIssueSeverity.Error, "CollisionTilemapのPhysics LayerはMapCollisionにしてください。", collisionObject);
            }

            if (tilemapCollider != null && !UsesCompositeCollider(tilemapCollider))
            {
                report.Add("COMPOSITE_OPERATION_DISABLED", MapAuthoringIssueSeverity.Error, "TilemapCollider2DをCompositeCollider2Dへ合成する設定にしてください。", collisionObject);
            }
        }

        /// <summary>
        /// Unity Version間のCollider2D API差を吸収し、Composite合成が有効かを読み取る。
        /// </summary>
        private static bool UsesCompositeCollider(TilemapCollider2D tilemapCollider)
        {
            var colliderType = tilemapCollider.GetType();
            var operationProperty = colliderType.GetProperty("compositeOperation", BindingFlags.Instance | BindingFlags.Public);
            if (operationProperty != null)
            {
                var value = operationProperty.GetValue(tilemapCollider, null);
                return value != null && !string.Equals(value.ToString(), "None", StringComparison.Ordinal);
            }

            var legacyProperty = colliderType.GetProperty("usedByComposite", BindingFlags.Instance | BindingFlags.Public);
            if (legacyProperty != null && legacyProperty.PropertyType == typeof(bool))
            {
                return (bool)legacyProperty.GetValue(tilemapCollider, null);
            }

            var serializedCollider = new SerializedObject(tilemapCollider);
            var serializedOperation = serializedCollider.FindProperty("m_CompositeOperation");
            if (serializedOperation != null)
            {
                return serializedOperation.intValue != 0;
            }

            var serializedLegacy = serializedCollider.FindProperty("m_UsedByComposite");
            return serializedLegacy != null && serializedLegacy.boolValue;
        }

        /// <summary>
        /// 必須Sorting LayerとPhysics LayerがProjectSettingsに定義されているかを検証する。
        /// </summary>
        private static void ValidateLayerDefinitions(MapAuthoringValidationReport report)
        {
            for (var i = 0; i < RequiredSortingLayers.Length; i++)
            {
                var requiredLayer = RequiredSortingLayers[i];
                if (!SortingLayerExists(requiredLayer))
                {
                    report.Add("SORTING_LAYER_MISSING", MapAuthoringIssueSeverity.Error, $"Sorting Layer {requiredLayer} がProjectSettingsにありません。");
                }
            }

            for (var i = 0; i < RequiredPhysicsLayers.Length; i++)
            {
                var requiredLayer = RequiredPhysicsLayers[i];
                if (LayerMask.NameToLayer(requiredLayer) < 0)
                {
                    report.Add("PHYSICS_LAYER_MISSING", MapAuthoringIssueSeverity.Error, $"Physics Layer {requiredLayer} がProjectSettingsにありません。");
                }
            }
        }

        /// <summary>
        /// 実描画を行うURP 2D RendererがCustom Y Axisを正本としているか検証する。
        /// </summary>
        private static void ValidateWorldYTransparencySorting(MapAuthoringValidationReport report)
        {
            var rendererData = AssetDatabase.LoadMainAssetAtPath(Renderer2DDataPath);
            if (rendererData == null)
            {
                report.Add(
                    "WORLD_Y_RENDERER_DATA_MISSING",
                    MapAuthoringIssueSeverity.Error,
                    $"URP 2D Renderer Dataがありません: {Renderer2DDataPath}");
                return;
            }

            var serializedRendererData = new SerializedObject(rendererData);
            var sortMode = serializedRendererData.FindProperty("m_TransparencySortMode");
            var sortAxis = serializedRendererData.FindProperty("m_TransparencySortAxis");
            if (sortMode == null
                || sortMode.intValue != (int)TransparencySortMode.CustomAxis)
            {
                report.Add(
                    "WORLD_Y_SORT_MODE_INVALID",
                    MapAuthoringIssueSeverity.Error,
                    "URP 2D RendererのTransparency Sort ModeはCustom Axisにしてください。",
                    rendererData);
            }

            if (sortAxis == null
                || (sortAxis.vector3Value - Vector3.up).sqrMagnitude > GeometryEpsilon)
            {
                report.Add(
                    "WORLD_Y_SORT_AXIS_INVALID",
                    MapAuthoringIssueSeverity.Error,
                    "URP 2D RendererのTransparency Sort Axisは(0, 1, 0)にしてください。",
                    rendererData);
            }
        }

        /// <summary>
        /// Unityに登録されたSorting Layerを名前で検索する。
        /// </summary>
        private static bool SortingLayerExists(string layerName)
        {
            var layers = SortingLayer.layers;
            for (var i = 0; i < layers.Length; i++)
            {
                if (string.Equals(layers[i].name, layerName, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 単一Production AtlasとCatalogについて、Import設定、寸法、Sprite名、矩形、Pivotの逸脱を報告する。
        /// </summary>
        private static void ValidateProductionSprites(MapAuthoringValidationReport report)
        {
            var atlasPath = GbaMapSpritePostprocessor.ProductionAtlasPath;
            var atlasTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(atlasPath);
            if (atlasTexture == null)
            {
                report.Add(
                    "PRODUCTION_ATLAS_MISSING",
                    MapAuthoringIssueSeverity.Error,
                    $"Production Atlasがありません: {atlasPath}");
                return;
            }

            var importer = AssetImporter.GetAtPath(atlasPath) as TextureImporter;
            if (importer == null)
            {
                report.Add(
                    "SPRITE_IMPORTER_MISSING",
                    MapAuthoringIssueSeverity.Error,
                    $"TextureImporterを取得できません: {atlasPath}",
                    atlasTexture);
                return;
            }

            var invalidSettings = new List<string>();
            if (importer.textureType != TextureImporterType.Sprite)
            {
                invalidSettings.Add("Texture Type=Sprite");
            }

            if (importer.spriteImportMode != SpriteImportMode.Multiple)
            {
                invalidSettings.Add("Sprite Mode=Multiple");
            }

            if (!Mathf.Approximately(importer.spritePixelsPerUnit, GbaMapSpritePostprocessor.RequiredPixelsPerUnit))
            {
                invalidSettings.Add("PPU=32");
            }

            if (importer.filterMode != FilterMode.Point)
            {
                invalidSettings.Add("Filter Mode=Point");
            }

            if (importer.textureCompression != TextureImporterCompression.Uncompressed)
            {
                invalidSettings.Add("Compression=None");
            }

            if (importer.mipmapEnabled)
            {
                invalidSettings.Add("Mip Maps=Off");
            }

            if (importer.wrapMode != TextureWrapMode.Clamp)
            {
                invalidSettings.Add("Wrap Mode=Clamp");
            }

            if (!importer.alphaIsTransparency)
            {
                invalidSettings.Add("Alpha Is Transparency=On");
            }

            if (importer.npotScale != TextureImporterNPOTScale.None)
            {
                invalidSettings.Add("NPOT Scale=None");
            }

            var textureSettings = new TextureImporterSettings();
            importer.ReadTextureSettings(textureSettings);
            if (textureSettings.spriteMeshType != SpriteMeshType.FullRect)
            {
                invalidSettings.Add("Mesh Type=Full Rect");
            }

            if (invalidSettings.Count > 0)
            {
                report.Add(
                    "SPRITE_IMPORT_SETTINGS_INVALID",
                    MapAuthoringIssueSeverity.Error,
                    $"{atlasPath} のImport設定を修正してください: {string.Join(", ", invalidSettings)}",
                    atlasTexture);
            }

            if (!GbaMapSpritePostprocessor.TryLoadCatalog(out var catalog, out var catalogError))
            {
                report.Add(
                    "ATLAS_CATALOG_INVALID",
                    MapAuthoringIssueSeverity.Error,
                    $"Production Atlas catalogが不正です: {catalogError}",
                    atlasTexture);
                return;
            }

            if (atlasTexture.width != catalog.atlasWidth || atlasTexture.height != catalog.atlasHeight)
            {
                report.Add(
                    "ATLAS_TEXTURE_SIZE_INVALID",
                    MapAuthoringIssueSeverity.Error,
                    $"Atlas画像寸法とCatalogが一致しません: "
                        + $"image={atlasTexture.width}x{atlasTexture.height}, "
                        + $"catalog={catalog.atlasWidth}x{catalog.atlasHeight}",
                    atlasTexture);
            }

            ValidateAtlasSpriteContract(atlasPath, atlasTexture, catalog, report);
        }

        /// <summary>
        /// CatalogのSprite集合をv4命名契約およびImport済みSub-Spriteと照合する。
        /// </summary>
        private static void ValidateAtlasSpriteContract(
            string atlasPath,
            Texture2D atlasTexture,
            GbaMapSpritePostprocessor.AtlasCatalog catalog,
            MapAuthoringValidationReport report)
        {
            var expectedNames = BuildExpectedAtlasSpriteNames();
            var catalogEntries = new Dictionary<string, GbaMapSpritePostprocessor.AtlasSpriteEntry>(StringComparer.Ordinal);
            for (var i = 0; i < catalog.sprites.Length; i++)
            {
                catalogEntries[catalog.sprites[i].name] = catalog.sprites[i];
            }

            foreach (var expectedName in expectedNames)
            {
                if (!catalogEntries.ContainsKey(expectedName))
                {
                    report.Add(
                        "ATLAS_CATALOG_SPRITE_MISSING",
                        MapAuthoringIssueSeverity.Error,
                        $"Catalogに必須Spriteがありません: {expectedName}",
                        atlasTexture);
                }
            }

            foreach (var catalogName in catalogEntries.Keys)
            {
                if (!expectedNames.Contains(catalogName))
                {
                    report.Add(
                        "ATLAS_CATALOG_SPRITE_UNEXPECTED",
                        MapAuthoringIssueSeverity.Error,
                        $"Catalogに契約外Spriteがあります: {catalogName}",
                        atlasTexture);
                }
            }

            var importedSprites = new Dictionary<string, Sprite>(StringComparer.Ordinal);
            var atlasAssets = AssetDatabase.LoadAllAssetsAtPath(atlasPath);
            for (var i = 0; i < atlasAssets.Length; i++)
            {
                if (!(atlasAssets[i] is Sprite sprite))
                {
                    continue;
                }

                if (importedSprites.ContainsKey(sprite.name))
                {
                    report.Add(
                        "ATLAS_SPRITE_NAME_DUPLICATE",
                        MapAuthoringIssueSeverity.Error,
                        $"Import済みSprite名が重複しています: {sprite.name}",
                        sprite);
                    continue;
                }

                importedSprites.Add(sprite.name, sprite);
            }

            foreach (var pair in catalogEntries)
            {
                if (!importedSprites.TryGetValue(pair.Key, out var sprite))
                {
                    report.Add(
                        "ATLAS_SPRITE_MISSING",
                        MapAuthoringIssueSeverity.Error,
                        $"Catalog SpriteがImportされていません: {pair.Key}",
                        atlasTexture);
                    continue;
                }

                var entry = pair.Value;
                var expectedRect = new Rect(entry.x, entry.y, entry.width, entry.height);
                if (!RectsApproximatelyEqual(sprite.rect, expectedRect))
                {
                    report.Add(
                        "ATLAS_SPRITE_RECT_INVALID",
                        MapAuthoringIssueSeverity.Error,
                        $"Sprite矩形がCatalogと一致しません: {entry.name}",
                        sprite);
                }

                var expectedPivot = new Vector2(
                    entry.width * entry.pivotX,
                    entry.height * entry.pivotY);
                if ((sprite.pivot - expectedPivot).sqrMagnitude > 0.0001f)
                {
                    report.Add(
                        "ATLAS_SPRITE_PIVOT_INVALID",
                        MapAuthoringIssueSeverity.Error,
                        $"Sprite PivotがCatalogと一致しません: {entry.name}",
                        sprite);
                }
            }

            if (importedSprites.Count != catalogEntries.Count)
            {
                report.Add(
                    "ATLAS_SPRITE_COUNT_INVALID",
                    MapAuthoringIssueSeverity.Error,
                    $"Import済みSprite数とCatalogが一致しません: "
                        + $"imported={importedSprites.Count}, catalog={catalogEntries.Count}",
                    atlasTexture);
            }
        }

        /// <summary>
        /// v4.3で許可する地形Tile、道路輪郭差分、表示専用Obstacle、Propの全Sprite名を決定的に構築する。
        /// </summary>
        private static HashSet<string> BuildExpectedAtlasSpriteNames()
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            for (var index = 1; index <= GroundVariationTile.RequiredVariantCount; index++)
            {
                names.Add($"ground_grass_{index:00}");
            }

            for (var mask = 0; mask < 16; mask++)
            {
                names.Add($"dirt_{mask:x2}");
                names.Add($"stone_{mask:x2}");
                for (var variant = 2; variant <= 4; variant++)
                {
                    names.Add($"dirt_{mask:x2}_v{variant:00}");
                    names.Add($"stone_{mask:x2}_v{variant:00}");
                }
            }

            var blobMasks = BuildCanonicalBlobMasksForValidation();
            for (var i = 0; i < blobMasks.Count; i++)
            {
                var mask = blobMasks[i];
                names.Add($"water_{mask:x2}");
            }

            for (var variant = 2; variant <= 4; variant++)
            {
                names.Add($"water_ff_v{variant:00}");
            }

            names.Add("detail_tall_grass");
            names.Add("detail_flower_patch");
            names.Add("detail_wildflowers");
            names.Add("detail_reeds");
            names.Add("prop_tree_medium_01");
            names.Add("prop_tree_medium_02");
            names.Add("prop_tree_medium_03");
            names.Add("prop_blocking_bush");
            names.Add("prop_mossy_rock");
            names.Add("prop_stump");
            names.Add("prop_fallen_log");
            names.Add("prop_mushroom_patch");
            names.Add("prop_chest");
            names.Add("prop_signpost");
            names.Add("obstacle_forest_mass_wide");
            names.Add("obstacle_forest_mass_deep");
            names.Add("obstacle_forest_front_strip");
            names.Add("obstacle_cliff_straight_wide");
            names.Add("obstacle_cliff_outer_corner");
            return names;
        }

        /// <summary>
        /// Water用にCardinalが両方接続する角だけ対角を許可し、Blob topologyの47 Maskを再現する。
        /// </summary>
        private static List<int> BuildCanonicalBlobMasksForValidation()
        {
            var masks = new HashSet<int>();
            for (var cardinalMask = 0; cardinalMask < 16; cardinalMask++)
            {
                var baseMask = 0;
                if ((cardinalMask & 1) != 0) baseMask |= 1;
                if ((cardinalMask & 2) != 0) baseMask |= 4;
                if ((cardinalMask & 4) != 0) baseMask |= 16;
                if ((cardinalMask & 8) != 0) baseMask |= 64;

                var optionalDiagonals = new List<int>();
                if ((cardinalMask & 3) == 3) optionalDiagonals.Add(2);
                if ((cardinalMask & 6) == 6) optionalDiagonals.Add(8);
                if ((cardinalMask & 12) == 12) optionalDiagonals.Add(32);
                if ((cardinalMask & 9) == 9) optionalDiagonals.Add(128);

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
            return result;
        }

        /// <summary>
        /// 浮動小数へ変換された二つのSprite矩形をPixel単位の許容差で比較する。
        /// </summary>
        private static bool RectsApproximatelyEqual(Rect first, Rect second)
        {
            return Mathf.Abs(first.x - second.x) < 0.001f
                && Mathf.Abs(first.y - second.y) < 0.001f
                && Mathf.Abs(first.width - second.width) < 0.001f
                && Mathf.Abs(first.height - second.height) < 0.001f;
        }

        /// <summary>
        /// Socketの所属、正の範囲、Ground内包、相互重複を検証する。
        /// </summary>
        private static void ValidateSockets(
            Transform mapRoot,
            Transform socketsRoot,
            Tilemap groundTilemap,
            MapAuthoringValidationReport report)
        {
            var markers = mapRoot.GetComponentsInChildren<MapSocketMarker>(true);
            var validMarkers = new List<MapSocketMarker>();
            var socketIds = new HashSet<string>(StringComparer.Ordinal);
            var mapCollisionLayer = LayerMask.NameToLayer("MapCollision");
            var mapCollisionColliders = GetMapCollisionColliders(mapRoot, mapCollisionLayer);
            var hasGroundBounds = groundTilemap != null && groundTilemap.GetUsedTilesCount() > 0;
            Vector3[] groundCorners = null;

            if (groundTilemap != null && !hasGroundBounds && markers.Length > 0)
            {
                report.Add("GROUND_EMPTY", MapAuthoringIssueSeverity.Error, "Socket範囲を検証するため、GroundTilemapに地面Tileを配置してください。", groundTilemap.gameObject);
            }
            else if (hasGroundBounds)
            {
                groundCorners = GetGroundWorldCorners(groundTilemap);
            }

            for (var i = 0; i < markers.Length; i++)
            {
                var marker = markers[i];
                var socketId = marker.SocketId == null ? string.Empty : marker.SocketId.Trim();
                if (string.IsNullOrWhiteSpace(socketId))
                {
                    report.Add(
                        "SOCKET_ID_MISSING",
                        MapAuthoringIssueSeverity.Error,
                        $"{marker.name} のSocket IDを設定してください。",
                        marker);
                }
                else if (!socketIds.Add(socketId))
                {
                    report.Add(
                        "SOCKET_ID_DUPLICATE",
                        MapAuthoringIssueSeverity.Error,
                        $"Socket IDが重複しています: {socketId}",
                        marker);
                }

                ValidateEventSocketContract(marker, report);
                if (socketsRoot == null || !marker.transform.IsChildOf(socketsRoot))
                {
                    report.Add("SOCKET_OUTSIDE_ROOT", MapAuthoringIssueSeverity.Error, $"{marker.name} はSockets配下へ置いてください。", marker);
                    continue;
                }

                if (!IsFinitePositive(marker.Size.x) || !IsFinitePositive(marker.Size.y))
                {
                    report.Add("SOCKET_SIZE_INVALID", MapAuthoringIssueSeverity.Error, $"{marker.name} のSizeは0より大きい有限値にしてください。", marker);
                    continue;
                }

                var worldBounds = marker.WorldBounds;
                if (worldBounds.size.x <= GeometryEpsilon || worldBounds.size.y <= GeometryEpsilon)
                {
                    report.Add("SOCKET_WORLD_SIZE_INVALID", MapAuthoringIssueSeverity.Error, $"{marker.name} のTransform ScaleによりWorld範囲が失われています。", marker);
                    continue;
                }

                validMarkers.Add(marker);
                if (groundCorners != null && !IsSocketInsideGround(marker, groundCorners))
                {
                    report.Add("SOCKET_OUT_OF_BOUNDS", MapAuthoringIssueSeverity.Error, $"{marker.name} の範囲がGroundTilemap外へ出ています。", marker);
                }

                if (marker.SocketKind == MapSocketKind.Event
                    && groundTilemap != null
                    && IsFinitePositive(marker.InteractionRadius))
                {
                    ValidateEventSocketReachability(
                        marker,
                        groundTilemap,
                        mapCollisionColliders,
                        report);
                }
            }

            for (var firstIndex = 0; firstIndex < validMarkers.Count; firstIndex++)
            {
                for (var secondIndex = firstIndex + 1; secondIndex < validMarkers.Count; secondIndex++)
                {
                    var first = validMarkers[firstIndex];
                    var second = validMarkers[secondIndex];
                    if (SocketsOverlap(first, second))
                    {
                        report.Add(
                            "SOCKET_OVERLAP",
                            MapAuthoringIssueSeverity.Error,
                            $"Socket範囲が重複しています: {first.name} / {second.name}",
                            second);
                    }
                }
            }
        }

        /// <summary>
        /// Eventだけが定義ID・ScriptableObject参照・接近半径を持ち、他種別へ混入しないことを確認する。
        /// </summary>
        private static void ValidateEventSocketContract(
            MapSocketMarker marker,
            MapAuthoringValidationReport report)
        {
            var hasDefinitionReference = marker.EventDefinition != null;
            var hasSerializedDefinitionId = !string.IsNullOrWhiteSpace(marker.SerializedEventDefinitionId);
            var hasRadiusOverride = marker.InteractionRadiusOverride != 0f;

            if (marker.SocketKind != MapSocketKind.Event)
            {
                if (hasDefinitionReference || hasSerializedDefinitionId || hasRadiusOverride)
                {
                    report.Add(
                        "SOCKET_EVENT_FIELDS_ON_NON_EVENT",
                        MapAuthoringIssueSeverity.Error,
                        $"Event以外のSocket {marker.name} にEvent定義情報を設定しないでください。",
                        marker);
                }

                return;
            }

            if (!hasDefinitionReference && !hasSerializedDefinitionId)
            {
                report.Add(
                    "SOCKET_EVENT_DEFINITION_MISSING",
                    MapAuthoringIssueSeverity.Error,
                    $"Event Socket {marker.name} にEventDefinition参照または定義IDがありません。",
                    marker);
            }

            if (hasDefinitionReference
                && hasSerializedDefinitionId
                && !string.Equals(
                    marker.SerializedEventDefinitionId.Trim(),
                    marker.EventDefinition.EventDefinitionId.Trim(),
                    StringComparison.Ordinal))
            {
                report.Add(
                    "SOCKET_EVENT_DEFINITION_MISMATCH",
                    MapAuthoringIssueSeverity.Error,
                    $"Event Socket {marker.name} の定義IDとScriptableObject参照が一致しません。",
                    marker);
            }

            if (!IsFiniteNonNegative(marker.InteractionRadiusOverride))
            {
                report.Add(
                    "SOCKET_EVENT_RADIUS_OVERRIDE_INVALID",
                    MapAuthoringIssueSeverity.Error,
                    $"Event Socket {marker.name} の接近半径上書きは0以上の有限値にしてください。",
                    marker);
            }

            // 直接参照がある場合は定義の初期値まで検証できる。IDだけの生成データはCatalog解決時に初期値を得るため、ここではID契約を正本とする。
            if ((hasDefinitionReference || (!hasDefinitionReference && !hasSerializedDefinitionId))
                && !IsFinitePositive(marker.InteractionRadius))
            {
                report.Add(
                    "SOCKET_EVENT_RADIUS_INVALID",
                    MapAuthoringIssueSeverity.Error,
                    $"Event Socket {marker.name} の有効な接近判定半径がありません。",
                    marker);
            }
        }

        /// <summary>
        /// Event中心自体は水や表示物上でも許可し、接近半径内にPlayerの足元を置ける地点があるか検査する。
        /// </summary>
        private static void ValidateEventSocketReachability(
            MapSocketMarker marker,
            Tilemap groundTilemap,
            IReadOnlyList<Collider2D> mapCollisionColliders,
            MapAuthoringValidationReport report)
        {
            if (HasReachablePointWithinEventRadius(
                    marker.transform.position,
                    marker.InteractionRadius,
                    groundTilemap,
                    mapCollisionColliders))
            {
                return;
            }

            report.Add(
                "SOCKET_EVENT_NO_REACHABLE_POINT",
                MapAuthoringIssueSeverity.Error,
                $"Event Socket {marker.name} の接近範囲内に、Playerが立てる地面がありません。",
                marker);
        }

        /// <summary>
        /// Event円内を細かく標本化し、Ground上かつMapCollisionから足元幅ぶん離れた候補を探す。
        /// </summary>
        private static bool HasReachablePointWithinEventRadius(
            Vector3 socketPosition,
            float interactionRadius,
            Tilemap groundTilemap,
            IReadOnlyList<Collider2D> mapCollisionColliders)
        {
            var sampleExtent = Mathf.CeilToInt(interactionRadius / EventReachabilitySampleStep);
            var radiusSquared = interactionRadius * interactionRadius;
            for (var yIndex = -sampleExtent; yIndex <= sampleExtent; yIndex++)
            {
                for (var xIndex = -sampleExtent; xIndex <= sampleExtent; xIndex++)
                {
                    var offset = new Vector2(
                        xIndex * EventReachabilitySampleStep,
                        yIndex * EventReachabilitySampleStep);
                    if (offset.sqrMagnitude > radiusSquared + GeometryEpsilon)
                    {
                        continue;
                    }

                    var candidate = (Vector2)socketPosition + offset;
                    if (!groundTilemap.HasTile(groundTilemap.WorldToCell(candidate))
                        || !HasPlayerFootClearance(candidate, mapCollisionColliders))
                    {
                        continue;
                    }

                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 足元中心と周囲8点がCollision内に入らないことを調べ、点だけ通る狭い隙間を候補から除外する。
        /// </summary>
        private static bool HasPlayerFootClearance(
            Vector2 candidate,
            IReadOnlyList<Collider2D> mapCollisionColliders)
        {
            var diagonal = EventReachabilityClearance * 0.70710678f;
            var offsets = new[]
            {
                Vector2.zero,
                Vector2.left * EventReachabilityClearance,
                Vector2.right * EventReachabilityClearance,
                Vector2.up * EventReachabilityClearance,
                Vector2.down * EventReachabilityClearance,
                new Vector2(-diagonal, -diagonal),
                new Vector2(-diagonal, diagonal),
                new Vector2(diagonal, -diagonal),
                new Vector2(diagonal, diagonal)
            };

            for (var colliderIndex = 0; colliderIndex < mapCollisionColliders.Count; colliderIndex++)
            {
                var collider = mapCollisionColliders[colliderIndex];
                for (var offsetIndex = 0; offsetIndex < offsets.Length; offsetIndex++)
                {
                    if (collider.OverlapPoint(candidate + offsets[offsetIndex]))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        /// <summary>
        /// Event到達可能性の検査対象を、MapRoot内の有効なMapCollision Colliderだけへ限定する。
        /// </summary>
        private static List<Collider2D> GetMapCollisionColliders(
            Transform mapRoot,
            int mapCollisionLayer)
        {
            var result = new List<Collider2D>();
            if (mapCollisionLayer < 0)
            {
                return result;
            }

            var colliders = mapRoot.GetComponentsInChildren<Collider2D>(true);
            for (var index = 0; index < colliders.Length; index++)
            {
                var collider = colliders[index];
                if (collider != null
                    && collider.enabled
                    && collider.gameObject.activeInHierarchy
                    && collider.gameObject.layer == mapCollisionLayer)
                {
                    result.Add(collider);
                }
            }

            return result;
        }

        /// <summary>
        /// GroundTilemapの使用Cell Boundsを、回転やScaleを反映したWorld四隅へ変換する。
        /// </summary>
        private static Vector3[] GetGroundWorldCorners(Tilemap groundTilemap)
        {
            var bounds = groundTilemap.cellBounds;
            return new[]
            {
                groundTilemap.CellToWorld(new Vector3Int(bounds.xMin, bounds.yMin, 0)),
                groundTilemap.CellToWorld(new Vector3Int(bounds.xMin, bounds.yMax, 0)),
                groundTilemap.CellToWorld(new Vector3Int(bounds.xMax, bounds.yMax, 0)),
                groundTilemap.CellToWorld(new Vector3Int(bounds.xMax, bounds.yMin, 0))
            };
        }

        /// <summary>
        /// Socketの全四隅がGround矩形の境界内に含まれるかを判定する。
        /// </summary>
        private static bool IsSocketInsideGround(MapSocketMarker marker, Vector3[] groundCorners)
        {
            var socketCorners = marker.GetWorldCorners();
            for (var i = 0; i < socketCorners.Length; i++)
            {
                if (!IsPointInsideConvexQuad(socketCorners[i], groundCorners))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// 点が凸四角形の全辺で同じ側にあるかを調べ、境界上を内側として扱う。
        /// </summary>
        private static bool IsPointInsideConvexQuad(Vector3 point, Vector3[] corners)
        {
            var hasPositive = false;
            var hasNegative = false;
            for (var i = 0; i < corners.Length; i++)
            {
                var start = corners[i];
                var end = corners[(i + 1) % corners.Length];
                var cross = (end.x - start.x) * (point.y - start.y)
                    - (end.y - start.y) * (point.x - start.x);
                hasPositive |= cross > GeometryEpsilon;
                hasNegative |= cross < -GeometryEpsilon;
                if (hasPositive && hasNegative)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// 一方の矩形の各辺法線に投影し、二つのSocketを分離する軸が存在するかを調べる。
        /// </summary>
        private static bool HasSeparatingAxis(Vector3[] axisSource, Vector3[] firstCorners, Vector3[] secondCorners)
        {
            for (var i = 0; i < axisSource.Length; i++)
            {
                var edge = axisSource[(i + 1) % axisSource.Length] - axisSource[i];
                var axis = new Vector2(-edge.y, edge.x);
                if (axis.sqrMagnitude <= GeometryEpsilon * GeometryEpsilon)
                {
                    continue;
                }

                axis.Normalize();
                ProjectCorners(firstCorners, axis, out var firstMin, out var firstMax);
                ProjectCorners(secondCorners, axis, out var secondMin, out var secondMax);
                if (firstMax <= secondMin + GeometryEpsilon || secondMax <= firstMin + GeometryEpsilon)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 四隅を指定軸へ投影し、矩形が占める一次元区間を返す。
        /// </summary>
        private static void ProjectCorners(Vector3[] corners, Vector2 axis, out float minimum, out float maximum)
        {
            minimum = Vector2.Dot(corners[0], axis);
            maximum = minimum;
            for (var i = 1; i < corners.Length; i++)
            {
                var projection = Vector2.Dot(corners[i], axis);
                minimum = Mathf.Min(minimum, projection);
                maximum = Mathf.Max(maximum, projection);
            }
        }

        /// <summary>
        /// Socket Sizeに使える、0より大きい有限値かを判定する。
        /// </summary>
        private static bool IsFinitePositive(float value)
        {
            return value > 0f && !float.IsNaN(value) && !float.IsInfinity(value);
        }

        /// <summary>
        /// Socket固有上書きに使える、0を含む有限値かを判定する。
        /// </summary>
        private static bool IsFiniteNonNegative(float value)
        {
            return value >= 0f && !float.IsNaN(value) && !float.IsInfinity(value);
        }

        /// <summary>
        /// 親の直下だけを名前で検索し、深い階層の同名Objectを誤って受理しないようにする。
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
        /// Reportの全問題をContext付きでConsoleへ出し、問題がない場合は成功を一行で通知する。
        /// </summary>
        private static void LogReport(Scene scene, MapAuthoringValidationReport report)
        {
            if (report.Issues.Count == 0)
            {
                Debug.Log($"Map Authoring validation passed: {scene.path}");
                return;
            }

            for (var i = 0; i < report.Issues.Count; i++)
            {
                var issue = report.Issues[i];
                var message = $"[MapAuthoring:{issue.Code}] {issue.Message}";
                if (issue.Severity == MapAuthoringIssueSeverity.Error)
                {
                    Debug.LogError(message, issue.Context);
                }
                else
                {
                    Debug.LogWarning(message, issue.Context);
                }
            }

            Debug.Log($"Map Authoring validation finished: {report.Issues.Count} issue(s), errors={report.HasErrors}");
        }
    }
}
