// battle-sim-extraction unit 18-E/3 — 수명 계열의 **특성화 복제**.
//
// 원본: `LastRunSystemTests`(5) · `HazardLifetimeSystemTests`(6, 순회 순서 2건 포함).
// `ObstacleLifetimeSystem` 은 오라클 0 목록에 없었고(구 오라클 `SpawnBlockingHazardTests` 등이
// 진다) 시스템 골격 핀은 여기서 새로 붙인다.
using System.Collections.Generic;
using NUnit.Framework;
using Wassup.Sim;
using Wassup.Sim.Effects;
using Wassup.Sim.Units;

namespace Wassup.Tests.EditMode
{
    public class SimLastRunTests
    {
        private SimWorld _world;
        private LastRunSystem _sys;

        [SetUp]
        public void SetUp()
        {
            _world = new SimWorld(new SimConfig(1u, 1u));
            _sys = new LastRunSystem();
        }

        private void Configure(float fraction)
            => _world.Set(_world.Create(), new RedBullGimmickConfig { lastRunDamageFraction = fraction });

        private SimEntityId Victim(float remaining, float maxHp = 100f, bool withDamageBuffer = true)
        {
            var e = _world.Create();
            _world.Set(e, new LastRun { remaining = remaining });
            _world.Set(e, new Health { value = maxHp, max = maxHp });
            if (withDamageBuffer) _world.AddBuffer<IncomingDamage>(e);
            return e;
        }

        private void Tick(float dt)
        {
            _world.SetDeltaTime(dt);
            _sys.Run(_world);
        }

        private List<IncomingDamage> Dmg(SimEntityId e) => _world.GetBuffer<IncomingDamage>(e);

        [Test]
        public void NoGimmickConfig_SelfGate_DoesNotEvenTick()
        {
            var e = Victim(remaining: 1f);
            Tick(5f);

            Assert.IsTrue(_world.Has<LastRun>(e), "기믹 비활성이면 시스템이 안 돈다.");
            Assert.AreEqual(1f, _world.Get<LastRun>(e).remaining, 1e-5f, "감소조차 없다.");
            Assert.AreEqual(0, Dmg(e).Count);
        }

        [Test]
        public void TicksDown_WithoutFiring_WhileRemainingPositive()
        {
            Configure(0.5f);
            var e = Victim(remaining: 1f);
            Tick(0.25f);

            Assert.AreEqual(0.75f, _world.Get<LastRun>(e).remaining, 1e-5f);
            Assert.AreEqual(0, Dmg(e).Count, "만료 전엔 피해 없음.");
        }

        [Test]
        public void OnExpiry_DealsMaxHpFraction_AndRemovesComponent()
        {
            Configure(0.5f);
            var e = Victim(remaining: 0.1f, maxHp: 200f);
            Tick(1f);

            Assert.AreEqual(1, Dmg(e).Count, "만료 프레임에 1건.");
            Assert.AreEqual(100f, Dmg(e)[0].amount, 1e-5f, "최대체력(200) × fraction(0.5).");
            Assert.AreEqual(SimEntityId.Null, Dmg(e)[0].source, "자해 — 킬 미귀속.");
            Assert.IsFalse(_world.Has<LastRun>(e), "만료 후 컴포넌트 제거.");
        }

        [Test]
        public void Expiry_IsAtOrBelowZero_NotStrictlyBelow()
        {
            Configure(0.5f);
            var e = Victim(remaining: 1f);
            Tick(1f);   // 정확히 0

            Assert.AreEqual(1, Dmg(e).Count, "remaining==0 은 만료다.");
            Assert.IsFalse(_world.Has<LastRun>(e));
        }

        [Test]
        public void MissingDamageBuffer_StillRemovesComponent_ButDealsNoDamage()
        {
            Configure(0.5f);
            var e = Victim(remaining: 0.1f, withDamageBuffer: false);
            Tick(1f);

            Assert.IsFalse(_world.Has<LastRun>(e), "버퍼가 없어도 컴포넌트는 제거된다.");
        }
    }

    public class SimHazardLifetimeTests
    {
        private SimWorld _world;
        private HazardCellIndex _index;
        private HazardLifetimeSystem _sys;

        [SetUp]
        public void SetUp()
        {
            _world = new SimWorld(new SimConfig(1u, 1u));
            _index = new HazardCellIndex();
            _world.Set(_world.Create(), new HazardSingleton { cellToEffects = _index });
            _sys = new HazardLifetimeSystem();
        }

        private SimEntityId Hazard(float life, SimInt2[] cells, params HazardEffect[] effects)
        {
            var e = _world.Create();
            _world.Set(e, new Hazard { remainingLife = life });
            var cb = _world.AddBuffer<HazardCellsBuffer>(e);
            foreach (var c in cells) cb.Add(new HazardCellsBuffer { cell = c });
            var eb = _world.AddBuffer<HazardEffectsBuffer>(e);
            foreach (var f in effects) eb.Add(new HazardEffectsBuffer { effect = f });
            return e;
        }

