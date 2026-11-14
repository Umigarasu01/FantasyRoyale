using System;

namespace FantasyRoyale.Gameplay.Hud
{
    /// <summary>ハート1つの満ち具合。</summary>
    public enum HeartFill
    {
        Empty,
        Half,
        Full
    }

    /// <summary>
    /// HPをハートの並びに直す規則。ハートは半分ずつ欠ける。
    /// HPが残っていれば最低でも半分のハートを見せるため、半分の単位へ切り上げる。
    /// ハート1つ分のHPはゲームの決めごとなので、呼ぶ側が渡す。
    /// </summary>
    public static class HeartGauge
    {
        /// <summary>最大HPを入れるのに要るハートの数。</summary>
        public static int CountHearts(int maximumHealth, int healthPerHeart)
        {
            Validate(healthPerHeart);
            if (maximumHealth < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maximumHealth), maximumHealth, "最大HPは0以上です。");
            }

            return (maximumHealth + healthPerHeart - 1) / healthPerHeart;
        }

        /// <summary>今のHPで満ちている半分のハートの数。</summary>
        public static int CountHalves(int currentHealth, int healthPerHeart)
        {
            Validate(healthPerHeart);
            if (currentHealth <= 0)
            {
                return 0;
            }

            return (currentHealth * 2 + healthPerHeart - 1) / healthPerHeart;
        }

        /// <summary>左から数えてheartIndex番目のハートの満ち具合。</summary>
        public static HeartFill GetFill(int heartIndex, int currentHealth, int healthPerHeart)
        {
            if (heartIndex < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(heartIndex), heartIndex, "ハートの番号は0以上です。");
            }

            var filled = CountHalves(currentHealth, healthPerHeart) - heartIndex * 2;
            return filled >= 2 ? HeartFill.Full : filled == 1 ? HeartFill.Half : HeartFill.Empty;
        }

        private static void Validate(int healthPerHeart)
        {
            if (healthPerHeart < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(healthPerHeart), healthPerHeart, "ハート1つ分のHPは1以上です。");
            }
        }
    }
}
