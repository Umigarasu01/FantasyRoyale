using System;
using NUnit.Framework;

namespace FantasyRoyale.Gameplay.Characters.Tests.Editor
{
    /// <summary>
    /// Unity Sceneを使わず、8方向、Actionの時間進行、装備、多重命中の防止を検証する。
    /// </summary>
    public sealed class CharacterActionTests
    {
        /// <summary>
        /// 段階の進み具合が0から1へ進み、待機中は1を返すことを確認する。
        /// </summary>
        [Test]
        public void PhaseProgress_FollowsThePhase()
        {
            var state = new CharacterActionState();
            Assert.That(state.PhaseProgress, Is.EqualTo(1f));

            var spec = CharacterActionSpec.CreateMelee("test", 1, new CharacterActionTiming(1f, 1f, 1f), 1f, 90f);
            state.TryStart(spec, CharacterDirection8.East);
            Assert.That(state.PhaseProgress, Is.EqualTo(0f));
            state.Advance(0.5f);
            Assert.That(state.PhaseProgress, Is.EqualTo(0.5f).Within(0.001f));
        }

        private static CharacterActionSpec CreateSword()
        {
            return CharacterActionSpec.CreateMelee(
                "sword",
                20,
                new CharacterActionTiming(0.1f, 0.1f, 0.2f),
                1.4f,
                120f);
        }

        private static CharacterActionSpec CreateBow()
        {
            return CharacterActionSpec.CreateProjectile(
                "bow",
                12,
                new CharacterActionTiming(0.25f, 0f, 0.3f),
                6f,
                12f);
        }

        /// <summary>
        /// 入力値が最も近い8方向へ丸められることを確認する。
        /// </summary>
        [TestCase(1f, 0f, CharacterDirection8.East)]
        [TestCase(1f, 1f, CharacterDirection8.NorthEast)]
        [TestCase(0f, 1f, CharacterDirection8.North)]
        [TestCase(-1f, 1f, CharacterDirection8.NorthWest)]
        [TestCase(-1f, 0f, CharacterDirection8.West)]
        [TestCase(-1f, -1f, CharacterDirection8.SouthWest)]
        [TestCase(0f, -1f, CharacterDirection8.South)]
        [TestCase(1f, -1f, CharacterDirection8.SouthEast)]
        [TestCase(0.9f, 0.1f, CharacterDirection8.East)]
        public void FromVector_RoundsToNearestDirection(float x, float y, CharacterDirection8 expected)
        {
            Assert.That(CharacterDirections.FromVector(x, y, CharacterDirection8.South), Is.EqualTo(expected));
        }

        /// <summary>
        /// 入力がないときは最後の向きを保てるようFallbackを返すことを確認する。
        /// </summary>
        [Test]
        public void FromVector_ReturnsFallbackWithoutInput()
        {
            Assert.That(
                CharacterDirections.FromVector(0f, 0f, CharacterDirection8.West),
                Is.EqualTo(CharacterDirection8.West));
            Assert.That(
                CharacterDirections.FromVector(float.NaN, 1f, CharacterDirection8.West),
                Is.EqualTo(CharacterDirection8.West));
        }

        /// <summary>
        /// 8方向すべてが単位長のVectorへ変換でき、元の方向へ戻せることを確認する。
        /// </summary>
        [Test]
        public void ToUnitVector_RoundTripsAllDirections()
        {
            foreach (CharacterDirection8 direction in Enum.GetValues(typeof(CharacterDirection8)))
            {
                CharacterDirections.ToUnitVector(direction, out var x, out var y);
                Assert.That(x * x + y * y, Is.EqualTo(1f).Within(0.0001f));
                Assert.That(CharacterDirections.FromVector(x, y, CharacterDirection8.East), Is.EqualTo(direction));
            }
        }

