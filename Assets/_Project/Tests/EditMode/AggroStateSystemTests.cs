using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Wassup.Battle.Combat;
using Wassup.Battle.Effects;
using Wassup.Battle.Movement;
using Wassup.Battle.Units;
using Wassup.Data;

namespace Wassup.Tests.EditMode
{
    // aggro-targeting Unit 12/14 — 히트 구동 AggroStateSystem 회귀. 근접 즉시 배정을
    // 폐기했으므로 획득은 AggroAcquireEvent 드레인으로만 일어난다. capacity 게이트, 선점,
    // 사망 해제, orphan 해제, H1(같은 틱 상한 초과 방지), 도발 grant/strip 을 고정.
    public class AggroStateSystemTests
    {
        private World _world;
        private EntityManager _em;
        private SimulationSystemGroup _simGroup;
        private NativeQueue<AggroAcquireEvent> _hitQueue;

        [SetUp]
        public void SetUp()
        {
            _world = new World("AggroStateTestWorld");
            _em = _world.EntityManager;
            _simGroup = _world.CreateSystemManaged<SimulationSystemGroup>();
            _simGroup.AddSystemToUpdateList(_world.CreateSystem<AggroStateSystem>());
            _simGroup.AddSystemToUpdateList(_world.CreateSystem<TauntAttackGrantSystem>());

            // Combat→Effects 히트 채널 싱글턴(테스트 하네스가 브리지 역할).
            _hitQueue = new NativeQueue<AggroAcquireEvent>(Allocator.Persistent);
            var singleton = _em.CreateEntity();
            _em.AddComponentData(singleton, new AggroAcquireEventsSingleton { queue = _hitQueue });
        }

        [TearDown]
        public void TearDown()
        {
            if (_hitQueue.IsCreated) _hitQueue.Dispose();
            if (_world != null && _world.IsCreated) _world.Dispose();
        }

        private Entity MakeGuardian(int capacity, float3 pos)
        {
            var e = _em.CreateEntity();
            _em.AddComponentData(e, new Health { value = 100f, max = 100f });
            _em.AddComponentData(e, new FactionTag { value = Faction.DefenderUnit });
            _em.AddComponentData(e, LocalTransform.FromPosition(pos));
            _em.AddComponentData(e, new AggroCapacity { max = capacity, held = 0 });
            return e;
        }

        private Entity MakeEnemy(float3 pos)
        {
            var e = _em.CreateEntity();
            _em.AddComponentData(e, new Health { value = 100f, max = 100f });
            _em.AddComponentData(e, new FactionTag { value = Faction.EnemyUnit });
            _em.AddComponentData(e, LocalTransform.FromPosition(pos));
            // aggro-tile-chase unit 1 — 전투수단 없는 적은 획득 거부되므로, 실데이터처럼
            // 도발 프로파일을 기본 부여(모든 적 에셋이 aggroAttackDamage>0 로 베이크됨).
            _em.AddComponentData(e, new AggroAttackProfile { damage = 5f, cooldown = 1f, range = 1f });
            return e;
        }

        private void Hit(Entity guardian, Entity enemy)
            => _hitQueue.Enqueue(new AggroAcquireEvent { guardian = guardian, enemy = enemy });

        private int AggroedCount()
        {
            using var q = _em.CreateEntityQuery(ComponentType.ReadOnly<Aggroed>());
            return q.CalculateEntityCount();
        }

        [Test]
        public void HitDrivenAcquire_AggrosHitEnemy()
        {
            var g = MakeGuardian(4, float3.zero);
            var e = MakeEnemy(new float3(1, 0, 0));
            Hit(g, e);
            _simGroup.Update();
            Assert.IsTrue(_em.HasComponent<Aggroed>(e), "명중한 적이 어그로됨");
            Assert.AreEqual(g, _em.GetComponentData<Aggroed>(e).guardian);
        }

        // ── battle-structures unit 2 — 도발 범위 게이트 ─────────────────────────
        // 판정은 **저작 의도**(EnemyTargetFilter.factionMask)다. 런타임 마스크를 읽으면
        // 무기 없는 적이 순환에 빠져 영구 도발 불가가 된다(계약 2).
        // 위 HitDrivenAcquire_AggrosHitEnemy 를 비롯한 기존 전량이 필터 **없이** 돌므로
        // fail-open(컴포넌트 부재 = 통과)은 그쪽이 덮는다.

