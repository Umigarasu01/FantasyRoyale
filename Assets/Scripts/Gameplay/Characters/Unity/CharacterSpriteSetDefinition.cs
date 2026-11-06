using System;
using System.Collections.Generic;
using UnityEngine;

namespace FantasyRoyale.Gameplay.Characters.Unity
{
    /// <summary>
    /// 1コマの中の手と背中の位置、利き手の武器の向き。足元からのUnity単位で、右寄りの絵を基準にする。
    /// </summary>
    [Serializable]
    public struct CharacterSpriteAnchors
    {
        [SerializeField] private Vector2 mainHand;
        [SerializeField] private Vector2 offHand;
        [SerializeField] private Vector2 back;
        [SerializeField] private float mainHandAngle;
        [SerializeField] private Sprite mainHandOver;
        [SerializeField] private Sprite offHandOver;

        public CharacterSpriteAnchors(
            Vector2 mainHand,
            Vector2 offHand,
            Vector2 back,
            float mainHandAngle = 0f,
            Sprite mainHandOver = null,
            Sprite offHandOver = null)
        {
            this.mainHand = mainHand;
            this.offHand = offHand;
            this.back = back;
            this.mainHandAngle = mainHandAngle;
            this.mainHandOver = mainHandOver;
            this.offHandOver = offHandOver;
        }

        /// <summary>
        /// 利き手の武器の先が向く角度。東を0とする反時計回りの度数である。
        /// </summary>
        public float MainHandAngle => mainHandAngle;

        /// <summary>
        /// 持った物の上へ重ねる拳の絵を返す。柄が拳の中を通って見える。
        /// </summary>
        public Sprite GetHandOver(CharacterGearSlot slot)
        {
            return slot == CharacterGearSlot.MainHand ? mainHandOver : slot == CharacterGearSlot.OffHand ? offHandOver : null;
        }

        public Vector2 Get(CharacterGearSlot slot)
        {
            return slot == CharacterGearSlot.MainHand ? mainHand : slot == CharacterGearSlot.OffHand ? offHand : back;
        }
    }

    /// <summary>
    /// 1方向分の、動きの型1つの発生、持続、硬直のコマと手の位置。
    /// </summary>
    [Serializable]
    public sealed class CharacterMotionFrames
    {
        [SerializeField] private CharacterActionMotion motion;
        [SerializeField] private Sprite[] frames = new Sprite[0];
        [SerializeField] private CharacterSpriteAnchors[] anchors = new CharacterSpriteAnchors[0];

        public CharacterMotionFrames(CharacterActionMotion motion, Sprite[] frames, CharacterSpriteAnchors[] anchors)
        {
            this.motion = motion;
            this.frames = frames;
            this.anchors = anchors;
        }

        public CharacterActionMotion Motion => motion;
        public Sprite[] Frames => frames;
        public CharacterSpriteAnchors[] Anchors => anchors;
    }

    /// <summary>
    /// 1方向分の立ち姿と走りのコマ、それぞれの手と背中の位置、動きの型ごとのコマ。
    /// </summary>
    [Serializable]
    public sealed class CharacterSpriteView
    {
        [SerializeField] private Sprite idle;
        [SerializeField] private Sprite[] run = new Sprite[0];
        [SerializeField] private CharacterSpriteAnchors idleAnchors;
        [SerializeField] private CharacterSpriteAnchors[] runAnchors = new CharacterSpriteAnchors[0];
        [SerializeField] private CharacterMotionFrames[] motions = new CharacterMotionFrames[0];

        public CharacterSpriteView(Sprite idle, Sprite[] run)
            : this(idle, run, default, new CharacterSpriteAnchors[0])
        {
        }

        public CharacterSpriteView(
            Sprite idle,
            Sprite[] run,
            CharacterSpriteAnchors idleAnchors,
            CharacterSpriteAnchors[] runAnchors,
            CharacterMotionFrames[] motions)
            : this(idle, run, idleAnchors, runAnchors)
        {
            this.motions = motions;
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

        /// <summary>
        /// 動きの型のコマを返す。持っていなければnullを返し、攻撃中も通常の絵を使う。
        /// </summary>
        public CharacterMotionFrames GetMotion(CharacterActionMotion motion)
        {
            foreach (var item in motions)
            {
                if (item.Motion == motion)
                {
                    return item;
                }
            }

            return null;
        }

        /// <summary>
        /// 持っている全ての動きの型のコマ。
        /// </summary>
        public CharacterMotionFrames[] Motions => motions;
    }

