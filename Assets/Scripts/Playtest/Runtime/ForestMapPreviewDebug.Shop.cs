using System.Collections.Generic;
using FantasyRoyale.Gameplay.Characters;
using FantasyRoyale.Gameplay.Characters.Unity;
using FantasyRoyale.Gameplay.Hud;
using UnityEngine;
using UnityEngine.InputSystem;

namespace FantasyRoyale.Playtest
{
    /// <summary>
    /// 森の確認用Sceneの店。店ごとに1つか2つの品物を売る。値段、レア度、店主の名前と台詞は仮置きで、
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

        // 店ごとの品揃え。どの店も1つか2つで、店の種類に合う物を売る。同じ物を2つの店では売らない。
        // 「potion」「weapon:<id>」「magic:<id>」「armour:<組のid>:<部位>」で書く。騎士の装備は初めから着ているので売らない。
        private static readonly Dictionary<string, string[]> Stocks = new Dictionary<string, string[]>
        {
            { "weapon-shop", new[] { "weapon:sword", "weapon:bow" } },
            { "armour-shop", new[] { "armour:leather:Head", "armour:leather:Torso" } },
            { "town-stall", new[] { "armour:sandals:Legs" } },
            { "town-rug", new[] { "armour:iron-boots:Legs" } },
            { "magic-shop", new[] { "magic:fire", "magic:ice" } },
            { "item-shop", new[] { "potion" } },
        };

        // 仮の防具の値段とレア度。部位を問わず組（Set）ごとに決める。
        private static readonly Dictionary<string, (int Price, ShopRarity Rarity)> ArmourPrices = new Dictionary<string, (int, ShopRarity)>
        {
            { "leather", (10, ShopRarity.Common) },
            { "sandals", (6, ShopRarity.Common) },
            { "iron-boots", (15, ShopRarity.Rare) },
            // 騎士の防具は売らないが、売るときの値の元にする。
            { "knight", (20, ShopRarity.Epic) },
        };

        // 盾はどの店も売らないが、売るときの値の元にする。
        private const int ShieldPrice = 8;

        // 店の画面の装備の枠の並び（`ShopHitKind.Equipment`の番号）。
        private static readonly CharacterEquipSlot[] EquipmentDropSlots =
        {
            CharacterEquipSlot.Head, CharacterEquipSlot.Torso, CharacterEquipSlot.Legs, CharacterEquipSlot.MainHand, CharacterEquipSlot.OffHand
        };

        // マウスを押してからこれだけ動かすと、品物を掴んで運ぶ。
        private const float DragStartDistance = 10f;

        // 同じ所をこの秒数の内に2回押すと、ダブルクリックとする。
        private const float DoubleClickSeconds = 0.35f;

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
        private ShopHit pressedHit = new ShopHit(ShopHitKind.None, -1);
        private Vector2 pressedPosition;
        private bool isDragging;
        private ShopHit lastClickHit = new ShopHit(ShopHitKind.None, -1);
        private float lastClickTime = float.NegativeInfinity;

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
            BuildShopStock(OpenKeeperId);
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
            CancelShopDrag();
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

        /// <summary>カバンの物を売る。どの店でも何でも買い取る。</summary>
        public bool TrySellBagItem(int index)
        {
            return TrySell(index >= 0 && index < Inventory.BagCapacity ? Inventory.GetBagItem(index) : null, () => Inventory.RemoveFromBag(index));
        }

        /// <summary>使用枠の物を売る。</summary>
        public bool TrySellQuickItem(int index)
        {
            return TrySell(index >= 0 && index < Inventory.QuickCapacity ? Inventory.GetQuickItem(index) : null, () => Inventory.UseQuickItem(index));
        }

        /// <summary>
        /// 売るときの値。仮に買う値の半分（1以上）とする。値の分からない物は0で、売れない。
        /// </summary>
        public int GetSellPrice(CharacterItem item)
        {
            var price = GetBasePrice(item);
            return price <= 0 ? 0 : Mathf.Max(1, price / 2);
        }

        private bool TrySell(CharacterItem item, System.Action remove)
        {
            var sellPrice = GetSellPrice(item);
            if (item == null)
            {
                LastMessage = string.Empty;
                return false;
            }

            if (!IsShopOpen || sellPrice <= 0)
            {
                LastMessage = "これは売れない";
                shopMessage = LastMessage;
                shopMessageIsWarning = true;
                shopDirty = true;
                return false;
            }

            remove();
            Coins += sellPrice;
            LastMessage = $"{GetItemName(item)}を{sellPrice}コインで売った";
            shopMessage = LastMessage;
            shopMessageIsWarning = false;
            shopDirty = true;
            return true;
        }

