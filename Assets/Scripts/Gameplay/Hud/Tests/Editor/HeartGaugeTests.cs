using System;
using NUnit.Framework;

namespace FantasyRoyale.Gameplay.Hud.Tests.Editor
{
    /// <summary>
    /// HPをハートの並びに直す規則を、Sceneを使わずに確かめる。
    /// </summary>
    public sealed class HeartGaugeTests
    {
        /// <summary>
        /// 最大HPを入れるのに要るハートの数を、ハート1つ分のHPで切り上げて数える。
        /// </summary>
        [Test]
        public void CountHearts_RoundsUpToWholeHearts()
        {
            Assert.That(HeartGauge.CountHearts(100, 20), Is.EqualTo(5));
            Assert.That(HeartGauge.CountHearts(110, 20), Is.EqualTo(6), "端数も1つのハートに入れます。");
            Assert.That(HeartGauge.CountHearts(0, 20), Is.EqualTo(0));
        }

        /// <summary>
        /// 左から満ち、半分のハートが1つだけ挟まり、残りは空になる。
        /// </summary>
        [Test]
        public void GetFill_FillsFromTheLeftInHalves()
        {
            Assert.That(Fills(100), Is.EqualTo(new[] { HeartFill.Full, HeartFill.Full, HeartFill.Full, HeartFill.Full, HeartFill.Full }));
            Assert.That(Fills(70), Is.EqualTo(new[] { HeartFill.Full, HeartFill.Full, HeartFill.Full, HeartFill.Half, HeartFill.Empty }));
            Assert.That(Fills(60), Is.EqualTo(new[] { HeartFill.Full, HeartFill.Full, HeartFill.Full, HeartFill.Empty, HeartFill.Empty }));
            Assert.That(Fills(0), Is.EqualTo(new[] { HeartFill.Empty, HeartFill.Empty, HeartFill.Empty, HeartFill.Empty, HeartFill.Empty }));
        }

        /// <summary>
        /// HPが半分のハートに満たなくても、残っていれば半分のハートを見せる。倒れたら全て空になる。
        /// </summary>
        [Test]
        public void GetFill_ShowsHalfHeartWhileAnyHealthRemains()
        {
            Assert.That(HeartGauge.GetFill(0, 1, 20), Is.EqualTo(HeartFill.Half));
            Assert.That(HeartGauge.GetFill(0, 11, 20), Is.EqualTo(HeartFill.Full), "半分を超えたら次の半分まで切り上げます。");
            Assert.That(HeartGauge.CountHalves(75, 20), Is.EqualTo(8));
            Assert.That(HeartGauge.CountHalves(-5, 20), Is.EqualTo(0));
        }

        /// <summary>
        /// ハート1つ分のHPと番号の不正な値を拒む。
        /// </summary>
        [Test]
        public void RejectsInvalidValues()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => HeartGauge.CountHearts(100, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => HeartGauge.CountHearts(-1, 20));
            Assert.Throws<ArgumentOutOfRangeException>(() => HeartGauge.GetFill(-1, 100, 20));
        }

        private static HeartFill[] Fills(int health)
        {
            var fills = new HeartFill[HeartGauge.CountHearts(100, 20)];
            for (var index = 0; index < fills.Length; index++)
            {
                fills[index] = HeartGauge.GetFill(index, health, 20);
            }

            return fills;
        }
    }
}
