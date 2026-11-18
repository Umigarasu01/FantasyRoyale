using System.Collections.Generic;
using FantasyRoyale.Gameplay.Characters;
using FantasyRoyale.Gameplay.Characters.Unity;
using FantasyRoyale.Gameplay.Hud;
using UnityEngine;
using UnityEngine.InputSystem;

namespace FantasyRoyale.Playtest
{
    /// <summary>
    /// 森の確認用Sceneの持ち物の画面。Tabで開き、同じキーで閉じる。開いている間もゲームは止めず、騎士はWASDで動ける。
    /// 枠はI、J、K、Lか矢印キーかマウスで選び、UかEnterか右クリックで決める。物は掴んで別の枠へ運べる。
    /// 攻撃、技、魔法、調べるは開いている間は使わない。
    /// 画面は`InventoryScreen`が描き、ここは所持品から値を作って、選ぶ、決めるを受ける。
    /// </summary>
    public sealed partial class ForestMapPreviewDebug
    {
        public const string MenuConfirmActionName = "MenuConfirm";

        private static readonly CharacterEquipSlot[] InventoryEquipSlots =
        {
            CharacterEquipSlot.Head, CharacterEquipSlot.Torso, CharacterEquipSlot.Legs, CharacterEquipSlot.MainHand, CharacterEquipSlot.OffHand
        };

        private static readonly string[] EquipSlotNames = { "頭", "胴", "足", "武器", "オフハンド" };

        // 空の装備の枠の背景に薄く出す影の絵。`build_shop_art.py`がアイコンから作る。
        private static readonly string[] EquipSlotPlaceholders = { "slot-head", "slot-torso", "slot-legs", "slot-main-hand", "slot-off-hand" };

        private int inventoryCursor;
        private string inventoryMessage = string.Empty;
        private bool inventoryMessageIsWarning;
        private bool inventoryScreenDirty = true;
        private Vector2 inventoryLastPointer;
        private int inventoryPressedCell = -1;
        private Vector2 inventoryPressedPosition;
        private bool isInventoryDragging;

        public bool IsInventoryOpen { get; private set; }

        /// <summary>持ち物の画面で選んでいる枠の番号。並びは装備5つ、技、魔法の枠、使用枠、カバンの順である。</summary>
        public int InventoryCursor => inventoryCursor;

        public void OpenInventory()
        {
            if (IsShopOpen || IsInventoryOpen)
            {
                return;
            }

            IsInventoryOpen = true;
            inventoryMessage = string.Empty;
            inventoryScreenDirty = true;
            inventoryLastPointer = Mouse.current != null ? Mouse.current.position.ReadValue() : inventoryLastPointer;
        }

        public void CloseInventory()
        {
            IsInventoryOpen = false;
            CancelInventoryDrag();
            if (Hud != null)
            {
                Hud.Inventory.Hide();
            }
        }

        /// <summary>指定の番号の枠を選ぶ。</summary>
        public void SelectInventoryCell(int index)
        {
            inventoryCursor = Mathf.Clamp(index, 0, Mathf.Max(0, CountInventoryCells() - 1));
            inventoryMessage = string.Empty;
            inventoryScreenDirty = true;
        }

        /// <summary>
        /// 選んでいる枠で決める。使用枠の物は使い、カバンの物は着ける、はめる、使用枠へ入れる。装備と魔法はカバンへ外す。固有の技は外せない。
        /// </summary>
        public bool ActivateInventoryCell()
        {
            var (kind, slot) = DescribeCell(inventoryCursor);
            bool done;
            switch (kind)
            {
                case InventoryCellKind.Equipment:
                    done = TryUnequipToBag(InventoryEquipSlots[slot]);
                    break;
                case InventoryCellKind.Magic:
                    done = TryRemoveMagicToBag(slot);
                    break;
                case InventoryCellKind.Quick:
                    done = TryUseQuickSlot(slot);
                    break;
                case InventoryCellKind.Bag:
                    done = TryUseBagItem(slot);
                    break;
                default:
                    LastMessage = "固有の技は外せない";
                    done = false;
                    break;
            }

            inventoryMessage = LastMessage;
            inventoryMessageIsWarning = !done;
            inventoryScreenDirty = true;
            return done;
        }

