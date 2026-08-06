using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

namespace FantasyRoyale.Editor.MapAuthoring
{
    /// <summary>
    /// GBA風森林マップを編集し始めるための標準Sceneを、素材参照なしで再現可能に構築する。
    /// テンプレートSceneだけを作り直し、現在開いている他のSceneには手を加えない。
    /// </summary>
    public static class GbaForestMapAuthoringSceneBuilder
    {
        public const string ScenePath = "Assets/Scenes/MapAuthoring/GbaForestMapAuthoringTemplate.unity";

        private const string RootName = "MapRoot";
        private const string GridName = "Grid";
        private const string DefaultLayerName = "Default";
        private const string CollisionLayerName = "MapCollision";
        private const string InteractionLayerName = "MapInteraction";
        private const string GroundSortingLayerName = "MapGround";
        private const string ForegroundSortingLayerName = "MapForeground";

        /// <summary>
        /// 空のTilemap階層、配置物Root、Socket Root、確認用Cameraを持つテンプレートを生成または更新する。
        /// </summary>
        [MenuItem("FantasyRoyale/Map Authoring/Create or Refresh GBA Forest Template")]
        public static void BuildOrRefreshTemplate()
        {
            EnsureAssetFolder("Assets/Scenes/MapAuthoring");

            var previousActiveScene = SceneManager.GetActiveScene();
            var templateWasAlreadyLoaded = TryGetLoadedTemplateScene(out var templateScene);
            var replacedUntitledScene = false;

            if (!templateWasAlreadyLoaded)
            {
                var sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
                replacedUntitledScene = previousActiveScene.IsValid()
                    && previousActiveScene.isLoaded
                    && string.IsNullOrEmpty(previousActiveScene.path);

                if (replacedUntitledScene
                    && !Application.isBatchMode
                    && (previousActiveScene.isDirty || SceneManager.sceneCount > 1))
                {
                    throw new InvalidOperationException(
                        "Save or close the current untitled scene and any additional scenes before creating the map authoring template.");
                }

                var openMode = replacedUntitledScene ? OpenSceneMode.Single : OpenSceneMode.Additive;
                var newSceneMode = replacedUntitledScene ? NewSceneMode.Single : NewSceneMode.Additive;
                templateScene = sceneAsset != null
                    ? EditorSceneManager.OpenScene(ScenePath, openMode)
                    : EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, newSceneMode);
            }

            try
            {
                SceneManager.SetActiveScene(templateScene);

                // 既存Objectを名前で再利用し、再実行時もScene内fileIDと差分を安定させる。
                RefreshAuthoringHierarchy(templateScene);

                EditorSceneManager.MarkSceneDirty(templateScene);
                if (!EditorSceneManager.SaveScene(templateScene, ScenePath))
                {
                    throw new InvalidOperationException($"Failed to save map authoring template: {ScenePath}");
                }

                AssetDatabase.ImportAsset(ScenePath, ImportAssetOptions.ForceUpdate);
                Debug.Log($"GBA forest map authoring template refreshed: {ScenePath}");
            }
            finally
            {
                RestorePreviouslyActiveScene(previousActiveScene, templateScene);

                // この処理のためだけに開いたSceneは閉じ、利用者の現在のScene構成を保つ。
                if (!templateWasAlreadyLoaded
                    && !replacedUntitledScene
                    && templateScene.IsValid()
                    && templateScene.isLoaded)
                {
                    EditorSceneManager.CloseScene(templateScene, true);
                }
            }
        }

        /// <summary>
        /// 既にテンプレートが開かれている場合は、そのSceneを再利用する。
        /// </summary>
        private static bool TryGetLoadedTemplateScene(out Scene templateScene)
        {
            templateScene = SceneManager.GetSceneByPath(ScenePath);
            return templateScene.IsValid() && templateScene.isLoaded;
        }

        /// <summary>
        /// テンプレートScene内の標準Rootを再利用し、余分なRootだけを除外する。
        /// </summary>
        private static GameObject FindOrCreateTemplateRoot(Scene templateScene)
        {
            var roots = templateScene.GetRootGameObjects();
            GameObject root = null;

            for (var i = 0; i < roots.Length; i++)
            {
                if (roots[i].name == RootName && root == null)
                {
                    root = roots[i];
                }
            }

            for (var i = roots.Length - 1; i >= 0; i--)
            {
                if (roots[i] != root)
                {
                    UnityEngine.Object.DestroyImmediate(roots[i]);
                }
            }

            if (root == null)
            {
                root = new GameObject(RootName);
                SceneManager.MoveGameObjectToScene(root, templateScene);
            }

            root.name = RootName;
            root.layer = ResolveGameObjectLayer(DefaultLayerName);
            ResetTransform(root.transform);
            return root;
        }

