using System.Collections.Generic;
using FantasyRoyale.Gameplay.Characters;
using FantasyRoyale.Gameplay.Characters.Unity;
using FantasyRoyale.Gameplay.Hud;
using UnityEngine;
using UnityEngine.InputSystem;

namespace FantasyRoyale.Playtest
{
    /// <summary>
    /// 森の確認用Sceneの店。品揃え、値段、レア度、店主の名前と台詞は仮置きで、
    /// `docs/fantasy-royale/exec-plans/active/shop-screen.md`の「仮置きの決め」にある。
    /// 画面は`ShopScreen`が描き、ここは渡す値を作って、選ぶ、買う、閉じるを受ける。
    /// </summary>
    public sealed partial class ForestMapPreviewDebug
    {
        private const int SwordPrice = 10;
        private const int MagicPrice = 12;
        private const int WeaponMaxDamage = 40;

        // 仮の店主。順番は`ShopSpots`と同じで、Builderが店の絵の名前を渡す。
        private static readonly Dictionary<string, (string Name, string Line)> Keepers = new Dictionary<string, (string, string)>
        {
            { "weapon-shop", ("武器屋", "いらっしゃい。いい刃がそろってるぜ。") },
            { "armour-shop", ("防具屋", "身を守る物なら任せてくれ。") },
            { "town-stall", ("屋台", "安いよ安いよ！ 見ていきな。") },
            { "town-rug", ("露店", "掘り出し物があるかもしれんぞ。") },
            { "magic-shop", ("魔法屋", "ふしぎな品はいかが？") },
            { "item-shop", ("道具屋", "旅のお供にどうぞ。") },
        };

        // 仮の防具の値段とレア度。部位を問わず組（Set）ごとに決める。
        private static readonly Dictionary<string, (int Price, ShopRarity Rarity)> ArmourPrices = new Dictionary<string, (int, ShopRarity)>
        {
            { "leather", (10, ShopRarity.Common) },
            { "sandals", (6, ShopRarity.Common) },
            { "iron-boots", (15, ShopRarity.Rare) },
            { "knight", (20, ShopRarity.Epic) },
        };

        private static readonly CharacterArmourSlot[] ArmourSlots = { CharacterArmourSlot.Head, CharacterArmourSlot.Torso, CharacterArmourSlot.Legs };

        [SerializeField] private HudIconLibrary shopArt;
        [SerializeField] private string[] shopKeeperIds = new string[0];

        private readonly List<ShopEntry> shopStock = new List<ShopEntry>();
        private Vector2 lastPointer;
        private bool mouseIgnoredForTests;
        private int openShopIndex = -1;
        private int shopSelected;
        private string shopMessage = string.Empty;
        private bool shopMessageIsWarning;
        private bool shopDirty;
        private int shopShownCoins = -1;

        /// <summary>今開いている店の番号。閉じているときは-1。</summary>
        public int OpenShopIndex => IsShopOpen ? openShopIndex : -1;

        /// <summary>店の品物の数。</summary>
        public int ShopItemCount => shopStock.Count;

        /// <summary>選んでいる品物の番号。</summary>
        public int ShopSelectedIndex => shopSelected;

        /// <summary>
        /// Editor Builderから店の仮の絵と、店ごとの店主を設定する。店主の順番は店の置き場所と同じである。
        /// </summary>
        public void ConfigureShops(HudIconLibrary newShopArt, string[] newShopKeeperIds)
        {
            shopArt = newShopArt;
            shopKeeperIds = newShopKeeperIds ?? new string[0];
        }

        /// <summary>
        /// 手の届く店を開く。いちばん近い店の店主で開く。
        /// </summary>
        public bool TryOpenShop()
        {
            var position = (Vector2)PlayerObject.transform.position;
            var nearest = -1;
            var nearestDistance = float.MaxValue;
            for (var index = 0; index < shopSpots.Length; index++)
            {
                var distance = Vector2.Distance(position, shopSpots[index]);
                if (distance <= ShopReach && distance < nearestDistance)
                {
                    nearest = index;
                    nearestDistance = distance;
                }
            }

            if (nearest < 0)
            {
                return false;
            }

            IsShopOpen = true;
            openShopIndex = nearest;
            shopSelected = 0;
            shopMessage = string.Empty;
            // 開いたときのマウスの位置を覚え、動かすまでは乗っている行を選ばない。
            lastPointer = Mouse.current != null ? Mouse.current.position.ReadValue() : lastPointer;
            shopDirty = true;
            return true;
        }

