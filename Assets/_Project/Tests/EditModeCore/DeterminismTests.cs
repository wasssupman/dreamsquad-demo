using NUnit.Framework;
using Unity.Mathematics;
using Wassup.BattleCore;

namespace Wassup.Tests.EditMode.Core
{
    // battle-core-rebuild unit 1 완료 기준 ② — 결정론.
    //
    // 「같은 정의표 + 같은 스케줄 = 같은 트레이스」. 이 단언이 깨지는 방식은 대개
    // 시간(`Time`·`DateTime`)·난수(`System.Random`)·순회 순서(사전·해시셋) 셋이고,
    // 셋 다 코어에서 금지돼 있다. 그 금지가 실제로 지켜지는지를 여기서 실행으로 묻는다.
    [TestFixture]
    public class DeterminismTests
    {
        private static CommandSchedule Schedule() => new CommandSchedule()
            .Add(5, Command.DebugSpawnEnemy(0, new int2(0, 0)))
            .Add(5, Command.PlaceDefender(0, new int2(1, 1)))
            .Add(30, Command.DebugSpawnEnemy(0, new int2(2, 2)))
            .Add(60, Command.DebugDestroy(new SimEntityId(1)))
            .Add(90, Command.Submit());   // 해금 전이라 거절 — 거절도 결정론이어야 한다

        [Test]
        public void 같은_입력을_두_번_돌리면_트레이스가_같다()
        {
            var a = CoreHarness.Run(CoreGoldenCorpus.Fixture(1234), Schedule(), 600, "determinism");
            var b = CoreHarness.Run(CoreGoldenCorpus.Fixture(1234), Schedule(), 600, "determinism");

            Assert.IsNull(a.Trace.DiffAgainst(b.Trace), "두 실행의 트레이스가 갈렸다");
            Assert.AreEqual(a.Trace.Serialize(), b.Trace.Serialize(), "직렬화 바이트까지 같아야 한다");
            Assert.AreEqual(a.Trace.finalStateHash, b.Trace.finalStateHash);
        }

        // unit 2 — 군집 결정론. 분리 누적이 `SimEntityId` 오름차순으로 닫혔는지를 묻는다(M27).
        // 적 20기가 같은 문에서 나와 서로 밀어내는 구간이 그 축이 드러나는 자리다.
        private static CommandSchedule CrowdSchedule()
        {
            var s = new CommandSchedule();
            for (int i = 0; i < 20; i++) s.Add(2 + i, Command.DebugSpawnEnemyInLane(0, i % 2));
            return s;
        }

        [Test]
        public void 적_20기_군집도_두_실행이_같다()
        {
            var a = CoreHarness.Run(CoreGoldenCorpus.MarchFixture(77), CrowdSchedule(), 1800, "crowd");
            var b = CoreHarness.Run(CoreGoldenCorpus.MarchFixture(77), CrowdSchedule(), 1800, "crowd");

            Assert.IsNull(a.Trace.DiffAgainst(b.Trace), "군집에서 트레이스가 갈렸다");
            Assert.AreEqual(a.Trace.finalStateHash, b.Trace.finalStateHash,
                "이벤트는 같은데 상태가 갈렸다면 분리 누적 순서를 의심한다");
        }

        // unit 3 — 전투가 도는 판의 결정론. 30기가 죽고 320여 건의 사건이 나는 동안
        // **두 실행이 바이트로 같아야** 한다(피해 인박스 순회·킬 귀속·요청 줄이 전부 이 축이다).
        [Test]
        public void 킬_레이스는_두_실행이_같다()
        {
            var sc = CoreGoldenCorpus.ByName("kill_race_basic");
            var a = CoreHarness.Run(sc.BuildDefinition(), sc.BuildSchedule(), sc.Ticks, sc.Name);
            var b = CoreHarness.Run(sc.BuildDefinition(), sc.BuildSchedule(), sc.Ticks, sc.Name);

            Assert.IsNull(a.Trace.DiffAgainst(b.Trace));
            Assert.AreEqual(a.Trace.Serialize(), b.Trace.Serialize());
            Assert.AreEqual(a.Trace.finalStateHash, b.Trace.finalStateHash);
            Assert.Greater(a.Trace.finalKills, 0, "처치가 0 이면 이 골든은 아무것도 증언하지 않는다");
        }

