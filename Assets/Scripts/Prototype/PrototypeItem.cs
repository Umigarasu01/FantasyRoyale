using System;
using UnityEngine;

namespace FantasyRoyale.Prototype
{
    /// <summary>
    /// アイテムが試合内でどれだけ事件性を持つかをUI表示と抽選重みに使う。
    /// </summary>
    public enum PrototypeItemRarity
    {
        Common,
        Rare,
        Legendary
    }

    /// <summary>
    /// 宝箱から得られる仮アイテムの効果と表示名をまとめる。
    /// </summary>
    [Serializable]
    public sealed class PrototypeItem
    {
        [SerializeField] private string displayName = "小さな薬草";
        [SerializeField] private PrototypeItemRarity rarity = PrototypeItemRarity.Common;
        [SerializeField] private int attackBonus;
        [SerializeField] private int healAmount;
        [SerializeField] private int coinBonus;

        public string DisplayName => displayName;
        public PrototypeItemRarity Rarity => rarity;
        public int AttackBonus => attackBonus;
        public int HealAmount => healAmount;
        public int CoinBonus => coinBonus;

        public PrototypeItem(string displayName, PrototypeItemRarity rarity, int attackBonus, int healAmount, int coinBonus)
        {
            this.displayName = displayName;
            this.rarity = rarity;
            this.attackBonus = attackBonus;
            this.healAmount = healAmount;
            this.coinBonus = coinBonus;
        }
    }
}
