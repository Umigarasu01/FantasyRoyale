using FantasyRoyale.Gameplay.Characters;
using FantasyRoyale.Gameplay.Characters.Unity;
using UnityEngine;
using UnityEngine.InputSystem;

namespace FantasyRoyale.Playtest
{
    /// <summary>
    /// 承認済みの森マップを走り回る感覚を確かめる一時Playtest足場。
    /// 移動は本番のActorと人間入力Adapterを使い、追従Cameraと足元の描画順だけをここで持つ。
    /// </summary>
    [AddComponentMenu("FantasyRoyale/Playtest/Forest Map Preview Debug")]
    [DisallowMultipleComponent]
    public sealed class ForestMapPreviewDebug : MonoBehaviour
    {
        public const string MoveActionName = "Move";

        private const string PlayerActionMapName = "Player";
        private const float PixelsPerUnit = 64f;
        private const int SortingOffset = 8000;
        private const int PreviewHealth = 100;

        // 足元ではなく体の中心を画面の中央に置く。
        private const float CameraTargetHeight = 0.5f;

        [SerializeField] private InputActionAsset inputActions;
        [SerializeField] private CharacterSpriteSetDefinition heroSprites;
        [SerializeField] private Camera gameplayCamera;
        [SerializeField] private Rect mapBounds;
        [SerializeField] private Vector2 spawnPosition;
        [SerializeField, Min(0.1f)] private float moveSpeed = 5f;
        [SerializeField, Min(1f)] private float cameraHeight = 8f;
        [SerializeField, Min(0f)] private float cameraSmoothSeconds = 0.08f;
        [SerializeField, Min(0f)] private float cameraLookAhead = 0.8f;

        private InputActionAsset runtimeInputActions;
        private HumanCharacterMoveInputAdapter humanMoveInput;
        private SpriteRenderer playerRenderer;
        private CharacterMoveCommand latestMoveCommand;
        private bool hasTestMoveOverride;
        private CharacterMoveCommand testMoveCommand;
        private Vector2 cameraPosition;
        private Vector2 cameraVelocity;
        private float smoothedFrameSeconds = 1f / 60f;

        public bool IsReady { get; private set; }
        public GameObject PlayerObject { get; private set; }
        public CharacterActor2D PlayerActor { get; private set; }
        public CharacterSpriteAnimator2D PlayerAnimator { get; private set; }
        public Camera GameplayCamera => gameplayCamera;
        public Rect MapBounds => mapBounds;
        public int PlayerSortingOrder => playerRenderer == null ? 0 : playerRenderer.sortingOrder;

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
        /// 本番のActorと人間入力Adapterで騎士を生成し、Cameraを初期位置へ置く。
        /// </summary>
        private void Start()
        {
            PlayerObject = new GameObject("PlayerPreviewDebug");
            PlayerObject.transform.position = spawnPosition;
            playerRenderer = PlayerObject.AddComponent<SpriteRenderer>();
            playerRenderer.sharedMaterial = new Material(Shader.Find("Sprites/Default"));

            PlayerActor = PlayerObject.AddComponent<CharacterActor2D>();
            PlayerActor.Initialize(new CharacterHealth(PreviewHealth), moveSpeed);
            PlayerAnimator = PlayerObject.AddComponent<CharacterSpriteAnimator2D>();
            PlayerAnimator.Initialize(heroSprites);

            // 入力状態をAsset本体へ残さないよう、Runtime複製だけを有効化する。
            runtimeInputActions = Instantiate(inputActions);
            humanMoveInput = new HumanCharacterMoveInputAdapter(
                runtimeInputActions.FindAction($"{PlayerActionMapName}/{MoveActionName}", true));
            humanMoveInput.Enable();

            gameplayCamera.orthographic = true;
            gameplayCamera.orthographicSize = cameraHeight * 0.5f;
            cameraPosition = ClampToMap(spawnPosition + Vector2.up * CameraTargetHeight);
            ApplyCameraPosition();
            IsReady = true;
        }

        private void Update()
        {
            if (!IsReady)
            {
                return;
            }

            latestMoveCommand = hasTestMoveOverride ? testMoveCommand : humanMoveInput.ReadMoveCommand();
            PlayerActor.SetMoveCommand(latestMoveCommand);
            smoothedFrameSeconds = Mathf.Lerp(smoothedFrameSeconds, Time.unscaledDeltaTime, 0.05f);
        }

        /// <summary>
        /// 足元のYで描画順を決め、進む方向を少し先まで見せながらCameraを追従させる。
        /// </summary>
        private void LateUpdate()
        {
            if (!IsReady)
            {
                return;
            }

            var position = (Vector2)PlayerObject.transform.position;
            playerRenderer.sortingOrder =
                Mathf.RoundToInt((mapBounds.yMax - position.y) * PixelsPerUnit) - SortingOffset;

            var lookAhead = new Vector2(latestMoveCommand.Horizontal, latestMoveCommand.Vertical) * cameraLookAhead;
            var target = ClampToMap(position + Vector2.up * CameraTargetHeight + lookAhead);
            cameraPosition = Vector2.SmoothDamp(cameraPosition, target, ref cameraVelocity, cameraSmoothSeconds);
            ApplyCameraPosition();
        }

        private void OnDestroy()
        {
            humanMoveInput?.Dispose();
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
            GUILayout.BeginArea(new Rect(12f, 12f, 300f, 90f), GUI.skin.box);
            GUILayout.Label("Forest Map Preview Debug");
            GUILayout.Label("Move: WASD / Arrow Keys");
            GUILayout.Label($"Position: {position.x:0.0}, {position.y:0.0}   FPS: {1f / smoothedFrameSeconds:0}");
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
        /// 64 PPUの画素単位へ丸めて、地面のドットが揺れないようにする。
        /// </summary>
        private void ApplyCameraPosition()
        {
            gameplayCamera.transform.position = new Vector3(
                Mathf.Round(cameraPosition.x * PixelsPerUnit) / PixelsPerUnit,
                Mathf.Round(cameraPosition.y * PixelsPerUnit) / PixelsPerUnit,
                -10f);
        }
    }
}
