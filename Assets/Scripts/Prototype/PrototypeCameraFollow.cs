using UnityEngine;

namespace FantasyRoyale.Prototype
{
    /// <summary>
    /// 広いプロトタイプマップを探索できるよう、プレイヤーを追いかける簡易カメラ。
    /// </summary>
    public sealed class PrototypeCameraFollow : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private float followSpeed = 8f;
        [SerializeField] private Vector2 minPosition = new(-28f, -16f);
        [SerializeField] private Vector2 maxPosition = new(28f, 16f);

        /// <summary>
        /// Scene生成時に追跡対象と移動可能範囲を設定する。
        /// </summary>
        public void Initialize(Transform target, Vector2 minPosition, Vector2 maxPosition)
        {
            this.target = target;
            this.minPosition = minPosition;
            this.maxPosition = maxPosition;
        }

        private void LateUpdate()
        {
            if (target == null)
            {
                return;
            }

            var desired = new Vector3(
                Mathf.Clamp(target.position.x, minPosition.x, maxPosition.x),
                Mathf.Clamp(target.position.y, minPosition.y, maxPosition.y),
                transform.position.z);

            transform.position = Vector3.Lerp(transform.position, desired, 1f - Mathf.Exp(-followSpeed * Time.deltaTime));
        }
    }
}
