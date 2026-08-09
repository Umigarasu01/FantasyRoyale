using System;
using System.Collections;
using System.Collections.Generic;
using FantasyRoyale.Gameplay.Characters;
using FantasyRoyale.Gameplay.Characters.Unity;
using FantasyRoyale.Gameplay.Events;
using FantasyRoyale.MapAuthoringKit;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

namespace FantasyRoyale.Playtest
{
    /// <summary>
    /// 一回のPreview実行中に変化するEventSocket状態。ScriptableObjectへ書き戻さないための値データ。
    /// </summary>
    public readonly struct PreviewDebugMapEventSocketState
    {
        public PreviewDebugMapEventSocketState(bool isUsed)
        {
            IsUsed = isUsed;
        }

        public bool IsUsed { get; }
    }

    /// <summary>
    /// Socket IDをKeyとしてPreview中の使用済み状態を管理する一時Runtime索引。
    /// 定義AssetとScene配置を不変に保ち、Match終了時にまとめて破棄できるようにする。
    /// </summary>
    public sealed class PreviewDebugMapEventRuntimeStateStore : IMapEventRuntimeStateStore
    {
        private readonly Dictionary<string, PreviewDebugMapEventSocketState> states =
            new Dictionary<string, PreviewDebugMapEventSocketState>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, PreviewDebugMapEventSocketState> States => states;

        /// <summary>
        /// 指定Socketが今回のPreviewですでに使用済みかを調べる。
        /// </summary>
        public bool IsUsed(string socketId)
        {
            var normalizedId = NormalizeSocketId(socketId);
            return normalizedId.Length > 0
                   && states.TryGetValue(normalizedId, out var state)
                   && state.IsUsed;
        }

        /// <summary>
        /// 有効なSocket IDを使用済みにし、初回更新だけtrueを返す。
        /// </summary>
        public bool TryMarkUsed(string socketId)
        {
            var normalizedId = NormalizeSocketId(socketId);
            if (normalizedId.Length == 0 || IsUsed(normalizedId))
            {
                return false;
            }

            states[normalizedId] = new PreviewDebugMapEventSocketState(true);
            return true;
        }

        /// <summary>
        /// 共通Event実行サービスが効果成功後にだけ呼び、Socketを冪等に使用済みへ更新する。
        /// </summary>
        public void MarkUsed(string socketId)
        {
            var normalizedId = NormalizeSocketId(socketId);
            if (normalizedId.Length == 0)
            {
                throw new ArgumentException("Socket IDが空です。", nameof(socketId));
            }

            states[normalizedId] = new PreviewDebugMapEventSocketState(true);
        }

        /// <summary>
        /// Scene名ではなく配置固有IDを安定Keyとして使うため、前後空白だけを除去する。
        /// </summary>
        private static string NormalizeSocketId(string socketId)
        {
            return socketId == null ? string.Empty : socketId.Trim();
        }
    }

    /// <summary>
    /// 96x72基準Mapを加算読込し、仮Playerの移動、Collision、Camera追従、Event操作を確認する一時Playtest足場。
    /// 本番のCharacterやMatch進行を確定せず、Map探索とEvent契約に必要な最小接続を検証する。
    /// </summary>
    [AddComponentMenu("FantasyRoyale/Playtest/Battle Royale Exploration Preview Debug")]
    [DisallowMultipleComponent]
    public sealed class BattleRoyaleExplorationPreviewDebug : MonoBehaviour
    {
        public const string DefaultMapScenePath =
            "Assets/Scenes/MapAuthoring/GbaForestBattleRoyaleReference.unity";

        public const string PlayerActionMapName = "Player";
        public const string MoveActionName = "Move";
        public const string InteractActionName = "Interact";
        public const string WorldObjectSortingLayerName = "WorldObjects";

        private const float PixelsPerUnit = 32f;
        private const string GroundTilemapName = "GroundTilemap";
        private const string CollisionTilemapName = "CollisionTilemap";

        [SerializeField] private string mapScenePath = DefaultMapScenePath;
        [SerializeField] private InputActionAsset inputActions;
        [SerializeField] private MapEventCatalog eventCatalog;
        [SerializeField] private MapEventPoolDefinition eventPool;
        [SerializeField] private int previewEventSelectionSeed = 20260807;
        [SerializeField, Min(0.1f)] private float moveSpeed = 5f;
        [SerializeField, Min(1f)] private float cameraOrthographicSize = 8f;
        [SerializeField, Min(1)] private int previewMaximumHealth = 100;
        [SerializeField, Min(0)] private int previewStartingHealth = 50;

