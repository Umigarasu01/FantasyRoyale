using System;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace FantasyRoyale.MapAuthoringKit
{
    /// <summary>
    /// 見た目の材質が異なる道路Tile同士を、共通の接続グループで一続きとして扱うRuleTile。
    /// DirtとStoneの境界でもNeighbor.Thisが成立するようにし、接続部へ草縁が閉じることを防ぐ。
    /// </summary>
    [CreateAssetMenu(
        fileName = "RoadConnectionRuleTile",
        menuName = "FantasyRoyale/Map Authoring/Road Connection Rule Tile")]
    public sealed class RoadConnectionRuleTile : RuleTile
    {
        private const int RoadVariantCount = 4;

        // 4セル区間の中で同じ輪郭を重ねず、区間ごとの並びだけをHashで切り替えるための全順列。
        private static readonly byte[,] FourVariantPermutations =
        {
            { 0, 1, 2, 3 }, { 0, 1, 3, 2 }, { 0, 2, 1, 3 }, { 0, 2, 3, 1 },
            { 0, 3, 1, 2 }, { 0, 3, 2, 1 }, { 1, 0, 2, 3 }, { 1, 0, 3, 2 },
            { 1, 2, 0, 3 }, { 1, 2, 3, 0 }, { 1, 3, 0, 2 }, { 1, 3, 2, 0 },
            { 2, 0, 1, 3 }, { 2, 0, 3, 1 }, { 2, 1, 0, 3 }, { 2, 1, 3, 0 },
            { 2, 3, 0, 1 }, { 2, 3, 1, 0 }, { 3, 0, 1, 2 }, { 3, 0, 2, 1 },
            { 3, 1, 0, 2 }, { 3, 1, 2, 0 }, { 3, 2, 0, 1 }, { 3, 2, 1, 0 },
        };

        [SerializeField] private string connectionGroup = string.Empty;
        [SerializeField] private int variationSeed;

        /// <summary>
        /// 相互接続を許可する道路グループ名を返す。空の場合は同じAsset自身だけへ接続する。
        /// </summary>
        public string ConnectionGroup => connectionGroup;

        /// <summary>
        /// Builderから道路グループ名を設定し、前後の空白を除いた安定した値として保存する。
        /// </summary>
        public void Configure(string group, int seed = 0)
        {
            connectionGroup = string.IsNullOrWhiteSpace(group)
                ? string.Empty
                : group.Trim();
            variationSeed = seed;
        }

        /// <summary>
        /// 標準RuleTileで接続Maskを決めた後、複数Spriteだけは座標Hashで選び直す。
        /// 4差分は横4セル内で重複させず、Perlinの偏りと同一輪郭の長い連続を同時に避ける。
        /// </summary>
        public override void GetTileData(Vector3Int position, ITilemap tilemap, ref TileData tileData)
        {
            base.GetTileData(position, tilemap, ref tileData);

            var transform = Matrix4x4.identity;
            for (var ruleIndex = 0; ruleIndex < m_TilingRules.Count; ruleIndex++)
            {
                var rule = m_TilingRules[ruleIndex];
                if (!RuleMatches(rule, position, tilemap, ref transform))
                {
                    continue;
                }

                if (rule.m_Sprites != null && rule.m_Sprites.Length > 1)
                {
                    tileData.sprite = rule.m_Sprites[
                        GetVariantIndex(position, rule.m_Sprites.Length, rule.m_Id)];
                }

                return;
            }
        }

        /// <summary>
        /// Cell座標、保存Seed、接続Maskから決定的Indexを返す。4差分では区間単位の順列を使う。
        /// </summary>
        public int GetVariantIndex(Vector3Int position, int variantCount, int ruleId)
        {
            if (variantCount <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(variantCount));
            }

            if (variantCount == RoadVariantCount)
            {
                // 横4セルを一組にして全差分を一度ずつ使う。負座標でも同じ区切りになるよう床除算する。
                var localX = position.x % RoadVariantCount;
                var blockX = position.x / RoadVariantCount;
                if (localX < 0)
                {
                    localX += RoadVariantCount;
                    blockX--;
                }

                var permutationIndex = (int)(Hash2D(
                    blockX,
                    position.y,
                    variationSeed,
                    ruleId) % (uint)FourVariantPermutations.GetLength(0));
                return FourVariantPermutations[permutationIndex, localX];
            }

            var hash = Hash2D(position.x, position.y, variationSeed, ruleId);
            return (int)(hash % (uint)variantCount);
        }

        /// <summary>
        /// 指定Tileが同じ道路接続グループかを判定する。
        /// 同じAsset自身はグループ未設定でも接続し、RuleOverrideTileは実体のRuleTileを参照する。
        /// </summary>
        public bool CanConnectTo(TileBase other)
        {
            var candidate = UnwrapOverrideTile(other);
            if (ReferenceEquals(candidate, this))
            {
                return true;
            }

            if (string.IsNullOrEmpty(connectionGroup)
                || !(candidate is RoadConnectionRuleTile roadTile))
            {
                return false;
            }

            return string.Equals(
                connectionGroup,
                roadTile.connectionGroup,
                StringComparison.Ordinal);
        }

        /// <summary>
        /// 標準RuleTileのAsset同一性判定を道路グループ判定へ置き換え、ThisとNotThisを対称に評価する。
        /// </summary>
        public override bool RuleMatch(int neighbor, TileBase other)
        {
            switch (neighbor)
            {
                case TilingRuleOutput.Neighbor.This:
                    return CanConnectTo(other);
                case TilingRuleOutput.Neighbor.NotThis:
                    return !CanConnectTo(other);
                default:
                    return base.RuleMatch(neighbor, other);
            }
        }

        /// <summary>
        /// RuleOverrideTileが渡された場合に、接続設定を保持する生成済み実体または元Tileへ解決する。
        /// </summary>
        private static TileBase UnwrapOverrideTile(TileBase tile)
        {
            if (!(tile is RuleOverrideTile overrideTile))
            {
                return tile;
            }

            return overrideTile.m_InstanceTile != null
                ? overrideTile.m_InstanceTile
                : overrideTile.m_Tile;
        }

        /// <summary>
        /// 符号付きXYとMaskを32bitへ拡散し、短い道路でも差分が局所的に偏らないHashを作る。
        /// </summary>
        private static uint Hash2D(int x, int y, int seed, int ruleId)
        {
            unchecked
            {
                var hash = 0x9E3779B9u ^ (uint)seed;
                hash ^= (uint)x * 0x85EBCA6Bu;
                hash = RotateLeft(hash, 13);
                hash ^= (uint)y * 0xC2B2AE35u;
                hash = RotateLeft(hash, 17);
                hash ^= (uint)ruleId * 0x27D4EB2Fu;

                hash ^= hash >> 16;
                hash *= 0x7FEB352Du;
                hash ^= hash >> 15;
                hash *= 0x846CA68Bu;
                hash ^= hash >> 16;
                return hash;
            }
        }

        /// <summary>
        /// 32bit値を循環Shiftし、XとYが同じbit位置へ偏らないように混合する。
        /// </summary>
        private static uint RotateLeft(uint value, int count)
        {
            return value << count | value >> (32 - count);
        }
    }
}