        /// <summary>
        /// 不正な秒数とDamageを生成時に拒否することを確認する。
        /// </summary>
        [Test]
        public void Spec_RejectsInvalidSettings()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new CharacterActionTiming(-0.1f, 0f, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new CharacterActionTiming(0f, float.NaN, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                CharacterActionSpec.CreateMelee("sword", 0, default, 1f, 90f));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                CharacterActionSpec.CreateMelee("sword", 1, default, 0f, 90f));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                CharacterActionSpec.CreateMelee("sword", 1, default, 1f, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                CharacterActionSpec.CreateProjectile("bow", 1, default, 1f, 0f));
            Assert.Throws<ArgumentException>(() =>
                CharacterActionSpec.CreateMelee(" ", 1, default, 1f, 90f));
        }

        /// <summary>
        /// 近接Actionが距離と前方の角度の両方で当たりを判定することを確認する。
        /// </summary>
        [Test]
        public void IsInMeleeArea_ChecksDistanceAndArc()
        {
            var sword = CreateSword();

            Assert.That(sword.IsInMeleeArea(1f, 0f, CharacterDirection8.East), Is.True);
            Assert.That(sword.IsInMeleeArea(0.5f, 0.8f, CharacterDirection8.East), Is.True, "59度は120度の扇の内側です。");
            Assert.That(sword.IsInMeleeArea(0.4f, 0.9f, CharacterDirection8.East), Is.False, "66度は扇の外側です。");
            Assert.That(sword.IsInMeleeArea(1.5f, 0f, CharacterDirection8.East), Is.False, "距離が足りません。");
            Assert.That(sword.IsInMeleeArea(-1f, 0f, CharacterDirection8.East), Is.False, "背後には当たりません。");
            Assert.That(sword.IsInMeleeArea(0f, 0f, CharacterDirection8.East), Is.True);
            Assert.That(CreateBow().IsInMeleeArea(1f, 0f, CharacterDirection8.East), Is.False);
        }

        /// <summary>
        /// 発生、持続、硬直の順に進み、段階ごとの移動制限を返すことを確認する。
        /// </summary>
        [Test]
        public void Advance_MovesThroughPhasesWithMovementLimits()
        {
            var state = new CharacterActionState();
            Assert.That(state.MovementScale, Is.EqualTo(1f));
            Assert.That(state.TryStart(CreateSword(), CharacterDirection8.North), Is.True);
            Assert.That(state.Phase, Is.EqualTo(CharacterActionPhase.Windup));
            Assert.That(state.Direction, Is.EqualTo(CharacterDirection8.North));
            Assert.That(state.MovementScale, Is.EqualTo(0f));

            var result = state.Advance(0.05f);
            Assert.That(state.Phase, Is.EqualTo(CharacterActionPhase.Windup));
            Assert.That(result.EnteredActive, Is.False);

            result = state.Advance(0.06f);
            Assert.That(state.Phase, Is.EqualTo(CharacterActionPhase.Active));
            Assert.That(result.EnteredActive, Is.True);
            Assert.That(state.MovementScale, Is.EqualTo(0f));

            state.Advance(0.1f);
            Assert.That(state.Phase, Is.EqualTo(CharacterActionPhase.Recovery));
            Assert.That(state.MovementScale, Is.EqualTo(CharacterActionState.RecoveryMovementScale));

            result = state.Advance(0.3f);
            Assert.That(result.Finished, Is.True);
            Assert.That(state.Phase, Is.EqualTo(CharacterActionPhase.Idle));
            Assert.That(state.CurrentSpec, Is.Null);
            Assert.That(state.MovementScale, Is.EqualTo(1f));
        }

        /// <summary>
        /// 持続0の遠隔Actionでも発動の機会を失わず、硬直へ進むことを確認する。
        /// </summary>
        [Test]
        public void Advance_ZeroActiveStillReportsEnteredActive()
        {
            var state = new CharacterActionState();
            state.TryStart(CreateBow(), CharacterDirection8.East);

            var result = state.Advance(0.26f);

            Assert.That(result.EnteredActive, Is.True);
            Assert.That(state.Phase, Is.EqualTo(CharacterActionPhase.Recovery));
        }

        /// <summary>
        /// 長い時間更新で全段階を飛び越えても、発動と終了の両方を報告することを確認する。
        /// </summary>
        [Test]
        public void Advance_LongStepReportsActiveAndFinish()
        {
            var state = new CharacterActionState();
            state.TryStart(CreateSword(), CharacterDirection8.East);

            var result = state.Advance(5f);

            Assert.That(result.EnteredActive, Is.True);
            Assert.That(result.Finished, Is.True);
            Assert.That(state.IsBusy, Is.False);
        }

