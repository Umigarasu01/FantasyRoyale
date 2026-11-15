using System.Collections;
using System.Collections.Generic;
using FantasyRoyale.Gameplay.Characters;
using FantasyRoyale.Gameplay.Characters.Unity;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace FantasyRoyale.Playtest.Tests.PlayMode
{
    /// <summary>
    /// 本番のActor、Combat、Hurtboxだけで、攻撃が当たった相手が押し戻され、
    /// アーマーがあれば押されず攻撃も止まらないことを確かめる。
    /// </summary>
    public sealed class CharacterKnockbackPlayModeTests
    {
        private const float Knockback = 0.6f;
        private const float TargetDistance = 0.7f;

        // 前のTestが読み込んだMapのSceneが残っていても、その壁に当たらない遠くで戦わせる。
        private static readonly Vector2 Arena = new Vector2(5000f, 5000f);

        private readonly List<GameObject> created = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (var item in created)
            {
                Object.Destroy(item);
            }

            created.Clear();
        }

        /// <summary>
        /// 剣で斬られた相手は斬った向きへ押され、振りかけていた攻撃が止まることを確認する。
        /// </summary>
        [UnityTest]
        public IEnumerator Hit_PushesTargetAwayAndInterruptsItsAction()
        {
            var attacker = CreateFighter("Attacker", Vector2.zero, 0f);
            var target = CreateFighter("Target", new Vector2(TargetDistance, 0f), 0f);
            yield return new WaitForFixedUpdate();

            CharacterHitEvent? hit = null;
            attacker.Combat.HitApplied += value => hit = value;
            Assert.That(target.Combat.TryStartAction(Command(CharacterDirection8.West)), Is.True);
            Assert.That(attacker.Combat.TryStartAction(Command(CharacterDirection8.East)), Is.True);
            yield return WaitUntilKnockbackEnds(attacker, target);

            Assert.That(hit.HasValue && hit.Value.KnockedBack, Is.True);
            Assert.That(
                target.Actor.Body.position.x - Arena.x,
                Is.EqualTo(TargetDistance + Knockback).Within(0.05f),
                "斬られた相手はKnockbackの距離だけ押し戻される。");
            Assert.That(target.Actor.Body.position.y - Arena.y, Is.EqualTo(0f).Within(0.01f));
            Assert.That(target.Combat.ActionState.IsBusy, Is.False, "押された相手の攻撃は止まる。");
        }

        /// <summary>
        /// Knockback以上のアーマーを持つ相手はダメージだけを受け、押されず攻撃も続けることを確認する。
        /// </summary>
        [UnityTest]
        public IEnumerator Armor_BlocksKnockbackAndKeepsAction()
        {
            var attacker = CreateFighter("Attacker", Vector2.zero, 0f);
            var boss = CreateFighter("Boss", new Vector2(TargetDistance, 0f), float.PositiveInfinity);
            yield return new WaitForFixedUpdate();

            CharacterHitEvent? hit = null;
            attacker.Combat.HitApplied += value => hit = value;
            Assert.That(boss.Combat.TryStartAction(Command(CharacterDirection8.West)), Is.True);
            Assert.That(attacker.Combat.TryStartAction(Command(CharacterDirection8.East)), Is.True);
            yield return WaitUntilKnockbackEnds(attacker, boss);

            Assert.That(hit.HasValue, Is.True);
            Assert.That(hit.Value.Result.IsApplied, Is.True, "アーマーがあってもダメージは受ける。");
            Assert.That(hit.Value.KnockedBack, Is.False);
            Assert.That(boss.Actor.Body.position.x - Arena.x, Is.EqualTo(TargetDistance).Within(0.01f));
            Assert.That(boss.Combat.ActionState.IsBusy, Is.True, "アーマーがあれば攻撃は止まらない。");
        }

        /// <summary>
        /// 攻撃者の振りが終わり、相手の押し戻しも終わるまで待つ。
        /// </summary>
        private static IEnumerator WaitUntilKnockbackEnds(Fighter attacker, Fighter target)
        {
            for (var frame = 0; frame < 200; frame++)
            {
                yield return new WaitForFixedUpdate();
                if (!attacker.Combat.ActionState.IsBusy && !target.Actor.IsKnockedBack)
                {
                    yield break;
                }
            }

            Assert.Fail("攻撃か押し戻しが終わらなかった。");
        }

        private static CharacterActionCommand Command(CharacterDirection8 direction)
        {
            return new CharacterActionCommand(CharacterActionSlot.Primary, direction);
        }

        /// <summary>
        /// 剣を持ったCharacterを作る。相手の攻撃は長く溜めるので、押されたら途中で止まったと分かる。
        /// </summary>
        private Fighter CreateFighter(string name, Vector2 position, float armor)
        {
            var fighterObject = new GameObject(name);
            created.Add(fighterObject);
            fighterObject.transform.position = Arena + position;
            var health = new CharacterHealth(100);
            var actor = fighterObject.AddComponent<CharacterActor2D>();
            actor.Initialize(health, 5f);
            actor.SetKnockbackArmor(armor);
            fighterObject.AddComponent<CharacterHurtbox2D>().Initialize(health);
            var combat = fighterObject.AddComponent<CharacterCombat2D>();
            var windup = name == "Attacker" ? 0.05f : 1f;
            var sword = CharacterActionSpec.CreateMelee(
                $"{name}-slash",
                10,
                new CharacterActionTiming(windup, 0.05f, 0.05f),
                1f,
                120f,
                knockback: Knockback);
            combat.Initialize(new CharacterLoadout(sword, null), 0);
            return new Fighter(actor, combat);
        }

        private readonly struct Fighter
        {
            public Fighter(CharacterActor2D actor, CharacterCombat2D combat)
            {
                Actor = actor;
                Combat = combat;
            }

            public CharacterActor2D Actor { get; }
            public CharacterCombat2D Combat { get; }
        }
    }
}
