using NUnit.Framework;
using Unity.Collections;
using Unity.Core;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Wassup.Battle.Combat;
using Wassup.Battle.Effects;
using Wassup.Battle.Units;
using Wassup.Data;

namespace Wassup.Tests.EditMode
{
    // target-persistence unit 3 — Nearest 모드에도 락(보스 포함, D4) + CC 해제 재선정(D5).
    //
    // 이 파일이 «행동이 실제로 바뀌었다»의 증거다. unit 3 을 넣고 기존 2044건이 기대값
    // 갱신 0 으로 통과했는데, 그건 «안 깼다»일 뿐 «바뀌었다»가 아니다 — 기존 테스트가
    // Nearest 적을 2프레임 이상 돌리며 최근접을 바꾸는 케이스를 갖고 있지 않았다.
    public class NearestLockTests
    {
        private World _world;
        private EntityManager _em;
        private SimulationSystemGroup _simGroup;
        private NativeQueue<UnitAttackVisualEvent> _attackQ;
        private NativeQueue<EnemyCcEvent> _ccQ;
        private Entity _fieldEntity;

        [SetUp]
        public void SetUp()
        {
            _world = new World("NearestLockTests");
            _em = _world.EntityManager;
            _simGroup = _world.CreateSystemManaged<SimulationSystemGroup>();
            _simGroup.AddSystemToUpdateList(_world.CreateSystem<EnemyAiStateSystem>());
            _simGroup.AddSystemToUpdateList(_world.CreateSystem<AttackSystem>());

            _attackQ = new NativeQueue<UnitAttackVisualEvent>(Allocator.Persistent);
            _em.AddComponentData(_em.CreateEntity(), new UnitAttackVisualEventsSingleton { queue = _attackQ });
            _ccQ = new NativeQueue<EnemyCcEvent>(Allocator.Persistent);
            _em.AddComponentData(_em.CreateEntity(), new EnemyCcEventsSingleton { queue = _ccQ });

            int w = 16;
            var flow = new NativeArray<float2>(w, Allocator.Persistent);
            var dist = new NativeArray<int>(w, Allocator.Persistent);
            for (int i = 0; i < w - 1; i++) { flow[i] = new float2(1, 0); dist[i] = (w - 1) - i; }
            flow[w - 1] = float2.zero; dist[w - 1] = 0;
            _fieldEntity = _em.CreateEntity();
            _em.AddComponentData(_fieldEntity, new FlowFieldSingleton
            {
                flow = flow, dist = dist, gridSize = new int2(w, 1),
                goalCell = new int2(w - 1, 0), tileSize = 1f, version = 1,
            });
        }

        [TearDown]
        public void TearDown()
        {
            if (_attackQ.IsCreated) _attackQ.Dispose();
            if (_ccQ.IsCreated) _ccQ.Dispose();
            if (_fieldEntity != Entity.Null && _em.Exists(_fieldEntity) && _em.HasComponent<FlowFieldSingleton>(_fieldEntity))
                _em.GetComponentData<FlowFieldSingleton>(_fieldEntity).Dispose();
            _world?.Dispose();
        }

        private void Tick(float dt = 0.016f)
        {
            _world.SetTime(new TimeData(_world.Time.ElapsedTime + dt, dt));
            _simGroup.Update();
        }

        // Nearest 모드 적. hitDelay 는 인자 — CC 와 committedTarget 의 층 분리를 보려면 창이 필요하다.
        private Entity CreateNearestEnemy(float3 pos, float range = 3f, bool boss = false, float hitDelay = 0f)
        {
            var e = _em.CreateEntity();
            _em.AddComponentData(e, LocalTransform.FromPosition(pos));
            _em.AddComponentData(e, new FactionTag { value = Faction.EnemyUnit });
            _em.AddComponentData(e, new Health { value = 100f, max = 100f });
            _em.AddBuffer<IncomingDamage>(e);
            _em.AddBuffer<CcEffect>(e);
            _em.AddComponentData(e, new AttackState
            {
                range = range, cooldownDuration = 0.05f, cooldownRemaining = 0f,
                attackTargetCount = 1, targetMask = (int)Faction.DefenderUnit, hitDelaySec = hitDelay,
            });
            _em.AddComponentData(e, new EnemyBehavior
            {
                targetMode = EnemyTargetMode.Nearest,
                engageMovement = EngageMovement.Halt,
            });
            _em.AddComponentData(e, new FocusTarget { current = Entity.Null });
            _em.AddComponentData(e, new EnemyAiState { value = AiState.Marching });
            if (boss) _em.AddComponent<BossTag>(e);
            var outs = _em.AddBuffer<AttackOutputElement>(e);
            outs.Add(new AttackOutputElement
            {
                value = new AttackOutput { kind = AttackOutputKind.Damage, magnitude = 4f },
            });
            return e;
        }

