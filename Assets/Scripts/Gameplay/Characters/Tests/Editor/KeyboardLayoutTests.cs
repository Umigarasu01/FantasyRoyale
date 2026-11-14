using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine.InputSystem;

namespace FantasyRoyale.Gameplay.Characters.Tests.Editor
{
    /// <summary>
    /// 所有者が決めたキーボードの配置（2026-11-13）が、入力のAssetに入っていることを確かめる。
    /// 技はQ、魔法はE、魔法の切り替えはR、アイテムは数字キーで使う。調べるはFへ移した。
    /// </summary>
    public sealed class KeyboardLayoutTests
    {
        private const string InputActionsPath = "Assets/InputSystem_Actions.inputactions";

        [TestCase("Attack", "<Keyboard>/j")]
        [TestCase("Special", "<Keyboard>/q")]
        [TestCase("CastMagic", "<Keyboard>/e")]
        [TestCase("NextMagic", "<Keyboard>/r")]
        [TestCase("Interact", "<Keyboard>/f")]
        [TestCase("Inventory", "<Keyboard>/tab")]
        [TestCase("Jump", "<Keyboard>/space")]
        [TestCase("MenuUp", "<Keyboard>/i")]
        [TestCase("MenuDown", "<Keyboard>/k")]
        [TestCase("MenuLeft", "<Keyboard>/j")]
        [TestCase("MenuRight", "<Keyboard>/l")]
        [TestCase("MenuConfirm", "<Keyboard>/u")]
        [TestCase("MenuConfirm", "<Keyboard>/enter")]
        public void PlayerMap_BindsTheDecidedKey(string actionName, string path)
        {
            var action = LoadPlayerAction(actionName);

            Assert.That(action.bindings.Select(binding => binding.path), Does.Contain(path));
        }

        /// <summary>
        /// 1〜9の数字キーは1つのActionに並び、その番号の使用枠の物を使う。;とPはキーボードに割り当てない。
        /// </summary>
        [Test]
        public void PlayerMap_UsesBagSlotsWithDigits()
        {
            var paths = LoadPlayerAction("UseQuickSlot").bindings.Select(binding => binding.path).ToArray();

            for (var digit = 1; digit <= 9; digit++)
            {
                Assert.That(paths, Does.Contain($"<Keyboard>/{digit}"));
            }

            Assert.That(LoadPlayerAction("UseItem").bindings.Select(binding => binding.path), Does.Not.Contain("<Keyboard>/semicolon"));
            Assert.That(LoadPlayerAction("NextItem").bindings.Select(binding => binding.path), Does.Not.Contain("<Keyboard>/p"));
        }

        private static InputAction LoadPlayerAction(string actionName)
        {
            var asset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath);
            Assert.That(asset, Is.Not.Null);
            var action = asset.FindActionMap("Player", true).FindAction(actionName);
            Assert.That(action, Is.Not.Null, actionName);
            return action;
        }
    }
}
