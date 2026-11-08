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
    /// 発生、持続、硬直の時間進行と、連撃のつながりを管理する純C#状態。
    /// 開始後は割り込まれず、撃破時だけCancelで打ち切る。
    /// 連撃は振っている間の入力を次の段へ予約し、硬直が終わると続ける。予約がなくても少しの間は続けられる。
    /// </summary>
    public sealed class CharacterActionState
    {
        /// <summary>
        /// 攻撃中の移動の速さ。走りながら攻撃できるよう、まず通常と同じにしている。
        /// </summary>
        public const float ActingMovementScale = 1f;

        /// <summary>
        /// 段が終わった後、次の段を続けられる秒数。過ぎると最初の段へ戻る。
        /// </summary>
        public const float ComboWindowSeconds = 0.3f;

        private float phaseElapsedSeconds;
        private CharacterActionSpec comboFirst;
        private CharacterActionSpec comboNext;
        private float comboWindowLeft;
        private bool hasQueuedStep;
        private CharacterDirection8 queuedDirection;

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
        /// 実行中の移動の速さを返す。待機中は通常速度である。
        /// </summary>
        public float MovementScale => IsBusy ? ActingMovementScale : 1f;

        /// <summary>
        /// 待機中はActionを開始する。連撃を続けられる間に同じActionを押すと次の段になる。
        /// 実行中は同じ連撃の次の段だけを予約し、他の要求は受け付けない。
        /// </summary>
        public bool TryStart(CharacterActionSpec spec, CharacterDirection8 direction)
        {
            if (spec == null)
            {
                return false;
            }

            if (IsBusy)
            {
                return TryQueueNextStep(spec, direction);
            }

            var continues = comboWindowLeft > 0f && ReferenceEquals(spec, comboFirst);
            if (!continues)
            {
                comboFirst = spec;
            }

            Begin(continues ? comboNext : spec, direction);
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

            if (!IsBusy)
            {
                comboWindowLeft = Math.Max(0f, comboWindowLeft - deltaSeconds);
                return default;
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
                        if (hasQueuedStep)
                        {
                            Begin(CurrentSpec.Next, queuedDirection);
                        }
                        else
                        {
                            comboNext = CurrentSpec.Next;
                            comboWindowLeft = comboNext != null ? ComboWindowSeconds : 0f;
                            Clear();
                            finished = true;
                        }

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
            comboFirst = null;
            comboNext = null;
            comboWindowLeft = 0f;
            hasQueuedStep = false;
        }

        private bool TryQueueNextStep(CharacterActionSpec spec, CharacterDirection8 direction)
        {
            if (hasQueuedStep || CurrentSpec.Next == null || !ReferenceEquals(spec, comboFirst))
            {
                return false;
            }

            hasQueuedStep = true;
            queuedDirection = direction;
            return true;
        }

        private void Begin(CharacterActionSpec spec, CharacterDirection8 direction)
        {
            CurrentSpec = spec;
            Direction = direction;
            Phase = CharacterActionPhase.Windup;
            phaseElapsedSeconds = 0f;
            comboWindowLeft = 0f;
            hasQueuedStep = false;
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
