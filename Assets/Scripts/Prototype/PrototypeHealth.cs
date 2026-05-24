using System;
using UnityEngine;

namespace FantasyRoyale.Prototype
{
    /// <summary>
    /// プロトタイプ内のプレイヤーと敵が共有する、HPの増減と死亡通知を受け持つ。
    /// </summary>
    public sealed class PrototypeHealth : MonoBehaviour
    {
        [SerializeField] private int maxHitPoints = 10;
        [SerializeField] private int currentHitPoints = 10;

        public event Action<PrototypeHealth> Died;
        public event Action<PrototypeHealth> HitPointsChanged;

        public int MaxHitPoints => maxHitPoints;
        public int CurrentHitPoints => currentHitPoints;
        public bool IsDead => currentHitPoints <= 0;

        private void Awake()
        {
            currentHitPoints = Mathf.Clamp(currentHitPoints, 0, maxHitPoints);
        }

        /// <summary>
        /// 最大HPと現在HPを初期化し、生成直後の個体差を反映する。
        /// </summary>
        public void Initialize(int hitPoints)
        {
            maxHitPoints = Mathf.Max(1, hitPoints);
            currentHitPoints = maxHitPoints;
            HitPointsChanged?.Invoke(this);
        }

        /// <summary>
        /// ダメージを受け取り、HPが0になった瞬間だけ死亡イベントを通知する。
        /// </summary>
        public void TakeDamage(int amount)
        {
            if (IsDead || amount <= 0)
            {
                return;
            }

            currentHitPoints = Mathf.Max(0, currentHitPoints - amount);
            HitPointsChanged?.Invoke(this);

            if (currentHitPoints == 0)
            {
                Died?.Invoke(this);
            }
        }

        /// <summary>
        /// 回復量を受け取り、最大HPを超えない範囲で現在HPを戻す。
        /// </summary>
        public void Heal(int amount)
        {
            if (IsDead || amount <= 0)
            {
                return;
            }

            currentHitPoints = Mathf.Min(maxHitPoints, currentHitPoints + amount);
            HitPointsChanged?.Invoke(this);
        }
    }
}
