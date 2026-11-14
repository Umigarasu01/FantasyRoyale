using System;
using NUnit.Framework;

namespace FantasyRoyale.Gameplay.Characters.Tests.Editor
{
    /// <summary>
    /// MPの使う、足りない、時間で戻るを確かめる。
    /// </summary>
    public sealed class CharacterManaTests
    {
        [Test]
        public void Constructor_RejectsInvalidValues()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new CharacterMana(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new CharacterMana(10, 11));
            Assert.That(new CharacterMana(10).Current, Is.EqualTo(10));
        }

        /// <summary>
        /// 足りないときは使わず、今のMPを変えない。
        /// </summary>
        [Test]
        public void TrySpend_RefusesWhenShort()
        {
            var mana = new CharacterMana(10, 4);

            Assert.That(mana.TrySpend(5), Is.False);
            Assert.That(mana.Current, Is.EqualTo(4));
            Assert.That(mana.TrySpend(4), Is.True);
            Assert.That(mana.Current, Is.Zero);
        }

        /// <summary>
        /// 1秒に戻る量の端数を持ち越し、最大を超えない。
        /// </summary>
        [Test]
        public void Regenerate_CarriesFractionsAndClamps()
        {
            var mana = new CharacterMana(10, 0);

            Assert.That(mana.Regenerate(0.25f, 2f), Is.Zero);
            Assert.That(mana.Regenerate(0.25f, 2f), Is.EqualTo(1));
            Assert.That(mana.Regenerate(10f, 2f), Is.EqualTo(9));
            Assert.That(mana.Current, Is.EqualTo(10));
        }
    }

    /// <summary>
    /// 固有の技の待ち時間が、使うと始まり、時間で減り、0で使えるようになることを確かめる。
    /// </summary>
    public sealed class CharacterCooldownTests
    {
        [Test]
        public void Cooldown_CountsDownToReady()
        {
            var cooldown = new CharacterCooldown();
            Assert.That(cooldown.IsReady, Is.True);
            Assert.That(cooldown.RemainingFraction, Is.Zero);

            cooldown.Start(2f);
            Assert.That(cooldown.IsReady, Is.False);
            Assert.That(cooldown.RemainingFraction, Is.EqualTo(1f));

            cooldown.Advance(0.5f);
            Assert.That(cooldown.RemainingFraction, Is.EqualTo(0.75f).Within(0.0001f));

            cooldown.Advance(5f);
            Assert.That(cooldown.IsReady, Is.True);
            Assert.That(cooldown.Remaining, Is.Zero);
        }
    }
}
