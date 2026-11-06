using System.Collections.Generic;
using FantasyRoyale.Gameplay.Characters;
using FantasyRoyale.Gameplay.Characters.Unity;
using UnityEngine;
using UnityEngine.InputSystem;

namespace FantasyRoyale.Playtest
{
    /// <summary>
    /// 森マップを遊びながら調整するための一時Playtest足場。
    /// 移動、攻撃、敵の判断は本番のComponentを使い、数値と試合の仮ルールだけをここで持つ。
    /// </summary>
    [AddComponentMenu("FantasyRoyale/Playtest/Forest Map Preview Debug")]
    [DisallowMultipleComponent]
    public sealed class ForestMapPreviewDebug : MonoBehaviour
    {
        public const string MoveActionName = "Move";
        public const string InteractActionName = "Interact";
        public const string AttackActionName = "Attack";
        public const string SpecialActionName = "Special";
        public const string NextEquipmentActionName = "Next";
        public const string PreviousEquipmentActionName = "Previous";
        public const string MapCollisionLayerName = "MapCollision";

        private const string PlayerActionMapName = "Player";
        private const float PixelsPerUnit = 64f;
        private const int SortingOffset = 8000;
        private const int PlayerHealthPoints = 100;

        // 足元ではなく体の中心を画面の中央に置く。
        private const float CameraTargetHeight = 0.5f;

        // 敵と宝箱の仮の数値。遊びながら調整する。
        private const int EnemyHealthPoints = 40;
        private const float EnemySpeed = 3f;
        private const float EnemyNoticeRange = 6f;
        private const float EnemyLeashRange = 14f;
        private const float EnemyStopRange = 0.6f;
        private const float EnemyContactRange = 0.8f;
        private const int EnemyContactDamage = 10;
        private const float EnemyContactInterval = 1f;
        private const float EnemyRespawnSeconds = 30f;
        private const float EnemyFlashSeconds = 0.12f;
        private const float PlayerDownSeconds = 1.5f;
        private const float PlayerGraceSeconds = 2f;
        private const float PlayerBlinkPerSecond = 10f;
        private const float ChestReach = 1.6f;
        private const int CoinsPerEnemy = 1;
        private const int CoinsPerChest = 10;

        private static readonly Color DownColor = new Color(0.45f, 0.45f, 0.45f);
        private static readonly Color HitColor = new Color(1f, 0.35f, 0.3f);
        private static readonly Color GraceColor = new Color(1f, 1f, 1f, 0.45f);

        [SerializeField] private InputActionAsset inputActions;
        [SerializeField] private CharacterSpriteSetDefinition heroSprites;
        [SerializeField] private Camera gameplayCamera;
        [SerializeField] private Rect mapBounds;
        [SerializeField] private Vector2 spawnPosition;
        [SerializeField, Min(0.1f)] private float moveSpeed = 5f;
        [SerializeField, Min(1f)] private float cameraHeight = 14f;
        [SerializeField] private CharacterSpriteSetDefinition enemySprites;
        [SerializeField] private CharacterEquipmentDefinition[] equipment = new CharacterEquipmentDefinition[0];
        [SerializeField] private Sprite chestClosed;
        [SerializeField] private Sprite chestOpen;
        [SerializeField] private Vector2[] enemySpots = new Vector2[0];
        [SerializeField] private Vector2[] chestSpots = new Vector2[0];

        private readonly List<Enemy> enemies = new List<Enemy>();
        private readonly List<Chest> chests = new List<Chest>();
        private InputActionAsset runtimeInputActions;
        private HumanCharacterMoveInputAdapter humanMoveInput;
        private HumanCharacterActionInputAdapter humanActionInput;
        private InputAction interactAction;
        private InputAction nextEquipmentAction;
        private InputAction previousEquipmentAction;
        private PreviewDebugCombatPresenter combatPresenter;
        private SpriteRenderer playerRenderer;
        private bool hasTestMoveOverride;
        private CharacterMoveCommand testMoveCommand;
        private float smoothedFrameSeconds = 1f / 60f;
        private int equipmentIndex;
        private float playerReturnAt = -1f;
        private float playerGraceUntil;