        private Entity CreateDefender(float3 pos)
        {
            var e = _em.CreateEntity();
            _em.AddComponentData(e, LocalTransform.FromPosition(pos));
            _em.AddComponentData(e, new FactionTag { value = Faction.DefenderUnit });
            _em.AddComponentData(e, new Health { value = 500f, max = 500f });
            _em.AddBuffer<IncomingDamage>(e);
            _em.AddComponent<DefenderUnitTag>(e);
            return e;
        }

        private void Stun(Entity e, float seconds)
        {
            var buf = _em.GetBuffer<CcEffect>(e);
            buf.Add(new CcEffect { kind = CcKind.Stun, remainingTime = seconds });
        }

        // ───────── ① Nearest 도 락을 유지한다 ─────────

        [Test]
        public void NearestEnemy_KeepsLock_WhenACloserDefenderAppears()
        {
            // 이것이 unit 3 의 본체다. 예전엔 매 프레임 최근접을 다시 골라
            // 더 가까운 적이 나타나면 즉시 갈아탔다.
            var enemy = CreateNearestEnemy(new float3(0f, 0f, 0f));
            var first = CreateDefender(new float3(2f, 0f, 0f));

            Tick();
            Assert.AreEqual(first, _em.GetComponentData<FocusTarget>(enemy).current, "첫 대상을 잠근다");

            CreateDefender(new float3(1f, 0f, 0f));   // 더 가까운 후보 등장
            for (int i = 0; i < 10; i++) Tick();

            Assert.AreEqual(first, _em.GetComponentData<FocusTarget>(enemy).current,
                "더 가까운 대상이 나타나도 갈아타지 않는다 — 원칙 2");
        }

        [Test]
        public void NearestEnemy_ReleasesLock_WhenTargetLeavesRange()
        {
            // 락이 «영원»이 아니라는 음성 대조군. 이게 없으면 위 테스트는
            // "그냥 안 바뀐다"와 구분되지 않는다.
            var enemy = CreateNearestEnemy(new float3(0f, 0f, 0f));
            var locked = CreateDefender(new float3(1f, 0f, 0f));
            var other = CreateDefender(new float3(2f, 0f, 0f));

            Tick();
            Assert.AreEqual(locked, _em.GetComponentData<FocusTarget>(enemy).current);

            _em.SetComponentData(locked, LocalTransform.FromPosition(new float3(12f, 0f, 0f)));
            for (int i = 0; i < 10; i++) Tick();

            Assert.AreEqual(other, _em.GetComponentData<FocusTarget>(enemy).current,
                "사거리 이탈은 해제 사유다(D2)");
        }

        // ───────── ② 보스도 같다 (D4) ─────────

        [Test]
        public void Boss_KeepsLock_LikeAnyOtherNearestEnemy()
        {
            // "보스는 한 놈 타겟되면 한 놈만 팬다"(사용자 2026-08-10).
            // BossTag 분기를 넣지 **않은 것**이 구현이므로, 그 부재를 여기서 고정한다.
            var boss = CreateNearestEnemy(new float3(0f, 0f, 0f), boss: true);
            var first = CreateDefender(new float3(2f, 0f, 0f));

            Tick();
            Assert.AreEqual(first, _em.GetComponentData<FocusTarget>(boss).current);

            CreateDefender(new float3(1f, 0f, 0f));
            for (int i = 0; i < 10; i++) Tick();

            Assert.AreEqual(first, _em.GetComponentData<FocusTarget>(boss).current,
                "보스도 예외가 아니다");
        }

        // ───────── ③④ CC 해제 재선정 (D5) ─────────