        private readonly List<MapSocketMarker> socketBuffer = new List<MapSocketMarker>();
        private readonly List<MapSocketMarker> eventSocketBuffer = new List<MapSocketMarker>();
        private readonly List<MapSocketMarker> poolCandidateSocketBuffer = new List<MapSocketMarker>();
        private readonly Dictionary<MapSocketMarker, MapEventDefinition> resolvedEventDefinitions =
            new Dictionary<MapSocketMarker, MapEventDefinition>();
        private readonly List<Collider2D> spawnOverlapBuffer = new List<Collider2D>();
        private readonly PreviewDebugMapEventRuntimeStateStore eventRuntimeState =
            new PreviewDebugMapEventRuntimeStateStore();
        private readonly PreviewDebugMapEventPresenter eventPresenter =
            new PreviewDebugMapEventPresenter();
        private Scene mapScene;
        private Camera gameplayCamera;
        private GameObject playerObject;
        private CharacterActor2D playerActor;
        private Rigidbody2D playerBody;
        private CapsuleCollider2D playerCollider;
        private InputActionAsset runtimeInputActions;
        private HumanCharacterMoveInputAdapter humanMoveInput;
        private InputAction interactAction;
        private Tilemap groundTilemap;
        private Tilemap collisionTilemap;
        private Bounds mapWorldBounds;
        private Texture2D previewTexture;
        private Sprite previewSprite;
        private CharacterMoveCommand latestMoveCommand;
        private bool hasTestMoveOverride;
        private CharacterMoveCommand testMoveCommand;
        private float readyAtTime;
        private MapEventExecutionService eventExecutionService;
        private MapSocketMarker currentEventSocket;
        private string lastEventFeedback = string.Empty;
        private MapEventExecutionResult lastEventExecutionResult;
        private MapEventPlacementPlan eventPlacementPlan =
            new MapEventPlacementPlan(Array.Empty<MapEventPlacementAssignment>());
        private int allEventSocketCount;

        public bool IsReady { get; private set; }
        public string FailureMessage { get; private set; } = string.Empty;
        public string MapScenePath => mapScenePath;
        public InputActionAsset InputActions => inputActions;
        public MapEventCatalog EventCatalog => eventCatalog;
        public MapEventPoolDefinition EventPool => eventPool;
        public int EventSelectionSeed => previewEventSelectionSeed;
        public float MoveSpeed => moveSpeed;
        public float CameraOrthographicSize => cameraOrthographicSize;
        public Scene LoadedMapScene => mapScene;
        public Camera GameplayCamera => gameplayCamera;
        public GameObject PlayerObject => playerObject;
        public CharacterActor2D PlayerActor => playerActor;
        public Rigidbody2D PlayerBody => playerBody;
        public CapsuleCollider2D PlayerCollider => playerCollider;
        public InputActionAsset RuntimeInputActions => runtimeInputActions;
        public Tilemap CollisionTilemap => collisionTilemap;
        public Bounds MapWorldBounds => mapWorldBounds;
        public CharacterMoveCommand LatestMoveCommand => latestMoveCommand;
        public Vector2 LatestMoveInput => new Vector2(
            latestMoveCommand.Horizontal,
            latestMoveCommand.Vertical);
        public int CurrentHealth => playerActor == null || playerActor.Health == null
            ? 0
            : playerActor.Health.CurrentHealth;
        public int MaximumHealth => playerActor == null || playerActor.Health == null
            ? previewMaximumHealth
            : playerActor.Health.MaximumHealth;
        public MapSocketMarker CurrentEventSocket => currentEventSocket;
        public string CurrentEventPrompt => currentEventSocket != null
            && resolvedEventDefinitions.TryGetValue(currentEventSocket, out var definition)
                ? definition.PromptText
                : string.Empty;
        public string LastEventFeedback => lastEventFeedback;
        public MapEventExecutionResult LastEventExecutionResult => lastEventExecutionResult;
        public MapEventPresentationRequest LastEventPresentationRequest => eventPresenter.LastRequest;
        public int RegisteredEventHandlerCount => eventExecutionService == null
            ? 0
            : eventExecutionService.RegisteredHandlerCount;
        public PreviewDebugMapEventRuntimeStateStore EventRuntimeState => eventRuntimeState;
        public int AllEventSocketCount => allEventSocketCount;
        public int ActiveEventSocketCount => eventSocketBuffer.Count;
        public MapEventPlacementPlan EventPlacementPlan => eventPlacementPlan;
        public IReadOnlyDictionary<MapSocketMarker, MapEventDefinition> ResolvedEventDefinitions =>
            resolvedEventDefinitions;

