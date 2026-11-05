using System;
using System.Collections.Generic;

namespace FantasyRoyale.Gameplay.Characters
{
    /// <summary>
    /// 一回の攻撃で同じ相手へ複数回Damageを与えないようにする純C#状態。
    /// Unity Physicsは候補を集めるだけで、Damageの適用可否はここで決める。
    /// </summary>
    public sealed class CharacterHitTracker
    {
        private readonly HashSet<ICharacterHealthTarget> hitTargets = new HashSet<ICharacterHealthTarget>();
        private ICharacterHealthTarget attacker;
        private int damage;

        /// <summary>
        /// 新しい攻撃を始め、攻撃者とDamageを記録して命中済みの相手を空にする。
        /// </summary>
        public void Begin(ICharacterHealthTarget newAttacker, int newDamage)
        {
            attacker = newAttacker ?? throw new ArgumentNullException(nameof(newAttacker));
            if (newDamage <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(newDamage), "Damageは1以上である必要があります。");
            }

            damage = newDamage;
            hitTargets.Clear();
        }

        /// <summary>
        /// 攻撃者自身と命中済みの相手を除いてDamageを適用し、実際に当たった場合だけtrueを返す。
        /// </summary>
        public bool TryHit(ICharacterHealthTarget target, out CharacterHealthChangeResult result)
        {
            result = default;
            if (attacker == null
                || target == null
                || ReferenceEquals(target, attacker)
                || hitTargets.Contains(target))
            {
                return false;
            }

            result = target.ApplyDamage(damage);
            if (!result.IsApplied)
            {
                return false;
            }

            hitTargets.Add(target);
            return true;
        }
    }
}