        [Test]
        public void Cc_ClearsTheLock_WhileActionLocked()
        {
            var enemy = CreateNearestEnemy(new float3(0f, 0f, 0f));
            CreateDefender(new float3(2f, 0f, 0f));

            Tick();
            Assert.AreNotEqual(Entity.Null, _em.GetComponentData<FocusTarget>(enemy).current, "먼저 잠긴다");

            Stun(enemy, 1f);
            Tick();

            Assert.AreEqual(Entity.Null, _em.GetComponentData<FocusTarget>(enemy).current,
                "행동정지 중엔 락이 비어 있다 — 깨어날 때 새로 고르기 위한 상태");
        }

        [Test]
        public void AfterCcEnds_ThePickIsMadeFresh_NotResumedFromTheOldLock()
        {
            // D5 의 목적 그 자체: 자는 동안 세상이 바뀌었으면 깨어나서 다시 본다.
            var enemy = CreateNearestEnemy(new float3(0f, 0f, 0f));
            var far = CreateDefender(new float3(3f, 0f, 0f));

            Tick();
            Assert.AreEqual(far, _em.GetComponentData<FocusTarget>(enemy).current, "처음엔 이쪽뿐이라 잠긴다");

            Stun(enemy, 0.05f);
            Tick();
            Assert.AreEqual(Entity.Null, _em.GetComponentData<FocusTarget>(enemy).current, "자는 동안엔 비어 있다");

            var near = CreateDefender(new float3(1f, 0f, 0f));   // 자는 동안 더 가까운 대상 등장

            // CC 만료를 **직접 시뮬**한다 — 지속시간을 깎는 시스템(Effects)은 이 월드에
            // 없다. 여기서 검증할 것은 «CC 가 풀린 뒤의 선택»이지 «CC 가 제때 풀리는가»가
            // 아니다. 후자는 그 시스템의 테스트 소관이다.
            _em.GetBuffer<CcEffect>(enemy).Clear();
            for (int i = 0; i < 5; i++) Tick();

            Assert.AreEqual(near, _em.GetComponentData<FocusTarget>(enemy).current,
                "깨어나면 옛 락을 이어받지 않고 새로 고른다");
        }

        // ───────── ⑤ committedTarget 과 층이 다르다 ─────────

        [Test]
        public void CcDuringWindup_DoesNotStealTheCommittedTarget()
        {
            // 스윙 도중 기절해도 그 한 방은 겨눈 대상에 꽂힌다(unit 0 계약 유지).
            // D5 가 비우는 것은 «다음 공격을 위한 락»이지 «진행 중 스윙의 커밋»이 아니다.
            var enemy = CreateNearestEnemy(new float3(0f, 0f, 0f), hitDelay: 0.3f);
            var target = CreateDefender(new float3(2f, 0f, 0f));

            Tick();   // START — 커밋 성립
            var committed = _em.GetComponentData<AttackState>(enemy).committedTarget;
            Assert.AreEqual(target, committed, "wind-up 시작 시 커밋된다");

            Stun(enemy, 1f);
            Tick();

            Assert.AreEqual(Entity.Null, _em.GetComponentData<FocusTarget>(enemy).current, "락은 비었다");
            Assert.AreEqual(committed, _em.GetComponentData<AttackState>(enemy).committedTarget,
                "커밋은 살아 있다 — 진행 중 스윙은 겨눈 대상에 꽂힌다");
        }

        // ───────── ⑥ 미러가 같은 게이트를 쓴다 ─────────

        [Test]
        public void Mirror_DoesNotFallToMarching_WhileTheLockIsValid()
        {
            // 계약 4 — AttackSystem 과 EnemyAiStateSystem 의 게이트가 갈리면
            // "락은 있는데 FSM 은 Marching" 데드락이 재발한다(B2 의 절반).
            var enemy = CreateNearestEnemy(new float3(0f, 0f, 0f));
            CreateDefender(new float3(1f, 0f, 0f));

            for (int i = 0; i < 5; i++) Tick();

            Assert.AreNotEqual(Entity.Null, _em.GetComponentData<FocusTarget>(enemy).current);
            Assert.AreEqual(AiState.Engaging, _em.GetComponentData<EnemyAiState>(enemy).value,
                "락이 유효하면 교전 상태여야 한다");
        }
    }
}
