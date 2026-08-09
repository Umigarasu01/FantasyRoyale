using System;
using System.Collections.Generic;

namespace FantasyRoyale.Gameplay.Events
{
    /// <summary>
    /// 純C#抽選器へ渡すEvent候補。Unity Asset参照を持たず、安定IDと整数重みだけを保持する。
    /// </summary>
    public readonly struct MapEventPoolCandidate
    {
        public MapEventPoolCandidate(string eventDefinitionId, int weight)
        {
            EventDefinitionId = eventDefinitionId ?? string.Empty;
            Weight = weight;
        }

        public string EventDefinitionId { get; }
        public int Weight { get; }
    }

    /// <summary>
    /// 純C#抽選器へ渡すPool候補地点。Scene参照や座標を持たず、Socket IDだけを保持する。
    /// </summary>
    public readonly struct MapEventSocketCandidate
    {
        public MapEventSocketCandidate(string socketId)
        {
            SocketId = socketId ?? string.Empty;
        }

        public string SocketId { get; }
    }

    /// <summary>
    /// 一つの有効Socketへ割り当てられたEvent Definition IDを表す不変結果。
    /// </summary>
    public readonly struct MapEventPlacementAssignment
    {
        public MapEventPlacementAssignment(string socketId, string eventDefinitionId)
        {
            SocketId = socketId ?? string.Empty;
            EventDefinitionId = eventDefinitionId ?? string.Empty;
        }

        public string SocketId { get; }
        public string EventDefinitionId { get; }
    }

    /// <summary>
    /// 一回の対戦開始時に確定したEvent配置。元のPoolやScene情報を書き換えずに保持する。
    /// </summary>
    public sealed class MapEventPlacementPlan
    {
        private readonly MapEventPlacementAssignment[] assignments;

        /// <summary>
        /// Socket ID順へ確定済みの割当一覧を受け取り、不変な配置計画として保持する。
        /// </summary>
        public MapEventPlacementPlan(MapEventPlacementAssignment[] assignments)
        {
            this.assignments = assignments ?? Array.Empty<MapEventPlacementAssignment>();
        }

        public IReadOnlyList<MapEventPlacementAssignment> Assignments => assignments;
    }

    /// <summary>
    /// Seedから有効Socket選択と重み付きEvent割当を再現可能に生成する純C#サービス。
    /// 入力順に結果が左右されないよう、安定IDで整列してから抽選する。
    /// </summary>
    public static class MapEventPlacementSelector
    {
        /// <summary>
        /// Socket候補、Event候補、有効数、Seedを検証してから一括で配置計画を生成する。
        /// </summary>
        public static bool TryCreatePlan(
            IEnumerable<MapEventSocketCandidate> socketCandidates,
            IEnumerable<MapEventPoolCandidate> eventCandidates,
            int activeSocketCount,
            int seed,
            out MapEventPlacementPlan plan,
            out string error)
        {
            var sockets = socketCandidates == null
                ? new List<MapEventSocketCandidate>()
                : new List<MapEventSocketCandidate>(socketCandidates);
            var events = eventCandidates == null
                ? new List<MapEventPoolCandidate>()
                : new List<MapEventPoolCandidate>(eventCandidates);

            if (!ValidateSockets(sockets, activeSocketCount, out error)
                || !ValidateEvents(events, activeSocketCount, out error))
            {
                plan = new MapEventPlacementPlan(Array.Empty<MapEventPlacementAssignment>());
                return false;
            }

            sockets.Sort((left, right) =>
                string.CompareOrdinal(left.SocketId.Trim(), right.SocketId.Trim()));
            events.Sort((left, right) =>
                string.CompareOrdinal(left.EventDefinitionId.Trim(), right.EventDefinitionId.Trim()));

            var random = new StableRandom(seed);
            // Socketは重複配置しないため、全候補をSeedでShuffleして先頭だけを採用する。
            for (var index = sockets.Count - 1; index > 0; index--)
            {
                var swapIndex = random.NextInt(index + 1);
                var temporary = sockets[index];
                sockets[index] = sockets[swapIndex];
                sockets[swapIndex] = temporary;
            }

            var selectedSocketIds = new string[activeSocketCount];
            for (var index = 0; index < activeSocketCount; index++)
            {
                selectedSocketIds[index] = sockets[index].SocketId.Trim();
            }

            Array.Sort(selectedSocketIds, StringComparer.Ordinal);
            var totalWeight = 0;
            for (var index = 0; index < events.Count; index++)
            {
                checked
                {
                    totalWeight += events[index].Weight;
                }
            }

            var assignments = new MapEventPlacementAssignment[activeSocketCount];
            for (var socketIndex = 0; socketIndex < selectedSocketIds.Length; socketIndex++)
            {
                var roll = random.NextInt(totalWeight);
                var accumulatedWeight = 0;
                var selectedEventId = string.Empty;
                for (var eventIndex = 0; eventIndex < events.Count; eventIndex++)
                {
                    accumulatedWeight += events[eventIndex].Weight;
                    if (roll >= accumulatedWeight)
                    {
                        continue;
                    }

                    selectedEventId = events[eventIndex].EventDefinitionId.Trim();
                    break;
                }

                assignments[socketIndex] = new MapEventPlacementAssignment(
                    selectedSocketIds[socketIndex],
                    selectedEventId);
            }

            plan = new MapEventPlacementPlan(assignments);
            error = string.Empty;
            return true;
        }

