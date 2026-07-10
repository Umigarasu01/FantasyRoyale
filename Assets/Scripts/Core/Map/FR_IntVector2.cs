using System;

namespace FantasyRoyale.Core.Map
{
    /// <summary>
    /// Core層でグリッド座標を扱うための整数ベクトル。
    /// UnityEngine.Vector2Intへ依存しないために用意する。
    /// </summary>
    [Serializable]
    public readonly struct FR_IntVector2 : IEquatable<FR_IntVector2>
    {
        public readonly int X;
        public readonly int Y;

        public FR_IntVector2(int x, int y)
        {
            X = x;
            Y = y;
        }

        public bool Equals(FR_IntVector2 other)
        {
            return X == other.X && Y == other.Y;
        }

        public override bool Equals(object obj)
        {
            return obj is FR_IntVector2 other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(X, Y);
        }

        public override string ToString()
        {
            return $"({X}, {Y})";
        }
    }
}
