using System.Collections;
using System.Collections.Generic;
using System.IO;
using FantasyRoyale.Gameplay.Characters;
using FantasyRoyale.Gameplay.Characters.Unity;
using FantasyRoyale.Gameplay.Hud;
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
        /// HPが減ると、騎士は体と部品の全ての層が、敵は体が少しの間赤くなり、元の色へ戻る。斬撃は赤くならない。
        /// </summary>
        [UnityTest]
        public IEnumerator ForestMap_DamageFlashesTheBodyRed()
        {
            yield return LoadForest();
            yield return null;
            var animator = preview.PlayerAnimator;
            Assert.That(animator.IsHitFlashing, Is.False);

            // 振っている最中に被弾させ、斬撃が出ている間の色も確かめる。
            Assert.That(
                preview.PlayerCombat.TryStartAction(new CharacterActionCommand(CharacterActionSlot.Primary, CharacterDirection8.East)),
                Is.True);
            while (preview.PlayerCombat.ActionState.Phase != CharacterActionPhase.Active)
            {
                yield return null;
            }

            preview.PlayerHealth.ApplyDamage(10);
            yield return null;
            Assert.That(animator.IsHitFlashing, Is.True, "HPが減ると赤くなります。");
            var layers = 0;
            foreach (var layer in preview.PlayerObject.GetComponentsInChildren<SpriteRenderer>())
            {
                if (!layer.enabled || layer.sprite == null || layer.gameObject.name == "Shadow")
                {
                    continue;
                }

                if (layer.sprite == animator.GetSlashSprite())
                {
                    Assert.That(layer.color.g, Is.EqualTo(1f).Within(0.01f), "斬撃は赤くしません。");
                    continue;
                }

                layers++;
                Assert.That(layer.color.r, Is.EqualTo(1f).Within(0.01f), $"{layer.gameObject.name}は赤くなります。");
                Assert.That(layer.color.g, Is.LessThan(0.5f), $"{layer.gameObject.name}は赤くなります。");
            }

            Assert.That(layers, Is.GreaterThanOrEqualTo(4), "足、胴、頭、剣と盾の層が赤くなります。");
            Assert.That(animator.GetSlashSprite(), Is.Not.Null, "持続の間は斬撃が出ています。");

            var enemy = preview.GetEnemyActor(0);
            var enemyRenderer = enemy.GetComponent<SpriteRenderer>();
            enemy.Health.ApplyDamage(10);
            yield return null;
            Assert.That(enemyRenderer.color.g, Is.LessThan(0.5f), "敵も赤くなります。");

            yield return new WaitForSeconds(0.3f);
            Assert.That(animator.IsHitFlashing, Is.False);
            Assert.That(preview.PlayerObject.GetComponent<SpriteRenderer>().color, Is.EqualTo(Color.white), "騎士は元の色へ戻ります。");
            Assert.That(enemyRenderer.color, Is.EqualTo(Color.white), "敵も元の色へ戻ります。");

            preview.PlayerHealth.RestoreHealth(10);
            yield return null;
            Assert.That(animator.IsHitFlashing, Is.False, "回復では赤くなりません。");
        }

        /// <summary>
        /// 剣は斬撃の届く所までしか当たらない。斬撃の縁のすぐ内に体がある敵には当たり、すぐ外の敵には当たらない。
        /// 斬撃は剣を振る高さにあるので、下の敵は近く、上の敵は遠くまで届く。
        /// </summary>
        [UnityTest]
        public IEnumerator ForestMap_SwordHitsWhereTheSlashReaches()
        {
            yield return LoadForest();
            var enemy = preview.GetEnemyActor(0);
            var health = enemy.Health;
            var range = preview.PlayerCombat.Loadout.Get(CharacterActionSlot.Primary).Range;
            var swing = preview.PlayerCombat.MeleeOffset.y;
            const float margin = 0.1f;

            // 下の敵は体の上端、上の敵は足元が斬撃の縁に来る距離を基準にする。
            var belowEdge = range - swing + ForestMapPreviewDebug.EnemyBodySize.y;
            yield return Swing(enemy, CharacterDirection8.South, belowEdge + margin);
            Assert.That(health.CurrentHealth, Is.EqualTo(health.MaximumHealth), "斬撃の外の下の敵には当たりません。");
            yield return Swing(enemy, CharacterDirection8.South, belowEdge - margin);
            Assert.That(health.CurrentHealth, Is.LessThan(health.MaximumHealth), "斬撃の内の下の敵には当たります。");

            health.RestoreHealth(health.MaximumHealth);
            var aboveEdge = range + swing;
            yield return Swing(enemy, CharacterDirection8.North, aboveEdge + margin);
            Assert.That(health.CurrentHealth, Is.EqualTo(health.MaximumHealth), "斬撃の外の上の敵には当たりません。");
            yield return Swing(enemy, CharacterDirection8.North, aboveEdge - margin);
            Assert.That(health.CurrentHealth, Is.LessThan(health.MaximumHealth), "斬撃の内の上の敵には当たります。");
        }

        /// <summary>
        /// 開けた村の広場で、騎士を敵から指定の向きの反対へ離して置き、敵の方へ一振りする。敵が動いても距離を保つ。
        /// </summary>
        private IEnumerator Swing(CharacterActor2D enemy, CharacterDirection8 direction, float distance)
        {
            CharacterDirections.ToUnitVector(direction, out var x, out var y);
            var away = new Vector2(-x, -y) * distance;
            enemy.Body.position = preview.SpawnPosition + Vector2.up;
            preview.PlayerActor.Body.position = enemy.Body.position + away;
            yield return new WaitForFixedUpdate();
            Assert.That(
                preview.PlayerCombat.TryStartAction(new CharacterActionCommand(CharacterActionSlot.Primary, direction)),
                Is.True);
            while (preview.PlayerCombat.ActionState.IsBusy)
            {
                preview.PlayerActor.Body.position = enemy.Body.position + away;
                yield return new WaitForFixedUpdate();
            }

            // 連撃を続けられる間を過ぎ、次も一段目から振る。
            yield return new WaitForSeconds(0.5f);
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

            preview.AddCoins(25);
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
        /// 左上のHUDのハートとコインがHPとコインに追従する。お店の前ではEキーの吹き出しがお店の上に出て、
        /// ショップを開くと消える。開始位置の近くには使える物がないので、吹き出しは出ない。
        /// </summary>
        [UnityTest]
        public IEnumerator ForestMap_HudFollowsHealthCoinsAndUseTargets()
        {
            yield return LoadForest();
            yield return null;
            var hud = preview.Hud;
            Assert.That(hud, Is.Not.Null, "BuilderがHUDの絵を渡します。");
            Assert.That(hud.HeartCount, Is.EqualTo(5), "HP 100は仮の規則でハート5つです。");
            Assert.That(hud.GetHeartFill(4), Is.EqualTo(HeartFill.Full));
            Assert.That(hud.CoinText, Is.EqualTo("0"));
            Assert.That(hud.NumberFont.name, Is.EqualTo("PixelMplus12-Bold"), "数字はピクセルフォントで描きます。");
            var scale = hud.GetComponent<Canvas>().scaleFactor;
            var screenSize = hud.NumberFontSize * scale;
            Assert.That(Mathf.Abs(screenSize - Mathf.Round(screenSize / 12f) * 12f), Is.LessThan(0.5f),
                "画面上で元の12 pixelの格子の整数倍に描きます。");
            Assert.That(hud.IsPromptVisible, Is.False, "開始位置の近くには使える物がありません。");

            preview.PlayerHealth.ApplyDamage(30);
            preview.AddCoins(12);
            yield return null;
            Assert.That(hud.GetHeartFill(2), Is.EqualTo(HeartFill.Full));
            Assert.That(hud.GetHeartFill(3), Is.EqualTo(HeartFill.Half), "HP 70はハート3つ半です。");
            Assert.That(hud.GetHeartFill(4), Is.EqualTo(HeartFill.Empty));
            Assert.That(hud.CoinText, Is.EqualTo("12"));

            preview.PlayerActor.Body.position = preview.GetShopPosition(2);
            preview.PlayerObject.transform.position = preview.GetShopPosition(2);
            yield return new WaitForFixedUpdate();
            yield return null;
            Assert.That(preview.TryGetUseTarget(out var anchor), Is.True);
            Assert.That(hud.IsPromptVisible, Is.True, "屋台の前では吹き出しが出ます。");
            var expected = (Vector2)preview.GameplayCamera.WorldToScreenPoint(anchor);
            Assert.That(Vector2.Distance(hud.PromptScreenPosition, expected), Is.LessThan(1f), "吹き出しは屋台の上を指します。");
            Assert.That(anchor.y, Is.GreaterThan(preview.GetShopPosition(2).y), "吹き出しは立つ位置より上に出ます。");

            Assert.That(preview.TryOpenShop(), Is.True);
            yield return null;
            Assert.That(hud.IsPromptVisible, Is.False, "ショップを開いている間は出しません。");
        }

        /// <summary>
        /// お店の前で開くと、店主の顔と名前、商品の一覧、選んだ商品の説明、自分の持ち物が出る。
        /// 買うとコインが減り、持ち替えや着替えが持ち物の枠と袋に出る。店ごとに店主が替わり、店にいる間は騎士が動かない。
        /// </summary>
        [UnityTest]
        public IEnumerator ForestMap_ShopScreenShowsKeeperGoodsAndInventory()
        {
            yield return LoadForest();
            yield return null;
            var shop = preview.Hud.Shop;
            Assert.That(shop.IsVisible, Is.False);

            preview.PlayerActor.Body.position = preview.GetShopPosition(0);
            preview.PlayerObject.transform.position = preview.GetShopPosition(0);
            yield return new WaitForFixedUpdate();
            Assert.That(preview.TryOpenShop(), Is.True);
            yield return null;
            Assert.That(shop.IsVisible, Is.True);
            Assert.That(shop.KeeperNameText, Is.EqualTo("武器屋"));
            Assert.That(shop.KeeperPortrait, Is.Not.Null, "武器屋の店主の顔が出ます。");
            Assert.That(shop.RowCount, Is.EqualTo(Mathf.Min(ShopScreen.VisibleRows, preview.ShopItemCount)));
            Assert.That(shop.GetRowName(0), Is.EqualTo("回復薬"));
            Assert.That(shop.WeaponIcon.name, Is.EqualTo("icon-weapon-sword"), "持ち物の武器の枠に剣が出ます。");

            var start = preview.PlayerObject.transform.position;
            preview.SetMoveInputOverrideForTests(Vector2.right);
            for (var frame = 0; frame < 10; frame++)
            {
                yield return new WaitForFixedUpdate();
            }

            preview.ClearMoveInputOverrideForTests();
            Assert.That(Vector2.Distance(preview.PlayerObject.transform.position, start), Is.GreaterThan(0.05f), "店にいる間も騎士は動けます。");
            Assert.That(preview.IsShopOpen, Is.True, "届く所の中なら、店は開いたままです。");
            Assert.That(preview.Hud.Loadout.IsVisible, Is.False, "店を開いている間は下の枠を隠します。");
            Assert.That(shop.OffHandIcon.name, Is.EqualTo("icon-weapon-shield"), "右の枠に盾も出ます。");

            yield return SelectShopItem("弓");
            Assert.That(preview.TryBuySelected(), Is.False);
            yield return null;
            Assert.That(shop.MessageText, Is.EqualTo("コインが足りない"));

            preview.AddCoins(25);
            Assert.That(preview.TryBuySelected(), Is.True);
            yield return null;
            Assert.That(preview.Coins, Is.EqualTo(5));
            Assert.That(shop.WeaponIcon.name, Is.EqualTo("icon-weapon-bow"), "買った弓に持ち替えます。");
            Assert.That(shop.GetBagIcon(0).name, Is.EqualTo("icon-weapon-sword"), "剣はカバンに入ります。");
            Assert.That(shop.OffHandIcon, Is.Null, "両手の弓では、オフハンドの枠に絵を出しません。");
            Assert.That(shop.IsOffHandGreyed, Is.True, "オフハンドの枠を灰色にします。");
            Assert.That(shop.IsOffHandPlaceholderVisible, Is.True, "灰色のオフハンドには、盾の影を薄く出します。");
            Assert.That(shop.IsHeadPlaceholderVisible, Is.False, "着けている頭には影の絵を出しません。");
            Assert.That(preview.Inventory.GetBagItem(1).Id, Is.EqualTo("shield"), "両手の弓なので、盾もカバンに入ります。");
            Assert.That(shop.GetRowStatus(preview.ShopSelectedIndex - shop.FirstRow), Is.EqualTo("装備中"));

            yield return SelectShopItem("革（頭）");
            preview.AddCoins(10);
            Assert.That(preview.TryBuySelected(), Is.True);
            yield return null;
            Assert.That(shop.HeadIcon.name, Is.EqualTo("icon-leather-head"), "買った革の頭を着ます。");
            Assert.That(shop.GetBagIcon(2).name, Is.EqualTo("icon-knight-head"), "脱いだ騎士の頭はカバンに入ります。");

            preview.PlayerActor.Body.position = preview.SpawnPosition;
            preview.PlayerObject.transform.position = preview.SpawnPosition;
            yield return new WaitForFixedUpdate();
            yield return null;
            yield return null;
            Assert.That(preview.IsShopOpen, Is.False, "店の届く所を出ると閉じます。");
            Assert.That(shop.IsVisible, Is.False);

            preview.PlayerActor.Body.position = preview.GetShopPosition(4);
            preview.PlayerObject.transform.position = preview.GetShopPosition(4);
            yield return new WaitForFixedUpdate();
            Assert.That(preview.TryOpenShop(), Is.True);
            yield return null;
            Assert.That(shop.KeeperNameText, Is.EqualTo("魔法屋"), "店ごとに店主が替わります。");
            preview.CloseShop();
        }

        /// <summary>
        /// 金の矢印のカーソルは先端が指す位置に出る。店の画面では、行の上でマウスを動かすとその品物を選び、
        /// 行を押して選び、買うキーを押して買い、ホイールで送り、閉じるキーを押して閉じる。キーは乗ると光る。
        /// </summary>
        [UnityTest]
        public IEnumerator ForestMap_ShopScreenWorksWithTheMouse()
        {
            yield return LoadForest();
            yield return null;
            var hud = preview.Hud;
            var shop = hud.Shop;
            hud.ShowCursor(new Vector2(200f, 300f));
            Assert.That(hud.IsCursorVisible, Is.True);
            Assert.That(Vector2.Distance(hud.CursorScreenPosition, new Vector2(200f, 300f)), Is.LessThan(1f), "カーソルの先端が指す位置に来ます。");

            preview.PlayerActor.Body.position = preview.GetShopPosition(0);
            preview.PlayerObject.transform.position = preview.GetShopPosition(0);
            yield return new WaitForFixedUpdate();
            Assert.That(preview.TryOpenShop(), Is.True);
            yield return null;

            preview.HandleShopPointer(shop.GetRowScreenCentre(3), false, 0f);
            yield return null;
            Assert.That(preview.ShopSelectedIndex, Is.EqualTo(shop.FirstRow + 3), "行の上でマウスを動かすと選びます。");
            Assert.That(shop.SelectedNameText, Is.EqualTo(shop.GetRowName(3)));

            preview.HandleShopPointer(shop.GetRowScreenCentre(3), false, 0f);
            preview.SelectShopItem(1);
            preview.HandleShopPointer(shop.GetRowScreenCentre(3), false, 0f);
            Assert.That(preview.ShopSelectedIndex, Is.EqualTo(shop.FirstRow + 4), "止まったマウスはキーボードで選んだ品物を取り返しません。");

            preview.HandleShopPointer(shop.BuyScreenCentre, false, 0f);
            Assert.That(shop.Hovered, Is.EqualTo(ShopHitKind.Buy), "買うキーに乗ると光ります。");

            var bowRow = -1;
            for (var row = 0; row < shop.RowCount; row++)
            {
                if (shop.GetRowName(row) == "弓")
                {
                    bowRow = row;
                }
            }

            Assert.That(bowRow, Is.GreaterThanOrEqualTo(0));
            preview.HandleShopPointer(shop.GetRowScreenCentre(bowRow), true, 0f);
            yield return null;
            Assert.That(shop.SelectedNameText, Is.EqualTo("弓"));
            preview.AddCoins(25);
            preview.HandleShopPointer(shop.BuyScreenCentre, true, 0f);
            yield return null;
            Assert.That(preview.Coins, Is.EqualTo(5), "買うキーを押すと買います。");
            Assert.That(preview.CurrentEquipmentName, Is.EqualTo("弓"));

            var before = preview.ShopSelectedIndex;
            preview.HandleShopPointer(shop.BuyScreenCentre, false, -1f);
            Assert.That(preview.ShopSelectedIndex, Is.EqualTo((before + 1) % preview.ShopItemCount), "ホイールを下へ回すと次の品物を選びます。");

            preview.HandleShopPointer(shop.CloseScreenCentre, true, 0f);
            yield return null;
            Assert.That(preview.IsShopOpen, Is.False, "閉じるキーを押すと閉じます。");
            Assert.That(shop.IsVisible, Is.False);
        }

        /// <summary>
        /// 剣を振ると上半身のコマが重なり、剣はコマごとの角度で腕と一緒に動き、盾は動かず、終わると元の絵へ戻る。
        /// 判定のある間は、判定の距離を外側の縁とする斬撃が、判定の起点の真上に出る。
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
            var range = preview.PlayerCombat.ActionState.CurrentSpec.Range;

            var bodies = new HashSet<Sprite>();
            var swords = new HashSet<Sprite>();
            var shields = new HashSet<Vector2>();
            var slashes = new HashSet<Sprite>();
            var slashInWindup = false;
            var slashPosition = Vector2.zero;
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
                    var slash = animator.GetSlashSprite();
                    if (slash != null)
                    {
                        slashes.Add(slash);
                        slashPosition = animator.GetSlashPosition();
                        slashInWindup |= preview.PlayerCombat.ActionState.Phase == CharacterActionPhase.Windup;
                    }
                }
            }

            Assert.That(bodies.Contains(null), Is.False, "攻撃中は上半身が重なります。");
            Assert.That(bodies.Count, Is.GreaterThanOrEqualTo(3), "上半身のコマが進みます。");
            Assert.That(swords.Count, Is.GreaterThanOrEqualTo(3), "剣はコマごとに角度が変わります。");
            Assert.That(shields.Count, Is.EqualTo(1), "振る間、盾は上半身に対して動きません。");
            Assert.That(slashes.Count, Is.EqualTo(1), "判定のある間は斬撃が出ます。");
            Assert.That(slashInWindup, Is.False, "構えの間は斬撃を出しません。");
            var extent = new List<Sprite>(slashes)[0].bounds.extents.x;
            Assert.That(extent, Is.InRange(range, range + 2f / CharacterSlashSprites.PixelsPerUnit), "斬撃の外側の縁は判定の距離です。");
            var melee = preview.PlayerCombat.MeleeOffset;
            Assert.That(Vector2.Distance(slashPosition, melee), Is.LessThan(0.01f), "斬撃は判定の扇の中心に出ます。");
            Assert.That(melee.y, Is.GreaterThan(preview.PlayerActor.FootCollider.offset.y), "判定の扇は剣を振る高さにあります。");
            yield return null;
            Assert.That(playerRenderer.sprite, Is.SameAs(idle), "終わると通常の絵へ戻ります。");
            Assert.That(animator.GetUpperSprite(), Is.SameAs(idleTorso), "胴も通常の絵へ戻ります。");
            Assert.That(animator.GetSlashSprite(), Is.Null, "終わると斬撃は消えます。");
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
        /// 回復薬は買うと使用枠に入り、数字キーの番号で使うと回復する。HPが減っていなければ使わずに残す。
        /// 使用枠がいっぱいなら、買った回復薬はカバンへ入る。
        /// </summary>
        [UnityTest]
        public IEnumerator ForestMap_PotionGoesToAQuickSlotAndHealsWhenUsed()
        {
            yield return LoadForest();
            preview.AddCoins(5);
            Assert.That(preview.TryBuyPotion(), Is.True);
            Assert.That(preview.Coins, Is.EqualTo(0));
            Assert.That(preview.Inventory.GetQuickItem(0).Id, Is.EqualTo(ForestMapPreviewDebug.PotionItemId), "買った回復薬は使用枠に入ります。");
            Assert.That(preview.Inventory.CountFreeBagSlots(), Is.EqualTo(preview.Inventory.BagCapacity), "カバンには入りません。");
            Assert.That(preview.TryUseQuickSlot(0), Is.False, "HPが減っていなければ飲みません。");
            Assert.That(preview.Inventory.GetQuickItem(0), Is.Not.Null);

            preview.PlayerHealth.ApplyDamage(50);
            Assert.That(preview.TryUseQuickSlot(0), Is.True);
            Assert.That(preview.PlayerHealth.CurrentHealth, Is.EqualTo(80));
            Assert.That(preview.Inventory.CountFreeQuickSlots(), Is.EqualTo(preview.Inventory.QuickCapacity), "飲んだ回復薬はなくなります。");

            preview.AddCoins(5 * (preview.Inventory.QuickCapacity + 1));
            for (var index = 0; index <= preview.Inventory.QuickCapacity; index++)
            {
                Assert.That(preview.TryBuyPotion(), Is.True);
            }

            Assert.That(preview.Inventory.FindInBag(ForestMapPreviewDebug.PotionItemId), Is.Zero, "使用枠がいっぱいなら、カバンへ入ります。");
        }

        /// <summary>
        /// 剣は初めから炎の弾を持ち、MPを使って唱える。カバンの魔法を使うと剣の空いた枠へはまり、切り替えて唱えられる。
        /// MPが足りなければ唱えない。両手の弓へ持ち替えると剣の魔法は剣に残り、戻すと選んでいた魔法のままである。
        /// </summary>
        [UnityTest]
        public IEnumerator ForestMap_MagicUsesManaAndStaysWithTheWeapon()
        {
            yield return LoadForest();
            var loadout = preview.PlayerCombat.Loadout;
            Assert.That(preview.Inventory.MainWeapon.MagicSlotCount, Is.EqualTo(2));
            Assert.That(loadout.Get(CharacterActionSlot.Magic).Id, Is.EqualTo("magic-fire"));

            var fullMana = preview.Mana.Current;
            Assert.That(preview.TryCastMagic(), Is.True);
            Assert.That(preview.Mana.Current, Is.LessThan(fullMana), "唱えるとMPを使います。");
            for (var frame = 0; frame < 60 && preview.PlayerCombat.ActionState.IsBusy; frame++)
            {
                yield return new WaitForFixedUpdate();
            }

            Assert.That(preview.Inventory.TryAddToBag(new CharacterItem("ice", CharacterItemKind.Magic)), Is.Zero);
            Assert.That(preview.TryUseBagItem(0), Is.True, "カバンの魔法は剣の空いた枠へはまります。");
            Assert.That(preview.Inventory.SelectNextMagic(), Is.EqualTo(1));
            Assert.That(loadout.Get(CharacterActionSlot.Magic).Id, Is.EqualTo("magic-ice"));

            preview.Mana.TrySpend(preview.Mana.Current);
            Assert.That(preview.TryCastMagic(), Is.False, "MPが足りなければ唱えません。");
            Assert.That(preview.LastMessage, Is.EqualTo("MPが足りない"));

            preview.AddCoins(20);
            Assert.That(preview.TryBuyBow(), Is.True);
            Assert.That(preview.CurrentEquipmentName, Is.EqualTo("弓"));
            Assert.That(loadout.Get(CharacterActionSlot.Magic), Is.Null, "弓の魔法の枠は空です。");

            var swordSlot = preview.Inventory.FindInBag("sword");
            Assert.That(swordSlot, Is.GreaterThanOrEqualTo(0));
            Assert.That(preview.TryUseBagItem(swordSlot), Is.True);
            Assert.That(preview.CurrentEquipmentName, Is.EqualTo("剣"));
            Assert.That(loadout.Get(CharacterActionSlot.Magic).Id, Is.EqualTo("magic-ice"), "剣の魔法は剣に残ります。");
        }

        /// <summary>
        /// HUDに武器は出さない。下の段にカバンのアイテムを並べ、その上に固有の技と魔法を置く。
        /// 魔法の枠の数が2から1へ変わっても、大きな枠の位置は動かない。MPはハートの下の玉で見せ、唱えると減る。
        /// 固有の技は使うと待ち時間が始まり、覆いが時間で減って消えるまで使えない。
        /// </summary>
        [UnityTest]
        public IEnumerator ForestMap_LoadoutBarFollowsTheInventory()
        {
            yield return LoadForest();
            yield return null;
            var hud = preview.Hud;
            var bar = hud.Loadout;
            Assert.That(bar.MagicSlotCount, Is.EqualTo(2), "剣の魔法の枠は2つです。");
            Assert.That(bar.MagicIcon.name, Is.EqualTo("icon-magic-fire"), "選んでいる魔法がLの枠に出ます。");
            Assert.That(bar.ItemSlotCount, Is.EqualTo(preview.Inventory.QuickCapacity), "下の段は使用枠です。");
            Assert.That(bar.ItemIcon, Is.Null, "カバンは空で始まります。");
            Assert.That(hud.ManaOrbCount, Is.EqualTo(4), "MP 40は玉4つです。");
            Assert.That(hud.LitManaOrbs, Is.EqualTo(4));

            var centres = new Vector2[3];
            for (var index = 0; index < centres.Length; index++)
            {
                centres[index] = bar.GetBigSlotScreenCentre(index);
            }

            Assert.That(preview.TryCastMagic(), Is.True);
            yield return null;
            Assert.That(hud.LitManaOrbs, Is.LessThan(4), "唱えるとMPの玉が減ります。");
            Assert.That(bar.IsMagicDimmed, Is.True, "唱えている間は魔法の枠を暗くします。");
            for (var frame = 0; frame < 60 && preview.PlayerCombat.ActionState.IsBusy; frame++)
            {
                yield return new WaitForFixedUpdate();
            }

            Assert.That(bar.SkillCooldownFill, Is.Zero, "技はすぐ使えます。");
            Assert.That(preview.TryUseSkill(), Is.True);
            yield return null;
            Assert.That(bar.SkillCooldownFill, Is.GreaterThan(0.8f), "使った技は待ち時間の覆いで暗くなります。");
            Assert.That(preview.TryUseSkill(), Is.False, "待ち時間の間は使えません。");
            yield return new WaitForSeconds(1f);
            Assert.That(bar.SkillCooldownFill, Is.LessThan(0.8f).And.GreaterThan(0f), "覆いは時間で減ります。");
            yield return new WaitForSeconds(2.5f);
            Assert.That(bar.SkillCooldownFill, Is.Zero, "待ち時間が終わると覆いが消えます。");
            Assert.That(preview.SkillCooldown.IsReady, Is.True);

            preview.AddCoins(25);
            Assert.That(preview.TryBuyPotion(), Is.True);
            Assert.That(preview.TryBuyBow(), Is.True);
            yield return null;
            Assert.That(preview.CurrentEquipmentName, Is.EqualTo("弓"));
            Assert.That(bar.MagicSlotCount, Is.EqualTo(1), "弓の魔法の枠は1つです。");
            Assert.That(bar.MagicIcon, Is.Null, "弓の魔法の枠は空です。");
            Assert.That(bar.ItemIcon.name, Is.EqualTo("icon-potion"), "選んでいるアイテムが;の枠に出ます。");
            Assert.That(preview.Inventory.FindInBag("sword"), Is.GreaterThanOrEqualTo(0), "外した剣はカバンに入り、下の段には出ません。");
            Assert.That(bar.GetItemIcon(1), Is.Null);
            for (var index = 0; index < centres.Length; index++)
            {
                Assert.That(Vector2.Distance(bar.GetBigSlotScreenCentre(index), centres[index]), Is.LessThan(0.5f), "枠の数が変わっても大きな枠は動きません。");
            }
        }

        /// <summary>
        /// Tabの持ち物の画面は、装備5つ、固有の技、魔法の枠、カバンを並べる。開いている間も騎士は動ける。
        /// 上下左右で隣の枠へ移り、決めるとカバンの弓を着け、剣の魔法をカバンへ外す。
        /// </summary>
        [UnityTest]
        public IEnumerator ForestMap_InventoryScreenEquipsAndKeepsTheKnightMoving()
        {
            yield return LoadForest();
            yield return null;
            var screen = preview.Hud.Inventory;
            preview.OpenInventory();
            yield return null;
            Assert.That(screen.IsVisible, Is.True);
            Assert.That(screen.CellCount, Is.EqualTo(5 + 1 + 2 + preview.Inventory.QuickCapacity + preview.Inventory.BagCapacity), "剣は魔法の枠が2つです。");
            Assert.That(screen.IsOffHandGreyed, Is.False, "片手の剣と盾では、オフハンドを灰色にしません。");
            for (var slot = 0; slot < InventoryScreen.EquipmentCount; slot++)
            {
                Assert.That(screen.IsPlaceholderVisible(screen.FindCell(InventoryCellKind.Equipment, slot)), Is.False, "着けている枠には影の絵を出しません。");
            }

            var start = preview.PlayerObject.transform.position;
            preview.SetMoveInputOverrideForTests(Vector2.right);
            for (var frame = 0; frame < 10; frame++)
            {
                yield return new WaitForFixedUpdate();
            }

            preview.ClearMoveInputOverrideForTests();
            Assert.That(Vector2.Distance(preview.PlayerObject.transform.position, start), Is.GreaterThan(0.05f), "開いている間も騎士は動けます。");

            var head = screen.FindCell(InventoryCellKind.Equipment, 0);
            var torso = screen.FindCell(InventoryCellKind.Equipment, 1);
            Assert.That(screen.GetCellScreenCentre(screen.FindCell(InventoryCellKind.Equipment, 3)).x,
                Is.GreaterThan(screen.GetCellScreenCentre(torso).x), "メイン武器は右です。");
            Assert.That(screen.Navigate(head, Vector2.down), Is.EqualTo(torso), "装備は体の形に並び、頭の下は胴です。");
            Assert.That(screen.Navigate(torso, Vector2.down), Is.EqualTo(screen.FindCell(InventoryCellKind.Equipment, 2)), "胴の下は足です。");
            Assert.That(screen.Navigate(torso, Vector2.right), Is.EqualTo(screen.FindCell(InventoryCellKind.Equipment, 3)), "胴の右はメイン武器です。");
            Assert.That(screen.Navigate(torso, Vector2.left), Is.EqualTo(screen.FindCell(InventoryCellKind.Equipment, 4)), "胴の左はオフハンドです。");
            var firstMagic = screen.FindCell(InventoryCellKind.Magic, 0);
            Assert.That(screen.Navigate(screen.FindCell(InventoryCellKind.Skill, 0), Vector2.right), Is.EqualTo(firstMagic), "技の右は魔法です。");
            Assert.That(screen.Navigate(screen.FindCell(InventoryCellKind.Equipment, 2), Vector2.down), Is.EqualTo(screen.FindCell(InventoryCellKind.Quick, 1)), "足の下は使用枠です。");
            Assert.That(screen.Navigate(screen.FindCell(InventoryCellKind.Quick, 0), Vector2.down), Is.EqualTo(screen.FindCell(InventoryCellKind.Bag, 0)), "使用枠の下はカバンです。");

            preview.SelectInventoryCell(firstMagic);
            yield return null;
            Assert.That(screen.DetailNameText, Is.EqualTo("炎の弾"));
            Assert.That(preview.ActivateInventoryCell(), Is.True, "剣の魔法をカバンへ外します。");
            Assert.That(preview.Inventory.GetBagItem(0).Id, Is.EqualTo("fire"));

            preview.Inventory.TryAddToBag(preview.EquipmentCatalog.FindWeapon("bow").CreateItem());
            preview.SelectInventoryCell(screen.FindCell(InventoryCellKind.Bag, 1));
            yield return null;
            Assert.That(screen.ActionLabelText, Is.EqualTo("着ける"));
            Assert.That(preview.ActivateInventoryCell(), Is.True);
            yield return null;
            Assert.That(preview.CurrentEquipmentName, Is.EqualTo("弓"));
            Assert.That(screen.CellCount, Is.EqualTo(5 + 1 + 1 + preview.Inventory.QuickCapacity + preview.Inventory.BagCapacity), "弓の魔法の枠は1つです。");
            Assert.That(screen.GetCellIcon(4), Is.Null, "両手の弓では、オフハンドの枠に絵を出しません。");
            Assert.That(screen.IsOffHandGreyed, Is.True, "オフハンドの枠を灰色にします。");
            Assert.That(screen.IsPlaceholderVisible(screen.FindCell(InventoryCellKind.Equipment, 4)), Is.True, "灰色のオフハンドには、盾の影を薄く出します。");

            preview.SelectInventoryCell(screen.FindCell(InventoryCellKind.Equipment, 0));
            Assert.That(preview.ActivateInventoryCell(), Is.True, "頭の防具をカバンへ外します。");
            yield return null;
            Assert.That(screen.IsPlaceholderVisible(screen.FindCell(InventoryCellKind.Equipment, 0)), Is.True, "空いた頭の枠に影の絵を出します。");

            preview.CloseInventory();
            yield return null;
            Assert.That(screen.IsVisible, Is.False);
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
        /// デバッグの所持金はコインを増やして減らし、0枚より少なくはならない。
        /// </summary>
        [UnityTest]
        public IEnumerator ForestMap_DebugAddsAndRemovesCoins()
        {
            yield return LoadForest();
            preview.AddCoins(100);
            Assert.That(preview.Coins, Is.EqualTo(100));
            preview.AddCoins(-10);
            Assert.That(preview.Coins, Is.EqualTo(90));
            preview.AddCoins(-1000);
            Assert.That(preview.Coins, Is.EqualTo(0), "0枚より少なくはなりません。");
        }

        /// <summary>
        /// デバッグのHP全回復は、減ったHPを最大まで戻す。減っていなければ何もしない。
        /// </summary>
        [UnityTest]
        public IEnumerator ForestMap_DebugRestoresFullHealth()
        {
            yield return LoadForest();
            Assert.That(preview.RestorePlayerHealth(), Is.False, "HPが減っていなければ戻しません。");

            preview.PlayerHealth.ApplyDamage(70);
            Assert.That(preview.RestorePlayerHealth(), Is.True);
            Assert.That(preview.PlayerHealth.CurrentHealth, Is.EqualTo(preview.PlayerHealth.MaximumHealth));
        }

        /// <summary>
        /// デバッグの「敵を全部倒す」は、出ている敵を全て倒し、1体ごとにコインを渡す。
        /// </summary>
        [UnityTest]
        public IEnumerator ForestMap_DebugDefeatsAllEnemies()
        {
            yield return LoadForest();
            Assert.That(preview.DefeatAllEnemies(), Is.EqualTo(preview.EnemyCount));
            for (var index = 0; index < preview.EnemyCount; index++)
            {
                Assert.That(preview.GetEnemyActor(index).gameObject.activeSelf, Is.False, "倒した敵は消えます。");
            }

            Assert.That(preview.Coins, Is.EqualTo(preview.EnemyCount));
            Assert.That(preview.DefeatAllEnemies(), Is.EqualTo(0), "倒した敵はもう一度倒しません。");
        }

        /// <summary>
        /// 無敵の間は敵に触れてもHPが減らず、切ると減る。
        /// </summary>
        [UnityTest]
        public IEnumerator ForestMap_InvincibleHeroTakesNoContactDamage()
        {
            yield return LoadForest();
            var enemy = preview.GetEnemyActor(0);
            preview.IsInvincible = true;

            // 接触の間隔は1秒なので、2秒半触れ続ければ無敵でなければ2回以上当たる。
            var untilAt = Time.time + 2.5f;
            while (Time.time < untilAt)
            {
                preview.PlayerActor.Body.position = enemy.Body.position + Vector2.right * 0.7f;
                yield return new WaitForFixedUpdate();
            }

            Assert.That(preview.PlayerHealth.CurrentHealth, Is.EqualTo(preview.PlayerHealth.MaximumHealth), "無敵の間はHPが減りません。");

            preview.IsInvincible = false;
            var timeoutAt = Time.time + 4f;
            while (preview.PlayerHealth.CurrentHealth == preview.PlayerHealth.MaximumHealth && Time.time < timeoutAt)
            {
                preview.PlayerActor.Body.position = enemy.Body.position + Vector2.right * 0.7f;
                yield return new WaitForFixedUpdate();
            }

            Assert.That(preview.PlayerHealth.CurrentHealth, Is.LessThan(preview.PlayerHealth.MaximumHealth), "無敵を切るとHPが減ります。");
        }

        /// <summary>
        /// ワンパンの間は、剣の一振りで敵を倒す。切っているときの一振りでは倒れない。
        /// </summary>
        [UnityTest]
        public IEnumerator ForestMap_OneHitKillDefeatsEnemyWithOneSwing()
        {
            yield return LoadForest();
            var enemy = preview.GetEnemyActor(0);
            var health = enemy.Health;

            yield return Swing(enemy, CharacterDirection8.West, 0.9f);
            Assert.That(health.CurrentHealth, Is.LessThan(health.MaximumHealth), "一振りが当たります。");
            Assert.That(health.IsAlive, Is.True, "ワンパンを切っていれば一振りでは倒れません。");

            health.RestoreHealth(health.MaximumHealth);
            preview.IsOneHitKill = true;
            yield return Swing(enemy, CharacterDirection8.West, 0.9f);
            Assert.That(health.IsAlive, Is.False, "ワンパンの間は一振りで倒れます。");
            Assert.That(enemy.gameObject.activeSelf, Is.False, "倒した敵は消えます。");
            Assert.That(preview.Coins, Is.EqualTo(1));
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

        /// <summary>
        /// 店の品物を名前が一致するまで送る。
        /// </summary>
        private IEnumerator SelectShopItem(string name)
        {
            for (var step = 0; step < preview.ShopItemCount && preview.Hud.Shop.SelectedNameText != name; step++)
            {
                preview.SelectShopItem(1);
                yield return null;
            }

            Assert.That(preview.Hud.Shop.SelectedNameText, Is.EqualTo(name));
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

            // 本物のマウスを動かしても結果が変わらないよう、マウスはTestが渡す位置だけを使う。
            preview.SetMouseIgnoredForTests(true);
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
