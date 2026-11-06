using UnityEngine;

namespace FantasyRoyale.Gameplay.Characters.Unity
{
    /// <summary>
    /// Characterの向きごとの待機Spriteと走りのコマをまとめる定義Asset。
    /// 左向きは横向きの反転で表すため、持つのは正面、横、背面の3方向だけである。
    /// </summary>
    [CreateAssetMenu(
        fileName = "CharacterSprites",
        menuName = "FantasyRoyale/Gameplay/Character Sprites")]
    public sealed class CharacterSpriteSetDefinition : ScriptableObject
    {
        [SerializeField, Min(1f)] private float framesPerSecond = 11f;
        [SerializeField] private Sprite idleDown;
        [SerializeField] private Sprite idleSide;
        [SerializeField] private Sprite idleUp;
        [SerializeField] private Sprite[] runDown = new Sprite[0];
        [SerializeField] private Sprite[] runSide = new Sprite[0];
        [SerializeField] private Sprite[] runUp = new Sprite[0];

        public float FramesPerSecond => framesPerSecond;
        public Sprite IdleDown => idleDown;
        public Sprite IdleSide => idleSide;
        public Sprite IdleUp => idleUp;
        public Sprite[] RunDown => runDown;
        public Sprite[] RunSide => runSide;
        public Sprite[] RunUp => runUp;

        /// <summary>
        /// Editor Builderと確認用Testから、全てのSpriteと再生速度を設定する。
        /// </summary>
        public void Configure(
            float newFramesPerSecond,
            Sprite newIdleDown,
            Sprite newIdleSide,
            Sprite newIdleUp,
            Sprite[] newRunDown,
            Sprite[] newRunSide,
            Sprite[] newRunUp)
        {
            framesPerSecond = newFramesPerSecond;
            idleDown = newIdleDown;
            idleSide = newIdleSide;
            idleUp = newIdleUp;
            runDown = newRunDown;
            runSide = newRunSide;
            runUp = newRunUp;
        }
    }
}
