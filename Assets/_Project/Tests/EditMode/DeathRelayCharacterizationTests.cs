// battle-sim-extraction unit 18-G/1 — **특성화 테스트(구 sim)**.
//
// 18-G 클러스터에서 오라클이 **0** 인 셋: `LethalTimerSystem`(#12) ·
// `ShieldCastSystem`(#19) · `ResignationDropSystem`(#35). 계획서 §증인 4 —
// 구 sim 에 먼저 붙여 초록을 확인하고, 이식 후 어서션 그대로 복제한다.
//
// 순수 부분(`ShieldMath`·`ShieldTargeting`)은 이미 오라클이 있다. 여기서 박제하는 것은
// **시스템 골격**이다: self-gate · 사망 창 안에서의 관측 시점 · 쿨다운 규약 · 이중 태그 방지.
using NUnit.Framework;
using Unity.Collections;
using Unity.Core;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Wassup.Battle.Effects;
using Wassup.Battle.Units;
using Wassup.Data;

namespace Wassup.Tests.EditMode
{
    // ── #12 LethalTimerSystem ─────────────────────────────────────────────────

    public class LethalTimerSystemTests
    {
        private World _world;
        private EntityManager _em;
        private SimulationSystemGroup _grp;

        [SetUp]
        public void SetUp()
        {
            _world = new World("LethalTimerTests");
            _em = _world.EntityManager;
            _grp = _world.CreateSystemManaged<SimulationSystemGroup>();
            _grp.AddSystemToUpdateList(_world.CreateSystem<LethalTimerSystem>());
        }

        [TearDown]
        public void TearDown() => _world?.Dispose();

        private Entity Bomber(float remaining, bool alreadyDead = false)
        {
            var e = _em.CreateEntity();
            _em.AddComponentData(e, new LethalTimer { remaining = remaining });
            if (alreadyDead) _em.AddComponent<DeadTag>(e);
            return e;
        }

        private void Tick(float dt)
        {
            _world.SetTime(new TimeData(_world.Time.ElapsedTime + dt, dt));
            _grp.Update();
        }

        [Test]
        public void NoLethalTimer_SelfGate_DoesNotRun()
        {
            var e = _em.CreateEntity();   // 타이머 없음
            Assert.DoesNotThrow(() => Tick(1f));
            Assert.IsFalse(_em.HasComponent<DeadTag>(e));
        }

        [Test]
        public void CountsDown_WithoutFiring()
        {
            var e = Bomber(1f);
            Tick(0.25f);
            Assert.AreEqual(0.75f, _em.GetComponentData<LethalTimer>(e).remaining, 1e-5f);
            Assert.IsFalse(_em.HasComponent<DeadTag>(e));
        }

        [Test]
        public void OnExpiry_AddsDeadTag_AndRemovesTheTimer()
        {
            var e = Bomber(0.1f);
            Tick(1f);
            Assert.IsTrue(_em.HasComponent<DeadTag>(e), "자폭도 공용 사망 경로를 탄다.");
            Assert.IsFalse(_em.HasComponent<LethalTimer>(e), "타이머는 제거된다(재발화 방지).");
        }

        [Test]
        public void Expiry_IsAtOrBelowZero()
        {
            var e = Bomber(1f);
            Tick(1f);   // 정확히 0
            Assert.IsTrue(_em.HasComponent<DeadTag>(e));
        }

        [Test]
        public void AlreadyDeadUnit_IsSkipped_SoItIsNeverDoubleTagged()
        {
            // 같은 프레임에 피해로 이미 죽은 유닛은 쿼리에서 빠진다 — 타이머도 안 줄어든다.
            var e = Bomber(0.1f, alreadyDead: true);
            Tick(1f);
            Assert.AreEqual(0.1f, _em.GetComponentData<LethalTimer>(e).remaining, 1e-5f,
                "WithNone<DeadTag> — 이미 죽은 유닛은 건드리지 않는다.");
        }
    }

    // ── #35 ResignationDropSystem ─────────────────────────────────────────────

    public class ResignationDropSystemTests
    {
        private World _world;
        private EntityManager _em;
        private SimulationSystemGroup _grp;

        [SetUp]
        public void SetUp()
        {
            _world = new World("ResignationDropTests");
            _em = _world.EntityManager;
            _grp = _world.CreateSystemManaged<SimulationSystemGroup>();
            _grp.AddSystemToUpdateList(_world.CreateSystem<ResignationDropSystem>());
        }

        [TearDown]
        public void TearDown() => _world?.Dispose();

        private void Configure()
            => _em.AddComponentData(_em.CreateEntity(), new ClockOutGimmickConfig { resignationThreshold = 3 });

