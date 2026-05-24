using UnityEngine;

namespace FantasyRoyale.Prototype
{
    /// <summary>
    /// プレイヤーを追いかけ、接触間隔ごとにダメージを与える最小構成の雑魚敵。
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(PrototypeHealth))]
    public sealed class PrototypeEnemy : MonoBehaviour
    {
        [SerializeField] private float moveSpeed = 2.2f;
        [SerializeField] private int contactDamage = 1;
        [SerializeField] private float damageInterval = 0.8f;
        [SerializeField] private int coinReward = 2;
        [SerializeField] private int prototypeHitPoints = 2;

        private Rigidbody2D body;
        private PrototypeHealth health;
        private Transform target;
        private PrototypePlayerController2D rewardTarget;
        private float damageTimer;

        public bool IsMoving => body != null && body.linearVelocity.sqrMagnitude > 0.001f && !health.IsDead;

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            health = GetComponent<PrototypeHealth>();
            body.gravityScale = 0f;
            body.freezeRotation = true;
            // 既存Sceneに残ったPrototypeHealthの初期値を使わず、敵側のプロトタイプHPで必ず上書きする。
            health.Initialize(prototypeHitPoints);
            health.Died += OnDied;
        }

        private void OnDestroy()
        {
            if (health != null)
            {
                health.Died -= OnDied;
            }
        }

        private void FixedUpdate()
        {
            if (target == null || health.IsDead)
            {
                body.linearVelocity = Vector2.zero;
                return;
            }

            var direction = ((Vector2)target.position - body.position).normalized;
            body.linearVelocity = direction * moveSpeed;
        }

        private void Update()
        {
            damageTimer -= Time.deltaTime;
        }

        /// <summary>
        /// 追跡対象と撃破報酬の渡し先を設定する。
        /// </summary>
        public void Initialize(Transform target, PrototypePlayerController2D rewardTarget, int hitPoints)
        {
            this.target = target;
            this.rewardTarget = rewardTarget;
            prototypeHitPoints = Mathf.Max(1, hitPoints);
            health.Initialize(hitPoints);
        }

        /// <summary>
        /// プレイヤー攻撃から受けたダメージをHPへ渡す。
        /// </summary>
        public void TakeDamage(int damage)
        {
            health.TakeDamage(damage);
        }

        private void OnCollisionStay2D(Collision2D collision)
        {
            if (damageTimer > 0f || !collision.collider.TryGetComponent<PrototypePlayerController2D>(out var player))
            {
                return;
            }

            damageTimer = damageInterval;
            player.Health.TakeDamage(contactDamage);
        }

        /// <summary>
        /// 死亡時に報酬を渡して敵を消し、地道な強化ルートを成立させる。
        /// </summary>
        private void OnDied(PrototypeHealth deadHealth)
        {
            rewardTarget?.AddCoins(coinReward);
            Destroy(gameObject);
        }
    }
}
