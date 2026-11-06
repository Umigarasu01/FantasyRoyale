using System;
using UnityEngine;

namespace FantasyRoyale.Gameplay.Characters.Unity
{
    /// <summary>
    /// Actorの向きと移動状態を読み、表示するSpriteを切り替える本番Component。
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
        private float diagonalReleaseSeconds;
        private float runSeconds;

        public bool IsRunning { get; private set; }

        /// <summary>
        /// 表示に使うSpriteの定義を注入し、すぐに現在の状態を反映する。
        /// </summary>
        public void Initialize(CharacterSpriteSetDefinition newSprites)
        {
            sprites = newSprites != null ? newSprites : throw new ArgumentNullException(nameof(newSprites));
            Refresh(0f);
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
                return;
            }

            runSeconds = 0f;
            spriteRenderer.sprite = view.Idle;
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
