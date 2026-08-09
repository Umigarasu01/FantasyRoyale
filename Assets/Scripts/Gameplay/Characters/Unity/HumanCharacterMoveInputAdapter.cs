using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace FantasyRoyale.Gameplay.Characters.Unity
{
    /// <summary>
    /// Unity Input Systemの人間操作を、CPUや将来のNetwork入力と共有する移動Commandへ変換するAdapter。
    /// </summary>
    public sealed class HumanCharacterMoveInputAdapter : IDisposable
    {
        private readonly InputAction moveAction;
        private bool isEnabled;

        /// <summary>
        /// Vector2を返すMove Actionを受け取り、Assetの所有権を奪わず入力変換だけを担当する。
        /// </summary>
        public HumanCharacterMoveInputAdapter(InputAction moveAction)
        {
            this.moveAction = moveAction ?? throw new ArgumentNullException(nameof(moveAction));
        }

        public InputAction MoveAction => moveAction;
        public bool IsEnabled => isEnabled;

        /// <summary>
        /// 入力元の利用開始時にMove Actionを有効化する。
        /// </summary>
        public void Enable()
        {
            if (isEnabled)
            {
                return;
            }

            moveAction.Enable();
            isEnabled = true;
        }

        /// <summary>
        /// 現在の人間入力を読み、斜め速度を共通Command側で正規化して返す。
        /// </summary>
        public CharacterMoveCommand ReadMoveCommand()
        {
            if (!isEnabled)
            {
                return CharacterMoveCommand.None;
            }

            var value = moveAction.ReadValue<Vector2>();
            return new CharacterMoveCommand(value.x, value.y);
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

            moveAction.Disable();
            isEnabled = false;
        }
    }
}
