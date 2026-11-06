using System;

namespace FantasyRoyale.Gameplay.Characters
{
    /// <summary>
    /// 実行中Actionの段階。
    /// </summary>
    public enum CharacterActionPhase
    {
        Idle = 0,
        Windup = 1,
        Active = 2,
        Recovery = 3
    }

    /// <summary>
    /// 一回の時間更新で起きた段階の変化。一度に複数の段階を通過した場合も全て残す。
    /// </summary>
    public readonly struct CharacterActionAdvanceResult
    {
        public CharacterActionAdvanceResult(bool enteredActive, bool finished)
        {
            EnteredActive = enteredActive;
            Finished = finished;
        }

        public bool EnteredActive { get; }
        public bool Finished { get; }
    }

    /// <summary>
    /// 発生、持続、硬直の時間進行と移動制限を管理する純C#状態。
    /// 開始後は割り込まれず、撃破時だけCancelで打ち切る。
    /// </summary>
    public sealed class CharacterActionState
    {
        public const float RecoveryMovementScale = 0.5f;

        private float phaseElapsedSeconds;

        public CharacterActionPhase Phase { get; private set; }
        public CharacterActionSpec CurrentSpec { get; private set; }
        public CharacterDirection8 Direction { get; private set; }
        public bool IsBusy => Phase != CharacterActionPhase.Idle;

        /// <summary>
        /// 今の段階の進み具合を0から1で返す。長さ0の段階と待機中は1とする。
        /// </summary>
        public float PhaseProgress
        {
            get
            {
                if (!IsBusy)
                {
                    return 1f;
                }

                var duration = GetPhaseDuration();
                return duration > 0f ? Math.Min(1f, phaseElapsedSeconds / duration) : 1f;
            }
        }

        /// <summary>
        /// 発生と持続の間は移動不可、硬直中は半速、待機中は通常速度を返す。
        /// </summary>
        public float MovementScale
        {
            get
            {
                switch (Phase)
                {
                    case CharacterActionPhase.Windup:
                    case CharacterActionPhase.Active:
                        return 0f;
                    case CharacterActionPhase.Recovery:
                        return RecoveryMovementScale;
                    default:
                        return 1f;
                }
            }
        }

        /// <summary>
        /// 待機中だけActionを開始し、実行中の要求は受け付けない。
        /// </summary>
        public bool TryStart(CharacterActionSpec spec, CharacterDirection8 direction)
        {
            if (spec == null || IsBusy)
            {
                return false;
            }

            CurrentSpec = spec;
            Direction = direction;
            Phase = CharacterActionPhase.Windup;
            phaseElapsedSeconds = 0f;
            return true;
        }

        /// <summary>
        /// 経過時間を進める。余った時間は次の段階へ持ち越し、持続0のActionも一度はActiveへ入る。
        /// </summary>
        public CharacterActionAdvanceResult Advance(float deltaSeconds)
        {
            if (float.IsNaN(deltaSeconds) || float.IsInfinity(deltaSeconds) || deltaSeconds < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds), "経過時間は0以上の有限値である必要があります。");
            }

            var enteredActive = false;
            var finished = false;
            var remaining = deltaSeconds;
            while (IsBusy)
            {
                var left = GetPhaseDuration() - phaseElapsedSeconds;
                if (remaining < left)
                {
                    phaseElapsedSeconds += remaining;
                    break;
                }

                remaining -= left;
                phaseElapsedSeconds = 0f;
                switch (Phase)
                {
                    case CharacterActionPhase.Windup:
                        Phase = CharacterActionPhase.Active;
                        enteredActive = true;
                        break;
                    case CharacterActionPhase.Active:
                        Phase = CharacterActionPhase.Recovery;
                        break;
                    default:
                        Clear();
                        finished = true;
                        break;
                }
            }

            return new CharacterActionAdvanceResult(enteredActive, finished);
        }

        /// <summary>
        /// 撃破などでActionを続けられない場合に、途中でも待機へ戻す。
        /// </summary>
        public void Cancel()
        {
            Clear();
        }

        private float GetPhaseDuration()
        {
            switch (Phase)
            {
                case CharacterActionPhase.Windup:
                    return CurrentSpec.Timing.WindupSeconds;
                case CharacterActionPhase.Active:
                    return CurrentSpec.Timing.ActiveSeconds;
                default:
                    return CurrentSpec.Timing.RecoverySeconds;
            }
        }

        private void Clear()
        {
            Phase = CharacterActionPhase.Idle;
            CurrentSpec = null;
            phaseElapsedSeconds = 0f;
        }
    }
}
