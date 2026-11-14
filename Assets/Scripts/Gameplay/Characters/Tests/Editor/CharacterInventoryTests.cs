using System;
using NUnit.Framework;

namespace FantasyRoyale.Gameplay.Characters.Tests.Editor
{
    /// <summary>
    /// 所持品の決まり（重ねないカバン、一体の装備、武器ごとの魔法、空の枠を飛ばす切り替え）を確かめる。
    /// </summary>
    public sealed class CharacterInventoryTests
    {
        private static CharacterItem Sword(int magicSlots = 0)
        {
            return new CharacterItem("sword", CharacterItemKind.Equipment, new[] { CharacterEquipSlot.MainHand }, magicSlots);
        }

        private static CharacterItem Shield()
        {
            return new CharacterItem("shield", CharacterItemKind.Equipment, new[] { CharacterEquipSlot.OffHand });
        }

        private static CharacterItem Bow(int magicSlots = 0)
        {
            return new CharacterItem(
                "bow",
                CharacterItemKind.Equipment,
                new[] { CharacterEquipSlot.MainHand, CharacterEquipSlot.OffHand },
                magicSlots);
        }

        private static CharacterItem Potion()
        {
            return new CharacterItem("potion", CharacterItemKind.Consumable);
        }

        private static CharacterItem Magic(string id)
        {
            return new CharacterItem(id, CharacterItemKind.Magic);
        }

