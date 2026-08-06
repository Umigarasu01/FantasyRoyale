using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace FantasyRoyale.MapAuthoringKit
{
    /// <summary>
    /// 8枚の地面Spriteから座標とSeedだけで表示差分を選び、再生成しても同じCellへ同じ見た目を返すTile。
    /// 短周期の剰余パターンを避けるため、符号付き座標を32bit avalanche hashへ直接混合する。
    /// </summary>
    [CreateAssetMenu(
        fileName = "GroundVariationTile",
        menuName = "FantasyRoyale/Map Authoring/Ground Variation Tile")]
    public sealed class GroundVariationTile : TileBase
    {
        public const int RequiredVariantCount = 8;

        [SerializeField] private Sprite[] variants = new Sprite[RequiredVariantCount];
        [SerializeField] private int seed;
        [NonSerialized] private IReadOnlyList<Sprite> readOnlyVariants;

        public int VariantCount => variants?.Length ?? 0;

        /// <summary>
        /// 保存中のSprite参照を配列変更できないViewとして返し、呼び出し側による要素差し替えを防ぐ。
        /// </summary>
        public IReadOnlyList<Sprite> Variants
        {
            get
            {
                if (readOnlyVariants == null)
                {
                    readOnlyVariants = Array.AsReadOnly(variants ?? Array.Empty<Sprite>());
                }

                return readOnlyVariants;
            }
        }

        /// <summary>
        /// 8枚の表示Spriteと選択Seedを受け取り、外部配列の後変更に影響されない参照配列として保存する。
        /// </summary>
        public void Configure(Sprite[] sprites, int variationSeed)
        {
            if (sprites == null)
            {
                throw new ArgumentNullException(nameof(sprites));
            }

            if (sprites.Length != RequiredVariantCount)
            {
                throw new ArgumentException(
                    $"地面差分Spriteは{RequiredVariantCount}枚必要です: {sprites.Length}",
                    nameof(sprites));
            }

            for (var index = 0; index < sprites.Length; index++)
            {
                if (sprites[index] == null)
                {
                    throw new ArgumentException(
                        $"地面差分Spriteにnullが含まれています: index={index}",
                        nameof(sprites));
                }
            }

            variants = (Sprite[])sprites.Clone();
            seed = variationSeed;
            readOnlyVariants = null;
        }

        /// <summary>
        /// Cell座標と保存済みSeedを受け取り、0以上8未満の決定的なSprite indexを返す。
        /// Zは2D地面の選択へ影響させず、同じXY座標では常に同じ差分を選ぶ。
        /// </summary>
        public int GetVariantIndex(Vector3Int position)
        {
            var hash = Hash2D(position.x, position.y, seed);
            return (int)(hash & (RequiredVariantCount - 1u));
        }

        /// <summary>
        /// Tilemapから要求されたCellへ選択済みSpriteと固定描画設定を返し、Colliderや任意Transformを持たせない。
        /// </summary>
        public override void GetTileData(Vector3Int position, ITilemap tilemap, ref TileData tileData)
        {
            var index = GetVariantIndex(position);
            tileData.sprite = variants != null && variants.Length == RequiredVariantCount
                ? variants[index]
                : null;
            tileData.color = Color.white;
            tileData.transform = Matrix4x4.identity;
            tileData.gameObject = null;
            tileData.flags = TileFlags.LockColor | TileFlags.LockTransform;
            tileData.colliderType = Tile.ColliderType.None;
        }

        /// <summary>
        /// X、Y、Seedを異なる奇数定数と回転で混合し、全bitへ差分を拡散する32bit 2D hashを生成する。
        /// オーバーフローは意図したmod 2^32演算であり、小さい座標差が短い軸周期へ戻らないよう最終avalancheを行う。
        /// </summary>
        private static uint Hash2D(int x, int y, int variationSeed)
        {
            unchecked
            {
                var hash = 0x9E3779B9u ^ (uint)variationSeed;
                hash ^= (uint)x * 0x85EBCA6Bu;
                hash = RotateLeft(hash, 13);
                hash ^= (uint)y * 0xC2B2AE35u;
                hash = RotateLeft(hash, 17);
                hash ^= (uint)variationSeed * 0x27D4EB2Fu;

                hash ^= hash >> 16;
                hash *= 0x7FEB352Du;
                hash ^= hash >> 15;
                hash *= 0x846CA68Bu;
                hash ^= hash >> 16;
                return hash;
            }
        }

        /// <summary>
        /// 32bit値を指定bit数だけ循環左Shiftし、座標ごとの情報を乗算前後で別bit位置へ移す。
        /// </summary>
        private static uint RotateLeft(uint value, int count)
        {
            return value << count | value >> (32 - count);
        }
    }
}
