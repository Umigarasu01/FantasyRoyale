using System;

namespace FantasyRoyale.Gameplay.Characters
{
    /// <summary>
    /// HP変更がダメージと回復のどちらとして要求されたかを表す。
    /// </summary>
    public enum CharacterHealthChangeKind
    {
        None = 0,
        Damage = 1,
        Healing = 2
    }

    /// <summary>
    /// HP変更が適用、条件拒否、不正入力のどれで終了したかを表す。
    /// </summary>
    public enum CharacterHealthChangeOutcome
    {
        None = 0,
        Applied = 1,
        Rejected = 2,
        Invalid = 3
    }

    /// <summary>
    /// 一回のHP変更前後を不変値として返し、表示や撃破判定がCharacter内部を書き換えないようにする。
    /// </summary>
    public readonly struct CharacterHealthChangeResult
    {
        /// <summary>
        /// HP変更の種類、結果、要求量、実適用量、変更前後を一つの結果へまとめる。
        /// </summary>
        public CharacterHealthChangeResult(
            CharacterHealthChangeKind kind,
            CharacterHealthChangeOutcome outcome,
            int requestedAmount,
            int appliedAmount,
            int previousHealth,
            int currentHealth)
        {
            Kind = kind;
            Outcome = outcome;
            RequestedAmount = requestedAmount;
            AppliedAmount = appliedAmount;
            PreviousHealth = previousHealth;
            CurrentHealth = currentHealth;
        }

        public CharacterHealthChangeKind Kind { get; }
        public CharacterHealthChangeOutcome Outcome { get; }
        public int RequestedAmount { get; }
        public int AppliedAmount { get; }
        public int PreviousHealth { get; }
        public int CurrentHealth { get; }
        public bool IsApplied => Outcome == CharacterHealthChangeOutcome.Applied;
        public bool WasAlive => PreviousHealth > 0;
        public bool IsAlive => CurrentHealth > 0;
        public bool BecameDefeated => IsApplied && WasAlive && !IsAlive;
    }

    /// <summary>
    /// Event、戦闘、エリア外ダメージが同じCharacter HPを操作するための純C#契約。
    /// </summary>
    public interface ICharacterHealthTarget
    {
        int CurrentHealth { get; }
        int MaximumHealth { get; }
        bool IsAlive { get; }

        /// <summary>
        /// 正のダメージ要求を現在HPへ適用し、実ダメージ量と撃破遷移を返す。
        /// </summary>
        CharacterHealthChangeResult ApplyDamage(int requestedAmount);

        /// <summary>
        /// 正の回復要求を生存中のHPへ適用し、上限Clamp後の実回復量を返す。
        /// </summary>
        CharacterHealthChangeResult RestoreHealth(int requestedAmount);
    }

    /// <summary>
    /// Characterの最大HP、現在HP、生死を管理する本番用の純C#状態。
    /// 通常回復で死亡状態を解除せず、将来の復活処理を別の明示的な契約として追加できるようにする。
    /// </summary>
    public sealed class CharacterHealth : ICharacterHealthTarget
    {
        private int currentHealth;

        /// <summary>
        /// 最大HPで開始するCharacter Healthを作る。
        /// </summary>
        public CharacterHealth(int maximumHealth)
            : this(maximumHealth, maximumHealth)
        {
        }

        /// <summary>
        /// 正の最大HPと0以上最大以下の初期HPを受け取り、設定不正は生成時に例外で止める。
        /// </summary>
        public CharacterHealth(int maximumHealth, int startingHealth)
        {
            if (maximumHealth <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(maximumHealth),
                    "Maximum Healthは1以上である必要があります。");
            }

            if (startingHealth < 0 || startingHealth > maximumHealth)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(startingHealth),
                    "Starting Healthは0以上Maximum Health以下である必要があります。");
            }

            MaximumHealth = maximumHealth;
            currentHealth = startingHealth;
        }

        public int CurrentHealth => currentHealth;
        public int MaximumHealth { get; }
        public bool IsAlive => currentHealth > 0;

        /// <summary>
        /// ダメージを現在HPまでにClampして適用し、0到達を撃破遷移として結果へ残す。
        /// </summary>
        public CharacterHealthChangeResult ApplyDamage(int requestedAmount)
        {
            if (requestedAmount <= 0)
            {
                return CreateUnchangedResult(
                    CharacterHealthChangeKind.Damage,
                    CharacterHealthChangeOutcome.Invalid,
                    requestedAmount);
            }

            if (!IsAlive)
            {
                return CreateUnchangedResult(
                    CharacterHealthChangeKind.Damage,
                    CharacterHealthChangeOutcome.Rejected,
                    requestedAmount);
            }

            var previousHealth = currentHealth;
            var appliedAmount = Math.Min(requestedAmount, currentHealth);
            currentHealth -= appliedAmount;
            return new CharacterHealthChangeResult(
                CharacterHealthChangeKind.Damage,
                CharacterHealthChangeOutcome.Applied,
                requestedAmount,
                appliedAmount,
                previousHealth,
                currentHealth);
        }

        /// <summary>
        /// 生存中だけ回復を最大HPまで適用し、通常回復による復活を起こさない。
        /// </summary>
        public CharacterHealthChangeResult RestoreHealth(int requestedAmount)
        {
            if (requestedAmount <= 0)
            {
                return CreateUnchangedResult(
                    CharacterHealthChangeKind.Healing,
                    CharacterHealthChangeOutcome.Invalid,
                    requestedAmount);
            }

            if (!IsAlive || currentHealth >= MaximumHealth)
            {
                return CreateUnchangedResult(
                    CharacterHealthChangeKind.Healing,
                    CharacterHealthChangeOutcome.Rejected,
                    requestedAmount);
            }

            var previousHealth = currentHealth;
            var appliedAmount = Math.Min(requestedAmount, MaximumHealth - currentHealth);
            currentHealth += appliedAmount;
            return new CharacterHealthChangeResult(
                CharacterHealthChangeKind.Healing,
                CharacterHealthChangeOutcome.Applied,
                requestedAmount,
                appliedAmount,
                previousHealth,
                currentHealth);
        }

        /// <summary>
        /// 不正入力や条件拒否でHPを変えず、要求内容と現在値を診断可能な結果として返す。
        /// </summary>
        private CharacterHealthChangeResult CreateUnchangedResult(
            CharacterHealthChangeKind kind,
            CharacterHealthChangeOutcome outcome,
            int requestedAmount)
        {
            return new CharacterHealthChangeResult(
                kind,
                outcome,
                requestedAmount,
                0,
                currentHealth,
                currentHealth);
        }
    }
}