        /// <summary>
        /// Editor Builderから、読込Scene、入力Asset、仮移動速度、Camera表示範囲をまとめて設定する。
        /// </summary>
        public void Configure(
            string newMapScenePath,
            InputActionAsset newInputActions,
            float newMoveSpeed,
            float newCameraOrthographicSize,
            MapEventCatalog newEventCatalog = null,
            int newPreviewStartingHealth = 50,
            int newPreviewMaximumHealth = 100,
            MapEventPoolDefinition newEventPool = null,
            int newPreviewEventSelectionSeed = 20260807)
        {
            mapScenePath = string.IsNullOrWhiteSpace(newMapScenePath)
                ? DefaultMapScenePath
                : newMapScenePath;
            inputActions = newInputActions;
            moveSpeed = Mathf.Max(0.1f, newMoveSpeed);
            cameraOrthographicSize = Mathf.Max(1f, newCameraOrthographicSize);
            eventCatalog = newEventCatalog;
            eventPool = newEventPool;
            previewEventSelectionSeed = newPreviewEventSelectionSeed;
            previewMaximumHealth = Mathf.Max(1, newPreviewMaximumHealth);
            previewStartingHealth = Mathf.Clamp(
                newPreviewStartingHealth,
                0,
                previewMaximumHealth);
        }

        /// <summary>
        /// PlayMode TestがKeyboard入力と物理移動を分けて検査できるよう、移動入力値を一時的に上書きする。
        /// </summary>
        public void SetMoveInputOverrideForTests(Vector2 moveInput)
        {
            hasTestMoveOverride = true;
            testMoveCommand = new CharacterMoveCommand(moveInput.x, moveInput.y);
            if (playerActor != null)
            {
                playerActor.SetMoveCommand(testMoveCommand);
            }
        }

        /// <summary>
        /// Test用の移動入力値上書きを解除し、Input SystemのMove Actionへ戻す。
        /// </summary>
        public void ClearMoveInputOverrideForTests()
        {
            hasTestMoveOverride = false;
            testMoveCommand = CharacterMoveCommand.None;
            if (playerActor != null)
            {
                playerActor.SetMoveCommand(latestMoveCommand);
            }
        }

        /// <summary>
        /// PlayMode Testから現在の接近候補へ操作を発火し、Keyboard検証と効果検証を分離する。
        /// </summary>
        public bool TryInteractWithCurrentEventForTests()
        {
            return TryInteractWithCurrentEvent();
        }

        /// <summary>
        /// PlayMode Testが回復成功、満タン、撃破を同じSceneで確認できるよう、本番HP状態を再注入する。
        /// </summary>
        public void SetPreviewHealthForTests(int health)
        {
            if (playerActor == null || playerActor.Health == null)
            {
                return;
            }

            var maximumHealth = playerActor.Health.MaximumHealth;
            playerActor.Initialize(
                new CharacterHealth(
                    maximumHealth,
                    Mathf.Clamp(health, 0, maximumHealth)),
                moveSpeed);
        }

        /// <summary>
        /// Playtest Scene開始後にReference Mapを加算読込し、Scene内の制作情報を変更せず探索足場を構築する。
        /// </summary>
        private IEnumerator Start()
        {
            if (!ValidateConfiguration())
            {
                yield break;
            }

            mapScene = SceneManager.GetSceneByPath(mapScenePath);
            if (!mapScene.IsValid() || !mapScene.isLoaded)
            {
                var loadOperation = SceneManager.LoadSceneAsync(mapScenePath, LoadSceneMode.Additive);
                if (loadOperation == null)
                {
                    Fail($"Reference Mapを読込開始できません: {mapScenePath}");
                    yield break;
                }

                while (!loadOperation.isDone)
                {
                    yield return null;
                }

                mapScene = SceneManager.GetSceneByPath(mapScenePath);
            }

            if (!mapScene.IsValid() || !mapScene.isLoaded)
            {
                Fail($"Reference Mapの加算読込に失敗しました: {mapScenePath}");
                yield break;
            }

            if (!ResolveMapComponents())
            {
                yield break;
            }

            var playerStart = FindUniquePlayerStart();
            if (playerStart == null)
            {
                yield break;
            }

            CreatePreviewPlayer(playerStart.transform.position);
            if (!ValidateSpawnClearance())
            {
                yield break;
            }

            if (!ResolveEventSockets())
            {
                yield break;
            }

            InitializeEventRuntime();
            ConfigureGameplayCamera();
            IsReady = true;
            readyAtTime = Time.unscaledTime;
            UpdateCurrentEventSocket();
            Debug.Log(
                $"Exploration Preview Debug ready: player={playerObject.transform.position}, "
                + $"map={mapScene.path}, speed={moveSpeed:0.##}, "
                + $"events={eventSocketBuffer.Count}/{allEventSocketCount}, "
                + $"seed={previewEventSelectionSeed}",
                this);
        }