        public bool IsReady { get; private set; }
        public GameObject PlayerObject { get; private set; }
        public CharacterActor2D PlayerActor { get; private set; }
        public CharacterSpriteAnimator2D PlayerAnimator { get; private set; }
        public CharacterCombat2D PlayerCombat { get; private set; }
        public CharacterHealth PlayerHealth { get; private set; }
        public int Coins { get; private set; }
        public int OwnedEquipmentCount { get; private set; } = 1;
        public string LastMessage { get; private set; } = string.Empty;
        public Camera GameplayCamera => gameplayCamera;
        public Rect MapBounds => mapBounds;
        public Vector2 SpawnPosition => spawnPosition;
        public int PlayerSortingOrder => playerRenderer == null ? 0 : playerRenderer.sortingOrder;
        public int EnemyCount => enemies.Count;
        public int ChestCount => chests.Count;
        public string CurrentEquipmentName => equipment.Length == 0 ? string.Empty : equipment[equipmentIndex].DisplayName;

        public CharacterActor2D GetEnemyActor(int index)
        {
            return enemies[index].Actor;
        }

        public Vector2 GetChestPosition(int index)
        {
            return chests[index].Position;
        }

        /// <summary>
        /// Editor BuilderからScene固有の参照とMapの範囲を設定する。
        /// </summary>
        public void Configure(
            InputActionAsset newInputActions,
            CharacterSpriteSetDefinition newHeroSprites,
            Camera newGameplayCamera,
            Rect newMapBounds,
            Vector2 newSpawnPosition)
        {
            inputActions = newInputActions;
            heroSprites = newHeroSprites;
            gameplayCamera = newGameplayCamera;
            mapBounds = newMapBounds;
            spawnPosition = newSpawnPosition;
        }

        /// <summary>
        /// Editor Builderから敵、宝箱、装備の素材と置き場所を設定する。
        /// </summary>
        public void ConfigureGame(
            CharacterSpriteSetDefinition newEnemySprites,
            CharacterEquipmentDefinition[] newEquipment,
            Sprite newChestClosed,
            Sprite newChestOpen,
            Vector2[] newEnemySpots,
            Vector2[] newChestSpots)
        {
            enemySprites = newEnemySprites;
            equipment = newEquipment;
            chestClosed = newChestClosed;
            chestOpen = newChestOpen;
            enemySpots = newEnemySpots;
            chestSpots = newChestSpots;
        }

        /// <summary>
        /// PlayMode TestがKeyboardなしで移動を確認できるよう、移動入力を一時的に上書きする。
        /// </summary>
        public void SetMoveInputOverrideForTests(Vector2 moveInput)
        {
            hasTestMoveOverride = true;
            testMoveCommand = new CharacterMoveCommand(moveInput.x, moveInput.y);
        }

        /// <summary>
        /// Test用の上書きを解除し、Input SystemのMove Actionへ戻す。
        /// </summary>
        public void ClearMoveInputOverrideForTests()
        {
            hasTestMoveOverride = false;
        }

        /// <summary>
        /// 手の届く閉じた宝箱を開ける。最初は装備、持ち切った後はコインが手に入る。
        /// </summary>
        public bool TryOpenNearestChest()
        {
            var position = (Vector2)PlayerObject.transform.position;
            for (var index = 0; index < chests.Count; index++)
            {
                var chest = chests[index];
                if (chest.IsOpen || Vector2.Distance(position, chest.Position) > ChestReach)
                {
                    continue;
                }

                chest.IsOpen = true;
                chest.Renderer.sprite = chestOpen;
                if (OwnedEquipmentCount < equipment.Length)
                {
                    equipmentIndex = OwnedEquipmentCount;
                    OwnedEquipmentCount++;
                    equipment[equipmentIndex].ApplyTo(PlayerCombat.Loadout);
                    LastMessage = $"{CurrentEquipmentName}を手に入れた";
                }
                else
                {
                    Coins += CoinsPerChest;
                    LastMessage = $"コイン {CoinsPerChest} 枚を手に入れた";
                }

                return true;
            }

            return false;
        }

