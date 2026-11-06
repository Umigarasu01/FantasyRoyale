using System;
using System.Collections.Generic;
using UnityEngine;

namespace FantasyRoyale.Gameplay.Characters.Unity
{
    /// <summary>
    /// Actorの向きと移動状態を読み、表示するSpriteを切り替える本番Component。
    /// 装備の部品は体の手と背中の位置へ毎Frame重ねるので、部品を替えてもアニメーションは保たれる。
    /// 表示だけを担当し、移動や判定には影響しない。
    /// </summary>
    [AddComponentMenu("FantasyRoyale/Gameplay/Characters/Character Sprite Animator 2D")]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterActor2D), typeof(SpriteRenderer))]
    public sealed class CharacterSpriteAnimator2D : MonoBehaviour
    {
        private const float DiagonalReleaseWaitSeconds = 0.08f;

        private CharacterActor2D actor;
        private SpriteRenderer spriteRenderer;
        private CharacterSpriteSetDefinition sprites;
        private CharacterDirection8 shownFacing = CharacterDirection8.South;
        private CharacterDirection8 shownPartFacing = CharacterDirection8.South;
        private float diagonalReleaseSeconds;
        private float runSeconds;
        private readonly Dictionary<CharacterGearSlot, GearPart> parts = new Dictionary<CharacterGearSlot, GearPart>();

        public bool IsRunning { get; private set; }

        /// <summary>
        /// 表示に使うSpriteの定義を注入し、すぐに現在の状態を反映する。
        /// </summary>
        public void Initialize(CharacterSpriteSetDefinition newSprites)
        {
            sprites = newSprites != null ? newSprites : throw new ArgumentNullException(nameof(newSprites));
            Refresh(0f);
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
            }

            for (var index = 0; index < gear.Count; index++)
            {
                GetPart(gear[index].Slot).Gear = gear[index];
            }

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
        /// 指定の位置の部品の、体から見た位置を返す。
        /// </summary>
        public Vector2 GetGearPosition(CharacterGearSlot slot)
        {
            return parts.TryGetValue(slot, out var part) ? (Vector2)part.Renderer.transform.localPosition : Vector2.zero;
        }

        /// <summary>
        /// 指定の位置の部品が体より後ろに描かれているかを返す。
        /// </summary>
        public bool IsGearBehindBody(CharacterGearSlot slot)
        {
            return parts.TryGetValue(slot, out var part) && part.Renderer.sortingOrder < spriteRenderer.sortingOrder;
        }

        private void Awake()
        {
            actor = GetComponent<CharacterActor2D>();
            spriteRenderer = GetComponent<SpriteRenderer>();
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

            var facing = UpdateShownFacing(deltaSeconds);
            spriteRenderer.flipX = facing == CharacterDirection8.West
                                   || facing == CharacterDirection8.NorthWest
                                   || facing == CharacterDirection8.SouthWest;

            var view = sprites.GetView(facing);
            if (IsRunning && view.Run.Length > 0)
            {
                // 硬直中などの減速に合わせて、足の動きも遅くする。
                runSeconds += deltaSeconds * actor.MovementScale;
                var frame = (int)(runSeconds * sprites.FramesPerSecond) % view.Run.Length;
                spriteRenderer.sprite = view.Run[frame];
                ApplyParts(facing, frame < view.RunAnchors.Length ? view.RunAnchors[frame] : view.IdleAnchors);
                return;
            }

            runSeconds = 0f;
            spriteRenderer.sprite = view.Idle;
            ApplyParts(facing, view.IdleAnchors);
        }

        /// <summary>
        /// 今のコマの手と背中へ部品を置く。左向きは体と同じように反転する。
        /// </summary>
        private void ApplyParts(CharacterDirection8 facing, CharacterSpriteAnchors anchors)
        {
            shownPartFacing = facing;
            foreach (var pair in parts)
            {
                var part = pair.Value;
                if (part.Gear == null)
                {
                    continue;
                }

                var anchor = anchors.Get(pair.Key);
                part.Renderer.enabled = true;
                part.Renderer.sprite = part.Gear.GetSprite(facing);
                part.Renderer.flipX = spriteRenderer.flipX;
                part.Renderer.color = spriteRenderer.color;
                part.Renderer.transform.localPosition = new Vector3(spriteRenderer.flipX ? -anchor.x : anchor.x, anchor.y, 0f);
            }

            ApplyPartOrder();
        }

        private void ApplyPartOrder()
        {
            foreach (var pair in parts)
            {
                pair.Value.Renderer.sortingOrder =
                    spriteRenderer.sortingOrder + (IsBehind(pair.Key, shownPartFacing) ? -1 : 1);
            }
        }

        /// <summary>
        /// 体のどちら側に描くかを決める。背中の物は後ろからだけ見え、手の物は体の陰になる向きで後ろへ回す。
        /// </summary>
        private static bool IsBehind(CharacterGearSlot slot, CharacterDirection8 facing)
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
                    return facing == CharacterDirection8.North;
            }
        }

        private GearPart GetPart(CharacterGearSlot slot)
        {
            if (parts.TryGetValue(slot, out var part))
            {
                return part;
            }

            var partObject = new GameObject($"Gear{slot}");
            partObject.transform.SetParent(transform, false);
            var renderer = partObject.AddComponent<SpriteRenderer>();
            renderer.sharedMaterial = spriteRenderer.sharedMaterial;
            renderer.sortingLayerID = spriteRenderer.sortingLayerID;
            part = new GearPart(renderer);
            parts.Add(slot, part);
            return part;
        }

        /// <summary>
        /// 位置ごとの部品の表示。
        /// </summary>
        private sealed class GearPart
        {
            public GearPart(SpriteRenderer renderer)
            {
                Renderer = renderer;
            }

            public SpriteRenderer Renderer { get; }
            public CharacterGearDefinition Gear { get; set; }
        }

        /// <summary>
        /// 斜めの2つのキーは同時には離れないため、斜めから隣の向きへ変わっても少し待つ。
        /// その間に止まれば、斜めの向きのまま立つ。
        /// </summary>
        private CharacterDirection8 UpdateShownFacing(float deltaSeconds)
        {
            if (!IsDiagonalRelease(shownFacing, actor.Facing))
            {
                shownFacing = actor.Facing;
                diagonalReleaseSeconds = 0f;
                return shownFacing;
            }

            if (actor.MoveCommand.IsMoving)
            {
                diagonalReleaseSeconds += deltaSeconds;
                if (diagonalReleaseSeconds >= DiagonalReleaseWaitSeconds)
                {
                    shownFacing = actor.Facing;
                    diagonalReleaseSeconds = 0f;
                }
            }

            return shownFacing;
        }

        /// <summary>
        /// 斜めの向きから、片方のキーを離した隣の向きへ変わったかを判定する。
        /// 8方向は東から順に並ぶので、奇数が斜めで、差が1なら隣である。
        /// </summary>
        private static bool IsDiagonalRelease(CharacterDirection8 shown, CharacterDirection8 facing)
        {
            var step = ((int)facing - (int)shown + 8) % 8;
            return (int)shown % 2 == 1 && (step == 1 || step == 7);
        }
    }
}
