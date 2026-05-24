using UnityEngine;

namespace FantasyRoyale.Prototype
{
    /// <summary>
    /// Eキーで開ける宝箱。低確率の伝説級アイテムを含む抽選を行う。
    /// </summary>
    public sealed class PrototypeChest : MonoBehaviour, IPrototypeInteractable
    {
        [SerializeField] private PrototypeGameController gameController;
        [SerializeField] private Sprite openedSprite;
        [SerializeField] private bool opened;

        /// <summary>
        /// 生成時にゲーム管理者を受け取り、開封結果のログ表示を委譲する。
        /// </summary>
        public void Initialize(PrototypeGameController controller)
        {
            gameController = controller;
        }

        /// <summary>
        /// 開封後の見た目をScene生成ツールから設定する。
        /// </summary>
        public void ConfigureVisual(Sprite openedSprite)
        {
            this.openedSprite = openedSprite;
        }

        /// <summary>
        /// 未開封ならアイテム抽選を行い、プレイヤーへ効果を反映して箱を暗くする。
        /// </summary>
        public void Interact(PrototypePlayerController2D player)
        {
            if (opened || gameController == null || player == null)
            {
                return;
            }

            opened = true;
            var item = gameController.RollChestItem();
            player.ApplyItem(item);
            gameController.NotifyItemFound(item);

            if (TryGetComponent<SpriteRenderer>(out var renderer))
            {
                if (openedSprite != null)
                {
                    renderer.sprite = openedSprite;
                    renderer.color = Color.white;
                    return;
                }

                renderer.color = new Color(0.35f, 0.25f, 0.15f);
            }
        }
    }
}
