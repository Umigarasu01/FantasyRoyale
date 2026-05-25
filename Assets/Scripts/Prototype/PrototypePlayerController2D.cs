using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

namespace FantasyRoyale.Prototype
{
    /// <summary>
    /// 一人用プロトタイプのキーボード移動、向き、近接攻撃、宝箱インタラクトを受け持つ。
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(PrototypeHealth))]
    public sealed class PrototypePlayerController2D : MonoBehaviour
    {
        [SerializeField] private float moveSpeed = 5f;
        [SerializeField] private float attackRadius = 0.85f;
        [SerializeField] private float attackOffset = 0.85f;
        [SerializeField] private float attackCooldown = 0.35f;
        [SerializeField] private float interactRadius = 1.2f;
        [SerializeField] private int baseAttackPower = 2;
        [SerializeField] private LayerMask enemyLayerMask;
        [SerializeField] private LayerMask interactableLayerMask;
        [SerializeField] private PrototypeAttackEffect attackEffectPrefab;

        private Rigidbody2D body;
        private PrototypeHealth health;
        private Vector2 moveInput;
        private Vector2 facingDirection = Vector2.down;
        private float attackTimer;
        private int attackBonus;
        private int coins;
        private bool inputLocked;
        private readonly HashSet<PrototypeEnemy> attackHitEnemies = new();

        public PrototypeHealth Health => health;
        public int Coins => coins;
        public int AttackPower => baseAttackPower + attackBonus;
        public Vector2 FacingDirection => facingDirection;
        public bool IsMoving => moveInput.sqrMagnitude > 0.001f && !inputLocked && !health.IsDead;

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            health = GetComponent<PrototypeHealth>();
            body.gravityScale = 0f;
            body.freezeRotation = true;
        }

        /// <summary>
        /// Scene生成ツールから、仮実装で使う判定レイヤーと基本能力を設定する。
        /// </summary>
        public void ConfigureForPrototype(LayerMask enemyLayerMask, LayerMask interactableLayerMask, int hitPoints, int attackPower)
        {
            this.enemyLayerMask = enemyLayerMask;
            this.interactableLayerMask = interactableLayerMask;
            baseAttackPower = Mathf.Max(1, attackPower);
            GetComponent<PrototypeHealth>().Initialize(hitPoints);
        }

        /// <summary>
        /// Scene生成ツールから、攻撃時に出す仮エフェクトを設定する。
        /// </summary>
        public void ConfigureAttackEffect(PrototypeAttackEffect prefab)
        {
            attackEffectPrefab = prefab;
        }

        private void Update()
        {
            if (inputLocked || health.IsDead)
            {
                moveInput = Vector2.zero;
                return;
            }

            ReadKeyboardInput();
            attackTimer -= Time.deltaTime;

            if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
            {
                TryAttack();
            }

            if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
            {
                TryInteract();
            }
        }

        private void FixedUpdate()
        {
            body.linearVelocity = inputLocked || health.IsDead ? Vector2.zero : moveInput * moveSpeed;
        }

        /// <summary>
        /// WASDと矢印キーを読み取り、最後に入力された方向を攻撃の向きとして保持する。
        /// </summary>
        private void ReadKeyboardInput()
        {
            if (Keyboard.current == null)
            {
                moveInput = Vector2.zero;
                return;
            }

            var keyboard = Keyboard.current;
            var horizontal = 0f;
            var vertical = 0f;

            if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed)
            {
                horizontal -= 1f;
            }

            if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed)
            {
                horizontal += 1f;
            }

            if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed)
            {
                vertical -= 1f;
            }

            if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed)
            {
                vertical += 1f;
            }

            moveInput = new Vector2(horizontal, vertical);
            if (moveInput.sqrMagnitude > 1f)
            {
                moveInput.Normalize();
            }

            if (moveInput.sqrMagnitude > 0.001f)
            {
                facingDirection = moveInput.normalized;
            }
        }

        /// <summary>
        /// 斬撃の見た目に近い前方範囲を調べ、敵がいれば現在攻撃力分のダメージを与える。
        /// </summary>
        private void TryAttack()
        {
            if (attackTimer > 0f)
            {
                return;
            }

            attackTimer = attackCooldown;
            var origin = (Vector2)transform.position;
            var direction = facingDirection.sqrMagnitude > 0.001f ? facingDirection.normalized : Vector2.down;
            var center = origin + direction * attackOffset;
            SpawnAttackEffect(center, direction);
            DamageEnemiesInSlash(origin, center, direction);
        }

        /// <summary>
        /// 斬撃の根元と先端の両方を調べ、見た目が触れたのに外れる違和感を減らす。
        /// </summary>
        private void DamageEnemiesInSlash(Vector2 origin, Vector2 center, Vector2 direction)
        {
            attackHitEnemies.Clear();
            var innerCenter = origin + direction * (attackOffset * 0.45f);
            DamageEnemiesInCircle(innerCenter, attackRadius * 0.7f);
            DamageEnemiesInCircle(center, attackRadius);
        }

        /// <summary>
        /// 指定円内の敵へ一度だけダメージを与える。
        /// </summary>
        private void DamageEnemiesInCircle(Vector2 center, float radius)
        {
            var hits = Physics2D.OverlapCircleAll(center, radius, enemyLayerMask);
            foreach (var hit in hits)
            {
                if (hit.TryGetComponent<PrototypeEnemy>(out var enemy) && attackHitEnemies.Add(enemy))
                {
                    enemy.TakeDamage(AttackPower);
                }
            }
        }

        /// <summary>
        /// 攻撃判定と同じ位置に短命な斬撃エフェクトを出し、攻撃した手応えを見えるようにする。
        /// </summary>
        private void SpawnAttackEffect(Vector2 center, Vector2 direction)
        {
            if (attackEffectPrefab == null)
            {
                return;
            }

            var effect = Instantiate(attackEffectPrefab, center, Quaternion.identity);
            effect.gameObject.SetActive(true);
            effect.Play(direction);
        }

        /// <summary>
        /// 周囲のインタラクト対象を探し、最も近い宝箱などを起動する。
        /// </summary>
        private void TryInteract()
        {
            var hits = Physics2D.OverlapCircleAll(transform.position, interactRadius, interactableLayerMask);
            var bestDistance = float.MaxValue;
            IPrototypeInteractable closest = null;

            foreach (var hit in hits)
            {
                if (hit.TryGetComponent<IPrototypeInteractable>(out var interactable))
                {
                    var distance = Vector2.SqrMagnitude((Vector2)hit.transform.position - (Vector2)transform.position);
                    if (distance < bestDistance)
                    {
                        bestDistance = distance;
                        closest = interactable;
                    }
                }
            }

            closest?.Interact(this);
        }

        /// <summary>
        /// アイテム効果をプレイヤーに反映し、攻撃力やコインなどの成長をプロトタイプ内で確認できるようにする。
        /// </summary>
        public void ApplyItem(PrototypeItem item)
        {
            if (item == null)
            {
                return;
            }

            attackBonus += item.AttackBonus;
            coins += item.CoinBonus;
            health.Heal(item.HealAmount);
        }

        /// <summary>
        /// 敵撃破報酬のコインを加算する。
        /// </summary>
        public void AddCoins(int amount)
        {
            coins += Mathf.Max(0, amount);
        }

        /// <summary>
        /// 購入などでコインを支払えるかを確認し、足りていれば消費する。
        /// </summary>
        public bool TrySpendCoins(int amount)
        {
            var cost = Mathf.Max(0, amount);
            if (coins < cost)
            {
                return false;
            }

            coins -= cost;
            return true;
        }

        /// <summary>
        /// ゲームオーバー時など、移動と入力を止めたい場面で使う。
        /// </summary>
        public void SetInputLocked(bool locked)
        {
            inputLocked = locked;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.red;
            var direction = facingDirection.sqrMagnitude > 0.001f ? facingDirection : Vector2.down;
            Gizmos.DrawWireSphere((Vector2)transform.position + direction * attackOffset, attackRadius);

            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, interactRadius);
        }
    }
}
