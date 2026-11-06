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
        private CharacterActor2D actor;
        private SpriteRenderer spriteRenderer;
        private CharacterSpriteSetDefinition sprites;
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

            var facing = actor.Facing;
            var isDown = facing == CharacterDirection8.South;
            var isUp = facing == CharacterDirection8.North;
            spriteRenderer.flipX = facing == CharacterDirection8.West
                                   || facing == CharacterDirection8.NorthWest
                                   || facing == CharacterDirection8.SouthWest;

            var frames = isDown ? sprites.RunDown : isUp ? sprites.RunUp : sprites.RunSide;
            if (IsRunning && frames.Length > 0)
            {
                // 硬直中などの減速に合わせて、足の動きも遅くする。
                runSeconds += deltaSeconds * actor.MovementScale;
                var frame = (int)(runSeconds * sprites.FramesPerSecond) % frames.Length;
                spriteRenderer.sprite = frames[frame];
                return;
            }

            runSeconds = 0f;
            spriteRenderer.sprite = isDown ? sprites.IdleDown : isUp ? sprites.IdleUp : sprites.IdleSide;
        }
    }
}
