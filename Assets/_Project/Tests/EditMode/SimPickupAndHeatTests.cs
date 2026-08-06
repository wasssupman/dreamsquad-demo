using System.Linq;
using NUnit.Framework;
using Wassup.Sim;
using Wassup.Sim.Effects;
using Wassup.Sim.Movement;
using Wassup.Sim.Units;

namespace Wassup.Tests.EditMode
{
    /// <summary>
    /// battle-sim-extraction unit 18-J/2 — 캡처 #21(온천 열기) · #22(픽업 스폰) · #23(픽업 소비).
    ///
    /// 계약 넷: **① 열기는 사망 원인이 될 수 없다**(HP 1 바닥) **② 오버힐은 잘린다**
    /// **③ 픽업 rng 는 상태 해시에 실린다**(draw 한 번만 어긋나도 이후 스폰이 전부 갈린다)
    /// **④ 소비 락** — 라스트런 중인 유닛은 밟아도 안 먹는다(crash 무한 회피 차단).
    /// </summary>
    public class SimPickupAndHeatTests
    {
        private SimWorld _world;
        private SimChannels _channels;

        [SetUp]
        public void SetUp()
        {
            _world = new SimWorld(new SimConfig(1u, 1u));
            _channels = new SimChannels();
            _world.SetDeltaTime(0.5f);
        }

        private void Onsen(float interval = 1f, byte flip = 2, float heal = 0.1f,
                           float loss = 0.2f, byte maxStack = 5)
        {
            var e = _world.Create();
            _world.Set(e, new OnsenGimmickConfig
            {
                heatInterval = interval, flipThreshold = flip,
                healPercent = heal, lossPercent = loss, heatMaxStack = maxStack,
            });
        }

        private void RedBull(float interval = 1f, float lifetime = 5f, int maxActive = 2,
                             float speedMul = 1.5f, float duration = 4f)
        {
            var e = _world.Create();
            _world.Set(e, new RedBullGimmickConfig
            {
                redbullSpawnInterval = interval, redbullLifetime = lifetime,
                redbullMaxActive = maxActive, lastRunAttackSpeedMul = speedMul,
                lastRunDuration = duration, lastRunDamageFraction = 0.3f,
            });
        }

        private SimEntityId SpawnState(SimInt2[] cells, uint seed = 7u)
        {
            var e = _world.Create();
            _world.Set(e, new PickupSpawnState
            {
                candidateCells = cells, elapsed = 0f, rng = new SimRandom(seed),
            });
            return e;
        }

        private void FlowField(int w = 8, int h = 8)
        {
            var e = _world.Create();
            _world.Set(e, new FlowFieldSingleton
            {
                flow = new SimVec2[w * h], dist = new int[w * h],
                gridSize = new SimInt2(w, h), tileSize = 1f, origin = default,
            });
        }

        private SimEntityId Unit(bool defender, float hp = 100f, float max = 100f)
        {
            var e = _world.Create();
            _world.Set(e, new Health { value = hp, max = max });
            if (defender) _world.Set(e, new DefenderUnitTag());
            else _world.Set(e, new AttackUnitTag());
            _world.AddBuffer<IncomingDamage>(e);
            return e;
        }

        private int PickupCount()
        {
            int n = 0;
            foreach (var _ in _world.With<Pickup>()) n++;
            return n;
        }

        // ── #21 온천 열기 ─────────────────────────────────────────────────────

        [Test]
        public void Heat_AttachesLazily_AndHealsBelowTheFlip()
        {
            Onsen(interval: 0.5f, flip: 2, heal: 0.1f);
            var u = Unit(defender: true, hp: 50f, max: 100f);
            var sut = new HeatAccrualSystem();

            sut.Run(_world); // dt 0.5 → 정확히 1주기

            Assert.AreEqual(1, _world.Get<HeatAccrual>(u).stacks);
            var heals = _world.GetBuffer<IncomingHeal>(u);
            Assert.AreEqual(1, heals.Count, "부착 프레임에 이미 한 번 tick 한다(2-pass 순서)");
            Assert.AreEqual(10f, heals[0].amount, 1e-4f, "maxHp × healPercent");
        }

        [Test]
        public void Heat_ClipsOverheal()
        {
            // ⚠ 만피 유닛이 매 주기 회복 VFX 를 뿜지 않게 한다.
            Onsen(interval: 0.5f, flip: 2, heal: 0.1f);
            var u = Unit(defender: true, hp: 95f, max: 100f);
            new HeatAccrualSystem().Run(_world);

            Assert.AreEqual(5f, _world.GetBuffer<IncomingHeal>(u)[0].amount, 1e-4f, "headroom 까지만");
        }