        private void SetTargetIntent(Entity enemy, Faction factions)
            => _em.AddComponentData(enemy, new EnemyTargetFilter
            {
                classMask = -1,
                priorityClass = -1,
                factionMask = (int)factions,
            });

        [Test]
        public void StructureOnlyEnemy_IsNotAggroed()
        {
            var g = MakeGuardian(4, float3.zero);
            var e = MakeEnemy(new float3(1, 0, 0));
            SetTargetIntent(e, Faction.DefenderCore);   // 유닛 비트 전무 = 거점 전담

            Hit(g, e);
            _simGroup.Update();

            Assert.IsFalse(_em.HasComponent<Aggroed>(e),
                "거점 전담 적은 유인으로 막을 수 없다 — 죽여야만 막힌다");
            Assert.AreEqual(0, AggroedCount());
        }

        [Test]
        public void UnitTargetingEnemy_WithIntent_IsStillAggroed()
        {
            var g = MakeGuardian(4, float3.zero);
            var e = MakeEnemy(new float3(1, 0, 0));
            SetTargetIntent(e, Faction.DefenderUnit | Faction.BlockingHazard | Faction.DefenderCore);

            Hit(g, e);
            _simGroup.Update();

            Assert.IsTrue(_em.HasComponent<Aggroed>(e),
                "유닛을 노리는 적은 그대로 도발된다 — 이게 깨지면 게이트가 과잉 차단이다");
        }

        // 투트랙 리뷰 H1 회귀선 — factionMask 미설정(0) 적도 도발된다. 0 = «미저작» 이고
        // 그 의미는 베이크와 게이트가 같은 함수(EnemyTargetDefaults.Resolve)로 읽는다.
        // 게이트가 raw 필드를 읽으면 0 & AnyUnit == 0 → 영구 도발 불가(무음)가 된다.
        [Test]
        public void UnauthoredFactionMask_IsStillAggroed()
        {
            var g = MakeGuardian(4, float3.zero);
            var e = MakeEnemy(new float3(1, 0, 0));
            // 실존 합성 사이트와 동형: classMask/priorityClass 만 설정, factionMask 는 0.
            _em.AddComponentData(e, new EnemyTargetFilter { classMask = -1, priorityClass = -1 });

            Hit(g, e);
            _simGroup.Update();

            Assert.IsTrue(_em.HasComponent<Aggroed>(e),
                "factionMask 0 = 미저작 → 레거시 마스크(유닛 포함)로 해석돼 도발된다");
        }

        // 계약 2 의 순환 함정 회귀선. MakeEnemy 는 AttackState 를 주지 않는다(러너·스위프트
        // 동형) — 런타임 마스크로 판정했다면 여기서 영구 도발 불가가 된다.
        [Test]
        public void WeaponlessEnemy_TargetingUnits_IsStillAggroed()
        {
            var g = MakeGuardian(4, float3.zero);
            var e = MakeEnemy(new float3(1, 0, 0));
            Assert.IsFalse(_em.HasComponent<AttackState>(e), "전제: 무기(AttackState) 없음");
            SetTargetIntent(e, Faction.DefenderUnit | Faction.BlockingHazard | Faction.DefenderCore);

            Hit(g, e);
            _simGroup.Update();

            Assert.IsTrue(_em.HasComponent<Aggroed>(e),
                "무기가 없어도 저작 의도가 유닛을 포함하면 도발된다");
        }

        [Test]
        public void CapacityCap_SameTick_DoesNotExceed()
        {
            var g = MakeGuardian(2, float3.zero);
            var e1 = MakeEnemy(new float3(1, 0, 0));
            var e2 = MakeEnemy(new float3(2, 0, 0));
            var e3 = MakeEnemy(new float3(3, 0, 0));
            Hit(g, e1); Hit(g, e2); Hit(g, e3); // 같은 틱 3 히트, 상한 2
            _simGroup.Update();
            Assert.AreEqual(2, AggroedCount(), "capacity 만큼만 어그로");
        }

        [Test]
        public void CapacityCap_AcrossTicks_RespectsRunningHeld()
        {
            // critic H1 — held 가 이전 틱 값이어도 같은 틱 드레인이 상한을 소프트 초과하면 안 됨.
            var g = MakeGuardian(2, float3.zero);
            var e1 = MakeEnemy(new float3(1, 0, 0));
            Hit(g, e1);
            _simGroup.Update(); // held 1
            Assert.AreEqual(1, AggroedCount());

            var e2 = MakeEnemy(new float3(2, 0, 0));
            var e3 = MakeEnemy(new float3(3, 0, 0));
            Hit(g, e2); Hit(g, e3); // 남은 슬롯 1, 히트 2
            _simGroup.Update();
            Assert.AreEqual(2, AggroedCount(), "held 1 + 여유 1 → 총 2 (3번째 거절)");
            // held 는 1-tick 지연(Pass 2 재계산은 드레인 전 커밋분 기준). 한 틱 더 돌리면 수렴.
            _simGroup.Update();
            Assert.AreEqual(2, _em.GetComponentData<AggroCapacity>(g).held, "held 재계산 수렴");
        }