        // 조각 A 의 완료 기준 — 헤드리스로 3분 판이 **완주**하고, 스폰한 적이 전부 사라진다.
        [Test]
        public void 킬_레이스가_완주한다()
        {
            var sc = CoreGoldenCorpus.ByName("kill_race_basic");
            var run = CoreHarness.Run(sc.BuildDefinition(), sc.BuildSchedule(), sc.Ticks, sc.Name);

            int spawnedEnemies = 0, destroyed = 0, slain = 0, ended = 0;
            for (int i = 0; i < run.Trace.events.Count; i++)
            {
                var e = run.Trace.events[i];
                if (e.channel == CoreTraceChannel.UnitSpawned && e.i == (int)UnitKind.Enemy) spawnedEnemies++;
                if (e.channel == CoreTraceChannel.UnitDestroyed) destroyed++;
                if (e.channel == CoreTraceChannel.UnitSlain) slain++;
                if (e.channel == CoreTraceChannel.MatchEnded) ended++;
            }

            Assert.AreEqual(1, ended, "판이 끝난다");
            Assert.AreEqual(30, spawnedEnemies);
            Assert.AreEqual(spawnedEnemies, destroyed, "스폰한 적이 전부 사라진다");
            Assert.AreEqual(spawnedEnemies, slain, "전부 **피해로** 죽었다");
        }

        // unit 6a — **쿨다운·스택이 여럿 걸린 판**의 결정론. 키 스냅샷 없이 도는 순회가
        // 셋(모디파이어 슬롯·스택 슬롯·지속 피해 슬롯) 늘었고, 전부 리스트 삽입 순서에
        // 기대고 있다. 그 주장을 주석이 아니라 실행으로 묻는다.
        private static MatchDefinition EffectFixture(int seed)
        {
            var def = CoreGoldenCorpus.KillRaceFixture(seed);

            // 방어유닛 — 피해 + 감속 + 불 스택. 한 공격이 세 종류를 한꺼번에 낸다.
            def.Units[0].Attack.Outputs = new[]
            {
                new AttackOutputDef { Kind = AttackOutputKind.Damage, Magnitude = 12f },
                new AttackOutputDef
                {
                    Kind = AttackOutputKind.ApplyStat,
                    Stat = (int)Wassup.BattleCore.Effects.StatKind.MoveSpeedMul,
                    Op = (int)Wassup.BattleCore.Effects.CombineOp.Multiplicative,
                    Magnitude = 0.7f,
                    Duration = 2f,
                },
                new AttackOutputDef
                {
                    Kind = AttackOutputKind.ApplyStack,
                    StackKind = (int)Wassup.BattleCore.Effects.StackKind.Fire,
                    Magnitude = 1f,
                },
            };
            // 적 — 맞으면 방어유닛의 공속을 깎는다(양쪽에 슬롯이 생긴다).
            def.Enemies[0].Attack.Outputs = new[]
            {
                new AttackOutputDef { Kind = AttackOutputKind.Damage, Magnitude = 5f },
                new AttackOutputDef
                {
                    Kind = AttackOutputKind.ApplyStat,
                    Stat = (int)Wassup.BattleCore.Effects.StatKind.AttackSpeedMul,
                    Op = (int)Wassup.BattleCore.Effects.CombineOp.Multiplicative,
                    Magnitude = 0.9f,
                    Duration = 1.5f,
                },
            };
            def.StackRules = new[]
            {
                new StackRuleDef
                {
                    Id = "determinism_fire",
                    Kind = (int)Wassup.BattleCore.Effects.StackKind.Fire,
                    MaxStack = 5,
                    PerAppDuration = 3f,
                    Thresholds = new[]
                    {
                        // ⚠ 임계를 3 으로 둔다 — 방어유닛마다 **자기 슬롯**이라(출처가 키다)
                        // 5 를 쓰면 적이 먼저 죽어 임계가 한 번도 안 터지고, 그러면 이
                        // 테스트가 파생 경로에 대해 아무것도 증언하지 않는다.
                        new StackThresholdDef
                        {
                            AtStack = 3,
                            Mode = StackThresholdMode.Consume,
                            Derived = StackDerivedKind.ApplyDot,
                            Magnitude = 10f,
                            Duration = 4f,
                            TickInterval = 1f,
                        },
                    },
                },
            };
            def.ConfigHash = def.ComputeConfigHash();
            return def;
        }