        /// <summary>
        /// マップ編集で共有する標準Rootと描画順を、素材がなくても成立する空構成として作る。
        /// </summary>
        private static void RefreshAuthoringHierarchy(Scene templateScene)
        {
            var root = FindOrCreateTemplateRoot(templateScene);
            RemoveUnexpectedChildren(
                root.transform,
                GridName,
                "Decorations",
                "ObstacleVisuals",
                "Props",
                "Sockets",
                "Main Camera");

            var gridObject = FindOrCreateChild(root.transform, GridName, DefaultLayerName, 0);
            var grid = GetOrAddComponent<Grid>(gridObject);
            grid.cellLayout = GridLayout.CellLayout.Rectangle;
            grid.cellSize = Vector3.one;
            grid.cellGap = Vector3.zero;
            grid.cellSwizzle = GridLayout.CellSwizzle.XYZ;

            RemoveUnexpectedChildren(
                gridObject.transform,
                "GroundTilemap",
                "TerrainTilemap",
                "CollisionTilemap",
                "ForegroundTilemap");

            CreateOrRefreshTilemap(gridObject.transform, "GroundTilemap", GroundSortingLayerName, 0, 0, false, false);
            CreateOrRefreshTilemap(gridObject.transform, "TerrainTilemap", GroundSortingLayerName, 10, 1, false, false);
            CreateOrRefreshTilemap(gridObject.transform, "CollisionTilemap", string.Empty, 0, 2, false, true);
            CreateOrRefreshTilemap(gridObject.transform, "ForegroundTilemap", ForegroundSortingLayerName, 0, 3, true, false);

            // 表示物はGridの外へ分離し、1px単位の自由配置を許可する。
            var decorations = FindOrCreateChild(root.transform, "Decorations", DefaultLayerName, 1);
            RemoveForbiddenDisplayRootComponents(decorations);
            var obstacleVisuals = FindOrCreateChild(root.transform, "ObstacleVisuals", DefaultLayerName, 2);
            RemoveForbiddenDisplayRootComponents(obstacleVisuals);
            var props = FindOrCreateChild(root.transform, "Props", DefaultLayerName, 3);
            RemoveForbiddenDisplayRootComponents(props);
            var sockets = FindOrCreateChild(root.transform, "Sockets", InteractionLayerName, 4);
            RemoveUnexpectedChildren(decorations.transform);
            RemoveUnexpectedChildren(obstacleVisuals.transform);
            RemoveUnexpectedChildren(props.transform);
            RemoveUnexpectedChildren(sockets.transform);
            CreateOrRefreshAuthoringCamera(root.transform);
        }

        /// <summary>
        /// 指定された表示層を持つTilemapを作り、Collision層だけ物理合成用Componentを追加する。
        /// </summary>
        private static Tilemap CreateOrRefreshTilemap(
            Transform parent,
            string name,
            string sortingLayerName,
            int sortingOrder,
            int siblingIndex,
            bool individualRendering,
            bool addCollision)
        {
            var gameObject = FindOrCreateChild(
                parent,
                name,
                addCollision ? CollisionLayerName : DefaultLayerName,
                siblingIndex);
            var tilemap = GetOrAddComponent<Tilemap>(gameObject);
            tilemap.ClearAllTiles();
            tilemap.tileAnchor = new Vector3(0.5f, 0.5f, 0f);
            tilemap.orientation = Tilemap.Orientation.XY;

            var renderer = GetOrAddComponent<TilemapRenderer>(gameObject);
            renderer.sortingLayerName = ResolveSortingLayerName(sortingLayerName);
            renderer.sortingOrder = sortingOrder;
            renderer.mode = individualRendering
                ? TilemapRenderer.Mode.Individual
                : TilemapRenderer.Mode.Chunk;

            if (addCollision)
            {
                // Collision Tilemapは実画面には描かず、TileのCollider形状だけを合成して使う。
                renderer.enabled = false;

                var body = GetOrAddComponent<Rigidbody2D>(gameObject);
                body.bodyType = RigidbodyType2D.Static;
                body.simulated = true;

                var composite = GetOrAddComponent<CompositeCollider2D>(gameObject);
                composite.geometryType = CompositeCollider2D.GeometryType.Polygons;

                var tilemapCollider = GetOrAddComponent<TilemapCollider2D>(gameObject);
                tilemapCollider.compositeOperation = Collider2D.CompositeOperation.Merge;
            }

            return tilemap;
        }

        /// <summary>
        /// 64x48の初期検証マップ全体を確認しやすい、素材非依存の正投影Cameraを作る。
        /// </summary>
        private static void CreateOrRefreshAuthoringCamera(Transform parent)
        {
            var cameraObject = FindOrCreateChild(parent, "Main Camera", DefaultLayerName, 5);
            cameraObject.tag = "MainCamera";
            cameraObject.transform.localPosition = new Vector3(0f, 0f, -10f);

            var camera = GetOrAddComponent<Camera>(cameraObject);
            camera.orthographic = true;
            camera.orthographicSize = 24f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 100f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color32(20, 32, 28, 255);
            camera.transparencySortMode = TransparencySortMode.CustomAxis;
            camera.transparencySortAxis = Vector3.up;
        }

