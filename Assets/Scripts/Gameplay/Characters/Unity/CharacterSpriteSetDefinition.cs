using System;
using System.Collections.Generic;
using UnityEngine;

namespace FantasyRoyale.Gameplay.Characters.Unity
{
    /// <summary>
    /// 防具の部位。1つの定義Assetは、1部位だけでも複数の部位でも持てる。
    /// </summary>
    public enum CharacterArmourSlot
    {
        Head = 0,
        Torso = 1,
        Legs = 2
    }

    /// <summary>
    /// 1コマの中の手、背中、兜の位置、利き手の武器の向き。足元からのUnity単位で、右寄りの絵を基準にする。
    /// 攻撃の上半身のコマは、足元ではなく兜の位置からの値を持つ。
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
        [SerializeField] private Vector2 head;
        [SerializeField] private float mainHandFront;
        [SerializeField] private float mainHandBack;

        public CharacterSpriteAnchors(
            Vector2 mainHand,
            Vector2 offHand,
            Vector2 back,
            float mainHandAngle = 0f,
            Sprite mainHandOver = null,
            Sprite offHandOver = null,
            Vector2 head = default,
            float mainHandFront = 0f,
            float mainHandBack = 0f)
        {
            this.mainHand = mainHand;
            this.offHand = offHand;
            this.back = back;
            this.mainHandAngle = mainHandAngle;
            this.mainHandOver = mainHandOver;
            this.offHandOver = offHandOver;
            this.head = head;
            this.mainHandFront = mainHandFront;
            this.mainHandBack = mainHandBack;
        }

        /// <summary>
        /// 兜の決まった位置。攻撃の上半身をここへ重ねるので、上半身は走りの上下の揺れについていく。
        /// </summary>
        public Vector2 Head => head;

        /// <summary>
        /// 利き手の武器の先が向く角度。東を0とする反時計回りの度数である。
        /// </summary>
        public float MainHandAngle => mainHandAngle;

        /// <summary>
        /// 利き手の拳の中心から、武器の先の向きに測った拳の端までの距離。鍔から先の絵はここから始まる。
        /// </summary>
        public float MainHandFront => mainHandFront;

        /// <summary>
        /// 利き手の拳の中心から、武器の先の向きに測った柄頭の側の拳の端までの距離。多くは負の値で、柄頭の絵はここから始まる。
        /// </summary>
        public float MainHandBack => mainHandBack;

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
    /// 1方向分の、動きの型1つの上半身のコマと手の位置、残像、止まって振るときの脚。
    /// 走っている間の脚は走りのコマを続けるので持たない。
    /// </summary>
    [Serializable]
    public sealed class CharacterMotionFrames
    {
        [SerializeField] private CharacterActionMotion motion;
        [SerializeField] private Sprite[] frames = new Sprite[0];
        [SerializeField] private CharacterSpriteAnchors[] anchors = new CharacterSpriteAnchors[0];
        [SerializeField] private Sprite[] smears = new Sprite[0];
        [SerializeField] private Sprite[] standLegs = new Sprite[0];
        [SerializeField] private Vector2[] standHeads = new Vector2[0];

        public CharacterMotionFrames(CharacterActionMotion motion, Sprite[] frames, CharacterSpriteAnchors[] anchors, Sprite[] smears)
            : this(motion, frames, anchors, smears, new Sprite[0], new Vector2[0])
        {
        }

        public CharacterMotionFrames(
            CharacterActionMotion motion,
            Sprite[] frames,
            CharacterSpriteAnchors[] anchors,
            Sprite[] smears,
            Sprite[] standLegs,
            Vector2[] standHeads)
        {
            this.motion = motion;
            this.frames = frames;
            this.anchors = anchors;
            this.smears = smears;
            this.standLegs = standLegs;
            this.standHeads = standHeads;
        }

        public CharacterActionMotion Motion => motion;
        public Sprite[] Frames => frames;
        public CharacterSpriteAnchors[] Anchors => anchors;

        /// <summary>
        /// コマごとの残像。動きの遅いコマはnullである。
        /// </summary>
        public Sprite[] Smears => smears;

        /// <summary>
        /// 止まって振るときの脚のコマ。持たない型は立ち姿の脚のまま振る。
        /// </summary>
        public Sprite[] StandLegs => standLegs;

        /// <summary>
        /// 止まって振るときの、脚のコマごとの兜の位置。足元からのUnity単位である。
        /// </summary>
        public Vector2[] StandHeads => standHeads;
    }

