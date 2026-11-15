using System;
using UnityEngine;

namespace FantasyRoyale.Gameplay.Characters.Unity
{
    /// <summary>
    /// 攻撃の候補となるColliderと、Damageを受ける純C#のHP状態を結び付ける。
    /// 同じGameObjectまたは子のCollider2Dを当たり範囲として使う。
    /// </summary>
    [AddComponentMenu("FantasyRoyale/Gameplay/Characters/Character Hurtbox 2D")]
    [DisallowMultipleComponent]
    public sealed class CharacterHurtbox2D : MonoBehaviour
    {
        private CharacterActor2D actor;

        public ICharacterHealthTarget Target { get; private set; }

        /// <summary>
        /// Damageを受けるHP状態を注入する。
        /// </summary>
        public void Initialize(ICharacterHealthTarget target)
        {
            Target = target ?? throw new ArgumentNullException(nameof(target));
        }

        /// <summary>
        /// 同じGameObjectか親のActorを押し戻す。Actorを持たない的は押されない。押されたらtrueを返す。
        /// </summary>
        public bool TryKnockBack(Vector2 direction, float power)
        {
            if (actor == null)
            {
                actor = GetComponentInParent<CharacterActor2D>();
            }

            return actor != null && actor.TryKnockBack(direction, power);
        }
    }
}
