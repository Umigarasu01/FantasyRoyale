using System;
using UnityEngine;

namespace FantasyRoyale.Gameplay.Hud
{
    /// <summary>
    /// ピクセルフォントの大きさを、画面上で元の格子の整数倍になるように決める規則。
    /// 整数倍でないと、ドットの太さが文字の中で揃わない。HUDのCanvasは画面の高さに合わせて
    /// 拡大縮小するので、その倍率を打ち消すfontSizeを選ぶ。
    /// </summary>
    public static class PixelFontSizing
    {
        /// <summary>
        /// cell pixelの格子で作られたフォントを、1920×1080の画面でmultiple倍に見せたいときのfontSize。
        /// canvasScaleはCanvasの拡大縮小の倍率。画面上の倍率は最も近い整数（最低1倍）に丸める。
        /// </summary>
        public static int GetFontSize(int cell, int multiple, float canvasScale)
        {
            if (cell < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(cell), cell, "格子は1 pixel以上です。");
            }

            if (multiple < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(multiple), multiple, "倍率は1以上です。");
            }

            if (canvasScale <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(canvasScale), canvasScale, "Canvasの倍率は0より大きい値です。");
            }

            return Mathf.RoundToInt(GetScreenMultiple(multiple, canvasScale) * cell / canvasScale);
        }

        /// <summary>画面上で格子の何倍に描くか。</summary>
        public static int GetScreenMultiple(int multiple, float canvasScale)
        {
            return Mathf.Max(1, Mathf.RoundToInt(multiple * canvasScale));
        }
    }
}