        private int GetBasePrice(CharacterItem item)
        {
            if (item == null)
            {
                return 0;
            }

            if (item.Id == PotionItemId)
            {
                return PotionPrice;
            }

            if (item.Kind == CharacterItemKind.Magic)
            {
                return FindMagic(item) == null ? 0 : MagicPrice;
            }

            if (item.Kind != CharacterItemKind.Equipment)
            {
                return 0;
            }

            var weapon = FindEquipmentDefinition(item.Id);
            if (weapon != null)
            {
                var isOffHand = weapon == offHandEquipment || (catalog != null && catalog.FindOffHand(item.Id) != null);
                return isOffHand ? ShieldPrice : weapon.WeaponKind == CharacterActionMotion.Shoot ? BowPrice : SwordPrice;
            }

            return ArmourPrices.TryGetValue(ArmourPartId(item.Id), out var armour) ? armour.Price : 0;
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

        private string OpenKeeperId => openShopIndex >= 0 && openShopIndex < shopKeeperIds.Length ? shopKeeperIds[openShopIndex] : string.Empty;

        /// <summary>
        /// 店主の店の品揃えを`Stocks`から作る。定義や絵が見つからない品物は並べない。
        /// </summary>
        private void BuildShopStock(string keeperId)
        {
            shopStock.Clear();
            if (!Stocks.TryGetValue(keeperId, out var keys))
            {
                return;
            }

            foreach (var key in keys)
            {
                var parts = key.Split(':');
                switch (parts[0])
                {
                    case "potion":
                        shopStock.Add(new ShopEntry(ShopEntryKind.Potion, "回復薬", $"空いた使用枠に入り、なければカバンに入る。使うとHPが{PotionHealing}回復する。", PotionPrice, ShopRarity.Common, "icon-potion"));
                        break;
                    case "weapon":
                        AddWeaponStock(parts[1]);
                        break;
                    case "magic":
                        AddMagicStock(parts[1]);
                        break;
                    case "armour":
                        AddArmourStock(parts[1], (CharacterArmourSlot)System.Enum.Parse(typeof(CharacterArmourSlot), parts[2]));
                        break;
                }
            }
        }

        private void AddWeaponStock(string id)
        {
            var index = System.Array.FindIndex(equipment, weapon => weapon != null && weapon.Id == id);
            if (index < 0)
            {
                return;
            }

            var found = equipment[index];
            var isBow = found.WeaponKind == CharacterActionMotion.Shoot;
            shopStock.Add(new ShopEntry(
                ShopEntryKind.Weapon,
                found.DisplayName,
                (isBow ? "遠くの敵を射る両手の弓。矢筒を背負う。" : "片手剣。三段まで続けて振れる。") + $"魔法の枠{found.MagicSlotCount}つ。",
                isBow ? BowPrice : SwordPrice,
                isBow ? ShopRarity.Rare : ShopRarity.Common,
                $"icon-weapon-{found.Id}") { WeaponIndex = index });
        }

        private void AddMagicStock(string id)
        {
            var index = System.Array.FindIndex(magicDefinitions, magic => magic != null && magic.Id == id);
            if (index < 0)
            {
                return;
            }

            var found = magicDefinitions[index];
            shopStock.Add(new ShopEntry(
                ShopEntryKind.Magic,
                found.DisplayName,
                $"カバンに入る。使うとメイン武器の空いた魔法の枠へはまる。MP{found.ManaCost}で唱える。",
                MagicPrice,
                ShopRarity.Rare,
                $"icon-magic-{found.Id}") { MagicIndex = index });
        }

        private void AddArmourStock(string id, CharacterArmourSlot slot)
        {
            var part = catalog == null ? null : catalog.FindArmour(id);
            if (part == null || !part.Covers(slot) || !ArmourPrices.TryGetValue(id, out var price))
            {
                return;
            }

            shopStock.Add(new ShopEntry(
                ShopEntryKind.Armour,
                $"{part.DisplayName}（{SlotName(slot)}）",
                $"{part.DisplayName}の{SlotName(slot)}。今は見た目だけで、強さはまだない。",
                price.Price,
                price.Rarity,
                ArmourIconName(part, slot)) { Armour = part, Slot = slot });
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
        /// 左ボタンを押した瞬間だけを渡す、短い形。Testと、掴んで運ばない操作に使う。
        /// </summary>
        public void HandleShopPointer(Vector2 screenPosition, bool clicked, float scroll)
        {
            HandleShopPointer(screenPosition, clicked, clicked, clicked, false, scroll);
        }

        /// <summary>
        /// 店の画面でのマウスの操作。行の上でマウスを動かすか押すとその品物を選び、買うキーか言葉を押すと買い、
        /// 閉じるキーか言葉を押すと閉じる。ホイールで選ぶ品物を上下に送る。乗っているキーは少し大きくなり、言葉が金になる。
        /// 品物の行を持ち物の枠へ運ぶと買い、カバンか使用枠の物を店の枠へ運ぶと売る。右クリックかダブルクリックでも同じく売り買いする。
        /// マウスを動かしていない間は、乗っている行を選ばない。キーボードで選んだ品物を、止まったマウスが取り返さないためである。
        /// </summary>
        public void HandleShopPointer(Vector2 screenPosition, bool pressed, bool held, bool released, bool rightClicked, float scroll)
        {
            if (!IsShopOpen || Hud == null)
            {
                return;
            }

            var hit = Hud.Shop.HitTest(screenPosition);
            var moved = screenPosition != lastPointer;
            lastPointer = screenPosition;
            if (hit.Kind == ShopHitKind.Item && (moved || pressed || rightClicked) && hit.ItemIndex != shopSelected && !isDragging)
            {
                shopSelected = hit.ItemIndex;
                shopMessage = string.Empty;
                shopDirty = true;
            }

            Hud.Shop.SetHover(hit);
            if (rightClicked)
            {
                TradeAt(hit);
            }
            else if (pressed && hit.Kind == ShopHitKind.Buy)
            {
                TryBuySelected();
            }
            else if (pressed && hit.Kind == ShopHitKind.Close)
            {
                CloseShop();
                return;
            }
            else if (pressed && IsTradeable(hit))
            {
                var isDoubleClick = SameHit(hit, lastClickHit) && Time.unscaledTime - lastClickTime <= DoubleClickSeconds;
                lastClickHit = isDoubleClick ? new ShopHit(ShopHitKind.None, -1) : hit;
                lastClickTime = Time.unscaledTime;
                if (isDoubleClick)
                {
                    TradeAt(hit);
                }
                else
                {
                    pressedHit = hit;
                    pressedPosition = screenPosition;
                }
            }
            else if (scroll != 0f && !isDragging)
            {
                SelectShopItem(scroll > 0f ? -1 : 1);
            }

            if (held && !isDragging && pressedHit.Kind != ShopHitKind.None
                && Vector2.Distance(screenPosition, pressedPosition) >= DragStartDistance)
            {
                isDragging = true;
                // 運んだ押しはクリックに数えず、続けて押してもダブルクリックにしない。
                lastClickHit = new ShopHit(ShopHitKind.None, -1);
                Hud.Shop.BeginDrag(pressedHit, GetDropTargets(pressedHit));
            }

            if (isDragging)
            {
                Hud.Shop.MoveDrag(screenPosition);
            }

            if (released)
            {
                if (isDragging)
                {
                    DropAt(pressedHit, hit);
                }

                CancelShopDrag();
            }
        }

        /// <summary>品物の行なら買い、カバンか使用枠の物なら売る。</summary>
        private void TradeAt(ShopHit hit)
        {
            switch (hit.Kind)
            {
                case ShopHitKind.Item:
                    shopSelected = hit.ItemIndex;
                    TryBuySelected();
                    break;
                case ShopHitKind.Bag:
                    TrySellBagItem(hit.ItemIndex);
                    break;
                case ShopHitKind.Quick:
                    TrySellQuickItem(hit.ItemIndex);
                    break;
            }
        }

        /// <summary>
        /// 品物の行を落とせる枠へ落とすと、その枠へ直に買う。持ち物を店の側へ落とすと売る。落とせない所では何もしない。
        /// </summary>
        private void DropAt(ShopHit source, ShopHit target)
        {
            if (source.Kind == ShopHitKind.Item)
            {
                if (source.ItemIndex >= 0 && source.ItemIndex < shopStock.Count && CanBuyInto(shopStock[source.ItemIndex], target))
                {
                    shopSelected = source.ItemIndex;
                    var bought = TryBuyInto(shopStock[source.ItemIndex], target);
                    shopMessage = LastMessage;
                    shopMessageIsWarning = !bought;
                    shopDirty = true;
                }

                return;
            }

            var onShop = target.Kind == ShopHitKind.ShopArea || target.Kind == ShopHitKind.Item || target.Kind == ShopHitKind.Buy || target.Kind == ShopHitKind.Close;
            if (onShop)
            {
                TradeAt(source);
            }
        }

        /// <summary>
        /// 運んでいる物を落とせる枠。品物なら入る持ち物の枠、持ち物なら売れるときの店の枠である。
        /// </summary>
        private List<ShopHit> GetDropTargets(ShopHit source)
        {
            var targets = new List<ShopHit>();
            if (source.Kind == ShopHitKind.Item)
            {
                if (source.ItemIndex < 0 || source.ItemIndex >= shopStock.Count)
                {
                    return targets;
                }

                var entry = shopStock[source.ItemIndex];
                for (var index = 0; index < EquipmentDropSlots.Length; index++)
                {
                    AddIfValid(targets, entry, new ShopHit(ShopHitKind.Equipment, index));
                }

                for (var index = 0; index < Inventory.QuickCapacity; index++)
                {
                    AddIfValid(targets, entry, new ShopHit(ShopHitKind.Quick, index));
                }

                for (var index = 0; index < Inventory.BagCapacity; index++)
                {
                    AddIfValid(targets, entry, new ShopHit(ShopHitKind.Bag, index));
                }
            }
            else
            {
                var item = source.Kind == ShopHitKind.Bag ? Inventory.GetBagItem(source.ItemIndex)
                    : source.Kind == ShopHitKind.Quick ? Inventory.GetQuickItem(source.ItemIndex)
                    : null;
                if (GetSellPrice(item) > 0)
                {
                    targets.Add(new ShopHit(ShopHitKind.ShopArea, -1));
                }
            }

            return targets;
        }

        private void AddIfValid(List<ShopHit> targets, ShopEntry entry, ShopHit target)
        {
            if (CanBuyInto(entry, target))
            {
                targets.Add(target);
            }
        }

        /// <summary>
        /// 品物をその枠へ直に買えるか。使う物は空いた使用枠とカバンへ、装備は合う装備の枠と空いたカバンへ、
        /// 魔法は空いたカバンと、空いた魔法の枠のあるメイン武器へ入る。着けている装備はどこへも買えない。
        /// カバンに持っている装備は、装備の枠へ落とすと代金なしで着け替える。
        /// </summary>
        private bool CanBuyInto(ShopEntry entry, ShopHit target)
        {
            var emptyBag = target.Kind == ShopHitKind.Bag && target.ItemIndex >= 0 && target.ItemIndex < Inventory.BagCapacity
                && Inventory.GetBagItem(target.ItemIndex) == null;
            switch (entry.Kind)
            {
                case ShopEntryKind.Potion:
                    return emptyBag || (target.Kind == ShopHitKind.Quick && target.ItemIndex >= 0 && target.ItemIndex < Inventory.QuickCapacity
                                        && Inventory.GetQuickItem(target.ItemIndex) == null);
                case ShopEntryKind.Magic:
                    return emptyBag || (target.Kind == ShopHitKind.Equipment && EquipmentDropSlot(target) == CharacterEquipSlot.MainHand && HasFreeMagicSlot(Inventory.MainWeapon));
                default:
                    var itemId = EntryItemId(entry);
                    if (IsWearing(itemId))
                    {
                        return false;
                    }

                    if (emptyBag)
                    {
                        return Inventory.FindInBag(itemId) < 0;
                    }

                    var slot = EquipmentDropSlot(target);
                    return slot.HasValue && EntrySlots(entry).Contains(slot.Value);
            }
        }

        /// <summary>品物をその枠へ買う。CanBuyIntoで落とせる枠と分かっている物に呼ぶ。</summary>
        private bool TryBuyInto(ShopEntry entry, ShopHit target)
        {
            if (target.Kind == ShopHitKind.Equipment)
            {
                if (entry.Kind == ShopEntryKind.Magic)
                {
                    return TryBuyMagicIntoWeapon(entry);
                }

                return entry.Kind == ShopEntryKind.Weapon ? TryBuyWeapon(entry) : TryBuyArmour(entry);
            }

            if (Coins < entry.Price)
            {
                LastMessage = "コインが足りない";
                return false;
            }

            var item = CreateEntryItem(entry);
            var placed = target.Kind == ShopHitKind.Quick
                ? Inventory.TryPutInQuick(item, target.ItemIndex)
                : Inventory.TryPutInBag(item, target.ItemIndex);
            if (!placed)
            {
                LastMessage = "その枠には入らない";
                return false;
            }

            Coins -= entry.Price;
            LastMessage = target.Kind == ShopHitKind.Quick ? $"{entry.Name}を買った" : $"{entry.Name}を買ってカバンに入れた";
            return true;
        }

        private bool TryBuyMagicIntoWeapon(ShopEntry entry)
        {
            var weapon = Inventory.MainWeapon;
            if (Coins < entry.Price)
            {
                LastMessage = "コインが足りない";
                return false;
            }

            if (weapon == null || weapon.TryInsertMagic(CreateEntryItem(entry)) < 0)
            {
                LastMessage = "武器に空いた魔法の枠がない";
                return false;
            }

            Coins -= entry.Price;
            Inventory.NotifyChanged();
            LastMessage = $"{entry.Name}を{GetItemName(weapon)}にはめた";
            return true;
        }

        private static bool HasFreeMagicSlot(CharacterItem weapon)
        {
            if (weapon == null)
            {
                return false;
            }

            for (var index = 0; index < weapon.MagicSlotCount; index++)
            {
                if (weapon.GetMagic(index) == null)
                {
                    return true;
                }
            }

            return false;
        }

        private CharacterItem CreateEntryItem(ShopEntry entry)
        {
            switch (entry.Kind)
            {
                case ShopEntryKind.Potion:
                    return new CharacterItem(PotionItemId, CharacterItemKind.Consumable);
                case ShopEntryKind.Weapon:
                    return equipment[entry.WeaponIndex].CreateItem();
                case ShopEntryKind.Magic:
                    return magicDefinitions[entry.MagicIndex].CreateItem();
                default:
                    return CreateArmourItem(entry.Armour, entry.Slot);
            }
        }

        private string EntryItemId(ShopEntry entry)
        {
            return entry.Kind == ShopEntryKind.Weapon ? equipment[entry.WeaponIndex].Id : ArmourKey(entry.Armour, entry.Slot);
        }

        /// <summary>装備の品物が埋める装備の枠。弓のような両手の武器は、メイン武器とオフハンドの両方である。</summary>
        private List<CharacterEquipSlot> EntrySlots(ShopEntry entry)
        {
            if (entry.Kind == ShopEntryKind.Armour)
            {
                return new List<CharacterEquipSlot> { ToEquipSlot(entry.Slot) };
            }

            return new List<CharacterEquipSlot>(equipment[entry.WeaponIndex].CreateItem().Slots);
        }

        private static CharacterEquipSlot? EquipmentDropSlot(ShopHit target)
        {
            return target.Kind == ShopHitKind.Equipment && target.ItemIndex >= 0 && target.ItemIndex < EquipmentDropSlots.Length
                ? EquipmentDropSlots[target.ItemIndex]
                : (CharacterEquipSlot?)null;
        }

        private bool IsTradeable(ShopHit hit)
        {
            switch (hit.Kind)
            {
                case ShopHitKind.Item:
                    return true;
                case ShopHitKind.Bag:
                    return Inventory.GetBagItem(hit.ItemIndex) != null;
                case ShopHitKind.Quick:
                    return Inventory.GetQuickItem(hit.ItemIndex) != null;
                default:
                    return false;
            }
        }

        private static bool SameHit(ShopHit a, ShopHit b)
        {
            return a.Kind == b.Kind && a.ItemIndex == b.ItemIndex;
        }

        private void CancelShopDrag()
        {
            pressedHit = new ShopHit(ShopHitKind.None, -1);
            if (isDragging && Hud != null)
            {
                Hud.Shop.EndDrag();
            }

            isDragging = false;
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
                HandleShopPointer(
                    mouse.position.ReadValue(),
                    mouse.leftButton.wasPressedThisFrame,
                    mouse.leftButton.isPressed,
                    mouse.leftButton.wasReleasedThisFrame,
                    mouse.rightButton.wasPressedThisFrame,
                    mouse.scroll.ReadValue().y);
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
            var keeperId = OpenKeeperId;
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
            var quickPrices = new List<int>();
            for (var index = 0; index < Inventory.QuickCapacity; index++)
            {
                quick.Add(Icon(GetItemIconName(Inventory.GetQuickItem(index))));
                quickPrices.Add(GetSellPrice(Inventory.GetQuickItem(index)));
            }

            var bag = new List<Sprite>();
            var bagPrices = new List<int>();
            for (var index = 0; index < Inventory.BagCapacity; index++)
            {
                bag.Add(Icon(GetItemIconName(Inventory.GetBagItem(index))));
                bagPrices.Add(GetSellPrice(Inventory.GetBagItem(index)));
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
                EquipmentPlaceholderSprites(),
                bagPrices,
                quickPrices);
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