        /// <summary>
        /// 持ち物の画面での操作。上下左右で隣の枠へ移り、決めるキーかクリックで決める。マウスは動かしたときだけ乗った枠を選ぶ。
        /// </summary>
        private void PollInventoryScreen(HumanInventoryInput input)
        {
            if (input.ToggleInventory)
            {
                CloseInventory();
                return;
            }

            var direction = input.MenuUp ? Vector2.up : input.MenuDown ? Vector2.down : input.MenuLeft ? Vector2.left : input.MenuRight ? Vector2.right : Vector2.zero;
            if (direction != Vector2.zero && Hud != null)
            {
                SelectInventoryCell(Hud.Inventory.Navigate(inventoryCursor, direction));
            }

            if (input.MenuConfirm)
            {
                ActivateInventoryCell();
            }

            var mouse = Mouse.current;
            if (mouse == null || mouseIgnoredForTests)
            {
                return;
            }

            HandleInventoryPointer(
                mouse.position.ReadValue(),
                mouse.leftButton.wasPressedThisFrame,
                mouse.leftButton.isPressed,
                mouse.leftButton.wasReleasedThisFrame,
                mouse.rightButton.wasPressedThisFrame);
        }

        /// <summary>
        /// 持ち物の画面でのマウスの操作。枠の上でマウスを動かすか押すとその枠を選び、右クリックで決める（使う、着ける、外す、はめる）。
        /// 物を押したまま動かすと枠ごと掴み、落とせる枠が光る。光った枠へ落とすとそこへ動かし、埋まっていれば入れ替える。
        /// マウスを動かしていない間は、乗っている枠を選ばない。キーボードで選んだ枠を、止まったマウスが取り返さないためである。
        /// </summary>
        public void HandleInventoryPointer(Vector2 screenPosition, bool pressed, bool held, bool released, bool rightClicked)
        {
            if (!IsInventoryOpen || Hud == null)
            {
                return;
            }

            var moved = screenPosition != inventoryLastPointer;
            inventoryLastPointer = screenPosition;
            var hit = Hud.Inventory.HitTest(screenPosition);
            if (hit >= 0 && (moved || pressed || rightClicked) && hit != inventoryCursor && !isInventoryDragging)
            {
                SelectInventoryCell(hit);
            }

            if (rightClicked && hit >= 0 && !isInventoryDragging)
            {
                ActivateInventoryCell();
            }
            else if (pressed && hit >= 0 && GetCellItem(DescribeCell(hit)) != null)
            {
                inventoryPressedCell = hit;
                inventoryPressedPosition = screenPosition;
            }

            if (held && !isInventoryDragging && inventoryPressedCell >= 0
                && Vector2.Distance(screenPosition, inventoryPressedPosition) >= DragStartDistance)
            {
                isInventoryDragging = true;
                var targets = new List<int>();
                var source = DescribeCell(inventoryPressedCell);
                var count = CountInventoryCells();
                for (var index = 0; index < count; index++)
                {
                    if (index != inventoryPressedCell && CanMoveInventoryItem(source, DescribeCell(index)))
                    {
                        targets.Add(index);
                    }
                }

                Hud.Inventory.BeginDrag(inventoryPressedCell, targets);
            }

            if (isInventoryDragging)
            {
                Hud.Inventory.MoveDrag(screenPosition);
            }

            if (released)
            {
                if (isInventoryDragging && hit >= 0 && hit != inventoryPressedCell)
                {
                    var source = DescribeCell(inventoryPressedCell);
                    var target = DescribeCell(hit);
                    if (CanMoveInventoryItem(source, target))
                    {
                        var done = MoveInventoryItem(source, target);
                        inventoryCursor = hit;
                        inventoryMessage = LastMessage;
                        inventoryMessageIsWarning = !done;
                        inventoryScreenDirty = true;
                    }
                }

                CancelInventoryDrag();
            }
        }

        private void CancelInventoryDrag()
        {
            inventoryPressedCell = -1;
            if (isInventoryDragging && Hud != null)
            {
                Hud.Inventory.EndDrag();
            }

            isInventoryDragging = false;
        }