    /// <summary>
    /// 1方向分の、武器種1つの走りの胴のコマと手の位置。武器を持つ腕の形は武器種で違うので、武器種ごとに持てる。
    /// 脚は全ての武器種で共通なので持たない。コマの数は共通の走りと同じである。
    /// </summary>
    [Serializable]
    public sealed class CharacterRunFrames
    {
        [SerializeField] private CharacterActionMotion weaponKind;
        [SerializeField] private Sprite[] frames = new Sprite[0];
        [SerializeField] private CharacterSpriteAnchors[] anchors = new CharacterSpriteAnchors[0];

        public CharacterRunFrames(CharacterActionMotion weaponKind, Sprite[] frames, CharacterSpriteAnchors[] anchors)
        {
            this.weaponKind = weaponKind;
            this.frames = frames;
            this.anchors = anchors;
        }

        /// <summary>
        /// この走りを使う武器種。武器の通常攻撃の動きの型で表す。
        /// </summary>
        public CharacterActionMotion WeaponKind => weaponKind;
        public Sprite[] Frames => frames;
        public CharacterSpriteAnchors[] Anchors => anchors;
    }

    /// <summary>
    /// 1方向分の絵。立ち姿と走りのコマ、それぞれの手と背中の位置、足のコマ、頭、動きの型ごとのコマ、武器種ごとの走り。
    /// 部位で着替える体では、立ち姿と走りのコマは胴だけの絵になる。
    /// </summary>
    [Serializable]
    public sealed class CharacterSpriteView
    {
        [SerializeField] private Sprite idle;
        [SerializeField] private Sprite[] run = new Sprite[0];
        [SerializeField] private CharacterSpriteAnchors idleAnchors;
        [SerializeField] private CharacterSpriteAnchors[] runAnchors = new CharacterSpriteAnchors[0];
        [SerializeField] private Sprite idleLegs;
        [SerializeField] private Sprite[] runLegs = new Sprite[0];
        [SerializeField] private CharacterMotionFrames[] motions = new CharacterMotionFrames[0];
        [SerializeField] private Sprite head;
        [SerializeField] private CharacterRunFrames[] weaponRuns = new CharacterRunFrames[0];

        public CharacterSpriteView(Sprite idle, Sprite[] run)
            : this(idle, run, default, new CharacterSpriteAnchors[0])
        {
        }

        public CharacterSpriteView(
            Sprite idle,
            Sprite[] run,
            CharacterSpriteAnchors idleAnchors,
            CharacterSpriteAnchors[] runAnchors,
            Sprite idleLegs,
            Sprite[] runLegs,
            CharacterMotionFrames[] motions,
            Sprite head = null,
            CharacterRunFrames[] weaponRuns = null)
            : this(idle, run, idleAnchors, runAnchors)
        {
            this.idleLegs = idleLegs;
            this.runLegs = runLegs;
            this.motions = motions;
            this.head = head;
            this.weaponRuns = weaponRuns ?? new CharacterRunFrames[0];
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
        /// 足の部位の、立ち姿と走りのコマ。
        /// </summary>
        public Sprite IdleLegs => idleLegs;
        public Sprite[] RunLegs => runLegs;

        /// <summary>
        /// 頭の部位の絵。兜の位置へ付くので、全ての姿勢で同じ絵を使う。
        /// </summary>
        public Sprite Head => head;

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

        /// <summary>
        /// 武器種ごとの走りの胴。
        /// </summary>
        public CharacterRunFrames[] WeaponRuns => weaponRuns;

        /// <summary>
        /// 武器種の走りの胴を返す。持っていなければnullを返し、共通の走りを使う。
        /// </summary>
        public CharacterRunFrames GetRun(CharacterActionMotion weaponKind)
        {
            foreach (var item in weaponRuns)
            {
                if (item.WeaponKind == weaponKind && item.Frames.Length > 0)
                {
                    return item;
                }
            }

            return null;
        }
    }

    /// <summary>
    /// Characterの向きごとの絵をまとめる定義Asset。頭、胴、足の1部位だけでも、複数の部位でも持てる。
    /// 部位は同じ体の着替えなので、手の位置と拳を共有し、どの部位を組み合わせてもどの武器もそのまま持てる。
    /// 敵のように部位を持たない体は、立ち姿と走りのコマが体全体の絵になる。
    /// 左寄りの向きは右寄りの反転で表すため、持つのは正面、斜め前、横、斜め後ろ、背面の5方向だけである。
    /// </summary>
    [CreateAssetMenu(
        fileName = "CharacterSprites",
        menuName = "FantasyRoyale/Gameplay/Character Sprites")]
    public sealed class CharacterSpriteSetDefinition : ScriptableObject
    {
        [SerializeField] private string id = string.Empty;
        [SerializeField] private string displayName = string.Empty;
        // 走りの速さ6に足の運びを合わせた値。6コマの走りで、地面に着いた足が1コマに後ろへ動く量から決めた。
        [SerializeField, Min(1f)] private float framesPerSecond = 17f;
        [SerializeField] private CharacterSpriteView down;
        [SerializeField] private CharacterSpriteView downSide;
        [SerializeField] private CharacterSpriteView side;
        [SerializeField] private CharacterSpriteView upSide;
        [SerializeField] private CharacterSpriteView up;

