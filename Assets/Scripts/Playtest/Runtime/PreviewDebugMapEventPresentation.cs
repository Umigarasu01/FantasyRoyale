using FantasyRoyale.Gameplay.Events;
using FantasyRoyale.MapAuthoringKit;

namespace FantasyRoyale.Playtest
{
    /// <summary>
    /// Handlerの意味的なPresentation要求をPreview HUD用テキストへ変換する。
    /// Effect / Audio Cueも保持するが、正式素材の生成と再生は後続Stepへ残す。
    /// </summary>
    public sealed class PreviewDebugMapEventPresenter
    {
        public string LastMessage { get; private set; } = string.Empty;
        public MapEventPresentationRequest LastRequest { get; private set; }

        /// <summary>
        /// Event結果とDefinition表示名から確認文を作り、HandlerからUnity UI操作を排除する。
        /// </summary>
        public string Present(
            MapEventDefinition definition,
            MapEventExecutionResult result)
        {
            LastRequest = result.Presentation;
            var displayName = definition == null || string.IsNullOrWhiteSpace(definition.DisplayName)
                ? "Event"
                : definition.DisplayName;

            switch (result.Presentation.MessageKey)
            {
                case HealingFountainEventHandler.HealedMessageKey:
                    LastMessage = $"{displayName}を使用：HP +{result.AppliedValue}";
                    break;
                case HealingFountainEventHandler.FullHealthMessageKey:
                    LastMessage = "HPはすでに満タンです。泉は消費されません。";
                    break;
                case HealingFountainEventHandler.DefeatedTargetMessageKey:
                    LastMessage = "倒れているため通常の泉では回復できません。";
                    break;
                case MapEventHandlerRegistry.UnsupportedEventMessageKey:
                    LastMessage = $"未対応Eventです: {displayName}";
                    break;
                case MapEventExecutionService.AlreadyUsedMessageKey:
                    LastMessage = $"使用済みEventです: {displayName}";
                    break;
                case HealingFountainEventHandler.MissingHealthTargetMessageKey:
                case MapEventHandlerRegistry.InvalidEventMessageKey:
                case MapEventExecutionService.InvalidSocketMessageKey:
                    LastMessage = $"Eventを実行できません: {displayName}";
                    break;
                default:
                    LastMessage = result.IsSuccess
                        ? $"{displayName}を使用しました。"
                        : $"{displayName}は使用できません。";
                    break;
            }

            return LastMessage;
        }
    }
}
