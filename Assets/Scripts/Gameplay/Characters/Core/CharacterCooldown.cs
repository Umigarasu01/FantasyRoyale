using System;

namespace FantasyRoyale.Gameplay.Characters
{
    /// <summary>
    /// 固有の技のように、使った後しばらく使えない物の待ち時間。純C#で、呼び出し側が時間を進める。
    /// </summary>
    public sealed class CharacterCooldown
    {
        public float Duration { get; private set; }
        public float Remaining { get; private set; }
        public bool IsReady => Remaining <= 0f;

        /// <summary>
        /// 残りの割合。使ってすぐは1で、使えるようになると0になる。HUDの覆いに使う。
        /// </summary>
        public float RemainingFraction => Duration <= 0f ? 0f : Remaining / Duration;

        /// <summary>
        /// 待ち時間を始める。0以下なら待たない。
        /// </summary>
        public void Start(float seconds)
        {
            if (float.IsNaN(seconds) || float.IsInfinity(seconds))
            {
                throw new ArgumentOutOfRangeException(nameof(seconds), "待ち時間は有限の値です。");
            }

            Duration = Math.Max(0f, seconds);
            Remaining = Duration;
        }

        public void Advance(float deltaSeconds)
        {
            if (deltaSeconds > 0f && Remaining > 0f)
            {
                Remaining = Math.Max(0f, Remaining - deltaSeconds);
            }
        }
    }
}
