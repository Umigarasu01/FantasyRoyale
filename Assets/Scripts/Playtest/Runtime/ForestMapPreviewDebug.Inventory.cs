using System.Collections.Generic;
using FantasyRoyale.Gameplay.Characters;
using FantasyRoyale.Gameplay.Characters.Unity;
using FantasyRoyale.Gameplay.Hud;
using UnityEngine;

namespace FantasyRoyale.Playtest
{
    /// <summary>
    /// 森の確認用Sceneの所持品とMP。状態は本番の`CharacterInventory`と`CharacterMana`が持ち、
    /// ここは持ち物のidを定義Assetへつないで、騎士の見た目とActionへ反映する。
    /// 決まりは`docs/fantasy-royale/product-specs/inventory.md`にあり、数値は触りながら調整する仮の値である。
    /// </summary>
    public sealed partial class ForestMapPreviewDebug
    {
        public const string CastMagicActionName = "CastMagic";
        public const string NextMagicActionName = "NextMagic";
        public const string UseItemActionName = "UseItem";
        public const string NextItemActionName = "NextItem";
        public const string InventoryActionName = "Inventory";
        public const string UseQuickSlotActionName = "UseQuickSlot";
        public const string MenuUpActionName = "MenuUp";
        public const string MenuDownActionName = "MenuDown";
        public const string MenuLeftActionName = "MenuLeft";
        public const string MenuRightActionName = "MenuRight";
        public const string PotionItemId = "potion";

        // 仮の値。使用枠は5（数字キーの1〜5）、カバンは6枠、MPは最大40で1秒に2戻る。
        private const int QuickCapacity = 5;
        private const int BagCapacity = 6;
        private const int ManaMaximum = 40;
        private const float ManaPerSecond = 2f;

        // 固有の技の仮の待ち時間（秒）。触りながら調整する。
        private const float SkillCooldownSeconds = 3f;

        // HUDのMPの玉1つ分。最大40で玉4つになる。
        private const int ManaPerOrb = 10;

        private HumanInventoryInputAdapter inventoryInput;
        private bool loadoutDirty = true;

        public CharacterInventory Inventory { get; private set; }
        public CharacterMana Mana { get; private set; }

        /// <summary>固有の技の待ち時間。使うと始まり、終わるまでKは効かない。</summary>
        public CharacterCooldown SkillCooldown { get; } = new CharacterCooldown();

