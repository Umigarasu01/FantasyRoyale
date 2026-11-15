using System;
using NUnit.Framework;

namespace FantasyRoyale.Gameplay.Characters.Tests.Editor
{
    /// <summary>
    /// 押し戻しの距離、向き、アーマーによる軽減と無効化を確認する。
    /// </summary>
    public sealed class CharacterKnockbackTests
    {
        /// <summary>
        /// 押し戻しの時間を進め切ると、攻撃のKnockbackの距離だけ指定の向きへ進むことを確認する。
        /// </summary>
        [Test]
        public void Knockback_TravelsFullDistanceAlongNormalizedDirection()
        {
            var knockback = new CharacterKnockback();

            Assert.That(knockback.TryStart(0.6f, 3f, 4f), Is.True);
            Assert.That(knockback.IsActive, Is.True);
            Assert.That(knockback.DirectionX, Is.EqualTo(0.6f).Within(1e-5f));
            Assert.That(knockback.DirectionY, Is.EqualTo(0.8f).Within(1e-5f));

            var travelled = 0f;
            var firstStep = knockback.Advance(0.02f);
            travelled += firstStep;
            var lastStep = firstStep;
            while (knockback.IsActive)
            {
                lastStep = knockback.Advance(0.02f);
                travelled += lastStep;
            }

            Assert.That(travelled, Is.EqualTo(0.6f).Within(1e-4f));
            Assert.That(lastStep, Is.LessThan(firstStep), "押し戻しは初めが速く、だんだん止まる。");
            Assert.That(knockback.Advance(0.02f), Is.EqualTo(0f));
        }

        /// <summary>
        /// アーマーの分だけ押し戻しが短くなり、Knockback以上のアーマーでは押されないことを確認する。
        /// </summary>
        [Test]
        public void Armor_ReducesOrBlocksKnockback()
        {
            var knockback = new CharacterKnockback(0.25f);
            Assert.That(knockback.TryStart(0.6f, 1f, 0f), Is.True);
            Assert.That(knockback.Distance, Is.EqualTo(0.35f).Within(1e-5f));

            var heavy = new CharacterKnockback(1f);
            Assert.That(heavy.TryStart(0.9f, 1f, 0f), Is.False);
            Assert.That(heavy.TryStart(1f, 1f, 0f), Is.False);
            Assert.That(heavy.IsActive, Is.False);

            var superArmor = new CharacterKnockback(float.PositiveInfinity);
            Assert.That(superArmor.TryStart(100f, 1f, 0f), Is.False);

            heavy.SetArmor(0f);
            Assert.That(heavy.TryStart(0.9f, 1f, 0f), Is.True);
        }

        /// <summary>
        /// 軽い相手ほど遠くへ飛び、重さで割った後にアーマーを引くことを確認する。
        /// </summary>
        [Test]
        public void Weight_DividesKnockbackBeforeArmor()
        {
            var slime = new CharacterKnockback(weight: 0.5f);
            Assert.That(slime.TryStart(0.4f, 1f, 0f), Is.True);
            Assert.That(slime.Distance, Is.EqualTo(0.8f).Within(1e-5f));

            var heavy = new CharacterKnockback(0.5f, 2f);
            Assert.That(heavy.TryStart(0.9f, 1f, 0f), Is.False, "0.9 / 2 = 0.45はアーマー0.5に届かない。");
            Assert.That(heavy.TryStart(2f, 1f, 0f), Is.True);
            Assert.That(heavy.Distance, Is.EqualTo(0.5f).Within(1e-5f));
        }

        /// <summary>
        /// 0のKnockback、向きのない押し戻し、不正なアーマーと秒数を受け付けないことを確認する。
        /// </summary>
        [Test]
        public void Knockback_RejectsInvalidInput()
        {
            var knockback = new CharacterKnockback();

            Assert.That(knockback.TryStart(0f, 1f, 0f), Is.False);
            Assert.That(knockback.TryStart(float.NaN, 1f, 0f), Is.False);
            Assert.That(knockback.TryStart(0.5f, 0f, 0f), Is.False);
            Assert.Throws<ArgumentOutOfRangeException>(() => new CharacterKnockback(-1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new CharacterKnockback(0f, durationSeconds: 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new CharacterKnockback(0f, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => knockback.SetWeight(float.PositiveInfinity));
            Assert.Throws<ArgumentOutOfRangeException>(() => knockback.SetArmor(float.NaN));
        }

        /// <summary>
        /// 押し戻し中に当たり直すと新しい向きでやり直し、Cancelで止まることを確認する。
        /// </summary>
        [Test]
        public void Knockback_RestartsOnNewHitAndCancels()
        {
            var knockback = new CharacterKnockback();
            knockback.TryStart(0.5f, 1f, 0f);
            knockback.Advance(0.1f);

            Assert.That(knockback.TryStart(0.8f, 0f, -1f), Is.True);
            Assert.That(knockback.DirectionY, Is.EqualTo(-1f));
            Assert.That(knockback.Distance, Is.EqualTo(0.8f));

            knockback.Cancel();
            Assert.That(knockback.IsActive, Is.False);
            Assert.That(knockback.Distance, Is.EqualTo(0f));
        }

        /// <summary>
        /// ActionのKnockbackは0以上の有限値だけを受け付け、指定しなければ0であることを確認する。
        /// </summary>
        [Test]
        public void ActionSpec_ValidatesKnockback()
        {
            var timing = new CharacterActionTiming(0.1f, 0.1f, 0.1f);

            Assert.That(CharacterActionSpec.CreateMelee("slash", 1, timing, 1f, 90f).Knockback, Is.EqualTo(0f));
            Assert.That(
                CharacterActionSpec.CreateMelee("slash", 1, timing, 1f, 90f, knockback: 0.4f).Knockback,
                Is.EqualTo(0.4f));
            Assert.That(
                CharacterActionSpec.CreateProjectile("shot", 1, timing, 5f, 10f, knockback: 0.3f).Knockback,
                Is.EqualTo(0.3f));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                CharacterActionSpec.CreateMelee("slash", 1, timing, 1f, 90f, knockback: -0.1f));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                CharacterActionSpec.CreateProjectile("shot", 1, timing, 5f, 10f, knockback: float.PositiveInfinity));
        }
    }
}
