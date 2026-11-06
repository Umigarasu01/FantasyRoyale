using System.Collections.Generic;
using UnityEngine;

namespace FantasyRoyale.Gameplay.Characters.Unity
{
    /// <summary>
    /// 装備の部品を付ける体の位置。
    /// </summary>
    public enum CharacterGearSlot
    {
        MainHand,
        OffHand,
        Back
    }

    /// <summary>
    /// 体のアニメーションへ重ねる装備の部品1つ。向きごとの絵だけを持ち、走りのコマは持たない。
    /// 体が手の位置を持つので、新しい部品を足しても体のアニメーションはそのまま使える。
    /// 各Spriteのpivotは、手や背中に付ける点へ合わせる。
    /// </summary>
    [CreateAssetMenu(
        fileName = "CharacterGear",
        menuName = "FantasyRoyale/Gameplay/Character Gear")]
    public sealed class CharacterGearDefinition : ScriptableObject
    {
        [SerializeField] private CharacterGearSlot slot;
        [SerializeField] private Sprite down;
        [SerializeField] private Sprite downSide;
        [SerializeField] private Sprite side;
        [SerializeField] private Sprite upSide;
        [SerializeField] private Sprite up;

        public CharacterGearSlot Slot => slot;

        /// <summary>
        /// 8方向の向きに対応する絵を返す。左寄りの向きは右寄りの反転で表す。
        /// </summary>
        public Sprite GetSprite(CharacterDirection8 facing)
        {
            switch (facing)
            {
                case CharacterDirection8.South:
                    return down;
                case CharacterDirection8.SouthEast:
                case CharacterDirection8.SouthWest:
                    return downSide;
                case CharacterDirection8.NorthEast:
                case CharacterDirection8.NorthWest:
                    return upSide;
                case CharacterDirection8.North:
                    return up;
                default:
                    return side;
            }
        }

        /// <summary>
        /// 体の絵と組み合わせられるかを調べ、問題の一覧を返す。空なら組み合わせられる。
        /// 全ての向きの絵があり、ドットの細かさが体と同じである必要がある。
        /// </summary>
        public List<string> FindProblems(float bodyPixelsPerUnit)
        {
            var problems = new List<string>();
            CheckSprite("正面", down, bodyPixelsPerUnit, problems);
            CheckSprite("斜め前", downSide, bodyPixelsPerUnit, problems);
            CheckSprite("横", side, bodyPixelsPerUnit, problems);
            CheckSprite("斜め後ろ", upSide, bodyPixelsPerUnit, problems);
            CheckSprite("背面", up, bodyPixelsPerUnit, problems);
            return problems;
        }

        /// <summary>
        /// Editor Builderと確認用Testから、付ける位置と5方向の絵を設定する。
        /// </summary>
        public void Configure(
            CharacterGearSlot newSlot,
            Sprite newDown,
            Sprite newDownSide,
            Sprite newSide,
            Sprite newUpSide,
            Sprite newUp)
        {
            slot = newSlot;
            down = newDown;
            downSide = newDownSide;
            side = newSide;
            upSide = newUpSide;
            up = newUp;
        }

        private void CheckSprite(string view, Sprite sprite, float bodyPixelsPerUnit, List<string> problems)
        {
            if (sprite == null)
            {
                problems.Add($"{name}の{view}: 絵がありません。");
            }
            else if (!Mathf.Approximately(sprite.pixelsPerUnit, bodyPixelsPerUnit))
            {
                problems.Add($"{name}の{view}: PPUが{sprite.pixelsPerUnit}で、体の{bodyPixelsPerUnit}と違います。");
            }
        }
    }
}