        [Test]
        public void Preemption_SameTick_FirstGuardianWins()
        {
            var g1 = MakeGuardian(2, float3.zero);
            var g2 = MakeGuardian(2, new float3(1, 0, 0));
            var e = MakeEnemy(new float3(0.5f, 0, 0));
            Hit(g1, e); Hit(g2, e); // 같은 적, g1 먼저
            _simGroup.Update();
            Assert.AreEqual(g1, _em.GetComponentData<Aggroed>(e).guardian, "먼저 때린 가디언이 선점");
        }

        [Test]
        public void Preemption_AcrossTicks_KeepsFirstGuardian()
        {
            var g1 = MakeGuardian(2, float3.zero);
            var g2 = MakeGuardian(2, new float3(1, 0, 0));
            var e = MakeEnemy(new float3(0.5f, 0, 0));
            Hit(g1, e);
            _simGroup.Update();
            Hit(g2, e); // 이미 어그로된 적
            _simGroup.Update();
            Assert.AreEqual(g1, _em.GetComponentData<Aggroed>(e).guardian, "이미 어그로된 적은 유지");
        }

        [Test]
        public void GuardianDeath_ReleasesAggro()
        {
            var g = MakeGuardian(2, float3.zero);
            var e = MakeEnemy(new float3(1, 0, 0));
            Hit(g, e);
            _simGroup.Update();
            Assert.IsTrue(_em.HasComponent<Aggroed>(e));

            _em.SetComponentData(g, new Health { value = 0f, max = 100f });
            _simGroup.Update();
            Assert.IsFalse(_em.HasComponent<Aggroed>(e), "가디언 사망 → 해제");
        }

        [Test]
        public void LastGuardianDestroyed_ReleasesOrphan()
        {
            var g = MakeGuardian(2, float3.zero);
            var e = MakeEnemy(new float3(1, 0, 0));
            Hit(g, e);
            _simGroup.Update();
            Assert.IsTrue(_em.HasComponent<Aggroed>(e));

            _em.DestroyEntity(g); // 마지막 가디언 소멸 — orphan 도 해제돼야
            _simGroup.Update();
            Assert.IsFalse(_em.HasComponent<Aggroed>(e), "orphan 어그로 해제");
        }

        // ── aggro-tile-chase unit 1 — 획득 게이트 + chase field ──────────────

        [Test]
        public void NoAttackNoProfile_Refused()
        {
            var g = MakeGuardian(4, float3.zero);
            var e = _em.CreateEntity(); // MakeEnemy 미사용 — 전투수단 없음
            _em.AddComponentData(e, new Health { value = 100f, max = 100f });
            _em.AddComponentData(e, new FactionTag { value = Faction.EnemyUnit });
            _em.AddComponentData(e, LocalTransform.FromPosition(new float3(1, 0, 0)));
            Hit(g, e);
            _simGroup.Update();
            Assert.IsFalse(_em.HasComponent<Aggroed>(e), "전투수단 없는 적은 획득 거부");
        }

