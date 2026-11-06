using System;
using UnityEngine;

namespace FantasyRoyale.Gameplay.Characters.Unity
{
    /// <summary>
    /// 1方向分の立ち姿と走りのコマ。
    /// </summary>
    [Serializable]
    public sealed class CharacterSpriteView
    {
        [SerializeField] private Sprite idle;
        [SerializeField] private Sprite[] run = new Sprite[0];

        public CharacterSpriteView(Sprite idle, Sprite[] run)
        {
            this.idle = idle;
            this.run = run;
        }

        public Sprite Idle => idle;
        public Sprite[] Run => run;
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
