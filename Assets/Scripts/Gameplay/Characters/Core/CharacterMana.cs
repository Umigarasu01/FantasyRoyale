using System;

namespace FantasyRoyale.Gameplay.Characters
{
    /// <summary>
    /// 魔法に使うMPの純C#状態。最大値と今の値を整数で持ち、時間で少しずつ戻る。
    /// 戻る速さと量の数値は、触りながら調整する仮の値を外から渡す。
    /// </summary>
    public sealed class CharacterMana
    {
        private float regenerationCarry;

        /// <summary>
        /// 最大MPと初めのMPから作る。初めのMPを省くと満タンで始める。
        /// </summary>
        public CharacterMana(int maximum, int? starting = null)
        {
            if (maximum <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maximum), "最大MPは1以上です。");
            }

            var current = starting ?? maximum;
            if (current < 0 || current > maximum)
            {
                throw new ArgumentOutOfRangeException(nameof(starting), "初めのMPは0から最大までです。");
            }

            Maximum = maximum;
            Current = current;
        }

        public int Maximum { get; }
        public int Current { get; private set; }

        public bool CanSpend(int cost)
        {
            return cost >= 0 && Current >= cost;
        }

        /// <summary>
        /// MPが足りれば使ってtrueを返す。足りなければ何も変えずにfalseを返す。
        /// </summary>
        public bool TrySpend(int cost)
        {
            if (!CanSpend(cost))
            {
                return false;
            }

            Current -= cost;
            return true;
        }

        /// <summary>
        /// MPを戻し、実際に戻った量を返す。最大を超えない。
        /// </summary>
        public int Restore(int amount)
        {
            if (amount <= 0)
            {
                return 0;
            }

            var before = Current;
            Current = Math.Min(Maximum, Current + amount);
            return Current - before;
        }

        /// <summary>
        /// 経った秒数と1秒に戻る量から、整数に満ちた分だけMPを戻す。端数は次へ持ち越す。
        /// 満タンの間は端数をためない。
        /// </summary>
        public int Regenerate(float deltaSeconds, float perSecond)
        {
            if (deltaSeconds <= 0f || perSecond <= 0f || Current >= Maximum)
            {
                regenerationCarry = 0f;
                return 0;
            }

            regenerationCarry += deltaSeconds * perSecond;
            var whole = (int)regenerationCarry;
            if (whole == 0)
            {
                return 0;
            }

            regenerationCarry -= whole;
            return Restore(whole);
        }
    }
}