        // 4×3 flow field: y1 행만 walk (goal (0,1)). 나머지 벽(zero-flow).
        private FlowFieldSingleton MakeFlowField(bool includeAirLayers = false)
        {
            int2 gridSize = new int2(4, 3);
            var flow = new NativeArray<float2>(12, Allocator.Persistent);
            var dist = new NativeArray<int>(12, Allocator.Persistent);
            // continuous-agent-movement unit 2 — 픽스처의 의도("통로는 y=1 행 하나뿐")는
            // 그대로이고 그 의도를 표현하는 수단만 flow=0 → walkMask=0 으로 바뀐다.
            // 이제 벽은 지형이 정하므로 flow 만으로는 y=0/y=2 가 벽이 되지 않는다.
            var walk = new NativeArray<byte>(12, Allocator.Persistent);
            var cellLayers = includeAirLayers
                ? new NativeArray<byte>(12, Allocator.Persistent)
                : default;
            for (int i = 0; i < 12; i++) { flow[i] = float2.zero; dist[i] = int.MaxValue; walk[i] = 0; }
            if (cellLayers.IsCreated)
                for (int i = 0; i < 12; i++) cellLayers[i] = (byte)PlacementLayer.Air;
            for (int x = 0; x < 4; x++)
            {
                int idx = 1 * 4 + x;
                dist[idx] = x;
                flow[idx] = x == 0 ? float2.zero : new float2(-1f, 0f); // goal (0,1) 는 zero(특례)
                walk[idx] = 1;                                          // 통로만 Walk 타일
                if (cellLayers.IsCreated)
                    cellLayers[idx] |= (byte)PlacementLayer.Path;
            }
            var f = new FlowFieldSingleton
            {
                flow = flow, dist = dist, walkMask = walk, cellLayers = cellLayers, gridSize = gridSize,
                goalCell = new int2(0, 1), tileSize = 1f, origin = float3.zero,
            };
            var s = _em.CreateEntity();
            _em.AddComponentData(s, f);
            return f;
        }

        [Test]
        public void ChaseField_AttachedWhenReachable_RemovedOnRelease()
        {
            var f = MakeFlowField();
            var g = MakeGuardian(2, new float3(2f, 0f, 0f));   // 셀 (2,0) — 통로 y1 인접 (range1 소스 존재)
            var e = MakeEnemy(new float3(3f, 0f, 1f));          // 셀 (3,1) — 통로 위
            Hit(g, e);
            _simGroup.Update();
            Assert.IsTrue(_em.HasComponent<Aggroed>(e), "도달 가능 — 획득");
            Assert.IsTrue(_em.HasBuffer<AggroChaseCell>(e), "chase field 부착");
            var buf = _em.GetBuffer<AggroChaseCell>(e);
            Assert.AreEqual(12, buf.Length);
            Assert.AreNotEqual(int.MaxValue, buf[1 * 4 + 3].dist, "적 셀 도달 가능");

            _em.SetComponentData(g, new Health { value = 0f, max = 100f });
            _simGroup.Update();
            Assert.IsFalse(_em.HasComponent<Aggroed>(e), "가디언 사망 해제");
            Assert.IsFalse(_em.HasBuffer<AggroChaseCell>(e), "해제 시 chase field 도 제거");
            f.Dispose();
        }

        [Test]
        public void ChaseField_UnreachableEnemy_Refused()
        {
            var f = MakeFlowField();
            var g = MakeGuardian(2, new float3(2f, 0f, 0f));    // 통로 인접 — 목적지 후보는 있음
            var e = MakeEnemy(new float3(3f, 0f, 2f));          // 셀 (3,2) — 벽 위(BFS 도달 불가)
            Hit(g, e);
            _simGroup.Update();
            Assert.IsFalse(_em.HasComponent<Aggroed>(e), "도달 불가(벽/고립 셀) — 거부");
            f.Dispose();
        }

        [Test]
        public void AirEnemy_ChaseFieldUsesAirLayer_AcrossGroundWalls()
        {
            var f = MakeFlowField(includeAirLayers: true);
            var g = MakeGuardian(2, new float3(2f, 0f, 0f));
            var e = MakeEnemy(new float3(3f, 0f, 2f)); // Path 층에선 고립된 벽 셀
            _em.AddComponentData(e, new PathFollowState
            {
                speed = 1f,
                radius = 0.25f,
                traversalLayers = (byte)PlacementLayer.Air,
            });

            Hit(g, e);
            _simGroup.Update();

            Assert.IsTrue(_em.HasComponent<Aggroed>(e),
                "Air 적은 지상 벽 너머 가디언에게도 유인될 수 있어야 한다");
            Assert.IsTrue(_em.HasBuffer<AggroChaseCell>(e));
            var chase = _em.GetBuffer<AggroChaseCell>(e);
            Assert.AreNotEqual(int.MaxValue, chase[2 * 4 + 3].dist,
                "추격 필드가 적의 Air 층으로 구워져 벽 셀도 도달 가능");
            f.Dispose();
        }

        // (무후보 거부 srcCount==0 경로는 순수함수 테스트 PerpendicularPin_Range1_NoSources 가 커버 —
        //  3행 합성 필드에선 range1 무후보 가디언 배치가 기하적으로 성립하지 않는다.)

