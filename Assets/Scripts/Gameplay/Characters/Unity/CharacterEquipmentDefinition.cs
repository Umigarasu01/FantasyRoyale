using UnityEngine;

namespace FantasyRoyale.Gameplay.Characters.Unity
{
    /// <summary>
    /// 装備一つが持つ通常攻撃と特殊Action、見た目の部品を定義するAsset。
    /// 差し替えるとCharacterのActionと部品がまとめて替わり、体のアニメーションはそのまま使う。
    /// </summary>
    [CreateAssetMenu(
        fileName = "CharacterEquipment",
        menuName = "FantasyRoyale/Gameplay/Character Equipment")]
    public sealed class CharacterEquipmentDefinition : ScriptableObject
    {
        [SerializeField] private string displayName = string.Empty;
        [SerializeField] private CharacterActionDefinition primaryAction;
        [SerializeField] private CharacterActionDefinition specialAction;
        [SerializeField] private CharacterGearDefinition[] gear = new CharacterGearDefinition[0];

        public string DisplayName => displayName;
        public CharacterActionDefinition PrimaryAction => primaryAction;
        public CharacterActionDefinition SpecialAction => specialAction;

        /// <summary>
        /// この装備を持ったときに体へ重ねる部品。
        /// </summary>
        public CharacterGearDefinition[] Gear => gear;

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
        /// Editor Builderから、この装備を持ったときに体へ重ねる部品を設定する。
        /// </summary>
        public void SetGear(CharacterGearDefinition[] newGear)
        {
            gear = newGear;
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
