using UnityEngine;

namespace FantasyRoyale.Gameplay.Characters.Unity
{
    /// <summary>
    /// 装備一つが持つ通常攻撃と特殊Actionを定義するAsset。
    /// 差し替えるとCharacterのActionがまとめて替わる。
    /// </summary>
    [CreateAssetMenu(
        fileName = "CharacterEquipment",
        menuName = "FantasyRoyale/Gameplay/Character Equipment")]
    public sealed class CharacterEquipmentDefinition : ScriptableObject
    {
        [SerializeField] private string displayName = string.Empty;
        [SerializeField] private CharacterActionDefinition primaryAction;
        [SerializeField] private CharacterActionDefinition specialAction;

        public string DisplayName => displayName;
        public CharacterActionDefinition PrimaryAction => primaryAction;
        public CharacterActionDefinition SpecialAction => specialAction;

        /// <summary>
        /// Editor Builderから表示名と二つのSlotを設定する。
        /// </summary>
        public void Configure(string newDisplayName, CharacterActionDefinition newPrimaryAction, CharacterActionDefinition newSpecialAction)
        {
            displayName = newDisplayName;
            primaryAction = newPrimaryAction;
            specialAction = newSpecialAction;
        }

        /// <summary>
        /// 実行中の装備状態へこの定義のActionを反映する。空のSlotはActionなしになる。
        /// </summary>
        public void ApplyTo(CharacterLoadout loadout)
        {
            loadout.Replace(
                primaryAction == null ? null : primaryAction.CreateSpec(),
                specialAction == null ? null : specialAction.CreateSpec());
        }
    }
}