        [Test]
        public void TauntAttack_GrantedOnAggro_StrippedOnRelease()
        {
            var g = MakeGuardian(2, float3.zero);
            var runner = MakeEnemy(new float3(1, 0, 0)); // AttackState/outputs 없음 (프로파일은 MakeEnemy 기본)
            Hit(g, runner);

            _simGroup.Update();
            Assert.IsTrue(_em.HasComponent<Aggroed>(runner));
            Assert.IsTrue(_em.HasComponent<AttackState>(runner), "도발 공격 부여");
            Assert.IsTrue(_em.HasComponent<TauntAttackGranted>(runner));
            Assert.IsTrue(_em.HasBuffer<AttackOutputElement>(runner));

            _em.SetComponentData(g, new Health { value = 0f, max = 100f });
            _simGroup.Update();
            Assert.IsFalse(_em.HasComponent<Aggroed>(runner));
            Assert.IsFalse(_em.HasComponent<AttackState>(runner), "해제 시 도발 공격 strip");
            Assert.IsFalse(_em.HasComponent<TauntAttackGranted>(runner));
        }

        // ── on-place-skill-rework unit 3 — 시한 도발 ─────────────────────────
        //
        // 도발은 히트 어그로와 **세 가지만** 다르다: 시간이 있고, 상한을 무시하고, 선점을
        // 가져온다. 나머지 게이트(보스 면역 · 유닛 미조준 · 공격 수단 · 도달 가능)는 공유하며
        // 그건 기존 테스트가 이미 고정한다.

        private void Taunt(Entity guardian, Entity enemy, float durationSec)
            => _hitQueue.Enqueue(new AggroAcquireEvent
            {
                guardian = guardian,
                enemy = enemy,
                kind = AggroAcquireKind.Taunt,
                durationSec = durationSec,
            });

        // dt 를 실어 한 틱 돌린다. 합성 월드의 기본 시간은 0 이라 만료가 영원히 안 온다.
        private void UpdateWithDelta(float dt)
        {
            _world.SetTime(new Unity.Core.TimeData(_world.Time.ElapsedTime + dt, dt));
            _simGroup.Update();
        }

        [Test]
        public void Taunt_BypassesCapacity_AllTargetsAggroed()
        {
            var g = MakeGuardian(2, float3.zero);   // 상한 2
            var enemies = new Entity[5];
            for (int i = 0; i < 5; i++) { enemies[i] = MakeEnemy(new float3(i, 0, 0)); Taunt(g, enemies[i], 5f); }

            _simGroup.Update();

            Assert.AreEqual(5, AggroedCount(),
                "도발은 상한을 우회해야 한다 — 상한 2 에 5기가 전부 걸려야 한다");
            for (int i = 0; i < 5; i++)
                Assert.AreEqual(g, _em.GetComponentData<Aggroed>(enemies[i]).guardian);
        }

        [Test]
        public void Taunt_ExpiresAfterDuration_AndReleases()
        {
            var g = MakeGuardian(2, float3.zero);
            var e = MakeEnemy(new float3(1, 0, 0));
            Taunt(g, e, 1f);

            _simGroup.Update();
            Assert.IsTrue(_em.HasComponent<Aggroed>(e), "부착");

            UpdateWithDelta(0.5f);
            Assert.IsTrue(_em.HasComponent<Aggroed>(e), "절반 지났을 뿐인데 풀렸다");

            UpdateWithDelta(0.6f);   // 누적 1.1s > 1s
            Assert.IsFalse(_em.HasComponent<Aggroed>(e), "만료됐는데 안 풀렸다");
        }

        // **기존 계약 회귀 핀** — 0 = 무기한 sentinel. 이게 깨지면 히트 어그로가 첫 틱에 풀린다.
        [Test]
        public void HitAggro_IsIndefinite_AndNeverTimesOut()
        {
            var g = MakeGuardian(2, float3.zero);
            var e = MakeEnemy(new float3(1, 0, 0));
            Hit(g, e);

            _simGroup.Update();
            Assert.IsTrue(_em.HasComponent<Aggroed>(e));
            Assert.AreEqual(0f, _em.GetComponentData<Aggroed>(e).remainingTime,
                "히트 획득은 remainingTime 0(무기한)이어야 한다");

            for (int i = 0; i < 5; i++) UpdateWithDelta(10f);
            Assert.IsTrue(_em.HasComponent<Aggroed>(e), "무기한 어그로가 시간으로 풀렸다");
        }

