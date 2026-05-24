using UnityEngine;
using UnityEngine.UI;

namespace FantasyRoyale.Prototype
{
    /// <summary>
    /// 一人用プロトタイプの状態を最低限確認できるよう、HP、コイン、攻撃力、ログを表示する。
    /// </summary>
    public sealed class PrototypeHud : MonoBehaviour
    {
        [SerializeField] private Text hitPointText;
        [SerializeField] private Text coinText;
        [SerializeField] private Text attackText;
        [SerializeField] private Text latestItemText;
        [SerializeField] private Text messageText;

        /// <summary>
        /// プレイヤーの現在値を受け取り、画面左上の状態表示を更新する。
        /// </summary>
        public void UpdatePlayerStatus(int currentHitPoints, int maxHitPoints, int coins, int attackPower)
        {
            if (hitPointText != null)
            {
                hitPointText.text = $"HP {currentHitPoints}/{maxHitPoints}";
            }

            if (coinText != null)
            {
                coinText.text = $"Coins {coins}";
            }

            if (attackText != null)
            {
                attackText.text = $"Attack {attackPower}";
            }
        }

        /// <summary>
        /// 直近で拾ったアイテムと試合ログを表示し、幸運や事件を見える形にする。
        /// </summary>
        public void ShowMessage(string message, string latestItemName)
        {
            if (messageText != null)
            {
                messageText.text = message;
            }

            if (latestItemText != null)
            {
                latestItemText.text = $"Latest: {latestItemName}";
            }
        }
    }
}
