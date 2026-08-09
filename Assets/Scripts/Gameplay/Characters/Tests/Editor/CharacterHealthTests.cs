using System;
using NUnit.Framework;

namespace FantasyRoyale.Gameplay.Characters.Tests.Editor
{
    /// <summary>
    /// Unity Sceneを使わず、本番Character HPの初期化、Clamp、撃破、通常回復非復活を検証する。
    /// </summary>
    public sealed class CharacterHealthTests
    {
        /// <summary>
        /// 最大HPと初期HPの不正値を生成時に拒否し、壊れたCharacter状態を作らないことを確認する。
        /// </summary>
        [Test]
        public void Constructor_RejectsInvalidMaximumOrStartingHealth()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new CharacterHealth(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new CharacterHealth(100, -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new CharacterHealth(100, 101));

            var health = new CharacterHealth(100, 40);
            Assert.That(health.MaximumHealth, Is.EqualTo(100));
            Assert.That(health.CurrentHealth, Is.EqualTo(40));
            Assert.That(health.IsAlive, Is.True);
        }

        /// <summary>
        /// 過剰ダメージを現在HPまでにClampし、HP 0への一度の遷移だけを撃破として返す。
        /// </summary>
        [Test]
        public void ApplyDamage_ClampsAtZeroAndReportsDefeatTransition()
        {
            var health = new CharacterHealth(100, 40);

            var result = health.ApplyDamage(75);

            Assert.That(result.Outcome, Is.EqualTo(CharacterHealthChangeOutcome.Applied));
            Assert.That(result.Kind, Is.EqualTo(CharacterHealthChangeKind.Damage));
            Assert.That(result.RequestedAmount, Is.EqualTo(75));
            Assert.That(result.AppliedAmount, Is.EqualTo(40));
            Assert.That(result.PreviousHealth, Is.EqualTo(40));
            Assert.That(result.CurrentHealth, Is.Zero);
            Assert.That(result.BecameDefeated, Is.True);
            Assert.That(health.IsAlive, Is.False);
        }

        /// <summary>
        /// 死亡後の追加ダメージと通常回復を拒否し、将来の復活専用処理と混同しないことを確認する。
        /// </summary>
        [Test]
        public void DefeatedCharacter_RejectsDamageAndNormalHealing()
        {
            var health = new CharacterHealth(100, 10);
            health.ApplyDamage(10);

            var damageResult = health.ApplyDamage(5);
            var healingResult = health.RestoreHealth(30);

            Assert.That(damageResult.Outcome, Is.EqualTo(CharacterHealthChangeOutcome.Rejected));
            Assert.That(healingResult.Outcome, Is.EqualTo(CharacterHealthChangeOutcome.Rejected));
            Assert.That(damageResult.AppliedAmount, Is.Zero);
            Assert.That(healingResult.AppliedAmount, Is.Zero);
            Assert.That(health.CurrentHealth, Is.Zero);
            Assert.That(health.IsAlive, Is.False);
        }

        /// <summary>
        /// 回復を最大HPまでにClampし、実際に増えた量だけを結果へ返すことを確認する。
        /// </summary>
        [Test]
        public void RestoreHealth_ClampsAtMaximumAndReportsAppliedAmount()
        {
            var health = new CharacterHealth(100, 85);

            var result = health.RestoreHealth(30);

            Assert.That(result.Outcome, Is.EqualTo(CharacterHealthChangeOutcome.Applied));
            Assert.That(result.Kind, Is.EqualTo(CharacterHealthChangeKind.Healing));
            Assert.That(result.AppliedAmount, Is.EqualTo(15));
            Assert.That(result.PreviousHealth, Is.EqualTo(85));
            Assert.That(result.CurrentHealth, Is.EqualTo(100));
            Assert.That(result.BecameDefeated, Is.False);
            Assert.That(health.CurrentHealth, Is.EqualTo(100));
        }

        /// <summary>
        /// 0以下の変更量を不正として返し、ダメージと回復のどちらでもHPを変更しないことを確認する。
        /// </summary>
        [Test]
        public void NonPositiveRequests_AreInvalidAndDoNotMutateHealth()
        {
            var health = new CharacterHealth(100, 50);

            var damageResult = health.ApplyDamage(0);
            var healingResult = health.RestoreHealth(-10);

            Assert.That(damageResult.Outcome, Is.EqualTo(CharacterHealthChangeOutcome.Invalid));
            Assert.That(healingResult.Outcome, Is.EqualTo(CharacterHealthChangeOutcome.Invalid));
            Assert.That(health.CurrentHealth, Is.EqualTo(50));
        }
    }
}
