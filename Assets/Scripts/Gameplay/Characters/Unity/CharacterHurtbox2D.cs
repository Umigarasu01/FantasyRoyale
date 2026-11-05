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
        public ICharacterHealthTarget Target { get; private set; }

        /// <summary>
        /// Damageを受けるHP状態を注入する。
        /// </summary>
        public void Initialize(ICharacterHealthTarget target)
        {
            Target = target ?? throw new ArgumentNullException(nameof(target));
        }
    }
}
