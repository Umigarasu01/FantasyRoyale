using System;
using System.Collections.Generic;
using FantasyRoyale.Gameplay.Characters;
using FantasyRoyale.MapAuthoringKit;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace FantasyRoyale.Gameplay.Events.Tests.Editor
{
    /// <summary>
    /// Unity Sceneを使わず、Handler検索、HP更新、Presentation要求、OneShot更新順を検証する。
    /// </summary>
    public sealed class MapEventRuntimeTests
    {
        private readonly List<ScriptableObject> createdDefinitions =
            new List<ScriptableObject>();

        /// <summary>
        /// 各Testで作成した一時Definitionを破棄し、Asset外の状態を次Testへ持ち越さない。
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            for (var index = 0; index < createdDefinitions.Count; index++)
            {
                Object.DestroyImmediate(createdDefinitions[index]);
            }

            createdDefinitions.Clear();
        }

        /// <summary>
        /// 同じDefinition型へ複数Handlerを登録できず、実行先が一意に保たれることを確認する。
        /// </summary>
        [Test]
        public void Registry_RejectsDuplicateDefinitionHandler()
        {
            var registry = new MapEventHandlerRegistry();
            registry.Register(new HealingFountainEventHandler());

            Assert.Throws<InvalidOperationException>(
                () => registry.Register(new HealingFountainEventHandler()));
        }

        /// <summary>
        /// 回復成功時にHP、演出Cue、OneShot状態が一つの結果として確定することを確認する。
        /// </summary>
        [Test]
        public void Execute_HealingSuccessReturnsCuesAndMarksOneShot()
        {
            var definition = CreateFountainDefinition(true);
            var health = new CharacterHealth(100, 50);
            var state = new FakeRuntimeStateStore();
            var service = CreateService();

            var result = service.Execute(
                "Event_Test_01",
                definition,
                new MapEventExecutionContext(health),
                state);

            Assert.That(result.Outcome, Is.EqualTo(MapEventExecutionOutcome.Succeeded));
            Assert.That(result.AppliedValue, Is.EqualTo(30));
            Assert.That(health.CurrentHealth, Is.EqualTo(80));
            Assert.That(state.IsUsed("Event_Test_01"), Is.True);
            Assert.That(
                result.Presentation.MessageKey,
                Is.EqualTo(HealingFountainEventHandler.HealedMessageKey));
            Assert.That(
                result.Presentation.EffectCueId,
                Is.EqualTo(HealingFountainEventHandler.HealEffectCueId));
            Assert.That(
                result.Presentation.AudioCueId,
                Is.EqualTo(HealingFountainEventHandler.HealAudioCueId));
        }

        /// <summary>
        /// HP満タンでは効果とOneShot更新が発生せず、拒否用表示要求だけを返すことを確認する。
        /// </summary>
        [Test]
        public void Execute_FullHealthReturnsRejectedWithoutConsumingOneShot()
        {
            var definition = CreateFountainDefinition(true);
            var health = new CharacterHealth(100, 100);
            var state = new FakeRuntimeStateStore();

            var result = CreateService().Execute(
                "Event_Test_02",
                definition,
                new MapEventExecutionContext(health),
                state);

            Assert.That(result.Outcome, Is.EqualTo(MapEventExecutionOutcome.Rejected));
            Assert.That(result.AppliedValue, Is.Zero);
            Assert.That(state.IsUsed("Event_Test_02"), Is.False);
            Assert.That(
                result.Presentation.MessageKey,
                Is.EqualTo(HealingFountainEventHandler.FullHealthMessageKey));
            Assert.That(result.Presentation.EffectCueId, Is.Empty);
            Assert.That(result.Presentation.AudioCueId, Is.Empty);
        }

        /// <summary>
        /// 撃破済みCharacterを通常の泉で復活させず、OneShotも消費しないことを確認する。
        /// </summary>
        [Test]
        public void Execute_DefeatedCharacterRejectsHealingWithoutConsumingOneShot()
        {
            var definition = CreateFountainDefinition(true);
            var health = new CharacterHealth(100, 0);
            var state = new FakeRuntimeStateStore();

            var result = CreateService().Execute(
                "Event_Test_Defeated",
                definition,
                new MapEventExecutionContext(health),
                state);

            Assert.That(result.Outcome, Is.EqualTo(MapEventExecutionOutcome.Rejected));
            Assert.That(result.AppliedValue, Is.Zero);
            Assert.That(health.CurrentHealth, Is.Zero);
            Assert.That(health.IsAlive, Is.False);
            Assert.That(state.IsUsed("Event_Test_Defeated"), Is.False);
            Assert.That(
                result.Presentation.MessageKey,
                Is.EqualTo(HealingFountainEventHandler.DefeatedTargetMessageKey));
        }

        /// <summary>
        /// 未登録Definitionが既存Handlerへ誤配送されず、対象状態を変更しないことを確認する。
        /// </summary>
        [Test]
        public void Execute_UnsupportedDefinitionDoesNotMutateHealthOrState()
        {
            var definition = CreateDefinition<MapEventDefinition>();
            definition.Configure("unsupported", "未対応", "E：調べる", 1.25f, true);
            var health = new CharacterHealth(100, 50);
            var state = new FakeRuntimeStateStore();

            var result = CreateService().Execute(
                "Event_Test_03",
                definition,
                new MapEventExecutionContext(health),
                state);

            Assert.That(result.Outcome, Is.EqualTo(MapEventExecutionOutcome.Unsupported));
            Assert.That(health.CurrentHealth, Is.EqualTo(50));
            Assert.That(state.IsUsed("Event_Test_03"), Is.False);
            Assert.That(
                result.Presentation.MessageKey,
                Is.EqualTo(MapEventHandlerRegistry.UnsupportedEventMessageKey));
        }

        /// <summary>
        /// 使用済みOneShotをHandler実行前に拒否し、効果の二重適用を防ぐことを確認する。
        /// </summary>
        [Test]
        public void Execute_RepeatOneShotIsRejectedBeforeApplyingEffect()
        {
            var definition = CreateFountainDefinition(true);
            var health = new CharacterHealth(100, 40);
            var state = new FakeRuntimeStateStore();
            state.MarkUsed("Event_Test_04");

            var result = CreateService().Execute(
                "Event_Test_04",
                definition,
                new MapEventExecutionContext(health),
                state);

            Assert.That(result.Outcome, Is.EqualTo(MapEventExecutionOutcome.Rejected));
            Assert.That(health.CurrentHealth, Is.EqualTo(40));
            Assert.That(
                result.Presentation.MessageKey,
                Is.EqualTo(MapEventExecutionService.AlreadyUsedMessageKey));
        }

        /// <summary>
        /// 再利用可能Eventは成功しても使用済み状態を書き込まないことを確認する。
        /// </summary>
        [Test]
        public void Execute_NonOneShotSuccessDoesNotWriteUsedState()
        {
            var definition = CreateFountainDefinition(false);
            var health = new CharacterHealth(100, 50);
            var state = new FakeRuntimeStateStore();

            var result = CreateService().Execute(
                "Event_Test_05",
                definition,
                new MapEventExecutionContext(health),
                state);

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(health.CurrentHealth, Is.EqualTo(80));
            Assert.That(state.IsUsed("Event_Test_05"), Is.False);
        }

        /// <summary>
        /// 同じSeedなら入力Listの順序が違っても、SocketとEventの割当が完全に一致することを確認する。
        /// </summary>
        [Test]
        public void Placement_SameSeedAndReorderedInputProducesSamePlan()
        {
            var sockets = new[]
            {
                new MapEventSocketCandidate("Event_03"),
                new MapEventSocketCandidate("Event_01"),
                new MapEventSocketCandidate("Event_04"),
                new MapEventSocketCandidate("Event_02")
            };
            var reversedSockets = new[]
            {
                sockets[3], sockets[2], sockets[1], sockets[0]
            };
            var events = new[]
            {
                new MapEventPoolCandidate("shrine", 1),
                new MapEventPoolCandidate("fountain", 3)
            };
            var reversedEvents = new[]
            {
                events[1], events[0]
            };

            Assert.That(
                MapEventPlacementSelector.TryCreatePlan(
                    sockets,
                    events,
                    3,
                    20260807,
                    out var first,
                    out var firstError),
                Is.True,
                firstError);
            Assert.That(
                MapEventPlacementSelector.TryCreatePlan(
                    reversedSockets,
                    reversedEvents,
                    3,
                    20260807,
                    out var second,
                    out var secondError),
                Is.True,
                secondError);

            Assert.That(FormatAssignments(second), Is.EqualTo(FormatAssignments(first)));
            Assert.That(first.Assignments.Count, Is.EqualTo(3));
            Assert.That(
                new HashSet<string>(GetAssignedSocketIds(first), StringComparer.Ordinal),
                Has.Count.EqualTo(3));
        }

        /// <summary>
        /// 有効数が候補Socket数を超える場合、部分結果を返さず抽選前に失敗することを確認する。
        /// </summary>
        [Test]
        public void Placement_WhenActiveCountExceedsSockets_RejectsWholePlan()
        {
            var result = MapEventPlacementSelector.TryCreatePlan(
                new[] { new MapEventSocketCandidate("Event_01") },
                new[] { new MapEventPoolCandidate("fountain", 1) },
                2,
                1,
                out var plan,
                out var error);

            Assert.That(result, Is.False);
            Assert.That(plan.Assignments, Is.Empty);
            Assert.That(error, Does.Contain("候補範囲外"));
        }

        /// <summary>
        /// Pool ScriptableObjectがInspector Listを正本にし、抽選候補とID検索を実行時に構築できることを確認する。
        /// </summary>
        [Test]
        public void EventPoolDefinition_BuildsCandidatesAndDefinitionLookupFromList()
        {
            var fountain = CreateFountainDefinition(true);
            var shrine = CreateDefinition<MapEventDefinition>();
            shrine.Configure("shrine", "祠", "E：祈る", 1.5f, true);
            var pool = CreateDefinition<MapEventPoolDefinition>();
            pool.Configure(
                "test-pool",
                2,
                new[]
                {
                    new MapEventPoolEntry(fountain, 3),
                    new MapEventPoolEntry(shrine, 1)
                });

            Assert.That(pool.Validate(out var validationError), Is.True, validationError);
            Assert.That(
                pool.TryBuildRuntimeCandidates(out var candidates, out var candidateError),
                Is.True,
                candidateError);
            Assert.That(candidates.Count, Is.EqualTo(2));
            Assert.That(pool.TryGetDefinition("healing-test", out var resolved), Is.True);
            Assert.That(resolved, Is.SameAs(fountain));
        }

        /// <summary>
        /// Previewで使う初期Pool Assetが泉一種、重み1、有効3地点として保存されていることを確認する。
        /// </summary>
        [Test]
        public void DefaultEventPoolAsset_UsesThreeSocketsAndHealingFountain()
        {
            const string poolPath = "Assets/Data/Gameplay/Events/BattleRoyaleReferenceEventPool.asset";
            var pool = AssetDatabase.LoadAssetAtPath<MapEventPoolDefinition>(poolPath);

            Assert.That(pool, Is.Not.Null, poolPath);
            Assert.That(pool.Validate(out var validationError), Is.True, validationError);
            Assert.That(pool.PoolId, Is.EqualTo("battle-royale-reference-events"));
            Assert.That(pool.ActiveSocketCount, Is.EqualTo(3));
            Assert.That(pool.Entries, Has.Count.EqualTo(1));
            Assert.That(pool.Entries[0].EventDefinition.EventDefinitionId, Is.EqualTo("healing-fountain-basic"));
            Assert.That(pool.Entries[0].Weight, Is.EqualTo(1));
        }

        /// <summary>
        /// OneShot設定だけを切り替えた回復の泉DefinitionをTest用に生成する。
        /// </summary>
        private HealingFountainEventDefinition CreateFountainDefinition(bool oneShot)
        {
            var definition = CreateDefinition<HealingFountainEventDefinition>();
            definition.ConfigureHealingFountain(
                "healing-test",
                "回復の泉",
                "E：泉を使う",
                1.25f,
                oneShot,
                30);
            return definition;
        }

        /// <summary>
        /// Test終了時に確実に破棄できるよう、生成Definitionを追跡Listへ登録する。
        /// </summary>
        private T CreateDefinition<T>()
            where T : ScriptableObject
        {
            var definition = ScriptableObject.CreateInstance<T>();
            createdDefinitions.Add(definition);
            return definition;
        }

        /// <summary>
        /// 回復の泉Handler一件を登録した共通実行サービスを作る。
        /// </summary>
        private static MapEventExecutionService CreateService()
        {
            return new MapEventExecutionService(
                new MapEventHandlerRegistry(
                    new IMapEventHandler[]
                    {
                        new HealingFountainEventHandler()
                    }));
        }

        /// <summary>
        /// 配置計画を比較しやすい安定文字列へ変換する。
        /// </summary>
        private static string FormatAssignments(MapEventPlacementPlan plan)
        {
            var parts = new string[plan.Assignments.Count];
            for (var index = 0; index < plan.Assignments.Count; index++)
            {
                var assignment = plan.Assignments[index];
                parts[index] = $"{assignment.SocketId}:{assignment.EventDefinitionId}";
            }

            return string.Join("|", parts);
        }

        /// <summary>
        /// 配置計画からSocket IDだけを取り出し、重複選択の検査に使う。
        /// </summary>
        private static IEnumerable<string> GetAssignedSocketIds(MapEventPlacementPlan plan)
        {
            for (var index = 0; index < plan.Assignments.Count; index++)
            {
                yield return plan.Assignments[index].SocketId;
            }
        }

        /// <summary>
        /// OneShot更新の有無と順序だけをScene外で検査するSocket状態StoreのFake。
        /// </summary>
        private sealed class FakeRuntimeStateStore : IMapEventRuntimeStateStore
        {
            private readonly HashSet<string> usedSocketIds =
                new HashSet<string>(StringComparer.Ordinal);

            /// <summary>
            /// 指定SocketがFake Storeへ記録済みか判定する。
            /// </summary>
            public bool IsUsed(string socketId)
            {
                return usedSocketIds.Contains(socketId);
            }

            /// <summary>
            /// 成功したOneShotのSocket IDをFake Storeへ記録する。
            /// </summary>
            public void MarkUsed(string socketId)
            {
                usedSocketIds.Add(socketId);
            }
        }
    }
}
