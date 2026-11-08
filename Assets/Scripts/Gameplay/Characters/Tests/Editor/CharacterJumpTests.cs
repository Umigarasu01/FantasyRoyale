using System;
using NUnit.Framework;

namespace FantasyRoyale.Gameplay.Characters.Tests.Editor
{
    /// <summary>
    /// Unity Sceneを使わず、跳躍の高さの放物線と、地上でだけ跳べる規則を検証する。
    /// </summary>
    public sealed class CharacterJumpTests
    {
        /// <summary>
        /// 踏み切りと着地で高さ0、中ほどで一番高くなり、時間を過ぎると着地する。
        /// </summary>
        [Test]
        public void Height_RisesToThePeakAndLands()
        {
            var jump = new CharacterJump(0.5f, 0.4f);
            Assert.That(jump.IsAirborne, Is.False);
            Assert.That(jump.Height, Is.EqualTo(0f));

            Assert.That(jump.TryStart(), Is.True);
            Assert.That(jump.IsAirborne, Is.True);
            Assert.That(jump.Height, Is.EqualTo(0f));

            jump.Advance(0.1f);
            var rising = jump.Height;
            jump.Advance(0.1f);
            Assert.That(jump.Height, Is.EqualTo(0.5f).Within(0.0001f));
            Assert.That(rising, Is.GreaterThan(0f).And.LessThan(0.5f));

            jump.Advance(0.1f);
            Assert.That(jump.Height, Is.EqualTo(rising).Within(0.0001f));
            jump.Advance(0.1f);
            Assert.That(jump.IsAirborne, Is.False);
            Assert.That(jump.Height, Is.EqualTo(0f));
        }

        /// <summary>
        /// 跳んでいる間は次の跳躍を受け付けず、着地すればまた跳べる。
        /// </summary>
        [Test]
        public void TryStart_OnlyWorksOnTheGround()
        {
            var jump = new CharacterJump(0.5f, 0.4f);
            Assert.That(jump.TryStart(), Is.True);
            jump.Advance(0.2f);
            Assert.That(jump.TryStart(), Is.False);
            Assert.That(jump.Height, Is.EqualTo(0.5f).Within(0.0001f));

            jump.Land();
            Assert.That(jump.IsAirborne, Is.False);
            Assert.That(jump.TryStart(), Is.True);
        }

        /// <summary>
        /// 高さ、秒数、経過秒数の不正な値は例外で止める。
        /// </summary>
        [Test]
        public void InvalidValues_Throw()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new CharacterJump(0f, 0.4f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new CharacterJump(0.5f, float.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => new CharacterJump(0.5f, 0.4f).Advance(-0.1f));
        }
    }
}
