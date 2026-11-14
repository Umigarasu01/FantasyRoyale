using System;
using System.Collections.Generic;
using UnityEngine;

namespace FantasyRoyale.Gameplay.Hud
{
    /// <summary>品物のレア度。宝箱の光と同じ白、青、紫、金の色で表す。</summary>
    public enum ShopRarity
    {
        Common,
        Rare,
        Epic,
        Legendary
    }

    /// <summary>説明に出す強さの1行。今の物との差を緑の数字で添える。</summary>
    public sealed class ShopStatView
    {
        public ShopStatView(string label, int value, int maximum, int difference)
        {
            Label = label ?? string.Empty;
            Value = value;
            Maximum = Mathf.Max(1, maximum);
            Difference = difference;
        }

        public string Label { get; }
        public int Value { get; }
        /// <summary>棒がいっぱいになる値。</summary>
        public int Maximum { get; }
        /// <summary>今の物との差。0なら出さない。</summary>
        public int Difference { get; }
    }

    /// <summary>商品の1つ。店の画面は渡された値をそのまま出す。</summary>
    public sealed class ShopItemView
    {
        public ShopItemView(
            Sprite icon,
            string name,
            string description,
            int price,
            ShopRarity rarity,
            string status,
            IReadOnlyList<ShopStatView> stats)
        {
            Icon = icon;
            Name = name ?? string.Empty;
            Description = description ?? string.Empty;
            Price = price;
            Rarity = rarity;
            Status = status ?? string.Empty;
            Stats = stats ?? Array.Empty<ShopStatView>();
        }

        public Sprite Icon { get; }
        public string Name { get; }
        public string Description { get; }
        public int Price { get; }
        public ShopRarity Rarity { get; }
        /// <summary>「装備中」「持っている」など。空でないときは、一覧で値段の代わりに出す。</summary>
        public string Status { get; }
        public IReadOnlyList<ShopStatView> Stats { get; }
    }

    /// <summary>自分の持ち物。着けている物の枠と、袋の枠の中身。</summary>
    public sealed class ShopInventoryView
    {
        public ShopInventoryView(
            Sprite head,
            Sprite torso,
            Sprite legs,
            Sprite weapon,
            IReadOnlyList<Sprite> bag,
            Sprite offHand = null,
            IReadOnlyList<Sprite> quick = null,
            bool twoHanded = false,
            IReadOnlyList<Sprite> equipmentPlaceholders = null)
        {
            Head = head;
            Torso = torso;
            Legs = legs;
            Weapon = weapon;
            Bag = bag ?? Array.Empty<Sprite>();
            OffHand = offHand;
            Quick = quick ?? Array.Empty<Sprite>();
            TwoHanded = twoHanded;
            EquipmentPlaceholders = equipmentPlaceholders ?? Array.Empty<Sprite>();
        }

        public Sprite Head { get; }
        public Sprite Torso { get; }
        public Sprite Legs { get; }
        public Sprite Weapon { get; }

        /// <summary>カバンの枠。カバンの数だけ並び、空の枠はnullである。</summary>
        public IReadOnlyList<Sprite> Bag { get; }

        public Sprite OffHand { get; }

        /// <summary>使用枠。空の枠はnullである。</summary>
        public IReadOnlyList<Sprite> Quick { get; }

        /// <summary>両手の武器を持っているか。オフハンドの枠には同じ武器を薄く出す。</summary>
        public bool TwoHanded { get; }

        /// <summary>空の装備の枠の背景に薄く出す影の絵。頭、胴、足、メイン武器、オフハンドの順。</summary>
        public IReadOnlyList<Sprite> EquipmentPlaceholders { get; }
    }

    /// <summary>店の画面に出す全て。</summary>
    public sealed class ShopScreenModel
    {
        public ShopScreenModel(
            Sprite keeperPortrait,
            string keeperName,
            string dialogue,
            IReadOnlyList<ShopItemView> items,
            int selected,
            ShopInventoryView inventory,
            string message,
            bool messageIsWarning = false)
        {
            MessageIsWarning = messageIsWarning;
            KeeperPortrait = keeperPortrait;
            KeeperName = keeperName ?? string.Empty;
            Dialogue = dialogue ?? string.Empty;
            Items = items ?? Array.Empty<ShopItemView>();
            Selected = Items.Count == 0 ? -1 : Mathf.Clamp(selected, 0, Items.Count - 1);
            Inventory = inventory ?? new ShopInventoryView(null, null, null, null, null);
            Message = message ?? string.Empty;
        }

        public Sprite KeeperPortrait { get; }
        public string KeeperName { get; }
        public string Dialogue { get; }
        public IReadOnlyList<ShopItemView> Items { get; }
        /// <summary>選んでいる商品の番号。商品がないときは-1。</summary>
        public int Selected { get; }
        public ShopInventoryView Inventory { get; }
        /// <summary>買った物や、買えなかった理由などの短い知らせ。</summary>
        public string Message { get; }
        /// <summary>知らせが買えなかった理由なら、目立つ色で出す。</summary>
        public bool MessageIsWarning { get; }
    }
}
