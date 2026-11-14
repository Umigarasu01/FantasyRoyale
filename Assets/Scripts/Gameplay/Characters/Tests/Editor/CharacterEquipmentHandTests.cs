using FantasyRoyale.Gameplay.Characters.Unity;
using NUnit.Framework;
using UnityEngine;

namespace FantasyRoyale.Gameplay.Characters.Tests.Editor
{
    /// <summary>
    /// 武器の定義の持つ手と魔法の枠が、所持品の持ち物と魔法のSlotへ正しく渡ることを確かめる。
    /// </summary>
    public sealed class CharacterEquipmentHandTests
    {
        /// <summary>
        /// 片手、両手、オフハンドで埋める枠が変わり、魔法の枠はメイン武器だけが持つ。
        /// </summary>
        [Test]
        public void CreateItem_UsesHandUseAndMagicSlots()
        {
            var sword = Create("sword", CharacterHandUse.OneHanded, 2);
            var bow = Create("bow", CharacterHandUse.TwoHanded, 1);
            var shield = Create("shield", CharacterHandUse.OffHand, 3);

            var swordItem = sword.CreateItem();
            var bowItem = bow.CreateItem();
            var shieldItem = shield.CreateItem();

            Assert.That(swordItem.Slots, Is.EqualTo(new[] { CharacterEquipSlot.MainHand }));
            Assert.That(swordItem.MagicSlotCount, Is.EqualTo(2));
            Assert.That(bowItem.Slots, Is.EqualTo(new[] { CharacterEquipSlot.MainHand, CharacterEquipSlot.OffHand }));
            Assert.That(shieldItem.Slots, Is.EqualTo(new[] { CharacterEquipSlot.OffHand }));
            Assert.That(shieldItem.MagicSlotCount, Is.Zero, "オフハンドは魔法の枠を持たない。");
            Assert.That(shield.IsOffHand, Is.True);

            Object.DestroyImmediate(sword);
            Object.DestroyImmediate(bow);
            Object.DestroyImmediate(shield);
        }

        /// <summary>
        /// オフハンドに通常攻撃を持たせると、問題として返す。
        /// </summary>
        [Test]
        public void FindMotionProblems_RejectsActionsOnOffHand()
        {
            var shield = Create("shield", CharacterHandUse.OffHand, 0);
            Assert.That(shield.FindMotionProblems(), Is.Empty);

            var action = ScriptableObject.CreateInstance<CharacterActionDefinition>();
            shield.Configure("盾", action, null);
            Assert.That(shield.FindMotionProblems(), Is.Not.Empty);

            Object.DestroyImmediate(action);
            Object.DestroyImmediate(shield);
        }

        /// <summary>
        /// 魔法のSlotは武器を差し替えても残り、別に差し替える。
        /// </summary>
        [Test]
        public void Loadout_KeepsMagicAcrossWeaponReplace()
        {
            var timing = new CharacterActionTiming(0.1f, 0f, 0.2f);
            var fire = CharacterActionSpec.CreateProjectile("magic-fire", 20, timing, 6f, 9f, CharacterActionMotion.Shoot);
            var loadout = new CharacterLoadout(null, null);
            loadout.SetMagic(fire);

            loadout.Replace(null, null);

            Assert.That(loadout.Get(CharacterActionSlot.Magic), Is.SameAs(fire));
            loadout.SetMagic(null);
            Assert.That(loadout.Get(CharacterActionSlot.Magic), Is.Null);
        }

        private static CharacterEquipmentDefinition Create(string id, CharacterHandUse handUse, int magicSlots)
        {
            var definition = ScriptableObject.CreateInstance<CharacterEquipmentDefinition>();
            definition.SetId(id);
            definition.ConfigureHands(handUse, magicSlots);
            return definition;
        }
    }
}
