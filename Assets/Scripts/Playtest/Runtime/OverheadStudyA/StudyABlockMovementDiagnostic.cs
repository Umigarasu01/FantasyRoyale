using System;
using System.Collections;
using FantasyRoyale.Gameplay.Characters;
using FantasyRoyale.Gameplay.Characters.Unity;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif

namespace FantasyRoyale.Playtest.OverheadStudyA
{
    /// <summary>Editor専用のブロック歩行確認。既存の人間移動入力または自動Commandで本番Actorを動かす。</summary>
    [DefaultExecutionOrder(-100)]
    [DisallowMultipleComponent]
    public sealed class StudyABlockMovementDiagnostic : MonoBehaviour
    {
        public string MapScenePath;
        public Vector2[] Route = Array.Empty<Vector2>();
        public Sprite HeroSprite;
        public Material HeroMaterial;
        public float MoveSpeed = 5f;
        public float MaxRunSeconds = 10f;
        public bool AutoTraverse = true;
        public bool UseHumanInput;
        public InputActionAsset InputActions;
        public bool ReverseAtEnd;
        public bool FollowActorCamera;
        public Rect CameraMapBounds;
        public Camera GameplayCamera { get; private set; }
        public CharacterActor2D Actor { get; private set; }
        public InputActionAsset RuntimeInputActions { get; private set; }
        public bool InputEnabled => humanInput != null && humanInput.IsEnabled;
        public Vector2 LastHumanMove { get; private set; }
        public int InputReadFrames { get; private set; }
        public bool WaitingForNeutral => UseHumanInput && neutralFrames < 2;
        public bool Ready { get; private set; }
        public string Failure { get; private set; } = string.Empty;
        public int Frame { get; private set; }
        public int FixedFrame { get; private set; }
        public int RouteIndex { get; private set; }
        public int CompletedPasses { get; private set; }
        public float ElapsedGameSeconds { get; private set; }
        public float TotalTravelled { get; private set; }
        public bool Running { get; private set; }
        public Vector2 Position => Actor != null && Actor.Body != null ? Actor.Body.position : Vector2.zero;
        private int direction = 1;
        private Vector2 previousPosition;
        private Scene mapScene;
        private HumanCharacterMoveInputAdapter humanInput;
        private int neutralFrames;

        private IEnumerator Start()
        {
            if (string.IsNullOrEmpty(MapScenePath) || !MapScenePath.StartsWith("Assets/Scenes/MapAuthoring/OverheadStudyA/", StringComparison.Ordinal)
                || Route == null || Route.Length < 2 || HeroSprite == null || HeroMaterial == null
                || !Finite(MoveSpeed) || MoveSpeed <= 0 || (!UseHumanInput && (!Finite(MaxRunSeconds) || MaxRunSeconds <= 0)))
            { Failure = "独立Study Scene/経路2点以上/Hero SpriteとMaterial/正の速度と時間が必要です。"; yield break; }
            var sourceMove = UseHumanInput && InputActions != null ? InputActions.FindAction("Player/Move", false) : null;
            if (UseHumanInput && (AutoTraverse || sourceMove == null || sourceMove.expectedControlType != "Vector2" || sourceMove.type != InputActionType.Value))
            { Failure = "手動操作にはUse Human Input=true、Auto Traverse=false、既存Player/Move入力Assetが必要です。"; yield break; }
            if (FollowActorCamera && (!UseHumanInput || !Finite(CameraMapBounds.xMin) || !Finite(CameraMapBounds.yMin)
                || !Finite(CameraMapBounds.xMax) || !Finite(CameraMapBounds.yMax) || CameraMapBounds.width <= 0f || CameraMapBounds.height <= 0f))
            { Failure = "追従Cameraには手動入力と有限の正のMap境界が必要です。"; yield break; }
            foreach (var point in Route)
                if (!Finite(point.x) || !Finite(point.y)) { Failure = "Route座標は有限値にしてください。"; yield break; }
#if UNITY_EDITOR
            mapScene = SceneManager.GetSceneByPath(MapScenePath);
            if (!mapScene.IsValid() || !mapScene.isLoaded)
            {
                try { mapScene = EditorSceneManager.LoadSceneInPlayMode(MapScenePath, new LoadSceneParameters(LoadSceneMode.Additive)); }
                catch (Exception exception) { Failure = "Study Sceneを読み込めません: " + exception.Message; }
                if (Failure.Length != 0) yield break;
                // Unity6000.4のこのAPIはSceneを同期で返す。次frameまで待ち、loaded状態を再確認する。
                yield return null;
                mapScene = SceneManager.GetSceneByPath(MapScenePath);
            }
            if (!mapScene.IsValid() || !mapScene.isLoaded) { Failure = "Study Sceneの読み込みが完了していません。"; yield break; }
            try { InitializeLoadedMap(); }
            catch (Exception exception) { Failure = "歩行診断を初期化できません: " + exception.Message; }
#else
            Failure = "このStudy歩行診断はUnity Editor Play専用です。Player buildには対応していません。";
            yield break;
#endif
        }

