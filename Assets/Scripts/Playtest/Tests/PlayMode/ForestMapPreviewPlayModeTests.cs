using System.Collections;
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
        /// 斜めの2つのキーが少しずれて離れても斜めの立ち姿で止まり、
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

            actor.SetMoveCommand(new CharacterMoveCommand(1f, 1f));
            yield return null;
            actor.SetMoveCommand(new CharacterMoveCommand(1f, 0f));
            for (var frame = 0; frame < 8; frame++)
            {
                yield return null;
            }

            Assert.That(Frames(CharacterDirection8.East), Does.Contain(renderer.sprite));
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
            var preview = Object.FindAnyObjectByType<ForestMapPreviewDebug>();
            Assert.That(preview, Is.Not.Null);
            while (!preview.IsReady)
            {
                yield return null;
            }

            // 描画順はLateUpdateで決まるので、1Frame待ってから基準を読む。
            yield return null;

            var camera = preview.GameplayCamera;
            Assert.That(camera.orthographicSize * 2f, Is.EqualTo(14f).Within(0.001f), "通常Cameraは縦14.0です。");
            var start = (Vector2)preview.PlayerObject.transform.position;
            var startOrder = preview.PlayerSortingOrder;
            var playerRenderer = preview.PlayerObject.GetComponent<SpriteRenderer>();
            Assert.That(playerRenderer.sprite.name, Is.EqualTo("hero-idle-down"), "止まっている間は走りのコマを使いません。");

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
                Is.LessThan(1.5f),
                "Cameraは進む方向を少し先まで見せながら追従します。");

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