        /// <summary>枠に入っている物。両手の武器が埋めたオフハンドの枠と、固有の技は掴めないのでnullを返す。</summary>
        private CharacterItem GetCellItem((InventoryCellKind Kind, int Slot) cell)
        {
            switch (cell.Kind)
            {
                case InventoryCellKind.Equipment:
                    var item = Inventory.GetEquipped(InventoryEquipSlots[cell.Slot]);
                    return item != null && InventoryEquipSlots[cell.Slot] == CharacterEquipSlot.OffHand && item == Inventory.MainWeapon ? null : item;
                case InventoryCellKind.Magic:
                    return Inventory.MainWeapon?.GetMagic(cell.Slot);
                case InventoryCellKind.Quick:
                    return Inventory.GetQuickItem(cell.Slot);
                case InventoryCellKind.Bag:
                    return Inventory.GetBagItem(cell.Slot);
                default:
                    return null;
            }
        }

        /// <summary>
        /// 掴んだ物をその枠へ動かせるか。カバンの中と使用枠の中では自由に動き、使用枠には使う物だけが入る。
        /// 装備は埋める装備の枠へ着け、装備の枠からはカバンへ外す（その装備の枠に合う物となら入れ替える）。
        /// 魔法はカバンと武器の魔法の枠の間で動き、魔法同士なら入れ替える。
        /// </summary>
        private bool CanMoveInventoryItem((InventoryCellKind Kind, int Slot) source, (InventoryCellKind Kind, int Slot) target)
        {
            var item = GetCellItem(source);
            if (item == null || (source.Kind == target.Kind && source.Slot == target.Slot))
            {
                return false;
            }

            var other = GetCellItem(target);
            switch (source.Kind)
            {
                case InventoryCellKind.Bag:
                    switch (target.Kind)
                    {
                        case InventoryCellKind.Bag:
                            return true;
                        case InventoryCellKind.Quick:
                            return item.Kind == CharacterItemKind.Consumable;
                        case InventoryCellKind.Equipment:
                            return item.Kind == CharacterItemKind.Equipment && item.Occupies(InventoryEquipSlots[target.Slot]);
                        case InventoryCellKind.Magic:
                            return item.Kind == CharacterItemKind.Magic;
                        default:
                            return false;
                    }

                case InventoryCellKind.Quick:
                    return target.Kind == InventoryCellKind.Quick
                           || (target.Kind == InventoryCellKind.Bag && (other == null || other.Kind == CharacterItemKind.Consumable));
                case InventoryCellKind.Equipment:
                    return target.Kind == InventoryCellKind.Bag
                           && (other == null || (other.Kind == CharacterItemKind.Equipment && other.Occupies(InventoryEquipSlots[source.Slot])));
                case InventoryCellKind.Magic:
                    return target.Kind == InventoryCellKind.Magic
                           || (target.Kind == InventoryCellKind.Bag && (other == null || other.Kind == CharacterItemKind.Magic));
                default:
                    return false;
            }
        }

        /// <summary>掴んだ物をその枠へ動かす。CanMoveInventoryItemで動かせると分かっている物に呼ぶ。</summary>
        private bool MoveInventoryItem((InventoryCellKind Kind, int Slot) source, (InventoryCellKind Kind, int Slot) target)
        {
            var item = GetCellItem(source);
            var name = GetItemName(item);
            var weapon = Inventory.MainWeapon;
            switch (source.Kind)
            {
                case InventoryCellKind.Bag when target.Kind == InventoryCellKind.Equipment:
                    return TryUseBagItem(source.Slot);
                case InventoryCellKind.Bag when target.Kind == InventoryCellKind.Magic:
                    return SwapMagicWithBag(weapon, target.Slot, source.Slot);
                case InventoryCellKind.Equipment:
                {
                    var slot = InventoryEquipSlots[source.Slot];
                    if (GetCellItem(target) != null)
                    {
                        return TryUseBagItem(target.Slot);
                    }

                    var done = Inventory.TryUnequipTo(slot, target.Slot);
                    LastMessage = done ? $"{name}をカバンへ外した" : "外せない";
                    return done;
                }

                case InventoryCellKind.Magic when target.Kind == InventoryCellKind.Magic:
                    weapon.SwapMagic(source.Slot, target.Slot);
                    Inventory.NotifyChanged();
                    LastMessage = $"{name}を動かした";
                    return true;
                case InventoryCellKind.Magic:
                    return SwapMagicWithBag(weapon, source.Slot, target.Slot);
                default:
                {
                    var done = Inventory.TryMoveStored(
                        source.Kind == InventoryCellKind.Quick, source.Slot,
                        target.Kind == InventoryCellKind.Quick, target.Slot);
                    LastMessage = done ? $"{name}を動かした" : "そこへは動かせない";
                    return done;
                }
            }
        }