        /// <summary>
        /// 枠を持たない装備や、メイン武器でない物の魔法の枠を、作るときに拒む。
        /// </summary>
        [Test]
        public void Item_RejectsBrokenDefinitions()
        {
            Assert.Throws<ArgumentException>(() => new CharacterItem("x", CharacterItemKind.Equipment));
            Assert.Throws<ArgumentException>(() => new CharacterItem("x", CharacterItemKind.Consumable, new[] { CharacterEquipSlot.Head }));
            Assert.Throws<ArgumentException>(() => new CharacterItem("x", CharacterItemKind.Equipment, new[] { CharacterEquipSlot.OffHand }, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new CharacterItem("x", CharacterItemKind.Equipment, new[] { CharacterEquipSlot.MainHand }, -1));
        }

        /// <summary>
        /// 同じ物を重ねず、1つの物が1つの枠を使い、いっぱいなら入らない。
        /// </summary>
        [Test]
        public void Bag_HoldsOneItemPerSlotWithoutStacking()
        {
            var inventory = new CharacterInventory(2);

            Assert.That(inventory.TryAddToBag(Potion()), Is.EqualTo(0));
            Assert.That(inventory.TryAddToBag(Potion()), Is.EqualTo(1));
            Assert.That(inventory.TryAddToBag(Potion()), Is.EqualTo(-1));
            Assert.That(inventory.CountFreeBagSlots(), Is.Zero);

            inventory.ExpandBag(1);
            Assert.That(inventory.BagCapacity, Is.EqualTo(3));
            Assert.That(inventory.TryAddToBag(Potion()), Is.EqualTo(2));
        }

        /// <summary>
        /// 両手の武器は剣と盾の2つを外してカバンへ入れ、戻すと剣だけが戻ってオフハンドは空になる。
        /// </summary>
        [Test]
        public void TwoHandedWeapon_ReplacesMainHandAndOffHandTogether()
        {
            var inventory = new CharacterInventory(5);
            var sword = Sword();
            var shield = Shield();
            var bow = Bow();
            Assert.That(inventory.TryEquipNew(sword), Is.True);
            Assert.That(inventory.TryEquipNew(shield), Is.True);
            inventory.TryAddToBag(bow);

            var result = inventory.UseBagItem(0);

            Assert.That(result.Outcome, Is.EqualTo(CharacterItemUseOutcome.Equipped));
            Assert.That(inventory.GetEquipped(CharacterEquipSlot.MainHand), Is.SameAs(bow));
            Assert.That(inventory.GetEquipped(CharacterEquipSlot.OffHand), Is.SameAs(bow));
            Assert.That(inventory.GetBagItem(0), Is.SameAs(sword), "外れた1つ目は使った物の枠へ入る。");
            Assert.That(inventory.GetBagItem(1), Is.SameAs(shield));

            inventory.UseBagItem(0);
            Assert.That(inventory.GetEquipped(CharacterEquipSlot.MainHand), Is.SameAs(sword));
            Assert.That(inventory.GetEquipped(CharacterEquipSlot.OffHand), Is.Null);
            Assert.That(inventory.GetBagItem(0), Is.SameAs(bow));
        }

        /// <summary>
        /// 外れる物がカバンに入りきらなければ、何も変えずに断る。
        /// </summary>
        [Test]
        public void EquipFromBag_RejectsWhenDisplacedItemsDoNotFit()
        {
            var inventory = new CharacterInventory(1);
            var sword = Sword();
            var shield = Shield();
            var bow = Bow();
            inventory.TryEquipNew(sword);
            inventory.TryEquipNew(shield);
            inventory.TryAddToBag(bow);

            var result = inventory.UseBagItem(0);

            Assert.That(result.Outcome, Is.EqualTo(CharacterItemUseOutcome.NoRoomInBag));
            Assert.That(inventory.GetEquipped(CharacterEquipSlot.MainHand), Is.SameAs(sword));
            Assert.That(inventory.GetEquipped(CharacterEquipSlot.OffHand), Is.SameAs(shield));
            Assert.That(inventory.GetBagItem(0), Is.SameAs(bow));
        }

        /// <summary>
        /// 胴と足を埋める一体の防具は、2つの部位を外し、着けている間は両方の枠を同じ物で埋める。
        /// </summary>
        [Test]
        public void OnePieceArmour_FillsTorsoAndLegs()
        {
            var inventory = new CharacterInventory(3);
            var torso = new CharacterItem("leather", CharacterItemKind.Equipment, new[] { CharacterEquipSlot.Torso });
            var legs = new CharacterItem("sandals", CharacterItemKind.Equipment, new[] { CharacterEquipSlot.Legs });
            var robe = new CharacterItem("robe", CharacterItemKind.Equipment, new[] { CharacterEquipSlot.Torso, CharacterEquipSlot.Legs });
            inventory.TryEquipNew(torso);
            inventory.TryEquipNew(legs);

            Assert.That(inventory.TryEquipNew(robe), Is.True);

            Assert.That(inventory.GetEquipped(CharacterEquipSlot.Torso), Is.SameAs(robe));
            Assert.That(inventory.GetEquipped(CharacterEquipSlot.Legs), Is.SameAs(robe));
            Assert.That(inventory.CountFreeBagSlots(), Is.EqualTo(1));
            Assert.That(inventory.TryUnequip(CharacterEquipSlot.Legs), Is.True);
            Assert.That(inventory.GetEquipped(CharacterEquipSlot.Torso), Is.Null, "一体の装備は全ての枠から外れる。");
        }

        /// <summary>
        /// はめた魔法は武器の1本ごとに残り、持ち替えて戻ると選んでいた魔法のままである。
        /// </summary>
        [Test]
        public void Magic_StaysWithEachWeaponAcrossSwaps()
        {
            var inventory = new CharacterInventory(5);
            var staff = Sword(3);
            var bow = Bow(1);
            inventory.TryEquipNew(staff);
            var fire = Magic("fire");
            var ice = Magic("ice");
            inventory.TryAddToBag(fire);
            inventory.TryAddToBag(ice);
            inventory.TryAddToBag(bow);

            Assert.That(inventory.UseBagItem(0).Outcome, Is.EqualTo(CharacterItemUseOutcome.MagicInserted));
            Assert.That(inventory.UseBagItem(1).Outcome, Is.EqualTo(CharacterItemUseOutcome.MagicInserted));
            Assert.That(inventory.SelectNextMagic(), Is.EqualTo(1));

            inventory.UseBagItem(2);
            Assert.That(inventory.MainWeapon, Is.SameAs(bow));
            Assert.That(bow.SelectedMagic, Is.Null);

            inventory.UseBagItem(2);
            Assert.That(inventory.MainWeapon, Is.SameAs(staff));
            Assert.That(staff.SelectedMagic, Is.SameAs(ice));
        }

        /// <summary>
        /// 魔法の枠のない武器や、空きのない武器には、魔法を入れずにカバンへ残す。
        /// </summary>
        [Test]
        public void Magic_StaysInBagWhenWeaponHasNoFreeSlot()
        {
            var inventory = new CharacterInventory(3);
            inventory.TryEquipNew(Sword());
            var fire = Magic("fire");
            inventory.TryAddToBag(fire);

            var result = inventory.UseBagItem(0);

            Assert.That(result.Outcome, Is.EqualTo(CharacterItemUseOutcome.NoMagicSlot));
            Assert.That(inventory.GetBagItem(0), Is.SameAs(fire));
            Assert.That(inventory.SelectNextMagic(), Is.EqualTo(-1));
        }

        /// <summary>
        /// 魔法の切り替えは空の枠を飛ばし、1つしかなければ変わらない。
        /// </summary>
        [Test]
        public void SelectNextMagic_SkipsEmptySlots()
        {
            var staff = Sword(4);
            var bolt = Magic("bolt");
            var wind = Magic("wind");
            staff.TryInsertMagic(bolt);
            staff.TryInsertMagic(Magic("fire"));
            staff.TryInsertMagic(wind);
            staff.RemoveMagic(1);

            Assert.That(staff.SelectedMagic, Is.SameAs(bolt));
            Assert.That(staff.SelectNextMagic(), Is.EqualTo(2));
            Assert.That(staff.SelectNextMagic(), Is.EqualTo(0));

            staff.RemoveMagic(2);
            Assert.That(staff.SelectNextMagic(), Is.EqualTo(0));
        }

        /// <summary>
        /// 確認用の着替えはカバンを使わず、外れた物を返して所持品から消す。
        /// </summary>
        [Test]
        public void ForceEquip_DropsDisplacedItemsWithoutTouchingTheBag()
        {
            var inventory = new CharacterInventory(1);
            var sword = Sword();
            inventory.TryEquipNew(sword);
            inventory.TryEquipNew(Shield());
            inventory.TryAddToBag(Potion());
            var bow = Bow();

            var displaced = inventory.ForceEquip(bow);

            Assert.That(displaced, Has.Count.EqualTo(2));
            Assert.That(inventory.MainWeapon, Is.SameAs(bow));
            Assert.That(inventory.Contains(sword), Is.False);
            Assert.That(inventory.FindInBag("potion"), Is.Zero);
            Assert.That(inventory.FindInBag("sword"), Is.EqualTo(-1));
        }

        /// <summary>
        /// 使う物は使用枠へ先に入り、入らなければカバンへ入る。使用枠の物を使うと消え、選んでいた物がなくなれば次の物を選ぶ。
        /// 使う物でない物は使用枠へ入らない。
        /// </summary>
        [Test]
        public void QuickSlots_HoldConsumablesApartFromTheBag()
        {
            var inventory = new CharacterInventory(5, 2);
            var first = Potion();
            var second = Potion();
            var third = Potion();
            var relic = new CharacterItem("relic", CharacterItemKind.Other);

            Assert.That(inventory.TryStore(first), Is.True);
            Assert.That(inventory.TryStore(relic), Is.True);
            Assert.That(inventory.TryStore(second), Is.True);
            Assert.That(inventory.TryStore(third), Is.True);
            Assert.That(inventory.GetQuickItem(0), Is.SameAs(first));
            Assert.That(inventory.GetQuickItem(1), Is.SameAs(second));
            Assert.That(inventory.GetBagItem(0), Is.SameAs(relic), "使う物でない物はカバンへ入ります。");
            Assert.That(inventory.GetBagItem(1), Is.SameAs(third), "使用枠がいっぱいなら、使う物もカバンへ入ります。");
            Assert.That(inventory.TryAddToQuick(relic), Is.EqualTo(-1));

            var used = inventory.UseSelected();
            Assert.That(used.Outcome, Is.EqualTo(CharacterItemUseOutcome.Consumed));
            Assert.That(used.Item, Is.SameAs(first));
            Assert.That(inventory.SelectedQuickItem, Is.SameAs(second), "使い切ったら次の物を選びます。");

            Assert.That(inventory.UseBagItem(1).Outcome, Is.EqualTo(CharacterItemUseOutcome.MovedToQuickSlot), "カバンの使う物は使用枠へ移ります。");
            Assert.That(inventory.GetQuickItem(0), Is.SameAs(third));
            Assert.That(inventory.TryStore(Potion()), Is.True);
            var extra = inventory.FindInBag("potion");
            Assert.That(inventory.UseBagItem(extra).Outcome, Is.EqualTo(CharacterItemUseOutcome.NoRoomInQuickSlots));

            Assert.That(inventory.TryMoveQuickToBag(1), Is.True);
            Assert.That(inventory.GetQuickItem(1), Is.Null);
            Assert.That(inventory.UseQuickItem(1).Outcome, Is.EqualTo(CharacterItemUseOutcome.EmptySlot));
        }
    }
}
