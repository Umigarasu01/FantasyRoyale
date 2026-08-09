using System;
using System.Collections.Generic;
using FantasyRoyale.Gameplay.Characters;
using FantasyRoyale.MapAuthoringKit;

namespace FantasyRoyale.Gameplay.Events
{
    /// <summary>
    /// Event処理がルール上どの結果になったかを表し、表示文言やUnity演出と判定を分離する。
    /// </summary>
    public enum MapEventExecutionOutcome
    {
        None = 0,
        Succeeded = 1,
        Rejected = 2,
        Unsupported = 3,
        Invalid = 4
    }

    /// <summary>
    /// Handlerが要求する表示の意味だけを保持する。Prefab生成、UI更新、SE再生はPresenter側が担当する。
    /// </summary>
    public readonly struct MapEventPresentationRequest
    {
        /// <summary>
        /// 表示文言、視覚効果、音響効果の意味IDを一つの要求へまとめる。
        /// </summary>
        public MapEventPresentationRequest(string messageKey, string effectCueId, string audioCueId)
        {
            MessageKey = messageKey ?? string.Empty;
            EffectCueId = effectCueId ?? string.Empty;
            AudioCueId = audioCueId ?? string.Empty;
        }

        public string MessageKey { get; }
        public string EffectCueId { get; }
        public string AudioCueId { get; }
    }

    /// <summary>
    /// Eventの状態更新結果と演出要求を一括して返し、成功判定後のOneShot更新を共通化する。
    /// </summary>
    public readonly struct MapEventExecutionResult
    {
        private MapEventExecutionResult(
            MapEventExecutionOutcome outcome,
            int appliedValue,
            MapEventPresentationRequest presentation)
        {
            Outcome = outcome;
            AppliedValue = appliedValue;
            Presentation = presentation;
        }

        public MapEventExecutionOutcome Outcome { get; }
        public int AppliedValue { get; }
        public MapEventPresentationRequest Presentation { get; }
        public bool IsSuccess => Outcome == MapEventExecutionOutcome.Succeeded;

        /// <summary>
        /// 適用値と演出要求を持つ成功結果を生成する。
        /// </summary>
        public static MapEventExecutionResult Success(
            int appliedValue,
            MapEventPresentationRequest presentation)
        {
            return new MapEventExecutionResult(
                MapEventExecutionOutcome.Succeeded,
                Math.Max(0, appliedValue),
                presentation);
        }

        /// <summary>
        /// 条件不足など、効果を適用せず再試行可能な拒否結果を生成する。
        /// </summary>
        public static MapEventExecutionResult Rejected(MapEventPresentationRequest presentation)
        {
            return new MapEventExecutionResult(
                MapEventExecutionOutcome.Rejected,
                0,
                presentation);
        }

        /// <summary>
        /// RegistryへHandlerが登録されていないEventの結果を生成する。
        /// </summary>
        public static MapEventExecutionResult Unsupported(MapEventPresentationRequest presentation)
        {
            return new MapEventExecutionResult(
                MapEventExecutionOutcome.Unsupported,
                0,
                presentation);
        }

        /// <summary>
        /// Definitionや実行対象が欠けた不正な要求の結果を生成する。
        /// </summary>
        public static MapEventExecutionResult Invalid(MapEventPresentationRequest presentation)
        {
            return new MapEventExecutionResult(
                MapEventExecutionOutcome.Invalid,
                0,
                presentation);
        }
    }

    /// <summary>
    /// Handlerへ渡す実行対象。UnityのGameObjectやTransformを渡さず、必要なルール契約だけを束ねる。
    /// </summary>
    public readonly struct MapEventExecutionContext
    {
        /// <summary>
        /// Eventが操作できるルール対象だけを受け取り、Unity Scene参照を持たないContextを作る。
        /// </summary>
        public MapEventExecutionContext(ICharacterHealthTarget healthTarget)
        {
            HealthTarget = healthTarget;
        }

        public ICharacterHealthTarget HealthTarget { get; }
    }

    /// <summary>
    /// Socket単位の使用済み状態をEvent実行サービスから操作するための最小Runtime契約。
    /// </summary>
    public interface IMapEventRuntimeStateStore
    {
        bool IsUsed(string socketId);

        /// <summary>
        /// 成功済みのOneShot Eventを使用済みにする。呼び出し前にSocket IDと未使用状態は検証済みとする。
        /// </summary>
        void MarkUsed(string socketId);
    }

    /// <summary>
    /// 一種類のMapEventDefinitionに対するルール処理。Unity表示を直接操作せず、実行結果だけを返す。
    /// </summary>
    public interface IMapEventHandler
    {
        Type SupportedDefinitionType { get; }

        /// <summary>
        /// 不変Definitionと純C#の実行対象を受け取り、状態更新と演出要求を結果として返す。
        /// </summary>
        MapEventExecutionResult Execute(
            MapEventDefinition definition,
            MapEventExecutionContext context);
    }

