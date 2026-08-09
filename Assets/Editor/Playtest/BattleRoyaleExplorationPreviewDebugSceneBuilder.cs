using System;
using System.Collections.Generic;
using System.Linq;
using FantasyRoyale.Gameplay.Events;
using FantasyRoyale.MapAuthoringKit;
using FantasyRoyale.Playtest;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace FantasyRoyale.Editor.Playtest
{
    /// <summary>
    /// 基準Mapを変更せずに加算読込する、探索確認専用Playtest Sceneを決定的に再生成する。
    /// </summary>
    public static class BattleRoyaleExplorationPreviewDebugSceneBuilder
    {
        public const string PlaytestScenePath =
            "Assets/Scenes/Playtest/GbaForestBattleRoyaleExplorationPreviewDebug.unity";

        public const string InputActionsPath = "Assets/InputSystem_Actions.inputactions";
        public const string EventCatalogPath =
            "Assets/Data/MapAuthoring/Events/MapEventCatalog.asset";
        public const string EventPoolPath =
            "Assets/Data/Gameplay/Events/BattleRoyaleReferenceEventPool.asset";
        public const int PreviewEventSelectionSeed = 20260807;

        /// <summary>
        /// 空のPlaytest SceneへBootstrapだけを保存し、PlaytestとReference MapをBuild Settingsへ登録する。
        /// </summary>
        [MenuItem("FantasyRoyale/Playtest/Build Exploration Preview Debug Scene")]
        public static void BuildPlaytestScene()
        {
            // Scene生成で現在の編集内容を失わないよう、対話実行では保存確認と元のScene構成復元を行う。
            if (!Application.isBatchMode
                && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                Debug.Log("Exploration Preview Debug Scene build canceled.");
                return;
            }

            var previousSceneSetup = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                BuildPlaytestSceneInternal();
            }
            finally
            {
                if (!Application.isBatchMode && CanRestoreSceneSetup(previousSceneSetup))
                {
                    EditorSceneManager.RestoreSceneManagerSetup(previousSceneSetup);
                }
            }
        }

        /// <summary>
        /// 保存確認後に生成、Build Settings登録、再読込検査を一括実行する。
        /// </summary>
        private static void BuildPlaytestSceneInternal()
        {
            if (!System.IO.File.Exists(BattleRoyaleExplorationPreviewDebug.DefaultMapScenePath))
            {
                throw new InvalidOperationException(
                    $"Reference Map Sceneがありません: {BattleRoyaleExplorationPreviewDebug.DefaultMapScenePath}");
            }

            EnsureAssetFolder("Assets/Scenes", "Playtest");
            var inputActions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath);
            if (inputActions == null)
            {
                throw new InvalidOperationException($"InputActionAssetがありません: {InputActionsPath}");
            }

            var eventCatalog = AssetDatabase.LoadAssetAtPath<MapEventCatalog>(EventCatalogPath);
            var catalogError = string.Empty;
            if (eventCatalog == null || !eventCatalog.Validate(out catalogError))
            {
                throw new InvalidOperationException(
                    $"MapEventCatalogがありません、または不正です: {EventCatalogPath} / {catalogError}");
            }

            var eventPool = LoadOrCreateEventPool(eventCatalog);

            if (inputActions.FindAction(
                    $"{BattleRoyaleExplorationPreviewDebug.PlayerActionMapName}/"
                    + BattleRoyaleExplorationPreviewDebug.MoveActionName,
                    false) == null)
            {
                throw new InvalidOperationException("Player/Move Input Actionがありません。");
            }

            if (inputActions.FindAction(
                    $"{BattleRoyaleExplorationPreviewDebug.PlayerActionMapName}/"
                    + BattleRoyaleExplorationPreviewDebug.InteractActionName,
                    false) == null)
            {
                throw new InvalidOperationException("Player/Interact Input Actionがありません。");
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("PlaytestRoot");
            SceneManager.MoveGameObjectToScene(root, scene);

            var bootstrapObject = new GameObject("BattleRoyaleExplorationPreviewDebug");
            bootstrapObject.transform.SetParent(root.transform, false);
            var bootstrap = bootstrapObject.AddComponent<BattleRoyaleExplorationPreviewDebug>();
            bootstrap.Configure(
                BattleRoyaleExplorationPreviewDebug.DefaultMapScenePath,
                inputActions,
                5f,
                8f,
                eventCatalog,
                50,
                100,
                eventPool,
                PreviewEventSelectionSeed);

            if (!EditorSceneManager.SaveScene(scene, PlaytestScenePath, false))
            {
                throw new InvalidOperationException($"Playtest Sceneを保存できません: {PlaytestScenePath}");
            }

            EnsureBuildSettingsScenes(
                BattleRoyaleExplorationPreviewDebug.DefaultMapScenePath,
                PlaytestScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            ValidateGeneratedScene();
            Debug.Log($"Exploration Preview Debug Scene built: {PlaytestScenePath}");
        }

        /// <summary>
        /// 初回だけ泉一種・重み1・有効3地点のPool Assetを作り、以後はInspector編集値を正本として検証する。
        /// </summary>
        private static MapEventPoolDefinition LoadOrCreateEventPool(MapEventCatalog eventCatalog)
        {
            var eventPool = AssetDatabase.LoadAssetAtPath<MapEventPoolDefinition>(EventPoolPath);
            if (eventPool == null)
            {
                if (!eventCatalog.TryGet("healing-fountain-basic", out var fountainDefinition))
                {
                    throw new InvalidOperationException(
                        "MapEventCatalogにhealing-fountain-basicがありません。");
                }

                EnsureAssetFolder("Assets/Data", "Gameplay");
                EnsureAssetFolder("Assets/Data/Gameplay", "Events");
                eventPool = ScriptableObject.CreateInstance<MapEventPoolDefinition>();
                eventPool.name = "BattleRoyaleReferenceEventPool";
                eventPool.Configure(
                    "battle-royale-reference-events",
                    3,
                    new[]
                    {
                        new MapEventPoolEntry(fountainDefinition, 1)
                    });
                AssetDatabase.CreateAsset(eventPool, EventPoolPath);
            }

            if (!eventPool.Validate(out var poolError))
            {
                throw new InvalidOperationException(
                    $"MapEventPoolDefinitionが不正です: {EventPoolPath} / {poolError}");
            }

            return eventPool;
        }

        /// <summary>
        /// 保存済みSceneだけで構成された元の編集状態かを判定し、安全に復元できる場合だけ戻す。
        /// </summary>
        private static bool CanRestoreSceneSetup(SceneSetup[] sceneSetup)
        {
            return sceneSetup != null
                   && sceneSetup.Length > 0
                   && sceneSetup.All(setup => !string.IsNullOrEmpty(setup.path));
        }

        /// <summary>
        /// 既存のBuild Settings順を保ち、Runtime Loadに必要なSceneだけを重複なく有効化する。
        /// </summary>
        private static void EnsureBuildSettingsScenes(params string[] requiredPaths)
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            for (var requiredIndex = 0; requiredIndex < requiredPaths.Length; requiredIndex++)
            {
                var requiredPath = requiredPaths[requiredIndex];
                var existingIndex = scenes.FindIndex(scene =>
                    string.Equals(scene.path, requiredPath, StringComparison.Ordinal));
                if (existingIndex >= 0)
                {
                    scenes[existingIndex] = new EditorBuildSettingsScene(requiredPath, true);
                }
                else
                {
                    scenes.Add(new EditorBuildSettingsScene(requiredPath, true));
                }
            }

            EditorBuildSettings.scenes = scenes.ToArray();
        }

        /// <summary>
        /// Unity Asset Folderがなければ一階層だけ作り、OS依存の直接File作成を避ける。
        /// </summary>
        private static void EnsureAssetFolder(string parentPath, string folderName)
        {
            var fullPath = $"{parentPath}/{folderName}";
            if (!AssetDatabase.IsValidFolder(fullPath))
            {
                AssetDatabase.CreateFolder(parentPath, folderName);
            }
        }

        /// <summary>
        /// 保存済みSceneがBootstrap一つだけを持ち、入力とMap参照が欠けていないことを再読込して確認する。
        /// </summary>
        private static void ValidateGeneratedScene()
        {
            var scene = EditorSceneManager.OpenScene(PlaytestScenePath, OpenSceneMode.Single);
            var roots = scene.GetRootGameObjects();
            var bootstrap = roots
                .SelectMany(root => root.GetComponentsInChildren<BattleRoyaleExplorationPreviewDebug>(true))
                .SingleOrDefault();
            if (bootstrap == null)
            {
                throw new InvalidOperationException("生成Playtest SceneにBootstrapが一つ存在しません。");
            }

            if (bootstrap.InputActions == null
                || bootstrap.EventCatalog == null
                || bootstrap.EventPool == null
                || bootstrap.EventSelectionSeed != PreviewEventSelectionSeed
                || !string.Equals(
                    bootstrap.MapScenePath,
                    BattleRoyaleExplorationPreviewDebug.DefaultMapScenePath,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException("生成Playtest Sceneの入力またはMap設定が不正です。");
            }

            var enabledBuildScenes = EditorBuildSettings.scenes
                .Where(buildScene => buildScene.enabled)
                .Select(buildScene => buildScene.path)
                .ToHashSet(StringComparer.Ordinal);
            if (!enabledBuildScenes.Contains(PlaytestScenePath)
                || !enabledBuildScenes.Contains(BattleRoyaleExplorationPreviewDebug.DefaultMapScenePath))
            {
                throw new InvalidOperationException("Playtest / Reference MapがBuild Settingsで有効ではありません。");
            }
        }
    }
}
