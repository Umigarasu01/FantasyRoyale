using System;
using NUnit.Framework;

namespace FantasyRoyale.Gameplay.Characters.Tests.Editor
{
    /// <summary>
    /// 敵が相手に気付いて追い、離れた相手を諦めて持ち場へ戻ることを確認する。
    /// </summary>
    public sealed class CharacterChaseBrainTests
    {
        [Test]
        public void Constructor_RejectsLeashShorterThanNotice()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new CharacterChaseBrain(0f, 0f, 6f, 5f, 0.6f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new CharacterChaseBrain(0f, 0f, 0f, 5f, 0.6f));
        }

        [Test]
        public void Decide_WaitsAtHomeUntilTargetIsNear()
        {
            var brain = new CharacterChaseBrain(0f, 0f, 6f, 12f, 0.6f);

            Assert.That(brain.Decide(0f, 0f, 8f, 0f, true).IsMoving, Is.False);
            Assert.That(brain.IsChasing, Is.False);

            var command = brain.Decide(0f, 0f, 5f, 0f, true);
            Assert.That(brain.IsChasing, Is.True);
            Assert.That(command.Horizontal, Is.EqualTo(1f).Within(0.001f));
        }

        [Test]
        public void Decide_KeepsChasingBeyondNoticeUntilTheLeash()
        {
            var brain = new CharacterChaseBrain(0f, 0f, 6f, 12f, 0.6f);
            brain.Decide(0f, 0f, 5f, 0f, true);

            Assert.That(brain.Decide(1f, 0f, 9f, 0f, true).Horizontal, Is.GreaterThan(0f), "気付いた後は離れても追います。");
            Assert.That(brain.IsChasing, Is.True);

            var command = brain.Decide(5f, 0f, 13f, 0f, true);
            Assert.That(brain.IsChasing, Is.False, "持ち場から離れ過ぎた相手は諦めます。");
            Assert.That(command.Horizontal, Is.LessThan(0f), "諦めたら持ち場へ戻ります。");
        }

        [Test]
        public void Decide_StopsBesideTargetWithoutPushing()
        {
            var brain = new CharacterChaseBrain(0f, 0f, 6f, 12f, 0.6f);

            Assert.That(brain.Decide(0f, 0f, 0.5f, 0f, true).IsMoving, Is.False);
            Assert.That(brain.IsChasing, Is.True);
        }

        [Test]
        public void Decide_StopsChasingDefeatedTarget()
        {
            var brain = new CharacterChaseBrain(0f, 0f, 6f, 12f, 0.6f);
            brain.Decide(0f, 0f, 3f, 0f, true);

            brain.Decide(1f, 0f, 3f, 0f, false);
            Assert.That(brain.IsChasing, Is.False);
        }
    }
}
