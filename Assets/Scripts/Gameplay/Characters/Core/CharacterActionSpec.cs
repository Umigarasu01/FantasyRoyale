using System;

namespace FantasyRoyale.Gameplay.Characters
{
    /// <summary>
    /// 装備が持つActionの枠。入力元はどの枠を使うかだけを指定する。
    /// </summary>
    public enum CharacterActionSlot
    {
        Primary = 0,
        Special = 1
    }

    /// <summary>
    /// Actionの命中方式。Unity側の候補取得方法をここで切り替える。
    /// </summary>
    public enum CharacterActionKind
    {
        Melee = 1,
        Projectile = 2
    }

    /// <summary>
    /// 発生、持続、硬直の秒数。持続0は飛び道具のように一瞬だけ発動するActionを表す。
    /// </summary>
    public readonly struct CharacterActionTiming
    {
        /// <summary>
        /// 0以上の有限値だけを受け取り、設定不正は生成時に例外で止める。
        /// </summary>
        public CharacterActionTiming(float windupSeconds, float activeSeconds, float recoverySeconds)
        {
            Validate(windupSeconds, nameof(windupSeconds));
            Validate(activeSeconds, nameof(activeSeconds));
            Validate(recoverySeconds, nameof(recoverySeconds));
            WindupSeconds = windupSeconds;
            ActiveSeconds = activeSeconds;
            RecoverySeconds = recoverySeconds;
        }

        public float WindupSeconds { get; }
        public float ActiveSeconds { get; }
        public float RecoverySeconds { get; }

        private static void Validate(float seconds, string parameterName)
        {
            if (float.IsNaN(seconds) || float.IsInfinity(seconds) || seconds < 0f)
            {
                throw new ArgumentOutOfRangeException(parameterName, "Actionの秒数は0以上の有限値である必要があります。");
            }
        }
    }

    /// <summary>
    /// 一つのActionの不変設定。Unityの定義Assetから作り、CoreはAssetへ依存しない。
    /// </summary>
    public sealed class CharacterActionSpec
    {
        private CharacterActionSpec(
            string id,
            CharacterActionKind kind,
            int damage,
            CharacterActionTiming timing,
            float range,
            float arcDegrees,
            float projectileSpeed)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException("Action IDが空です。", nameof(id));
            }

            if (damage <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(damage), "Damageは1以上である必要があります。");
            }

            if (float.IsNaN(range) || float.IsInfinity(range) || range <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(range), "Rangeは有限の正数である必要があります。");
            }

            Id = id;
            Kind = kind;
            Damage = damage;
            Timing = timing;
            Range = range;
            ArcDegrees = arcDegrees;
            ProjectileSpeed = projectileSpeed;
        }

        public string Id { get; }
        public CharacterActionKind Kind { get; }
        public int Damage { get; }
        public CharacterActionTiming Timing { get; }
        public float Range { get; }
        public float ArcDegrees { get; }
        public float ProjectileSpeed { get; }

        /// <summary>
        /// 前方の扇形へ当たる近接Actionを作る。
        /// </summary>
        public static CharacterActionSpec CreateMelee(
            string id,
            int damage,
            CharacterActionTiming timing,
            float range,
            float arcDegrees)
        {
            if (float.IsNaN(arcDegrees) || arcDegrees <= 0f || arcDegrees > 360f)
            {
                throw new ArgumentOutOfRangeException(nameof(arcDegrees), "Arcは0より大きく360以下である必要があります。");
            }

            return new CharacterActionSpec(id, CharacterActionKind.Melee, damage, timing, range, arcDegrees, 0f);
        }

        /// <summary>
        /// 向いた方向へ飛ぶ遠隔Actionを作る。
        /// </summary>
        public static CharacterActionSpec CreateProjectile(
            string id,
            int damage,
            CharacterActionTiming timing,
            float range,
            float projectileSpeed)
        {
            if (float.IsNaN(projectileSpeed) || float.IsInfinity(projectileSpeed) || projectileSpeed <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(projectileSpeed), "弾速は有限の正数である必要があります。");
            }

            return new CharacterActionSpec(id, CharacterActionKind.Projectile, damage, timing, range, 0f, projectileSpeed);
        }

        /// <summary>
        /// 攻撃の起点から見た相手の位置が、近接Actionの距離と角度へ収まるかを判定する。
        /// </summary>
        public bool IsInMeleeArea(float offsetX, float offsetY, CharacterDirection8 direction)
        {
            if (Kind != CharacterActionKind.Melee)
            {
                return false;
            }

            var distanceSquared = offsetX * offsetX + offsetY * offsetY;
            if (distanceSquared > Range * Range)
            {
                return false;
            }

            // 起点と重なる相手は方向を定義できないため、当たりとして扱う。
            if (distanceSquared <= 0f)
            {
                return true;
            }

            CharacterDirections.ToUnitVector(direction, out var directionX, out var directionY);
            var cosine = (offsetX * directionX + offsetY * directionY) / Math.Sqrt(distanceSquared);
            var halfArcRadians = ArcDegrees * 0.5d * Math.PI / 180d;
            return cosine >= Math.Cos(halfArcRadians);
        }
    }

    /// <summary>
    /// 人間、CPU、将来のNetwork入力が共通して渡す、一回分のAction開始要求。
    /// </summary>
    public readonly struct CharacterActionCommand
    {
        public CharacterActionCommand(CharacterActionSlot slot, CharacterDirection8 direction)
        {
            Slot = slot;
            Direction = direction;
        }

        public CharacterActionSlot Slot { get; }
        public CharacterDirection8 Direction { get; }
    }

    /// <summary>
    /// 入力元を知らずにAction開始要求を受け取るためのCharacter側の最小契約。
    /// </summary>
    public interface ICharacterActionCommandTarget
    {
        /// <summary>
        /// Actionを開始できた場合だけtrueを返す。実行中や空のSlotでは開始しない。
        /// </summary>
        bool TryStartAction(CharacterActionCommand command);
    }
}