        private void InitializeLoadedMap()
        {
            Camera mapCamera = null;
            foreach (var root in mapScene.GetRootGameObjects())
            {
                if (UseHumanInput)
                    foreach (var label in root.GetComponentsInChildren<TextMesh>(true))
                        if (label.name == "StructuralDiagnosticLabel") label.text = "WALK STUDY | WASD / ARROWS";
                foreach (var renderer in root.GetComponentsInChildren<SpriteRenderer>(true))
                    if (renderer.name.StartsWith("ConceptScaleHero", StringComparison.Ordinal)) renderer.enabled = false;
                foreach (var camera in root.GetComponentsInChildren<Camera>(true))
                    if (camera.name == "Main Camera") mapCamera = camera;
            }
            if (mapCamera == null) throw new InvalidOperationException("読み込んだStudyにMain Cameraがありません。");
            mapCamera.orthographic = true; mapCamera.orthographicSize = 8f; mapCamera.aspect = 1.5f;
            mapCamera.transform.position = new Vector3(12f, 8f, -10f); mapCamera.transform.rotation = Quaternion.identity;
            mapCamera.transparencySortMode = TransparencySortMode.CustomAxis; mapCamera.transparencySortAxis = Vector3.up;
            GameplayCamera = mapCamera;
            if (FollowActorCamera) mapCamera.ResetAspect(); // Existing exploration preview uses the current GameView aspect.
            var actorObject = new GameObject("StudyA_ActualCharacterActor_Diagnostic");
            SceneManager.MoveGameObjectToScene(actorObject, mapScene); actorObject.transform.position = Route[0];
            Actor = actorObject.AddComponent<CharacterActor2D>(); Actor.Initialize(new CharacterHealth(100, 100), MoveSpeed);
            var sprite = actorObject.AddComponent<SpriteRenderer>(); sprite.sprite = HeroSprite; sprite.sharedMaterial = HeroMaterial;
            sprite.sortingLayerName = "WorldObjects"; sprite.sortingOrder = 0; sprite.spriteSortPoint = SpriteSortPoint.Pivot;
            Physics2D.SyncTransforms(); previousPosition = Position; RouteIndex = 1; Running = AutoTraverse || (UseHumanInput && isActiveAndEnabled);
            if (UseHumanInput && Running && Application.isFocused) EnableHumanInput();
            Ready = true;
        }

        private void Update()
        {
            Frame++;
            if (!Ready || Actor == null) return;
            if (UseHumanInput)
            {
                if (AutoTraverse)
                { Failure = "手動入力と自動移動は同時に有効にできません。"; StopMovement(); ReleaseHumanInput(); return; }
                var command = CharacterMoveCommand.None;
                if (Running && Application.isFocused && !InputEnabled && Failure.Length == 0)
                {
                    try { EnableHumanInput(); }
                    catch (Exception error) { Failure = "移動入力を開始できません: " + error.Message; StopMovement(); }
                }
                if (Running && Application.isFocused && InputEnabled)
                {
                    var input = humanInput.ReadMoveCommand(); InputReadFrames++;
                    if (neutralFrames < 2) neutralFrames = input.IsMoving ? 0 : neutralFrames + 1;
                    else command = input;
                }
                LastHumanMove = new Vector2(command.Horizontal, command.Vertical); Actor.SetMoveCommand(command);
                return;
            }
            if (humanInput != null || RuntimeInputActions != null) ReleaseHumanInput();
            if (!Running) return;
            ElapsedGameSeconds += Time.deltaTime;
            if (ElapsedGameSeconds >= MaxRunSeconds) StopMovement();
        }

        private void LateUpdate()
        {
            if (!FollowActorCamera || !Ready || GameplayCamera == null || Actor == null || Actor.Body == null) return;
            // Match the existing exploration camera: actual physics position, ortho 8, map clamp and 1/32-unit snap.
            var target = Actor.Body.position;
            var verticalExtent = GameplayCamera.orthographicSize;
            var x = ClampCameraAxis(target.x, CameraMapBounds.xMin, CameraMapBounds.xMax, verticalExtent * GameplayCamera.aspect);
            var y = ClampCameraAxis(target.y, CameraMapBounds.yMin, CameraMapBounds.yMax, verticalExtent);
            GameplayCamera.transform.position = new Vector3(Mathf.Round(x * 32f) / 32f, Mathf.Round(y * 32f) / 32f, GameplayCamera.transform.position.z);
        }

