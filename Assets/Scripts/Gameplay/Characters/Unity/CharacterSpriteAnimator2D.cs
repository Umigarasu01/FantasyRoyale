using System;
using System.Collections.Generic;
using UnityEngine;

namespace FantasyRoyale.Gameplay.Characters.Unity
{
    /// <summary>
    /// Actorの向きと移動状態を読み、表示するSpriteを切り替える本番Component。
    /// 装備の部品は体の手と背中の位置へ毎Frame重ねるので、部品を替えてもアニメーションは保たれる。
    /// 部位で着替える体は足、胴、頭の3層で表す。足と胴はコマごとの絵で、頭は兜の位置へ付く1枚の絵である。
    /// 攻撃中は胴が動きの型の上半身に替わる。足は走っていれば走りのコマを続け、止まっていれば型の踏み込みのコマを見せる。
    /// 武器はコマごとの手の位置と角度で腕と一緒に動く。
    /// 足、胴、頭の3層の体は、足元へいつも影を出す。走って足が着くコマと着地では、足元に砂ぼこりを出す。
    /// 跳んでいる間は、足元を残して全ての層を持ち上げ、影を小さくする。足のコマは踏み切った時のまま止める。
    /// 表示だけを担当し、移動や判定には影響しない。
    /// </summary>
    [AddComponentMenu("FantasyRoyale/Gameplay/Characters/Character Sprite Animator 2D")]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterActor2D), typeof(SpriteRenderer))]
    public sealed class CharacterSpriteAnimator2D : MonoBehaviour
    {
        private const int AttackFrameCount = 4;
        private const float SmearRecoveryShare = 0.3f;
        private const int StandDeepFrame = 2;
        private const float StandHoldShare = 0.5f;

        // 体より手前に重ねる順。足の上へ柄頭、胴、背を向けて振る剣、頭、盾の手の部品、残像、武器、拳の順に積む。
        // 柄頭は胴の下なので、腕に隠れる所は見えない。背を向けて走る剣も胴の下へ回し、拳の下から出す。
        private const int UnderUpperOrder = 1;
        private const int UpperOrder = 2;
        private const int UnderHeadOrder = 3;
        private const int HeadOrder = 4;
        private const int OffHandOrder = 5;
        private const int SmearOrder = 6;
        private const int MainHandOrder = 7;
        private const int HandOverOrder = 8;

        // 足元の影。体の後ろへ回った部品よりさらに奥へ置き、跳んで一番高い所で小さくする。
        private const int ShadowOrder = -2;
        private const float ShadowAlpha = 0.4f;
        private const float ShadowPeakScale = 0.7f;
        private const int ShadowWidth = 28;
        private const int ShadowHeight = 10;
        private const float ShadowPixelsPerUnit = 64f;

        private static Sprite shadowSprite;

        // 足が着いた所の砂ぼこり。体の後ろに出て、少し上がりながら広がって消える。
        private const int DustOrder = -1;
        private const int DustCount = 6;
        private const float DustSeconds = 0.22f;
        private const float DustAlpha = 0.9f;
        private const float DustRise = 0.15f;
        private const float DustGrowth = 0.5f;
        private const float DustBehind = 0.08f;
        private const float LandingDustSpread = 0.16f;
        private static readonly Color DustColour = new Color(0.96f, 0.93f, 0.83f);
        private static readonly string[] DustShape = { "...##.....", "..####.##.", ".########.", "##########", ".########." };
        private static Sprite dustSprite;
        private readonly List<DustPuff> dust = new List<DustPuff>();
        private int lastRunFrame = -1;

        private CharacterActor2D actor;
        private SpriteRenderer spriteRenderer;

        // 手と兜の位置は胴の定義が持つ。足と頭は別の定義の絵を着られる。
        private CharacterSpriteSetDefinition sprites;
        private CharacterSpriteSetDefinition legSprites;
        private CharacterSpriteSetDefinition headSprites;
        private CharacterCombat2D combat;
        private SpriteRenderer upperRenderer;
        private SpriteRenderer headRenderer;
        private SpriteRenderer smearRenderer;
        private Transform lift;
        private SpriteRenderer airLegsRenderer;
        private SpriteRenderer shadowRenderer;
        private bool isLifted;
        private bool wasBodyShown;
        private bool isAttacking;
        private CharacterDirection8 shownFacing = CharacterDirection8.South;
        private CharacterDirection8 shownPartFacing = CharacterDirection8.South;
        private float runSeconds;
        private CharacterActionMotion? weaponKind;
        private readonly Dictionary<CharacterGearSlot, GearPart> parts = new Dictionary<CharacterGearSlot, GearPart>();

        public bool IsRunning { get; private set; }

        /// <summary>
        /// 表示に使うSpriteの定義を注入し、すぐに現在の状態を反映する。全ての部位がこの定義の絵になる。
        /// </summary>
        public void Initialize(CharacterSpriteSetDefinition newSprites)
        {
            sprites = newSprites != null ? newSprites : throw new ArgumentNullException(nameof(newSprites));
            legSprites = newSprites;
            headSprites = newSprites;
            Refresh(0f);
        }

        /// <summary>
        /// 部位を1つだけ着替える。武器の部品はそのまま残る。
        /// </summary>
        public void SetArmour(CharacterArmourSlot slot, CharacterSpriteSetDefinition part)
        {
            if (part == null)
            {
                throw new ArgumentNullException(nameof(part));
            }

            switch (slot)
            {
                case CharacterArmourSlot.Head:
                    headSprites = part;
                    break;
                case CharacterArmourSlot.Legs:
                    legSprites = part;
                    break;
                default:
                    sprites = part;
                    break;
            }

            Refresh(0f);
        }

        /// <summary>
        /// 指定の部位で今着ている定義を返す。
        /// </summary>
        public CharacterSpriteSetDefinition GetArmour(CharacterArmourSlot slot)
        {
            switch (slot)
            {
                case CharacterArmourSlot.Head:
                    return headSprites;
                case CharacterArmourSlot.Legs:
                    return legSprites;
                default:
                    return sprites;
            }
        }

        /// <summary>
        /// 攻撃の姿勢を見せるために、Characterの攻撃状態を読めるようにする。
        /// </summary>
        public void BindCombat(CharacterCombat2D newCombat)
        {
            combat = newCombat;
        }

        /// <summary>
        /// 体へ重ねる部品を差し替える。同じ位置に付く部品は後のものが使われる。
        /// </summary>
        public void SetGear(IReadOnlyList<CharacterGearDefinition> gear)
        {
            foreach (var part in parts.Values)
            {
                part.Gear = null;
                part.Renderer.enabled = false;
                part.Back.enabled = false;
                part.HandOver.enabled = false;
            }

            for (var index = 0; index < gear.Count; index++)
            {
                GetPart(gear[index].Slot).Gear = gear[index];
            }

            Refresh(0f);
        }

        /// <summary>
        /// 持っている武器の武器種を渡す。胴がその武器種の走りを持っていれば、走る間はそのコマになる。
        /// nullなら共通の走りを使う。
        /// </summary>
        public void SetWeaponKind(CharacterActionMotion? newWeaponKind)
        {
            weaponKind = newWeaponKind;
            Refresh(0f);
        }

        /// <summary>
        /// 体と部品の描画順をまとめて変える。部品は向きに応じて体の前か後ろへ置く。
        /// </summary>
        public void SetSortingOrder(int order)
        {
            spriteRenderer.sortingOrder = order;
            ApplyPartOrder();
        }

        /// <summary>
        /// 指定の位置に今表示している部品の絵を返す。
        /// </summary>
        public Sprite GetGearSprite(CharacterGearSlot slot)
        {
            return parts.TryGetValue(slot, out var part) && part.Gear != null ? part.Renderer.sprite : null;
        }

        /// <summary>
        /// 拳の両端へ分けて付ける部品の、柄頭の側に今表示している絵を返す。分けない部品ではnullを返す。
        /// </summary>
        public Sprite GetGearBackSprite(CharacterGearSlot slot)
        {
            return parts.TryGetValue(slot, out var part) && part.Back.enabled ? part.Back.sprite : null;
        }

        /// <summary>
        /// 指定の位置の部品の、表示の重ね順を返す。体の足と同じ順を0とする。
        /// </summary>
        public int GetGearOrder(CharacterGearSlot slot)
        {
            return parts.TryGetValue(slot, out var part) ? part.Renderer.sortingOrder - spriteRenderer.sortingOrder : 0;
        }

        /// <summary>
        /// 胴と頭の表示の重ね順を返す。体の足と同じ順を0とする。
        /// </summary>
        public (int upper, int head) GetBodyOrders()
        {
            return (upperRenderer.sortingOrder - spriteRenderer.sortingOrder, headRenderer.sortingOrder - spriteRenderer.sortingOrder);
        }

        /// <summary>
        /// 指定の位置の部品の上に重ねている拳の絵を返す。重ねていなければnullを返す。
        /// </summary>
        public Sprite GetHandOverSprite(CharacterGearSlot slot)
        {
            return parts.TryGetValue(slot, out var part) && part.HandOver.enabled ? part.HandOver.sprite : null;
        }

        /// <summary>
        /// 今の胴の絵を返す。体全体を1枚で表す体ではnullを返す。
        /// </summary>
        public Sprite GetUpperSprite()
        {
            return upperRenderer != null && upperRenderer.enabled ? upperRenderer.sprite : null;
        }

        /// <summary>
        /// 今の頭の絵を返す。頭の絵がなければnullを返す。
        /// </summary>
        public Sprite GetHeadSprite()
        {
            return headRenderer != null && headRenderer.enabled ? headRenderer.sprite : null;
        }

        /// <summary>
        /// 攻撃中の上半身の、足元から見た兜の位置を返す。攻撃していなければゼロを返す。
        /// </summary>
        public Vector2 GetUpperPosition()
        {
            return upperRenderer != null && upperRenderer.enabled ? (Vector2)upperRenderer.transform.localPosition : Vector2.zero;
        }

        /// <summary>
        /// 今の残像の絵を返す。出ていなければnullを返す。
        /// </summary>
        public Sprite GetSmearSprite()
        {
            return smearRenderer != null && smearRenderer.enabled ? smearRenderer.sprite : null;
        }

        /// <summary>
        /// 指定の位置の部品の、体から見た位置を返す。
        /// </summary>
        public Vector2 GetGearPosition(CharacterGearSlot slot)
        {
            return parts.TryGetValue(slot, out var part) ? (Vector2)part.Renderer.transform.localPosition : Vector2.zero;
        }

        /// <summary>
        /// 指定の位置の部品が体より後ろに描かれているかを返す。
        /// </summary>
        /// <summary>
        /// 跳躍で今持ち上げている高さ。地上では0である。
        /// </summary>
        public float GetLiftHeight()
        {
            return lift != null ? lift.localPosition.y : 0f;
        }

        /// <summary>
        /// 足元の影を見せているかを返す。
        /// </summary>
        public bool IsShadowShown()
        {
            return shadowRenderer != null && shadowRenderer.enabled;
        }

        /// <summary>
        /// 足元の影の大きさ。地上では1で、跳ぶと小さくなる。
        /// </summary>
        public float GetShadowScale()
        {
            return shadowRenderer != null ? shadowRenderer.transform.localScale.x : 0f;
        }

        /// <summary>
        /// 今見えている砂ぼこりの数を返す。
        /// </summary>
        public int GetDustCount()
        {
            var count = 0;
            foreach (var puff in dust)
            {
                count += puff.Renderer.enabled ? 1 : 0;
            }

            return count;
        }

        public bool IsGearBehindBody(CharacterGearSlot slot)
        {
            return parts.TryGetValue(slot, out var part) && part.Renderer.sortingOrder < spriteRenderer.sortingOrder;
        }

        private void Awake()
        {
            actor = GetComponent<CharacterActor2D>();
            spriteRenderer = GetComponent<SpriteRenderer>();

            // 足の絵と当たり判定は足元の物体に付いているので、持ち上げる層は別の親へまとめる。
            var liftObject = new GameObject("Lift");
            liftObject.transform.SetParent(transform, false);
            lift = liftObject.transform;
            upperRenderer = CreateLayer("UpperBody");
            headRenderer = CreateLayer("Head");
            smearRenderer = CreateLayer("Smear");
            airLegsRenderer = CreateLayer("AirLegs");
            shadowRenderer = CreateLayer("Shadow");
            shadowRenderer.transform.SetParent(transform, false);
            shadowRenderer.sprite = GetShadowSprite();
        }

        /// <summary>
        /// Physics更新の後に、そのFrameの状態へ表示を合わせる。
        /// </summary>
        private void LateUpdate()
        {
            if (sprites != null)
            {
                Refresh(Time.deltaTime);
            }

            ApplyLift();
            AdvanceDust(Time.deltaTime);
        }

        private void OnDestroy()
        {
            // 砂ぼこりは体と一緒に動かないようSceneの直下に置くので、体と一緒に片付ける。
            foreach (var puff in dust)
            {
                if (puff.Renderer != null)
                {
                    Destroy(puff.Renderer.gameObject);
                }
            }

            dust.Clear();
        }

        private bool IsAirborne => actor.Jump != null && actor.Jump.IsAirborne;

        /// <summary>
        /// 跳んでいる高さだけ全ての層を持ち上げ、足元の影を高さに合わせて小さくする。
        /// 足の絵は足元の物体に付いていて動かせないので、跳んでいる間は同じ絵を持ち上げた層で見せる。
        /// 影は足、胴、頭の3層の体だけに出す。体全体の絵を1枚で見せる敵は、跳ぶときだけ出す。
        /// </summary>
        private void ApplyLift()
        {
            var jump = actor.Jump;
            var height = jump != null ? jump.Height : 0f;
            lift.localPosition = new Vector3(0f, height, 0f);
            if (IsAirborne && !isLifted)
            {
                isLifted = true;
                wasBodyShown = spriteRenderer.enabled;
                spriteRenderer.enabled = false;
            }
            else if (!IsAirborne && isLifted)
            {
                isLifted = false;
                spriteRenderer.enabled = wasBodyShown;
                SpawnLandingDust();
            }

            var isBodyShown = isLifted ? wasBodyShown : spriteRenderer.enabled;
            if (isLifted)
            {
                ShowLayer(airLegsRenderer, spriteRenderer.sprite, Vector3.zero);
                airLegsRenderer.enabled = wasBodyShown;
                airLegsRenderer.sortingOrder = spriteRenderer.sortingOrder;
            }
            else
            {
                airLegsRenderer.enabled = false;
            }

            shadowRenderer.enabled = isBodyShown && (isLifted || upperRenderer.enabled);
            if (!shadowRenderer.enabled)
            {
                return;
            }

            shadowRenderer.sortingLayerID = spriteRenderer.sortingLayerID;
            shadowRenderer.sortingOrder = spriteRenderer.sortingOrder + ShadowOrder;
            shadowRenderer.color = new Color(0f, 0f, 0f, ShadowAlpha);
            var scale = jump != null ? Mathf.Lerp(1f, ShadowPeakScale, height / jump.PeakHeight) : 1f;
            shadowRenderer.transform.localScale = new Vector3(scale, scale, 1f);
        }

        /// <summary>
        /// 走りのコマが替わったときに呼ぶ。1周は2歩なので、各歩の最初の着地のコマに入ったら、地上なら足元へ砂ぼこりを出す。
        /// </summary>
        private void NoteRunFrame(int frame, int cycle)
        {
            var step = Mathf.Max(1, cycle / 2);
            if (frame != lastRunFrame && frame % step == 0 && !IsAirborne)
            {
                var command = actor.MoveCommand;
                var behind = -new Vector2(command.Horizontal, command.Vertical) * DustBehind;
                SpawnDust(behind);
            }

            lastRunFrame = frame;
        }

        private void SpawnLandingDust()
        {
            SpawnDust(Vector2.left * LandingDustSpread);
            SpawnDust(Vector2.right * LandingDustSpread);
        }

        /// <summary>
        /// 足元から少し離れた所へ砂ぼこりを1つ出す。3層の体だけに出し、空いている物がなければ一番古い物を使う。
        /// </summary>
        private void SpawnDust(Vector2 offset)
        {
            if (!upperRenderer.enabled && !isLifted)
            {
                return;
            }

            DustPuff chosen = null;
            foreach (var puff in dust)
            {
                if (!puff.Renderer.enabled)
                {
                    chosen = puff;
                    break;
                }

                if (chosen == null || puff.Age > chosen.Age)
                {
                    chosen = puff;
                }
            }

            if (chosen == null || (dust.Count < DustCount && chosen.Renderer.enabled))
            {
                var puffObject = new GameObject($"{name}Dust");
                var renderer = puffObject.AddComponent<SpriteRenderer>();
                renderer.sprite = GetDustSprite();
                renderer.sharedMaterial = spriteRenderer.sharedMaterial;
                chosen = new DustPuff(renderer);
                dust.Add(chosen);
            }

            chosen.Age = 0f;
            chosen.Start = (Vector2)transform.position + offset;
            chosen.Renderer.enabled = true;
            chosen.Renderer.sortingLayerID = spriteRenderer.sortingLayerID;
            chosen.Renderer.sortingOrder = spriteRenderer.sortingOrder + DustOrder;
            ShowDust(chosen);
        }

        private void AdvanceDust(float deltaSeconds)
        {
            foreach (var puff in dust)
            {
                if (!puff.Renderer.enabled)
                {
                    continue;
                }

                puff.Age += deltaSeconds;
                if (puff.Age >= DustSeconds)
                {
                    puff.Renderer.enabled = false;
                    continue;
                }

                ShowDust(puff);
            }
        }

        private static void ShowDust(DustPuff puff)
        {
            var share = puff.Age / DustSeconds;
            puff.Renderer.color = new Color(DustColour.r, DustColour.g, DustColour.b, DustAlpha * (1f - share));
            var scale = 1f + DustGrowth * share;
            puff.Renderer.transform.localScale = new Vector3(scale, scale, 1f);
            puff.Renderer.transform.position = puff.Start + Vector2.up * (DustRise * puff.Age);
        }

        /// <summary>
        /// 砂ぼこりの絵。全てのCharacterで共有する小さな塊で、ドットの細かさを体と揃える。
        /// </summary>
        private static Sprite GetDustSprite()
        {
            if (dustSprite != null)
            {
                return dustSprite;
            }

            var width = DustShape[0].Length;
            var height = DustShape.Length;
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = "CharacterDust",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            var pixels = new Color32[width * height];
            for (var row = 0; row < height; row++)
            {
                for (var x = 0; x < width; x++)
                {
                    // 絵の上の行が上に来るよう、下の行から詰める。
                    var filled = DustShape[height - 1 - row][x] == '#';
                    pixels[row * width + x] = filled ? new Color32(255, 255, 255, 255) : new Color32(0, 0, 0, 0);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply();
            dustSprite = Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0f), ShadowPixelsPerUnit);
            dustSprite.name = texture.name;
            return dustSprite;
        }

        private sealed class DustPuff
        {
            public DustPuff(SpriteRenderer renderer)
            {
                Renderer = renderer;
            }

            public SpriteRenderer Renderer { get; }
            public float Age { get; set; }
            public Vector2 Start { get; set; }
        }

        /// <summary>
        /// 足元の影の絵。全てのCharacterで共有する小さな楕円で、ドットの細かさを体と揃える。
        /// </summary>
        private static Sprite GetShadowSprite()
        {
            if (shadowSprite != null)
            {
                return shadowSprite;
            }

            var texture = new Texture2D(ShadowWidth, ShadowHeight, TextureFormat.RGBA32, false)
            {
                name = "CharacterJumpShadow",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            var pixels = new Color32[ShadowWidth * ShadowHeight];
            for (var y = 0; y < ShadowHeight; y++)
            {
                for (var x = 0; x < ShadowWidth; x++)
                {
                    var dx = (x + 0.5f - ShadowWidth / 2f) / (ShadowWidth / 2f);
                    var dy = (y + 0.5f - ShadowHeight / 2f) / (ShadowHeight / 2f);
                    pixels[y * ShadowWidth + x] = dx * dx + dy * dy <= 1f ? new Color32(255, 255, 255, 255) : new Color32(0, 0, 0, 0);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply();
            shadowSprite = Sprite.Create(texture, new Rect(0f, 0f, ShadowWidth, ShadowHeight), new Vector2(0.5f, 0.5f), ShadowPixelsPerUnit);
            shadowSprite.name = texture.name;
            return shadowSprite;
        }

        /// <summary>
        /// 動いている間だけコマを進め、止まると向きに合う待機Spriteへ戻す。
        /// </summary>
        private void Refresh(float deltaSeconds)
        {
            IsRunning = actor.MoveCommand.IsMoving
                        && actor.MovementScale > 0f
                        && actor.Health != null
                        && actor.Health.IsAlive;

            if (TryShowAttack(deltaSeconds))
            {
                return;
            }

            isAttacking = false;
            smearRenderer.enabled = false;
            var facing = UpdateShownFacing();
            spriteRenderer.flipX = IsLeftFacing(facing);

            var view = sprites.GetView(facing);
            var legs = legSprites.GetView(facing);
            if (IsRunning && view.Run.Length > 0)
            {
                var frame = AdvanceRun(IsAirborne ? 0f : deltaSeconds, view);
                NoteRunFrame(frame, view.Run.Length);
                var (runFrames, runAnchors) = GetRun(view);
                var anchors = frame < runAnchors.Length ? runAnchors[frame] : view.IdleAnchors;
                ShowBody(facing, runFrames[frame], frame < legs.RunLegs.Length ? legs.RunLegs[frame] : null, anchors);
                ApplyParts(facing, anchors, HeldAngle(anchors));
                return;
            }

            runSeconds = 0f;
            lastRunFrame = -1;
            ShowBody(facing, view.Idle, legs.IdleLegs, view.IdleAnchors);
            ApplyParts(facing, view.IdleAnchors, HeldAngle(view.IdleAnchors));
        }

        /// <summary>
        /// 足のコマがあれば足、胴、頭の3層で見せ、なければ体全体の絵を1枚で見せる。
        /// </summary>
        private void ShowBody(CharacterDirection8 facing, Sprite torso, Sprite legs, CharacterSpriteAnchors anchors)
        {
            if (legs == null)
            {
                spriteRenderer.sprite = torso;
                upperRenderer.enabled = false;
                headRenderer.enabled = false;
                return;
            }

            spriteRenderer.sprite = legs;
            ShowLayer(upperRenderer, torso, Vector3.zero);
            ShowHead(facing, anchors.Head);
        }

        private void ShowHead(CharacterDirection8 facing, Vector2 position)
        {
            var head = headSprites.GetView(facing).Head;
            headRenderer.enabled = head != null;
            if (head != null)
            {
                ShowLayer(headRenderer, head, new Vector3(spriteRenderer.flipX ? -position.x : position.x, position.y, 0f));
            }
        }

        /// <summary>
        /// Actionの動きの型のコマを体が持っていれば、脚の上へ上半身を重ねる。攻撃の向きは入力が変わっても固定である。
        /// </summary>
        private bool TryShowAttack(float deltaSeconds)
        {
            if (combat == null || !combat.ActionState.IsBusy)
            {
                return false;
            }

            var state = combat.ActionState;
            var view = sprites.GetView(state.Direction);
            var motion = view.GetMotion(state.CurrentSpec.Motion);
            if (motion == null || motion.Frames.Length < AttackFrameCount)
            {
                return false;
            }

            spriteRenderer.flipX = IsLeftFacing(state.Direction);
            var head = ShowLegs(view, legSprites.GetView(state.Direction), state, deltaSeconds);
            var frame = AttackFrame(state);
            var position = new Vector3(spriteRenderer.flipX ? -head.x : head.x, head.y, 0f);
            ShowLayer(upperRenderer, motion.Frames[frame], position);
            ShowHead(state.Direction, head);
            var smear = frame < motion.Smears.Length && ShowsSmear(state) ? motion.Smears[frame] : null;
            smearRenderer.enabled = smear != null;
            if (smear != null)
            {
                ShowLayer(smearRenderer, smear, position);
            }

            var anchors = motion.Anchors[frame];
            isAttacking = true;
            ApplyParts(state.Direction, anchors, HeldAngle(anchors), head);
            return true;
        }

        /// <summary>
        /// 攻撃中の脚を見せ、今の兜の位置を返す。走っていれば走りのコマを進め、止まっていれば型の踏み込みのコマにする。
        /// </summary>
        private Vector2 ShowLegs(CharacterSpriteView view, CharacterSpriteView legs, CharacterActionState state, float deltaSeconds)
        {
            if (IsRunning && legs.RunLegs.Length > 0)
            {
                var frame = AdvanceRun(IsAirborne ? 0f : deltaSeconds, view);
                NoteRunFrame(frame, view.Run.Length);
                spriteRenderer.sprite = legs.RunLegs[frame];
                return GetRun(view).anchors[frame].Head;
            }

            runSeconds = 0f;
            lastRunFrame = -1;

            // 空中では踏み込まず、立ち姿の足のまま振る。
            var stance = IsAirborne ? null : legs.GetMotion(state.CurrentSpec.Motion);
            if (stance != null && stance.StandLegs.Length >= AttackFrameCount)
            {
                var frame = StandFrame(state);
                spriteRenderer.sprite = stance.StandLegs[frame];
                return stance.StandHeads[frame];
            }

            spriteRenderer.sprite = legs.IdleLegs;
            return view.IdleAnchors.Head;
        }

        /// <summary>
        /// 走りの胴のコマと手の位置を返す。持っている武器の武器種の走りがあればそれを、なければ共通の走りを使う。
        /// コマの数はどちらも同じなので、脚とコマの番号が揃う。
        /// </summary>
        private (Sprite[] frames, CharacterSpriteAnchors[] anchors) GetRun(CharacterSpriteView view)
        {
            var weaponRun = weaponKind.HasValue ? view.GetRun(weaponKind.Value) : null;
            return weaponRun != null && weaponRun.Frames.Length == view.Run.Length
                ? (weaponRun.Frames, weaponRun.Anchors)
                : (view.Run, view.RunAnchors);
        }

        /// <summary>
        /// 走りのコマを進める。硬直中などの減速に合わせて、足の動きも遅くする。
        /// </summary>
        private int AdvanceRun(float deltaSeconds, CharacterSpriteView view)
        {
            runSeconds += deltaSeconds * actor.MovementScale;
            return (int)(runSeconds * sprites.FramesPerSecond) % view.Run.Length;
        }

        /// <summary>
        /// 構え、振り2コマ、振り抜きの順にコマを選ぶ。振りの2コマは持続の前半と後半である。
        /// </summary>
        private static int AttackFrame(CharacterActionState state)
        {
            switch (state.Phase)
            {
                case CharacterActionPhase.Windup:
                    return 0;
                case CharacterActionPhase.Active:
                    return state.PhaseProgress < 0.5f ? 1 : 2;
                default:
                    return 3;
            }
        }

        /// <summary>
        /// 踏み込みの脚は上半身と同じ順に進む。硬直は長いので、前半は深く踏み込んだまま止まり、後半で戻り始める。
        /// </summary>
        private static int StandFrame(CharacterActionState state)
        {
            return state.Phase == CharacterActionPhase.Recovery && state.PhaseProgress < StandHoldShare
                ? StandDeepFrame
                : AttackFrame(state);
        }

        /// <summary>
        /// 残像は動きの速い間だけ見せる。硬直に入ると少しで消す。
        /// </summary>
        private static bool ShowsSmear(CharacterActionState state)
        {
            return state.Phase == CharacterActionPhase.Active
                   || (state.Phase == CharacterActionPhase.Recovery && state.PhaseProgress < SmearRecoveryShare);
        }

        private void ShowLayer(SpriteRenderer layer, Sprite sprite, Vector3 position)
        {
            layer.enabled = true;
            layer.sharedMaterial = spriteRenderer.sharedMaterial;
            layer.sortingLayerID = spriteRenderer.sortingLayerID;
            layer.sprite = sprite;
            layer.flipX = spriteRenderer.flipX;
            layer.color = spriteRenderer.color;
            layer.transform.localPosition = position;
        }

        private float HeldAngle(CharacterSpriteAnchors anchors)
        {
            return spriteRenderer.flipX ? 180f - anchors.MainHandAngle : anchors.MainHandAngle;
        }

        private static bool IsLeftFacing(CharacterDirection8 facing)
        {
            return facing == CharacterDirection8.West
                   || facing == CharacterDirection8.NorthWest
                   || facing == CharacterDirection8.SouthWest;
        }

        /// <summary>
        /// 今のコマの手と背中へ部品を置く。左向きは体と同じように反転する。
        /// 攻撃の上半身のコマは兜の位置からの値なので、originへ兜の位置を渡す。
        /// </summary>
        private void ApplyParts(CharacterDirection8 facing, CharacterSpriteAnchors anchors, float weaponAngle, Vector2 origin = default)
        {
            shownPartFacing = facing;
            foreach (var pair in parts)
            {
                var part = pair.Value;
                if (part.Gear == null)
                {
                    continue;
                }

                var anchor = origin + anchors.Get(pair.Key);
                var hand = new Vector2(spriteRenderer.flipX ? -anchor.x : anchor.x, anchor.y);
                var split = part.Gear.IsSplitAtFist && pair.Key == CharacterGearSlot.MainHand;
                var tip = Quaternion.Euler(0f, 0f, weaponAngle) * Vector3.right;
                part.Renderer.enabled = true;
                part.Renderer.sprite = part.Gear.Rotates ? part.Gear.GetRotatedSprite(weaponAngle) : part.Gear.GetSprite(facing);
                part.Renderer.flipX = !part.Gear.Rotates && spriteRenderer.flipX;
                part.Renderer.color = spriteRenderer.color;
                part.Renderer.transform.localPosition = split ? (Vector3)hand + tip * anchors.MainHandFront : (Vector3)hand;

                // 拳の両端へ分けて付ける部品は、握りを描かないので拳を重ねない。柄頭の側は拳の反対の端へ付ける。
                part.Back.enabled = split;
                if (split)
                {
                    part.Back.sprite = part.Gear.GetBackRotatedSprite(weaponAngle);
                    part.Back.color = spriteRenderer.color;
                    part.Back.transform.localPosition = (Vector3)hand + tip * anchors.MainHandBack;
                }

                // 体の後ろへ回った物は、体の絵の拳がそのまま上に来る。
                var held = part.Gear.HeldInFist && !split && !IsBehind(pair.Key, facing);
                var handOver = held ? anchors.GetHandOver(pair.Key) : null;
                part.HandOver.enabled = handOver != null;
                part.HandOver.sprite = handOver;
                part.HandOver.flipX = spriteRenderer.flipX;
                part.HandOver.color = spriteRenderer.color;
                part.HandOver.transform.localPosition = new Vector3(spriteRenderer.flipX ? -origin.x : origin.x, origin.y, 0f);
            }

            ApplyPartOrder();
        }

        private void ApplyPartOrder()
        {
            upperRenderer.sortingOrder = spriteRenderer.sortingOrder + UpperOrder;
            headRenderer.sortingOrder = spriteRenderer.sortingOrder + HeadOrder;
            var mainHand = parts.TryGetValue(CharacterGearSlot.MainHand, out var main) ? main.Gear : null;
            var smearBehind = IsBehind(CharacterGearSlot.MainHand, shownPartFacing);
            var smearOrder = mainHand != null && mainHand.IsSplitAtFist && smearBehind ? SplitBladeOrder() : smearBehind ? -1 : SmearOrder;
            smearRenderer.sortingOrder = spriteRenderer.sortingOrder + smearOrder;
            foreach (var pair in parts)
            {
                // 振った剣は盾の手前を通る。
                var inFront = pair.Key == CharacterGearSlot.MainHand ? MainHandOrder : OffHandOrder;
                var split = pair.Value.Gear != null && pair.Value.Gear.IsSplitAtFist && pair.Key == CharacterGearSlot.MainHand;
                var order = split ? SplitBladeOrder() : IsBehind(pair.Key, shownPartFacing) ? -1 : inFront;
                pair.Value.Renderer.sortingOrder = spriteRenderer.sortingOrder + order;
                pair.Value.Back.sortingOrder = spriteRenderer.sortingOrder + UnderUpperOrder;
                pair.Value.HandOver.sortingOrder = spriteRenderer.sortingOrder + HandOverOrder;
            }
        }

        /// <summary>
        /// 拳の両端へ分けて付ける剣の、鍔から先の重ね順。背を向けて振るときは胴と頭の間を通るので、拳や腕に隠れず、
        /// 兜には隠れる。背を向けて走るときや立つときは胴の下で、拳の下から出る。それ以外は一番手前である。
        /// </summary>
        private int SplitBladeOrder()
        {
            if (!IsBehind(CharacterGearSlot.MainHand, shownPartFacing))
            {
                return MainHandOrder;
            }

            return isAttacking ? UnderHeadOrder : UnderUpperOrder;
        }

        /// <summary>
        /// 体のどちら側に描くかを決める。背中の物は後ろからだけ見え、手の物は体の陰になる向きで後ろへ回す。
        /// </summary>
        private bool IsBehind(CharacterGearSlot slot, CharacterDirection8 facing)
        {
            var isFacingAway = facing == CharacterDirection8.North
                               || facing == CharacterDirection8.NorthEast
                               || facing == CharacterDirection8.NorthWest;
            var isSide = facing == CharacterDirection8.East || facing == CharacterDirection8.West;
            switch (slot)
            {
                case CharacterGearSlot.Back:
                    return !isFacingAway;
                case CharacterGearSlot.OffHand:
                    return isFacingAway || isSide;
                default:
                    // 背を向けて振るときは、剣が体の向こう側を通る。
                    return isAttacking ? isFacingAway : facing == CharacterDirection8.North;
            }
        }

        private GearPart GetPart(CharacterGearSlot slot)
        {
            if (parts.TryGetValue(slot, out var part))
            {
                return part;
            }

            part = new GearPart(CreateLayer($"Gear{slot}"), CreateLayer($"GearBack{slot}"), CreateLayer($"HandOver{slot}"));
            parts.Add(slot, part);
            return part;
        }

        private SpriteRenderer CreateLayer(string layerName)
        {
            var layerObject = new GameObject(layerName);
            layerObject.transform.SetParent(lift, false);
            var layer = layerObject.AddComponent<SpriteRenderer>();
            layer.sharedMaterial = spriteRenderer.sharedMaterial;
            layer.sortingLayerID = spriteRenderer.sortingLayerID;
            layer.enabled = false;
            return layer;
        }

        /// <summary>
        /// 位置ごとの部品の表示。
        /// </summary>
        private sealed class GearPart
        {
            public GearPart(SpriteRenderer renderer, SpriteRenderer back, SpriteRenderer handOver)
            {
                Renderer = renderer;
                Back = back;
                HandOver = handOver;
            }

            public SpriteRenderer Renderer { get; }

            /// <summary>
            /// 拳の両端へ分けて付ける部品の、柄頭の側の絵。
            /// </summary>
            public SpriteRenderer Back { get; }
            public SpriteRenderer HandOver { get; }
            public CharacterGearDefinition Gear { get; set; }
        }

        /// <summary>
        /// 見せる向きはCharacterの向きそのものである。斜めのキーが少しずれて離れても斜めのまま残す決まりは、
        /// 攻撃の向きと揃えるためCharacterActor2Dが持つ。
        /// </summary>
        private CharacterDirection8 UpdateShownFacing()
        {
            shownFacing = actor.Facing;
            return shownFacing;
        }
    }
}