        [Test]
        public void 효과가_잔뜩_걸린_판도_두_실행이_같다()
        {
            var sc = CoreGoldenCorpus.ByName("kill_race_basic");
            var a = CoreHarness.Run(EffectFixture(3001), sc.BuildSchedule(), sc.Ticks, "effects");
            var b = CoreHarness.Run(EffectFixture(3001), sc.BuildSchedule(), sc.Ticks, "effects");

            Assert.IsNull(a.Trace.DiffAgainst(b.Trace), "효과가 걸린 판에서 트레이스가 갈렸다");
            Assert.AreEqual(a.Trace.Serialize(), b.Trace.Serialize());
            Assert.AreEqual(a.Trace.finalStateHash, b.Trace.finalStateHash,
                "이벤트는 같은데 상태가 갈렸다면 슬롯 순회 순서를 의심한다");

            int applied = 0, stacks = 0, thresholds = 0, cc = 0;
            for (int i = 0; i < a.Trace.events.Count; i++)
            {
                switch (a.Trace.events[i].channel)
                {
                    case CoreTraceChannel.ModifierApplied: applied++; break;
                    case CoreTraceChannel.StackChanged: stacks++; break;
                    case CoreTraceChannel.StackThreshold: thresholds++; break;
                    case CoreTraceChannel.CcApplied: cc++; break;
                }
            }
            Assert.Greater(applied, 0, "모디파이어가 한 번도 안 걸렸으면 이 테스트는 아무것도 증언하지 않는다");
            Assert.Greater(stacks, 0);
            Assert.Greater(thresholds, 0, "임계가 안 터지면 파생 경로가 안 돌았다");
            Assert.AreEqual(0, cc, "이 판에는 군중 제어 저작이 없다");
        }

        [Test]
        public void 거절_receipt_도_두_실행이_같다()
        {
            var a = CoreHarness.Run(CoreGoldenCorpus.Fixture(1234), Schedule(), 600, "determinism");
            var b = CoreHarness.Run(CoreGoldenCorpus.Fixture(1234), Schedule(), 600, "determinism");

            Assert.AreEqual(a.Receipts.Count, b.Receipts.Count);
            for (int i = 0; i < a.Receipts.Count; i++)
            {
                Assert.AreEqual(a.Receipts[i].Accepted, b.Receipts[i].Accepted, $"receipt #{i}");
                Assert.AreEqual(a.Receipts[i].Reason, b.Receipts[i].Reason, $"receipt #{i}");
            }
        }

        [Test]
        public void 직렬화_왕복이_바이트로_같다()
        {
            var run = CoreHarness.Run(CoreGoldenCorpus.Fixture(1234), Schedule(), 600, "determinism");
            string text = run.Trace.Serialize();
            string again = CoreTrace.Deserialize(text).Serialize();

            Assert.AreEqual(text, again, "왕복을 통과하지 못하는 기록은 골든이 될 자격이 없다");
        }

        [Test]
        public void 옛_계열_트레이스는_코어_리더가_거절한다()
        {
            // 포맷은 같아도 채널 어휘가 다르다. 구분자(`channels=core`)가 없으면 골든 하나가 조용히
            // 엉뚱한 채널 이름으로 읽힌다. 옛 계열(`LTV0`)의 머리만 흉내 낸다 — 그 직렬화기는 지웠다.
            const string legacy = "LTV0\nscenario=old\nconfigHash=deadbeefdeadbeef\nmatchSeed=0\nstepDt=0.016666668\ntickCount=0\nevents=0\n"
                                + "finalKills=0\nfinalScore=0\nfinalLeaks=0\nfinalStateHash=0000000000000000\n";
            Assert.Throws<System.FormatException>(() => CoreTrace.Deserialize(legacy));
        }

        [Test]
        public void 버스는_모르는_종류를_바로_거절한다()
        {
            var bus = new EventBus();

            // 기본값(`None`)으로 남은 이벤트가 새어 들어오면 구독자가 0명이라 조용히
            // 사라진다 — 「이벤트가 안 온다」를 며칠 쫓게 되는 종류의 실패다.
            Assert.Throws<System.ArgumentOutOfRangeException>(
                () => bus.Subscribe(CoreEventKind.None, 0, _ => { }));
            Assert.Throws<System.ArgumentOutOfRangeException>(
                () => bus.Publish(default));
            Assert.Throws<System.ArgumentOutOfRangeException>(
                () => bus.Subscribe(CoreEventKind._Count, 0, _ => { }));
        }

        [Test]
        public void 시드가_다르면_난수_계열이_갈린다()
        {
            var a = new RngStreams(1234);
            var b = new RngStreams(4321);

            Assert.AreNotEqual(a.Wave.NextUInt(), b.Wave.NextUInt());

            // 같은 시드의 계열끼리는 상관이 없어야 한다(salt 분리의 목적).
            var c = new RngStreams(1234);
            Assert.AreNotEqual(c.Map.NextUInt(), c.Wave.NextUInt());
        }

        [Test]
        public void 같은_시드는_같은_난수열을_낸다()
        {
            var a = new RngStreams(777);
            var b = new RngStreams(777);
            for (int i = 0; i < 16; i++) Assert.AreEqual(a.Visual.NextUInt(), b.Visual.NextUInt());
        }
    }
}