        /// <summary>
        /// 全装備の一覧で引く名前。絵のFolderの名前と同じである。敵のように装備でない定義は空のままにする。
        /// </summary>
        public string Id => id;

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
        /// 指定の部位の絵を持っているかを返す。
        /// </summary>
        public bool Covers(CharacterArmourSlot slot)
        {
            if (down == null)
            {
                return false;
            }

            switch (slot)
            {
                case CharacterArmourSlot.Head:
                    return down.Head != null;
                case CharacterArmourSlot.Legs:
                    return down.IdleLegs != null;
                default:
                    return down.Idle != null;
            }
        }

        /// <summary>
        /// 持っている部位の絵が揃っているかを調べ、問題の一覧を返す。空なら組み合わせに使える。
        /// 胴は全ての向きの立ち姿と全てのコマに、手と背中の位置が必要である。
        /// </summary>
        public List<string> FindAnchorProblems()
        {
            var problems = new List<string>();
            if (!Covers(CharacterArmourSlot.Head) && !Covers(CharacterArmourSlot.Torso) && !Covers(CharacterArmourSlot.Legs))
            {
                problems.Add($"{name}: 部位の絵がありません。");
                return problems;
            }

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

        private void CheckActionFrames(CharacterActionDefinition first, List<string> problems)
        {
            var action = first;
            for (var step = 0; action != null && step < CharacterActionDefinition.MaxComboLength; step++)
            {
                if (action.Motion != CharacterActionMotion.Shoot)
                {
                    CheckMotion("正面", down, action, problems);
                    CheckMotion("斜め前", downSide, action, problems);
                    CheckMotion("横", side, action, problems);
                    CheckMotion("斜め後ろ", upSide, action, problems);
                    CheckMotion("背面", up, action, problems);
                }

                action = action.NextInCombo;
            }
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

        private void CheckView(string viewName, CharacterSpriteView view, List<string> problems)
        {
            if (view == null)
            {
                problems.Add($"{viewName}: 絵がありません。");
                return;
            }

            if (Covers(CharacterArmourSlot.Head) && view.Head == null)
            {
                problems.Add($"{viewName}: 頭の絵がありません。");
            }

            if (Covers(CharacterArmourSlot.Torso))
            {
                CheckTorso(viewName, view, problems);
            }

            if (Covers(CharacterArmourSlot.Legs))
            {
                CheckLegs(viewName, view, problems);
            }
        }

        private static void CheckTorso(string name, CharacterSpriteView view, List<string> problems)
        {
            if (view.Idle == null)
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

            // 武器種の走りは共通の脚の上に重ねるので、コマの数が共通の走りと同じでなければならない。
            foreach (var run in view.WeaponRuns)
            {
                if (run.Frames.Length != view.Run.Length || run.Anchors.Length != run.Frames.Length)
                {
                    problems.Add($"{name}: {run.WeaponKind}の走りが{run.Frames.Length}コマ、手の位置が{run.Anchors.Length}コマ分で、共通の走りの{view.Run.Length}コマと違います。");
                }

                foreach (var anchors in run.Anchors)
                {
                    CheckHands(name, anchors, problems);
                }
            }
        }

        private static void CheckLegs(string name, CharacterSpriteView view, List<string> problems)
        {
            if (view.IdleLegs == null || view.RunLegs.Length == 0 || (view.Run.Length > 0 && view.RunLegs.Length != view.Run.Length))
            {
                problems.Add($"{name}: 足のコマが足りません。");
            }

            foreach (var motion in view.Motions)
            {
                CheckStandLegs(name, motion, problems);
            }
        }

        private static void CheckStandLegs(string name, CharacterMotionFrames motion, List<string> problems)
        {
            if (motion.StandLegs.Length == 0)
            {
                return;
            }

            if (motion.StandHeads.Length != motion.StandLegs.Length || Array.IndexOf(motion.StandLegs, null) >= 0)
            {
                problems.Add($"{name}: {motion.Motion}の止まって振る脚のコマが足りません。");
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
        /// Editor Builderからidを設定する。
        /// </summary>
        public void SetId(string newId)
        {
            id = newId;
        }

        /// <summary>
        /// Editor Builderから、画面に出す名前を設定する。
        /// </summary>
        public void SetDisplayName(string newDisplayName)
        {
            displayName = newDisplayName;
        }
    }
}