        /// <summary>武器の魔法の枠とカバンの枠の中身を入れ替える。片方が空なら、もう片方へ動かすだけになる。</summary>
        private bool SwapMagicWithBag(CharacterItem weapon, int magicSlot, int bagSlot)
        {
            if (weapon == null || magicSlot < 0 || magicSlot >= weapon.MagicSlotCount)
            {
                LastMessage = "魔法の枠がない";
                return false;
            }

            var fromBag = Inventory.RemoveFromBag(bagSlot);
            var fromWeapon = weapon.RemoveMagic(magicSlot);
            if (fromBag != null)
            {
                weapon.TryPutMagic(fromBag, magicSlot);
            }

            if (fromWeapon != null)
            {
                Inventory.TryPutInBag(fromWeapon, bagSlot);
            }

            Inventory.NotifyChanged();
            LastMessage = fromBag != null ? $"{GetItemName(fromBag)}を{GetItemName(weapon)}にはめた" : $"{GetItemName(fromWeapon)}をカバンへ外した";
            return true;
        }

        /// <summary>開いている持ち物の画面へ、変わったときだけ値を渡す。</summary>
        private void UpdateInventoryScreen()
        {
            if (Hud == null || !IsInventoryOpen || !inventoryScreenDirty)
            {
                return;
            }

            inventoryScreenDirty = false;
            inventoryCursor = Mathf.Clamp(inventoryCursor, 0, CountInventoryCells() - 1);
            Hud.Inventory.Show(BuildInventoryModel());
        }

        private InventoryScreenModel BuildInventoryModel()
        {
            var weapon = Inventory.MainWeapon;
            var equipmentIcons = new List<Sprite>();
            foreach (var equipSlot in InventoryEquipSlots)
            {
                equipmentIcons.Add(Icon(GetItemIconName(Inventory.GetEquipped(equipSlot))));
            }

            var magics = new List<Sprite>();
            for (var index = 0; weapon != null && index < weapon.MagicSlotCount; index++)
            {
                magics.Add(Icon(GetItemIconName(weapon.GetMagic(index))));
            }

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

            var (kind, slot) = DescribeCell(inventoryCursor);
            var detail = DescribeDetail(kind, slot);
            var twoHanded = weapon != null && Inventory.GetEquipped(CharacterEquipSlot.OffHand) == weapon;
            return new InventoryScreenModel(
                equipmentIcons,
                twoHanded,
                weapon == null ? null : Icon($"icon-skill-{weapon.Id}"),
                magics,
                weapon == null ? -1 : weapon.SelectedMagicIndex,
                quick,
                bag,
                inventoryCursor,
                detail.Icon,
                detail.Name,
                detail.Description,
                detail.Action,
                inventoryMessage,
                inventoryMessageIsWarning,
                EquipmentPlaceholderSprites());
        }

        private List<Sprite> EquipmentPlaceholderSprites()
        {
            var sprites = new List<Sprite>();
            foreach (var name in EquipSlotPlaceholders)
            {
                sprites.Add(Icon(name));
            }

            return sprites;
        }

        private (Sprite Icon, string Name, string Description, string Action) DescribeDetail(InventoryCellKind kind, int slot)
        {
            var weapon = Inventory.MainWeapon;
            switch (kind)
            {
                case InventoryCellKind.Equipment:
                {
                    var item = Inventory.GetEquipped(InventoryEquipSlots[slot]);
                    return item == null
                        ? (null, $"{EquipSlotNames[slot]}に何も着けていない", string.Empty, string.Empty)
                        : (Icon(GetItemIconName(item)), GetItemName(item), GetItemDescription(item), "カバンへ外す");
                }

                case InventoryCellKind.Skill:
                    return weapon == null
                        ? (null, "固有の技", "武器を持っていない。", string.Empty)
                        : (Icon($"icon-skill-{weapon.Id}"), $"{GetItemName(weapon)}の技",
                            $"武器ごとに決まった技。Qで使い、使うと{SkillCooldownSeconds:0}秒待つ。付け替えられない。", string.Empty);
                case InventoryCellKind.Magic:
                {
                    var magic = weapon?.GetMagic(slot);
                    return magic == null
                        ? (null, "空いた魔法の枠", "カバンの魔法を使うと、ここへはまる。", string.Empty)
                        : (Icon(GetItemIconName(magic)), GetItemName(magic), GetItemDescription(magic), "カバンへ外す");
                }

                case InventoryCellKind.Quick:
                {
                    var item = Inventory.GetQuickItem(slot);
                    return item == null
                        ? (null, "空いた使用枠", $"回復薬などの使う物が入る。数字キーの{slot + 1}で使う。", string.Empty)
                        : (Icon(GetItemIconName(item)), GetItemName(item), GetItemDescription(item) + $"数字キーの{slot + 1}で使う。", "使う");
                }

                default:
                {
                    var item = Inventory.GetBagItem(slot);
                    if (item == null)
                    {
                        return (null, "空いたカバンの枠", "外した装備や予備の物をしまう。", string.Empty);
                    }

                    var action = item.Kind == CharacterItemKind.Equipment ? "着ける"
                        : item.Kind == CharacterItemKind.Magic ? "武器にはめる"
                        : item.Kind == CharacterItemKind.Consumable ? "使用枠へ入れる"
                        : string.Empty;
                    return (Icon(GetItemIconName(item)), GetItemName(item), GetItemDescription(item), action);
                }
            }
        }