        private static float ClampCameraAxis(float target, float mapMin, float mapMax, float cameraExtent)
        {
            var minimum = mapMin + cameraExtent; var maximum = mapMax - cameraExtent;
            return minimum > maximum ? (mapMin + mapMax) * 0.5f : Mathf.Clamp(target, minimum, maximum);
        }

        private void FixedUpdate()
        {
            FixedFrame++;
            if (!Ready || Actor == null) return;
            TotalTravelled += Vector2.Distance(previousPosition, Position); previousPosition = Position;
            if (!Running) { Actor.SetMoveCommand(CharacterMoveCommand.None); return; }
            if (UseHumanInput) return; // Existing adapter Update owns the command; do not overwrite it in FixedUpdate.
            if (!AutoTraverse) { Actor.SetMoveCommand(CharacterMoveCommand.None); return; }
            var delta = Route[RouteIndex] - Position;
            if (delta.sqrMagnitude <= 0.0004f)
            {
                var next = RouteIndex + direction;
                if (next < 0 || next >= Route.Length)
                {
                    CompletedPasses++;
                    if (!ReverseAtEnd) { StopMovement(); return; }
                    direction = -direction; next = RouteIndex + direction;
                }
                RouteIndex = next; delta = Route[RouteIndex] - Position;
            }
            var distance = delta.magnitude;
            var step = Mathf.Max(0.00001f, MoveSpeed * Time.fixedDeltaTime);
            var move = distance > 0f ? delta.normalized * Mathf.Min(1f, distance / step) : Vector2.zero;
            Actor.SetMoveCommand(new CharacterMoveCommand(move.x, move.y));
        }

        public void StopMovement()
        {
            Running = false; LastHumanMove = Vector2.zero;
            if (Actor != null) Actor.SetMoveCommand(CharacterMoveCommand.None);
        }

        private void EnableHumanInput()
        {
            ReleaseHumanInput();
            try
            {
                RuntimeInputActions = Instantiate(InputActions); RuntimeInputActions.name = "StudyA_PlayerMove_Runtime";
                humanInput = new HumanCharacterMoveInputAdapter(RuntimeInputActions.FindAction("Player/Move", true)); humanInput.Enable();
            }
            catch { ReleaseHumanInput(); throw; }
        }

        private void ReleaseHumanInput()
        {
            neutralFrames = 0; LastHumanMove = Vector2.zero;
            if (Actor != null) Actor.SetMoveCommand(CharacterMoveCommand.None);
            humanInput?.Dispose(); humanInput = null;
            if (RuntimeInputActions != null) Destroy(RuntimeInputActions);
            RuntimeInputActions = null;
        }

        private void OnApplicationFocus(bool focused)
        {
            if (!UseHumanInput || !Ready) return;
            if (!focused) ReleaseHumanInput();
            else if (isActiveAndEnabled && Running)
            {
                try { EnableHumanInput(); }
                catch (Exception error) { Failure = "移動入力を再開できません: " + error.Message; StopMovement(); }
            }
        }

        private void OnEnable()
        {
            if (!Ready || !UseHumanInput || Failure.Length != 0) return;
            Running = true;
            if (!Application.isFocused) return;
            try { EnableHumanInput(); }
            catch (Exception error) { Failure = "移動入力を再開できません: " + error.Message; StopMovement(); }
        }

        private void OnDisable() { StopMovement(); ReleaseHumanInput(); }

        private void OnGUI()
        {
            var text = Failure.Length > 0 ? Failure : Ready
                ? $"route {RouteIndex + 1}/{Route.Length}, passes {CompletedPasses}, travel {TotalTravelled:F2}u, frames {Frame}/{FixedFrame}"
                : "loading independent Study map...";
            if (UseHumanInput)
            {
                var help = !Application.isFocused || !InputEnabled ? "操作は一時停止中。ゲーム画面をクリックしてください。"
                    : !Running ? "操作は停止中です。" : WaitingForNeutral ? "キーを離して操作を再開してください。"
                    : "WASD / 矢印キー / 左スティックで移動。離すと停止します。";
                GUI.Box(new Rect(12f, 46f, 720f, 48f), help + "\n" + (Failure.Length>0 ? Failure : (FollowActorCamera ? "広場 → 林道 → 橋岸を歩いて確認できます。" : "歩行テスト：木の前後・切株への衝突を確認できます。")));
            }
            else GUI.Box(new Rect(12f, 46f, 720f, 48f), "STRUCTURE TEST / ACTUAL ACTOR / AUTOMATIC COMMANDS\n" + text);
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
