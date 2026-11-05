using System;
using System.Collections.Generic;
using UnityEngine;

namespace FantasyRoyale.Gameplay.Characters.Unity
{
    /// <summary>
    /// 直進する飛び道具。最初に当たった相手か壁で消え、射程まで進むと消える。
    /// 表示を持たないので、見た目は受け取った側が子や同じGameObjectへ追加する。
    /// </summary>
    [AddComponentMenu("")]
    [DisallowMultipleComponent]
    public sealed class CharacterProjectile2D : MonoBehaviour
    {
        private const float CastRadius = 0.12f;

        private readonly List<RaycastHit2D> castBuffer = new List<RaycastHit2D>();
        private readonly CharacterHitTracker hitTracker = new CharacterHitTracker();
        private ICharacterHealthTarget attacker;
        private ContactFilter2D castFilter;
        private LayerMask wallMask;
        private float travelled;

        public event Action<CharacterHitEvent> HitApplied;

        public CharacterActionSpec Spec { get; private set; }
        public Vector2 Direction { get; private set; }

        /// <summary>
        /// 攻撃者、Action、単位方向、開始位置、止まる壁のLayerを受け取って飛び始める。
        /// </summary>
        public void Launch(
            ICharacterHealthTarget newAttacker,
            CharacterActionSpec spec,
            Vector2 direction,
            Vector2 origin,
            LayerMask newWallMask)
        {
            attacker = newAttacker ?? throw new ArgumentNullException(nameof(newAttacker));
            Spec = spec ?? throw new ArgumentNullException(nameof(spec));
            Direction = direction;
            wallMask = newWallMask;
            transform.position = origin;
            castFilter = new ContactFilter2D().NoFilter();
            hitTracker.Begin(attacker, spec.Damage);
        }

        /// <summary>
        /// 次の移動区間を先に調べ、速い矢でも相手や壁をすり抜けないようにする。
        /// </summary>
        private void FixedUpdate()
        {
            if (Spec == null)
            {
                return;
            }

            var position = (Vector2)transform.position;
            var step = Mathf.Min(Spec.ProjectileSpeed * Time.fixedDeltaTime, Spec.Range - travelled);
            castBuffer.Clear();
            Physics2D.CircleCast(position, CastRadius, Direction, castFilter, castBuffer, step);
            castBuffer.Sort((left, right) => left.distance.CompareTo(right.distance));
            for (var index = 0; index < castBuffer.Count; index++)
            {
                if (TryStopAt(castBuffer[index].collider))
                {
                    Destroy(gameObject);
                    return;
                }
            }

            transform.position = position + Direction * step;
            travelled += step;
            if (travelled >= Spec.Range)
            {
                Destroy(gameObject);
            }
        }

        /// <summary>
        /// 相手へ当たるか壁へぶつかった場合にtrueを返す。攻撃者と倒れた相手は通り抜ける。
        /// </summary>
        private bool TryStopAt(Collider2D candidate)
        {
            var hurtbox = candidate.GetComponentInParent<CharacterHurtbox2D>();
            if (hurtbox != null && hurtbox.Target != null)
            {
                if (!hitTracker.TryHit(hurtbox.Target, out var result))
                {
                    return false;
                }

                HitApplied?.Invoke(new CharacterHitEvent(Spec, hurtbox, result));
                return true;
            }

            return !candidate.isTrigger
                   && (wallMask.value & (1 << candidate.gameObject.layer)) != 0;
        }
    }
}