        /// <summary>
        /// 人間入力Adapterから共通移動Commandを読み、入力元を知らない本番Actorへ渡す。
        /// </summary>
        private void Update()
        {
            if (!IsReady || humanMoveInput == null || playerActor == null)
            {
                latestMoveCommand = CharacterMoveCommand.None;
                if (playerActor != null)
                {
                    playerActor.SetMoveCommand(CharacterMoveCommand.None);
                }

                return;
            }

            latestMoveCommand = humanMoveInput.ReadMoveCommand();
            playerActor.SetMoveCommand(
                hasTestMoveOverride ? testMoveCommand : latestMoveCommand);
            UpdateCurrentEventSocket();
            if (interactAction != null && interactAction.WasPressedThisFrame())
            {
                TryInteractWithCurrentEvent();
            }
        }

        /// <summary>
        /// Physics更新後にCameraをPlayerへ即時追従させ、Map外とsub-pixel位置を避ける。
        /// </summary>
        private void LateUpdate()
        {
            if (!IsReady || gameplayCamera == null || playerObject == null)
            {
                return;
            }

            // Rigidbody interpolation中のTransformはPhysics位置より遅れるため、追従元はBody位置を使う。
            var target = playerBody.position;
            var verticalExtent = gameplayCamera.orthographicSize;
            var horizontalExtent = verticalExtent * gameplayCamera.aspect;
            var x = ClampCameraAxis(
                target.x,
                mapWorldBounds.min.x,
                mapWorldBounds.max.x,
                horizontalExtent);
            var y = ClampCameraAxis(
                target.y,
                mapWorldBounds.min.y,
                mapWorldBounds.max.y,
                verticalExtent);

            gameplayCamera.transform.position = new Vector3(
                SnapToPixelGrid(x),
                SnapToPixelGrid(y),
                gameplayCamera.transform.position.z);
        }

        /// <summary>
        /// 仮PlayerとRuntime生成Spriteを破棄し、PlayMode再開時に入力やTextureが残らないようにする。
        /// </summary>
        private void OnDestroy()
        {
            if (humanMoveInput != null)
            {
                humanMoveInput.Dispose();
                humanMoveInput = null;
            }

            if (interactAction != null)
            {
                interactAction.Disable();
                interactAction = null;
            }

            if (runtimeInputActions != null)
            {
                Destroy(runtimeInputActions);
            }

            if (previewSprite != null)
            {
                Destroy(previewSprite);
            }

            if (previewTexture != null)
            {
                Destroy(previewTexture);
            }
        }

        /// <summary>
        /// Game View上で操作方法、現在位置、失敗内容を確認できる最小HUDを表示する。
        /// </summary>
        private void OnGUI()
        {
            var area = new Rect(12f, 12f, 440f, 190f);
            GUILayout.BeginArea(area, GUI.skin.box);
            GUILayout.Label("Exploration Preview Debug");
            if (!string.IsNullOrEmpty(FailureMessage))
            {
                GUILayout.Label($"ERROR: {FailureMessage}");
            }
            else if (!IsReady)
            {
                GUILayout.Label("Reference Map loading...");
            }
            else
            {
                var position = playerObject.transform.position;
                GUILayout.Label("Move: WASD / Arrow Keys");
                GUILayout.Label("Interact: E");
                GUILayout.Label($"Position: {position.x:0.00}, {position.y:0.00}");
                GUILayout.Label($"HP: {CurrentHealth} / {MaximumHealth}");
                GUILayout.Label(
                    $"Events: {ActiveEventSocketCount} / {AllEventSocketCount} "
                    + $"(Seed {previewEventSelectionSeed})");
                if (!string.IsNullOrEmpty(CurrentEventPrompt))
                {
                    GUILayout.Label($"Event: {CurrentEventPrompt}");
                }

                if (!string.IsNullOrEmpty(lastEventFeedback))
                {
                    GUILayout.Label(lastEventFeedback);
                }

                GUILayout.Label($"Elapsed: {Time.unscaledTime - readyAtTime:0.0}s");
            }

            GUILayout.EndArea();
        }

