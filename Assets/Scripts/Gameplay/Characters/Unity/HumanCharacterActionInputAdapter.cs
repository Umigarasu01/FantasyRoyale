using System;
using UnityEngine.InputSystem;

namespace FantasyRoyale.Gameplay.Characters.Unity
{
    /// <summary>
    /// Unity Input Systemの人間操作を、CPUや将来のNetwork入力と共有するAction Commandへ変換するAdapter。
    /// </summary>
    public sealed class HumanCharacterActionInputAdapter : IDisposable
    {
        private readonly InputAction primaryAction;
        private readonly InputAction specialAction;
        private bool isEnabled;

        /// <summary>
        /// 通常攻撃と特殊ActionのButtonを受け取り、Assetの所有権を奪わず入力変換だけを担当する。
        /// </summary>
        public HumanCharacterActionInputAdapter(InputAction primaryAction, InputAction specialAction)
        {
            this.primaryAction = primaryAction ?? throw new ArgumentNullException(nameof(primaryAction));
            this.specialAction = specialAction ?? throw new ArgumentNullException(nameof(specialAction));
        }

        /// <summary>
        /// 入力元の利用開始時にActionを有効化する。
        /// </summary>
        public void Enable()
        {
            if (isEnabled)
            {
                return;
            }

            primaryAction.Enable();
            specialAction.Enable();
            isEnabled = true;
        }

        /// <summary>
        /// Buttonを押した瞬間だけ、現在の向きを持つCommandを返す。同時押しは通常攻撃を優先する。
        /// </summary>
        public bool TryReadCommand(CharacterDirection8 facing, out CharacterActionCommand command)
        {
            command = default;
            if (!isEnabled)
            {
                return false;
            }

            if (primaryAction.WasPressedThisFrame())
            {
                command = new CharacterActionCommand(CharacterActionSlot.Primary, facing);
                return true;
            }

            if (specialAction.WasPressedThisFrame())
            {
                command = new CharacterActionCommand(CharacterActionSlot.Special, facing);
                return true;
            }

            return false;
        }

        /// <summary>
        /// 入力元の利用終了時にActionを無効化し、Runtime複製Assetの破棄前に状態を戻す。
        /// </summary>
        public void Dispose()
        {
            if (!isEnabled)
            {
                return;
            }

            primaryAction.Disable();
            specialAction.Disable();
            isEnabled = false;
        }
    }
}
