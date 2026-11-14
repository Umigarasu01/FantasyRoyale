using System;
using System.Collections.Generic;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace FantasyRoyale.Gameplay.Characters.Unity
{
    /// <summary>
    /// 1フレームに押された、所持品と画面の操作。押した瞬間だけ立つ。
    /// </summary>
    public readonly struct HumanInventoryInput
    {
        public HumanInventoryInput(
            bool nextMagic,
            bool useItem,
            bool nextItem,
            bool toggleInventory,
            int usedQuickSlot,
            bool menuUp,
            bool menuDown,
            bool menuLeft,
            bool menuRight,
            bool menuConfirm = false)
        {
            NextMagic = nextMagic;
            UseItem = useItem;
            NextItem = nextItem;
            ToggleInventory = toggleInventory;
            UsedQuickSlot = usedQuickSlot;
            MenuUp = menuUp;
            MenuDown = menuDown;
            MenuLeft = menuLeft;
            MenuRight = menuRight;
            MenuConfirm = menuConfirm;
        }

        public bool NextMagic { get; }
        public bool UseItem { get; }
        public bool NextItem { get; }
        public bool ToggleInventory { get; }

        /// <summary>
        /// 数字キーで使う使用枠の番号（0から）。押していなければ-1である。
        /// </summary>
        public int UsedQuickSlot { get; }

        public bool MenuUp { get; }
        public bool MenuDown { get; }
        public bool MenuLeft { get; }
        public bool MenuRight { get; }

        /// <summary>画面で選んだ物を決める（着ける、外す、使う、買う）。U、Enter。</summary>
        public bool MenuConfirm { get; }
    }

    /// <summary>
    /// 魔法の切り替え、アイテムを使う、切り替える、持ち物の画面、数字キーでアイテムを使う、画面の上下左右を読むAdapter。
    /// 唱える操作は攻撃と同じAction Commandなので`HumanCharacterActionInputAdapter`が読む。
    /// 画面の上下左右はI、J、K、Lで、開いている間だけ呼び出し側が使い、攻撃や魔法の操作と重なっても構わない。
    /// </summary>
    public sealed class HumanInventoryInputAdapter : IDisposable
    {
        private readonly InputAction[] actions;
        private readonly InputAction nextMagic;
        private readonly InputAction useItem;
        private readonly InputAction nextItem;
        private readonly InputAction toggleInventory;
        private readonly InputAction useQuickSlot;
        private readonly InputAction menuUp;
        private readonly InputAction menuDown;
        private readonly InputAction menuLeft;
        private readonly InputAction menuRight;
        private readonly InputAction menuConfirm;
        private bool isEnabled;

        /// <summary>
        /// 各操作のActionを受け取る。Assetの所有権は持たない。
        /// </summary>
        public HumanInventoryInputAdapter(
            InputAction nextMagic,
            InputAction useItem,
            InputAction nextItem,
            InputAction toggleInventory,
            InputAction useQuickSlot,
            InputAction menuUp,
            InputAction menuDown,
            InputAction menuLeft,
            InputAction menuRight,
            InputAction menuConfirm = null)
        {
            this.nextMagic = nextMagic ?? throw new ArgumentNullException(nameof(nextMagic));
            this.useItem = useItem ?? throw new ArgumentNullException(nameof(useItem));
            this.nextItem = nextItem ?? throw new ArgumentNullException(nameof(nextItem));
            this.toggleInventory = toggleInventory ?? throw new ArgumentNullException(nameof(toggleInventory));
            this.useQuickSlot = useQuickSlot ?? throw new ArgumentNullException(nameof(useQuickSlot));
            this.menuUp = menuUp ?? throw new ArgumentNullException(nameof(menuUp));
            this.menuDown = menuDown ?? throw new ArgumentNullException(nameof(menuDown));
            this.menuLeft = menuLeft ?? throw new ArgumentNullException(nameof(menuLeft));
            this.menuRight = menuRight ?? throw new ArgumentNullException(nameof(menuRight));
            this.menuConfirm = menuConfirm;
            var all = new List<InputAction> { nextMagic, useItem, nextItem, toggleInventory, useQuickSlot, menuUp, menuDown, menuLeft, menuRight };
            if (menuConfirm != null)
            {
                all.Add(menuConfirm);
            }

            actions = all.ToArray();
        }

        public void Enable()
        {
            if (isEnabled)
            {
                return;
            }

            foreach (var action in actions)
            {
                action.Enable();
            }

            isEnabled = true;
        }

        /// <summary>
        /// このフレームに押された操作をまとめて返す。無効の間は何も押されていない値を返す。
        /// </summary>
        public HumanInventoryInput Read()
        {
            if (!isEnabled)
            {
                return new HumanInventoryInput(false, false, false, false, -1, false, false, false, false);
            }

            return new HumanInventoryInput(
                nextMagic.WasPressedThisFrame(),
                useItem.WasPressedThisFrame(),
                nextItem.WasPressedThisFrame(),
                toggleInventory.WasPressedThisFrame(),
                ReadBagSlot(),
                menuUp.WasPressedThisFrame(),
                menuDown.WasPressedThisFrame(),
                menuLeft.WasPressedThisFrame(),
                menuRight.WasPressedThisFrame(),
                menuConfirm != null && menuConfirm.WasPressedThisFrame());
        }

        public void Dispose()
        {
            if (!isEnabled)
            {
                return;
            }

            foreach (var action in actions)
            {
                action.Disable();
            }

            isEnabled = false;
        }

        // 1つのActionに1〜9の数字キーを並べてあるので、押したキーから番号を出す。
        private int ReadBagSlot()
        {
            if (!useQuickSlot.WasPressedThisFrame() || !(useQuickSlot.activeControl is KeyControl key))
            {
                return -1;
            }

            return key.keyCode >= Key.Digit1 && key.keyCode <= Key.Digit9 ? key.keyCode - Key.Digit1 : -1;
        }
    }
}
