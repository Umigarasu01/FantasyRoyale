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

        private Rigidbody2D body;
        private CapsuleCollider2D footCollider;
        private CharacterMoveCommand moveCommand;

        public CharacterHealth Health { get; private set; }
        public Rigidbody2D Body => body;
        public CapsuleCollider2D FootCollider => footCollider;
        public CharacterMoveCommand MoveCommand => moveCommand;
        public float MovementSpeed => movementSpeed;

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
        }

        /// <summary>
        /// 入力元を区別せず最新Commandを保持し、次のFixedUpdateで一度だけ同じ規則を適用する。
        /// </summary>
        public void SetMoveCommand(CharacterMoveCommand command)
        {
            moveCommand = command;
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
            body.MovePosition(body.position + direction * (movementSpeed * Time.fixedDeltaTime));
        }
    }
}
