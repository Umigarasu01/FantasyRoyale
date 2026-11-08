using FantasyRoyale.Gameplay.Characters.Unity;
using NUnit.Framework;

namespace FantasyRoyale.Gameplay.Characters.Tests.Editor
{
    /// <summary>
    /// 斬撃の絵が、近接攻撃の判定の大きさをそのまま見せることを確認する。
    /// </summary>
    public sealed class CharacterSlashSpriteTests
    {
        private const float PixelTolerance = 2f / CharacterSlashSprites.PixelsPerUnit;

        /// <summary>
        /// 斬撃の外側の縁は判定の距離で、同じ武器種でも距離の長い武器ほど斬撃が大きい。
        /// </summary>
        [Test]
        public void Get_SizesSlashToRange()
        {
            var sword = CharacterSlashSprites.Get(1f, 180f);
            var longSword = CharacterSlashSprites.Get(1.6f, 180f);

            Assert.That(sword.bounds.extents.x, Is.InRange(1f, 1f + PixelTolerance));
            Assert.That(longSword.bounds.extents.x, Is.InRange(1.6f, 1.6f + PixelTolerance));
            Assert.That(sword.bounds.center.x, Is.EqualTo(0f).Within(0.001f), "軸は扇の中心です。");
            Assert.That(sword.bounds.center.y, Is.EqualTo(0f).Within(0.001f), "軸は扇の中心です。");
        }

        /// <summary>
        /// 同じ距離と幅の斬撃は作り直さない。幅が違えば別の絵になる。
        /// </summary>
        [Test]
        public void Get_ReusesSpriteForSameArea()
        {
            var first = CharacterSlashSprites.Get(1.2f, 180f);

            Assert.That(CharacterSlashSprites.Get(1.2f, 180f), Is.SameAs(first));
            Assert.That(CharacterSlashSprites.Get(1.2f, 120f), Is.Not.SameAs(first));
        }
    }
}