    /// <summary>
    /// Characterの向きごとの立ち姿と走りのコマをまとめる定義Asset。防具の全身セット1つがこの1つになる。
    /// 全身セットは同じ姿勢の着替えなので、手の位置と拳を共有し、どの武器もそのまま持てる。
    /// 左寄りの向きは右寄りの反転で表すため、持つのは正面、斜め前、横、斜め後ろ、背面の5方向だけである。
    /// </summary>
    [CreateAssetMenu(
        fileName = "CharacterSprites",
        menuName = "FantasyRoyale/Gameplay/Character Sprites")]
    public sealed class CharacterSpriteSetDefinition : ScriptableObject
    {
        [SerializeField] private string displayName = string.Empty;
        [SerializeField, Min(1f)] private float framesPerSecond = 11f;
        [SerializeField] private CharacterSpriteView down;
        [SerializeField] private CharacterSpriteView downSide;
        [SerializeField] private CharacterSpriteView side;
        [SerializeField] private CharacterSpriteView upSide;
        [SerializeField] private CharacterSpriteView up;

        public string DisplayName => displayName;
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

        /// <summary>
        /// この体が装備を持てるかを調べ、問題の一覧を返す。空なら持てる。
        /// 部品が揃っていて、部品が動きの型に合い、振る動きの絵が全ての向きにある必要がある。
        /// 射る動きの絵はまだないので、なくても走りと立ち姿のまま撃てる。
        /// </summary>
        public List<string> FindEquipmentProblems(CharacterEquipmentDefinition equipment)
        {
            var problems = equipment.FindMotionProblems();
            var pixelsPerUnit = down != null && down.Idle != null ? down.Idle.pixelsPerUnit : 0f;
            foreach (var gear in equipment.Gear)
            {
                problems.AddRange(gear.FindProblems(pixelsPerUnit));
            }

            CheckActionFrames(equipment.PrimaryAction, problems);
            CheckActionFrames(equipment.SpecialAction, problems);
            return problems;
        }

        private void CheckActionFrames(CharacterActionDefinition action, List<string> problems)
        {
            if (action == null || action.Motion != CharacterActionMotion.Swing)
            {
                return;
            }

            CheckMotion("正面", down, action, problems);
            CheckMotion("斜め前", downSide, action, problems);
            CheckMotion("横", side, action, problems);
            CheckMotion("斜め後ろ", upSide, action, problems);
            CheckMotion("背面", up, action, problems);
        }

        private void CheckMotion(string viewName, CharacterSpriteView view, CharacterActionDefinition action, List<string> problems)
        {
            var frames = view?.GetMotion(action.Motion);
            if (frames == null || frames.Frames.Length == 0)
            {
                problems.Add($"{name}の{viewName}: {action.ActionId}の{action.Motion}のコマがありません。");
            }
        }

        private static void CheckHands(string name, CharacterSpriteAnchors anchors, List<string> problems)
        {
            if (anchors.GetHandOver(CharacterGearSlot.MainHand) == null
                || anchors.GetHandOver(CharacterGearSlot.OffHand) == null)
            {
                problems.Add($"{name}: 拳の絵がありません。");
            }
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

            CheckHands(name, view.IdleAnchors, problems);
            foreach (var anchors in view.RunAnchors)
            {
                CheckHands(name, anchors, problems);
            }

            foreach (var motion in view.Motions)
            {
                if (motion.Anchors.Length != motion.Frames.Length)
                {
                    problems.Add($"{name}: 手の位置が{motion.Anchors.Length}コマ分で、{motion.Motion}の{motion.Frames.Length}コマと違います。");
                }

                foreach (var anchors in motion.Anchors)
                {
                    CheckHands(name, anchors, problems);
                }
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

        /// <summary>
        /// Editor Builderから、画面に出す全身セットの名前を設定する。
        /// </summary>
        public void SetDisplayName(string newDisplayName)
        {
            displayName = newDisplayName;
        }
    }
}
