using System;

namespace FantasyRoyale.Gameplay.Characters
{
    /// <summary>
    /// 人間、CPU、将来のNetwork入力が共通してCharacterへ渡す、一回分の移動意思を表す純C#値。
    /// </summary>
    public readonly struct CharacterMoveCommand : IEquatable<CharacterMoveCommand>
    {
        private const float MaximumMagnitudeSquared = 1f;

        /// <summary>
        /// 水平・垂直入力を受け取り、斜め入力がCardinal移動より速くならない範囲へ正規化する。
        /// </summary>
        public CharacterMoveCommand(float horizontal, float vertical)
        {
            if (float.IsNaN(horizontal)
                || float.IsInfinity(horizontal)
                || float.IsNaN(vertical)
                || float.IsInfinity(vertical))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(horizontal),
                    "移動入力には有限値が必要です。");
            }

            var magnitudeSquared = horizontal * horizontal + vertical * vertical;
            if (magnitudeSquared > MaximumMagnitudeSquared)
            {
                var inverseMagnitude = 1f / (float)Math.Sqrt(magnitudeSquared);
                horizontal *= inverseMagnitude;
                vertical *= inverseMagnitude;
            }

            Horizontal = horizontal;
            Vertical = vertical;
        }

        public static CharacterMoveCommand None => default;
        public float Horizontal { get; }
        public float Vertical { get; }
        public float MagnitudeSquared => Horizontal * Horizontal + Vertical * Vertical;
        public bool IsMoving => MagnitudeSquared > 0f;

        /// <summary>
        /// Command値が完全一致するかを比較し、入力元に依存しないTestや状態確認へ使う。
        /// </summary>
        public bool Equals(CharacterMoveCommand other)
        {
            return Horizontal.Equals(other.Horizontal)
                   && Vertical.Equals(other.Vertical);
        }

        public override bool Equals(object obj)
        {
            return obj is CharacterMoveCommand other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (Horizontal.GetHashCode() * 397) ^ Vertical.GetHashCode();
            }
        }

        public static bool operator ==(CharacterMoveCommand left, CharacterMoveCommand right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(CharacterMoveCommand left, CharacterMoveCommand right)
        {
            return !left.Equals(right);
        }
    }

    /// <summary>
    /// 入力元を知らずに移動Commandを受け取るためのCharacter側の最小契約。
    /// </summary>
    public interface ICharacterMoveCommandTarget
    {
        /// <summary>
        /// 次の物理更新で使用する最新の移動Commandを置き換える。
        /// </summary>
        void SetMoveCommand(CharacterMoveCommand command);
    }
}
