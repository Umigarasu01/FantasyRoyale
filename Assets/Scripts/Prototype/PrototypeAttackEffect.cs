using UnityEngine;

namespace FantasyRoyale.Prototype
{
    /// <summary>
    /// 攻撃した瞬間だけ表示される仮の斬撃エフェクト。
    /// 判定処理とは分け、見た目だけを短時間で拡大、フェードアウトさせる。
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class PrototypeAttackEffect : MonoBehaviour
    {
        [SerializeField] private float duration = 0.16f;
        [SerializeField] private float startScale = 0.7f;
        [SerializeField] private float endScale = 1.25f;

        private SpriteRenderer spriteRenderer;
        private Color baseColor;
        private float elapsed;

        private void Awake()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            baseColor = spriteRenderer.color;
        }

        /// <summary>
        /// 攻撃方向を受け取り、斬撃の角度と寿命を初期化する。
        /// </summary>
        public void Play(Vector2 direction)
        {
            if (direction.sqrMagnitude < 0.001f)
            {
                direction = Vector2.down;
            }

            elapsed = 0f;
            direction.Normalize();
            transform.right = direction;
            transform.localScale = Vector3.one * startScale;

            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponent<SpriteRenderer>();
            }

            baseColor = spriteRenderer.color;
            spriteRenderer.color = new Color(baseColor.r, baseColor.g, baseColor.b, baseColor.a);
        }

        private void Update()
        {
            elapsed += Time.deltaTime;
            var t = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, duration));
            transform.localScale = Vector3.one * Mathf.Lerp(startScale, endScale, t);

            if (spriteRenderer != null)
            {
                var alpha = Mathf.Lerp(baseColor.a, 0f, t);
                spriteRenderer.color = new Color(baseColor.r, baseColor.g, baseColor.b, alpha);
            }

            if (elapsed >= duration)
            {
                Destroy(gameObject);
            }
        }
    }
}