        /// <summary>
        /// 指定Root直下へ、同じTransform初期値を持つ空Objectを作る。
        /// </summary>
        private static GameObject FindOrCreateChild(
            Transform parent,
            string name,
            string requestedLayerName,
            int siblingIndex)
        {
            GameObject child = null;
            for (var i = 0; i < parent.childCount; i++)
            {
                var candidate = parent.GetChild(i).gameObject;
                if (candidate.name == name && child == null)
                {
                    child = candidate;
                }
            }

            for (var i = parent.childCount - 1; i >= 0; i--)
            {
                var candidate = parent.GetChild(i).gameObject;
                if (candidate.name == name && candidate != child)
                {
                    UnityEngine.Object.DestroyImmediate(candidate);
                }
            }

            if (child == null)
            {
                child = new GameObject(name);
                child.transform.SetParent(parent, false);
            }

            child.name = name;
            child.layer = ResolveGameObjectLayer(requestedLayerName);
            ResetTransform(child.transform);
            child.transform.SetSiblingIndex(siblingIndex);
            return child;
        }

        /// <summary>
        /// 標準Hierarchyに含まれない直下Objectを除き、テンプレート構造を一意に保つ。
        /// </summary>
        private static void RemoveUnexpectedChildren(Transform parent, params string[] expectedNames)
        {
            for (var i = parent.childCount - 1; i >= 0; i--)
            {
                var child = parent.GetChild(i);
                if (Array.IndexOf(expectedNames, child.name) < 0)
                {
                    UnityEngine.Object.DestroyImmediate(child.gameObject);
                }
            }
        }

        /// <summary>
        /// 既存Componentを再利用し、再生成でScene内fileIDを変えない。
        /// </summary>
        private static T GetOrAddComponent<T>(GameObject target) where T : Component
        {
            var component = target.GetComponent<T>();
            return component != null ? component : target.AddComponent<T>();
        }

        /// <summary>
        /// 自由配置Rootへ旧設計や誤操作由来のComponentが残っている場合に取り除き、表示とCell・物理責務を分離する。
        /// </summary>
        private static void RemoveForbiddenDisplayRootComponents(GameObject target)
        {
            var grid = target.GetComponent<Grid>();
            if (grid != null)
            {
                UnityEngine.Object.DestroyImmediate(grid);
            }

            var colliders = target.GetComponents<Collider2D>();
            for (var i = 0; i < colliders.Length; i++)
            {
                UnityEngine.Object.DestroyImmediate(colliders[i]);
            }

            var rigidbody = target.GetComponent<Rigidbody2D>();
            if (rigidbody != null)
            {
                UnityEngine.Object.DestroyImmediate(rigidbody);
            }

            var tilemapRenderer = target.GetComponent<TilemapRenderer>();
            if (tilemapRenderer != null)
            {
                UnityEngine.Object.DestroyImmediate(tilemapRenderer);
            }

            var tilemap = target.GetComponent<Tilemap>();
            if (tilemap != null)
            {
                UnityEngine.Object.DestroyImmediate(tilemap);
            }
        }

        /// <summary>
        /// Projectに専用Layerがまだない場合もコンパイル・生成を止めずDefaultへ戻す。
        /// </summary>
        private static int ResolveGameObjectLayer(string requestedLayerName)
        {
            var requestedLayer = LayerMask.NameToLayer(requestedLayerName);
            return requestedLayer >= 0 ? requestedLayer : LayerMask.NameToLayer(DefaultLayerName);
        }

        /// <summary>
        /// Projectに専用Sorting Layerがない場合はDefaultを使い、Scene生成を素材設定から独立させる。
        /// </summary>
        private static string ResolveSortingLayerName(string requestedSortingLayerName)
        {
            var sortingLayers = SortingLayer.layers;
            for (var i = 0; i < sortingLayers.Length; i++)
            {
                if (sortingLayers[i].name == requestedSortingLayerName)
                {
                    return requestedSortingLayerName;
                }
            }

            return DefaultLayerName;
        }

        /// <summary>
        /// 生成階層の座標差分が積み上がらないよう、Transformを共通初期値へ戻す。
        /// </summary>
        private static void ResetTransform(Transform target)
        {
            target.localPosition = Vector3.zero;
            target.localRotation = Quaternion.identity;
            target.localScale = Vector3.one;
        }

        /// <summary>
        /// テンプレート保存先が未作成でも、AssetDatabase経由で親から順に作る。
        /// </summary>
        private static void EnsureAssetFolder(string assetFolderPath)
        {
            if (AssetDatabase.IsValidFolder(assetFolderPath))
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
            EnsureAssetFolder(parentPath);
            AssetDatabase.CreateFolder(parentPath, folderName);
        }

        /// <summary>
        /// 生成前に利用者が開いていたSceneが残っている場合は、Active Sceneだけ元へ戻す。
        /// </summary>
        private static void RestorePreviouslyActiveScene(Scene previousActiveScene, Scene templateScene)
        {
            if (previousActiveScene.IsValid()
                && previousActiveScene.isLoaded
                && previousActiveScene != templateScene)
            {
                SceneManager.SetActiveScene(previousActiveScene);
            }
        }
    }
}
