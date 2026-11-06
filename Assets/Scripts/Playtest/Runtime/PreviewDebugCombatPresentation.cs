using System;
using FantasyRoyale.Gameplay.Characters;
using FantasyRoyale.Gameplay.Characters.Unity;
using UnityEngine;
using Object = UnityEngine.Object;

namespace FantasyRoyale.Playtest
{
    /// <summary>
    /// 攻撃の結果を確認用の仮表示へ変換する一時Presenter。
    /// 正式なAnimationとEffectができるまで、Runtime生成Spriteで斬撃、矢、被弾を見せる。
    /// </summary>
    public sealed class PreviewDebugCombatPresenter : IDisposable
    {
        private const string EffectSortingLayerName = "MapForeground";
        private const int SlashTextureSize = 16;
        private const float SlashRadiusPixels = 7.5f;
        private const float SlashDisplaySeconds = 0.16f;
        private const float HitFlashSeconds = 0.12f;
        private const float ArrowPixelsPerUnit = 12f;

        private static readonly Color HitColor = new Color(1f, 0.35f, 0.3f);
        private static readonly Color DefeatedColor = new Color(0.45f, 0.45f, 0.45f);

        private readonly CharacterCombat2D combat;
        private readonly CharacterHurtbox2D target;
        private readonly SpriteRenderer targetRenderer;
        private readonly Texture2D slashTexture;
        private readonly Texture2D arrowTexture;
        private readonly Sprite arrowSprite;
        private Sprite slashSprite;
        private float slashPixelsPerUnit;
        private float flashRemaining;

        public string LastFeedback { get; private set; } = string.Empty;
        public int SlashEffectCount { get; private set; }

        /// <summary>
        /// 的を持たず、斬撃と矢だけを表示する。
        /// </summary>
        public PreviewDebugCombatPresenter(CharacterCombat2D combat)
            : this(combat, null, null)
        {
        }

        /// <summary>
        /// 攻撃のEventを購読し、確認用のSpriteを作る。
        /// </summary>
        public PreviewDebugCombatPresenter(
            CharacterCombat2D combat,
            CharacterHurtbox2D target,
            SpriteRenderer targetRenderer)
        {
            this.combat = combat;
            this.target = target;
            this.targetRenderer = targetRenderer;
            slashTexture = CreateSlashTexture();
            arrowTexture = CreateArrowTexture();
            arrowSprite = Sprite.Create(
                arrowTexture,
                new Rect(0f, 0f, arrowTexture.width, arrowTexture.height),
                new Vector2(0.5f, 0.5f),
                ArrowPixelsPerUnit,
                0,
                SpriteMeshType.FullRect);
            combat.ActionActivated += OnActionActivated;
            combat.HitApplied += OnHitApplied;
            combat.ProjectileLaunched += OnProjectileLaunched;
        }

        /// <summary>
        /// 被弾の点滅を進め、的の色を現在の状態へ合わせる。
        /// </summary>
        public void Tick(float deltaSeconds)
        {
            flashRemaining = Mathf.Max(0f, flashRemaining - deltaSeconds);
            if (targetRenderer == null)
            {
                return;
            }

            targetRenderer.color = flashRemaining > 0f
                ? HitColor
                : target.Target != null && target.Target.IsAlive
                    ? Color.white
                    : DefeatedColor;
        }

        /// <summary>
        /// 購読を解除し、Runtime生成したTextureとSpriteを破棄する。
        /// </summary>
        public void Dispose()
        {
            combat.ActionActivated -= OnActionActivated;
            combat.HitApplied -= OnHitApplied;
            combat.ProjectileLaunched -= OnProjectileLaunched;
            Object.Destroy(slashSprite);
            Object.Destroy(arrowSprite);
            Object.Destroy(slashTexture);
            Object.Destroy(arrowTexture);
        }