        /// <summary>
        /// Scene読込前に必須設定を検証し、不完全なPlaytest Sceneを黙って動かさない。
        /// </summary>
        private bool ValidateConfiguration()
        {
            if (string.IsNullOrWhiteSpace(mapScenePath))
            {
                Fail("Reference Map Scene Pathが空です。");
                return false;
            }

            if (inputActions == null)
            {
                Fail("InputActionAssetが設定されていません。");
                return false;
            }

            if (eventCatalog == null)
            {
                Fail("MapEventCatalogが設定されていません。");
                return false;
            }

            if (!eventCatalog.Validate(out var catalogError))
            {
                Fail($"MapEventCatalogが不正です: {catalogError}");
                return false;
            }

            if (eventPool != null && !eventPool.Validate(out var poolError))
            {
                Fail($"MapEventPoolDefinitionが不正です: {poolError}");
                return false;
            }

            var configuredMoveAction = inputActions.FindAction(
                $"{PlayerActionMapName}/{MoveActionName}",
                false);
            if (configuredMoveAction == null)
            {
                Fail($"Input Actionがありません: {PlayerActionMapName}/{MoveActionName}");
                return false;
            }

            var configuredInteractAction = inputActions.FindAction(
                $"{PlayerActionMapName}/{InteractActionName}",
                false);
            if (configuredInteractAction == null)
            {
                Fail($"Input Actionがありません: {PlayerActionMapName}/{InteractActionName}");
                return false;
            }

            return true;
        }

        /// <summary>
        /// 加算読込したMap Sceneだけを探索し、地面、Collision、制作Cameraを一意に取得する。
        /// </summary>
        private bool ResolveMapComponents()
        {
            groundTilemap = FindComponentByGameObjectName<Tilemap>(mapScene, GroundTilemapName);
            collisionTilemap = FindComponentByGameObjectName<Tilemap>(mapScene, CollisionTilemapName);
            var cameras = FindComponentsInScene<Camera>(mapScene);

            if (groundTilemap == null)
            {
                Fail($"{GroundTilemapName}がReference Mapにありません。");
                return false;
            }

            if (collisionTilemap == null)
            {
                Fail($"{CollisionTilemapName}がReference Mapにありません。");
                return false;
            }

            if (collisionTilemap.GetComponent<TilemapCollider2D>() == null
                || collisionTilemap.GetComponent<CompositeCollider2D>() == null)
            {
                Fail("CollisionTilemapにTilemapCollider2D / CompositeCollider2Dがありません。");
                return false;
            }

            if (cameras.Count != 1)
            {
                Fail($"Reference MapのCamera数が1ではありません: {cameras.Count}");
                return false;
            }

            gameplayCamera = cameras[0];
            var cellBounds = groundTilemap.cellBounds;
            var worldMin = groundTilemap.CellToWorld(cellBounds.min);
            var worldMax = groundTilemap.CellToWorld(cellBounds.max);
            mapWorldBounds = new Bounds(
                (worldMin + worldMax) * 0.5f,
                new Vector3(
                    Mathf.Abs(worldMax.x - worldMin.x),
                    Mathf.Abs(worldMax.y - worldMin.y),
                    0f));
            return true;
        }

        /// <summary>
        /// Map Scene内のPlayerStartを一つだけ許可し、そのTransformを仮PlayerのSpawn正本として返す。
        /// </summary>
        private MapSocketMarker FindUniquePlayerStart()
        {
            socketBuffer.Clear();
            socketBuffer.AddRange(FindComponentsInScene<MapSocketMarker>(mapScene));
            MapSocketMarker result = null;
            var count = 0;
            for (var index = 0; index < socketBuffer.Count; index++)
            {
                if (socketBuffer[index].SocketKind != MapSocketKind.PlayerStart)
                {
                    continue;
                }

                result = socketBuffer[index];
                count++;
            }

            if (count != 1)
            {
                Fail($"PlayerStart Socket数が1ではありません: {count}");
                return null;
            }

            return result;
        }

