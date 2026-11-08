using System;
using UnityEngine;

namespace FantasyRoyale.Gameplay.Characters.Unity
{
    /// <summary>
    /// 純C# Character状態とUnityの2D Physics・足元判定を接続する本番Character Actor。
    /// 入力機器を知らず、共通移動Commandだけを物理移動へ変換する。
    /// </summary>
    [AddComponentMenu("FantasyRoyale/Gameplay/Characters/Character Actor 2D")]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody2D), typeof(CapsuleCollider2D))]
    public sealed class CharacterActor2D : MonoBehaviour, ICharacterMoveCommandTarget
    {
        [SerializeField, Min(0.1f)] private float movementSpeed = 5f;
        [SerializeField] private Vector2 footColliderSize = new Vector2(0.48f, 0.55f);
        [SerializeField] private Vector2 footColliderOffset = new Vector2(0f, 0.22f);
        [SerializeField, Min(0.01f)] private float jumpHeight = 0.5f;
        [SerializeField, Min(0.05f)] private float jumpSeconds = 0.42f;

        // 斜めの2つのキーは同時には離れない。斜めから隣の向きへ変わっても、この間は斜めの向きを保つ。
        // その間に止まれば斜めの向きのまま立つので、止まって斜めへ振れる。
        private const float DiagonalReleaseWaitSeconds = 0.08f;

        private Rigidbody2D body;
        private CapsuleCollider2D footCollider;
        private CharacterMoveCommand moveCommand;
        private float movementScale = 1f;
        private CharacterJump jump;
        private CharacterDirection8? releasedFacing;
        private float releasedAt;

        public CharacterHealth Health { get; private set; }

        /// <summary>
        /// 跳躍の状態。見た目だけの跳躍なので、足元の位置と当たり判定は変えない。
        /// </summary>
        public CharacterJump Jump => jump;
        public Rigidbody2D Body => body;
        public CapsuleCollider2D FootCollider => footCollider;
        public CharacterMoveCommand MoveCommand => moveCommand;
        public float MovementSpeed => movementSpeed;
        public float MovementScale => movementScale;

        /// <summary>
        /// 最後に移動した向き。止まっている間の攻撃方向になる。斜めのキーが少しずれて離れても斜めのまま残る。
        /// </summary>
        public CharacterDirection8 Facing { get; private set; } = CharacterDirection8.South;

        /// <summary>
        /// Spawn時に純C# Healthと移動速度を注入し、同じActorを人間・CPU・Network入力から利用可能にする。
        /// </summary>
        public void Initialize(CharacterHealth health, float newMovementSpeed)
        {
            Health = health ?? throw new ArgumentNullException(nameof(health));
            if (float.IsNaN(newMovementSpeed)
                || float.IsInfinity(newMovementSpeed)
                || newMovementSpeed <= 0f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(newMovementSpeed),
                    "Movement Speedは有限の正数である必要があります。");
            }

            movementSpeed = newMovementSpeed;
            moveCommand = CharacterMoveCommand.None;
            movementScale = 1f;
            jump = new CharacterJump(jumpHeight, jumpSeconds);
        }

        /// <summary>
        /// 生きていて地上にいれば跳ぶ。跳んでいる間も今までどおり動け、攻撃もできる。
        /// </summary>
        public bool TryJump()
        {
            return Health != null && Health.IsAlive && jump != null && jump.TryStart();
        }

        /// <summary>
        /// 入力元を区別せず最新Commandを保持し、次のFixedUpdateで一度だけ同じ規則を適用する。
        /// 攻撃中で動けない間も、入力された向きは記録する。斜めから隣の向きへの変化は少し待ってから向きに入れる。
        /// </summary>
        public void SetMoveCommand(CharacterMoveCommand command)
        {
            moveCommand = command;
            var wanted = CharacterDirections.FromVector(command.Horizontal, command.Vertical, Facing);
            if (IsDiagonalRelease(Facing, wanted))
            {
                if (releasedFacing != wanted)
                {
                    releasedFacing = wanted;
                    releasedAt = Time.time;
                }

                return;
            }

            Facing = wanted;
            releasedFacing = null;
        }

        /// <summary>
        /// 斜めの向きから、片方のキーを離した隣の向きへ変わったかを判定する。
        /// 8方向は東から順に並ぶので、奇数が斜めで、差が1なら隣である。
        /// </summary>
        private static bool IsDiagonalRelease(CharacterDirection8 shown, CharacterDirection8 wanted)
        {
            var step = ((int)wanted - (int)shown + 8) % 8;
            return (int)shown % 2 == 1 && (step == 1 || step == 7);
        }

        /// <summary>
        /// 隣の向きへ動き続けて待つ時間を過ぎたら、その向きにする。止まれば斜めの向きのまま残る。
        /// </summary>
        private void ApplyReleasedFacing()
        {
            if (releasedFacing.HasValue && moveCommand.IsMoving && Time.time - releasedAt >= DiagonalReleaseWaitSeconds)
            {
                Facing = releasedFacing.Value;
                releasedFacing = null;
            }
        }

        /// <summary>
        /// 攻撃の段階などに応じた移動速度の倍率を0から1の範囲で設定する。
        /// </summary>
        public void SetMovementScale(float scale)
        {
            movementScale = Mathf.Clamp01(scale);
        }

        /// <summary>
        /// 必須Physics Componentを取得し、既存Previewで検証済みのトップダウン足元判定へ揃える。
        /// </summary>
        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            footCollider = GetComponent<CapsuleCollider2D>();

            body.bodyType = RigidbodyType2D.Dynamic;
            body.gravityScale = 0f;
            body.constraints = RigidbodyConstraints2D.FreezeRotation;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.sleepMode = RigidbodySleepMode2D.NeverSleep;

            footCollider.direction = CapsuleDirection2D.Vertical;
            footCollider.size = footColliderSize;
            footCollider.offset = footColliderOffset;
        }

        /// <summary>
        /// 跳躍は見た目の動きなので、描く間隔で進める。倒れたらすぐに着地させる。
        /// </summary>
        private void Update()
        {
            ApplyReleasedFacing();
            if (jump == null)
            {
                return;
            }

            if (Health == null || !Health.IsAlive)
            {
                jump.Land();
                return;
            }

            jump.Advance(Time.deltaTime);
        }

        /// <summary>
        /// 生存中のCommandだけをMovePositionへ変換し、Collision解決はMapCollision LayerのPhysicsへ委ねる。
        /// </summary>
        private void FixedUpdate()
        {
            if (Health == null || !Health.IsAlive)
            {
                moveCommand = CharacterMoveCommand.None;
                body.linearVelocity = Vector2.zero;
                return;
            }

            var direction = new Vector2(moveCommand.Horizontal, moveCommand.Vertical);
            body.MovePosition(body.position + direction * (movementSpeed * movementScale * Time.fixedDeltaTime));
        }
    }
}
