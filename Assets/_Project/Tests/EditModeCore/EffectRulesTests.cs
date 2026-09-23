using System.Collections.Generic;
using NUnit.Framework;
using Unity.Mathematics;
using Wassup.Battle.Units;
using Wassup.BattleCore;
using Wassup.BattleCore.Combat;
using Wassup.BattleCore.Effects;
using static Wassup.Tests.EditMode.Core.CoreCombatFixtures;

namespace Wassup.Tests.EditMode.Core
{
    // battle-core-rebuild unit 6a — 효과가 **판 위에서** 보이는 형태로 증언한다.
    //
    // 순수 함수 테스트(`ModifierSetTests` 등)와 나눈 이유는 `CombatRulesTests` 와 같다:
    // 여기 있는 것들은 「어느 단계에서 일어나나」가 규칙의 일부라 함수 하나로는 물을 수 없다.
    [TestFixture]
    public class EffectRulesTests
    {
        private static BattleMatch Match(MatchDefinition def)
        {
            var m = new BattleMatch(def);
            m.Begin();
            return m;
        }

        // ── 증상 단언 ⑴ — 느려진다 ──────────────────────────────────────────

        [Test]
        public void 감속을_건_적은_같은_판에서_느리게_간다()
        {
            float Walk(bool slowed)
            {
                var map = CoreMapFixtures.Open(24, 3, new int2(23, 1), new int2(0, 1));
                var m = Match(CoreMapFixtures.Definition(map, enemySpeed: 2f));
                m.Apply(Command.DebugSpawnEnemyInLane(0, 0));
                var e = m.World.Units[0];
                float from = e.Position.x;
                if (slowed)
                    e.Modifiers.Apply(ModifierKey.Of(e.Id, StatKind.MoveSpeedMul,
                                                     CombineOp.Multiplicative), 0.5f, 10f);
                for (int t = 0; t < 120; t++) m.Tick();
                return e.Position.x - from;
            }

            float normal = Walk(false);
            float slowed = Walk(true);

            Assert.Greater(normal, 3f, "2초 동안 초속 2 로 걸으면 3칸은 넘는다");
            Assert.AreEqual(normal * 0.5f, slowed, normal * 0.1f, "절반 속도면 절반 칸이다");
        }

        // ── 증상 단언 ⑵ — 장판을 나가면 불이 꺼진다 ─────────────────────────

        [Test]
        public void 출혈_중인_적이_장판을_나가면_불_피해가_멈춘다()
        {
            // 두 파이프라인이 한 슬롯을 공유하던 시절엔 장판을 나가도 **장판 요율로 계속
            // 탔다**(실측 총 ~194, 의도 50). 2축 키가 그 과피해를 없앤다.
            var m = Match(Definition(enemyHealth: 10000f));
            m.Apply(Command.DebugSpawnEnemy(0, new int2(9, 1)));
            var e = First(m, UnitKind.Enemy);

            e.Dot.Apply(DotOrigin.Stack, DotElement.Bleed, 5f, 1f, 60f);

            float beforeZone = e.Health;
            for (int t = 0; t < 300; t++)
            {
                e.Dot.Apply(DotOrigin.Zone, DotElement.Fire, 20f, 1f, 0.2f);   // 매 틱 갱신 = 장판 위
                m.Tick();
            }
            float onZone = beforeZone - e.Health;

            for (int t = 0; t < 30; t++) m.Tick();      // 장판 밖 — 0.2초 뒤 불이 꺼진다
            Assert.AreEqual(1, e.Dot.Count, "장판 슬롯만 사라진다");
            Assert.AreEqual(DotElement.Bleed, e.Dot.Slots[0].Element);

            float beforeOff = e.Health;
            for (int t = 0; t < 300; t++) m.Tick();
            float offZone = beforeOff - e.Health;

            Assert.Greater(onZone, 100f, "장판 위 5초는 출혈 25 + 장판 100 이다");
            Assert.Less(offZone, onZone * 0.5f, "장판 요율이 새면 여기서 드러난다");
            Assert.Greater(offZone, 10f, "출혈까지 같이 꺼지면 0 이 된다");
        }

        // ── 증상 단언 ⑶ — 다 막은 피격은 안 깨운다 ──────────────────────────

