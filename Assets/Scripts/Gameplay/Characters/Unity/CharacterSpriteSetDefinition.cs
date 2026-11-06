using System;
using System.Collections.Generic;
using UnityEngine;

namespace FantasyRoyale.Gameplay.Characters.Unity
{
    /// <summary>
    /// 1コマの中の手と背中の位置。足元からのUnity単位で、横向きの絵を基準にする。
    /// </summary>
    [Serializable]
    public struct CharacterSpriteAnchors
    {
        [SerializeField] private Vector2 mainHand;
        [SerializeField] private Vector2 offHand;
        [SerializeField] private Vector2 back;

        public CharacterSpriteAnchors(Vector2 mainHand, Vector2 offHand, Vector2 back)
        {
            this.mainHand = mainHand;
            this.offHand = offHand;
            this.back = back;
        }

        public Vector2 Get(CharacterGearSlot slot)
        {
            return slot == CharacterGearSlot.MainHand ? mainHand : slot == CharacterGearSlot.OffHand ? offHand : back;
        }
    }

    /// <summary>
    /// 1方向分の立ち姿と走りのコマ、それぞれの手と背中の位置。
    /// </summary>
    [Serializable]
    public sealed class CharacterSpriteView
    {
        [SerializeField] private Sprite idle;
        [SerializeField] private Sprite[] run = new Sprite[0];
        [SerializeField] private CharacterSpriteAnchors idleAnchors;
        [SerializeField] private CharacterSpriteAnchors[] runAnchors = new CharacterSpriteAnchors[0];

        public CharacterSpriteView(Sprite idle, Sprite[] run)
            : this(idle, run, default, new CharacterSpriteAnchors[0])
        {
        }

        public CharacterSpriteView(
            Sprite idle,
            Sprite[] run,
            CharacterSpriteAnchors idleAnchors,
            CharacterSpriteAnchors[] runAnchors)
        {
            this.idle = idle;
            this.run = run;
            this.idleAnchors = idleAnchors;
            this.runAnchors = runAnchors;
        }

        public Sprite Idle => idle;
        public Sprite[] Run => run;
        public CharacterSpriteAnchors IdleAnchors => idleAnchors;
        public CharacterSpriteAnchors[] RunAnchors => runAnchors;
    }

    /// <summary>
    /// Characterの向きごとの立ち姿と走りのコマをまとめる定義Asset。
    /// 左寄りの向きは右寄りの反転で表すため、持つのは正面、斜め前、横、斜め後ろ、背面の5方向だけである。
    /// </summary>
    [CreateAssetMenu(
        fileName = "CharacterSprites",
        menuName = "FantasyRoyale/Gameplay/Character Sprites")]
    public sealed class CharacterSpriteSetDefinition : ScriptableObject
    {
        [SerializeField, Min(1f)] private float framesPerSecond = 11f;
        [SerializeField] private CharacterSpriteView down;
        [SerializeField] private CharacterSpriteView downSide;
        [SerializeField] private CharacterSpriteView side;
        [SerializeField] private CharacterSpriteView upSide;
        [SerializeField] private CharacterSpriteView up;

        public float FramesPerSecond => framesPerSecond;

        /// <summary>
        /// 8方向の向きに対応する表示を返す。
        /// </summary>
        public CharacterSpriteView GetView(CharacterDirection8 facing)
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
        /// 装備の部品を重ねられる体かを調べ、問題の一覧を返す。空なら重ねられる。
        /// 全ての向きの立ち姿と全てのコマに、手と背中の位置が必要である。
        /// </summary>
        public List<string> FindAnchorProblems()
        {
            var problems = new List<string>();
            CheckView("正面", down, problems);
            CheckView("斜め前", downSide, problems);
            CheckView("横", side, problems);
            CheckView("斜め後ろ", upSide, problems);
            CheckView("背面", up, problems);
            return problems;
        }

        private static void CheckView(string name, CharacterSpriteView view, List<string> problems)
        {
            if (view == null || view.Idle == null)
            {
                problems.Add($"{name}: 立ち姿がありません。");
                return;
            }

            if (view.Run.Length == 0)
            {
                problems.Add($"{name}: 走りのコマがありません。");
            }

            if (view.RunAnchors.Length != view.Run.Length)
            {
                problems.Add($"{name}: 手の位置が{view.RunAnchors.Length}コマ分で、走りの{view.Run.Length}コマと違います。");
            }
        }

        /// <summary>
        /// Editor Builderと確認用Testから、全ての表示と再生速度を設定する。
        /// </summary>
        public void Configure(
            float newFramesPerSecond,
            CharacterSpriteView newDown,
            CharacterSpriteView newDownSide,
            CharacterSpriteView newSide,
            CharacterSpriteView newUpSide,
            CharacterSpriteView newUp)
        {
            framesPerSecond = newFramesPerSecond;
            down = newDown;
            downSide = newDownSide;
            side = newSide;
            upSide = newUpSide;
            up = newUp;
        }
    }
}
