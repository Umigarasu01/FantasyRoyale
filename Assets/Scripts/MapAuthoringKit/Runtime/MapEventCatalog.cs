using System;
using System.Collections.Generic;
using UnityEngine;

namespace FantasyRoyale.MapAuthoringKit
{
    /// <summary>
    /// Inspectorで編集するイベント定義Listを、実行時のID検索Dictionaryへ変換するカタログ。
    /// Listを保存の正本にし、Dictionaryは実行時だけ再構築する索引として扱う。
    /// </summary>
    [CreateAssetMenu(
        fileName = "MapEventCatalog",
        menuName = "FantasyRoyale/Map Authoring/Event Catalog")]
    public sealed class MapEventCatalog : ScriptableObject
    {
        [SerializeField] private List<MapEventDefinition> definitions = new List<MapEventDefinition>();

        [NonSerialized] private Dictionary<string, MapEventDefinition> lookup;

        /// <summary>
        /// Inspectorで編集・保存される定義Listを読み取り専用で公開する。
        /// </summary>
        public IReadOnlyList<MapEventDefinition> Definitions => definitions;

        /// <summary>
        /// EventDefinition IDから定義を取得する。未登録IDはfalseで返し、呼び出し側がFallbackを選べる。
        /// </summary>
        public bool TryGet(string eventDefinitionId, out MapEventDefinition definition)
        {
            EnsureLookup();
            if (string.IsNullOrWhiteSpace(eventDefinitionId))
            {
                definition = null;
                return false;
            }

            return lookup.TryGetValue(eventDefinitionId.Trim(), out definition);
        }

        /// <summary>
        /// 保存ListのID、参照、半径、重複を検査し、実行時索引を安全に作れるか判定する。
        /// </summary>
        public bool Validate(out string error)
        {
            var seenIds = new HashSet<string>(StringComparer.Ordinal);
            for (var index = 0; index < definitions.Count; index++)
            {
                var definition = definitions[index];
                if (definition == null)
                {
                    error = $"Definitions[{index}] が未設定です。";
                    return false;
                }

                var id = definition.EventDefinitionId;
                if (string.IsNullOrWhiteSpace(id))
                {
                    error = $"イベント定義 {definition.name} のIDが空です。";
                    return false;
                }

                id = id.Trim();

                if (!IsFinitePositive(definition.DefaultInteractionRadius))
                {
                    error = $"イベント定義 {definition.name} の接近判定半径が不正です。";
                    return false;
                }

                if (!seenIds.Add(id))
                {
                    error = $"イベント定義IDが重複しています: {id}";
                    return false;
                }
            }

            error = string.Empty;
            return true;
        }

        /// <summary>
        /// Assetを再読み込みした際に、古いRuntime索引を持ち越さないよう破棄する。
        /// </summary>
        private void OnEnable()
        {
            lookup = null;
        }

        /// <summary>
        /// Inspector編集後の次回検索で、変更後のListから索引を再構築する。
        /// </summary>
        private void OnValidate()
        {
            lookup = null;
        }

        /// <summary>
        /// Unityが保存したListを、IDの一意性を確認しながら実行時Dictionaryへ変換する。
        /// </summary>
        private void EnsureLookup()
        {
            if (lookup != null)
            {
                return;
            }

            lookup = new Dictionary<string, MapEventDefinition>(StringComparer.Ordinal);
            for (var index = 0; index < definitions.Count; index++)
            {
                var definition = definitions[index];
                if (definition == null || string.IsNullOrWhiteSpace(definition.EventDefinitionId))
                {
                    continue;
                }

                var id = definition.EventDefinitionId.Trim();
                if (lookup.ContainsKey(id))
                {
                    Debug.LogError($"イベント定義IDが重複しています: {id}", this);
                    continue;
                }

                lookup.Add(id, definition);
            }
        }

        /// <summary>
        /// 接近判定半径がInfinityやNaNを含まない有限正値か確認する。
        /// </summary>
        private static bool IsFinitePositive(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value) && value > 0f;
        }
    }
}