        [Test]
        public void Heat_FlipsToLoss_AboveTheThreshold()
        {
            Onsen(interval: 0.5f, flip: 1, loss: 0.2f);
            var u = Unit(defender: true, hp: 100f, max: 100f);
            var sut = new HeatAccrualSystem();

            sut.Run(_world); // stacks 1 → 회복
            sut.Run(_world); // stacks 2 → 손실

            var dmg = _world.GetBuffer<IncomingDamage>(u);
            Assert.AreEqual(1, dmg.Count);
            Assert.AreEqual(20f, dmg[0].amount, 1e-4f);
            Assert.AreEqual(SimEntityId.Null, dmg[0].source, "환경 피해는 킬을 귀속시키지 않는다");
        }

        [Test]
        public void Heat_NeverKills_TheHpFloorIsOne()
        {
            // ⚠ **열기는 사망 원인이 될 수 없다.**
            Onsen(interval: 0.5f, flip: 0, loss: 0.9f);
            var u = Unit(defender: true, hp: 5f, max: 100f);

            new HeatAccrualSystem().Run(_world);

            Assert.AreEqual(4f, _world.GetBuffer<IncomingDamage>(u)[0].amount, 1e-4f, "HP 1 까지만");
        }

        [Test]
        public void Heat_LargeDt_ClampsAcrossEveryPeriodInTheFrame()
        {
            // ⚠ 로컬 투영값을 추적하지 않으면 매 주기 같은 HP 를 읽어 바닥이 무너진다.
            Onsen(interval: 0.1f, flip: 0, loss: 0.5f);
            var u = Unit(defender: true, hp: 10f, max: 100f);
            _world.SetDeltaTime(0.35f); // 3주기

            new HeatAccrualSystem().Run(_world);

            var dmg = _world.GetBuffer<IncomingDamage>(u);
            float total = dmg.Sum(d => d.amount);
            Assert.AreEqual(9f, total, 1e-3f, "합계가 HP-1 을 넘지 않는다");
        }

        [Test]
        public void Heat_StacksAreCappedAndEnemiesGetAHealInbox()
        {
            Onsen(interval: 0.1f, flip: 9, maxStack: 2);
            var enemy = Unit(defender: false);
            _world.SetDeltaTime(0.5f); // 5주기

            new HeatAccrualSystem().Run(_world);

            Assert.AreEqual(2, _world.Get<HeatAccrual>(enemy).stacks, "상한에서 멈춘다");
            Assert.IsNotNull(_world.GetBuffer<IncomingHeal>(enemy), "적에게도 회복 인박스를 연다");
        }

        [Test]
        public void Heat_IsInert_WithoutTheConfig_OrWithAZeroInterval()
        {
            var u = Unit(defender: true);
            new HeatAccrualSystem().Run(_world);
            Assert.IsFalse(_world.Has<HeatAccrual>(u), "기믹 비활성");

            Onsen(interval: 0f);
            new HeatAccrualSystem().Run(_world);
            Assert.IsFalse(_world.Has<HeatAccrual>(u), "0 이면 while 이 끝나지 않는다 — 저작 오류 방어");
        }

        [Test]
        public void Heat_SkipsDeadAndPendingUnits()
        {
            Onsen(interval: 0.1f);
            var dead = Unit(defender: true);
            _world.Set(dead, new DeadTag());
            var pending = Unit(defender: true);
            _world.Set(pending, new PendingDeployment());

            new HeatAccrualSystem().Run(_world);

            Assert.IsFalse(_world.Has<HeatAccrual>(dead));
            Assert.IsFalse(_world.Has<HeatAccrual>(pending));
        }

        // ── #22 픽업 스폰 ─────────────────────────────────────────────────────

        [Test]
        public void Spawn_PlacesOnACandidateCell_OnCadence()
        {
            RedBull(interval: 1f, maxActive: 5);
            SpawnState(new[] { new SimInt2(1, 1), new SimInt2(2, 2) });
            _world.SetDeltaTime(1f);

            new PickupSpawnSystem().Run(_world);

            Assert.AreEqual(1, PickupCount());
            foreach (var e in _world.With<Pickup>())
            {
                var p = _world.Get<Pickup>(e);
                Assert.That(p.cell, Is.EqualTo(new SimInt2(1, 1)).Or.EqualTo(new SimInt2(2, 2)));
                Assert.AreEqual(PickupKind.Redbull, p.kind);
                Assert.AreEqual(5f, p.remainingLife, 1e-4f);
            }
        }

