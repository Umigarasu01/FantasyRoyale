using UnityEngine;

namespace FantasyRoyale.Prototype
{
    /// <summary>
    /// 一人用プロトタイプ全体の初期化、アイテム抽選、HUD更新、ゲームオーバーを受け持つ。
    /// </summary>
    public sealed class PrototypeGameController : MonoBehaviour
    {
        [SerializeField] private PrototypePlayerController2D player;
        [SerializeField] private PrototypeHud hud;
        [SerializeField, Range(0f, 1f)] private float legendaryChance = 0.05f;
        [SerializeField, Range(0f, 1f)] private float rareChance = 0.25f;

        private string latestItemName = "None";
        private string latestMessage = "WASD/Arrow: Move  Space: Attack  E: Open";

        private readonly PrototypeItem[] commonItems =
        {
            new PrototypeItem("薬草", PrototypeItemRarity.Common, 0, 2, 0),
            new PrototypeItem("銅の短剣", PrototypeItemRarity.Common, 1, 0, 0),
            new PrototypeItem("小銭袋", PrototypeItemRarity.Common, 0, 0, 5)
        };

        private readonly PrototypeItem[] rareItems =
        {
            new PrototypeItem("騎士の護符", PrototypeItemRarity.Rare, 1, 4, 0),
            new PrototypeItem("銀の剣", PrototypeItemRarity.Rare, 2, 0, 0),
            new PrototypeItem("商人の秘密袋", PrototypeItemRarity.Rare, 0, 0, 15)
        };

        private readonly PrototypeItem[] legendaryItems =
        {
            new PrototypeItem("伝説の指輪", PrototypeItemRarity.Legendary, 4, 8, 20),
            new PrototypeItem("聖剣の欠片", PrototypeItemRarity.Legendary, 6, 0, 0),
            new PrototypeItem("竜王の心臓", PrototypeItemRarity.Legendary, 3, 12, 10)
        };

        private void Awake()
        {
            if (player == null)
            {
                player = FindAnyObjectByType<PrototypePlayerController2D>();
            }

            if (hud == null)
            {
                hud = FindAnyObjectByType<PrototypeHud>();
            }
        }

        private void OnEnable()
        {
            if (player != null)
            {
                player.Health.Died += OnPlayerDied;
                player.Health.HitPointsChanged += OnPlayerHealthChanged;
            }
        }

        private void OnDisable()
        {
            if (player != null)
            {
                player.Health.Died -= OnPlayerDied;
                player.Health.HitPointsChanged -= OnPlayerHealthChanged;
            }
        }

        private void Start()
        {
            RefreshHud();
            hud?.ShowMessage(latestMessage, latestItemName);
        }

        private void Update()
        {
            RefreshHud();
        }

        /// <summary>
        /// 宝箱用のアイテムを抽選し、低確率で試合が跳ねる伝説級を返す。
        /// </summary>
        public PrototypeItem RollChestItem()
        {
            var roll = Random.value;
            if (roll < legendaryChance)
            {
                return legendaryItems[Random.Range(0, legendaryItems.Length)];
            }

            if (roll < legendaryChance + rareChance)
            {
                return rareItems[Random.Range(0, rareItems.Length)];
            }

            return commonItems[Random.Range(0, commonItems.Length)];
        }

        /// <summary>
        /// 入手アイテムのレアリティを見て、プレイヤーに試合の出来事として伝える。
        /// </summary>
        public void NotifyItemFound(PrototypeItem item)
        {
            latestItemName = item.DisplayName;
            latestMessage = item.Rarity == PrototypeItemRarity.Legendary
                ? $"LEGEND! {item.DisplayName}"
                : $"Found {item.DisplayName}";

            RefreshHud();
            hud?.ShowMessage(latestMessage, latestItemName);
        }

        /// <summary>
        /// 商人から買ったアイテムを、宝箱とは違う出来事としてHUDへ表示する。
        /// </summary>
        public void NotifyMerchantPurchase(PrototypeItem item, int price)
        {
            latestItemName = item.DisplayName;
            latestMessage = item.Rarity == PrototypeItemRarity.Legendary
                ? $"Merchant sold a LEGEND! {item.DisplayName}"
                : $"Bought {item.DisplayName} for {price} Coins";

            RefreshHud();
            hud?.ShowMessage(latestMessage, latestItemName);
        }

        /// <summary>
        /// コイン不足で買えなかったことをHUDへ表示する。
        /// </summary>
        public void NotifyMerchantNeedsCoins(int price)
        {
            latestMessage = $"Need {price} Coins to buy";
            RefreshHud();
            hud?.ShowMessage(latestMessage, latestItemName);
        }

        private void RefreshHud()
        {
            if (player == null || hud == null)
            {
                return;
            }

            hud.UpdatePlayerStatus(player.Health.CurrentHitPoints, player.Health.MaxHitPoints, player.Coins, player.AttackPower);
        }

        private void OnPlayerHealthChanged(PrototypeHealth changedHealth)
        {
            RefreshHud();
        }

        /// <summary>
        /// プレイヤー死亡時に入力を止め、最低限のゲームオーバー表示へ切り替える。
        /// </summary>
        private void OnPlayerDied(PrototypeHealth deadHealth)
        {
            player.SetInputLocked(true);
            latestMessage = "Game Over";
            hud?.ShowMessage(latestMessage, latestItemName);
        }
    }
}
