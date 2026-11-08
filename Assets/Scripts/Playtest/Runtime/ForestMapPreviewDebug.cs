using System;
using System.Collections.Generic;
using FantasyRoyale.Gameplay.Characters;
using FantasyRoyale.Gameplay.Characters.Unity;
using FantasyRoyale.Gameplay.Events;
using FantasyRoyale.MapAuthoringKit;
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
        public const string JumpActionName = "Jump";
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
        private const float HealingReach = 2f;
        private const float ShopReach = 2.2f;
        private const int PotionPrice = 5;
        private const int PotionHealing = 30;
        private const int BowPrice = 20;
        private const int ActiveEnemyCount = 12;
        private const int ActiveChestCount = 4;
        private const int CoinsPerEnemy = 1;
        private const int CoinsPerChest = 10;

        private static readonly Rect EquipmentToggleRect = new Rect(12f, 188f, 320f, 26f);
        private const float EquipmentPanelMaxHeight = 420f;
        private const float EquipmentBarHeight = 27f;
        private const float EquipmentRowHeight = 25f;
        private static readonly CharacterArmourSlot[] EquipmentPanelSlots = { CharacterArmourSlot.Head, CharacterArmourSlot.Torso, CharacterArmourSlot.Legs };
        private static readonly Color DownColor = new Color(0.45f, 0.45f, 0.45f);
        private static readonly Color HitColor = new Color(1f, 0.35f, 0.3f);
        private static readonly Color GraceColor = new Color(1f, 1f, 1f, 0.45f);

        [SerializeField] private InputActionAsset inputActions;
        [SerializeField] private CharacterSpriteSetDefinition heroSprites;
        [SerializeField] private Camera gameplayCamera;
        [SerializeField] private Rect mapBounds;
        [SerializeField] private Vector2 spawnPosition;
        // 大きく変えるときは、体の定義のFrames Per Secondも同じ割合で変えて足の滑りを防ぐ。
        [SerializeField, Min(0.1f)] private float moveSpeed = 6f;
        [SerializeField, Min(1f)] private float cameraHeight = 14f;
        [SerializeField] private CharacterSpriteSetDefinition enemySprites;
        [SerializeField] private CharacterEquipmentDefinition[] equipment = new CharacterEquipmentDefinition[0];
        [SerializeField] private CharacterEquipmentCatalog catalog;
        [SerializeField] private Sprite chestClosed;
        [SerializeField] private Sprite chestOpen;
        [SerializeField] private Vector2[] enemySpots = new Vector2[0];
        [SerializeField] private Vector2[] chestSpots = new Vector2[0];
        [SerializeField] private MapEventDefinition healingEvent;
        [SerializeField] private Vector2[] healingSpots = new Vector2[0];
        [SerializeField] private Vector2[] shopSpots = new Vector2[0];

        // 0なら試合ごとに置き場所が変わる。同じ配置を再現するときだけ値を入れる。
        [SerializeField] private int matchSeed;

        private readonly List<Enemy> enemies = new List<Enemy>();
        private readonly List<Chest> chests = new List<Chest>();
        private readonly PreviewDebugMapEventRuntimeStateStore eventState = new PreviewDebugMapEventRuntimeStateStore();
        private MapEventExecutionService eventService;
        private InputActionAsset runtimeInputActions;
        private HumanCharacterMoveInputAdapter humanMoveInput;
        private HumanCharacterActionInputAdapter humanActionInput;
        private InputAction interactAction;
        private InputAction nextEquipmentAction;
        private InputAction previousEquipmentAction;
        private InputAction jumpAction;
        private PreviewDebugCombatPresenter combatPresenter;
        private SpriteRenderer playerRenderer;
        private bool hasTestMoveOverride;
        private CharacterMoveCommand testMoveCommand;
        private float smoothedFrameSeconds = 1f / 60f;
        private int equipmentIndex;
        private CharacterEquipmentDefinition currentEquipment;
        private Vector2 equipmentPanelScroll;
        private int equipmentPanelTab;
        private int weaponKindTab;
        private GUIStyle equipmentTabStyle;
        private GUIStyle equipmentRowStyle;
        private GUIStyle equipmentIdStyle;
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
        public int Seed { get; private set; }
        public bool IsShopOpen { get; private set; }

        /// <summary>
        /// 全装備の確認画面を開いているか。
        /// </summary>
        public bool IsEquipmentPanelOpen { get; set; }

        public CharacterEquipmentCatalog EquipmentCatalog => catalog;
        public int EnemyCount => enemies.Count;
        public int ChestCount => chests.Count;
        public string CurrentEquipmentName => currentEquipment == null ? string.Empty : currentEquipment.DisplayName;

        /// <summary>
        /// 全装備の画面の枠。高さは今の一覧の行数に合わせ、多ければ上限で止めて一覧を送って選ぶ。
        /// </summary>
        private Rect EquipmentPanelRect
        {
            get
            {
                // 上の段と、武器なら武器種の段、一覧の行。
                var bars = equipmentPanelTab == 0 ? 2 : 1;
                var height = 20f + bars * EquipmentBarHeight + CountEquipmentChoices() * EquipmentRowHeight;
                return new Rect(Screen.width - 312f, 12f, 300f, Mathf.Min(height, EquipmentPanelMaxHeight, Screen.height - 24f));
            }
        }

        /// <summary>
        /// 全装備の画面で今並んでいる行の数。武器がなければ、その旨の1行を数える。
        /// </summary>
        private int CountEquipmentChoices()
        {
            if (equipmentPanelTab != 0)
            {
                return catalog.GetArmour(EquipmentPanelSlots[equipmentPanelTab - 1]).Count;
            }

            var kinds = catalog.GetWeaponKinds();
            return kinds.Count == 0 ? 1 : catalog.GetWeapons(kinds[Mathf.Clamp(weaponKindTab, 0, kinds.Count - 1)]).Count;
        }

        /// <summary>
        /// 指定の部位で今着ている物の名前を返す。
        /// </summary>
        public string GetArmourName(CharacterArmourSlot slot)
        {
            return PlayerAnimator.GetArmour(slot).DisplayName;
        }

        public CharacterActor2D GetEnemyActor(int index)
        {
            return enemies[index].Actor;
        }

        public Vector2 GetChestPosition(int index)
        {
            return chests[index].Position;
        }

        public Vector2 GetHealingPosition(int index)
        {
            return healingSpots[index];
        }

        public Vector2 GetShopPosition(int index)
        {
            return shopSpots[index];
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
            CharacterEquipmentCatalog newCatalog,
            Sprite newChestClosed,
            Sprite newChestOpen,
            Vector2[] newEnemySpots,
            Vector2[] newChestSpots,
            MapEventDefinition newHealingEvent,
            Vector2[] newHealingSpots,
            Vector2[] newShopSpots)
        {
            enemySprites = newEnemySprites;
            equipment = newEquipment;
            catalog = newCatalog;
            chestClosed = newChestClosed;
            chestOpen = newChestOpen;
            enemySpots = newEnemySpots;
            chestSpots = newChestSpots;
            healingEvent = newHealingEvent;
            healingSpots = newHealingSpots;
            shopSpots = newShopSpots;
        }

        /// <summary>
        /// PlayMode Testが敵を倒さずに買い物を確認できるよう、コインを足す。
        /// </summary>
        public void AddCoinsForTests(int amount)
        {
            Coins += amount;
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
        /// 手の届く閉じた宝箱を開ける。中身はショップで使うコインである。
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
                Coins += CoinsPerChest;
                LastMessage = $"コイン {CoinsPerChest} 枚を手に入れた";
                return true;
            }

            return false;
        }

        /// <summary>
        /// 手の届く回復の場所を使う。既存の回復の泉のEventと同じ規則で、一度使うと使えなくなる。
        /// </summary>
        public bool TryUseNearestHealing()
        {
            var position = (Vector2)PlayerObject.transform.position;
            for (var index = 0; index < healingSpots.Length; index++)
            {
                if (Vector2.Distance(position, healingSpots[index]) > HealingReach)
                {
                    continue;
                }

                var result = eventService.Execute(
                    $"forest-healing-{index}",
                    healingEvent,
                    new MapEventExecutionContext(PlayerHealth),
                    eventState);
                LastMessage = result.IsSuccess
                    ? $"HPが {result.AppliedValue} 回復した"
                    : eventState.IsUsed($"forest-healing-{index}") ? "もう水が湧いていない" : "HPは減っていない";
                return result.IsSuccess;
            }

            return false;
        }

        /// <summary>
        /// 屋台の前でショップを開く。
        /// </summary>
        public bool TryOpenShop()
        {
            IsShopOpen = IsNearShop();
            return IsShopOpen;
        }

        /// <summary>
        /// 回復薬を買ってその場で飲む。HPが減っていなければ買わない。
        /// </summary>
        public bool TryBuyPotion()
        {
            if (Coins < PotionPrice || !PlayerHealth.RestoreHealth(PotionHealing).IsApplied)
            {
                LastMessage = Coins < PotionPrice ? "コインが足りない" : "HPは減っていない";
                return false;
            }

            Coins -= PotionPrice;
            LastMessage = "回復薬を飲んだ";
            return true;
        }

        /// <summary>
        /// 弓を買って持ち替える。騎士の手の部品も弓に替わる。
        /// </summary>
        public bool TryBuyBow()
        {
            if (OwnedEquipmentCount >= equipment.Length || Coins < BowPrice)
            {
                LastMessage = Coins < BowPrice ? "コインが足りない" : "もう持っている";
                return false;
            }

            Coins -= BowPrice;
            OwnedEquipmentCount++;
            EquipIndex(OwnedEquipmentCount - 1);
            LastMessage = $"{CurrentEquipmentName}を買った";
            return true;
        }

        /// <summary>
        /// idの武器へ持ち替える。確認用なので、買っていなくても代金なしで持てる。
        /// </summary>
        public bool TryEquipWeapon(string id)
        {
            var weapon = catalog.FindWeapon(id);
            if (weapon == null)
            {
                LastMessage = $"{id}という武器はない";
                return false;
            }

            Equip(weapon);
            LastMessage = $"{weapon.DisplayName}に持ち替えた";
            return true;
        }

        /// <summary>
        /// 指定の部位をidの防具へ着替える。確認用なので代金はかからない。他の部位と武器はそのまま使える。
        /// </summary>
        public bool TryEquipArmour(CharacterArmourSlot slot, string id)
        {
            var part = catalog.FindArmour(id);
            if (part == null || !part.Covers(slot))
            {
                LastMessage = $"{id}は{slot}に着られない";
                return false;
            }

            PlayerAnimator.SetArmour(slot, part);
            LastMessage = $"{slot}を{part.DisplayName}に着替えた";
            return true;
        }

        private bool IsNearShop()
        {
            var position = (Vector2)PlayerObject.transform.position;
            for (var index = 0; index < shopSpots.Length; index++)
            {
                if (Vector2.Distance(position, shopSpots[index]) <= ShopReach)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 装備のActionと見た目の部品をまとめて替える。走りの胴は、胴が武器種の走りを持っていればそれになる。
        /// </summary>
        private void EquipIndex(int index)
        {
            equipmentIndex = index;
            Equip(equipment[index]);
        }

        private void Equip(CharacterEquipmentDefinition weapon)
        {
            currentEquipment = weapon;
            weapon.ApplyTo(PlayerCombat.Loadout);
            PlayerAnimator.SetGear(weapon.Gear);
            PlayerAnimator.SetWeaponKind(weapon.WeaponKind);
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
            PlayerCombat.Initialize(new CharacterLoadout(null, null), LayerMask.GetMask(MapCollisionLayerName));
            EquipIndex(equipmentIndex);
            PlayerCombat.HitApplied += OnHitApplied;
            PlayerAnimator.BindCombat(PlayerCombat);
            combatPresenter = new PreviewDebugCombatPresenter(PlayerCombat, false);

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
            jumpAction = FindPlayerAction(JumpActionName);
            interactAction.Enable();
            nextEquipmentAction.Enable();
            previousEquipmentAction.Enable();
            jumpAction.Enable();

            eventService = new MapEventExecutionService(
                new MapEventHandlerRegistry(new IMapEventHandler[] { new HealingFountainEventHandler() }));

            // 地形は固定で、敵と宝箱の置き場所だけを候補から試合ごとに選ぶ。
            Seed = matchSeed != 0 ? matchSeed : Environment.TickCount;
            foreach (var spot in PickSpots(enemySpots, ActiveEnemyCount, "enemy", Seed))
            {
                enemies.Add(CreateEnemy(spot));
            }

            foreach (var spot in PickSpots(chestSpots, ActiveChestCount, "chest", Seed + 1))
            {
                chests.Add(CreateChest(spot));
            }

            gameplayCamera.orthographic = true;
            gameplayCamera.orthographicSize = cameraHeight * 0.5f;
            FollowWithCamera(spawnPosition);
            IsReady = true;
        }

        /// <summary>
        /// 候補の中から指定数をSeedで選ぶ。抽選はEvent配置と同じ純C#の選択器を使う。
        /// </summary>
        private static List<Vector2> PickSpots(Vector2[] candidates, int count, string prefix, int seed)
        {
            var sockets = new List<MapEventSocketCandidate>();
            for (var index = 0; index < candidates.Length; index++)
            {
                sockets.Add(new MapEventSocketCandidate($"{prefix}-{index}"));
            }

            if (!MapEventPlacementSelector.TryCreatePlan(
                    sockets,
                    new[] { new MapEventPoolCandidate(prefix, 1) },
                    Mathf.Min(count, candidates.Length),
                    seed,
                    out var plan,
                    out var error))
            {
                throw new InvalidOperationException(error);
            }

            var picked = new List<Vector2>();
            foreach (var assignment in plan.Assignments)
            {
                picked.Add(candidates[int.Parse(assignment.SocketId.Substring(prefix.Length + 1))]);
            }

            return picked;
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
        /// 攻撃、跳躍、装備の切り替え、宝箱を開ける入力を処理する。
        /// </summary>
        private void PollActionInput()
        {
            if (jumpAction.WasPressedThisFrame())
            {
                PlayerActor.TryJump();
            }

            if (humanActionInput.TryReadCommand(PlayerActor.Facing, out var command) && !IsClickingEquipmentPanel())
            {
                PlayerCombat.TryStartAction(command);
            }

            if (interactAction.WasPressedThisFrame())
            {
                if (IsShopOpen)
                {
                    IsShopOpen = false;
                }
                else if (!TryOpenNearestChest() && !TryUseNearestHealing())
                {
                    TryOpenShop();
                }
            }

            if (IsShopOpen && !IsNearShop())
            {
                IsShopOpen = false;
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
            EquipIndex((equipmentIndex + step + OwnedEquipmentCount) % OwnedEquipmentCount);
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
            PlayerAnimator.SetSortingOrder(FeetOrder(position.y));
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
            renderer.sortingOrder = FeetOrder(y);
        }

        private int FeetOrder(float y)
        {
            return Mathf.RoundToInt((mapBounds.yMax - y) * PixelsPerUnit) - SortingOffset;
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
            jumpAction?.Disable();
            if (runtimeInputActions != null)
            {
                Destroy(runtimeInputActions);
            }
        }

        private void OnGUI()
        {
            // Play中にCodeが再Compileされると、保存されないHPの参照だけが消える。
            if (!IsReady || PlayerHealth == null)
            {
                return;
            }

            var position = PlayerObject.transform.position;
            GUILayout.BeginArea(new Rect(12f, 12f, 320f, 170f), GUI.skin.box);
            GUILayout.Label($"HP: {PlayerHealth.CurrentHealth} / {PlayerHealth.MaximumHealth}   Coins: {Coins}");
            GUILayout.Label($"Equipment (1 / 2): {CurrentEquipmentName}");
            GUILayout.Label($"Armour: {GetArmourName(CharacterArmourSlot.Head)} / {GetArmourName(CharacterArmourSlot.Torso)} / {GetArmourName(CharacterArmourSlot.Legs)}");
            GUILayout.Label("Move: WASD   Attack: J   Special: K   Jump: Space   Use: E");
            GUILayout.Label($"Position: {position.x:0.0}, {position.y:0.0}   FPS: {1f / smoothedFrameSeconds:0}   Seed: {Seed}");
            if (!string.IsNullOrEmpty(LastMessage))
            {
                GUILayout.Label(LastMessage);
            }

            GUILayout.EndArea();
            if (GUI.Button(EquipmentToggleRect, IsEquipmentPanelOpen ? "全装備を閉じる" : "全装備を開く"))
            {
                IsEquipmentPanelOpen = !IsEquipmentPanelOpen;
            }

            if (IsEquipmentPanelOpen)
            {
                DrawEquipmentPanel();
            }

            if (IsShopOpen)
            {
                DrawShop();
            }
        }

        /// <summary>
        /// 確認用の全装備の画面。上の段で武器と部位を切り替え、武器は武器種でも絞って一覧から選ぶ。
        /// 一覧にある物を全て並べるので、装備や武器種を増やしてもここは書き足さない。
        /// </summary>
        private void DrawEquipmentPanel()
        {
            EnsureEquipmentStyles();
            GUILayout.BeginArea(EquipmentPanelRect, GUI.skin.box);
            var tabs = new[]
            {
                $"武器 {catalog.Weapons.Count}",
                $"頭 {catalog.GetArmour(CharacterArmourSlot.Head).Count}",
                $"胴 {catalog.GetArmour(CharacterArmourSlot.Torso).Count}",
                $"足 {catalog.GetArmour(CharacterArmourSlot.Legs).Count}"
            };

            // 切り替えは描き終えてから反映する。同じ入力の途中で並びが変わると、IMGUIの配置が食い違うためである。
            var requestedTab = GUILayout.Toolbar(equipmentPanelTab, tabs, equipmentTabStyle);
            if (equipmentPanelTab == 0)
            {
                DrawWeaponChoices();
            }
            else
            {
                DrawArmourChoices(EquipmentPanelSlots[equipmentPanelTab - 1]);
            }

            GUILayout.EndArea();
            if (requestedTab != equipmentPanelTab)
            {
                equipmentPanelTab = requestedTab;
                equipmentPanelScroll = Vector2.zero;
            }
        }

        private void DrawWeaponChoices()
        {
            var kinds = catalog.GetWeaponKinds();
            if (kinds.Count == 0)
            {
                GUILayout.Label("武器がありません。");
                return;
            }

            weaponKindTab = Mathf.Clamp(weaponKindTab, 0, kinds.Count - 1);
            var names = new string[kinds.Count];
            for (var index = 0; index < kinds.Count; index++)
            {
                names[index] = $"{GetWeaponKindName(kinds[index])} {catalog.GetWeapons(kinds[index]).Count}";
            }

            var requestedKind = GUILayout.Toolbar(weaponKindTab, names, equipmentTabStyle);
            equipmentPanelScroll = GUILayout.BeginScrollView(equipmentPanelScroll);
            foreach (var weapon in catalog.GetWeapons(kinds[weaponKindTab]))
            {
                if (DrawChoice(weapon.DisplayName, weapon.Id, weapon == currentEquipment))
                {
                    TryEquipWeapon(weapon.Id);
                }
            }

            GUILayout.EndScrollView();
            if (requestedKind != weaponKindTab)
            {
                weaponKindTab = requestedKind;
                equipmentPanelScroll = Vector2.zero;
            }
        }

        private void DrawArmourChoices(CharacterArmourSlot slot)
        {
            var worn = PlayerAnimator.GetArmour(slot);
            equipmentPanelScroll = GUILayout.BeginScrollView(equipmentPanelScroll);
            foreach (var part in catalog.GetArmour(slot))
            {
                if (DrawChoice(part.DisplayName, part.Id, part == worn))
                {
                    TryEquipArmour(slot, part.Id);
                }
            }

            GUILayout.EndScrollView();
        }

        /// <summary>
        /// 一覧の1行を描き、押されたら真を返す。左に名前、右にidを出し、身につけている物は印と色で見せる。
        /// </summary>
        private bool DrawChoice(string displayName, string id, bool isCurrent)
        {
            var text = isCurrent ? $"● {displayName}" : $"    {displayName}";
            var pressed = GUILayout.Toggle(isCurrent, text, equipmentRowStyle) != isCurrent;
            GUI.Label(GUILayoutUtility.GetLastRect(), id, equipmentIdStyle);
            return pressed;
        }

        /// <summary>
        /// 一覧の行の見た目を作る。GUI.skinはOnGUIの中でしか使えないので、初めて描くときに作る。
        /// </summary>
        private void EnsureEquipmentStyles()
        {
            if (equipmentRowStyle != null)
            {
                return;
            }

            equipmentRowStyle = new GUIStyle(GUI.skin.button)
            {
                alignment = TextAnchor.MiddleLeft,
                fixedHeight = 24f,
                padding = new RectOffset(8, 8, 2, 2),
                margin = new RectOffset(2, 2, 1, 1)
            };
            var chosen = new Color(1f, 0.85f, 0.4f);
            equipmentRowStyle.onNormal.textColor = chosen;
            equipmentRowStyle.onHover.textColor = chosen;
            equipmentRowStyle.onActive.textColor = chosen;
            equipmentTabStyle = new GUIStyle(GUI.skin.button);
            equipmentTabStyle.onNormal.textColor = chosen;
            equipmentTabStyle.onHover.textColor = chosen;
            equipmentTabStyle.onActive.textColor = chosen;
            equipmentIdStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleRight,
                padding = new RectOffset(0, 10, 0, 0)
            };
            equipmentIdStyle.normal.textColor = new Color(1f, 1f, 1f, 0.5f);
        }

        /// <summary>
        /// 武器種の画面の名前。武器種を増やすときは動きの絵も作るので、名前もここへ足す。足すまでは型の名前を見せる。
        /// </summary>
        private static string GetWeaponKindName(CharacterActionMotion kind)
        {
            switch (kind)
            {
                case CharacterActionMotion.Swing:
                    return "片手剣";
                case CharacterActionMotion.Shoot:
                    return "弓";
                default:
                    return kind.ToString();
            }
        }

        /// <summary>
        /// 全装備の画面を押したクリックかを返す。攻撃はマウスにも割り当てているので、押しても振らないようにする。
        /// </summary>
        private bool IsClickingEquipmentPanel()
        {
            var mouse = Mouse.current;
            if (mouse == null || !(mouse.leftButton.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame))
            {
                return false;
            }

            var pointer = mouse.position.ReadValue();
            var fromTop = new Vector2(pointer.x, Screen.height - pointer.y);
            return EquipmentToggleRect.Contains(fromTop) || (IsEquipmentPanelOpen && EquipmentPanelRect.Contains(fromTop));
        }

        /// <summary>
        /// 確認用の簡単なショップ画面。Eで閉じるか、屋台から離れると閉じる。
        /// </summary>
        private void DrawShop()
        {
            GUILayout.BeginArea(new Rect(Screen.width * 0.5f - 150f, 40f, 300f, 220f), GUI.skin.box);
            GUILayout.Label($"ショップ   コイン: {Coins}");
            if (GUILayout.Button($"回復薬  {PotionPrice} コイン（HP {PotionHealing} 回復）"))
            {
                TryBuyPotion();
            }

            GUI.enabled = OwnedEquipmentCount < equipment.Length;
            if (GUILayout.Button(GUI.enabled ? $"弓  {BowPrice} コイン" : "弓  購入済み"))
            {
                TryBuyBow();
            }

            GUI.enabled = true;
            GUILayout.Label("E: 閉じる");
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