        private Entity DeadDefender(int2 cell)
        {
            var e = _em.CreateEntity();
            _em.AddComponentData(e, new DefenderTile { cell = cell });
            _em.AddComponent<DefenderUnitTag>(e);
            _em.AddComponent<DeadTag>(e);
            return e;
        }

        private void Tick()
        {
            _world.SetTime(new TimeData(_world.Time.ElapsedTime + 0.016f, 0.016f));
            _grp.Update();
        }

        private int ResignationCount()
            => _em.CreateEntityQuery(ComponentType.ReadOnly<Resignation>()).CalculateEntityCount();

        [Test]
        public void NoGimmickConfig_SelfGate_DropsNothing()
        {
            DeadDefender(new int2(2, 3));
            Tick();
            Assert.AreEqual(0, ResignationCount());
        }

        [Test]
        public void DeadDefender_DropsOneResignation_AtItsTile()
        {
            Configure();
            DeadDefender(new int2(2, 3));
            Tick();

            Assert.AreEqual(1, ResignationCount());
            var q = _em.CreateEntityQuery(ComponentType.ReadOnly<Resignation>());
            var arr = q.ToComponentDataArray<Resignation>(Allocator.Temp);
            Assert.AreEqual(new int2(2, 3), arr[0].cell, "사망 셀은 DefenderTile 에서 읽는다.");
            arr.Dispose();
        }

        [Test]
        public void LivingDefender_DropsNothing()
        {
            Configure();
            var e = _em.CreateEntity();
            _em.AddComponentData(e, new DefenderTile { cell = new int2(1, 1) });
            _em.AddComponent<DefenderUnitTag>(e);
            Tick();
            Assert.AreEqual(0, ResignationCount());
        }

        [Test]
        public void DeadNonDefender_DropsNothing()
        {
            Configure();
            var e = _em.CreateEntity();
            _em.AddComponentData(e, new DefenderTile { cell = new int2(1, 1) });
            _em.AddComponent<DeadTag>(e);   // DefenderUnitTag 없음
            Tick();
            Assert.AreEqual(0, ResignationCount());
        }

        [Test]
        public void EachDeadDefender_DropsExactlyOne()
        {
            Configure();
            DeadDefender(new int2(1, 1));
            DeadDefender(new int2(2, 2));
            Tick();
            Assert.AreEqual(2, ResignationCount());
        }
    }

    // ── #19 ShieldCastSystem ──────────────────────────────────────────────────

    public class ShieldCastSystemTests
    {
        private static readonly int2 Grid = new int2(12, 12);

        private World _world;
        private EntityManager _em;
        private SimulationSystemGroup _grp;
        private FlowFieldSingleton _field;

        [SetUp]
        public void SetUp()
        {
            _world = new World("ShieldCastTests");
            _em = _world.EntityManager;
            _grp = _world.CreateSystemManaged<SimulationSystemGroup>();
            _grp.AddSystemToUpdateList(_world.CreateSystem<ShieldCastSystem>());

            int n = Grid.x * Grid.y;
            _field = new FlowFieldSingleton
            {
                flow = new NativeArray<float2>(n, Allocator.Persistent),
                dist = new NativeArray<int>(n, Allocator.Persistent),
                gridSize = Grid, tileSize = 1f, origin = float3.zero,
                goalCell = new int2(11, 11),
            };
            _em.AddComponentData(_em.CreateEntity(), _field);
        }

        [TearDown]
        public void TearDown()
        {
            _field.Dispose();
            _world?.Dispose();
        }

        private Entity Defender(int2 cell, float hp = 10f, float maxHp = 10f)
        {
            var e = _em.CreateEntity();
            _em.AddComponent<DefenderUnitTag>(e);
            _em.AddComponentData(e, new Health { value = hp, max = maxHp });
            _em.AddComponentData(e, LocalTransform.FromPosition(new float3(cell.x, 0f, cell.y)));
            _em.AddBuffer<IncomingShield>(e);
            _em.AddBuffer<ShieldSlot>(e);
            return e;
        }

        private void MakeCaster(Entity e, float range, float amount, int targetCount,
                                ShieldTargetFilter filter, float cooldown = 4f)
            => _em.AddComponentData(e, new ShieldCastState
            {
                range = range, cooldownDuration = cooldown, cooldownRemaining = 0f,
                amount = amount, targetCount = targetCount, filter = filter,
            });

        private void Tick(float dt = 0.016f)
        {
            _world.SetTime(new TimeData(_world.Time.ElapsedTime + dt, dt));
            _grp.Update();
        }

        private int IncomingCount(Entity e) => _em.GetBuffer<IncomingShield>(e).Length;

        [Test]
        public void NoCaster_SelfGate_DoesNothing()
        {
            var d = Defender(new int2(2, 2));
            Assert.DoesNotThrow(() => Tick());
            Assert.AreEqual(0, IncomingCount(d));
        }

