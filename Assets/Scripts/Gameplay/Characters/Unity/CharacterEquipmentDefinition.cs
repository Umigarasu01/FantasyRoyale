using System.Collections.Generic;
using UnityEngine;

namespace FantasyRoyale.Gameplay.Characters.Unity
{
    /// <summary>
    /// 装備をどちらの手で持つか。両手の武器はメイン武器とオフハンドの枠を1つで埋める。
    /// </summary>
    public enum CharacterHandUse
    {
        OneHanded = 0,
        TwoHanded = 1,
        OffHand = 2
    }

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
        [SerializeField] private CharacterHandUse handUse = CharacterHandUse.OneHanded;
        [SerializeField, Min(0)] private int magicSlotCount;

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

        public CharacterHandUse HandUse => handUse;

        /// <summary>
        /// 盾のように反対の手だけに持つ装備か。通常攻撃も武器種も持たず、武器の一覧に入れない。
        /// </summary>
        public bool IsOffHand => handUse == CharacterHandUse.OffHand;

        /// <summary>
        /// 魔法の枠の数。武器ごとに違い、0もある。オフハンドは持たない。
        /// </summary>
        public int MagicSlotCount => IsOffHand ? 0 : magicSlotCount;

        /// <summary>
        /// 着けたときに埋める所持品の枠。
        /// </summary>
        public CharacterEquipSlot[] OccupiedSlots
        {
            get
            {
                switch (handUse)
                {
                    case CharacterHandUse.TwoHanded:
                        return new[] { CharacterEquipSlot.MainHand, CharacterEquipSlot.OffHand };
                    case CharacterHandUse.OffHand:
                        return new[] { CharacterEquipSlot.OffHand };
                    default:
                        return new[] { CharacterEquipSlot.MainHand };
                }
            }
        }

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
            if (IsOffHand)
            {
                if (primaryAction != null || specialAction != null || magicSlotCount > 0)
                {
                    problems.Add($"{name}: オフハンドは通常攻撃、技、魔法の枠を持ちません。");
                }

                return problems;
            }

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
        /// Editor Builderから、持つ手と魔法の枠の数を設定する。
        /// </summary>
        public void ConfigureHands(CharacterHandUse newHandUse, int newMagicSlotCount)
        {
            handUse = newHandUse;
            magicSlotCount = Mathf.Max(0, newMagicSlotCount);
        }

        /// <summary>
        /// この定義から、手に入れた1つ分の持ち物を作る。魔法の枠は空で始まる。
        /// </summary>
        public CharacterItem CreateItem()
        {
            return new CharacterItem(id, CharacterItemKind.Equipment, OccupiedSlots, MagicSlotCount);
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
