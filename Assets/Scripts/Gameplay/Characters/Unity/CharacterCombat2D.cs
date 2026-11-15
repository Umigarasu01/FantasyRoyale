using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FantasyRoyale.Gameplay.Characters.Unity
{
    /// <summary>
    /// 攻撃が相手へ当たった結果。Unity側の表示へ渡すための値データ。
    /// </summary>
    public readonly struct CharacterHitEvent
    {
        public CharacterHitEvent(
            CharacterActionSpec spec,
            CharacterHurtbox2D hurtbox,
            CharacterHealthChangeResult result,
            bool knockedBack = false)
        {
            Spec = spec;
            Hurtbox = hurtbox;
            Result = result;
            KnockedBack = knockedBack;
        }

        public CharacterActionSpec Spec { get; }
        public CharacterHurtbox2D Hurtbox { get; }
        public CharacterHealthChangeResult Result { get; }

        /// <summary>
        /// 相手が押し戻されたか。アーマーで防がれた、倒れた、Actorを持たない的ではfalseである。
        /// </summary>
        public bool KnockedBack { get; }
    }

    /// <summary>
    /// 純C#のAction状態を進め、Unity Physicsで集めた候補へ攻撃を適用する本番Component。
    /// 入力機器、表示、Match進行を持たず、結果はEventで外へ渡す。
    /// </summary>
    [AddComponentMenu("FantasyRoyale/Gameplay/Characters/Character Combat 2D")]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterActor2D))]
    public sealed class CharacterCombat2D : MonoBehaviour, ICharacterActionCommandTarget
    {
        private const float ProjectileSpawnDistance = 0.4f;

        private readonly List<Collider2D> overlapBuffer = new List<Collider2D>();
        private readonly CharacterActionState actionState = new CharacterActionState();
        private readonly CharacterHitTracker hitTracker = new CharacterHitTracker();
        private CharacterActor2D actor;
        private ContactFilter2D overlapFilter;
        private LayerMask wallMask;
        private float? swingHeight;

        public event Action<CharacterActionSpec, CharacterDirection8> ActionActivated;
        public event Action<CharacterHitEvent> HitApplied;
        public event Action<CharacterProjectile2D> ProjectileLaunched;

        public CharacterActionState ActionState => actionState;
        public CharacterLoadout Loadout { get; private set; }

        /// <summary>
        /// 足元ではなく体の中心付近を攻撃の起点にする。飛び道具はここから放つ。
        /// </summary>
        public Vector2 AttackOrigin => actor.Body.position + actor.FootCollider.offset;

        /// <summary>
        /// 近接攻撃の扇の中心の、足元からの位置。斬撃もここを中心に描くので、見えている斬撃がそのまま判定になる。
        /// 振る高さを決めていなければ、足元の判定の中心である。
        /// </summary>
        public Vector2 MeleeOffset => new Vector2(actor.FootCollider.offset.x, swingHeight ?? actor.FootCollider.offset.y);

        /// <summary>
        /// 近接攻撃の扇の中心。
        /// </summary>
        public Vector2 MeleeOrigin => actor.Body.position + MeleeOffset;

        /// <summary>
        /// 武器を振る高さを、足元からのUnity単位で決める。体の絵で剣が回る中心の高さに合わせる。
        /// </summary>
        public void SetSwingHeight(float height)
        {
            if (float.IsNaN(height) || float.IsInfinity(height))
            {
                throw new ArgumentOutOfRangeException(nameof(height), "振る高さは有限の値である必要があります。");
            }

            swingHeight = height;
        }

        /// <summary>
        /// 装備状態と、飛び道具を止める壁のLayerを注入する。
        /// </summary>
        public void Initialize(CharacterLoadout loadout, LayerMask newWallMask)
        {
            Loadout = loadout ?? throw new ArgumentNullException(nameof(loadout));
            wallMask = newWallMask;
        }

        /// <summary>
        /// 生存中で待機中の場合だけ、指定SlotのActionを開始する。
        /// </summary>
        public bool TryStartAction(CharacterActionCommand command)
        {
            if (Loadout == null || actor.Health == null || !actor.Health.IsAlive || actor.IsKnockedBack)
            {
                return false;
            }

            if (!actionState.TryStart(Loadout.Get(command.Slot), command.Direction))
            {
                return false;
            }

            actor.SetMovementScale(actionState.MovementScale);
            return true;
        }

        private void Awake()
        {
            actor = GetComponent<CharacterActor2D>();
            overlapFilter = new ContactFilter2D().NoFilter();
            actor.KnockedBack += OnKnockedBack;
        }

        private void OnDestroy()
        {
            if (actor != null)
            {
                actor.KnockedBack -= OnKnockedBack;
            }
        }

        /// <summary>
        /// 押し戻されたら、攻撃は途中でも止まる。アーマーで防いだときはこれが呼ばれず、攻撃を続ける。
        /// </summary>
        private void OnKnockedBack()
        {
            if (!actionState.IsBusy)
            {
                return;
            }

            actionState.Cancel();
            actor.SetMovementScale(1f);
        }

        /// <summary>
        /// Physicsの時間でActionを進め、発動の瞬間と持続中の命中を処理する。
        /// </summary>
        private void FixedUpdate()
        {
            if (!actionState.IsBusy)
            {
                // 待機中も連撃を続けられる時間は減っていく。
                actionState.Advance(Time.fixedDeltaTime);
                return;
            }

            if (actor.Health == null || !actor.Health.IsAlive)
            {
                actionState.Cancel();
                actor.SetMovementScale(1f);
                return;
            }

            // 一度の更新で終わるとCurrentSpecが空になるため、進める前に控える。次の段へ続くと替わる。
            var previous = actionState.CurrentSpec;
            var result = actionState.Advance(Time.fixedDeltaTime);
            var spec = actionState.CurrentSpec ?? previous;
            var direction = actionState.Direction;
            if (result.EnteredActive)
            {
                Activate(spec, direction);
            }

            if (spec.Kind == CharacterActionKind.Melee
                && (result.EnteredActive || actionState.Phase == CharacterActionPhase.Active))
            {
                ResolveMeleeHits(spec, direction);
            }

            actor.SetMovementScale(actionState.MovementScale);
        }

        /// <summary>
        /// 新しい攻撃の命中記録を始め、遠隔Actionなら矢を放つ。
        /// </summary>
        private void Activate(CharacterActionSpec spec, CharacterDirection8 direction)
        {
            hitTracker.Begin(actor.Health, spec.Damage);
            ActionActivated?.Invoke(spec, direction);
            if (spec.Kind == CharacterActionKind.Projectile)
            {
                LaunchProjectile(spec, direction);
            }
        }

        /// <summary>
        /// 距離内の候補をPhysicsで集め、形の判定と多重命中の防止はCoreへ任せる。
        /// 扇は振る高さに置き、相手の当たり範囲と比べる。相手の当たり範囲は見えている体に合わせる。
        /// </summary>
        private void ResolveMeleeHits(CharacterActionSpec spec, CharacterDirection8 direction)
        {
            var origin = MeleeOrigin;
            overlapBuffer.Clear();
            Physics2D.OverlapCircle(origin, spec.Range, overlapFilter, overlapBuffer);
            for (var index = 0; index < overlapBuffer.Count; index++)
            {
                var candidate = overlapBuffer[index];
                var hurtbox = candidate.GetComponentInParent<CharacterHurtbox2D>();
                if (hurtbox == null || hurtbox.Target == null)
                {
                    continue;
                }

                var offset = candidate.ClosestPoint(origin) - origin;
                if (spec.IsInMeleeArea(offset.x, offset.y, direction)
                    && hitTracker.TryHit(hurtbox.Target, out var result))
                {
                    var knockedBack = hurtbox.TryKnockBack(MeleePushDirection(hurtbox, direction), spec.Knockback);
                    HitApplied?.Invoke(new CharacterHitEvent(spec, hurtbox, result, knockedBack));
                }
            }
        }

        /// <summary>
        /// 近接攻撃で相手を押す向き。攻撃者の足元から相手の足元への向きで、重なっていれば振った向きにする。
        /// </summary>
        private Vector2 MeleePushDirection(CharacterHurtbox2D hurtbox, CharacterDirection8 direction)
        {
            var away = (Vector2)hurtbox.transform.position - actor.Body.position;
            if (away.sqrMagnitude > 1e-6f)
            {
                return away;
            }

            CharacterDirections.ToUnitVector(direction, out var x, out var y);
            return new Vector2(x, y);
        }

        /// <summary>
        /// 攻撃者と同じSceneへ矢を作り、命中結果を自身のEventへ中継する。
        /// </summary>
        private void LaunchProjectile(CharacterActionSpec spec, CharacterDirection8 direction)
        {
            CharacterDirections.ToUnitVector(direction, out var x, out var y);
            var unitDirection = new Vector2(x, y);
            var projectileObject = new GameObject($"{spec.Id}-projectile");
            SceneManager.MoveGameObjectToScene(projectileObject, gameObject.scene);
            var projectile = projectileObject.AddComponent<CharacterProjectile2D>();
            projectile.HitApplied += hit => HitApplied?.Invoke(hit);
            projectile.Launch(
                actor.Health,
                spec,
                unitDirection,
                AttackOrigin + unitDirection * ProjectileSpawnDistance,
                wallMask);
            ProjectileLaunched?.Invoke(projectile);
        }
    }
}
