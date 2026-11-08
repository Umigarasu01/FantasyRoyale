using System.Collections;
using System.Collections.Generic;
using System.IO;
using FantasyRoyale.Gameplay.Characters;
using FantasyRoyale.Gameplay.Characters.Unity;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace FantasyRoyale.Playtest.Tests.PlayMode
{
    /// <summary>
    /// 向きと移動状態に応じたSpriteの切り替えと、森マップの走行とCameraを確認する。
    /// </summary>
    public sealed class ForestMapPreviewPlayModeTests
    {
        private const string ScenePath = "Assets/ForestMapPreview/ForestMapPreviewDebug.unity";

        private static readonly Vector2 EastRiverBank = new Vector2(100f, 68.65f);

        private ForestMapPreviewDebug preview;
        private Texture2D texture;
        private CharacterSpriteSetDefinition sprites;
        private GameObject character;

        [TearDown]
        public void TearDown()
        {
            Time.captureDeltaTime = 0f;
            Object.Destroy(character);
            Object.Destroy(sprites);
            Object.Destroy(texture);
        }

        /// <summary>
        /// 走る向きに応じて5方向のコマを使い、左寄りの向きは右寄りの反転になることを確認する。
        /// </summary>
        [UnityTest]
        public IEnumerator Animator_UsesFramesForFacing()
        {
            var actor = CreateCharacter(out var renderer, out var animator);

            actor.SetMoveCommand(new CharacterMoveCommand(1f, 0f));
            yield return null;
            Assert.That(animator.IsRunning, Is.True);
            Assert.That(Frames(CharacterDirection8.East), Does.Contain(renderer.sprite));
            Assert.That(renderer.flipX, Is.False);

            actor.SetMoveCommand(new CharacterMoveCommand(-1f, -1f));
            yield return null;
            Assert.That(Frames(CharacterDirection8.SouthEast), Does.Contain(renderer.sprite));
            Assert.That(renderer.flipX, Is.True, "左斜め前は右斜め前の反転で表します。");

            actor.SetMoveCommand(new CharacterMoveCommand(1f, 1f));
            yield return null;
            Assert.That(Frames(CharacterDirection8.NorthEast), Does.Contain(renderer.sprite));
            Assert.That(renderer.flipX, Is.False);

            actor.SetMoveCommand(new CharacterMoveCommand(0f, -1f));
            yield return null;
            Assert.That(Frames(CharacterDirection8.South), Does.Contain(renderer.sprite));

            actor.SetMoveCommand(new CharacterMoveCommand(0f, 1f));
            yield return null;
            Assert.That(Frames(CharacterDirection8.North), Does.Contain(renderer.sprite));
            Assert.That(renderer.flipX, Is.False);
        }

        /// <summary>
        /// 斜めの2つのキーが少しずれて離れても斜めの立ち姿で止まり、止まったまま斜めへ振れる向きが残る。
        /// 片方のキーを押し続ければ隣の向きへ変わることを確認する。
        /// </summary>
        [UnityTest]
        public IEnumerator Animator_KeepsDiagonalWhenKeysReleaseApart()
        {
            var actor = CreateCharacter(out var renderer, out _);
            Time.captureDeltaTime = 1f / 60f;

            actor.SetMoveCommand(new CharacterMoveCommand(1f, 1f));
            yield return null;
            actor.SetMoveCommand(new CharacterMoveCommand(1f, 0f));
            yield return null;
            actor.SetMoveCommand(CharacterMoveCommand.None);
            yield return null;
            Assert.That(renderer.sprite, Is.SameAs(View(CharacterDirection8.NorthEast).Idle));
            Assert.That(actor.Facing, Is.EqualTo(CharacterDirection8.NorthEast), "止まったまま斜めへ振れます。");

            actor.SetMoveCommand(new CharacterMoveCommand(1f, 1f));
            yield return null;
            actor.SetMoveCommand(new CharacterMoveCommand(1f, 0f));
            for (var frame = 0; frame < 8; frame++)
            {
                yield return null;
            }

            Assert.That(Frames(CharacterDirection8.East), Does.Contain(renderer.sprite));
            Assert.That(actor.Facing, Is.EqualTo(CharacterDirection8.East));
        }

        /// <summary>
        /// 部品は体のコマごとの手の位置へ付いて動き、左向きでは反転し、背中の物は前向きで体の後ろへ回る。
        /// </summary>
        [UnityTest]
        public IEnumerator Animator_GearFollowsAnchorsOfEachFrame()
        {
            var actor = CreateCharacter(out var renderer, out var animator);
            Sprite Part() => Sprite.Create(texture, new Rect(0f, 0f, 4f, 4f), Vector2.zero);
            var hand = ScriptableObject.CreateInstance<CharacterGearDefinition>();
            hand.Configure(CharacterGearSlot.MainHand, Part(), Part(), Part(), Part(), Part());
            var back = ScriptableObject.CreateInstance<CharacterGearDefinition>();
            back.Configure(CharacterGearSlot.Back, Part(), Part(), Part(), Part(), Part());
            CharacterSpriteView AnchoredView()
            {
                var run = new[] { Part(), Part(), Part(), Part() };
                var anchors = new CharacterSpriteAnchors[4];
                for (var frame = 0; frame < 4; frame++)
                {
                    anchors[frame] = new CharacterSpriteAnchors(new Vector2(1f + frame, 2f), Vector2.zero, Vector2.up);
                }

                return new CharacterSpriteView(Part(), run, new CharacterSpriteAnchors(Vector2.right * 9f, Vector2.zero, Vector2.up), anchors);
            }

            sprites.Configure(30f, AnchoredView(), AnchoredView(), AnchoredView(), AnchoredView(), AnchoredView());
            animator.Initialize(sprites);
            animator.SetGear(new[] { hand, back });

            Assert.That(animator.GetGearPosition(CharacterGearSlot.MainHand).x, Is.EqualTo(9f), "立ち姿の手に付きます。");
            Assert.That(animator.IsGearBehindBody(CharacterGearSlot.Back), Is.True, "前向きでは背中の物は後ろです。");

            actor.SetMoveCommand(new CharacterMoveCommand(1f, 0f));
            var seen = new HashSet<float>();
            var endAt = Time.time + 1f;
            while (seen.Count < 2 && Time.time < endAt)
            {
                yield return null;
                seen.Add(animator.GetGearPosition(CharacterGearSlot.MainHand).x);
            }

            Assert.That(seen.Count, Is.GreaterThanOrEqualTo(2), "コマに合わせて部品も動きます。");

            actor.SetMoveCommand(new CharacterMoveCommand(-1f, 0f));
            yield return null;
            Assert.That(animator.GetGearPosition(CharacterGearSlot.MainHand).x, Is.LessThan(0f), "左向きでは反転します。");

            actor.SetMoveCommand(new CharacterMoveCommand(0f, 1f));
            yield return null;
            Assert.That(animator.IsGearBehindBody(CharacterGearSlot.Back), Is.False, "背を向けると背中の物が見えます。");

            Object.Destroy(hand);
            Object.Destroy(back);
        }

        /// <summary>
        /// 走っている間はコマが順に進み、止まると向きに合う待機Spriteへ戻ることを確認する。
        /// </summary>
        [UnityTest]
        public IEnumerator Animator_AdvancesFramesAndReturnsToIdle()
        {
            var actor = CreateCharacter(out var renderer, out var animator);
            actor.SetMoveCommand(new CharacterMoveCommand(1f, 0f));

            var first = renderer.sprite;
            var endAt = Time.time + 1f;
            while (renderer.sprite == first && Time.time < endAt)
            {
                yield return null;
            }

            Assert.That(renderer.sprite, Is.Not.SameAs(first), "走っている間はコマが進みます。");

            actor.SetMoveCommand(CharacterMoveCommand.None);
            yield return null;
            Assert.That(animator.IsRunning, Is.False);
            Assert.That(renderer.sprite, Is.SameAs(View(CharacterDirection8.East).Idle), "止まっても最後の向きを保ちます。");
        }

        /// <summary>
        /// 移動できない間と倒れた後は、入力があっても走りのコマを使わないことを確認する。
        /// </summary>
        [UnityTest]
        public IEnumerator Animator_DoesNotRunWhenUnableToMove()
        {
            var actor = CreateCharacter(out var renderer, out var animator);
            actor.SetMoveCommand(new CharacterMoveCommand(0f, 1f));
            actor.SetMovementScale(0f);
            yield return null;
            Assert.That(animator.IsRunning, Is.False);
            Assert.That(renderer.sprite, Is.SameAs(View(CharacterDirection8.North).Idle));

            actor.SetMovementScale(1f);
            actor.Health.ApplyDamage(actor.Health.MaximumHealth);
            actor.SetMoveCommand(new CharacterMoveCommand(0f, 1f));
            yield return null;
            Assert.That(animator.IsRunning, Is.False);
        }

        /// <summary>
        /// 森マップを実際に走り、本番の移動速度とCameraの追従、Map端の制限を確認する。
        /// Map出力はGitへ入れないため、Sceneがない環境では対象外にする。
        /// </summary>
        [UnityTest]
        public IEnumerator ForestMap_RunsAndCameraFollows()
        {
            yield return LoadForest();

            // 描画順はLateUpdateで決まるので、1Frame待ってから基準を読む。
            yield return null;

            var camera = preview.GameplayCamera;
            Assert.That(camera.orthographicSize * 2f, Is.EqualTo(14f).Within(0.001f), "通常Cameraは縦14.0です。");
            var start = (Vector2)preview.PlayerObject.transform.position;
            var startOrder = preview.PlayerSortingOrder;
            var playerRenderer = preview.PlayerObject.GetComponent<SpriteRenderer>();
            Assert.That(playerRenderer.sprite.name, Is.EqualTo("hero-legs-idle-down"), "止まっている間は走りのコマを使いません。");

            preview.SetMoveInputOverrideForTests(Vector2.right);
            var endAt = Time.time + 1f;
            while (Time.time < endAt)
            {
                yield return new WaitForFixedUpdate();
            }

            var position = (Vector2)preview.PlayerObject.transform.position;
            Assert.That(position.x - start.x, Is.EqualTo(preview.PlayerActor.MovementSpeed).Within(0.6f));
            Assert.That(preview.PlayerAnimator.IsRunning, Is.True);
            Assert.That(
                Mathf.Abs(camera.transform.position.x - position.x),
                Is.LessThan(0.2f),
                "Cameraは騎士へ遅れなく追従します。");

            preview.SetMoveInputOverrideForTests(Vector2.down);
            endAt = Time.time + 0.3f;
            while (Time.time < endAt)
            {
                yield return new WaitForFixedUpdate();
            }

            yield return null;
            Assert.That(preview.PlayerSortingOrder, Is.GreaterThan(startOrder), "手前へ進むと描画順も手前になります。");

            // 川の東岸から西へ走っても、水の判定で止まる。
            var riverHit = Physics2D.Raycast(EastRiverBank, Vector2.left, 20f, LayerMask.GetMask("MapCollision"));
            Assert.That(riverHit.collider, Is.Not.Null, "東岸の西に川の判定があります。");
            preview.PlayerActor.Body.position = riverHit.point + Vector2.right * 1.5f;
            preview.SetMoveInputOverrideForTests(Vector2.left);
            endAt = Time.time + 1f;
            while (Time.time < endAt)
            {
                yield return new WaitForFixedUpdate();
            }

            Assert.That(preview.PlayerActor.Body.position.x, Is.GreaterThan(riverHit.point.x), "川は走って渡れません。");

            // Mapの隅へ置いても、Cameraは外側を映さない。
            preview.SetMoveInputOverrideForTests(Vector2.zero);
            preview.PlayerActor.Body.position = new Vector2(preview.MapBounds.xMin + 0.5f, preview.MapBounds.yMin + 0.5f);
            endAt = Time.time + 1f;
            while (Time.time < endAt)
            {
                yield return null;
            }

            var halfHeight = camera.orthographicSize;
            var halfWidth = halfHeight * camera.aspect;
            Assert.That(camera.transform.position.x - halfWidth, Is.GreaterThanOrEqualTo(preview.MapBounds.xMin - 0.02f));
            Assert.That(camera.transform.position.y - halfHeight, Is.GreaterThanOrEqualTo(preview.MapBounds.yMin - 0.02f));
        }

        /// <summary>
        /// 敵は近づくと追ってきて、触れるとHPが減り、倒れた騎士は村へ戻って全回復する。
        /// </summary>
        [UnityTest]
        public IEnumerator ForestMap_EnemyHurtsPlayerAndPlayerReturnsToVillage()
        {
            yield return LoadForest();
            var enemy = preview.GetEnemyActor(0);
            preview.PlayerActor.Body.position = enemy.Body.position + Vector2.right * 0.7f;

            var timeoutAt = Time.time + 4f;
            while (preview.PlayerHealth.CurrentHealth == preview.PlayerHealth.MaximumHealth && Time.time < timeoutAt)
            {
                yield return null;
            }

            Assert.That(preview.PlayerHealth.CurrentHealth, Is.LessThan(preview.PlayerHealth.MaximumHealth), "敵に触れるとHPが減ります。");

            preview.PlayerHealth.ApplyDamage(preview.PlayerHealth.CurrentHealth);
            timeoutAt = Time.time + 4f;
            while (!preview.PlayerHealth.IsAlive && Time.time < timeoutAt)
            {
                yield return null;
            }

            yield return new WaitForFixedUpdate();
            Assert.That(preview.PlayerHealth.CurrentHealth, Is.EqualTo(preview.PlayerHealth.MaximumHealth));
            Assert.That(Vector2.Distance(preview.PlayerActor.Body.position, preview.SpawnPosition), Is.LessThan(0.5f), "村へ戻ります。");
        }

        /// <summary>
        /// 剣で敵を倒すとコインが増え、敵は消える。
        /// </summary>
        [UnityTest]
        public IEnumerator ForestMap_SwordDefeatsEnemyForCoin()
        {
            yield return LoadForest();
            var enemy = preview.GetEnemyActor(0);
            var command = new CharacterActionCommand(CharacterActionSlot.Primary, CharacterDirection8.West);
            var timeoutAt = Time.time + 6f;
            while (preview.Coins == 0 && Time.time < timeoutAt)
            {
                preview.PlayerActor.Body.position = enemy.Body.position + Vector2.right * 0.9f;
                preview.PlayerCombat.TryStartAction(command);
                yield return new WaitForFixedUpdate();
            }

            Assert.That(preview.Coins, Is.EqualTo(1));
            Assert.That(enemy.gameObject.activeSelf, Is.False, "倒した敵は消えます。");
        }

        /// <summary>
        /// 宝箱からコインが手に入り、開けた宝箱と遠い宝箱は開かない。
        /// </summary>
        [UnityTest]
        public IEnumerator ForestMap_ChestGivesCoins()
        {
            yield return LoadForest();
            Assert.That(preview.TryOpenNearestChest(), Is.False, "遠い宝箱は開きません。");

            yield return MoveNextTo(preview.GetChestPosition(0));
            Assert.That(preview.TryOpenNearestChest(), Is.True);
            Assert.That(preview.Coins, Is.EqualTo(10));
            Assert.That(preview.TryOpenNearestChest(), Is.False, "開けた宝箱は開きません。");
        }

        /// <summary>
        /// 屋台で弓を買うと手の部品が替わり、体のアニメーションはそのまま使う。コインが足りなければ買えない。
        /// </summary>
        [UnityTest]
        public IEnumerator ForestMap_ShopSellsBowAndChangesLook()
        {
            yield return LoadForest();
            yield return null;
            var playerRenderer = preview.PlayerObject.GetComponent<SpriteRenderer>();
            var body = playerRenderer.sprite;
            var animator = preview.PlayerAnimator;
            Assert.That(animator.GetGearSprite(CharacterGearSlot.MainHand), Is.Not.Null, "剣を持っています。");
            Assert.That(animator.GetGearSprite(CharacterGearSlot.OffHand), Is.Not.Null, "盾を持っています。");
            Assert.That(animator.GetGearSprite(CharacterGearSlot.Back), Is.Null);
            var shield = animator.GetGearSprite(CharacterGearSlot.OffHand);
            Assert.That(animator.GetHandOverSprite(CharacterGearSlot.MainHand), Is.Null, "剣は胴の拳の両端へ付くので、拳を重ねません。");
            Assert.That(animator.GetGearBackSprite(CharacterGearSlot.MainHand), Is.Not.Null, "柄頭の側も付きます。");
            Assert.That(animator.GetHandOverSprite(CharacterGearSlot.OffHand), Is.Null, "盾は拳を隠します。");
            Assert.That(preview.CurrentEquipmentName, Is.EqualTo("剣"));
            Assert.That(preview.TryOpenShop(), Is.False, "屋台から遠いと開きません。");

            preview.PlayerActor.Body.position = preview.GetShopPosition(0);
            preview.PlayerObject.transform.position = preview.GetShopPosition(0);
            yield return new WaitForFixedUpdate();
            Assert.That(preview.TryOpenShop(), Is.True);
            Assert.That(preview.TryBuyBow(), Is.False, "コインが足りなければ買えません。");

            preview.AddCoinsForTests(25);
            Assert.That(preview.TryBuyBow(), Is.True);
            Assert.That(preview.Coins, Is.EqualTo(5));
            Assert.That(preview.CurrentEquipmentName, Is.EqualTo("弓"));
            Assert.That(preview.PlayerCombat.Loadout.Get(CharacterActionSlot.Primary).Id, Is.EqualTo("bow-shot"));
            yield return null;
            Assert.That(playerRenderer.sprite, Is.SameAs(body), "体の絵は替わりません。");
            Assert.That(animator.GetGearSprite(CharacterGearSlot.MainHand), Is.Null, "剣はしまいます。");
            Assert.That(animator.GetGearBackSprite(CharacterGearSlot.MainHand), Is.Null, "柄頭もしまいます。");
            Assert.That(animator.GetGearSprite(CharacterGearSlot.OffHand), Is.Not.SameAs(shield), "盾が弓に替わります。");
            Assert.That(animator.GetGearSprite(CharacterGearSlot.Back), Is.Not.Null, "矢筒を背負います。");
            Assert.That(animator.GetHandOverSprite(CharacterGearSlot.MainHand), Is.Null);
            Assert.That(animator.GetHandOverSprite(CharacterGearSlot.OffHand), Is.Not.Null, "弓は拳で握ります。");
            Assert.That(preview.TryBuyBow(), Is.False, "同じ物は買えません。");
        }

        /// <summary>
        /// 剣を振ると上半身のコマが重なり、剣はコマごとの角度で腕と一緒に動き、盾は動かず、終わると元の絵へ戻る。
        /// </summary>
        [UnityTest]
        public IEnumerator ForestMap_SwordSwingsThroughAttackPose()
        {
            yield return LoadForest();
            yield return null;
            var playerRenderer = preview.PlayerObject.GetComponent<SpriteRenderer>();
            var animator = preview.PlayerAnimator;
            var idle = playerRenderer.sprite;
            var idleTorso = animator.GetUpperSprite();
            var command = new CharacterActionCommand(CharacterActionSlot.Primary, CharacterDirection8.East);
            Assert.That(preview.PlayerCombat.TryStartAction(command), Is.True);

            var bodies = new HashSet<Sprite>();
            var swords = new HashSet<Sprite>();
            var shields = new HashSet<Vector2>();
            var smears = 0;
            var timeoutAt = Time.time + 2f;
            while (preview.PlayerCombat.ActionState.IsBusy && Time.time < timeoutAt)
            {
                yield return null;
                if (preview.PlayerCombat.ActionState.IsBusy)
                {
                    bodies.Add(animator.GetUpperSprite());
                    swords.Add(animator.GetGearSprite(CharacterGearSlot.MainHand));
                    var shield = animator.GetGearPosition(CharacterGearSlot.OffHand) - animator.GetUpperPosition();
                    shields.Add(new Vector2(Mathf.Round(shield.x * 1000f), Mathf.Round(shield.y * 1000f)));
                    smears += animator.GetSmearSprite() != null ? 1 : 0;
                }
            }

            Assert.That(bodies.Contains(null), Is.False, "攻撃中は上半身が重なります。");
            Assert.That(bodies.Count, Is.GreaterThanOrEqualTo(3), "上半身のコマが進みます。");
            Assert.That(swords.Count, Is.GreaterThanOrEqualTo(3), "剣はコマごとに角度が変わります。");
            Assert.That(shields.Count, Is.EqualTo(1), "振る間、盾は上半身に対して動きません。");
            Assert.That(smears, Is.GreaterThan(0), "速い間は残像が出ます。");
            yield return null;
            Assert.That(playerRenderer.sprite, Is.SameAs(idle), "終わると通常の絵へ戻ります。");
            Assert.That(animator.GetUpperSprite(), Is.SameAs(idleTorso), "胴も通常の絵へ戻ります。");
        }

        /// <summary>
        /// 森の騎士は足、胴、頭の3層で表示する。
        /// </summary>
        [UnityTest]
        public IEnumerator ForestMap_HeroIsDrawnInThreeLayers()
        {
            yield return LoadForest();
            yield return null;
            var animator = preview.PlayerAnimator;
            var playerRenderer = preview.PlayerObject.GetComponent<SpriteRenderer>();
            Assert.That(playerRenderer.sprite.name, Does.StartWith("hero-legs"));
            Assert.That(animator.GetUpperSprite().name, Does.StartWith("hero-torso"));
            Assert.That(animator.GetHeadSprite().name, Does.StartWith("hero-head"));
        }

        /// <summary>
        /// 跳ぶと絵だけが持ち上がって足元に影が出て、足元の位置は動かない。空中では跳べず、着地すると元へ戻る。
        /// </summary>
        [UnityTest]
        public IEnumerator ForestMap_JumpLiftsTheLookButNotTheBody()
        {
            yield return LoadForest();
            yield return null;
            var actor = preview.PlayerActor;
            var animator = preview.PlayerAnimator;
            var playerRenderer = preview.PlayerObject.GetComponent<SpriteRenderer>();
            var start = actor.Body.position;
            Assert.That(actor.TryJump(), Is.True);
            var highest = 0f;
            var smallestShadow = 1f;
            var timeoutAt = Time.time + 2f;
            while (actor.Jump.IsAirborne && Time.time < timeoutAt)
            {
                yield return null;
                if (actor.Jump.IsAirborne)
                {
                    Assert.That(actor.TryJump(), Is.False, "空中では跳べません。");
                }

                if (animator.GetLiftHeight() > 0f)
                {
                    Assert.That(playerRenderer.enabled, Is.False, "足元の足の絵は隠れ、持ち上げた層で見せます。");
                    Assert.That(animator.IsShadowShown(), Is.True);
                    highest = Mathf.Max(highest, animator.GetLiftHeight());
                    smallestShadow = Mathf.Min(smallestShadow, animator.GetShadowScale());
                }
            }

            Assert.That(actor.Jump.IsAirborne, Is.False, "時間が過ぎると着地します。");
            Assert.That(highest, Is.GreaterThan(0.3f));
            Assert.That(smallestShadow, Is.LessThan(0.9f), "高く跳ぶほど影が小さくなります。");
            Assert.That(Vector2.Distance(actor.Body.position, start), Is.LessThan(0.01f), "足元は動きません。");
            yield return null;
            Assert.That(animator.GetLiftHeight(), Is.EqualTo(0f));
            Assert.That(animator.IsShadowShown(), Is.True, "地上でも足元に影があります。");
            Assert.That(animator.GetShadowScale(), Is.EqualTo(1f));
            Assert.That(animator.GetDustCount(), Is.GreaterThan(0), "着地で砂ぼこりが出ます。");
            Assert.That(playerRenderer.enabled, Is.True);
        }

        /// <summary>
        /// 走ると足が着くたびに足元へ砂ぼこりが出て、止まるとやがて消える。影は走っても止まっても足元にある。
        /// </summary>
        [UnityTest]
        public IEnumerator ForestMap_RunningKicksUpDustUnderAShadow()
        {
            yield return LoadForest();
            yield return null;
            var animator = preview.PlayerAnimator;
            Assert.That(animator.IsShadowShown(), Is.True);
            preview.SetMoveInputOverrideForTests(Vector2.right);
            var mostDust = 0;
            var runUntil = Time.time + 0.8f;
            while (Time.time < runUntil)
            {
                yield return null;
                mostDust = Mathf.Max(mostDust, animator.GetDustCount());
                Assert.That(animator.IsShadowShown(), Is.True);
            }

            preview.SetMoveInputOverrideForTests(Vector2.zero);
            Assert.That(mostDust, Is.GreaterThan(0), "足が着くたびに砂ぼこりが出ます。");
            var settleUntil = Time.time + 1f;
            while (animator.GetDustCount() > 0 && Time.time < settleUntil)
            {
                yield return null;
            }

            Assert.That(animator.GetDustCount(), Is.EqualTo(0), "止まると砂ぼこりは消えます。");
            Assert.That(animator.IsShadowShown(), Is.True);
        }

        /// <summary>
        /// 止まって振ると、脚が踏み込み、上半身が向いた方向へ乗り出す。
        /// </summary>
        [UnityTest]
        public IEnumerator ForestMap_StandingSwingLungesForward()
        {
            yield return LoadForest();
            yield return null;
            var combat = preview.PlayerCombat;
            var animator = preview.PlayerAnimator;
            var playerRenderer = preview.PlayerObject.GetComponent<SpriteRenderer>();
            var turns = new[]
            {
                (CharacterDirection8.East, Vector2.right),
                (CharacterDirection8.West, Vector2.left),
                (CharacterDirection8.South, Vector2.down)
            };
            foreach (var (direction, forward) in turns)
            {
                Assert.That(combat.TryStartAction(new CharacterActionCommand(CharacterActionSlot.Primary, direction)), Is.True);
                var legs = new HashSet<Sprite>();
                var nearest = float.MaxValue;
                var farthest = float.MinValue;
                var timeoutAt = Time.time + 2f;
                while (combat.ActionState.IsBusy && Time.time < timeoutAt)
                {
                    yield return null;
                    if (combat.ActionState.IsBusy)
                    {
                        var reach = Vector2.Dot(animator.GetUpperPosition(), forward);
                        nearest = Mathf.Min(nearest, reach);
                        farthest = Mathf.Max(farthest, reach);
                        legs.Add(playerRenderer.sprite);
                    }
                }

                Assert.That(legs.Count, Is.GreaterThanOrEqualTo(3), $"{direction}: 脚のコマが進みます。");
                Assert.That(farthest - nearest, Is.GreaterThan(0.1f), $"{direction}: 上半身が前へ乗り出します。");
            }
        }

        /// <summary>
        /// 走りながら振っても、脚は走りのコマを進め続ける。
        /// </summary>
        [UnityTest]
        public IEnumerator ForestMap_LegsKeepRunningWhileAttacking()
        {
            yield return LoadForest();
            yield return null;
            var combat = preview.PlayerCombat;
            var playerRenderer = preview.PlayerObject.GetComponent<SpriteRenderer>();
            preview.SetMoveInputOverrideForTests(Vector2.right);
            yield return new WaitForSeconds(0.2f);

            var command = new CharacterActionCommand(CharacterActionSlot.Primary, CharacterDirection8.East);
            Assert.That(combat.TryStartAction(command), Is.True);
            var legs = new HashSet<Sprite>();
            var timeoutAt = Time.time + 2f;
            while (combat.ActionState.IsBusy && Time.time < timeoutAt)
            {
                yield return null;
                if (combat.ActionState.IsBusy)
                {
                    legs.Add(playerRenderer.sprite);
                }
            }

            preview.SetMoveInputOverrideForTests(Vector2.zero);
            Assert.That(legs.Count, Is.GreaterThanOrEqualTo(3), "振っている間も脚のコマが進みます。");
        }

        /// <summary>
        /// 斜めに走ってキーを少しずらして離しても、止まったまま斜めへ振れる。
        /// </summary>
        [UnityTest]
        public IEnumerator ForestMap_StandingSwingKeepsTheDiagonal()
        {
            yield return LoadForest();
            yield return null;
            preview.SetMoveInputOverrideForTests(new Vector2(1f, 1f));
            yield return new WaitForSeconds(0.2f);
            preview.SetMoveInputOverrideForTests(Vector2.right);
            yield return null;
            preview.SetMoveInputOverrideForTests(Vector2.zero);
            yield return new WaitForSeconds(0.2f);
            Assert.That(preview.PlayerActor.Facing, Is.EqualTo(CharacterDirection8.NorthEast));

            var command = new CharacterActionCommand(CharacterActionSlot.Primary, preview.PlayerActor.Facing);
            Assert.That(preview.PlayerCombat.TryStartAction(command), Is.True);
            yield return null;
            Assert.That(preview.PlayerCombat.ActionState.Direction, Is.EqualTo(CharacterDirection8.NorthEast));
            Assert.That(preview.PlayerAnimator.GetUpperSprite().name, Does.StartWith("hero-torso-swing-upside"), "斜め奥へ振ります。");
        }

        /// <summary>
        /// 剣を持って走ると片手剣の型の胴になり、弓では共通の胴になる。剣は柄頭が胴の下、鍔から先が胴の上である。
        /// 背を向けて走ると鍔から先も胴の下へ回り、背を向けて振ると胴と頭の間を通る。
        /// </summary>
        [UnityTest]
        public IEnumerator ForestMap_SwordRunHoldsTheSwordAtTheFist()
        {
            yield return LoadForest();
            yield return null;
            var animator = preview.PlayerAnimator;
            Assert.That(preview.TryEquipWeapon("sword"), Is.True);
            preview.SetMoveInputOverrideForTests(Vector2.right);
            yield return new WaitForSeconds(0.2f);
            Assert.That(animator.GetUpperSprite().name, Does.StartWith("hero-torso-sword-side"), "剣の腕を下ろした胴で走ります。");
            var (upper, head) = animator.GetBodyOrders();
            Assert.That(animator.GetGearOrder(CharacterGearSlot.MainHand), Is.GreaterThan(head), "鍔から先は胴の上です。");
            Assert.That(animator.GetGearBackSprite(CharacterGearSlot.MainHand), Is.Not.Null);

            preview.SetMoveInputOverrideForTests(Vector2.up);
            yield return new WaitForSeconds(0.2f);
            Assert.That(animator.GetUpperSprite().name, Does.StartWith("hero-torso-sword-up"));
            Assert.That(animator.GetGearOrder(CharacterGearSlot.MainHand), Is.InRange(1, upper - 1), "背を向けて走ると、脚の上、胴の下です。");

            var command = new CharacterActionCommand(CharacterActionSlot.Primary, CharacterDirection8.North);
            Assert.That(preview.PlayerCombat.TryStartAction(command), Is.True);
            yield return null;
            Assert.That(animator.GetGearOrder(CharacterGearSlot.MainHand), Is.InRange(upper + 1, head - 1), "背を向けて振ると、胴と頭の間を通ります。");
            var timeoutAt = Time.time + 2f;
            while (preview.PlayerCombat.ActionState.IsBusy && Time.time < timeoutAt)
            {
                yield return null;
            }

            Assert.That(preview.TryEquipWeapon("bow"), Is.True);
            preview.SetMoveInputOverrideForTests(Vector2.right);
            yield return new WaitForSeconds(0.2f);
            preview.SetMoveInputOverrideForTests(Vector2.zero);
            Assert.That(animator.GetUpperSprite().name, Does.StartWith("hero-torso-side"), "弓では共通の走りの胴です。");
        }

        /// <summary>
        /// 攻撃のボタンを続けて押すと片手剣が三段まで続き、走りながらでも止まらない。
        /// </summary>
        [UnityTest]
        public IEnumerator ForestMap_SwordCombosThroughThreeStepsWhileRunning()
        {
            yield return LoadForest();
            yield return null;
            var combat = preview.PlayerCombat;
            var command = new CharacterActionCommand(CharacterActionSlot.Primary, CharacterDirection8.East);
            var start = preview.PlayerActor.Body.position;
            preview.SetMoveInputOverrideForTests(Vector2.right);
            yield return null;
            Assert.That(combat.TryStartAction(command), Is.True);

            var steps = new List<string>();
            var timeoutAt = Time.time + 4f;
            while (combat.ActionState.IsBusy && Time.time < timeoutAt)
            {
                var id = combat.ActionState.CurrentSpec.Id;
                if (!steps.Contains(id))
                {
                    steps.Add(id);
                }

                combat.TryStartAction(command);
                yield return new WaitForFixedUpdate();
            }

            preview.SetMoveInputOverrideForTests(Vector2.zero);
            Assert.That(steps, Is.EqualTo(new[] { "sword-slash", "sword-slash-2", "sword-slash-3" }));
            Assert.That(preview.PlayerActor.Body.position.x - start.x, Is.GreaterThan(1f), "走ったまま振れます。");
        }

        /// <summary>
        /// 部位を着替えると、その部位の絵だけが替わり、武器は同じ手の位置に残り、攻撃もその部位の絵で動く。
        /// </summary>
        [UnityTest]
        public IEnumerator ForestMap_ChangingPartsKeepsTheWeapon()
        {
            yield return LoadForest();
            yield return null;
            var playerRenderer = preview.PlayerObject.GetComponent<SpriteRenderer>();
            var animator = preview.PlayerAnimator;
            var legs = playerRenderer.sprite;
            var knightTorso = animator.GetUpperSprite();
            var knightHead = animator.GetHeadSprite();
            var sword = animator.GetGearSprite(CharacterGearSlot.MainHand);
            var hand = animator.GetGearPosition(CharacterGearSlot.MainHand);
            Assert.That(preview.GetArmourName(CharacterArmourSlot.Torso), Is.EqualTo("騎士"));

            Assert.That(preview.TryEquipArmour(CharacterArmourSlot.Torso, "leather"), Is.True);
            yield return null;
            Assert.That(preview.GetArmourName(CharacterArmourSlot.Torso), Is.EqualTo("革"));
            Assert.That(animator.GetUpperSprite(), Is.Not.SameAs(knightTorso), "胴の絵が替わります。");
            Assert.That(animator.GetHeadSprite(), Is.SameAs(knightHead), "頭はそのままです。");
            Assert.That(playerRenderer.sprite, Is.SameAs(legs), "足はそのままです。");
            Assert.That(animator.GetGearSprite(CharacterGearSlot.MainHand), Is.SameAs(sword), "剣はそのままです。");
            Assert.That(animator.GetGearPosition(CharacterGearSlot.MainHand), Is.EqualTo(hand), "手の位置は同じです。");

            Assert.That(preview.TryEquipArmour(CharacterArmourSlot.Head, "leather"), Is.True);
            yield return null;
            Assert.That(animator.GetHeadSprite(), Is.Not.SameAs(knightHead), "頭の絵が替わります。");
            Assert.That(preview.TryEquipArmour(CharacterArmourSlot.Legs, "leather"), Is.False, "革は足の絵を持ちません。");

            var leather = animator.GetUpperSprite();
            var command = new CharacterActionCommand(CharacterActionSlot.Primary, CharacterDirection8.East);
            Assert.That(preview.PlayerCombat.TryStartAction(command), Is.True);
            yield return null;
            Assert.That(animator.GetUpperSprite(), Is.Not.SameAs(leather), "革の胴でも攻撃のコマへ替わります。");
            Assert.That(animator.GetUpperSprite().name, Does.StartWith("hero-torso-swing"));

            var timeoutAt = Time.time + 2f;
            while (preview.PlayerCombat.ActionState.IsBusy && Time.time < timeoutAt)
            {
                yield return null;
            }

            Assert.That(preview.TryEquipArmour(CharacterArmourSlot.Torso, "knight"), Is.True);
            yield return null;
            Assert.That(preview.GetArmourName(CharacterArmourSlot.Torso), Is.EqualTo("騎士"), "元へ戻せます。");
        }

        /// <summary>
        /// 一覧にある武器と防具は、買わなくてもidでどの組み合わせでも身につけられる。
        /// </summary>
        [UnityTest]
        public IEnumerator ForestMap_EveryEquipmentCanBeWornById()
        {
            yield return LoadForest();
            yield return null;
            var catalog = preview.EquipmentCatalog;
            var animator = preview.PlayerAnimator;
            Assert.That(catalog.Weapons.Count, Is.GreaterThanOrEqualTo(2));
            var combinations = 0;
            foreach (var weapon in catalog.Weapons)
            {
                foreach (var head in catalog.GetArmour(CharacterArmourSlot.Head))
                {
                    foreach (var torso in catalog.GetArmour(CharacterArmourSlot.Torso))
                    {
                        foreach (var legs in catalog.GetArmour(CharacterArmourSlot.Legs))
                        {
                            Assert.That(preview.TryEquipWeapon(weapon.Id), Is.True);
                            Assert.That(preview.TryEquipArmour(CharacterArmourSlot.Head, head.Id), Is.True);
                            Assert.That(preview.TryEquipArmour(CharacterArmourSlot.Torso, torso.Id), Is.True);
                            Assert.That(preview.TryEquipArmour(CharacterArmourSlot.Legs, legs.Id), Is.True);
                            yield return null;
                            var view = preview.PlayerActor.Facing;
                            Assert.That(preview.CurrentEquipmentName, Is.EqualTo(weapon.DisplayName));
                            Assert.That(animator.GetHeadSprite(), Is.SameAs(head.GetView(view).Head));
                            Assert.That(animator.GetUpperSprite(), Is.SameAs(torso.GetView(view).Idle));
                            Assert.That(animator.GetArmour(CharacterArmourSlot.Legs), Is.SameAs(legs));
                            Assert.That(animator.GetGearSprite(CharacterGearSlot.MainHand) != null || animator.GetGearSprite(CharacterGearSlot.OffHand) != null, Is.True);
                            combinations++;
                        }
                    }
                }
            }

            Assert.That(combinations, Is.GreaterThanOrEqualTo(12));
            Assert.That(preview.Coins, Is.Zero, "確認用なので代金はかかりません。");
            Assert.That(preview.OwnedEquipmentCount, Is.EqualTo(1), "確認用の持ち替えは買った数に入りません。");
            Assert.That(preview.TryEquipWeapon("nothing"), Is.False);
            Assert.That(preview.TryEquipArmour(CharacterArmourSlot.Head, "nothing"), Is.False);
            Assert.That(preview.TryEquipArmour(CharacterArmourSlot.Torso, "bare"), Is.False, "素顔は頭だけの部位です。");
        }

        /// <summary>
        /// 回復薬はHPが減っているときだけ買え、その場で回復する。
        /// </summary>
        [UnityTest]
        public IEnumerator ForestMap_PotionHealsWhenHurt()
        {
            yield return LoadForest();
            preview.AddCoinsForTests(5);
            Assert.That(preview.TryBuyPotion(), Is.False, "HPが減っていなければ買いません。");
            Assert.That(preview.Coins, Is.EqualTo(5));

            preview.PlayerHealth.ApplyDamage(50);
            Assert.That(preview.TryBuyPotion(), Is.True);
            Assert.That(preview.PlayerHealth.CurrentHealth, Is.EqualTo(80));
            Assert.That(preview.Coins, Is.EqualTo(0));
        }

        /// <summary>
        /// 噴水でHPが回復し、一度使うと次は使えない。
        /// </summary>
        [UnityTest]
        public IEnumerator ForestMap_FountainHealsOnce()
        {
            yield return LoadForest();
            preview.PlayerActor.Body.position = preview.GetHealingPosition(0);
            preview.PlayerObject.transform.position = preview.GetHealingPosition(0);
            yield return new WaitForFixedUpdate();

            Assert.That(preview.TryUseNearestHealing(), Is.False, "HPが減っていなければ使いません。");
            preview.PlayerHealth.ApplyDamage(50);
            Assert.That(preview.TryUseNearestHealing(), Is.True);
            Assert.That(preview.PlayerHealth.CurrentHealth, Is.EqualTo(80));

            preview.PlayerHealth.ApplyDamage(20);
            Assert.That(preview.TryUseNearestHealing(), Is.False, "一度使うと使えません。");
        }

        /// <summary>
        /// 敵と宝箱は候補から決まった数だけ置かれる。
        /// </summary>
        [UnityTest]
        public IEnumerator ForestMap_PlacesEnemiesAndChestsFromCandidates()
        {
            yield return LoadForest();
            Assert.That(preview.EnemyCount, Is.EqualTo(12));
            Assert.That(preview.ChestCount, Is.EqualTo(4));
        }

        private IEnumerator MoveNextTo(Vector2 position)
        {
            preview.PlayerActor.Body.position = position + Vector2.down * 0.9f;
            preview.PlayerObject.transform.position = position + Vector2.down * 0.9f;
            yield return new WaitForFixedUpdate();
        }

        /// <summary>
        /// 森のSceneを読み込み、準備完了を待つ。Map出力はGitへ入れないため、Sceneがない環境では対象外にする。
        /// </summary>
        private IEnumerator LoadForest()
        {
            if (!File.Exists(ScenePath))
            {
                Assert.Ignore("森マップのSceneが生成されていません。");
            }

#if UNITY_EDITOR
            var load = UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
                ScenePath,
                new LoadSceneParameters(LoadSceneMode.Single));
            while (!load.isDone)
            {
                yield return null;
            }
#endif
            preview = Object.FindAnyObjectByType<ForestMapPreviewDebug>();
            Assert.That(preview, Is.Not.Null);
            while (!preview.IsReady)
            {
                yield return null;
            }
        }

        /// <summary>
        /// 本番のActorと表示Componentを持つCharacterを、Sprite定義と一緒に作る。
        /// </summary>
        private CharacterActor2D CreateCharacter(out SpriteRenderer renderer, out CharacterSpriteAnimator2D animator)
        {
            texture = new Texture2D(4, 4);
            Sprite Create() => Sprite.Create(texture, new Rect(0f, 0f, 4f, 4f), Vector2.zero);
            CharacterSpriteView NewView() =>
                new CharacterSpriteView(Create(), new[] { Create(), Create(), Create(), Create() });
            sprites = ScriptableObject.CreateInstance<CharacterSpriteSetDefinition>();
            sprites.Configure(30f, NewView(), NewView(), NewView(), NewView(), NewView());

            character = new GameObject("TestCharacter");
            renderer = character.AddComponent<SpriteRenderer>();
            var actor = character.AddComponent<CharacterActor2D>();
            actor.Initialize(new CharacterHealth(100), 5f);
            animator = character.AddComponent<CharacterSpriteAnimator2D>();
            animator.Initialize(sprites);
            return actor;
        }

        private CharacterSpriteView View(CharacterDirection8 facing)
        {
            return sprites.GetView(facing);
        }

        private Sprite[] Frames(CharacterDirection8 facing)
        {
            return View(facing).Run;
        }
    }
}