        [Test]
        public void Casts_ToSelf_AndResetsCooldown()
        {
            var c = Defender(new int2(2, 2));
            MakeCaster(c, range: 2f, amount: 5f, targetCount: 1, filter: ShieldTargetFilter.Self);
            Tick();

            Assert.AreEqual(1, IncomingCount(c), "자신도 항상 후보다.");
            Assert.AreEqual(5f, _em.GetBuffer<IncomingShield>(c)[0].amount, 1e-5f);
            Assert.AreEqual(c, _em.GetBuffer<IncomingShield>(c)[0].source);
            Assert.AreEqual(4f, _em.GetComponentData<ShieldCastState>(c).cooldownRemaining, 1e-5f,
                "발화 후 쿨다운 리셋.");
        }

        [Test]
        public void CooldownTicks_WithoutCasting()
        {
            var c = Defender(new int2(2, 2));
            MakeCaster(c, 2f, 5f, 1, ShieldTargetFilter.Self);
            Tick();                                   // 발화 + 쿨다운 4
            _em.GetBuffer<IncomingShield>(c).Clear();

            Tick(1f);
            Assert.AreEqual(0, IncomingCount(c), "쿨다운 중엔 발화하지 않는다.");
            Assert.AreEqual(3f, _em.GetComponentData<ShieldCastState>(c).cooldownRemaining, 1e-5f);
        }

        [Test]
        public void RangeGate_IsChebyshevTiles()
        {
            var c = Defender(new int2(2, 2));
            MakeCaster(c, range: 1f, amount: 5f, targetCount: 8, filter: ShieldTargetFilter.All);
            var near = Defender(new int2(3, 3));    // 체비셰프 1
            var far = Defender(new int2(5, 2));     // 체비셰프 3
            Tick();

            Assert.AreEqual(1, IncomingCount(near), "대각선도 거리 1.");
            Assert.AreEqual(0, IncomingCount(far));
        }

        [Test]
        public void SkipsTargetsAlreadyAtOrAboveTheAmount_FromTheSameSource()
        {
            // 병합이 max 라 no-op 이 될 부여는 건너뛴다 — 만충 아군에 매 주기 헛불꽃이 튀지 않게.
            var c = Defender(new int2(2, 2));
            MakeCaster(c, 2f, amount: 5f, targetCount: 8, filter: ShieldTargetFilter.All);
            var t = Defender(new int2(3, 2));
            _em.GetBuffer<ShieldSlot>(t).Add(new ShieldSlot { source = c, value = 5f });
            Tick();

            Assert.AreEqual(0, IncomingCount(t), "같은 출처가 이미 5 이상이면 스킵.");
        }

        [Test]
        public void DoesNotSkip_WhenTheExistingSlotIsFromAnotherSource()
        {
            var c = Defender(new int2(2, 2));
            MakeCaster(c, 2f, amount: 5f, targetCount: 8, filter: ShieldTargetFilter.All);
            var other = Defender(new int2(9, 9));
            var t = Defender(new int2(3, 2));
            _em.GetBuffer<ShieldSlot>(t).Add(new ShieldSlot { source = other, value = 99f });
            Tick();

            Assert.AreEqual(1, IncomingCount(t), "출처가 다르면 교차 합산 대상이다.");
        }

        [Test]
        public void CooldownResets_EvenWhenNothingWasGranted()
        {
            // 자신이 항상 후보라 매 주기 발화한다 — 미발화 시 매 프레임 재스캔을 막는 규약.
            var c = Defender(new int2(2, 2));
            MakeCaster(c, 2f, amount: 5f, targetCount: 8, filter: ShieldTargetFilter.All);
            _em.GetBuffer<ShieldSlot>(c).Add(new ShieldSlot { source = c, value = 99f });   // 자기도 스킵
            Tick();

            Assert.AreEqual(0, IncomingCount(c));
            Assert.AreEqual(4f, _em.GetComponentData<ShieldCastState>(c).cooldownRemaining, 1e-5f,
                "대상 유무와 무관하게 쿨다운은 리셋된다.");
        }

        [Test]
        public void DeadOrPendingDefenders_AreNeitherCastersNorTargets()
        {
            var c = Defender(new int2(2, 2));
            MakeCaster(c, 2f, 5f, 8, ShieldTargetFilter.All);
            var dead = Defender(new int2(3, 2));
            _em.AddComponent<DeadTag>(dead);
            var pending = Defender(new int2(2, 3));
            _em.AddComponent<PendingDeployment>(pending);
            Tick();

            Assert.AreEqual(0, IncomingCount(dead));
            Assert.AreEqual(0, IncomingCount(pending));
        }
    }
}
