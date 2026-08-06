using UnityEngine;

namespace FantasyRoyale.MapAuthoringKit
{
    /// <summary>
    /// EventSocketが参照する、イベントの不変設定を表すScriptableObject。
    /// 対戦中に変化する使用済み状態は保持せず、同じ定義を複数Socketから共有する。
    /// </summary>
    [CreateAssetMenu(
        fileName = "MapEventDefinition",
        menuName = "FantasyRoyale/Map Authoring/Event Definition")]
    public class MapEventDefinition : ScriptableObject
    {
        /// <summary>
        /// EventSocket側に個別上書きがない場合に使う、初期の接近判定半径。
        /// </summary>
        public const float DefaultInteractionRadiusValue = 1.25f;

        [SerializeField] private string eventDefinitionId = string.Empty;
        [SerializeField] private string displayName = string.Empty;
        [SerializeField] [TextArea(1, 3)] private string promptText = "E：調べる";
        [SerializeField] [Min(0f)] private float defaultInteractionRadius = DefaultInteractionRadiusValue;
        [SerializeField] private bool oneShot = true;

        public string EventDefinitionId => eventDefinitionId;
        public string DisplayName => displayName;
        public string PromptText => promptText;
        public float DefaultInteractionRadius => defaultInteractionRadius;
        public bool OneShot => oneShot;

        /// <summary>
        /// Editor生成やTestから、イベント定義の最小設定を一括して記録する。
        /// イベント処理本体や対戦中の状態はここへ持たせない。
        /// </summary>
        public void Configure(
            string newEventDefinitionId,
            string newDisplayName,
            string newPromptText,
            float newDefaultInteractionRadius,
            bool newOneShot)
        {
            eventDefinitionId = NormalizeId(newEventDefinitionId);
            displayName = newDisplayName ?? string.Empty;
            promptText = newPromptText ?? string.Empty;
            defaultInteractionRadius = IsFinitePositive(newDefaultInteractionRadius)
                ? newDefaultInteractionRadius
                : 0f;
            oneShot = newOneShot;
        }

        /// <summary>
        /// ID比較の前に前後空白だけを除き、Asset名変更に依存しない識別子を作る。
        /// </summary>
        private static string NormalizeId(string value)
        {
            return value == null ? string.Empty : value.Trim();
        }

        /// <summary>
        /// Inspectorから入る不正な半径を、Catalog検証で判定できる有限正値か確認する。
        /// </summary>
        private static bool IsFinitePositive(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value) && value > 0f;
        }
    }
}
