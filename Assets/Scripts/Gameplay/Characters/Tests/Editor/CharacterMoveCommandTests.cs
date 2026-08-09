using System;
using NUnit.Framework;

namespace FantasyRoyale.Gameplay.Characters.Tests.Editor
{
    /// <summary>
    /// 入力元やUnity Sceneを使わず、共通移動Commandの正規化と停止値を検証する。
    /// </summary>
    public sealed class CharacterMoveCommandTests
    {
        /// <summary>
        /// 単位円内のアナログ入力を弱めず、そのまま保持することを確認する。
        /// </summary>
        [Test]
        public void Constructor_PreservesInputInsideUnitCircle()
        {
            var command = new CharacterMoveCommand(0.3f, -0.4f);

            Assert.That(command.Horizontal, Is.EqualTo(0.3f).Within(0.0001f));
            Assert.That(command.Vertical, Is.EqualTo(-0.4f).Within(0.0001f));
            Assert.That(command.MagnitudeSquared, Is.EqualTo(0.25f).Within(0.0001f));
            Assert.That(command.IsMoving, Is.True);
        }

        /// <summary>
        /// 斜め入力を単位長へClampし、Cardinal移動より高速にならないことを確認する。
        /// </summary>
        [Test]
        public void Constructor_ClampsDiagonalInputToUnitMagnitude()
        {
            var command = new CharacterMoveCommand(1f, 1f);

            Assert.That(command.MagnitudeSquared, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(command.Horizontal, Is.EqualTo(command.Vertical).Within(0.0001f));
        }

        /// <summary>
        /// 入力なしの共通値が移動しないCommandとして扱われることを確認する。
        /// </summary>
        [Test]
        public void None_RepresentsStoppedMovement()
        {
            Assert.That(CharacterMoveCommand.None.Horizontal, Is.Zero);
            Assert.That(CharacterMoveCommand.None.Vertical, Is.Zero);
            Assert.That(CharacterMoveCommand.None.IsMoving, Is.False);
        }

        /// <summary>
        /// NaNやInfinityを拒否し、Physicsへ不正座標を伝播させないことを確認する。
        /// </summary>
        [Test]
        public void Constructor_RejectsNonFiniteInput()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new CharacterMoveCommand(float.NaN, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new CharacterMoveCommand(0f, float.PositiveInfinity));
        }
    }
}