        /// <summary>
        /// 本番のActorと入力Adapterで騎士を生成し、敵と宝箱を置いてCameraを初期位置へ置く。
        /// </summary>
        private void Start()
        {
            PlayerObject = new GameObject("PlayerPreviewDebug");
            PlayerObject.transform.position = spawnPosition;
            playerRenderer = PlayerObject.AddComponent<SpriteRenderer>();
            playerRenderer.sharedMaterial = new Material(Shader.Find("Sprites/Default"));

            PlayerHealth = new CharacterHealth(PlayerHealthPoints);
            PlayerActor = PlayerObject.AddComponent<CharacterActor2D>();
            PlayerActor.Initialize(PlayerHealth, moveSpeed);
            PlayerAnimator = PlayerObject.AddComponent<CharacterSpriteAnimator2D>();
            PlayerAnimator.Initialize(heroSprites);
            PlayerObject.AddComponent<CharacterHurtbox2D>().Initialize(PlayerHealth);

            PlayerCombat = PlayerObject.AddComponent<CharacterCombat2D>();
            var loadout = new CharacterLoadout(null, null);
            equipment[equipmentIndex].ApplyTo(loadout);
            PlayerCombat.Initialize(loadout, LayerMask.GetMask(MapCollisionLayerName));
            PlayerCombat.HitApplied += OnHitApplied;
            combatPresenter = new PreviewDebugCombatPresenter(PlayerCombat);

            // 入力状態をAsset本体へ残さないよう、Runtime複製だけを有効化する。
            runtimeInputActions = Instantiate(inputActions);
            humanMoveInput = new HumanCharacterMoveInputAdapter(FindPlayerAction(MoveActionName));
            humanMoveInput.Enable();
            humanActionInput = new HumanCharacterActionInputAdapter(
                FindPlayerAction(AttackActionName),
                FindPlayerAction(SpecialActionName));
            humanActionInput.Enable();
            interactAction = FindPlayerAction(InteractActionName);
            nextEquipmentAction = FindPlayerAction(NextEquipmentActionName);
            previousEquipmentAction = FindPlayerAction(PreviousEquipmentActionName);
            interactAction.Enable();
            nextEquipmentAction.Enable();
            previousEquipmentAction.Enable();

            foreach (var spot in enemySpots)
            {
                enemies.Add(CreateEnemy(spot));
            }

            foreach (var spot in chestSpots)
            {
                chests.Add(CreateChest(spot));
            }

            gameplayCamera.orthographic = true;
            gameplayCamera.orthographicSize = cameraHeight * 0.5f;
            FollowWithCamera(spawnPosition);
            IsReady = true;
        }

        private InputAction FindPlayerAction(string actionName)
        {
            return runtimeInputActions.FindAction($"{PlayerActionMapName}/{actionName}", true);
        }

        private Enemy CreateEnemy(Vector2 spot)
        {
            var enemyObject = new GameObject("EnemyPreviewDebug");
            enemyObject.transform.position = spot;
            var renderer = enemyObject.AddComponent<SpriteRenderer>();
            renderer.sharedMaterial = playerRenderer.sharedMaterial;

            var actor = enemyObject.AddComponent<CharacterActor2D>();
            var health = new CharacterHealth(EnemyHealthPoints);
            actor.Initialize(health, EnemySpeed);
            enemyObject.AddComponent<CharacterSpriteAnimator2D>().Initialize(enemySprites);
            var hurtbox = enemyObject.AddComponent<CharacterHurtbox2D>();
            hurtbox.Initialize(health);
            var chase = enemyObject.AddComponent<CharacterChaseController2D>();
            chase.Initialize(
                new CharacterChaseBrain(spot.x, spot.y, EnemyNoticeRange, EnemyLeashRange, EnemyStopRange),
                PlayerActor,
                PlayerHealth,
                EnemyContactRange,
                EnemyContactDamage,
                EnemyContactInterval);
            return new Enemy(enemyObject, actor, chase, hurtbox, renderer, health, spot);
        }

        private Chest CreateChest(Vector2 spot)
        {
            var chestObject = new GameObject("ChestPreviewDebug") { layer = LayerMask.NameToLayer(MapCollisionLayerName) };
            chestObject.transform.position = spot;
            var renderer = chestObject.AddComponent<SpriteRenderer>();
            renderer.sharedMaterial = playerRenderer.sharedMaterial;
            renderer.sprite = chestClosed;
            SortByFeet(renderer, spot.y);
            var body = chestObject.AddComponent<BoxCollider2D>();
            body.size = new Vector2(1f, 0.5f);
            body.offset = new Vector2(0f, 0.25f);
            return new Chest(renderer, spot);
        }

