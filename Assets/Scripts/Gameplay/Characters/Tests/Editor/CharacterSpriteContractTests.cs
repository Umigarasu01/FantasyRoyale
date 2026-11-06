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
        public void FindAnchorProblems_ReportsMissingFist()
        {
            var body = ScriptableObject.CreateInstance<CharacterSpriteSetDefinition>();
            created.Add(body);
            var view = new CharacterSpriteView(CreateSprite(64f), new[] { CreateSprite(64f) }, default, new CharacterSpriteAnchors[1]);
            body.Configure(11f, view, view, view, view, view);

            var problems = body.FindAnchorProblems();
            Assert.That(problems, Is.Not.Empty);
            Assert.That(problems[0], Does.Contain("拳の絵"));
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
            Assert.That(body.GetView(CharacterDirection8.East).GetMotion(CharacterActionMotion.Swing).Frames.Length, Is.EqualTo(3));
        }

        /// <summary>
        /// 回る部品は、指定の角度に一番近い絵を返す。
        /// </summary>
        [Test]
        public void GetRotatedSprite_PicksTheNearestAngle()
        {
            var gear = ScriptableObject.CreateInstance<CharacterGearDefinition>();
            created.Add(gear);
            var sprites = new Sprite[24];
            for (var index = 0; index < sprites.Length; index++)
            {
                sprites[index] = CreateSprite(64f);
            }

            gear.ConfigureRotations(CharacterGearSlot.MainHand, sprites);
            Assert.That(gear.Rotates, Is.True);
            Assert.That(gear.GetRotatedSprite(0f), Is.SameAs(sprites[0]));
            Assert.That(gear.GetRotatedSprite(44f), Is.SameAs(sprites[3]));
            Assert.That(gear.GetRotatedSprite(-90f), Is.SameAs(sprites[18]));
            Assert.That(gear.GetRotatedSprite(359f), Is.SameAs(sprites[0]));
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

        /// <summary>
        /// 全ての全身セットで全ての装備を持てることを確認する。
        /// </summary>
        [Test]
        public void RealEquipment_FitsEveryArmourSet()
        {
            var sets = 0;
            foreach (var setGuid in AssetDatabase.FindAssets("t:CharacterSpriteSetDefinition Hero"))
            {
                var body = AssetDatabase.LoadAssetAtPath<CharacterSpriteSetDefinition>(AssetDatabase.GUIDToAssetPath(setGuid));
                Assert.That(body.FindAnchorProblems(), Is.Empty, body.name);
                foreach (var guid in AssetDatabase.FindAssets("t:CharacterEquipmentDefinition"))
                {
                    var equipment = AssetDatabase.LoadAssetAtPath<CharacterEquipmentDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                    Assert.That(body.FindEquipmentProblems(equipment), Is.Empty, $"{body.name}と{equipment.name}");
                }

                sets++;
            }

            Assert.That(sets, Is.GreaterThanOrEqualTo(2), "全身セットが見つかりません。");
        }

        /// <summary>
        /// 振る動きのコマがない体は、振る武器を持てない。
        /// </summary>
        [Test]
        public void FindEquipmentProblems_ReportsBodyWithoutSwingFrames()
        {
            var equipment = CreateEquipment(CreateMelee(), CreateRotatingGear(CharacterGearSlot.MainHand));

            var problems = CreateBody(4, 4).FindEquipmentProblems(equipment);
            Assert.That(problems.Count, Is.EqualTo(5), "全ての向きが報告されます。");
            Assert.That(problems[0], Does.Contain("Swing"));
        }

        /// <summary>
        /// 射る動きの絵はまだないので、なくても弓は持てる。
        /// </summary>
        [Test]
        public void FindEquipmentProblems_AcceptsBowWithoutShootFrames()
        {
            var action = ScriptableObject.CreateInstance<CharacterActionDefinition>();
            created.Add(action);
            action.ConfigureProjectile("shot", 1, new CharacterActionTiming(0.1f, 0f, 0.1f), 6f, 10f);
            var sprite = CreateSprite(64f);
            var bow = ScriptableObject.CreateInstance<CharacterGearDefinition>();
            created.Add(bow);
            bow.Configure(CharacterGearSlot.OffHand, sprite, sprite, sprite, sprite, sprite);

            Assert.That(CreateBody(4, 4).FindEquipmentProblems(CreateEquipment(action, bow)), Is.Empty);
        }

        /// <summary>
        /// 振る武器には、利き手に角度ごとの絵が要る。
        /// </summary>
        [Test]
        public void FindMotionProblems_ReportsSwingWithoutRotatingGear()
        {
            var sprite = CreateSprite(64f);
            var gear = ScriptableObject.CreateInstance<CharacterGearDefinition>();
            created.Add(gear);
            gear.Configure(CharacterGearSlot.MainHand, sprite, sprite, sprite, sprite, sprite);

            var problems = CreateEquipment(CreateMelee(), gear).FindMotionProblems();
            Assert.That(problems.Count, Is.EqualTo(1));
            Assert.That(problems[0], Does.Contain("角度ごとの絵"));
        }

        /// <summary>
        /// 体が角度を持つのは利き手だけなので、他の位置の部品は回せない。
        /// </summary>
        [Test]
        public void FindProblems_ReportsRotatingGearOutsideMainHand()
        {
            var problems = CreateRotatingGear(CharacterGearSlot.OffHand).FindProblems(64f);
            Assert.That(problems.Count, Is.EqualTo(1));
            Assert.That(problems[0], Does.Contain("利き手"));
        }

        /// <summary>
        /// 定義Assetの動きの型は、実行時のActionへ渡る。
        /// </summary>
        [Test]
        public void CreateSpec_CarriesTheMotion()
        {
            Assert.That(CreateMelee().CreateSpec().Motion, Is.EqualTo(CharacterActionMotion.Swing));
        }

        private CharacterActionDefinition CreateMelee()
        {
            var action = ScriptableObject.CreateInstance<CharacterActionDefinition>();
            created.Add(action);
            action.ConfigureMelee("slash", 1, new CharacterActionTiming(0.1f, 0.1f, 0.1f), 1f, 90f);
            return action;
        }

        private CharacterGearDefinition CreateRotatingGear(CharacterGearSlot slot)
        {
            var gear = ScriptableObject.CreateInstance<CharacterGearDefinition>();
            created.Add(gear);
            gear.ConfigureRotations(slot, new[] { CreateSprite(64f) });
            return gear;
        }

        private CharacterEquipmentDefinition CreateEquipment(CharacterActionDefinition action, CharacterGearDefinition gear)
        {
            var equipment = ScriptableObject.CreateInstance<CharacterEquipmentDefinition>();
            created.Add(equipment);
            equipment.Configure("test", action, null);
            equipment.SetGear(new[] { gear });
            return equipment;
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

                var fist = CreateSprite(64f);
                var hands = new CharacterSpriteAnchors(Vector2.zero, Vector2.zero, Vector2.zero, 0f, fist, fist);
                var runAnchors = new CharacterSpriteAnchors[anchors];
                for (var index = 0; index < anchors; index++)
                {
                    runAnchors[index] = hands;
                }

                return new CharacterSpriteView(CreateSprite(64f), run, hands, runAnchors);
            }

            var body = ScriptableObject.CreateInstance<CharacterSpriteSetDefinition>();
            created.Add(body);
            body.Configure(11f, NewView(), NewView(), NewView(), NewView(), NewView());
            return body;
        }
    }
}