        [Test]
        public void Spawn_RngAdvances_AndIsDeterministic()
        {
            // ⚠ rng 가 상태 해시에 실린다 — 같은 시드는 같은 셀 수열을 낸다.
            SimInt2 First(uint seed)
            {
                _world = new SimWorld(new SimConfig(1u, 1u));
                _world.SetDeltaTime(1f);
                RedBull(interval: 1f, maxActive: 5);
                SpawnState(Enumerable.Range(0, 8).Select(i => new SimInt2(i, 0)).ToArray(), seed);
                new PickupSpawnSystem().Run(_world);
                foreach (var e in _world.With<Pickup>()) return _world.Get<Pickup>(e).cell;
                return default;
            }

            Assert.AreEqual(First(7u), First(7u), "같은 시드는 같은 셀");
            var state = SimSingleton.FindEntity<PickupSpawnState>(_world);
            Assert.AreNotEqual(7u, _world.Get<PickupSpawnState>(state).rng.state, "draw 로 전진한다");
        }

        [Test]
        public void Spawn_RespectsMaxActive_AndClampsTheDebt()
        {
            // ⚠ debt clamp 가 없으면 상한이 풀리는 순간 밀린 주기가 한꺼번에 터진다.
            RedBull(interval: 1f, maxActive: 1, lifetime: 99f);
            var state = SpawnState(new[] { new SimInt2(1, 1), new SimInt2(2, 2), new SimInt2(3, 3) });
            _world.SetDeltaTime(5f);
            var sut = new PickupSpawnSystem();

            sut.Run(_world);

            Assert.AreEqual(1, PickupCount(), "상한에서 멈춘다");
            Assert.AreEqual(1f, _world.Get<PickupSpawnState>(state).elapsed, 1e-4f,
                "debt 는 interval 로 clamp — 슬롯이 비면 다음 프레임에 정확히 1개");
        }

        [Test]
        public void Spawn_ExpiresUnconsumedPickups()
        {
            RedBull(interval: 99f, lifetime: 1f, maxActive: 5);
            SpawnState(new[] { new SimInt2(1, 1) });
            var p = _world.Create();
            _world.Set(p, new Pickup { cell = new SimInt2(4, 4), remainingLife = 0.4f });
            _world.SetDeltaTime(0.5f);

            new PickupSpawnSystem().Run(_world);

            Assert.IsFalse(_world.Exists(p), "수명이 다한 픽업은 사라진다");
        }

        [Test]
        public void Spawn_IsInert_WithoutConfigOrState()
        {
            SpawnState(new[] { new SimInt2(1, 1) });
            _world.SetDeltaTime(5f);
            new PickupSpawnSystem().Run(_world);
            Assert.AreEqual(0, PickupCount(), "기믹 비활성");

            _world = new SimWorld(new SimConfig(1u, 1u));
            _world.SetDeltaTime(5f);
            RedBull();
            new PickupSpawnSystem().Run(_world);
            Assert.AreEqual(0, PickupCount(), "맵 미빌드(스폰 상태 없음)");
        }

        // ── #23 픽업 소비 ─────────────────────────────────────────────────────

        [Test]
        public void Consume_GrantsLastRun_AndRemovesThePickup()
        {
            RedBull(speedMul: 1.5f, duration: 4f);
            FlowField();
            var pickup = _world.Create();
            _world.Set(pickup, new Pickup { cell = new SimInt2(2, 3), remainingLife = 9f });
            var defender = Unit(defender: true);
            _world.Set(defender, new DefenderTile { cell = new SimInt2(2, 3) });

            new PickupConsumeSystem(_channels).Run(_world);

            Assert.IsFalse(_world.Exists(pickup));
            Assert.AreEqual(4f, _world.Get<LastRun>(defender).remaining, 1e-4f);
            var mods = _channels.StatApply.Drain();
            Assert.AreEqual(1, mods.Count);
            Assert.AreEqual(StatKind.AttackSpeedMul, mods[0].stat);
            Assert.AreEqual(1.5f, mods[0].magnitude, 1e-4f);
            Assert.AreEqual(ModifierOrigin.Gimmick, mods[0].origin, "시즌 기믹 출처 태그");
        }

