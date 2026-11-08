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
    /// 体のアニメーションへ重ねる装備の部品1つ。走りや攻撃のコマは持たない。
    /// 体が手の位置と角度を持つので、新しい部品を足しても体のアニメーションはそのまま使える。
    /// 盾のように向きで見える面が変わる物は5方向の絵を持ち、剣のように変わらない物は角度ごとの絵を持つ。
    /// 各Spriteのpivotは、手や背中に付ける点へ合わせる。
    /// 剣は握りが拳に隠れるので、鍔から先と柄頭の2つの絵に分けて持ち、拳の両端へ付ける。
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
        [SerializeField] private Sprite[] rotations = new Sprite[0];
        [SerializeField] private Sprite[] backRotations = new Sprite[0];
        [SerializeField] private bool heldInFist;

        public CharacterGearSlot Slot => slot;

        /// <summary>
        /// 拳で握る部品か。握る部品には、体の拳を上から重ねる。拳の両端へ分けて付ける部品は拳を重ねない。
        /// </summary>
        public bool HeldInFist => heldInFist;

        /// <summary>
        /// 角度ごとの絵を持つ部品か。持つ部品は体の角度に従って回る。
        /// </summary>
        public bool Rotates => rotations.Length > 0;

        /// <summary>
        /// 拳の両端へ分けて付ける部品か。角度ごとの絵が先の側（鍔から先）、もう1組が柄頭の側である。
        /// 各Spriteのpivotは、拳に接する点に合わせてある。
        /// </summary>
        public bool IsSplitAtFist => Rotates && backRotations.Length > 0;

        /// <summary>
        /// 先が指定の角度を向く絵を返す。東を0とする反時計回りの度数である。
        /// </summary>
        public Sprite GetRotatedSprite(float tipAngle)
        {
            return Pick(rotations, tipAngle);
        }

        /// <summary>
        /// 先が指定の角度を向くときの、柄頭の側の絵を返す。分けていない部品ではnullを返す。
        /// </summary>
        public Sprite GetBackRotatedSprite(float tipAngle)
        {
            return backRotations.Length > 0 ? Pick(backRotations, tipAngle) : null;
        }

        private static Sprite Pick(Sprite[] sprites, float tipAngle)
        {
            var step = 360f / sprites.Length;
            var index = Mathf.RoundToInt(Mathf.Repeat(tipAngle, 360f) / step) % sprites.Length;
            return sprites[index];
        }

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
            if (Rotates)
            {
                if (slot != CharacterGearSlot.MainHand)
                {
                    problems.Add($"{name}: 体が角度を持つのは利き手だけです。");
                }

                for (var index = 0; index < rotations.Length; index++)
                {
                    CheckSprite($"{index}番目の角度", rotations[index], bodyPixelsPerUnit, problems);
                }

                if (backRotations.Length > 0 && backRotations.Length != rotations.Length)
                {
                    problems.Add($"{name}: 柄頭の側の絵が{backRotations.Length}角度分で、先の側の{rotations.Length}角度と違います。");
                }

                for (var index = 0; index < backRotations.Length; index++)
                {
                    CheckSprite($"柄頭の側の{index}番目の角度", backRotations[index], bodyPixelsPerUnit, problems);
                }

                return problems;
            }

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
            rotations = new Sprite[0];
            backRotations = new Sprite[0];
        }

        /// <summary>
        /// Editor Builderと確認用Testから、拳で握る部品かを設定する。
        /// </summary>
        public void SetHeldInFist(bool newHeldInFist)
        {
            heldInFist = newHeldInFist;
        }

        /// <summary>
        /// Editor Builderと確認用Testから、付ける位置と角度ごとの絵を設定する。
        /// 拳の両端へ分けて付ける部品は、柄頭の側の絵を同じ角度の数だけ渡す。
        /// </summary>
        public void ConfigureRotations(CharacterGearSlot newSlot, Sprite[] newRotations, Sprite[] newBackRotations = null)
        {
            Configure(newSlot, null, null, null, null, null);
            rotations = newRotations;
            backRotations = newBackRotations ?? new Sprite[0];
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
