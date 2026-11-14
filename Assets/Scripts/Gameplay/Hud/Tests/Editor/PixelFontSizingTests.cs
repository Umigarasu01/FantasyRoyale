using System;
using NUnit.Framework;
using UnityEngine;

namespace FantasyRoyale.Gameplay.Hud.Tests.Editor
{
    /// <summary>
    /// ピクセルフォントを、どの画面の高さでも元の格子の整数倍に描く大きさの規則を確かめる。
    /// </summary>
    public sealed class PixelFontSizingTests
    {
        /// <summary>
        /// 1080p、900p、720p、1440p、2160pで、画面上の大きさが12 pixelの格子の整数倍になる。
        /// </summary>
        [TestCase(1080, 24, 2)]
        [TestCase(900, 29, 2)]
        [TestCase(720, 18, 1)]
        [TestCase(1440, 27, 3)]
        [TestCase(2160, 24, 4)]
        public void GetFontSize_KeepsWholeMultiplesOnScreen(int screenHeight, int fontSize, int screenMultiple)
        {
            var scale = screenHeight / 1080f;
            Assert.That(PixelFontSizing.GetFontSize(12, 2, scale), Is.EqualTo(fontSize));
            Assert.That(PixelFontSizing.GetScreenMultiple(2, scale), Is.EqualTo(screenMultiple));
            Assert.That(Mathf.Abs(fontSize * scale - screenMultiple * 12), Is.LessThan(0.5f));
        }

        /// <summary>
        /// 画面がどれだけ小さくても、元の格子の1倍より小さくしない。
        /// </summary>
        [Test]
        public void GetScreenMultiple_NeverGoesBelowOne()
        {
            Assert.That(PixelFontSizing.GetScreenMultiple(2, 0.1f), Is.EqualTo(1));
            Assert.That(PixelFontSizing.GetFontSize(12, 2, 0.5f), Is.EqualTo(24));
        }

        [Test]
        public void RejectsInvalidValues()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => PixelFontSizing.GetFontSize(0, 2, 1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => PixelFontSizing.GetFontSize(12, 0, 1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => PixelFontSizing.GetFontSize(12, 2, 0f));
        }
    }
}
