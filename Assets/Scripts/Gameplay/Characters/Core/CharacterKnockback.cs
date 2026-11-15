using System;

namespace FantasyRoyale.Gameplay.Characters
{
    /// <summary>
    /// 攻撃を受けて押し戻される動き（ヒットバック）と、それを防ぐアーマーの純C#状態。
    /// 押される距離は攻撃のKnockbackを重さで割り、アーマーを引いた値で、0以下なら押されずActionも止まらない。
    /// 押されている間は移動入力もActionも受け付けない。
    /// </summary>
    public sealed class CharacterKnockback
    {
        /// <summary>
        /// 押し戻しにかける秒数。短く強く押して、だんだん止まる。
        /// </summary>
        public const float DefaultDurationSeconds = 0.18f;

        private readonly float durationSeconds;
        private float distance;
        private float elapsed;

        /// <summary>
        /// アーマー、重さ、押し戻しの秒数を受け取る。アーマーは0以上で、無限大なら決して押されない。
        /// 重さは正の有限値で、1より小さい軽い相手ほど遠くへ飛ぶ。
        /// </summary>
        public CharacterKnockback(float armor = 0f, float weight = 1f, float durationSeconds = DefaultDurationSeconds)
        {
            if (float.IsNaN(durationSeconds) || float.IsInfinity(durationSeconds) || durationSeconds <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(durationSeconds), "押し戻しの秒数は有限の正数である必要があります。");
            }

            this.durationSeconds = durationSeconds;
            SetArmor(armor);
            SetWeight(weight);
        }

        /// <summary>
        /// 攻撃のKnockbackから差し引く値。ボスなどは大きくし、無限大で常に押されない。
        /// </summary>
        public float Armor { get; private set; }

        /// <summary>
        /// 攻撃のKnockbackを割る値。1が標準で、スライムのような軽い相手は1より小さい。
        /// </summary>
        public float Weight { get; private set; } = 1f;

        public bool IsActive => elapsed < durationSeconds && distance > 0f;
        public float DirectionX { get; private set; }
        public float DirectionY { get; private set; }

        /// <summary>
        /// 今の押し戻しで進む全距離。押されていなければ0である。
        /// </summary>
        public float Distance => IsActive ? distance : 0f;

        /// <summary>
        /// アーマーを変える。0以上の値か無限大だけを受け取る。
        /// </summary>
        public void SetArmor(float armor)
        {
            if (float.IsNaN(armor) || armor < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(armor), "アーマーは0以上である必要があります。");
            }

            Armor = armor;
        }

        /// <summary>
        /// 重さを変える。正の有限値だけを受け取る。
        /// </summary>
        public void SetWeight(float weight)
        {
            if (float.IsNaN(weight) || float.IsInfinity(weight) || weight <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(weight), "重さは有限の正数である必要があります。");
            }

            Weight = weight;
        }

        /// <summary>
        /// 攻撃のKnockbackと押す向きを受け取り、重さで割ってアーマーを超えた分だけ押し戻しを始める。
        /// 押し戻し中に当たれば、新しい向きと距離でやり直す。押されたらtrueを返す。
        /// </summary>
        public bool TryStart(float power, float directionX, float directionY)
        {
            if (float.IsNaN(power) || float.IsInfinity(power) || power <= 0f)
            {
                return false;
            }

            var length = Math.Sqrt(directionX * directionX + directionY * directionY);
            if (double.IsNaN(length) || length <= 1e-6d)
            {
                return false;
            }

            var pushed = power / Weight - Armor;
            if (!(pushed > 0f))
            {
                return false;
            }

            distance = pushed;
            elapsed = 0f;
            DirectionX = (float)(directionX / length);
            DirectionY = (float)(directionY / length);
            return true;
        }

        /// <summary>
        /// 時間を進め、この間に進む距離を返す。速さは初めが最も速く、終わりに0になる。
        /// </summary>
        public float Advance(float deltaSeconds)
        {
            if (!IsActive || deltaSeconds <= 0f)
            {
                return 0f;
            }

            var before = Traveled(elapsed);
            elapsed = Math.Min(durationSeconds, elapsed + deltaSeconds);
            return Traveled(elapsed) - before;
        }

        /// <summary>
        /// 押し戻しを途中で止める。倒れたときなどに使う。
        /// </summary>
        public void Cancel()
        {
            elapsed = durationSeconds;
        }

        private float Traveled(float time)
        {
            var remaining = 1f - time / durationSeconds;
            return distance * (1f - remaining * remaining);
        }
    }
}