        /// <summary>持ち物の説明。仮の文で、数値は今の仮の値から作る。</summary>
        public string GetItemDescription(CharacterItem item)
        {
            if (item.Id == PotionItemId)
            {
                return $"使うとHPが{PotionHealing}回復する。";
            }

            var magic = FindMagic(item);
            if (magic != null)
            {
                return $"MP{magic.ManaCost}で唱える魔法。武器の空いた魔法の枠にはめて、Eで唱える。";
            }

            var definition = FindEquipmentDefinition(item.Id);
            if (definition != null)
            {
                if (definition.IsOffHand)
                {
                    return "反対の手に持つ。今は見た目だけで、強さはまだない。";
                }

                var hands = definition.HandUse == CharacterHandUse.TwoHanded ? "両手で持ち、オフハンドも埋める。" : "片手で持つ。";
                return $"{hands}魔法の枠{item.MagicSlotCount}つ。";
            }

            return "今は見た目だけで、強さはまだない。";
        }

        private bool TryUnequipToBag(CharacterEquipSlot slot)
        {
            var item = Inventory.GetEquipped(slot);
            if (item == null)
            {
                LastMessage = "何も着けていない";
                return false;
            }

            var name = GetItemName(item);
            if (!Inventory.TryUnequip(slot))
            {
                LastMessage = "カバンがいっぱい";
                return false;
            }

            LastMessage = $"{name}をカバンへ外した";
            return true;
        }

        private bool TryRemoveMagicToBag(int slot)
        {
            var weapon = Inventory.MainWeapon;
            var magic = weapon?.GetMagic(slot);
            if (magic == null)
            {
                LastMessage = "魔法が入っていない";
                return false;
            }

            if (Inventory.CountFreeBagSlots() == 0)
            {
                LastMessage = "カバンがいっぱい";
                return false;
            }

            weapon.RemoveMagic(slot);
            Inventory.TryAddToBag(magic);
            Inventory.NotifyChanged();
            LastMessage = $"{GetItemName(magic)}をカバンへ外した";
            return true;
        }

        private int CountInventoryCells()
        {
            var weapon = Inventory.MainWeapon;
            return InventoryScreen.EquipmentCount + 1 + (weapon == null ? 0 : weapon.MagicSlotCount) + Inventory.QuickCapacity + Inventory.BagCapacity;
        }

        // 枠の番号を種類と種類の中の番号へ直す。並びは装備5つ、技、魔法の枠、使用枠、カバンの順で、画面の並びと同じである。
        private (InventoryCellKind Kind, int Slot) DescribeCell(int index)
        {
            var magicCount = Inventory.MainWeapon == null ? 0 : Inventory.MainWeapon.MagicSlotCount;
            if (index < InventoryScreen.EquipmentCount)
            {
                return (InventoryCellKind.Equipment, index);
            }

            index -= InventoryScreen.EquipmentCount;
            if (index == 0)
            {
                return (InventoryCellKind.Skill, 0);
            }

            index--;
            if (index < magicCount)
            {
                return (InventoryCellKind.Magic, index);
            }

            index -= magicCount;
            return index < Inventory.QuickCapacity ? (InventoryCellKind.Quick, index) : (InventoryCellKind.Bag, index - Inventory.QuickCapacity);
        }
    }
}