        private void Tick(float dt)
        {
            _world.SetDeltaTime(dt);
            _sys.Run(_world);
        }

        private float[] EffectOrderAt(SimInt2 cell)
        {
            int n = _index.CountFor(cell);
            var o = new float[n];
            for (int i = 0; i < n; i++) o[i] = _index.Get(cell, i).param1;
            return o;
        }

        [Test]
        public void RebuildsIndex_EveryFrame_SoStaleEntriesCannotSurvive()
        {
            var e = Hazard(10f, new[] { new SimInt2(1, 1) },
                new HazardEffect { kind = CcKind.Slow, param1 = 0.5f });
            Tick(0.1f);
            Assert.AreEqual(1, _index.CountFor(new SimInt2(1, 1)));

            Tick(0.1f);
            Assert.AreEqual(1, _index.CountFor(new SimInt2(1, 1)),
                "매 프레임 Clear + 재적재 — 두 배로 쌓이지 않는다.");
            Assert.IsTrue(_world.Exists(e));
        }

        [Test]
        public void ExpiredHazard_IsDestroyed_AndDoesNotContributeToIndex()
        {
            var e = Hazard(0.05f, new[] { new SimInt2(2, 2) },
                new HazardEffect { kind = CcKind.DoT, param1 = 10f });
            Tick(1f);

            Assert.AreEqual(0, _index.CountFor(new SimInt2(2, 2)),
                "만료 프레임엔 인덱스에 기여하지 않는다.");
            Assert.IsFalse(_world.Exists(e), "만료 = 파괴(**P12 가 아니라 여기서** — 릴레이 밖).");
        }

        [Test]
        public void IndexIs_CellsCrossEffects()
        {
            var cells = new[] { new SimInt2(0, 0), new SimInt2(0, 1), new SimInt2(1, 0) };
            Hazard(10f, cells,
                new HazardEffect { kind = CcKind.Slow, param1 = 0.5f },
                new HazardEffect { kind = CcKind.DoT, param1 = 3f });
            Tick(0.1f);

            foreach (var c in cells)
                Assert.AreEqual(2, _index.CountFor(c), $"{c} 에 효과 2개(셀 × 효과 교차곱).");
            Assert.AreEqual(6, _index.Count, "3 셀 × 2 효과 = 6.");
        }

        [Test]
        public void LifeIsDecremented_BeforeExpiryCheck()
        {
            var e = Hazard(1f, new[] { new SimInt2(0, 0) }, new HazardEffect { kind = CcKind.Slow });
            Tick(0.25f);
            Assert.AreEqual(0.75f, _world.Get<Hazard>(e).remainingLife, 1e-5f);
        }

        // ── tie-break ⑥ — 구 sim 에서 실측한 순서를 그대로 요구한다 ──────────────

        [Test]
        public void EffectOrderWithinCell_IsReverseInsertion_NotInsertion()
        {
            Hazard(10f, new[] { new SimInt2(0, 0) },
                new HazardEffect { kind = CcKind.Slow, param1 = 1f },
                new HazardEffect { kind = CcKind.Slow, param1 = 2f },
                new HazardEffect { kind = CcKind.Slow, param1 = 3f });
            Tick(0.1f);

            CollectionAssert.AreEqual(new[] { 3f, 2f, 1f }, EffectOrderAt(new SimInt2(0, 0)),
                "구 `NativeParallelMultiHashMap` 은 버킷에 prepend 한다 — 관리 List 를 그대로 " +
                "옮기면 순서가 뒤집히고, ZoneApply 의 채널 적재 순서가 갈린다.");
        }

        [Test]
        public void EffectOrderAcrossHazards_OnSameCell_IsAlsoReverseOfAddOrder()
        {
            var cell = new SimInt2(2, 2);
            Hazard(10f, new[] { cell }, new HazardEffect { kind = CcKind.Slow, param1 = 10f });
            Hazard(10f, new[] { cell }, new HazardEffect { kind = CcKind.Slow, param1 = 20f });
            Tick(0.1f);

            CollectionAssert.AreEqual(new[] { 20f, 10f }, EffectOrderAt(cell),
                "나중에 적재된 해저드의 효과가 먼저 읽힌다.");
        }

        [Test]
        public void MissingBuffers_SkipTheHazard_WithoutTicking()
        {
            // 구 쿼리는 `Hazard` + 두 버퍼를 모두 요구한다 — 하나만 없어도 수명이 줄지 않는다.
            var e = _world.Create();
            _world.Set(e, new Hazard { remainingLife = 5f });
            _world.AddBuffer<HazardCellsBuffer>(e).Add(new HazardCellsBuffer { cell = SimInt2.Zero });
            // HazardEffectsBuffer 없음
            Tick(1f);

            Assert.AreEqual(5f, _world.Get<Hazard>(e).remainingLife, 1e-5f);
        }
    }

