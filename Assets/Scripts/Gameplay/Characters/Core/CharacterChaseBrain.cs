using System;

namespace FantasyRoyale.Gameplay.Characters
{
    /// <summary>
    /// 敵が相手を追うか、持ち場へ戻るかを決める純C#の規則。
    /// 近づいた相手を追い、持ち場から離れ過ぎた相手は諦める。
    /// </summary>
    public sealed class CharacterChaseBrain
    {
        private const float ArrivalDistance = 0.3f;

        private readonly float homeX;
        private readonly float homeY;
        private readonly float noticeRange;
        private readonly float leashRange;
        private readonly float stopRange;

        /// <summary>
        /// 持ち場の位置、相手に気付く距離、持ち場から追える距離、相手の手前で止まる距離を受け取る。
        /// </summary>
        public CharacterChaseBrain(float homeX, float homeY, float noticeRange, float leashRange, float stopRange)
        {
            if (noticeRange <= 0f || leashRange < noticeRange)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(leashRange),
                    "追える距離は気付く距離以上の正数である必要があります。");
            }

            this.homeX = homeX;
            this.homeY = homeY;
            this.noticeRange = noticeRange;
            this.leashRange = leashRange;
            this.stopRange = stopRange;
        }

        public bool IsChasing { get; private set; }

        /// <summary>
        /// 現在の位置から、次に動く方向のCommandを返す。
        /// </summary>
        public CharacterMoveCommand Decide(float selfX, float selfY, float targetX, float targetY, bool targetIsAlive)
        {
            var targetFromHome = Distance(homeX, homeY, targetX, targetY);
            if (!targetIsAlive || targetFromHome > leashRange)
            {
                IsChasing = false;
            }
            else if (Distance(selfX, selfY, targetX, targetY) <= noticeRange)
            {
                IsChasing = true;
            }

            if (IsChasing)
            {
                // 止まらないと、触れた相手を体で押し続けてしまう。
                return Distance(selfX, selfY, targetX, targetY) <= stopRange
                    ? CharacterMoveCommand.None
                    : Toward(selfX, selfY, targetX, targetY);
            }

            return Distance(selfX, selfY, homeX, homeY) > ArrivalDistance
                ? Toward(selfX, selfY, homeX, homeY)
                : CharacterMoveCommand.None;
        }

        private static CharacterMoveCommand Toward(float fromX, float fromY, float toX, float toY)
        {
            var distance = Distance(fromX, fromY, toX, toY);
            return distance <= 0f
                ? CharacterMoveCommand.None
                : new CharacterMoveCommand((toX - fromX) / distance, (toY - fromY) / distance);
        }

        private static float Distance(float ax, float ay, float bx, float by)
        {
            var x = bx - ax;
            var y = by - ay;
            return (float)Math.Sqrt(x * x + y * y);
        }
    }
}