        // 도발 중 가디언이 죽으면 만료를 기다리지 않는다.
        [Test]
        public void Taunt_ReleasesImmediately_WhenGuardianDies()
        {
            var g = MakeGuardian(2, float3.zero);
            var e = MakeEnemy(new float3(1, 0, 0));
            Taunt(g, e, 60f);
            _simGroup.Update();
            Assert.IsTrue(_em.HasComponent<Aggroed>(e));

            _em.SetComponentData(g, new Health { value = 0f, max = 100f });
            _simGroup.Update();
            Assert.IsFalse(_em.HasComponent<Aggroed>(e), "가디언이 죽었는데 도발이 남았다");
        }

        // 선점 우회 — 최근 우선. 만료 후엔 이전 가디언으로 **복귀하지 않는다**.
        [Test]
        public void Taunt_TakesEnemyFromAnotherGuardian_AndFullyReleasesOnExpiry()
        {
            var g1 = MakeGuardian(4, float3.zero);
            var g2 = MakeGuardian(4, new float3(5, 0, 0));
            var e = MakeEnemy(new float3(1, 0, 0));

            Hit(g1, e);
            _simGroup.Update();
            Assert.AreEqual(g1, _em.GetComponentData<Aggroed>(e).guardian, "먼저 문 가디언");

            Taunt(g2, e, 1f);
            _simGroup.Update();
            Assert.AreEqual(g2, _em.GetComponentData<Aggroed>(e).guardian,
                "도발이 선점을 가져오지 못했다(최근 우선)");

            UpdateWithDelta(1.5f);
            Assert.IsFalse(_em.HasComponent<Aggroed>(e),
                "만료 시 완전 해제여야 한다 — 이전 가디언으로 복귀하지 않는다");
        }

        // ⚠ **N4 회귀 핀.** 게이트를 한 줄로 합치면 히트가 먼저 dequeue 되면서 적을 claimed 에
        // 넣고, 뒤이은 도발이 같은 줄에 걸려 조용히 탈락한다. 브리지(Mono)와 AttackSystem(sim)이
        // 같은 큐를 쓰므로 이 혼재는 예외가 아니라 평상이다.
        [Test]
        public void HitThenTaunt_SameTick_TauntStillWins()
        {
            var g1 = MakeGuardian(4, float3.zero);
            var g2 = MakeGuardian(4, new float3(5, 0, 0));
            var e = MakeEnemy(new float3(1, 0, 0));

            Hit(g1, e);            // 먼저 큐에 들어간다
            Taunt(g2, e, 5f);      // 같은 틱, 뒤에

            _simGroup.Update();

            Assert.IsTrue(_em.HasComponent<Aggroed>(e));
            Assert.AreEqual(g2, _em.GetComponentData<Aggroed>(e).guardian,
                "같은 틱에 히트가 먼저 와도 도발이 이겨야 한다");
            Assert.Greater(_em.GetComponentData<Aggroed>(e).remainingTime, 0f, "시한이 실려야 한다");
        }

        // 겹친 배치가 남은 시간을 깎지 않는다(더 긴 쪽으로 갱신 — CC 갱신 관례).
        [Test]
        public void Taunt_Refresh_KeepsTheLongerRemainder()
        {
            var g = MakeGuardian(2, float3.zero);
            var e = MakeEnemy(new float3(1, 0, 0));

            Taunt(g, e, 10f);
            _simGroup.Update();
            Taunt(g, e, 2f);       // 더 짧은 도발이 덧씌워도
            _simGroup.Update();

            Assert.Greater(_em.GetComponentData<Aggroed>(e).remainingTime, 2.5f,
                "짧은 도발이 긴 잔여를 깎았다");
        }

        // 같은 틱 도발 중복은 한 번만 센다(runningHeld 이중 계상 방지).
        [Test]
        public void Taunt_DuplicateInSameTick_IsIdempotent()
        {
            var g = MakeGuardian(2, float3.zero);
            var e = MakeEnemy(new float3(1, 0, 0));
            Taunt(g, e, 5f);
            Taunt(g, e, 5f);
            _simGroup.Update();

            Assert.AreEqual(1, AggroedCount());

            // ⚠ `held` 는 Pass 1·2 가 매 틱 full recompute 하는데 부착은 ECB(틱 끝)라, 부착한
            // 틱에는 아직 0 이다. 한 틱 더 돌려야 보인다 — `runningHeld` 는 **틱 내** 이중
            // 계상만 막는 로컬 상태이고 권위가 아니다.
            _simGroup.Update();
            Assert.AreEqual(1, _em.GetComponentData<AggroCapacity>(g).held, "held 이중 계상");
        }
    }
}