        /// <summary>
        /// 実行中は割り込めず、空のSlotや不正な経過時間を受け付けないことを確認する。
        /// </summary>
        [Test]
        public void TryStart_RejectsBusyAndEmptyRequests()
        {
            var state = new CharacterActionState();
            Assert.That(state.TryStart(null, CharacterDirection8.East), Is.False);
            state.TryStart(CreateSword(), CharacterDirection8.East);

            Assert.That(state.TryStart(CreateBow(), CharacterDirection8.West), Is.False);
            Assert.That(state.CurrentSpec.Id, Is.EqualTo("sword"));
            Assert.That(state.Direction, Is.EqualTo(CharacterDirection8.East));
            Assert.Throws<ArgumentOutOfRangeException>(() => state.Advance(-1f));
        }

        /// <summary>
        /// 撃破時のCancelで実行中のActionを打ち切れることを確認する。
        /// </summary>
        [Test]
        public void Cancel_ReturnsToIdle()
        {
            var state = new CharacterActionState();
            state.TryStart(CreateSword(), CharacterDirection8.East);

            state.Cancel();

            Assert.That(state.Phase, Is.EqualTo(CharacterActionPhase.Idle));
            Assert.That(state.TryStart(CreateBow(), CharacterDirection8.West), Is.True);
        }

        /// <summary>
        /// 装備の差し替えで両方のSlotのActionが替わり、空のSlotも表せることを確認する。
        /// </summary>
        [Test]
        public void Loadout_ReplacesBothSlots()
        {
            var sword = CreateSword();
            var bow = CreateBow();
            var loadout = new CharacterLoadout(sword, bow);
            Assert.That(loadout.Get(CharacterActionSlot.Primary), Is.SameAs(sword));
            Assert.That(loadout.Get(CharacterActionSlot.Special), Is.SameAs(bow));

            loadout.Replace(bow, null);

            Assert.That(loadout.Get(CharacterActionSlot.Primary), Is.SameAs(bow));
            Assert.That(loadout.Get(CharacterActionSlot.Special), Is.Null);
        }

        /// <summary>
        /// 同じ攻撃で同じ相手へ一度だけ当たり、次の攻撃では再び当たることを確認する。
        /// </summary>
        [Test]
        public void HitTracker_AppliesDamageOncePerAttack()
        {
            var attacker = new CharacterHealth(100);
            var target = new CharacterHealth(100);
            var tracker = new CharacterHitTracker();

            tracker.Begin(attacker, 20);
            Assert.That(tracker.TryHit(target, out var result), Is.True);
            Assert.That(result.AppliedAmount, Is.EqualTo(20));
            Assert.That(tracker.TryHit(target, out _), Is.False);
            Assert.That(target.CurrentHealth, Is.EqualTo(80));

            tracker.Begin(attacker, 20);
            Assert.That(tracker.TryHit(target, out _), Is.True);
            Assert.That(target.CurrentHealth, Is.EqualTo(60));
        }

        /// <summary>
        /// 攻撃者自身、未指定の相手、すでに倒れた相手にはDamageを適用しないことを確認する。
        /// </summary>
        [Test]
        public void HitTracker_IgnoresAttackerAndDefeatedTargets()
        {
            var attacker = new CharacterHealth(100);
            var defeated = new CharacterHealth(100, 0);
            var tracker = new CharacterHitTracker();
            tracker.Begin(attacker, 20);

            Assert.That(tracker.TryHit(attacker, out _), Is.False);
            Assert.That(attacker.CurrentHealth, Is.EqualTo(100));
            Assert.That(tracker.TryHit(null, out _), Is.False);
            Assert.That(tracker.TryHit(defeated, out _), Is.False);
            Assert.Throws<ArgumentNullException>(() => tracker.Begin(null, 20));
            Assert.Throws<ArgumentOutOfRangeException>(() => tracker.Begin(attacker, 0));
        }
    }
}
