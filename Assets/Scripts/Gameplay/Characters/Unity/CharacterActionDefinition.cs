using System;
using UnityEngine;

namespace FantasyRoyale.Gameplay.Characters.Unity
{
    /// <summary>
    /// 一つのActionの設定をInspectorで編集するための定義Asset。
    /// 実行時は純C#の`CharacterActionSpec`へ変換し、対戦状態はAssetへ保存しない。
    /// </summary>
    [CreateAssetMenu(
        fileName = "CharacterAction",
        menuName = "FantasyRoyale/Gameplay/Character Action")]
    public sealed class CharacterActionDefinition : ScriptableObject
    {
        /// <summary>
        /// 連撃の段の上限。輪になった設定を見つけるために使う。
        /// </summary>
        public const int MaxComboLength = 8;

        [SerializeField] private string actionId = string.Empty;
        [SerializeField] private CharacterActionKind kind = CharacterActionKind.Melee;
        [SerializeField] private CharacterActionMotion motion = CharacterActionMotion.Swing;
        [SerializeField, Min(1)] private int damage = 1;
        [SerializeField, Min(0f)] private float windupSeconds = 0.1f;
        [SerializeField, Min(0f)] private float activeSeconds = 0.1f;
        [SerializeField, Min(0f)] private float recoverySeconds = 0.2f;
        [SerializeField, Min(0.01f)] private float range = 1f;
        [SerializeField, Range(1f, 360f)] private float arcDegrees = 120f;
        [SerializeField, Min(0.01f)] private float projectileSpeed = 12f;
        [SerializeField] private CharacterActionDefinition nextInCombo;

        public string ActionId => actionId;

        /// <summary>
        /// 体が見せる動きの型。武器の差は型の中の数値で出し、動きの絵は増やさない。
        /// </summary>
        public CharacterActionMotion Motion => motion;

        /// <summary>
        /// 連撃の次の段。続かないActionと最後の段はnullである。
        /// </summary>
        public CharacterActionDefinition NextInCombo => nextInCombo;

        /// <summary>
        /// Editor Builderから近接Actionの値をまとめて設定する。
        /// </summary>
        public void ConfigureMelee(string newActionId, int newDamage, CharacterActionTiming timing, float newRange, float newArcDegrees)
        {
            actionId = newActionId;
            kind = CharacterActionKind.Melee;
            motion = CharacterActionMotion.Swing;
            damage = newDamage;
            ApplyTiming(timing);
            range = newRange;
            arcDegrees = newArcDegrees;
        }

        /// <summary>
        /// Editor Builderから遠隔Actionの値をまとめて設定する。
        /// </summary>
        public void ConfigureProjectile(string newActionId, int newDamage, CharacterActionTiming timing, float newRange, float newProjectileSpeed)
        {
            actionId = newActionId;
            kind = CharacterActionKind.Projectile;
            motion = CharacterActionMotion.Shoot;
            damage = newDamage;
            ApplyTiming(timing);
            range = newRange;
            projectileSpeed = newProjectileSpeed;
        }

        /// <summary>
        /// Editor Builderから連撃の次の段を設定する。
        /// </summary>
        public void SetNextInCombo(CharacterActionDefinition newNext)
        {
            nextInCombo = newNext;
        }

        /// <summary>
        /// 設定値から実行時に使う不変のActionを作る。連撃は次の段まで作る。不正な値は例外で止める。
        /// </summary>
        public CharacterActionSpec CreateSpec()
        {
            return CreateSpec(0);
        }

        private CharacterActionSpec CreateSpec(int step)
        {
            if (step >= MaxComboLength)
            {
                throw new InvalidOperationException($"{actionId}: 連撃が長すぎるか、輪になっています。");
            }

            var next = nextInCombo == null ? null : nextInCombo.CreateSpec(step + 1);
            var timing = new CharacterActionTiming(windupSeconds, activeSeconds, recoverySeconds);
            return kind == CharacterActionKind.Projectile
                ? CharacterActionSpec.CreateProjectile(actionId, damage, timing, range, projectileSpeed, motion)
                : CharacterActionSpec.CreateMelee(actionId, damage, timing, range, arcDegrees, motion, next);
        }

        private void ApplyTiming(CharacterActionTiming timing)
        {
            windupSeconds = timing.WindupSeconds;
            activeSeconds = timing.ActiveSeconds;
            recoverySeconds = timing.RecoverySeconds;
        }
    }
}