        public void CloseShop()
        {
            IsShopOpen = false;
            openShopIndex = -1;
            if (Hud != null)
            {
                Hud.Shop.Hide();
            }
        }

        /// <summary>選ぶ品物をstepだけ動かす。端では反対の端へ回る。</summary>
        public void SelectShopItem(int step)
        {
            if (shopStock.Count == 0)
            {
                return;
            }

            shopSelected = (shopSelected + step % shopStock.Count + shopStock.Count) % shopStock.Count;
            shopMessage = string.Empty;
            shopDirty = true;
        }

        /// <summary>
        /// 選んでいる品物を買う。装備はすぐ着け、カバンに持っている装備は代金なしで着け替える。回復薬と魔法はカバンへ入る。
        /// </summary>
        public bool TryBuySelected()
        {
            if (!IsShopOpen || shopSelected < 0 || shopSelected >= shopStock.Count)
            {
                return false;
            }

            var entry = shopStock[shopSelected];
            bool bought;
            switch (entry.Kind)
            {
                case ShopEntryKind.Potion:
                    bought = TryBuyPotion();
                    break;
                case ShopEntryKind.Weapon:
                    bought = TryBuyWeapon(entry);
                    break;
                case ShopEntryKind.Magic:
                    bought = TryBuyMagic(entry);
                    break;
                default:
                    bought = TryBuyArmour(entry);
                    break;
            }

            shopMessage = LastMessage;
            shopMessageIsWarning = !bought;
            shopDirty = true;
            return bought;
        }

        private bool TryBuyWeapon(ShopEntry entry)
        {
            var weapon = equipment[entry.WeaponIndex];
            return TryBuyEquipment(weapon.Id, weapon.DisplayName, entry.Price, weapon.CreateItem);
        }

        private bool TryBuyArmour(ShopEntry entry)
        {
            return TryBuyEquipment(ArmourKey(entry.Armour, entry.Slot), entry.Name, entry.Price, () => CreateArmourItem(entry.Armour, entry.Slot));
        }

        /// <summary>
        /// 装備を買ってすぐ着け、外れた物はカバンへ入れる。外れた物が入りきらなければ、買った物をカバンへ入れる。
        /// カバンに持っている物は、代金なしで着け替える。着けている物は買わない。
        /// </summary>
        private bool TryBuyEquipment(string itemId, string name, int price, System.Func<CharacterItem> create)
        {
            if (IsWearing(itemId))
            {
                LastMessage = "もう着けている";
                return false;
            }

            var owned = Inventory.FindInBag(itemId);
            if (owned >= 0)
            {
                var result = Inventory.UseBagItem(owned);
                LastMessage = result.IsApplied ? $"{name}に着け替えた" : "外す物がカバンに入りきらない";
                return result.IsApplied;
            }

            if (Coins < price)
            {
                LastMessage = "コインが足りない";
                return false;
            }

            var item = create();
            if (Inventory.TryEquipNew(item))
            {
                LastMessage = $"{name}を買った";
            }
            else if (Inventory.TryAddToBag(item) >= 0)
            {
                LastMessage = $"{name}を買ってカバンに入れた";
            }
            else
            {
                LastMessage = "カバンがいっぱい";
                return false;
            }

            Coins -= price;
            return true;
        }

        /// <summary>
        /// 魔法を買ってカバンへ入れる。武器へはめるのは、カバンから使ったときである。
        /// </summary>
        private bool TryBuyMagic(ShopEntry entry)
        {
            if (Coins < entry.Price)
            {
                LastMessage = "コインが足りない";
                return false;
            }

            if (Inventory.TryAddToBag(magicDefinitions[entry.MagicIndex].CreateItem()) < 0)
            {
                LastMessage = "カバンがいっぱい";
                return false;
            }

            Coins -= entry.Price;
            LastMessage = $"{entry.Name}をカバンに入れた";
            return true;
        }

