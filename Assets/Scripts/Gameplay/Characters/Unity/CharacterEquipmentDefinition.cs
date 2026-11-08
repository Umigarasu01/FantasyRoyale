using System.Collections.Generic;
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
        [SerializeField] private string id = string.Empty;
        [SerializeField] private string displayName = string.Empty;
        [SerializeField] private CharacterActionDefinition primaryAction;
        [SerializeField] private CharacterActionDefinition specialAction;
        [SerializeField] private CharacterGearDefinition[] gear = new CharacterGearDefinition[0];

        /// <summary>
        /// 全装備の一覧で引く名前。これを入れた定義は一覧へ自動で載る。
        /// </summary>
        public string Id => id;

        public string DisplayName => displayName;
        public CharacterActionDefinition PrimaryAction => primaryAction;
        public CharacterActionDefinition SpecialAction => specialAction;

        /// <summary>
        /// この装備を持ったときに体へ重ねる部品。
        /// </summary>
        public CharacterGearDefinition[] Gear => gear;

        /// <summary>
        /// 武器種。同じ武器種の武器は同じ動きを使うので、通常攻撃の動きの型で決まる。
        /// 通常攻撃がなければ、Actionの既定の型と同じく振る武器として扱う。
        /// </summary>
        public CharacterActionMotion WeaponKind => primaryAction == null ? CharacterActionMotion.Swing : primaryAction.Motion;

        /// <summary>
        /// 通常攻撃の動きの型に部品が合っているかを調べ、問題の一覧を返す。空なら合っている。
        /// 振る武器は利き手に角度ごとの絵が、弓は反対の手の部品が要る。
        /// </summary>
        public List<string> FindMotionProblems()
        {
            var problems = new List<string>();
            if (primaryAction == null)
            {
                return problems;
            }

            if (primaryAction.Motion == CharacterActionMotion.Shoot)
            {
                if (!HasGear(CharacterGearSlot.OffHand, false))
                {
                    problems.Add($"{name}: 弓は、反対の手の部品が要ります。");
                }
            }
            else if (!HasGear(CharacterGearSlot.MainHand, true))
            {
                problems.Add($"{name}: 振る武器は、利き手に角度ごとの絵が要ります。");
            }

            return problems;
        }

        /// <summary>
        /// Editor Builderからidを設定する。
        /// </summary>
        public void SetId(string newId)
        {
            id = newId;
        }

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

        private bool HasGear(CharacterGearSlot slot, bool mustRotate)
        {
            foreach (var item in gear)
            {
                if (item != null && item.Slot == slot && (!mustRotate || item.Rotates))
                {
                    return true;
                }
            }

            return false;
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
