using System.Collections.Generic;
using FantasyRoyale.Gameplay.Characters.Unity;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace FantasyRoyale.Gameplay.Characters.Tests.Editor
{
    /// <summary>
    /// 装備の部品が体のアニメーションへ重ねられることを確認する。
    /// 部品や体を足したときに向きの絵、手の位置、ドットの細かさが欠けると、このTestで止まる。
    /// </summary>
    public sealed class CharacterSpriteContractTests
    {
        private const string HeroSpritesPath = "Assets/Data/Gameplay/Characters/HeroSprites.asset";

        private readonly List<Object> created = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (var item in created)
            {
                Object.DestroyImmediate(item);
            }

            created.Clear();
        }

        [Test]
        public void FindAnchorProblems_AcceptsBodyWithAnchorsOnEveryFrame()
        {
            Assert.That(CreateBody(4, 4).FindAnchorProblems(), Is.Empty);
        }

        [Test]
        public void FindAnchorProblems_ReportsFramesWithoutAnchors()
        {
            var problems = CreateBody(4, 3).FindAnchorProblems();
            Assert.That(problems, Is.Not.Empty);
            Assert.That(problems[0], Does.Contain("手の位置"));
        }

        [Test]
        public void FindAnchorProblems_ReportsMissingView()
        {
            var body = ScriptableObject.CreateInstance<CharacterSpriteSetDefinition>();
            created.Add(body);
            Assert.That(body.FindAnchorProblems(), Is.Not.Empty);
        }

        [Test]
        public void FindProblems_ReportsMissingViewAndDifferentPixelSize()
        {
            var gear = ScriptableObject.CreateInstance<CharacterGearDefinition>();
            created.Add(gear);
            var sprite = CreateSprite(32f);
            gear.Configure(CharacterGearSlot.MainHand, sprite, sprite, sprite, sprite, null);

            var problems = gear.FindProblems(64f);
            Assert.That(problems.Count, Is.EqualTo(5));
            Assert.That(problems[0], Does.Contain("PPU"));
            Assert.That(problems[4], Does.Contain("絵がありません"));
        }

        /// <summary>
        /// 実際の体と全ての部品が組み合わせられることを確認する。
        /// </summary>
        [Test]
        public void RealGear_FitsTheHeroBody()
        {
            var body = AssetDatabase.LoadAssetAtPath<CharacterSpriteSetDefinition>(HeroSpritesPath);
            Assert.That(body, Is.Not.Null);
            Assert.That(body.FindAnchorProblems(), Is.Empty);

            var pixelsPerUnit = body.GetView(CharacterDirection8.South).Idle.pixelsPerUnit;
            var count = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:CharacterGearDefinition"))
            {
                var gear = AssetDatabase.LoadAssetAtPath<CharacterGearDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                Assert.That(gear.FindProblems(pixelsPerUnit), Is.Empty, gear.name);
                count++;
            }

            Assert.That(count, Is.GreaterThanOrEqualTo(4), "部品が見つかりません。");
        }

        /// <summary>
        /// 全ての装備が部品を持ち、同じ位置へ部品が重ならないことを確認する。
        /// </summary>
        [Test]
        public void RealEquipment_UsesEachSlotOnce()
        {
            var count = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:CharacterEquipmentDefinition"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var equipment = AssetDatabase.LoadAssetAtPath<CharacterEquipmentDefinition>(path);
                Assert.That(equipment.Gear, Is.Not.Empty, equipment.name);
                var slots = new HashSet<CharacterGearSlot>();
                foreach (var gear in equipment.Gear)
                {
                    Assert.That(slots.Add(gear.Slot), Is.True, $"{equipment.name}の{gear.Slot}が重なっています。");
                }

                count++;
            }

            Assert.That(count, Is.GreaterThanOrEqualTo(2));
        }

        private Sprite CreateSprite(float pixelsPerUnit)
        {
            var texture = new Texture2D(8, 8);
            created.Add(texture);
            var sprite = Sprite.Create(texture, new Rect(0f, 0f, 8f, 8f), new Vector2(0.5f, 0.05f), pixelsPerUnit);
            created.Add(sprite);
            return sprite;
        }

        private CharacterSpriteSetDefinition CreateBody(int frames, int anchors)
        {
            CharacterSpriteView NewView()
            {
                var run = new Sprite[frames];
                for (var index = 0; index < frames; index++)
                {
                    run[index] = CreateSprite(64f);
                }

                return new CharacterSpriteView(CreateSprite(64f), run, default, new CharacterSpriteAnchors[anchors]);
            }

            var body = ScriptableObject.CreateInstance<CharacterSpriteSetDefinition>();
            created.Add(body);
            body.Configure(11f, NewView(), NewView(), NewView(), NewView(), NewView());
            return body;
        }
    }
}
