using System.Collections;
using FantasyRoyale.Gameplay.Characters;
using FantasyRoyale.Gameplay.Characters.Unity;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace FantasyRoyale.Playtest.Tests.PlayMode
{
    /// <summary>
    /// 実Playtest Sceneを通して、入力から攻撃、命中、装備の差し替えまでの経路を確認する。
    /// </summary>
    public sealed class PreviewDebugCombatPlayModeTests : InputTestFixture
    {
        private const string PlaytestScenePath =
            "Assets/Scenes/Playtest/GbaForestBattleRoyaleExplorationPreviewDebug.unity";

        private const int SwordDamage = 20;
        private const int BowDamage = 12;
        private const double InputClockStartSeconds = 1000000000d;

        private BattleRoyaleExplorationPreviewDebug bootstrap;
        private Keyboard keyboard;

        /// <summary>
        /// 剣は前方の的へ一回の攻撃で一度だけ当たり、背後の的には当たらないことを確認する。
        /// </summary>
        [UnityTest]
        public IEnumerator Sword_HitsTargetOncePerSwing()
        {
            yield return LoadPreview();
            var toTarget = DirectionToTarget();
            yield return PlaceNearTarget(1f, toTarget);
            var maximumHealth = bootstrap.PreviewTargetHealth.MaximumHealth;

            Assert.That(bootstrap.CurrentEquipmentName, Is.EqualTo("剣"));
            yield return TapKey(keyboard.jKey);
            Assert.That(bootstrap.PlayerCombat.ActionState.IsBusy, Is.True);
            Assert.That(bootstrap.PlayerCombat.ActionState.CurrentSpec.Id, Is.EqualTo("sword-slash"));
            yield return WaitForIdle();

            Assert.That(bootstrap.PreviewTargetHealth.CurrentHealth, Is.EqualTo(maximumHealth - SwordDamage));
            Assert.That(bootstrap.CombatPresenter.SlashEffectCount, Is.EqualTo(1));
            Assert.That(bootstrap.CombatPresenter.LastFeedback, Does.Contain("sword-slash"));

            // 的を背にして振っても、扇の外なのでDamageは増えない。
            yield return Face(-toTarget);
            yield return TapKey(keyboard.jKey);
            yield return WaitForIdle();
            Assert.That(bootstrap.PreviewTargetHealth.CurrentHealth, Is.EqualTo(maximumHealth - SwordDamage));
        }

        /// <summary>
        /// 発生と持続の間は動けず、硬直中は半速になることを確認する。
        /// </summary>
        [UnityTest]
        public IEnumerator Action_LimitsMovementByPhase()
        {
            yield return LoadPreview();
            var toTarget = DirectionToTarget();
            yield return PlaceNearTarget(2f, toTarget);

            var combat = bootstrap.PlayerCombat;
            bootstrap.SetMoveInputOverrideForTests(-toTarget);
            Assert.That(
                combat.TryStartAction(new CharacterActionCommand(CharacterActionSlot.Primary, CharacterDirection8.East)),
                Is.True);
            Assert.That(bootstrap.PlayerActor.MovementScale, Is.EqualTo(0f));

            var lockedPosition = bootstrap.PlayerBody.position;
            while (combat.ActionState.Phase != CharacterActionPhase.Recovery)
            {
                yield return new WaitForFixedUpdate();
                if (combat.ActionState.Phase != CharacterActionPhase.Recovery)
                {
                    Assert.That(
                        Vector2.Distance(bootstrap.PlayerBody.position, lockedPosition),
                        Is.LessThan(0.001f),
                        "発生と持続の間は移動しません。");
                }
            }

            Assert.That(bootstrap.PlayerActor.MovementScale, Is.EqualTo(CharacterActionState.RecoveryMovementScale));
            Assert.That(
                combat.TryStartAction(new CharacterActionCommand(CharacterActionSlot.Primary, CharacterDirection8.East)),
                Is.False,
                "実行中は割り込めません。");
            yield return WaitForIdle();
            Assert.That(bootstrap.PlayerActor.MovementScale, Is.EqualTo(1f));
            bootstrap.ClearMoveInputOverrideForTests();
        }

        /// <summary>
        /// 装備を替えると通常攻撃が弓に替わり、矢が的へ当たって消えることを確認する。
        /// </summary>
        [UnityTest]
        public IEnumerator Equipment_SwapChangesPrimaryToBow()
        {
            yield return LoadPreview();
            var toTarget = DirectionToTarget();
            yield return PlaceNearTarget(2.4f, toTarget);
            var maximumHealth = bootstrap.PreviewTargetHealth.MaximumHealth;

            yield return TapKey(keyboard.digit2Key);
            Assert.That(bootstrap.CurrentEquipmentName, Is.EqualTo("弓"));
            yield return TapKey(keyboard.jKey);
            Assert.That(bootstrap.PlayerCombat.ActionState.CurrentSpec.Id, Is.EqualTo("bow-shot"));
            yield return WaitForIdle();
            yield return WaitForSeconds(0.1f);

            Assert.That(bootstrap.PreviewTargetHealth.CurrentHealth, Is.EqualTo(maximumHealth - BowDamage));
            Assert.That(Object.FindAnyObjectByType<CharacterProjectile2D>(), Is.Null, "当たった矢は消えます。");

            // 前の装備へ戻すと、通常攻撃も剣へ戻る。
            yield return TapKey(keyboard.digit1Key);
            Assert.That(bootstrap.CurrentEquipmentName, Is.EqualTo("剣"));
            Assert.That(bootstrap.PlayerCombat.Loadout.Get(CharacterActionSlot.Primary).Id, Is.EqualTo("sword-slash"));
        }

        /// <summary>
        /// 特殊ActionのSlotがKキーから動き、的のいない方向の矢は射程か壁で消えることを確認する。
        /// </summary>
        [UnityTest]
        public IEnumerator Special_FiresBowAwayFromTargetAndArrowExpires()
        {
            yield return LoadPreview();
            var toTarget = DirectionToTarget();
            yield return PlaceNearTarget(2f, toTarget);
            yield return Face(-toTarget);
            var startingHealth = bootstrap.PreviewTargetHealth.CurrentHealth;

            yield return TapKey(keyboard.kKey);
            Assert.That(bootstrap.PlayerCombat.ActionState.CurrentSpec.Id, Is.EqualTo("bow-shot"));
            yield return WaitForIdle();
            yield return WaitForSeconds(0.6f);

            Assert.That(Object.FindAnyObjectByType<CharacterProjectile2D>(), Is.Null);
            Assert.That(bootstrap.PreviewTargetHealth.CurrentHealth, Is.EqualTo(startingHealth));
        }

        /// <summary>
        /// 的を倒すと表示が変わり、一定時間後にHPが戻ることを確認する。
        /// </summary>
        [UnityTest]
        public IEnumerator Target_IsDefeatedAndRestores()
        {
            yield return LoadPreview();
            var toTarget = DirectionToTarget();
            yield return PlaceNearTarget(1f, toTarget);
            var maximumHealth = bootstrap.PreviewTargetHealth.MaximumHealth;

            var swings = (maximumHealth + SwordDamage - 1) / SwordDamage;
            for (var swing = 0; swing < swings; swing++)
            {
                yield return TapKey(keyboard.jKey);
                yield return WaitForIdle();
            }

            Assert.That(bootstrap.PreviewTargetHealth.IsAlive, Is.False);
            Assert.That(bootstrap.CombatPresenter.LastFeedback, Does.Contain("的を倒した"));

            yield return WaitForSeconds(BattleRoyaleExplorationPreviewDebug.TargetRestoreSeconds + 0.3f);
            Assert.That(bootstrap.PreviewTargetHealth.CurrentHealth, Is.EqualTo(maximumHealth));
        }

        /// <summary>
        /// Playtest Sceneを読み込み、準備完了を待って仮想Keyboardを用意する。
        /// </summary>
        private IEnumerator LoadPreview()
        {
            var loadOperation = SceneManager.LoadSceneAsync(PlaytestScenePath, LoadSceneMode.Single);
            Assert.That(loadOperation, Is.Not.Null);
            while (!loadOperation.isDone)
            {
                yield return null;
            }

            bootstrap = Object.FindAnyObjectByType<BattleRoyaleExplorationPreviewDebug>();
            Assert.That(bootstrap, Is.Not.Null);
            var timeoutAt = Time.realtimeSinceStartup + 20f;
            while (!bootstrap.IsReady
                   && string.IsNullOrEmpty(bootstrap.FailureMessage)
                   && Time.realtimeSinceStartup < timeoutAt)
            {
                yield return null;
            }

            Assert.That(bootstrap.FailureMessage, Is.Empty);
            Assert.That(bootstrap.IsReady, Is.True);
            keyboard = InputSystem.AddDevice<Keyboard>();

            // Input SystemはEditを抜けてからPlayに入るまでの時刻の入力を捨てる。その時刻は
            // Editorの起動からの秒数で、Test用の時計も同じ範囲を通るため、確実に先へ進める。
            currentTime = InputClockStartSeconds;

            // 起動後に追加した仮想Keyboardは、Action Mapへ指定し直すまで入力の対象にならない。
            var actionMap = bootstrap.RuntimeInputActions.FindActionMap(
                BattleRoyaleExplorationPreviewDebug.PlayerActionMapName,
                true);
            actionMap.Disable();
            actionMap.devices = new InputDevice[] { keyboard };
            actionMap.Enable();
        }

        private Vector2 DirectionToTarget()
        {
            return ((Vector2)bootstrap.PreviewTargetObject.transform.position - bootstrap.PlayerBody.position).normalized;
        }

        /// <summary>
        /// 的から指定距離の手前へPlayerを置き、的の方を向かせる。
        /// </summary>
        private IEnumerator PlaceNearTarget(float distance, Vector2 toTarget)
        {
            bootstrap.PlayerBody.position =
                (Vector2)bootstrap.PreviewTargetObject.transform.position - toTarget * distance;
            yield return Face(toTarget);
        }

        /// <summary>
        /// 一瞬だけ移動入力を与えて向きを変え、すぐに止める。
        /// </summary>
        private IEnumerator Face(Vector2 direction)
        {
            bootstrap.SetMoveInputOverrideForTests(direction);
            yield return new WaitForFixedUpdate();
            bootstrap.ClearMoveInputOverrideForTests();
            yield return new WaitForFixedUpdate();
        }

        /// <summary>
        /// 仮想Keyboardを押して離す。UnityTestのPressはイベントを積むだけなので、
        /// 1 Frame進めて、本番のUpdateが押した瞬間を読めるようにする。
        /// </summary>
        private IEnumerator TapKey(UnityEngine.InputSystem.Controls.KeyControl key)
        {
            // Frameをまたぐと入力の状態Bufferが切り替わるため、Pressの前に現在の更新へ揃える。
            InputSystem.Update();
            Press(key);
            yield return null;
            Release(key);
            yield return null;
        }

        private IEnumerator WaitForIdle()
        {
            var timeoutAt = Time.realtimeSinceStartup + 3f;
            while (bootstrap.PlayerCombat.ActionState.IsBusy && Time.realtimeSinceStartup < timeoutAt)
            {
                yield return new WaitForFixedUpdate();
            }

            Assert.That(bootstrap.PlayerCombat.ActionState.IsBusy, Is.False);
        }

        private static IEnumerator WaitForSeconds(float seconds)
        {
            var endAt = Time.time + seconds;
            while (Time.time < endAt)
            {
                yield return null;
            }
        }
    }
}