        /// <summary>
        /// 固定Eventを直接解決し、Pool候補はSeed抽選で有効地点とEvent種類を割り当てる。
        /// </summary>
        private bool ResolveEventSockets()
        {
            eventSocketBuffer.Clear();
            poolCandidateSocketBuffer.Clear();
            resolvedEventDefinitions.Clear();
            eventPlacementPlan = new MapEventPlacementPlan(
                Array.Empty<MapEventPlacementAssignment>());
            allEventSocketCount = 0;
            socketBuffer.Clear();
            socketBuffer.AddRange(FindComponentsInScene<MapSocketMarker>(mapScene));
            for (var index = 0; index < socketBuffer.Count; index++)
            {
                var marker = socketBuffer[index];
                if (marker.SocketKind != MapSocketKind.Event)
                {
                    continue;
                }

                allEventSocketCount++;

                if (marker.EventPlacementMode == MapEventPlacementMode.PoolCandidate)
                {
                    if (marker.InteractionRadiusOverride <= 0f)
                    {
                        Fail($"Pool候補Eventの接近半径がありません: {marker.SocketId}");
                        return false;
                    }

                    poolCandidateSocketBuffer.Add(marker);
                    continue;
                }

                if (!marker.TryResolveEventDefinition(eventCatalog, out var definition))
                {
                    Fail($"Event定義を解決できません: {marker.SocketId} / {marker.EventDefinitionId}");
                    return false;
                }

                var radius = marker.ResolveInteractionRadius(eventCatalog);
                if (float.IsNaN(radius) || float.IsInfinity(radius) || radius <= 0f)
                {
                    Fail($"Event接近半径が不正です: {marker.SocketId} / {radius}");
                    return false;
                }

                eventSocketBuffer.Add(marker);
                resolvedEventDefinitions.Add(marker, definition);
            }

            if (allEventSocketCount == 0)
            {
                Fail("Reference MapにEventSocketがありません。");
                return false;
            }

            if (!AssignPoolCandidateEvents())
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Pool候補SocketとPool AssetをIDだけの純C#入力へ変換し、結果をScene参照へ安全に戻す。
        /// </summary>
        private bool AssignPoolCandidateEvents()
        {
            if (poolCandidateSocketBuffer.Count == 0)
            {
                return true;
            }

            if (eventPool == null)
            {
                Fail("Pool候補SocketがありますがMapEventPoolDefinitionが設定されていません。");
                return false;
            }

            var socketCandidates = new MapEventSocketCandidate[poolCandidateSocketBuffer.Count];
            var socketById = new Dictionary<string, MapSocketMarker>(StringComparer.Ordinal);
            for (var index = 0; index < poolCandidateSocketBuffer.Count; index++)
            {
                var marker = poolCandidateSocketBuffer[index];
                socketCandidates[index] = new MapEventSocketCandidate(marker.SocketId);
                if (!socketById.TryAdd(marker.SocketId.Trim(), marker))
                {
                    Fail($"Pool候補Socket IDが重複しています: {marker.SocketId}");
                    return false;
                }
            }

            if (!eventPool.TryBuildRuntimeCandidates(out var eventCandidates, out var poolError))
            {
                Fail($"Event Pool候補を構築できません: {poolError}");
                return false;
            }

            if (!MapEventPlacementSelector.TryCreatePlan(
                    socketCandidates,
                    eventCandidates,
                    eventPool.ActiveSocketCount,
                    previewEventSelectionSeed,
                    out eventPlacementPlan,
                    out var selectionError))
            {
                Fail($"Event Pool抽選に失敗しました: {selectionError}");
                return false;
            }

            for (var index = 0; index < eventPlacementPlan.Assignments.Count; index++)
            {
                var assignment = eventPlacementPlan.Assignments[index];
                if (!socketById.TryGetValue(assignment.SocketId, out var marker)
                    || !eventPool.TryGetDefinition(
                        assignment.EventDefinitionId,
                        out var definition))
                {
                    Fail(
                        $"Event Pool抽選結果を解決できません: "
                        + $"{assignment.SocketId} / {assignment.EventDefinitionId}");
                    return false;
                }

                if (!eventCatalog.TryGet(assignment.EventDefinitionId, out var catalogDefinition)
                    || catalogDefinition != definition)
                {
                    Fail($"Event Pool定義がCatalogと一致しません: {assignment.EventDefinitionId}");
                    return false;
                }

                var radius = marker.ResolveInteractionRadius(eventCatalog, definition);
                if (float.IsNaN(radius) || float.IsInfinity(radius) || radius <= 0f)
                {
                    Fail($"抽選済みEvent接近半径が不正です: {marker.SocketId} / {radius}");
                    return false;
                }

                eventSocketBuffer.Add(marker);
                resolvedEventDefinitions.Add(marker, definition);
            }

            return true;
        }

        /// <summary>
        /// 共通Registryと実行サービスを接続し、MonoBehaviour内のEvent型分岐をなくす。
        /// </summary>
        private void InitializeEventRuntime()
        {
            var registry = new MapEventHandlerRegistry(
                new IMapEventHandler[]
                {
                    new HealingFountainEventHandler()
                });
            eventExecutionService = new MapEventExecutionService(registry);
        }

        /// <summary>
        /// PlayerStartへ本番Character Actor、人間入力Adapter、確認専用Spriteを持つ仮表示を生成する。
        /// </summary>
        private void CreatePreviewPlayer(Vector3 spawnPosition)
        {
            playerObject = new GameObject("PlayerPreviewDebug");
            SceneManager.MoveGameObjectToScene(playerObject, gameObject.scene);
            playerObject.transform.position = spawnPosition;
            playerObject.transform.rotation = Quaternion.identity;
            playerObject.transform.localScale = Vector3.one;

            playerActor = playerObject.AddComponent<CharacterActor2D>();
            playerActor.Initialize(
                new CharacterHealth(previewMaximumHealth, previewStartingHealth),
                moveSpeed);
            playerBody = playerActor.Body;
            playerCollider = playerActor.FootCollider;

            var renderer = playerObject.AddComponent<SpriteRenderer>();
            previewSprite = CreatePreviewPlayerSprite(out previewTexture);
            renderer.sprite = previewSprite;
            renderer.sortingLayerName = WorldObjectSortingLayerName;
            renderer.sortingOrder = 0;
            renderer.spriteSortPoint = SpriteSortPoint.Pivot;

            // 一人用Previewの入力状態をAsset本体へ残さないよう、Runtime複製だけを有効化する。
            runtimeInputActions = Instantiate(inputActions);
            runtimeInputActions.name = "InputSystem_Actions_PreviewDebugRuntime";
            var moveAction = runtimeInputActions.FindAction(
                $"{PlayerActionMapName}/{MoveActionName}",
                true);
            interactAction = runtimeInputActions.FindAction(
                $"{PlayerActionMapName}/{InteractActionName}",
                true);
            humanMoveInput = new HumanCharacterMoveInputAdapter(moveAction);
            humanMoveInput.Enable();
            interactAction.Enable();
        }

        /// <summary>
        /// Playerの足元から各Socket Transformまでの距離を比較し、範囲内の最寄り未使用Eventを選ぶ。
        /// </summary>
        private void UpdateCurrentEventSocket()
        {
            currentEventSocket = null;
            if (playerBody == null)
            {
                return;
            }

            var bestDistanceSquared = float.PositiveInfinity;
            for (var index = 0; index < eventSocketBuffer.Count; index++)
            {
                var marker = eventSocketBuffer[index];
                var definition = resolvedEventDefinitions[marker];
                if (definition.OneShot && eventRuntimeState.IsUsed(marker.SocketId))
                {
                    continue;
                }

                var radius = marker.ResolveInteractionRadius(eventCatalog, definition);
                var distanceSquared = ((Vector2)marker.transform.position - playerBody.position).sqrMagnitude;
                if (distanceSquared > radius * radius
                    || distanceSquared > bestDistanceSquared)
                {
                    continue;
                }

                if (Mathf.Approximately(distanceSquared, bestDistanceSquared)
                    && currentEventSocket != null
                    && string.CompareOrdinal(marker.SocketId, currentEventSocket.SocketId) >= 0)
                {
                    continue;
                }

                currentEventSocket = marker;
                bestDistanceSquared = distanceSquared;
            }
        }

        /// <summary>
        /// 現在候補のEventを実行し、効果が成功した場合だけOneShot状態をSocket IDへ記録する。
        /// </summary>
        private bool TryInteractWithCurrentEvent()
        {
            if (!IsReady || currentEventSocket == null)
            {
                return false;
            }

            var marker = currentEventSocket;
            var definition = resolvedEventDefinitions[marker];
            lastEventExecutionResult = eventExecutionService.Execute(
                marker.SocketId,
                definition,
                new MapEventExecutionContext(playerActor.Health),
                eventRuntimeState);
            lastEventFeedback = eventPresenter.Present(definition, lastEventExecutionResult);
            UpdateCurrentEventSocket();
            return lastEventExecutionResult.IsSuccess;
        }

        /// <summary>
        /// Playerの足元Colliderが、Tilemapと子CollisionBodyを含むMapCollision層全体と重ならないことを確認する。
        /// </summary>
        private bool ValidateSpawnClearance()
        {
            Physics2D.SyncTransforms();

            var mapCollisionLayer = LayerMask.NameToLayer("MapCollision");
            if (mapCollisionLayer < 0)
            {
                Fail("MapCollision LayerがProjectにありません。");
                return false;
            }

            var contactFilter = new ContactFilter2D();
            contactFilter.SetLayerMask(1 << mapCollisionLayer);
            contactFilter.useTriggers = false;
            spawnOverlapBuffer.Clear();
            playerCollider.Overlap(contactFilter, spawnOverlapBuffer);

            for (var index = 0; index < spawnOverlapBuffer.Count; index++)
            {
                var obstacleCollider = spawnOverlapBuffer[index];
                if (obstacleCollider == null
                    || obstacleCollider == playerCollider)
                {
                    continue;
                }

                Fail(
                    $"PlayerStartがMapCollisionと重なっています: "
                    + $"{playerObject.transform.position} / {obstacleCollider.name}");
                return false;
            }

            return true;
        }

        /// <summary>
        /// Reference MapのQA CameraをRuntime中だけ追従Cameraへ切り替え、追加Cameraを作らない。
        /// </summary>
        private void ConfigureGameplayCamera()
        {
            gameplayCamera.orthographic = true;
            gameplayCamera.orthographicSize = cameraOrthographicSize;
            // Built-in RendererへのFallbackでも足元YソートになるようCamera側にも同じ軸を明示する。
            gameplayCamera.transparencySortMode = TransparencySortMode.CustomAxis;
            gameplayCamera.transparencySortAxis = Vector3.up;
            gameplayCamera.transform.position = new Vector3(
                playerObject.transform.position.x,
                playerObject.transform.position.y,
                -10f);
            gameplayCamera.enabled = true;

            if (gameplayCamera.GetComponent<AudioListener>() == null)
            {
                gameplayCamera.gameObject.AddComponent<AudioListener>();
            }
        }

        /// <summary>
        /// Camera半幅を考慮して追従座標をMap内へ制限し、画面がMap外を映さないようにする。
        /// </summary>
        private static float ClampCameraAxis(
            float target,
            float mapMin,
            float mapMax,
            float cameraExtent)
        {
            var minimum = mapMin + cameraExtent;
            var maximum = mapMax - cameraExtent;
            return minimum > maximum
                ? (mapMin + mapMax) * 0.5f
                : Mathf.Clamp(target, minimum, maximum);
        }

        /// <summary>
        /// 32 PPUのPixel GridへCamera座標を丸め、追従時のsub-pixel揺れを避ける。
        /// </summary>
        private static float SnapToPixelGrid(float value)
        {
            return Mathf.Round(value * PixelsPerUnit) / PixelsPerUnit;
        }

        /// <summary>
        /// 正式Character素材を追加せず、Playtest用途が明確な小さなRuntime専用Marker Spriteを生成する。
        /// </summary>
        private static Sprite CreatePreviewPlayerSprite(out Texture2D texture)
        {
            const int width = 8;
            const int height = 12;
            var rows = new[]
            {
                "........",
                "..OOOO..",
                ".OHHHHO.",
                ".OHHHHO.",
                ".OCCCCO.",
                "OCCCCCCO",
                "OCCCCCCO",
                ".OCCCCO.",
                ".OCCCCO.",
                "..OCCO..",
                "..O..O..",
                ".OO..OO."
            };
            var transparent = new Color32(0, 0, 0, 0);
            var outline = new Color32(24, 43, 47, 255);
            var body = new Color32(40, 214, 219, 255);
            var highlight = new Color32(192, 255, 232, 255);
            var pixels = new Color32[width * height];

            for (var row = 0; row < height; row++)
            {
                var source = rows[height - 1 - row];
                for (var x = 0; x < width; x++)
                {
                    var color = source[x] == 'O'
                        ? outline
                        : source[x] == 'C'
                            ? body
                            : source[x] == 'H'
                                ? highlight
                                : transparent;
                    pixels[row * width + x] = color;
                }
            }

            texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = "PlayerPreviewDebugTexture",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            texture.SetPixels32(pixels);
            texture.Apply(false, true);

            var sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, width, height),
                new Vector2(0.5f, 0.12f),
                12f,
                0,
                SpriteMeshType.FullRect);
            sprite.name = "PlayerPreviewDebugSprite";
            return sprite;
        }

