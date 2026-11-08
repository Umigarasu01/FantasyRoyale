using System;

namespace FantasyRoyale.Gameplay.Characters
{
    /// <summary>
    /// 跳んでから着地するまでの高さ。見た目の跳躍で、足元の位置、当たり判定、通れる場所は変えない。
    /// 地上にいる間だけ跳べ、高さは跳んでいる時間の割合に対する放物線で決まる。
    /// </summary>
    public sealed class CharacterJump
    {
        private float elapsedSeconds;

        /// <summary>
        /// 一番高い所の高さと、跳んでから着地するまでの秒数を受け取る。どちらも有限の正数である。
        /// </summary>
        public CharacterJump(float peakHeight, float durationSeconds)
        {
            Validate(peakHeight, nameof(peakHeight));
            Validate(durationSeconds, nameof(durationSeconds));
            PeakHeight = peakHeight;
            DurationSeconds = durationSeconds;
        }

        public float PeakHeight { get; }
        public float DurationSeconds { get; }
        public bool IsAirborne { get; private set; }

        /// <summary>
        /// 跳んでいる時間の割合。地上では0である。
        /// </summary>
        public float Progress => IsAirborne ? elapsedSeconds / DurationSeconds : 0f;

        /// <summary>
        /// 今の高さ。踏み切りと着地で0、跳んでいる時間の中ほどで一番高い。
        /// </summary>
        public float Height => 4f * PeakHeight * Progress * (1f - Progress);

        /// <summary>
        /// 地上にいれば跳ぶ。跳んでいる間は受け付けない。
        /// </summary>
        public bool TryStart()
        {
            if (IsAirborne)
            {
                return false;
            }

            IsAirborne = true;
            elapsedSeconds = 0f;
            return true;
        }

        /// <summary>
        /// 時間を進め、跳んでいる時間を過ぎれば着地する。
        /// </summary>
        public void Advance(float deltaSeconds)
        {
            if (float.IsNaN(deltaSeconds) || float.IsInfinity(deltaSeconds) || deltaSeconds < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds), "経過秒数は0以上の有限値である必要があります。");
            }

            if (!IsAirborne)
            {
                return;
            }

            elapsedSeconds += deltaSeconds;
            if (elapsedSeconds >= DurationSeconds)
            {
                Land();
            }
        }

        /// <summary>
        /// すぐに着地させる。倒れたときなどに使う。
        /// </summary>
        public void Land()
        {
            IsAirborne = false;
            elapsedSeconds = 0f;
        }

        private static void Validate(float value, string name)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value <= 0f)
            {
                throw new ArgumentOutOfRangeException(name, "跳躍の高さと秒数は有限の正数である必要があります。");
            }
        }
    }
}
