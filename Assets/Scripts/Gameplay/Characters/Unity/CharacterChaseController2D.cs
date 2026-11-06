using System;
using UnityEngine;

namespace FantasyRoyale.Gameplay.Characters.Unity
{
    /// <summary>
    /// 相手を追い、触れたときにダメージを与える敵の本番Component。
    /// 判断は純C#のBrainへ任せ、結果を人間と同じActorのCommandとして渡す。
    /// </summary>
    [AddComponentMenu("FantasyRoyale/Gameplay/Characters/Character Chase Controller 2D")]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterActor2D))]
    public sealed class CharacterChaseController2D : MonoBehaviour
    {
        private CharacterActor2D actor;
        private CharacterChaseBrain brain;
        private CharacterActor2D target;
        private ICharacterHealthTarget targetHealth;
        private float contactRange;
        private int contactDamage;
        private float contactInterval;
        private float nextContactAt;

        /// <summary>
        /// 相手に触れてダメージを与えたときに発生する。
        /// </summary>
        public event Action<CharacterHealthChangeResult> ContactApplied;

        public bool IsChasing => brain != null && brain.IsChasing;

        /// <summary>
        /// 相手の無敵時間などの間、触れてもダメージを与えないようにする。
        /// </summary>
        public bool ContactEnabled { get; set; } = true;

        /// <summary>
        /// 判断の規則と追う相手、触れたときのダメージを注入する。
        /// </summary>
        public void Initialize(
            CharacterChaseBrain newBrain,
            CharacterActor2D newTarget,
            ICharacterHealthTarget newTargetHealth,
            float newContactRange,
            int newContactDamage,
            float newContactInterval)
        {
            brain = newBrain ?? throw new ArgumentNullException(nameof(newBrain));
            target = newTarget != null ? newTarget : throw new ArgumentNullException(nameof(newTarget));
            targetHealth = newTargetHealth ?? throw new ArgumentNullException(nameof(newTargetHealth));
            contactRange = newContactRange;
            contactDamage = newContactDamage;
            contactInterval = newContactInterval;
        }

        private void Awake()
        {
            actor = GetComponent<CharacterActor2D>();
        }

        private void Update()
        {
            if (brain == null || actor.Health == null || !actor.Health.IsAlive)
            {
                return;
            }

            var self = actor.Body.position;
            var other = target.Body.position;
            actor.SetMoveCommand(brain.Decide(self.x, self.y, other.x, other.y, targetHealth.IsAlive));
            if (ContactEnabled
                && targetHealth.IsAlive
                && Time.time >= nextContactAt
                && Vector2.Distance(self, other) <= contactRange)
            {
                nextContactAt = Time.time + contactInterval;
                // 購読者がいなくてもダメージは必ず与える。
                var result = targetHealth.ApplyDamage(contactDamage);
                ContactApplied?.Invoke(result);
            }
        }
    }
}