        /// <summary>
        /// 近接Actionの発動時に、攻撃範囲と同じ大きさの斬撃を短時間だけ表示する。
        /// </summary>
        private void OnActionActivated(CharacterActionSpec spec, CharacterDirection8 direction)
        {
            if (spec.Kind != CharacterActionKind.Melee)
            {
                return;
            }

            var effect = new GameObject("SlashPreviewDebug");
            effect.transform.position = combat.AttackOrigin;
            effect.transform.rotation = Quaternion.Euler(0f, 0f, (int)direction * 45f);
            var renderer = effect.AddComponent<SpriteRenderer>();
            renderer.sprite = GetSlashSprite(spec.Range);
            renderer.sortingLayerName = EffectSortingLayerName;
            Object.Destroy(effect, SlashDisplaySeconds);
            SlashEffectCount++;
        }

        private void OnHitApplied(CharacterHitEvent hit)
        {
            flashRemaining = HitFlashSeconds;
            LastFeedback = hit.Result.BecameDefeated
                ? $"{hit.Spec.Id}: {hit.Result.AppliedAmount} ダメージ / 的を倒した"
                : $"{hit.Spec.Id}: {hit.Result.AppliedAmount} ダメージ";
        }

        private void OnProjectileLaunched(CharacterProjectile2D projectile)
        {
            var angle = Mathf.Atan2(projectile.Direction.y, projectile.Direction.x) * Mathf.Rad2Deg;
            projectile.transform.rotation = Quaternion.Euler(0f, 0f, angle);
            var renderer = projectile.gameObject.AddComponent<SpriteRenderer>();
            renderer.sprite = arrowSprite;
            renderer.sortingLayerName = EffectSortingLayerName;
        }

        /// <summary>
        /// 攻撃距離に応じてSpriteの縮尺を決める。同じ距離なら作り直さない。
        /// </summary>
        private Sprite GetSlashSprite(float range)
        {
            var pixelsPerUnit = SlashRadiusPixels / range;
            if (slashSprite != null && Mathf.Approximately(slashPixelsPerUnit, pixelsPerUnit))
            {
                return slashSprite;
            }

            Object.Destroy(slashSprite);
            slashPixelsPerUnit = pixelsPerUnit;
            slashSprite = Sprite.Create(
                slashTexture,
                new Rect(0f, 0f, SlashTextureSize, SlashTextureSize),
                new Vector2(0.5f, 0.5f),
                pixelsPerUnit,
                0,
                SpriteMeshType.FullRect);
            return slashSprite;
        }

        /// <summary>
        /// 東向きの120度の弧を持つTextureを描く。向きは回転で変える。
        /// </summary>
        private static Texture2D CreateSlashTexture()
        {
            var pixels = new Color32[SlashTextureSize * SlashTextureSize];
            var centre = (SlashTextureSize - 1) * 0.5f;
            for (var y = 0; y < SlashTextureSize; y++)
            {
                for (var x = 0; x < SlashTextureSize; x++)
                {
                    var offset = new Vector2(x - centre, y - centre);
                    var angle = Mathf.Abs(Mathf.Atan2(offset.y, offset.x) * Mathf.Rad2Deg);
                    var radius = offset.magnitude;
                    var inside = angle <= 60f && radius >= 4.5f && radius <= SlashRadiusPixels;
                    pixels[y * SlashTextureSize + x] = inside
                        ? new Color32(240, 250, 255, 220)
                        : new Color32(0, 0, 0, 0);
                }
            }

            return CreateTexture(SlashTextureSize, SlashTextureSize, pixels, "SlashPreviewDebugTexture");
        }

        /// <summary>
        /// 東向きの矢を描く。
        /// </summary>
        private static Texture2D CreateArrowTexture()
        {
            const int width = 8;
            const int height = 3;
            var clear = new Color32(0, 0, 0, 0);
            var shaft = new Color32(62, 36, 22, 255);
            var tip = new Color32(230, 235, 240, 255);
            var pixels = new Color32[width * height];
            for (var index = 0; index < pixels.Length; index++)
            {
                pixels[index] = clear;
            }

            for (var x = 0; x < width - 1; x++)
            {
                pixels[width + x] = shaft;
            }

            pixels[width + width - 1] = tip;
            pixels[width - 2] = tip;
            pixels[width * 2 + width - 2] = tip;
            return CreateTexture(width, height, pixels, "ArrowPreviewDebugTexture");
        }

        private static Texture2D CreateTexture(int width, int height, Color32[] pixels, string name)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = name,
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }
    }
}
