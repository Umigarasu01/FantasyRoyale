using System;
using FantasyRoyale.Gameplay.Characters;
using FantasyRoyale.MapAuthoringKit;

namespace FantasyRoyale.Gameplay.Events
{
    /// <summary>
    /// 回復の泉のルール処理だけを担当し、UI、Effect、Audioの実再生はPresentation要求として返す。
    /// </summary>
    public sealed class HealingFountainEventHandler : IMapEventHandler
    {
        public const string HealedMessageKey = "event.healing-fountain.healed";
        public const string FullHealthMessageKey = "event.healing-fountain.full-health";
        public const string DefeatedTargetMessageKey =
            "event.healing-fountain.target-defeated";
        public const string MissingHealthTargetMessageKey =
            "event.healing-fountain.missing-health-target";
        public const string HealEffectCueId = "effect.healing-fountain.heal";
        public const string HealAudioCueId = "audio.healing-fountain.heal";

        public Type SupportedDefinitionType => typeof(HealingFountainEventDefinition);

        /// <summary>
        /// 泉Definitionの回復量をHP契約へ適用し、実回復量がある場合だけ成功を返す。
        /// </summary>
        public MapEventExecutionResult Execute(
            MapEventDefinition definition,
            MapEventExecutionContext context)
        {
            if (!(definition is HealingFountainEventDefinition fountainDefinition)
                || context.HealthTarget == null)
            {
                return MapEventExecutionResult.Invalid(
                    new MapEventPresentationRequest(
                        MissingHealthTargetMessageKey,
                        string.Empty,
                        string.Empty));
            }

            if (!context.HealthTarget.IsAlive)
            {
                return MapEventExecutionResult.Rejected(
                    new MapEventPresentationRequest(
                        DefeatedTargetMessageKey,
                        string.Empty,
                        string.Empty));
            }

            var healthResult = context.HealthTarget.RestoreHealth(fountainDefinition.HealAmount);
            if (!healthResult.IsApplied)
            {
                return MapEventExecutionResult.Rejected(
                    new MapEventPresentationRequest(
                        FullHealthMessageKey,
                        string.Empty,
                        string.Empty));
            }

            return MapEventExecutionResult.Success(
                healthResult.AppliedAmount,
                new MapEventPresentationRequest(
                    HealedMessageKey,
                    HealEffectCueId,
                    HealAudioCueId));
        }
    }
}
