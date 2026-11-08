using System;
using System.Collections.Generic;
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
        private const float SlashPixelsPerUnit = 64f;
        private const float SlashThicknessPixels = 12f;
        private const float SlashRimPixels = 2f;

        // 判定の起点は足元で届く距離も長いので、斬撃は胸の高さで剣先に近い大きさへ縮めて見せる。
        private const float SlashRangeShare = 0.7f;
        private const float SlashChestHeight = 0.45f;
        private const float SlashDisplaySeconds = 0.12f;
        private const float HitFlashSeconds = 0.12f;
        private const float ArrowPixelsPerUnit = 12f;

        private static readonly Color HitColor = new Color(1f, 0.35f, 0.3f);
        private static readonly Color DefeatedColor = new Color(0.45f, 0.45f, 0.45f);

        private readonly CharacterCombat2D combat;
        private readonly CharacterHurtbox2D target;
        private readonly SpriteRenderer targetRenderer;
        private readonly Dictionary<(float Range, float Arc), Sprite> slashSprites = new Dictionary<(float Range, float Arc), Sprite>();
        private readonly Texture2D arrowTexture;
        private readonly Sprite arrowSprite;
        private readonly bool showsSlash;
        private float flashRemaining;

        public string LastFeedback { get; private set; } = string.Empty;
        public int SlashEffectCount { get; private set; }

        /// <summary>
        /// 的を持たず、斬撃と矢だけを表示する。体のコマが残像を持つなら、斬撃は出さない。
        /// </summary>
        public PreviewDebugCombatPresenter(CharacterCombat2D combat, bool showsSlash = true)
            : this(combat, null, null, showsSlash)
        {
        }

        /// <summary>
        /// 攻撃のEventを購読し、確認用のSpriteを作る。
        /// </summary>
        public PreviewDebugCombatPresenter(
            CharacterCombat2D combat,
            CharacterHurtbox2D target,
            SpriteRenderer targetRenderer,
            bool showsSlash = true)
        {
            this.showsSlash = showsSlash;
            this.combat = combat;
            this.target = target;
            this.targetRenderer = targetRenderer;
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
            foreach (var sprite in slashSprites.Values)
            {
                Object.Destroy(sprite.texture);
                Object.Destroy(sprite);
            }

            Object.Destroy(arrowSprite);
            Object.Destroy(arrowTexture);
        }

        /// <summary>
        /// 近接Actionの発動時に、扇の幅の細い斬撃を短時間だけ表示する。走っていても体についていく。
        /// </summary>
        private void OnActionActivated(CharacterActionSpec spec, CharacterDirection8 direction)
        {
            if (spec.Kind != CharacterActionKind.Melee || !showsSlash)
            {
                return;
            }

            var effect = new GameObject("SlashPreviewDebug");
            effect.transform.position = combat.AttackOrigin + Vector2.up * SlashChestHeight;
            effect.transform.rotation = Quaternion.Euler(0f, 0f, (int)direction * 45f);
            effect.transform.SetParent(combat.transform, true);
            var renderer = effect.AddComponent<SpriteRenderer>();
            renderer.sprite = GetSlashSprite(spec.Range, spec.ArcDegrees);
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
        /// 攻撃距離と扇の幅ごとの斬撃を返す。同じ組み合わせなら作り直さない。
        /// </summary>
        private Sprite GetSlashSprite(float range, float arcDegrees)
        {
            if (slashSprites.TryGetValue((range, arcDegrees), out var sprite))
            {
                return sprite;
            }

            var texture = CreateSlashTexture(range, arcDegrees);
            sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f),
                SlashPixelsPerUnit,
                0,
                SpriteMeshType.FullRect);
            slashSprites.Add((range, arcDegrees), sprite);
            return sprite;
        }

        /// <summary>
        /// 東向きの三日月形の弧を、体と同じドットの細かさで描く。中央が太く、両端は細くなる。向きは回転で変える。
        /// </summary>
        private static Texture2D CreateSlashTexture(float range, float arcDegrees)
        {
            var outer = range * SlashRangeShare * SlashPixelsPerUnit;
            var size = Mathf.CeilToInt(outer) * 2 + 2;
            var half = arcDegrees * 0.5f;
            var pixels = new Color32[size * size];
            var centre = (size - 1) * 0.5f;
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var offset = new Vector2(x - centre, y - centre);
                    var share = Mathf.Abs(Mathf.Atan2(offset.y, offset.x) * Mathf.Rad2Deg) / half;
                    var radius = offset.magnitude;
                    var inner = outer - SlashThicknessPixels * (1f - share * share);
                    if (share > 1f || radius > outer || radius < inner)
                    {
                        continue;
                    }

                    pixels[y * size + x] = radius >= outer - SlashRimPixels
                        ? new Color32(250, 253, 255, 235)
                        : new Color32(190, 225, 255, 150);
                }
            }

            return CreateTexture(size, size, pixels, "SlashPreviewDebugTexture");
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
