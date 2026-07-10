using UnityEngine;

namespace FantasyRoyale.Unity.Exploration
{
    /// <summary>
    /// 探索用の2Dカメラ追従。
    /// マップ外を見せすぎないよう、指定したワールド範囲で追従位置を制限する。
    /// </summary>
    public sealed class CameraFollow2D : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private Vector2 minPosition = new Vector2(-48f, -36f);
        [SerializeField] private Vector2 maxPosition = new Vector2(48f, 36f);
        [SerializeField] private float smoothing = 12f;

        public void Configure(Transform newTarget, Vector2 newMinPosition, Vector2 newMaxPosition)
        {
            target = newTarget;
            minPosition = newMinPosition;
            maxPosition = newMaxPosition;
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
            transform.position = Vector3.Lerp(transform.position, desired, 1f - Mathf.Exp(-smoothing * Time.deltaTime));
        }
    }
}
