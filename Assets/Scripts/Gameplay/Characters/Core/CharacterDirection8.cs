using System;

namespace FantasyRoyale.Gameplay.Characters
{
    /// <summary>
    /// Characterの向きと攻撃方向を表す8方向。値は東から反時計回りの45度刻みとする。
    /// </summary>
    public enum CharacterDirection8
    {
        East = 0,
        NorthEast = 1,
        North = 2,
        NorthWest = 3,
        West = 4,
        SouthWest = 5,
        South = 6,
        SouthEast = 7
    }

    /// <summary>
    /// 入力値と8方向の変換を、人間、CPU、Networkで共通にするための純C#処理。
    /// </summary>
    public static class CharacterDirections
    {
        private const int DirectionCount = 8;
        private const double StepRadians = Math.PI / 4d;

        /// <summary>
        /// 2D入力を最も近い8方向へ丸める。入力がない場合は向きを保てるようFallbackを返す。
        /// </summary>
        public static CharacterDirection8 FromVector(float x, float y, CharacterDirection8 fallback)
        {
            if ((x == 0f && y == 0f)
                || float.IsNaN(x)
                || float.IsNaN(y)
                || float.IsInfinity(x)
                || float.IsInfinity(y))
            {
                return fallback;
            }

            var step = (int)Math.Round(Math.Atan2(y, x) / StepRadians, MidpointRounding.AwayFromZero);
            return (CharacterDirection8)(((step % DirectionCount) + DirectionCount) % DirectionCount);
        }

        /// <summary>
        /// 8方向を単位Vectorの成分へ変換する。
        /// </summary>
        public static void ToUnitVector(CharacterDirection8 direction, out float x, out float y)
        {
            var angle = (int)direction * StepRadians;
            x = (float)Math.Cos(angle);
            y = (float)Math.Sin(angle);
        }
    }
}