        [Test]
        public void 실드가_다_막은_피격은_수면을_안_깨운다()
        {
            // 관통 0 = 「맞지 않았다」. 기상·피해 숫자·킬 귀속이 전부 그 분기로 갈린다.
            var m = Match(Definition(defenderDamage: 10f, defenderCooldown: 0.2f, enemyHealth: 500f));
            m.Apply(Command.DebugSpawnDefender(0, new int2(4, 1)));
            m.Apply(Command.DebugSpawnEnemy(0, new int2(5, 1)));
            var d = First(m, UnitKind.Defender);
            var e = First(m, UnitKind.Enemy);

            m.World.GrantShield(e.Id, d.Id, 1000f, 0);
            Tick(m, 2);                                   // 부여는 한 틱 늦게 든다
            Assert.Greater(ShieldMath.Sum(e.Shield.Slots), 0f);

            m.World.RequestCc(CcRequest.Of(e.Id, CcRequestKind.Sleep, 5f, d.Id));
            Tick(m, 1);
            Assert.IsTrue(e.Cc.IsActive(CcSlotKind.Sleep));

            // ⚠ 기준은 **실드가 든 뒤**다. 부여가 한 틱 늦게 들어서(C17) 그 전에 한 대를 맞는다.
            float shieldBefore = ShieldMath.Sum(e.Shield.Slots);
            float hpBefore = e.Health;
            Tick(m, 60);

            Assert.Less(ShieldMath.Sum(e.Shield.Slots), shieldBefore, "실제로 맞고 있다");
            Assert.AreEqual(hpBefore, e.Health, 1e-3f, "실드가 든 뒤로는 체력이 한 점도 안 깎였다");
            Assert.IsTrue(e.Cc.IsActive(CcSlotKind.Sleep), "다 막힌 피격은 기상 사유가 아니다");
        }

        // ── 잠금 ─────────────────────────────────────────────────────────────

        [Test]
        public void 행동_잠금은_START_만_막고_쿨다운은_계속_돈다()
        {
            var m = Match(Definition(defenderDamage: 10f, defenderCooldown: 1f, enemyHealth: 500f));
            m.Apply(Command.DebugSpawnDefender(0, new int2(4, 1)));
            m.Apply(Command.DebugSpawnEnemy(0, new int2(5, 1)));
            var d = First(m, UnitKind.Defender);
            var e = First(m, UnitKind.Enemy);

            Tick(m, 1);
            float hpAfterFirst = e.Health;
            Assert.Less(hpAfterFirst, 500f, "첫 공격이 들어갔다");

            m.World.RequestCc(CcRequest.Of(d.Id, CcRequestKind.Stun, 3f, e.Id));
            Tick(m, 1);
            Assert.IsTrue(d.ActionLocked, "군중 제어가 행동 잠금에 합류한다");

            float cooldownBefore = d.Attack.CooldownRemaining;
            Tick(m, 60);
            Assert.AreEqual(hpAfterFirst, e.Health, 1e-3f, "잠긴 동안에는 새 공격이 없다");
            Assert.Less(d.Attack.CooldownRemaining, cooldownBefore - 0.5f,
                "쿨다운은 잠긴 동안에도 돈다 — 풀리는 즉시 때리는 근거다");
        }

        [Test]
        public void 넉백은_잠금이_아니라_외력이다()
        {
            var map = CoreMapFixtures.Open(24, 3, new int2(23, 1), new int2(0, 1));
            var m = Match(CoreMapFixtures.Definition(map, enemySpeed: 2f));
            m.Apply(Command.DebugSpawnEnemyInLane(0, 0));
            var e = m.World.Units[0];
            Tick(m, 30);                                   // 진행 방향을 만든다

            float before = e.Position.x;
            m.World.RequestCc(CcRequest.Push(e.Id, new float3(-6f, 0f, 0f), 0.5f, SimEntityId.None));
            Tick(m, 1);
            Assert.IsFalse(e.ActionLocked, "밀리는 중에도 때린다");
            Assert.IsTrue(e.Cc.Any, "그래도 「군중 제어에 걸린 적」이긴 하다");

            Tick(m, 30);
            Assert.Less(e.Position.x - before, 1f, "뒤로 미는 외력이 전진을 상쇄한다");
        }

