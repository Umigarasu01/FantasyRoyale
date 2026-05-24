using UnityEngine;

namespace FantasyRoyale.Prototype
{
    /// <summary>
    /// 仮スプライトを軽く動かすための簡易アニメーション再生役。
    /// 本番Animatorへ移行する前に、プロトタイプの生き物感を確認するために使う。
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class PrototypeSpriteAnimator : MonoBehaviour
    {
        [SerializeField] private Sprite[] idleSprites;
        [SerializeField] private Sprite[] moveSprites;
        [SerializeField] private float framesPerSecond = 6f;

        private SpriteRenderer spriteRenderer;
        private PrototypePlayerController2D player;
        private PrototypeEnemy enemy;
        private float timer;
        private int frameIndex;

        private void Awake()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            player = GetComponent<PrototypePlayerController2D>();
            enemy = GetComponent<PrototypeEnemy>();
        }

        private void Update()
        {
            var sprites = IsMoving() && moveSprites != null && moveSprites.Length > 0 ? moveSprites : idleSprites;
            if (sprites == null || sprites.Length == 0)
            {
                return;
            }

            timer += Time.deltaTime;
            var frameDuration = 1f / Mathf.Max(1f, framesPerSecond);
            if (timer >= frameDuration)
            {
                timer -= frameDuration;
                frameIndex = (frameIndex + 1) % sprites.Length;
            }

            spriteRenderer.sprite = sprites[Mathf.Clamp(frameIndex, 0, sprites.Length - 1)];
        }

        /// <summary>
        /// 対象がプレイヤーか敵かを見て、移動中だけ歩行フレームへ切り替える。
        /// </summary>
        private bool IsMoving()
        {
            if (player != null)
            {
                return player.IsMoving;
            }

            if (enemy != null)
            {
                return enemy.IsMoving;
            }

            return false;
        }
    }
}