        [Test]
        public void Consume_IsLockedWhileLastRunIsActive()
        {
            // ⚠ 없으면 재소비로 타이머를 리셋해 crash 를 무한히 회피할 수 있다.
            RedBull();
            FlowField();
            var pickup = _world.Create();
            _world.Set(pickup, new Pickup { cell = new SimInt2(2, 3), remainingLife = 9f });
            var defender = Unit(defender: true);
            _world.Set(defender, new DefenderTile { cell = new SimInt2(2, 3) });
            _world.Set(defender, new LastRun { remaining = 1f });

            new PickupConsumeSystem(_channels).Run(_world);

            Assert.IsTrue(_world.Exists(pickup), "밟아도 픽업은 보드에 남는다");
            Assert.AreEqual(1f, _world.Get<LastRun>(defender).remaining, 1e-4f, "타이머가 리셋되지 않는다");
            Assert.AreEqual(0, _channels.StatApply.Count);
        }

        [Test]
        public void Consume_EnemyUsesItsWorldPosition()
        {
            RedBull();
            FlowField();
            var pickup = _world.Create();
            _world.Set(pickup, new Pickup { cell = new SimInt2(3, 0), remainingLife = 9f });
            var enemy = Unit(defender: false);
            _world.Set(enemy, SimTransform.FromPosition(new SimVec3(3f, 0f, 0f)));

            new PickupConsumeSystem(_channels).Run(_world);

            Assert.IsFalse(_world.Exists(pickup));
            Assert.IsTrue(_world.Has<LastRun>(enemy));
        }

        [Test]
        public void Consume_DefenderWinsTheSameCell()
        {
            // ⚠ 방어유닛이 먼저 순회된다(구 sim 순서).
            RedBull();
            FlowField();
            var pickup = _world.Create();
            _world.Set(pickup, new Pickup { cell = new SimInt2(3, 0), remainingLife = 9f });
            var enemy = Unit(defender: false);
            _world.Set(enemy, SimTransform.FromPosition(new SimVec3(3f, 0f, 0f)));
            var defender = Unit(defender: true);
            _world.Set(defender, new DefenderTile { cell = new SimInt2(3, 0) });

            new PickupConsumeSystem(_channels).Run(_world);

            Assert.IsTrue(_world.Has<LastRun>(defender));
            Assert.IsFalse(_world.Has<LastRun>(enemy));
        }

        [Test]
        public void Consume_SkipsDeadAndPending()
        {
            RedBull();
            FlowField();
            var pickup = _world.Create();
            _world.Set(pickup, new Pickup { cell = new SimInt2(1, 1), remainingLife = 9f });
            var dead = Unit(defender: true);
            _world.Set(dead, new DefenderTile { cell = new SimInt2(1, 1) });
            _world.Set(dead, new DeadTag());

            new PickupConsumeSystem(_channels).Run(_world);

            Assert.IsTrue(_world.Exists(pickup));
            Assert.IsFalse(_world.Has<LastRun>(dead));
        }

        // ── 클러스터 ──────────────────────────────────────────────────────────

        [Test]
        public void Cluster_OrdersSpawnBeforeConsume()
        {
            // ⚠ 스폰이 놓은 픽업을 소비가 **같은 틱**에 먹는다.
            var steps = new GimmickCluster(new SimChannels()).Steps().ToList();
            var orders = steps.Select(s => s.Order).ToArray();
            CollectionAssert.IsSubsetOf(new[] { 22, 23 }, orders);
            Assert.Less(orders.ToList().IndexOf(22), orders.ToList().IndexOf(23),
                "선언 순서도 스폰 → 소비다(정렬은 파이프라인이 하지만 읽는 사람도 본다)");
            foreach (var s in steps)
                Assert.AreEqual(SimPipeline.PhaseForOrder(s.Order), s.Phase, $"#{s.Order}({s.Name})");
        }

        [Test]
        public void SpawnThenConsume_InOneTick()
        {
            var ch = new SimChannels();
            var tick = new SimPipeline().Add(new GimmickCluster(ch).Steps()).Build();
            RedBull(interval: 1f, maxActive: 5);
            SpawnState(new[] { new SimInt2(2, 2) });
            FlowField();
            var defender = Unit(defender: true);
            _world.Set(defender, new DefenderTile { cell = new SimInt2(2, 2) });

            tick.Run(_world, 1f);

            Assert.AreEqual(0, PickupCount(), "놓자마자 먹혔다");
            Assert.IsTrue(_world.Has<LastRun>(defender));
        }
    }
}