        [Test]
        public void 내_피해가_내가_건_잠을_같은_틱에_깨우지_않는다()
        {
            // C9 — 옛 전투는 시스템 순서의 **우연**이 이것을 보장했다. 한 틱 안에서 도는
            // 새 코어는 가드 두 겹(CC 부여가 피해 뒤 · 같은 틱 부여는 기상 제외)으로 막는다.
            var def = Definition(defenderDamage: 10f, defenderCooldown: 1f, enemyHealth: 500f);
            def.Units[0].Attack.SleepOnHitSec = 5f;
            var m = Match(def);
            m.Apply(Command.DebugSpawnDefender(0, new int2(4, 1)));
            m.Apply(Command.DebugSpawnEnemy(0, new int2(5, 1)));
            var e = First(m, UnitKind.Enemy);

            Tick(m, 1);
            Assert.Less(e.Health, 500f, "때렸다");
            Assert.IsTrue(e.Cc.IsActive(CcSlotKind.Sleep), "그리고 재웠다 — 자기 피해로 안 깬다");
        }

        [Test]
        public void 피격_기상은_수면만_풀고_기절은_안_깬다()
        {
            var m = Match(Definition(defenderDamage: 10f, defenderCooldown: 0.2f, enemyHealth: 500f));
            m.Apply(Command.DebugSpawnDefender(0, new int2(4, 1)));
            m.Apply(Command.DebugSpawnEnemy(0, new int2(5, 1)));
            var d = First(m, UnitKind.Defender);
            var e = First(m, UnitKind.Enemy);

            m.World.RequestCc(CcRequest.Of(e.Id, CcRequestKind.Sleep, 10f, d.Id));
            m.World.RequestCc(CcRequest.Of(e.Id, CcRequestKind.Stun, 10f, d.Id));
            Tick(m, 1);
            Assert.IsTrue(e.Cc.IsActive(CcSlotKind.Sleep));
            Assert.IsTrue(e.Cc.IsActive(CcSlotKind.Stun));

            Tick(m, 30);                                   // 방어유닛이 때린다
            Assert.IsFalse(e.Cc.IsActive(CcSlotKind.Sleep), "맞으면 깬다");
            Assert.IsTrue(e.Cc.IsActive(CcSlotKind.Stun), "기절은 안 깬다");
        }

        // ── 공격 산출물 ──────────────────────────────────────────────────────

        [Test]
        public void 공격_산출물이_스탯을_걸고_출처는_때린_자다()
        {
            var def = Definition(defenderDamage: 0f, defenderCooldown: 1f, enemyHealth: 500f);
            def.Units[0].Attack.Outputs = new[]
            {
                new AttackOutputDef
                {
                    Kind = AttackOutputKind.ApplyStat,
                    Stat = (int)StatKind.MoveSpeedMul,
                    Op = (int)CombineOp.Multiplicative,
                    Magnitude = 0.5f,
                    Duration = 3f,
                },
            };
            var m = Match(def);
            var applied = new List<CoreEvent>();
            m.Bus.Subscribe(CoreEventKind.ModifierApplied, 0, e => applied.Add(e));

            m.Apply(Command.DebugSpawnDefender(0, new int2(4, 1)));
            m.Apply(Command.DebugSpawnEnemy(0, new int2(5, 1)));
            var d = First(m, UnitKind.Defender);
            var e2 = First(m, UnitKind.Enemy);

            Tick(m, 2);
            Assert.AreEqual(1, e2.Modifiers.Count);
            Assert.AreEqual(d.Id, e2.Modifiers.Slots[0].Key.Source, "출처는 때린 자다");
            Assert.AreEqual(ModifierOrigin.OnHit, e2.Modifiers.Slots[0].Origin);
            Assert.AreEqual(0.5f, e2.Modifiers.Effective.MoveSpeedMul, 1e-4f);
            Assert.AreEqual(1, applied.Count, "갱신은 사건이 아니다 — 새로 걸린 것만 신호한다");
        }

        [Test]
        public void 거점은_공격_산출물의_스탯도_안_받는다()
        {
            // F3 — 옛 전투는 이 규칙이 진입 가드 셋에 흩어져 있어 새 경로마다 구멍이 열렸다.
            var s = new Unit { Kind = UnitKind.Structure };
            Assert.IsFalse(EffectEligibility.AcceptsModifier(s));

            var set = new ModifierSet();
            Assert.AreEqual(0, set.Count);   // 가드는 호출부(`ApplyStatOutput`)에 있다
        }