        /// <summary>
        /// 持っている武器の数。着けているメイン武器とカバンの武器を数える。
        /// </summary>
        public int OwnedEquipmentCount
        {
            get
            {
                if (Inventory == null)
                {
                    return 0;
                }

                var count = Inventory.MainWeapon == null ? 0 : 1;
                for (var index = 0; index < Inventory.BagCapacity; index++)
                {
                    var item = Inventory.GetBagItem(index);
                    if (item != null && item.IsWeapon)
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        /// <summary>
        /// 初めの所持品を作る。剣と盾と今の防具を着け、魔法を試せるよう剣に1つ目の魔法を入れておく。カバンは空で始める。
        /// </summary>
        private void CreateInventory()
        {
            Inventory = new CharacterInventory(BagCapacity, QuickCapacity);
            Mana = new CharacterMana(ManaMaximum);
            if (equipment.Length > 0)
            {
                Inventory.TryEquipNew(equipment[0].CreateItem());
            }

            if (offHandEquipment != null)
            {
                Inventory.TryEquipNew(offHandEquipment.CreateItem());
            }

            foreach (var slot in ArmourSlots)
            {
                var worn = PlayerAnimator.GetArmour(slot);
                if (worn != null)
                {
                    Inventory.TryEquipNew(CreateArmourItem(worn, slot));
                }
            }

            var weapon = Inventory.MainWeapon;
            if (weapon != null && magicDefinitions.Length > 0 && magicDefinitions[0] != null)
            {
                weapon.TryInsertMagic(magicDefinitions[0].CreateItem());
            }

            Inventory.Changed += ApplyInventory;
            ApplyInventory();
        }

        private void CreateInventoryInput()
        {
            inventoryInput = new HumanInventoryInputAdapter(
                FindPlayerAction(NextMagicActionName),
                FindPlayerAction(UseItemActionName),
                FindPlayerAction(NextItemActionName),
                FindPlayerAction(InventoryActionName),
                FindPlayerAction(UseQuickSlotActionName),
                FindPlayerAction(MenuUpActionName),
                FindPlayerAction(MenuDownActionName),
                FindPlayerAction(MenuLeftActionName),
                FindPlayerAction(MenuRightActionName),
                FindPlayerAction(MenuConfirmActionName));
            inventoryInput.Enable();
        }

        /// <summary>
        /// 着けている物を騎士へ反映する。メイン武器が通常攻撃と技を、メイン武器で選んでいる魔法が唱えるActionを決め、
        /// 部品はメイン武器とオフハンドを重ねる。両手の武器はオフハンドの枠も同じ物で埋めるので、二重に重ねない。
        /// </summary>
        private void ApplyInventory()
        {
            var weaponItem = Inventory.MainWeapon;
            var weapon = weaponItem == null ? null : FindEquipmentDefinition(weaponItem.Id);
            var offHandItem = Inventory.GetEquipped(CharacterEquipSlot.OffHand);
            var offHand = offHandItem == null || offHandItem == weaponItem ? null : FindEquipmentDefinition(offHandItem.Id);

            currentEquipment = weapon;
            if (weapon != null)
            {
                weapon.ApplyTo(PlayerCombat.Loadout);
            }
            else
            {
                PlayerCombat.Loadout.Replace(null, null);
            }

            var gear = new List<CharacterGearDefinition>();
            if (weapon != null)
            {
                gear.AddRange(weapon.Gear);
            }

            if (offHand != null)
            {
                gear.AddRange(offHand.Gear);
            }

            PlayerAnimator.SetGear(gear);
            PlayerAnimator.SetWeaponKind(weapon == null ? (CharacterActionMotion?)null : weapon.WeaponKind);

            foreach (var slot in ArmourSlots)
            {
                var item = Inventory.GetEquipped(ToEquipSlot(slot));
                var part = item == null || catalog == null ? null : catalog.FindArmour(ArmourPartId(item.Id));
                if (part != null && PlayerAnimator.GetArmour(slot) != part)
                {
                    PlayerAnimator.SetArmour(slot, part);
                }
            }

            var magic = FindMagic(weaponItem?.SelectedMagic);
            PlayerCombat.Loadout.SetMagic(magic == null || magic.Action == null ? null : magic.Action.CreateSpec());
            shopDirty = true;
            loadoutDirty = true;
            inventoryScreenDirty = true;
        }

        /// <summary>
        /// 下のアイテムと技の枠へ値を渡す。中身は所持品が変わったときだけ作り直し、技と魔法を今使えるかは毎フレーム渡す。
        /// </summary>
        private void UpdateLoadoutBar()
        {
            var bar = Hud.Loadout;
            var weapon = Inventory.MainWeapon;

            // 店の画面は下まで広がるので、開いている間は下の枠を隠す。店では攻撃、技、魔法を使わない。
            bar.SetVisible(!IsShopOpen);
            if (loadoutDirty)
            {
                loadoutDirty = false;
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

                bar.Show(new LoadoutBarModel(
                    weapon == null ? null : Icon($"icon-skill-{weapon.Id}"),
                    magics,
                    weapon == null ? -1 : weapon.SelectedMagicIndex,
                    quick,
                    Inventory.SelectedQuickIndex));
            }

            var idle = !PlayerCombat.ActionState.IsBusy && PlayerHealth.IsAlive;
            var magic = FindMagic(weapon?.SelectedMagic);
            bar.SetSkillCooldown(PlayerCombat.Loadout.Get(CharacterActionSlot.Special) == null ? 1f : SkillCooldown.RemainingFraction);
            bar.SetMagicReady(idle && magic != null && Mana.CanSpend(magic.ManaCost));
        }

        /// <summary>
        /// メイン武器の固有の技を使う。待ち時間の間は使えず、Actionを始められたときだけ待ち時間を始める。
        /// </summary>
        public bool TryUseSkill()
        {
            return TryUseSkill(new CharacterActionCommand(CharacterActionSlot.Special, PlayerActor.Facing));
        }

        private bool TryUseSkill(CharacterActionCommand command)
        {
            if (!SkillCooldown.IsReady)
            {
                LastMessage = "技はまだ使えない";
                return false;
            }

            if (!PlayerCombat.TryStartAction(command))
            {
                return false;
            }

            SkillCooldown.Start(SkillCooldownSeconds);
            return true;
        }

        /// <summary>
        /// メイン武器で選んでいる魔法を唱える。MPが足りなければ唱えず、Actionを始められたときだけMPを使う。
        /// </summary>
        public bool TryCastMagic()
        {
            return TryCastMagic(new CharacterActionCommand(CharacterActionSlot.Magic, PlayerActor.Facing));
        }

        private bool TryCastMagic(CharacterActionCommand command)
        {
            var weapon = Inventory.MainWeapon;
            var magic = FindMagic(weapon?.SelectedMagic);
            if (magic == null)
            {
                LastMessage = weapon == null || weapon.MagicSlotCount == 0 ? "この武器に魔法の枠はない" : "魔法が入っていない";
                return false;
            }

            if (!Mana.CanSpend(magic.ManaCost))
            {
                LastMessage = "MPが足りない";
                return false;
            }

            if (!PlayerCombat.TryStartAction(command))
            {
                return false;
            }

            Mana.TrySpend(magic.ManaCost);
            LastMessage = $"{magic.DisplayName}を唱えた";
            return true;
        }

        /// <summary>
        /// 使用枠の物を使う。回復薬はHPが減っているときだけ飲む。数字キーはこれを呼ぶ。
        /// </summary>
        public bool TryUseQuickSlot(int index)
        {
            if (index < 0 || index >= Inventory.QuickCapacity || Inventory.GetQuickItem(index) == null)
            {
                LastMessage = "使用枠が空いている";
                return false;
            }

            var item = Inventory.GetQuickItem(index);
            if (item.Id == PotionItemId && PlayerHealth.CurrentHealth >= PlayerHealth.MaximumHealth)
            {
                LastMessage = "HPは減っていない";
                return false;
            }

            var result = Inventory.UseQuickItem(index);
            if (result.Outcome != CharacterItemUseOutcome.Consumed)
            {
                return false;
            }

            if (item.Id == PotionItemId)
            {
                PlayerHealth.RestoreHealth(PotionHealing);
            }

            LastMessage = $"{GetItemName(item)}を使った";
            return true;
        }

        /// <summary>
        /// 選んでいる使用枠の物を使う。数字キーのないゲームパッドで使う。
        /// </summary>
        public bool TryUseSelectedItem()
        {
            return TryUseQuickSlot(Inventory.SelectedQuickIndex);
        }

        /// <summary>
        /// カバンの物を使う。装備は着け、魔法は武器にはめ、回復薬は使用枠へ移す。
        /// </summary>
        public bool TryUseBagItem(int index)
        {
            var item = index >= 0 && index < Inventory.BagCapacity ? Inventory.GetBagItem(index) : null;
            if (item == null)
            {
                LastMessage = "カバンの枠が空いている";
                return false;
            }

            var name = GetItemName(item);
            var result = Inventory.UseBagItem(index);
            switch (result.Outcome)
            {
                case CharacterItemUseOutcome.Equipped:
                    LastMessage = $"{name}を着けた";
                    break;
                case CharacterItemUseOutcome.MagicInserted:
                    LastMessage = $"{name}を{GetItemName(Inventory.MainWeapon)}にはめた";
                    break;
                case CharacterItemUseOutcome.MovedToQuickSlot:
                    LastMessage = $"{name}を使用枠に入れた";
                    break;
                case CharacterItemUseOutcome.NoRoomInBag:
                    LastMessage = "外す物がカバンに入りきらない";
                    break;
                case CharacterItemUseOutcome.NoMagicSlot:
                    LastMessage = "魔法の枠が空いていない";
                    break;
                case CharacterItemUseOutcome.NoRoomInQuickSlots:
                    LastMessage = "使用枠がいっぱい";
                    break;
                default:
                    LastMessage = $"{name}は使えない";
                    break;
            }

            return result.IsApplied;
        }

        /// <summary>
        /// 所持品の操作を読む。魔法の切り替え、数字キーで使用枠の物を使う。ゲームパッドは使用枠を選んで使う。
        /// </summary>
        private void PollInventoryInput(HumanInventoryInput input)
        {
            if (input.NextMagic)
            {
                Inventory.SelectNextMagic();
            }

            // 数字キーはその番号の使用枠の物をすぐ使う（2026-11-13、所有者）。選んで使う形はゲームパッドに残す。
            if (input.UsedQuickSlot >= 0)
            {
                TryUseQuickSlot(input.UsedQuickSlot);
            }

            if (input.NextItem)
            {
                Inventory.SelectNextQuickItem();
            }

            if (input.UseItem)
            {
                TryUseSelectedItem();
            }
        }

        /// <summary>
        /// 持ち物の名前。定義の表示名を引き、見つからなければidを返す。
        /// </summary>
        public string GetItemName(CharacterItem item)
        {
            if (item == null)
            {
                return string.Empty;
            }

            if (item.Id == PotionItemId)
            {
                return "回復薬";
            }

            var equipmentDefinition = FindEquipmentDefinition(item.Id);
            if (equipmentDefinition != null)
            {
                return equipmentDefinition.DisplayName;
            }

            var magic = FindMagic(item);
            if (magic != null)
            {
                return magic.DisplayName;
            }

            var part = catalog == null ? null : catalog.FindArmour(ArmourPartId(item.Id));
            return part == null ? item.Id : $"{part.DisplayName}（{SlotName(ArmourSlotOf(item))}）";
        }

        private CharacterEquipmentDefinition FindEquipmentDefinition(string id)
        {
            foreach (var weapon in equipment)
            {
                if (weapon != null && weapon.Id == id)
                {
                    return weapon;
                }
            }

            if (offHandEquipment != null && offHandEquipment.Id == id)
            {
                return offHandEquipment;
            }

            if (catalog == null)
            {
                return null;
            }

            return catalog.FindWeapon(id) ?? catalog.FindOffHand(id);
        }

        private CharacterMagicDefinition FindMagic(CharacterItem item)
        {
            if (item == null || item.Kind != CharacterItemKind.Magic)
            {
                return null;
            }

            foreach (var magic in magicDefinitions)
            {
                if (magic != null && magic.Id == item.Id)
                {
                    return magic;
                }
            }

            return null;
        }

        /// <summary>
        /// 防具の部位1つを持ち物にする。idは組と部位をつないだ物で、店の持っている物の印と同じである。
        /// </summary>
        private static CharacterItem CreateArmourItem(CharacterSpriteSetDefinition part, CharacterArmourSlot slot)
        {
            return new CharacterItem(ArmourKey(part, slot), CharacterItemKind.Equipment, new[] { ToEquipSlot(slot) });
        }

        private static string ArmourPartId(string itemId)
        {
            var colon = itemId.LastIndexOf(':');
            return colon < 0 ? itemId : itemId.Substring(0, colon);
        }

        private static CharacterArmourSlot ArmourSlotOf(CharacterItem item)
        {
            return item.Occupies(CharacterEquipSlot.Head)
                ? CharacterArmourSlot.Head
                : item.Occupies(CharacterEquipSlot.Legs) && !item.Occupies(CharacterEquipSlot.Torso)
                    ? CharacterArmourSlot.Legs
                    : CharacterArmourSlot.Torso;
        }

        private static CharacterEquipSlot ToEquipSlot(CharacterArmourSlot slot)
        {
            switch (slot)
            {
                case CharacterArmourSlot.Head:
                    return CharacterEquipSlot.Head;
                case CharacterArmourSlot.Legs:
                    return CharacterEquipSlot.Legs;
                default:
                    return CharacterEquipSlot.Torso;
            }
        }
    }
}
