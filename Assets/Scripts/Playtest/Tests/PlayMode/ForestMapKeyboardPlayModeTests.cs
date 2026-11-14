using System.Collections;
using System.IO;
using FantasyRoyale.Gameplay.Characters;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace FantasyRoyale.Playtest.Tests.PlayMode
{
    /// <summary>
    /// 森の確認用Sceneを仮想Keyboardで操作し、所有者が決めたキー（2026-11-13）が入力のAssetから処理まで届くことを確かめる。
    /// 技はQ、魔法はE、魔法の切り替えはR、使用枠は数字キー、持ち物の画面はTabである。
    /// </summary>
    public sealed class ForestMapKeyboardPlayModeTests : InputTestFixture
    {
        private const string ScenePath = "Assets/ForestMapPreview/ForestMapPreviewDebug.unity";
        private const double InputClockStartSeconds = 1000000000d;

        private ForestMapPreviewDebug preview;
        private Keyboard keyboard;

        /// <summary>
        /// Eで魔法を唱えてMPが減り、Rで次の魔法へ切り替わり、Qで技を使うと待ち時間が始まり、数字の1で使用枠の回復薬を飲む。
        /// Tabで持ち物の画面を開き、開いている間はJで攻撃せず、もう一度Tabで閉じる。
        /// </summary>
        [UnityTest]
        public IEnumerator Keys_DriveMagicSkillQuickSlotsAndTheInventory()
        {
            yield return LoadForest();
            var fullMana = preview.Mana.Current;

            yield return TapKey(keyboard.eKey);
            Assert.That(preview.Mana.Current, Is.LessThan(fullMana), "Eで魔法を唱えます。");
            yield return WaitForIdle();

            preview.Inventory.TryAddToBag(new CharacterItem("ice", CharacterItemKind.Magic));
            Assert.That(preview.TryUseBagItem(0), Is.True);
            yield return TapKey(keyboard.rKey);
            Assert.That(preview.Inventory.MainWeapon.SelectedMagicIndex, Is.EqualTo(1), "Rで次の魔法へ切り替えます。");

            yield return TapKey(keyboard.qKey);
            Assert.That(preview.SkillCooldown.IsReady, Is.False, "Qで技を使い、待ち時間が始まります。");
            yield return WaitForIdle();

            preview.AddCoins(5);
            Assert.That(preview.TryBuyPotion(), Is.True);
            preview.PlayerHealth.ApplyDamage(40);
            var hurt = preview.PlayerHealth.CurrentHealth;
            yield return TapKey(keyboard.digit1Key);
            Assert.That(preview.PlayerHealth.CurrentHealth, Is.GreaterThan(hurt), "数字の1で使用枠の回復薬を飲みます。");
            Assert.That(preview.Inventory.GetQuickItem(0), Is.Null);

            // Tabで持ち物の画面を開き、開いている間はJで攻撃しない。もう一度Tabで閉じる。
            // 同じFixtureでSceneを読み直すと読み込みが終わらなかったので、1つのTestで続けて確かめる。
            yield return TapKey(keyboard.tabKey);
            Assert.That(preview.IsInventoryOpen, Is.True, "Tabで持ち物の画面を開きます。");

            yield return TapKey(keyboard.jKey);
            Assert.That(preview.PlayerCombat.ActionState.IsBusy, Is.False, "開いている間は攻撃しません。");

            yield return TapKey(keyboard.tabKey);
            Assert.That(preview.IsInventoryOpen, Is.False, "もう一度Tabで閉じます。");

            yield return TapKey(keyboard.jKey);
            Assert.That(preview.PlayerCombat.ActionState.IsBusy, Is.True, "閉じるとJで攻撃します。");
        }

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
            var timeoutAt = Time.realtimeSinceStartup + 30f;
            while (!load.isDone && Time.realtimeSinceStartup < timeoutAt)
            {
                yield return null;
            }

            Assert.That(load.isDone, Is.True, "Sceneの読み込みが終わりません。");
#endif
            preview = Object.FindAnyObjectByType<ForestMapPreviewDebug>();
            Assert.That(preview, Is.Not.Null);
            var readyBy = Time.realtimeSinceStartup + 30f;
            while (!preview.IsReady && Time.realtimeSinceStartup < readyBy)
            {
                yield return null;
            }

            Assert.That(preview.IsReady, Is.True, "森の確認用Sceneの準備が終わりません。");

            preview.SetMouseIgnoredForTests(true);
            keyboard = InputSystem.AddDevice<Keyboard>();

            // Input SystemはEditを抜けてからPlayに入るまでの時刻の入力を捨てる。Test用の時計も同じ範囲を通るので先へ進める。
            currentTime = InputClockStartSeconds;

            // 起動後に追加した仮想Keyboardは、Action Mapへ指定し直すまで入力の対象にならない。
            var actionMap = preview.RuntimeInputActions.FindActionMap("Player", true);
            actionMap.Disable();
            actionMap.devices = new InputDevice[] { keyboard };
            actionMap.Enable();
        }

        /// <summary>
        /// 仮想Keyboardを押して離す。Pressはイベントを積むだけなので、1 Frame進めて本番のUpdateに押した瞬間を読ませる。
        /// </summary>
        private IEnumerator TapKey(KeyControl key)
        {
            InputSystem.Update();
            Press(key);
            yield return null;
            Release(key);
            yield return null;
        }

        private IEnumerator WaitForIdle()
        {
            var timeoutAt = Time.realtimeSinceStartup + 3f;
            while (preview.PlayerCombat.ActionState.IsBusy && Time.realtimeSinceStartup < timeoutAt)
            {
                yield return new WaitForFixedUpdate();
            }
        }
    }
}
