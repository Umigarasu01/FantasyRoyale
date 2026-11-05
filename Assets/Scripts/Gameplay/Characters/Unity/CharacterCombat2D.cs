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
            CharacterHealthChangeResult result)
        {
            Spec = spec;
            Hurtbox = hurtbox;
            Result = result;
        }

        public CharacterActionSpec Spec { get; }
        public CharacterHurtbox2D Hurtbox { get; }
        public CharacterHealthChangeResult Result { get; }
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

        public event Action<CharacterActionSpec, CharacterDirection8> ActionActivated;
        public event Action<CharacterHitEvent> HitApplied;
        public event Action<CharacterProjectile2D> ProjectileLaunched;

        public CharacterActionState ActionState => actionState;
        public CharacterLoadout Loadout { get; private set; }

        /// <summary>
        /// 足元ではなく体の中心付近を攻撃の起点にする。
        /// </summary>
        public Vector2 AttackOrigin => actor.Body.position + actor.FootCollider.offset;

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
            if (Loadout == null || actor.Health == null || !actor.Health.IsAlive)
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
        }

        /// <summary>
        /// Physicsの時間でActionを進め、発動の瞬間と持続中の命中を処理する。
        /// </summary>
        private void FixedUpdate()
        {
            if (!actionState.IsBusy)
            {
                return;
            }

            if (actor.Health == null || !actor.Health.IsAlive)
            {
                actionState.Cancel();
                actor.SetMovementScale(1f);
                return;
            }

            // 一度の更新で終わるとCurrentSpecが空になるため、進める前に控える。
            var spec = actionState.CurrentSpec;
            var direction = actionState.Direction;
            var result = actionState.Advance(Time.fixedDeltaTime);
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
        /// </summary>
        private void ResolveMeleeHits(CharacterActionSpec spec, CharacterDirection8 direction)
        {
            var origin = AttackOrigin;
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
                    HitApplied?.Invoke(new CharacterHitEvent(spec, hurtbox, result));
                }
            }
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
