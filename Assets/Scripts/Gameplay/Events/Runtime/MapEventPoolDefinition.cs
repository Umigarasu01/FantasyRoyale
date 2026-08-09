using System;
using System.Collections.Generic;
using FantasyRoyale.MapAuthoringKit;
using UnityEngine;

namespace FantasyRoyale.Gameplay.Events
{
    /// <summary>
    /// 一つのEvent定義と抽選重みをInspector編集可能な形で保存するPool項目。
    /// </summary>
    [Serializable]
    public sealed class MapEventPoolEntry
    {
        [SerializeField] private MapEventDefinition eventDefinition;
        [SerializeField, Min(1)] private int weight = 1;

        /// <summary>
        /// Event定義と正の抽選重みを受け取り、Poolへ保存できる項目を作る。
        /// </summary>
        public MapEventPoolEntry(MapEventDefinition eventDefinition, int weight)
        {
            this.eventDefinition = eventDefinition;
            this.weight = weight;
        }

        public MapEventDefinition EventDefinition => eventDefinition;
        public int Weight => weight;
    }

    /// <summary>
    /// 対戦で抽選可能なEvent候補、重み、有効Socket数を保存するScriptableObject。
    /// Seedや抽選結果は対戦状態なので保持せず、Assetを実行中に変更しない。
    /// </summary>
    [CreateAssetMenu(
        fileName = "MapEventPoolDefinition",
        menuName = "FantasyRoyale/Gameplay/Map Event Pool")]
    public sealed class MapEventPoolDefinition : ScriptableObject
    {
        [SerializeField] private string poolId = string.Empty;
        [SerializeField, Min(0)] private int activeSocketCount;
        [SerializeField] private List<MapEventPoolEntry> entries = new List<MapEventPoolEntry>();

        [NonSerialized] private Dictionary<string, MapEventDefinition> definitionLookup;

        public string PoolId => poolId;
        public int ActiveSocketCount => activeSocketCount;
        public IReadOnlyList<MapEventPoolEntry> Entries => entries;

        /// <summary>
        /// Editor BuilderやTestから、Poolの保存値を一括して設定する。
        /// </summary>
        public void Configure(
            string newPoolId,
            int newActiveSocketCount,
            IEnumerable<MapEventPoolEntry> newEntries)
        {
            poolId = NormalizeId(newPoolId);
            activeSocketCount = Math.Max(0, newActiveSocketCount);
            entries = newEntries == null
                ? new List<MapEventPoolEntry>()
                : new List<MapEventPoolEntry>(newEntries);
            definitionLookup = null;
        }

        /// <summary>
        /// 保存Listを検証し、純C#抽選器へ渡すIDと重みだけの候補へ変換する。
        /// </summary>
        public bool TryBuildRuntimeCandidates(
            out IReadOnlyList<MapEventPoolCandidate> candidates,
            out string error)
        {
            if (!Validate(out error))
            {
                candidates = Array.Empty<MapEventPoolCandidate>();
                return false;
            }

            var result = new MapEventPoolCandidate[entries.Count];
            for (var index = 0; index < entries.Count; index++)
            {
                var entry = entries[index];
                result[index] = new MapEventPoolCandidate(
                    entry.EventDefinition.EventDefinitionId,
                    entry.Weight);
            }

            candidates = result;
            return true;
        }

        /// <summary>
        /// 抽選結果のEvent Definition IDを、保存Listから作ったRuntime Dictionaryで参照へ戻す。
        /// </summary>
        public bool TryGetDefinition(
            string eventDefinitionId,
            out MapEventDefinition definition)
        {
            EnsureLookup();
            var normalizedId = NormalizeId(eventDefinitionId);
            if (normalizedId.Length == 0)
            {
                definition = null;
                return false;
            }

            return definitionLookup.TryGetValue(normalizedId, out definition);
        }

        /// <summary>
        /// Pool ID、有効数、参照、重み、Event ID重複を検査し、安全に抽選できるか判定する。
        /// </summary>
        public bool Validate(out string error)
        {
            if (string.IsNullOrWhiteSpace(poolId))
            {
                error = "Event Pool IDが空です。";
                return false;
            }

            if (activeSocketCount < 0)
            {
                error = "有効Socket数は0以上にしてください。";
                return false;
            }

            if (activeSocketCount > 0 && entries.Count == 0)
            {
                error = "有効Socket数が1以上のEvent Poolには候補が必要です。";
                return false;
            }

            var seenIds = new HashSet<string>(StringComparer.Ordinal);
            for (var index = 0; index < entries.Count; index++)
            {
                var entry = entries[index];
                if (entry == null || entry.EventDefinition == null)
                {
                    error = $"Entries[{index}] のEvent定義が未設定です。";
                    return false;
                }

                var definitionId = NormalizeId(entry.EventDefinition.EventDefinitionId);
                if (definitionId.Length == 0)
                {
                    error = $"Entries[{index}] のEvent定義IDが空です。";
                    return false;
                }

                if (entry.Weight <= 0)
                {
                    error = $"Event {definitionId} の重みは1以上にしてください。";
                    return false;
                }

                if (!seenIds.Add(definitionId))
                {
                    error = $"Event Pool内の定義IDが重複しています: {definitionId}";
                    return false;
                }
            }

            error = string.Empty;
            return true;
        }

        /// <summary>
        /// Asset再読込時に、以前の保存Listから作った検索索引を破棄する。
        /// </summary>
        private void OnEnable()
        {
            definitionLookup = null;
        }

        /// <summary>
        /// Inspector編集後の次回検索で、新しいListから索引を作り直す。
        /// </summary>
        private void OnValidate()
        {
            definitionLookup = null;
        }

        /// <summary>
        /// Inspector保存ListをEvent Definition ID検索用Dictionaryへ変換する。
        /// </summary>
        private void EnsureLookup()
        {
            if (definitionLookup != null)
            {
                return;
            }

            definitionLookup = new Dictionary<string, MapEventDefinition>(StringComparer.Ordinal);
            for (var index = 0; index < entries.Count; index++)
            {
                var entry = entries[index];
                if (entry == null || entry.EventDefinition == null)
                {
                    continue;
                }

                var definitionId = NormalizeId(entry.EventDefinition.EventDefinitionId);
                if (definitionId.Length > 0 && !definitionLookup.ContainsKey(definitionId))
                {
                    definitionLookup.Add(definitionId, entry.EventDefinition);
                }
            }
        }

        /// <summary>
        /// ID入力の前後空白だけを除き、Asset名変更に依存しない値へ揃える。
        /// </summary>
        private static string NormalizeId(string value)
        {
            return value == null ? string.Empty : value.Trim();
        }
    }
}