        private bool IsWearing(string itemId)
        {
            foreach (CharacterEquipSlot slot in System.Enum.GetValues(typeof(CharacterEquipSlot)))
            {
                var item = Inventory.GetEquipped(slot);
                if (item != null && item.Id == itemId)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>品揃えを作る。今着ている防具は持っている物とする。</summary>
        private void BuildShopStock()
        {
            shopStock.Clear();
            shopStock.Add(new ShopEntry(ShopEntryKind.Potion, "回復薬", $"空いた使用枠に入り、なければカバンに入る。使うとHPが{PotionHealing}回復する。", PotionPrice, ShopRarity.Common, "icon-potion"));
            for (var index = 0; index < equipment.Length; index++)
            {
                var weapon = equipment[index];
                var isBow = weapon.WeaponKind == CharacterActionMotion.Shoot;
                shopStock.Add(new ShopEntry(
                    ShopEntryKind.Weapon,
                    weapon.DisplayName,
                    (isBow ? "遠くの敵を射る両手の弓。矢筒を背負う。" : "片手剣。三段まで続けて振れる。") + $"魔法の枠{weapon.MagicSlotCount}つ。",
                    isBow ? BowPrice : SwordPrice,
                    isBow ? ShopRarity.Rare : ShopRarity.Common,
                    $"icon-weapon-{weapon.Id}") { WeaponIndex = index });
            }

            for (var index = 0; index < magicDefinitions.Length; index++)
            {
                var magic = magicDefinitions[index];
                if (magic == null)
                {
                    continue;
                }

                shopStock.Add(new ShopEntry(
                    ShopEntryKind.Magic,
                    magic.DisplayName,
                    $"カバンに入る。使うとメイン武器の空いた魔法の枠へはまる。MP{magic.ManaCost}で唱える。",
                    MagicPrice,
                    ShopRarity.Rare,
                    $"icon-magic-{magic.Id}") { MagicIndex = index });
            }

            if (catalog == null)
            {
                return;
            }

            foreach (var part in catalog.Armour)
            {
                if (!ArmourPrices.TryGetValue(part.Id, out var price))
                {
                    continue;
                }

                foreach (var slot in ArmourSlots)
                {
                    if (!part.Covers(slot))
                    {
                        continue;
                    }

                    var name = $"{part.DisplayName}（{SlotName(slot)}）";
                    shopStock.Add(new ShopEntry(
                        ShopEntryKind.Armour,
                        name,
                        $"{part.DisplayName}の{SlotName(slot)}。今は見た目だけで、強さはまだない。",
                        price.Price,
                        price.Rarity,
                        ArmourIconName(part, slot)) { Armour = part, Slot = slot });
                }
            }
        }

        /// <summary>
        /// PlayMode Testが本物のマウスに左右されないよう、マウスの位置とボタンを読まなくする。
        /// 読まない間は金のカーソルも動かさず、OSのカーソルを隠さない。
        /// </summary>
        public void SetMouseIgnoredForTests(bool ignored)
        {
            mouseIgnoredForTests = ignored;
        }

        /// <summary>
        /// 店の画面でのマウスの操作。行の上でマウスを動かすか押すとその品物を選び、買うキーか言葉を押すと買い、
        /// 閉じるキーか言葉を押すと閉じる。ホイールで選ぶ品物を上下に送る。乗っているキーは少し大きくなり、言葉が金になる。
        /// マウスを動かしていない間は、乗っている行を選ばない。キーボードで選んだ品物を、止まったマウスが取り返さないためである。
        /// </summary>
        public void HandleShopPointer(Vector2 screenPosition, bool clicked, float scroll)
        {
            if (!IsShopOpen || Hud == null)
            {
                return;
            }

            var hit = Hud.Shop.HitTest(screenPosition);
            var moved = screenPosition != lastPointer;
            lastPointer = screenPosition;
            if (hit.Kind == ShopHitKind.Item && (moved || clicked) && hit.ItemIndex != shopSelected)
            {
                shopSelected = hit.ItemIndex;
                shopMessage = string.Empty;
                shopDirty = true;
            }

            Hud.Shop.SetHover(hit);
            if (clicked && hit.Kind == ShopHitKind.Buy)
            {
                TryBuySelected();
            }
            else if (clicked && hit.Kind == ShopHitKind.Close)
            {
                CloseShop();
            }
            else if (scroll != 0f)
            {
                SelectShopItem(scroll > 0f ? -1 : 1);
            }
        }

        /// <summary>
        /// 金の矢印のカーソルをマウスの位置に出し、OSのカーソルを隠す。全装備とデバッグの確認用の画面はOnGUIで、
        /// HUDより手前に描かれてカーソルを隠すので、開いている間はOSのカーソルに戻す。
        /// </summary>
        private void UpdateCursor()
        {
            if (mouseIgnoredForTests)
            {
                return;
            }

            var mouse = Mouse.current;
            var show = mouse != null && !IsEquipmentPanelOpen && !IsCheatPanelOpen;
            if (show)
            {
                Hud.ShowCursor(mouse.position.ReadValue());
            }
            else
            {
                Hud.HideCursor();
            }

            Cursor.visible = !show;
        }

        /// <summary>
        /// 店にいる間の操作。I、Kか上下の矢印で選び、UかEnterで買い、Fで閉じる。マウスでも選び、買い、閉じられる。
        /// 騎士はWASDで動けて、店の届く所を出ると閉じる。攻撃、技、魔法は使わない（2026-11-13、所有者）。
        /// </summary>
        private void PollShopInput(Gameplay.Characters.Unity.HumanInventoryInput input)
        {
            if (interactAction.WasPressedThisFrame() || !IsNearShop())
            {
                CloseShop();
                return;
            }

            if (input.MenuConfirm)
            {
                TryBuySelected();
            }

            if (input.MenuUp)
            {
                SelectShopItem(-1);
            }
            else if (input.MenuDown)
            {
                SelectShopItem(1);
            }

            var mouse = Mouse.current;
            if (mouse != null && !mouseIgnoredForTests)
            {
                HandleShopPointer(mouse.position.ReadValue(), mouse.leftButton.wasPressedThisFrame, mouse.scroll.ReadValue().y);
            }
        }

        /// <summary>開いている店の画面へ、変わったときだけ値を渡す。</summary>
        private void UpdateShopScreen()
        {
            if (Hud == null || !IsShopOpen || (!shopDirty && shopShownCoins == Coins))
            {
                return;
            }

            shopDirty = false;
            shopShownCoins = Coins;
            Hud.Shop.Show(BuildShopModel());
        }

        private ShopScreenModel BuildShopModel()
        {
            var keeperId = openShopIndex >= 0 && openShopIndex < shopKeeperIds.Length ? shopKeeperIds[openShopIndex] : string.Empty;
            var keeper = Keepers.TryGetValue(keeperId, out var found) ? found : ("お店", "いらっしゃい。");
            var items = new List<ShopItemView>(shopStock.Count);
            foreach (var entry in shopStock)
            {
                items.Add(new ShopItemView(Icon(entry.IconName), entry.Name, entry.Description, entry.Price, entry.Rarity, GetStatus(entry), GetStats(entry)));
            }

            return new ShopScreenModel(Icon($"keeper-{keeperId}"), keeper.Item1, keeper.Item2, items, shopSelected, BuildInventory(), shopMessage, shopMessageIsWarning);
        }

        private string GetStatus(ShopEntry entry)
        {
            switch (entry.Kind)
            {
                case ShopEntryKind.Weapon:
                    return GetOwnedStatus(equipment[entry.WeaponIndex].Id);
                case ShopEntryKind.Armour:
                    return GetOwnedStatus(ArmourKey(entry.Armour, entry.Slot));
                default:
                    return string.Empty;
            }
        }

        private string GetOwnedStatus(string itemId)
        {
            return IsWearing(itemId) ? "装備中" : Inventory.FindInBag(itemId) >= 0 ? "持っている" : string.Empty;
        }

        private IReadOnlyList<ShopStatView> GetStats(ShopEntry entry)
        {
            switch (entry.Kind)
            {
                case ShopEntryKind.Potion:
                    return new[] { new ShopStatView("回復", PotionHealing, PlayerHealthPoints, 0) };
                case ShopEntryKind.Weapon:
                    var damage = GetDamage(equipment[entry.WeaponIndex]);
                    var held = currentEquipment == null ? damage : GetDamage(currentEquipment);
                    return new[] { new ShopStatView("攻撃", damage, WeaponMaxDamage, damage - held) };
                default:
                    return null;
            }
        }

        /// <summary>着けている物、使用枠、カバンを所持品から作る。空の枠はnullの絵にして、枠の数を保つ。</summary>
        private ShopInventoryView BuildInventory()
        {
            var quick = new List<Sprite>();
            for (var index = 0; index < Inventory.QuickCapacity; index++)
            {
                quick.Add(Icon(GetItemIconName(Inventory.GetQuickItem(index))));
            }

            var bag = new List<Sprite>();
            for (var index = 0; index < Inventory.BagCapacity; index++)
            {
                bag.Add(Icon(GetItemIconName(Inventory.GetBagItem(index))));
            }

            var weapon = Inventory.MainWeapon;
            var offHand = Inventory.GetEquipped(CharacterEquipSlot.OffHand);
            return new ShopInventoryView(
                Icon(GetItemIconName(Inventory.GetEquipped(CharacterEquipSlot.Head))),
                Icon(GetItemIconName(Inventory.GetEquipped(CharacterEquipSlot.Torso))),
                Icon(GetItemIconName(Inventory.GetEquipped(CharacterEquipSlot.Legs))),
                Icon(GetItemIconName(weapon)),
                bag,
                offHand == null || offHand == weapon ? null : Icon(GetItemIconName(offHand)),
                quick,
                weapon != null && offHand == weapon,
                EquipmentPlaceholderSprites());
        }

        /// <summary>
        /// 持ち物のアイコンの名前。仮のアイコンは`build_shop_art.py`が作り、ない物は空の枠になる。
        /// </summary>
        public string GetItemIconName(CharacterItem item)
        {
            if (item == null)
            {
                return string.Empty;
            }

            if (item.Id == PotionItemId)
            {
                return "icon-potion";
            }

            switch (item.Kind)
            {
                case CharacterItemKind.Magic:
                    return $"icon-magic-{item.Id}";
                case CharacterItemKind.Equipment:
                    return FindEquipmentDefinition(item.Id) != null
                        ? $"icon-weapon-{item.Id}"
                        : $"icon-{ArmourPartId(item.Id)}-{ArmourSlotOf(item).ToString().ToLowerInvariant()}";
                default:
                    return $"icon-{item.Id}";
            }
        }

        private Sprite Icon(string name)
        {
            return shopArt == null ? null : shopArt.Find(name);
        }

        private static int GetDamage(CharacterEquipmentDefinition weapon)
        {
            return weapon.PrimaryAction == null ? 0 : weapon.PrimaryAction.CreateSpec().Damage;
        }

        private static string ArmourKey(CharacterSpriteSetDefinition part, CharacterArmourSlot slot)
        {
            return $"{part.Id}:{slot}";
        }

        private static string ArmourIconName(CharacterSpriteSetDefinition part, CharacterArmourSlot slot)
        {
            return part == null ? string.Empty : $"icon-{part.Id}-{slot.ToString().ToLowerInvariant()}";
        }

        private static string SlotName(CharacterArmourSlot slot)
        {
            return slot == CharacterArmourSlot.Head ? "頭" : slot == CharacterArmourSlot.Torso ? "胴" : "足";
        }

        private enum ShopEntryKind
        {
            Potion,
            Weapon,
            Magic,
            Armour
        }

        private sealed class ShopEntry
        {
            public ShopEntry(ShopEntryKind kind, string name, string description, int price, ShopRarity rarity, string iconName)
            {
                Kind = kind;
                Name = name;
                Description = description;
                Price = price;
                Rarity = rarity;
                IconName = iconName;
            }

            public ShopEntryKind Kind { get; }
            public string Name { get; }
            public string Description { get; }
            public int Price { get; }
            public ShopRarity Rarity { get; }
            public string IconName { get; }
            public int WeaponIndex { get; set; } = -1;
            public int MagicIndex { get; set; } = -1;
            public CharacterSpriteSetDefinition Armour { get; set; }
            public CharacterArmourSlot Slot { get; set; }
        }
    }
}