        /// <summary>
        /// 指定SceneのHierarchyだけから、GameObject名と型が一致するComponentを返す。
        /// </summary>
        private static T FindComponentByGameObjectName<T>(Scene scene, string objectName)
            where T : Component
        {
            var components = FindComponentsInScene<T>(scene);
            for (var index = 0; index < components.Count; index++)
            {
                if (string.Equals(
                        components[index].gameObject.name,
                        objectName,
                        StringComparison.Ordinal))
                {
                    return components[index];
                }
            }

            return null;
        }

        /// <summary>
        /// Global検索を避け、Additive Loadした対象SceneのRoot以下からComponentを列挙する。
        /// </summary>
        private static List<T> FindComponentsInScene<T>(Scene scene)
            where T : Component
        {
            var result = new List<T>();
            var roots = scene.GetRootGameObjects();
            for (var rootIndex = 0; rootIndex < roots.Length; rootIndex++)
            {
                result.AddRange(roots[rootIndex].GetComponentsInChildren<T>(true));
            }

            return result;
        }

        /// <summary>
        /// 起動失敗をHUDとConsoleの両方へ残し、不完全な状態をReadyとして扱わない。
        /// </summary>
        private void Fail(string message)
        {
            IsReady = false;
            FailureMessage = message ?? "Unknown exploration preview error.";
            Debug.LogError(FailureMessage, this);
        }
    }
}