    public class SimObstacleLifetimeTests
    {
        private SimWorld _world;
        private ObstacleSingleton _obstacles;
        private ObstacleLifetimeSystem _sys;

        [SetUp]
        public void SetUp()
        {
            _world = new SimWorld(new SimConfig(1u, 1u));
            _obstacles = new ObstacleSingleton { blockedCells = new HashSet<SimInt2>() };
            _world.Set(_world.Create(), _obstacles);
            _sys = new ObstacleLifetimeSystem();
        }

        private SimEntityId PlainObstacle(SimInt2 cell, float life)
        {
            var e = _world.Create();
            _world.Set(e, new Obstacle { cell = cell, remainingLife = life });
            return e;
        }

        private SimEntityId Blocking(SimInt2[] cells, bool dead = false)
        {
            var e = _world.Create();
            _world.Set(e, new BlockingHazard { maxHp = 10f });
            _world.Set(e, new Obstacle { cell = cells[0], remainingLife = 999f });
            var cb = _world.AddBuffer<BlockingHazardCellsBuffer>(e);
            foreach (var c in cells) cb.Add(new BlockingHazardCellsBuffer { cell = c });
            if (dead) _world.Set(e, default(DeadTag));
            return e;
        }

        private void Tick(float dt)
        {
            _world.SetDeltaTime(dt);
            _sys.Run(_world);
        }

        [Test]
        public void NoSingleton_SelfGate_DoesNotTick()
        {
            var fresh = new SimWorld(new SimConfig(1u, 1u));
            var e = fresh.Create();
            fresh.Set(e, new Obstacle { cell = SimInt2.Zero, remainingLife = 1f });
            fresh.SetDeltaTime(5f);
            new ObstacleLifetimeSystem().Run(fresh);

            Assert.AreEqual(1f, fresh.Get<Obstacle>(e).remainingLife, 1e-5f);
        }

        [Test]
        public void PlainObstacle_TicksAndRegistersCell()
        {
            var e = PlainObstacle(new SimInt2(2, 3), 1f);
            Tick(0.25f);

            Assert.AreEqual(0.75f, _world.Get<Obstacle>(e).remainingLife, 1e-5f);
            Assert.IsTrue(_obstacles.blockedCells.Contains(new SimInt2(2, 3)));
        }

        [Test]
        public void PlainObstacle_OnExpiry_IsDestroyed_AndUnregistered()
        {
            var e = PlainObstacle(new SimInt2(2, 3), 0.05f);
            Tick(1f);

            Assert.IsFalse(_world.Exists(e), "수명 만료 = 즉시 파괴(릴레이 밖).");
            Assert.IsFalse(_obstacles.blockedCells.Contains(new SimInt2(2, 3)),
                "만료 프레임엔 셀도 등록하지 않는다.");
        }

        [Test]
        public void RebuildsBlockedCells_EveryFrame()
        {
            PlainObstacle(new SimInt2(1, 1), 10f);
            Tick(0.1f);
            Assert.AreEqual(1, _obstacles.blockedCells.Count);
            Tick(0.1f);
            Assert.AreEqual(1, _obstacles.blockedCells.Count, "Clear 후 재적재 — 누적되지 않는다.");
        }

        [Test]
        public void BlockingHazard_RegistersAllItsCells_AndIsNotLifeTicked()
        {
            // 이동 차단 해저드의 수명은 #2 가 관리한다 — 여기서는 셀만 등록한다.
            var cells = new[] { new SimInt2(4, 4), new SimInt2(4, 5), new SimInt2(5, 4) };
            var e = Blocking(cells);
            Tick(1f);

            foreach (var c in cells)
                Assert.IsTrue(_obstacles.blockedCells.Contains(c), $"{c} 등록");
            Assert.AreEqual(999f, _world.Get<Obstacle>(e).remainingLife, 1e-5f,
                "차단 해저드는 이 시스템이 수명을 깎지 않는다(첫 루프가 버퍼 보유를 제외).");
        }

        [Test]
        public void DeadBlockingHazard_StopsBlockingImmediately()
        {
            // "죽었지만 아직 있는" 창(P10~P12) 동안 길을 막으면 안 된다.
            var cells = new[] { new SimInt2(4, 4) };
            var e = Blocking(cells, dead: true);
            Tick(0.1f);

            Assert.IsTrue(_world.Exists(e), "아직 파괴되지 않았다(#41 이 P12 에 한다).");
            Assert.IsFalse(_obstacles.blockedCells.Contains(new SimInt2(4, 4)),
                "그래도 즉시 길을 열어준다 — DeadTag 가 붙은 순간부터.");
        }
    }
}