        // ── 스택 ─────────────────────────────────────────────────────────────

        [Test]
        public void 스택이_쌓이면_임계가_한_번_터지고_소비형은_기준을_다시_맞춘다()
        {
            var def = Definition(defenderDamage: 0f, defenderCooldown: 0.2f, enemyHealth: 5000f);
            def.Units[0].Attack.Outputs = new[]
            {
                new AttackOutputDef
                {
                    Kind = AttackOutputKind.ApplyStack,
                    StackKind = (int)StackKind.Fire,
                    Magnitude = 1f,
                },
            };
            def.StackRules = new[]
            {
                new StackRuleDef
                {
                    Id = "fixture_fire",
                    Kind = (int)StackKind.Fire,
                    MaxStack = 5,
                    PerAppDuration = 30f,
                    Thresholds = new[]
                    {
                        new StackThresholdDef
                        {
                            AtStack = 5,
                            Mode = StackThresholdMode.Consume,
                            Derived = StackDerivedKind.ApplyDot,
                            Magnitude = 10f,
                            Duration = 2f,
                            TickInterval = 1f,
                        },
                    },
                },
            };

            var m = Match(def);
            var fired = new List<CoreEvent>();
            m.Bus.Subscribe(CoreEventKind.StackThreshold, 0, e => fired.Add(e));

            m.Apply(Command.DebugSpawnDefender(0, new int2(4, 1)));
            m.Apply(Command.DebugSpawnEnemy(0, new int2(5, 1)));
            var e2 = First(m, UnitKind.Enemy);

            Tick(m, 60);
            Assert.AreEqual(1, fired.Count, "임계는 올라가는 길에 한 번만 난다");
            Assert.AreEqual((int)StackKind.Fire, fired[0].Arg);
            Assert.AreEqual(5f, fired[0].Amount, 1e-4f);
            Assert.AreEqual(1, e2.Dot.Count, "파생 지속 피해가 붙었다");
            Assert.AreEqual(DotElement.Fire, e2.Dot.Slots[0].Element);
            Assert.AreEqual(DotOrigin.Stack, e2.Dot.Slots[0].Origin);
            Assert.LessOrEqual(e2.Stacks.CountOf(StackKind.Fire), 5,
                "소비형이 기준을 못 맞추면 여기서 상한을 넘거나 임계가 재발화한다");
        }

        [Test]
        public void 스택_파생_감속은_배치_감속과_다른_칸을_쓴다()
        {
            // F26 의 판 위 버전. 출처가 둘 다 피해자 자신이라 칸이 유일한 구분이다.
            var def = Definition(enemyHealth: 500f);
            def.StackRules = new[]
            {
                new StackRuleDef
                {
                    Id = "fixture_ice",
                    Kind = (int)StackKind.Ice,
                    MaxStack = 5,
                    PerAppDuration = 30f,
                    Thresholds = new[]
                    {
                        new StackThresholdDef
                        {
                            AtStack = 1,
                            Mode = StackThresholdMode.Edge,
                            Derived = StackDerivedKind.ApplyStat,
                            Magnitude = 0.9f,
                            Duration = 10f,
                            Stat = (int)StatKind.MoveSpeedMul,
                            Op = (int)CombineOp.Multiplicative,
                        },
                    },
                },
            };

            var m = Match(def);
            m.Apply(Command.DebugSpawnEnemy(0, new int2(5, 1)));
            var e = First(m, UnitKind.Enemy);

            // 배치 스킬 감속 — 출처는 대상 자신, 일반 칸.
            e.Modifiers.Apply(new ModifierKey(e.Id, StatKind.MoveSpeedMul,
                                              CombineOp.Multiplicative, SlotTag.Default), 0.4f, 10f);
            // 얼음 스택 1 — 임계가 같은 스탯·같은 연산자·같은 출처로 파생 감속을 건다.
            e.Stacks.Add(e.Id, StackKind.Ice, 0, 1, 5, 30f);
            Tick(m, 2);

            Assert.AreEqual(2, e.Modifiers.Count, "칸을 접으면 여기서 1 이 된다");
            Assert.AreEqual(0.36f, e.Modifiers.Effective.MoveSpeedMul, 1e-3f,
                "강한 배치 감속(0.4)이 약한 스택 감속(0.9)으로 깎이면 0.9 가 된다");
        }
    }
}