    /// <summary>
    /// Definition型からHandlerをDictionary検索し、イベント種類が増えても呼び出し側の条件分岐を増やさない。
    /// </summary>
    public sealed class MapEventHandlerRegistry
    {
        public const string InvalidEventMessageKey = "event.common.invalid";
        public const string UnsupportedEventMessageKey = "event.common.unsupported";

        private readonly Dictionary<Type, IMapEventHandler> handlers =
            new Dictionary<Type, IMapEventHandler>();

        /// <summary>
        /// 任意の初期Handler群を重複検査しながらRuntime索引へ登録する。
        /// </summary>
        public MapEventHandlerRegistry(IEnumerable<IMapEventHandler> initialHandlers = null)
        {
            if (initialHandlers == null)
            {
                return;
            }

            foreach (var handler in initialHandlers)
            {
                Register(handler);
            }
        }

        public int Count => handlers.Count;

        /// <summary>
        /// 対応Definition型を一意Keyとして登録し、曖昧な上書きや二重登録を例外で止める。
        /// </summary>
        public void Register(IMapEventHandler handler)
        {
            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            var definitionType = handler.SupportedDefinitionType;
            if (definitionType == null
                || !typeof(MapEventDefinition).IsAssignableFrom(definitionType))
            {
                throw new ArgumentException(
                    "HandlerのSupportedDefinitionTypeはMapEventDefinition派生型である必要があります。",
                    nameof(handler));
            }

            if (handlers.ContainsKey(definitionType))
            {
                throw new InvalidOperationException(
                    $"Event Handlerが重複しています: {definitionType.FullName}");
            }

            handlers.Add(definitionType, handler);
        }

        /// <summary>
        /// Definitionの実型へ完全一致するHandlerを取得し、未登録型を別Eventへ誤配送しない。
        /// </summary>
        public bool TryGetHandler(
            MapEventDefinition definition,
            out IMapEventHandler handler)
        {
            if (definition == null)
            {
                handler = null;
                return false;
            }

            return handlers.TryGetValue(definition.GetType(), out handler);
        }

        /// <summary>
        /// 対応Handlerへ処理を委譲し、定義欠落と未対応型も表示可能な結果へ変換する。
        /// </summary>
        public MapEventExecutionResult Execute(
            MapEventDefinition definition,
            MapEventExecutionContext context)
        {
            if (definition == null)
            {
                return MapEventExecutionResult.Invalid(
                    new MapEventPresentationRequest(InvalidEventMessageKey, string.Empty, string.Empty));
            }

            if (!TryGetHandler(definition, out var handler))
            {
                return MapEventExecutionResult.Unsupported(
                    new MapEventPresentationRequest(
                        UnsupportedEventMessageKey,
                        string.Empty,
                        string.Empty));
            }

            return handler.Execute(definition, context);
        }
    }

    /// <summary>
    /// Handler検索、既使用検査、効果成功後のOneShot更新を一つの共通実行順へ固定する。
    /// </summary>
    public sealed class MapEventExecutionService
    {
        public const string InvalidSocketMessageKey = "event.common.invalid-socket";
        public const string AlreadyUsedMessageKey = "event.common.already-used";

        private readonly MapEventHandlerRegistry registry;

        /// <summary>
        /// 検証済みRegistryを受け取り、Event共通実行順を提供するサービスを作る。
        /// </summary>
        public MapEventExecutionService(MapEventHandlerRegistry registry)
        {
            this.registry = registry ?? throw new ArgumentNullException(nameof(registry));
        }

        public int RegisteredHandlerCount => registry.Count;

        /// <summary>
        /// Socketと状態を先に検証してからHandlerを実行し、成功したOneShotだけを使用済みに更新する。
        /// </summary>
        public MapEventExecutionResult Execute(
            string socketId,
            MapEventDefinition definition,
            MapEventExecutionContext context,
            IMapEventRuntimeStateStore stateStore)
        {
            var normalizedSocketId = socketId == null ? string.Empty : socketId.Trim();
            if (normalizedSocketId.Length == 0 || stateStore == null)
            {
                return MapEventExecutionResult.Invalid(
                    new MapEventPresentationRequest(
                        InvalidSocketMessageKey,
                        string.Empty,
                        string.Empty));
            }

            if (definition != null
                && definition.OneShot
                && stateStore.IsUsed(normalizedSocketId))
            {
                return MapEventExecutionResult.Rejected(
                    new MapEventPresentationRequest(
                        AlreadyUsedMessageKey,
                        string.Empty,
                        string.Empty));
            }

            var result = registry.Execute(definition, context);
            if (result.IsSuccess && definition != null && definition.OneShot)
            {
                // 効果が成立しなかったEventは消費しない。成功判定後にだけRuntime状態を更新する。
                stateStore.MarkUsed(normalizedSocketId);
            }

            return result;
        }
    }
}
