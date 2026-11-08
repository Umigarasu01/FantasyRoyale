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

        private const string CatalogPath = "Assets/Data/Gameplay/Equipment/EquipmentCatalog.asset";

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
            Assert.That(body.GetView(CharacterDirection8.East).GetMotion(CharacterActionMotion.Swing).Frames.Length, Is.EqualTo(4));
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
        /// 拳の両端へ分けて付ける部品は、先の側と柄頭の側の絵を同じ規則で選ぶ。分けない部品は柄頭の側を持たない。
        /// </summary>
        [Test]
        public void SplitGear_PicksBothPiecesAtTheSameAngle()
        {
            var gear = ScriptableObject.CreateInstance<CharacterGearDefinition>();
            created.Add(gear);
            var blades = new Sprite[24];
            var pommels = new Sprite[24];
            for (var index = 0; index < blades.Length; index++)
            {
                blades[index] = CreateSprite(64f);
                pommels[index] = CreateSprite(64f);
            }

            gear.ConfigureRotations(CharacterGearSlot.MainHand, blades);
            Assert.That(gear.IsSplitAtFist, Is.False);
            Assert.That(gear.GetBackRotatedSprite(0f), Is.Null);

            gear.ConfigureRotations(CharacterGearSlot.MainHand, blades, pommels);
            Assert.That(gear.IsSplitAtFist, Is.True);
            Assert.That(gear.GetRotatedSprite(241.4f), Is.SameAs(blades[16]));
            Assert.That(gear.GetBackRotatedSprite(241.4f), Is.SameAs(pommels[16]));
            Assert.That(gear.FindProblems(64f), Is.Empty);

            gear.ConfigureRotations(CharacterGearSlot.MainHand, blades, new[] { pommels[0] });
            Assert.That(gear.FindProblems(64f), Is.Not.Empty, "柄頭の側の角度の数が違えば使えません。");
        }

        /// <summary>
        /// 武器種の走りは、その武器種で引けば返り、持たない武器種ではnullになる。共通の走りとコマの数が違えば問題になる。
        /// </summary>
        [Test]
        public void GetRun_ReturnsTheWeaponKindsRun()
        {
            var fist = CreateSprite(64f);
            var hands = new CharacterSpriteAnchors(Vector2.zero, Vector2.zero, Vector2.zero, 0f, fist, fist);
            var swordRun = new CharacterRunFrames(CharacterActionMotion.Swing, new[] { fist, fist }, new[] { hands, hands });
            var view = new CharacterSpriteView(
                fist, new[] { fist, fist }, hands, new[] { hands, hands }, null, new Sprite[0], new CharacterMotionFrames[0], null, new[] { swordRun });
            Assert.That(view.GetRun(CharacterActionMotion.Swing), Is.SameAs(swordRun));
            Assert.That(view.GetRun(CharacterActionMotion.Shoot), Is.Null);

            var body = ScriptableObject.CreateInstance<CharacterSpriteSetDefinition>();
            created.Add(body);
            body.Configure(10f, view, view, view, view, view);
            Assert.That(body.FindAnchorProblems(), Is.Empty);

            var shortRun = new CharacterRunFrames(CharacterActionMotion.Swing, new[] { fist }, new[] { hands });
            var wrong = new CharacterSpriteView(
                fist, new[] { fist, fist }, hands, new[] { hands, hands }, null, new Sprite[0], new CharacterMotionFrames[0], null, new[] { shortRun });
            body.Configure(10f, wrong, wrong, wrong, wrong, wrong);
            Assert.That(body.FindAnchorProblems(), Is.Not.Empty, "武器種の走りは共通の脚に重ねるので、コマの数が揃っていなければなりません。");
        }

        /// <summary>
        /// 胴を持つ実際の部位は、片手剣の型の走りを全ての向きで持つ。剣は拳の両端へ分けて付ける。
        /// </summary>
        [Test]
        public void RealTorsos_HaveTheSwordTypesRun()
        {
            var catalog = LoadCatalog();
            foreach (var id in new[] { "knight", "leather" })
            {
                var torso = catalog.FindArmour(id);
                foreach (var facing in new[] { CharacterDirection8.South, CharacterDirection8.SouthEast, CharacterDirection8.East, CharacterDirection8.NorthEast, CharacterDirection8.North })
                {
                    var view = torso.GetView(facing);
                    var run = view.GetRun(CharacterActionMotion.Swing);
                    Assert.That(run, Is.Not.Null, $"{id} {facing}");
                    Assert.That(run.Frames.Length, Is.EqualTo(view.Run.Length), $"{id} {facing}");
                    Assert.That(run.Frames, Has.None.SameAs(view.Run[0]), $"{id} {facing}: 剣の腕を下ろした胴です。");
                    foreach (var anchors in run.Anchors)
                    {
                        Assert.That(anchors.MainHandFront, Is.GreaterThan(anchors.MainHandBack), $"{id} {facing}: 鍔は拳の刃の側の端です。");
                    }
                }
            }

            var sword = AssetDatabase.LoadAssetAtPath<CharacterGearDefinition>("Assets/Data/Gameplay/Gear/sword.asset");
            Assert.That(sword.IsSplitAtFist, Is.True, "剣は鍔から先と柄頭に分けて持ちます。");
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
        /// 全ての胴で全ての装備を持てることを確認する。
        /// </summary>
        [Test]
        public void RealEquipment_FitsEveryArmourSet()
        {
            var catalog = LoadCatalog();
            Assert.That(catalog.Armour, Is.Not.Empty);
            foreach (var body in catalog.Armour)
            {
                Assert.That(body.FindAnchorProblems(), Is.Empty, body.name);
                if (!body.Covers(CharacterArmourSlot.Torso))
                {
                    continue;
                }

                foreach (var equipment in catalog.Weapons)
                {
                    Assert.That(body.FindEquipmentProblems(equipment), Is.Empty, $"{body.name}と{equipment.name}");
                }
            }
        }

        /// <summary>
        /// idを持つ定義は全て一覧に載り、idのない敵の絵は載らない。
        /// </summary>
        [Test]
        public void RealCatalog_ListsEveryDefinitionWithAnId()
        {
            var catalog = LoadCatalog();
            Assert.That(catalog.FindIdProblems(), Is.Empty);
            foreach (var guid in AssetDatabase.FindAssets("t:CharacterSpriteSetDefinition"))
            {
                var body = AssetDatabase.LoadAssetAtPath<CharacterSpriteSetDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                var isListed = new List<CharacterSpriteSetDefinition>(catalog.Armour).Contains(body);
                Assert.That(isListed, Is.EqualTo(!string.IsNullOrEmpty(body.Id)), body.name);
            }

            foreach (var guid in AssetDatabase.FindAssets("t:CharacterEquipmentDefinition"))
            {
                var weapon = AssetDatabase.LoadAssetAtPath<CharacterEquipmentDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                Assert.That(weapon.Id, Is.Not.Empty, weapon.name);
                Assert.That(catalog.FindWeapon(weapon.Id), Is.SameAs(weapon));
            }
        }

        /// <summary>
        /// 一覧はidで定義を引き、部位の絵を持つ物だけをその部位へ並べる。
        /// </summary>
        [Test]
        public void Catalog_FindsByIdAndSlot()
        {
            var sprite = CreateSprite(64f);
            var headView = new CharacterSpriteView(null, new Sprite[0], default, new CharacterSpriteAnchors[0], null, new Sprite[0], new CharacterMotionFrames[0], sprite);
            var hat = CreateArmour("hat", headView);
            var sword = CreateWeapon("sword");
            var catalog = CreateCatalog(new[] { hat }, new[] { sword });

            Assert.That(catalog.FindArmour("hat"), Is.SameAs(hat));
            Assert.That(catalog.FindWeapon("sword"), Is.SameAs(sword));
            Assert.That(catalog.FindArmour("sword"), Is.Null);
            Assert.That(catalog.FindWeapon("hat"), Is.Null);
            Assert.That(catalog.GetArmour(CharacterArmourSlot.Head), Is.EqualTo(new[] { hat }));
            Assert.That(catalog.GetArmour(CharacterArmourSlot.Torso), Is.Empty);
        }

        /// <summary>
        /// 同じidの装備は、防具と武器の間でも問題として挙がる。
        /// </summary>
        [Test]
        public void FindIdProblems_ReportsTheSameIdTwice()
        {
            var view = new CharacterSpriteView(null, new Sprite[0], default, new CharacterSpriteAnchors[0], null, new Sprite[0], new CharacterMotionFrames[0]);
            var catalog = CreateCatalog(
                new[] { CreateArmour("same", view), CreateArmour("other", view) },
                new[] { CreateWeapon("same") });
            Assert.That(catalog.FindIdProblems(), Has.Count.EqualTo(1));
            Assert.That(CreateCatalog(new[] { CreateArmour("hat", view) }, new[] { CreateWeapon("sword") }).FindIdProblems(), Is.Empty);
        }

        /// <summary>
        /// 胴と足の走りは、どの部位も全ての向きで同じコマ数である。違うと、組み合わせたときに足のコマが欠ける。
        /// </summary>
        [Test]
        public void RealParts_ShareTheRunFrameCount()
        {
            var catalog = LoadCatalog();
            var counts = new SortedSet<int>();
            var facings = new[]
            {
                CharacterDirection8.South, CharacterDirection8.SouthEast, CharacterDirection8.East,
                CharacterDirection8.NorthEast, CharacterDirection8.North
            };
            foreach (var part in catalog.Armour)
            {
                foreach (var facing in facings)
                {
                    var view = part.GetView(facing);
                    if (part.Covers(CharacterArmourSlot.Torso))
                    {
                        counts.Add(view.Run.Length);
                    }

                    if (part.Covers(CharacterArmourSlot.Legs))
                    {
                        counts.Add(view.RunLegs.Length);
                    }
                }
            }

            Assert.That(counts, Has.Count.EqualTo(1), string.Join(", ", counts));
        }

        /// <summary>
        /// 武器種は通常攻撃の動きの型で決まり、一覧は武器種ごとに武器を返す。通常攻撃がなければ振る武器になる。
        /// </summary>
        [Test]
        public void Catalog_GroupsWeaponsByKind()
        {
            var bow = CreateWeapon("bow", CharacterActionMotion.Shoot);
            var sword = CreateWeapon("sword", CharacterActionMotion.Swing);
            var bare = CreateWeapon("bare");
            var catalog = CreateCatalog(new CharacterSpriteSetDefinition[0], new[] { bow, sword, bare });

            Assert.That(catalog.GetWeaponKinds(), Is.EqualTo(new[] { CharacterActionMotion.Swing, CharacterActionMotion.Shoot }));
            Assert.That(catalog.GetWeapons(CharacterActionMotion.Swing), Is.EqualTo(new[] { sword, bare }));
            Assert.That(catalog.GetWeapons(CharacterActionMotion.Shoot), Is.EqualTo(new[] { bow }));
        }

        private static CharacterEquipmentCatalog LoadCatalog()
        {
            return AssetDatabase.LoadAssetAtPath<CharacterEquipmentCatalog>(CatalogPath);
        }

        private CharacterSpriteSetDefinition CreateArmour(string id, CharacterSpriteView view)
        {
            var armour = ScriptableObject.CreateInstance<CharacterSpriteSetDefinition>();
            created.Add(armour);
            armour.SetId(id);
            armour.Configure(11f, view, view, view, view, view);
            return armour;
        }

        private CharacterEquipmentDefinition CreateWeapon(string id)
        {
            var weapon = ScriptableObject.CreateInstance<CharacterEquipmentDefinition>();
            created.Add(weapon);
            weapon.SetId(id);
            return weapon;
        }

        private CharacterEquipmentDefinition CreateWeapon(string id, CharacterActionMotion motion)
        {
            var attack = ScriptableObject.CreateInstance<CharacterActionDefinition>();
            created.Add(attack);
            var timing = new CharacterActionTiming(0.1f, 0.1f, 0.1f);
            if (motion == CharacterActionMotion.Shoot)
            {
                attack.ConfigureProjectile(id, 1, timing, 5f, 10f);
            }
            else
            {
                attack.ConfigureMelee(id, 1, timing, 1f, 120f);
            }

            var weapon = CreateWeapon(id);
            weapon.Configure(id, attack, null);
            return weapon;
        }

        private CharacterEquipmentCatalog CreateCatalog(CharacterSpriteSetDefinition[] armour, CharacterEquipmentDefinition[] weapons)
        {
            var catalog = ScriptableObject.CreateInstance<CharacterEquipmentCatalog>();
            created.Add(catalog);
            catalog.Configure(armour, weapons);
            return catalog;
        }

        /// <summary>
        /// 足の部位には、走りのコマが要る。
        /// </summary>
        [Test]
        public void FindAnchorProblems_ReportsMissingLegs()
        {
            var fist = CreateSprite(64f);
            var hands = new CharacterSpriteAnchors(Vector2.zero, Vector2.zero, Vector2.zero, 0f, fist, fist);
            var motion = new CharacterMotionFrames(CharacterActionMotion.Swing, new[] { fist }, new[] { hands }, new Sprite[1]);
            var view = new CharacterSpriteView(fist, new[] { fist }, hands, new[] { hands }, fist, new Sprite[0], new[] { motion });
            var body = ScriptableObject.CreateInstance<CharacterSpriteSetDefinition>();
            created.Add(body);
            body.Configure(11f, view, view, view, view, view);

            var problems = body.FindAnchorProblems();
            Assert.That(problems, Is.Not.Empty);
            Assert.That(problems[0], Does.Contain("足のコマ"));
        }

        /// <summary>
        /// 頭だけの定義Assetは、全ての向きの頭の絵があれば組み合わせに使える。
        /// </summary>
        [Test]
        public void FindAnchorProblems_ChecksOnlyTheSlotsAPartCovers()
        {
            var head = CreateSprite(64f);
            var view = new CharacterSpriteView(null, new Sprite[0], default, new CharacterSpriteAnchors[0], null, new Sprite[0], new CharacterMotionFrames[0], head);
            var body = ScriptableObject.CreateInstance<CharacterSpriteSetDefinition>();
            created.Add(body);
            body.Configure(11f, view, view, view, view, view);

            Assert.That(body.Covers(CharacterArmourSlot.Head), Is.True);
            Assert.That(body.Covers(CharacterArmourSlot.Torso), Is.False);
            Assert.That(body.Covers(CharacterArmourSlot.Legs), Is.False);
            Assert.That(body.FindAnchorProblems(), Is.Empty);

            var missing = new CharacterSpriteView(null, new Sprite[0]);
            body.Configure(11f, view, view, view, view, missing);
            var problems = body.FindAnchorProblems();
            Assert.That(problems.Count, Is.EqualTo(1));
            Assert.That(problems[0], Does.Contain("頭の絵"));
        }

        /// <summary>
        /// 実際の部位は、持っている部位の絵が揃っている。
        /// </summary>
        [Test]
        public void RealParts_CoverTheirSlots()
        {
            var catalog = LoadCatalog();
            var hero = catalog.FindArmour("knight");
            var leather = catalog.FindArmour("leather");
            var bare = catalog.FindArmour("bare");
            Assert.That(hero.Covers(CharacterArmourSlot.Head) && hero.Covers(CharacterArmourSlot.Torso) && hero.Covers(CharacterArmourSlot.Legs), Is.True);
            Assert.That(leather.Covers(CharacterArmourSlot.Head) && leather.Covers(CharacterArmourSlot.Torso), Is.True);
            Assert.That(leather.Covers(CharacterArmourSlot.Legs), Is.False);
            Assert.That(bare.Covers(CharacterArmourSlot.Head), Is.True);
            Assert.That(bare.Covers(CharacterArmourSlot.Torso), Is.False);
            foreach (var id in new[] { "sandals", "iron-boots" })
            {
                var legs = catalog.FindArmour(id);
                Assert.That(legs, Is.Not.Null, id);
                Assert.That(legs.Covers(CharacterArmourSlot.Legs), Is.True, id);
                Assert.That(legs.Covers(CharacterArmourSlot.Head) || legs.Covers(CharacterArmourSlot.Torso), Is.False, id);
                Assert.That(legs.GetView(CharacterDirection8.South).GetMotion(CharacterActionMotion.Swing).StandLegs, Has.Length.EqualTo(4), id);
            }
        }

        /// <summary>
        /// 止まって振る足のコマが欠けた部位は、組み合わせの検査で止まる。
        /// </summary>
        [Test]
        public void FindAnchorProblems_ReportsMissingStandLegs()
        {
            var fist = CreateSprite(64f);
            var hands = new CharacterSpriteAnchors(Vector2.zero, Vector2.zero, Vector2.zero, 0f, fist, fist);
            var motion = new CharacterMotionFrames(
                CharacterActionMotion.Swing, new[] { fist }, new[] { hands }, new Sprite[1], new Sprite[1], new Vector2[1]);
            var view = new CharacterSpriteView(fist, new[] { fist }, hands, new[] { hands }, fist, new[] { fist }, new[] { motion });
            var body = ScriptableObject.CreateInstance<CharacterSpriteSetDefinition>();
            created.Add(body);
            body.Configure(11f, view, view, view, view, view);

            var problems = body.FindAnchorProblems();
            Assert.That(problems, Is.Not.Empty);
            Assert.That(problems[0], Does.Contain("止まって振る脚"));
        }

        /// <summary>
        /// 実際の騎士は、全ての向きで止まって振るときの脚を持つ。
        /// </summary>
        [Test]
        public void RealHero_HasStandLegsForTheSwing()
        {
            var body = AssetDatabase.LoadAssetAtPath<CharacterSpriteSetDefinition>(HeroSpritesPath);
            foreach (CharacterDirection8 direction in System.Enum.GetValues(typeof(CharacterDirection8)))
            {
                var motion = body.GetView(direction).GetMotion(CharacterActionMotion.Swing);
                Assert.That(motion.StandLegs.Length, Is.EqualTo(motion.Frames.Length), direction.ToString());
            }
        }

        /// <summary>
        /// 実際の片手剣は、全ての段が半円の攻撃である。
        /// </summary>
        [Test]
        public void RealSword_StrikesAHalfCircleOnEveryStep()
        {
            var equipment = AssetDatabase.LoadAssetAtPath<CharacterEquipmentDefinition>("Assets/Data/Gameplay/Equipment/SwordEquipment.asset");
            var count = 0;
            for (var spec = equipment.PrimaryAction.CreateSpec(); spec != null; spec = spec.Next)
            {
                Assert.That(spec.ArcDegrees, Is.EqualTo(180f), spec.Id);
                count++;
            }

            Assert.That(count, Is.EqualTo(3));
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

        /// <summary>
        /// 連撃の次の段も実行時のActionへ渡る。輪になった設定は例外で止まる。
        /// </summary>
        [Test]
        public void CreateSpec_FollowsTheComboAndStopsOnALoop()
        {
            var first = CreateMelee();
            var second = CreateMelee();
            first.SetNextInCombo(second);
            Assert.That(first.CreateSpec().Next, Is.Not.Null);
            Assert.That(first.CreateSpec().Next.Next, Is.Null);

            second.SetNextInCombo(first);
            Assert.Throws<System.InvalidOperationException>(() => first.CreateSpec());
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