        private void Update()
        {
            if (!IsReady)
            {
                return;
            }

            PlayerActor.SetMoveCommand(hasTestMoveOverride ? testMoveCommand : humanMoveInput.ReadMoveCommand());
            smoothedFrameSeconds = Mathf.Lerp(smoothedFrameSeconds, Time.unscaledDeltaTime, 0.05f);
            if (PlayerHealth.IsAlive)
            {
                PollActionInput();
            }

            UpdatePlayerDown();
            UpdateEnemies();
            combatPresenter.Tick(Time.deltaTime);
        }

        /// <summary>
        /// 攻撃、装備の切り替え、宝箱を開ける入力を処理する。
        /// </summary>
        private void PollActionInput()
        {
            if (humanActionInput.TryReadCommand(PlayerActor.Facing, out var command))
            {
                PlayerCombat.TryStartAction(command);
            }

            if (interactAction.WasPressedThisFrame())
            {
                TryOpenNearestChest();
            }

            if (nextEquipmentAction.WasPressedThisFrame())
            {
                CycleEquipment(1);
            }
            else if (previousEquipmentAction.WasPressedThisFrame())
            {
                CycleEquipment(-1);
            }
        }

        private void CycleEquipment(int step)
        {
            equipmentIndex = (equipmentIndex + step + OwnedEquipmentCount) % OwnedEquipmentCount;
            equipment[equipmentIndex].ApplyTo(PlayerCombat.Loadout);
        }

        /// <summary>
        /// 倒れたら少し間を置いて村へ戻し、HPを全回復して短い無敵時間を与える。育成中は脱落しない。
        /// </summary>
        private void UpdatePlayerDown()
        {
            if (!PlayerHealth.IsAlive)
            {
                if (playerReturnAt < 0f)
                {
                    playerReturnAt = Time.time + PlayerDownSeconds;
                    LastMessage = "倒れた";
                }
                else if (Time.time >= playerReturnAt)
                {
                    playerReturnAt = -1f;
                    PlayerHealth.Revive();
                    PlayerActor.Body.position = spawnPosition;
                    PlayerObject.transform.position = spawnPosition;
                    playerGraceUntil = Time.time + PlayerGraceSeconds;
                    LastMessage = "村へ戻った";
                }
            }

            var inGrace = Time.time < playerGraceUntil;
            playerRenderer.color = !PlayerHealth.IsAlive
                ? DownColor
                : inGrace && Mathf.Repeat(Time.time * PlayerBlinkPerSecond, 1f) < 0.5f
                    ? GraceColor
                    : Color.white;
        }

        private void UpdateEnemies()
        {
            var contactEnabled = Time.time >= playerGraceUntil;
            for (var index = 0; index < enemies.Count; index++)
            {
                var enemy = enemies[index];
                if (enemy.RespawnAt >= 0f)
                {
                    if (Time.time >= enemy.RespawnAt)
                    {
                        enemy.Respawn();
                    }

                    continue;
                }

                enemy.Chase.ContactEnabled = contactEnabled;
                enemy.Renderer.color = Time.time < enemy.FlashUntil ? HitColor : Color.white;
            }
        }

        private void OnHitApplied(CharacterHitEvent hit)
        {
            for (var index = 0; index < enemies.Count; index++)
            {
                var enemy = enemies[index];
                if (enemy.Hurtbox != hit.Hurtbox)
                {
                    continue;
                }

                enemy.FlashUntil = Time.time + EnemyFlashSeconds;
                if (hit.Result.BecameDefeated)
                {
                    Coins += CoinsPerEnemy;
                    LastMessage = "敵を倒した";
                    enemy.Defeat(Time.time + EnemyRespawnSeconds);
                }

                return;
            }
        }

        /// <summary>
        /// 足元のYで描画順を決め、Cameraを騎士へ遅れなく追従させる。
        /// </summary>
        private void LateUpdate()
        {
            if (!IsReady)
            {
                return;
            }

            var position = (Vector2)PlayerObject.transform.position;
            SortByFeet(playerRenderer, position.y);
            for (var index = 0; index < enemies.Count; index++)
            {
                SortByFeet(enemies[index].Renderer, enemies[index].Object.transform.position.y);
            }

            // Play中にInspectorで高さを変えて見比べられるよう、毎Frame反映する。
            gameplayCamera.orthographicSize = cameraHeight * 0.5f;

            FollowWithCamera(position);
        }

