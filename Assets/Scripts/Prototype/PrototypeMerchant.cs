using UnityEngine;

namespace FantasyRoyale.Prototype
{
    /// <summary>
    /// コインを支払ってランダム商品を買える仮商人。
    /// 商人が価値を知らずに伝説級を売る体験を、最小操作で確認する。
    /// </summary>
    public sealed class PrototypeMerchant : MonoBehaviour, IPrototypeInteractable
    {
        [SerializeField] private PrototypeGameController gameController;
        [SerializeField] private int price = 10;

        /// <summary>
        /// 生成時にゲーム管理者と価格を受け取り、購入ログと抽選を委譲する。
        /// </summary>
        public void Initialize(PrototypeGameController controller, int price)
        {
            gameController = controller;
            this.price = Mathf.Max(0, price);
        }

        /// <summary>
        /// プレイヤーが十分なコインを持っていれば商品を抽選し、効果を反映する。
        /// </summary>
        public void Interact(PrototypePlayerController2D player)
        {
            if (gameController == null || player == null)
            {
                return;
            }

            if (!player.TrySpendCoins(price))
            {
                gameController.NotifyMerchantNeedsCoins(price);
                return;
            }

            var item = gameController.RollChestItem();
            player.ApplyItem(item);
            gameController.NotifyMerchantPurchase(item, price);
        }
    }
}
