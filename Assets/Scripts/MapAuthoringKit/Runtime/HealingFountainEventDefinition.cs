using UnityEngine;

namespace FantasyRoyale.MapAuthoringKit
{
    /// <summary>
    /// 回復の泉に固有の回復量を、共通Event設定へ追加する不変のScriptableObject定義。
    /// 実際のHP更新や使用済み状態は持たず、複数Socketから同じAssetを共有する。
    /// </summary>
    [CreateAssetMenu(
        fileName = "HealingFountainEventDefinition",
        menuName = "FantasyRoyale/Map Authoring/Healing Fountain Event Definition")]
    public sealed class HealingFountainEventDefinition : MapEventDefinition
    {
        public const int DefaultHealAmountValue = 30;

        [SerializeField] [Min(1)] private int healAmount = DefaultHealAmountValue;

        public int HealAmount => healAmount;

        /// <summary>
        /// Editor生成やTestから、共通Event設定と泉固有の回復量を一括して記録する。
        /// </summary>
        public void ConfigureHealingFountain(
            string newEventDefinitionId,
            string newDisplayName,
            string newPromptText,
            float newDefaultInteractionRadius,
            bool newOneShot,
            int newHealAmount)
        {
            Configure(
                newEventDefinitionId,
                newDisplayName,
                newPromptText,
                newDefaultInteractionRadius,
                newOneShot);
            healAmount = Mathf.Max(1, newHealAmount);
        }
    }
}