        private void SortByFeet(SpriteRenderer renderer, float y)
        {
            renderer.sortingOrder = Mathf.RoundToInt((mapBounds.yMax - y) * PixelsPerUnit) - SortingOffset;
        }

        private void OnDestroy()
        {
            if (PlayerCombat != null)
            {
                PlayerCombat.HitApplied -= OnHitApplied;
            }

            combatPresenter?.Dispose();
            humanMoveInput?.Dispose();
            humanActionInput?.Dispose();
            interactAction?.Disable();
            nextEquipmentAction?.Disable();
            previousEquipmentAction?.Disable();
            if (runtimeInputActions != null)
            {
                Destroy(runtimeInputActions);
            }
        }

        private void OnGUI()
        {
            if (!IsReady)
            {
                return;
            }

            var position = PlayerObject.transform.position;
            GUILayout.BeginArea(new Rect(12f, 12f, 320f, 150f), GUI.skin.box);
            GUILayout.Label($"HP: {PlayerHealth.CurrentHealth} / {PlayerHealth.MaximumHealth}   Coins: {Coins}");
            GUILayout.Label($"Equipment (1 / 2): {CurrentEquipmentName}");
            GUILayout.Label("Move: WASD   Attack: J   Special: K   Open: E");
            GUILayout.Label($"Position: {position.x:0.0}, {position.y:0.0}   FPS: {1f / smoothedFrameSeconds:0}");
            if (!string.IsNullOrEmpty(LastMessage))
            {
                GUILayout.Label(LastMessage);
            }

            GUILayout.EndArea();
        }

        /// <summary>
        /// Map外を映さないよう、Cameraの中心を画面の半分だけ内側へ制限する。
        /// </summary>
        private Vector2 ClampToMap(Vector2 point)
        {
            var halfHeight = cameraHeight * 0.5f;
            var halfWidth = halfHeight * gameplayCamera.aspect;
            return new Vector2(
                Mathf.Clamp(point.x, mapBounds.xMin + halfWidth, mapBounds.xMax - halfWidth),
                Mathf.Clamp(point.y, mapBounds.yMin + halfHeight, mapBounds.yMax - halfHeight));
        }

        /// <summary>
        /// 騎士の体の中心を映す。64 PPUの画素単位へ丸めて、地面のドットが揺れないようにする。
        /// </summary>
        private void FollowWithCamera(Vector2 feet)
        {
            var centre = ClampToMap(feet + Vector2.up * CameraTargetHeight);
            gameplayCamera.transform.position = new Vector3(
                Mathf.Round(centre.x * PixelsPerUnit) / PixelsPerUnit,
                Mathf.Round(centre.y * PixelsPerUnit) / PixelsPerUnit,
                -10f);
        }

        /// <summary>
        /// 確認用の雑魚敵1体の状態。倒すと隠れ、時間が経つと持ち場へ戻る。
        /// </summary>
        private sealed class Enemy
        {
            private readonly CharacterHealth health;
            private readonly Vector2 home;

            public Enemy(
                GameObject enemyObject,
                CharacterActor2D actor,
                CharacterChaseController2D chase,
                CharacterHurtbox2D hurtbox,
                SpriteRenderer renderer,
                CharacterHealth health,
                Vector2 home)
            {
                Object = enemyObject;
                Actor = actor;
                Chase = chase;
                Hurtbox = hurtbox;
                Renderer = renderer;
                this.health = health;
                this.home = home;
            }

            public GameObject Object { get; }
            public CharacterActor2D Actor { get; }
            public CharacterChaseController2D Chase { get; }
            public CharacterHurtbox2D Hurtbox { get; }
            public SpriteRenderer Renderer { get; }
            public float RespawnAt { get; private set; } = -1f;
            public float FlashUntil { get; set; }

            public void Defeat(float respawnAt)
            {
                RespawnAt = respawnAt;
                Object.SetActive(false);
            }

            public void Respawn()
            {
                RespawnAt = -1f;
                health.Revive();
                Object.transform.position = home;
                Object.SetActive(true);
            }
        }

        /// <summary>
        /// 確認用の宝箱1つの状態。
        /// </summary>
        private sealed class Chest
        {
            public Chest(SpriteRenderer renderer, Vector2 position)
            {
                Renderer = renderer;
                Position = position;
            }

            public SpriteRenderer Renderer { get; }
            public Vector2 Position { get; }
            public bool IsOpen { get; set; }
        }
    }
}