        /// <summary>
        /// Socket IDの空値・重複と、有効数が候補数を超える不正を抽選前に除外する。
        /// </summary>
        private static bool ValidateSockets(
            IReadOnlyList<MapEventSocketCandidate> sockets,
            int activeSocketCount,
            out string error)
        {
            if (activeSocketCount < 0 || activeSocketCount > sockets.Count)
            {
                error = $"有効Event Socket数が候補範囲外です: {activeSocketCount} / {sockets.Count}";
                return false;
            }

            var seenIds = new HashSet<string>(StringComparer.Ordinal);
            for (var index = 0; index < sockets.Count; index++)
            {
                var socketId = sockets[index].SocketId == null
                    ? string.Empty
                    : sockets[index].SocketId.Trim();
                if (socketId.Length == 0)
                {
                    error = $"SocketCandidates[{index}] のIDが空です。";
                    return false;
                }

                if (!seenIds.Add(socketId))
                {
                    error = $"Socket候補IDが重複しています: {socketId}";
                    return false;
                }
            }

            error = string.Empty;
            return true;
        }

        /// <summary>
        /// Event ID、重み、重複を検証し、必要な抽選候補が揃っているか確認する。
        /// </summary>
        private static bool ValidateEvents(
            IReadOnlyList<MapEventPoolCandidate> events,
            int activeSocketCount,
            out string error)
        {
            if (activeSocketCount > 0 && events.Count == 0)
            {
                error = "有効Socketへ割り当てるEvent候補がありません。";
                return false;
            }

            var seenIds = new HashSet<string>(StringComparer.Ordinal);
            long totalWeight = 0;
            for (var index = 0; index < events.Count; index++)
            {
                var eventId = events[index].EventDefinitionId == null
                    ? string.Empty
                    : events[index].EventDefinitionId.Trim();
                if (eventId.Length == 0)
                {
                    error = $"EventCandidates[{index}] のIDが空です。";
                    return false;
                }

                if (events[index].Weight <= 0)
                {
                    error = $"Event {eventId} の重みは1以上にしてください。";
                    return false;
                }

                if (!seenIds.Add(eventId))
                {
                    error = $"Event候補IDが重複しています: {eventId}";
                    return false;
                }

                totalWeight += events[index].Weight;
                if (totalWeight > int.MaxValue)
                {
                    error = "Event候補の重み合計が大きすぎます。";
                    return false;
                }
            }

            error = string.Empty;
            return true;
        }

        /// <summary>
        /// Unityや.NET実装差に依存せず、同じint Seedから同じ列を返すXorshift32乱数。
        /// </summary>
        private struct StableRandom
        {
            private uint state;

            public StableRandom(int seed)
            {
                state = unchecked((uint)seed) ^ 0x9E3779B9u;
                if (state == 0u)
                {
                    state = 0x6D2B79F5u;
                }
            }

            /// <summary>
            /// modulo偏りを除外し、0以上maxExclusive未満の整数を決定的に返す。
            /// </summary>
            public int NextInt(int maxExclusive)
            {
                if (maxExclusive <= 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(maxExclusive));
                }

                var bound = (uint)maxExclusive;
                var threshold = unchecked(0u - bound) % bound;
                uint value;
                do
                {
                    value = NextUInt();
                }
                while (value < threshold);

                return (int)(value % bound);
            }

            /// <summary>
            /// Xorshift32の一段を進め、次の符号なし32bit値を返す。
            /// </summary>
            private uint NextUInt()
            {
                var value = state;
                value ^= value << 13;
                value ^= value >> 17;
                value ^= value << 5;
                state = value;
                return value;
            }
        }
    }
}
