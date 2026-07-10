using UnityEngine;

namespace FantasyRoyale.Unity.Exploration
{
    /// <summary>
    /// Milestone 1のキーボード入力経路。
    /// 操作デバイス追加時に差し替えやすいよう、移動処理本体とは分けている。
    /// </summary>
    [RequireComponent(typeof(PlayerMotor2D))]
    public sealed class KeyboardPlayerInput : MonoBehaviour
    {
        private PlayerMotor2D motor;

        private void Awake()
        {
            motor = GetComponent<PlayerMotor2D>();
        }

        private void Update()
        {
            var horizontal = Input.GetAxisRaw("Horizontal");
            var vertical = Input.GetAxisRaw("Vertical");
            motor.SetMoveInput(new Vector2(horizontal, vertical));
        }
    }
}
