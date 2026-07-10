using UnityEngine;

namespace FantasyRoyale.Unity.Exploration
{
    /// <summary>
    /// 探索操作の本番想定プレイヤー移動。
    /// 入力取得とは分離し、将来CPUやネットワーク入力にも同じ移動処理を使える形にする。
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public sealed class PlayerMotor2D : MonoBehaviour
    {
        [SerializeField] private float moveSpeed = 5f;

        private Rigidbody2D body;
        private Vector2 moveInput;

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            body.freezeRotation = true;
        }

        public void SetMoveInput(Vector2 input)
        {
            moveInput = Vector2.ClampMagnitude(input, 1f);
        }

        private void FixedUpdate()
        {
            body.MovePosition(body.position + moveInput * moveSpeed * Time.fixedDeltaTime);
        }
    }
}
