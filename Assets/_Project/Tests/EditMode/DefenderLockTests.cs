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
    // target-persistence unit 4 — 방어유닛 지속 락 (원칙 1의 본체).
    //
    // 방어유닛은 고정인데 적이 흘러가므로 최근접이 매 순간 바뀐다. unit 3(적 락)이
    // `Halt` 적에겐 거의 무효였던 것과 정반대로, 여기가 락이 실제로 일하는 자리다.
    public class DefenderLockTests
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
            _world = new World("DefenderLockTests");
            _em = _world.EntityManager;
            _simGroup = _world.CreateSystemManaged<SimulationSystemGroup>();
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

        // 평범한 방어유닛 — 제외 4종 어디에도 안 걸린다.
        private Entity CreateDefender(float3 pos, float range = 3f, float hitDelay = 0f)
        {
            var e = _em.CreateEntity();
            _em.AddComponentData(e, LocalTransform.FromPosition(pos));
            _em.AddComponentData(e, new FactionTag { value = Faction.DefenderUnit });
            _em.AddComponentData(e, new Health { value = 500f, max = 500f });
            _em.AddBuffer<IncomingDamage>(e);
            _em.AddBuffer<CcEffect>(e);
            _em.AddComponent<DefenderUnitTag>(e);
            _em.AddComponentData(e, new AttackState
            {
                range = range, cooldownDuration = 0.05f, cooldownRemaining = 0f,
                attackTargetCount = 1, targetMask = (int)Faction.EnemyUnit, hitDelaySec = hitDelay,
            });
            _em.AddComponentData(e, new FocusTarget { current = Entity.Null });
            var outs = _em.AddBuffer<AttackOutputElement>(e);
            outs.Add(new AttackOutputElement
            {
                value = new AttackOutput { kind = AttackOutputKind.Damage, magnitude = 4f },
            });
            return e;
        }

        private Entity CreateEnemy(float3 pos)
        {
            var e = _em.CreateEntity();
            _em.AddComponentData(e, LocalTransform.FromPosition(pos));
            _em.AddComponentData(e, new FactionTag { value = Faction.EnemyUnit });
            _em.AddComponentData(e, new Health { value = 500f, max = 500f });
            _em.AddBuffer<IncomingDamage>(e);
            return e;
        }

        private void Stun(Entity e, float seconds)
        {
            _em.GetBuffer<CcEffect>(e).Add(new CcEffect { kind = CcKind.Stun, remainingTime = seconds });
        }

        private Entity LockOf(Entity e) => _em.GetComponentData<FocusTarget>(e).current;


        // ───────── ① 원칙 1 그 자체 ─────────

        [Test]
        public void Defender_KeepsLock_WhenACloserEnemyArrives()
        {
            // 증상 단언. 예전엔 매 프레임 최근접을 다시 골라 더 가까운 적이 오면 즉시 갈아탔다.
            var def = CreateDefender(new float3(0f, 0f, 0f));
            var first = CreateEnemy(new float3(2f, 0f, 0f));

            Tick();
            Assert.AreEqual(first, LockOf(def), "첫 대상을 잠근다");

            CreateEnemy(new float3(1f, 0f, 0f));   // 더 가까운 적이 흘러 들어온다
            for (int i = 0; i < 10; i++) Tick();

            Assert.AreEqual(first, LockOf(def), "더 가까운 적이 와도 갈아타지 않는다 — 원칙 1");
        }

        [Test]
        public void Defender_ReleasesLock_WhenTargetLeavesRange()
        {
            // 음성 대조군 — 없으면 위 테스트는 "그냥 안 바뀐다"와 구분되지 않는다.
            var def = CreateDefender(new float3(0f, 0f, 0f));
            var locked = CreateEnemy(new float3(1f, 0f, 0f));
            var other = CreateEnemy(new float3(2f, 0f, 0f));

            Tick();
            Assert.AreEqual(locked, LockOf(def));

            _em.SetComponentData(locked, LocalTransform.FromPosition(new float3(12f, 0f, 0f)));
            for (int i = 0; i < 10; i++) Tick();

            Assert.AreEqual(other, LockOf(def), "사거리 이탈은 해제 사유다");
        }

        [Test]
        public void Defender_ReleasesLock_WhenTargetDies()
        {
            var def = CreateDefender(new float3(0f, 0f, 0f));
            var locked = CreateEnemy(new float3(1f, 0f, 0f));
            var other = CreateEnemy(new float3(2f, 0f, 0f));

            Tick();
            Assert.AreEqual(locked, LockOf(def));

            _em.SetComponentData(locked, new Health { value = 0f, max = 500f });
            _em.AddComponent<DeadTag>(locked);
            for (int i = 0; i < 10; i++) Tick();

            Assert.AreEqual(other, LockOf(def), "사망은 해제 사유다");
        }

        // ───────── ② CC 해제 재선정 (D5 균일 적용) ─────────

        [Test]
        public void Cc_ClearsTheDefenderLock_AndTheNextPickIsFresh()
        {
            var def = CreateDefender(new float3(0f, 0f, 0f));
            var far = CreateEnemy(new float3(3f, 0f, 0f));

            Tick();
            Assert.AreEqual(far, LockOf(def), "처음엔 이쪽뿐이라 잠긴다");

            Stun(def, 1f);
            Tick();
            Assert.AreEqual(Entity.Null, LockOf(def), "자는 동안엔 비어 있다");

            var near = CreateEnemy(new float3(1f, 0f, 0f));   // 자는 동안 더 가까운 적 등장

            // CC 만료 직접 시뮬 — 지속시간을 깎는 시스템(Effects)은 이 월드에 없다.
            // 여기서 볼 것은 «풀린 뒤의 선택»이지 «제때 풀리는가»가 아니다.
            _em.GetBuffer<CcEffect>(def).Clear();
            for (int i = 0; i < 5; i++) Tick();

            Assert.AreEqual(near, LockOf(def), "깨어나면 옛 락을 이어받지 않고 새로 고른다");
        }

        // ───────── ③ unit 0 과의 층 분리 (순서 회귀) ─────────

        [Test]
        public void DuringWindup_TheCommittedTargetWins_OverTheLock()
        {
            // ⚠ 순서 회귀 가드. unit 4 블록이 unit 0 커밋 **뒤**로 가면 이 테스트가 빨개진다.
            // 그 배치는 "A 겨누고 B 때림"(B1)의 부분 부활이다.
            var def = CreateDefender(new float3(0f, 0f, 0f), hitDelay: 0.3f);
            var target = CreateEnemy(new float3(2f, 0f, 0f));

            Tick();   // START — 커밋 성립
            var committed = _em.GetComponentData<AttackState>(def).committedTarget;
            Assert.AreEqual(target, committed, "wind-up 시작 시 커밋된다");

            // wind-up 도중 더 가까운 적이 들어온다
            CreateEnemy(new float3(1f, 0f, 0f));
            Tick();

            Assert.AreEqual(committed, _em.GetComponentData<AttackState>(def).committedTarget,
                "진행 중 스윙의 커밋은 락이 밀어내지 못한다");
        }

        // ───────── ④ 제외 4종 (계약 2) ─────────

        [Test]
        public void Healer_DoesNotLock_LowestHealthRerankingIsItsIdentity()
        {
            // 힐러 = targetAllies → targetMask == DefenderUnit. 매 순간 가장 아픈 아군을 다시 고른다.
            var healer = CreateDefender(new float3(0f, 0f, 0f));
            var st = _em.GetComponentData<AttackState>(healer);
            st.targetMask = (int)Faction.DefenderUnit;
            _em.SetComponentData(healer, st);

            var hurt = CreateDefender(new float3(2f, 0f, 0f));
            _em.SetComponentData(hurt, new Health { value = 100f, max = 500f });

            for (int i = 0; i < 5; i++) Tick();

            Assert.AreEqual(Entity.Null, LockOf(healer),
                "힐러는 락을 받지 않는다 — 제외는 누락이 아니라 계약이다");
        }

        [Test]
        public void Guardian_DoesNotLock_TheAggroMagnetNeedsFreshPicks()
        {
            // D1 — 어그로 자석이 "아직 어그로 안 걸린 적 우선"으로 신규 팩을 흡수한다.
            var guardian = CreateDefender(new float3(0f, 0f, 0f));
            _em.AddComponentData(guardian, new AggroCapacity { max = 3, held = 0 });
            CreateEnemy(new float3(1f, 0f, 0f));

            for (int i = 0; i < 5; i++) Tick();

            Assert.AreEqual(Entity.Null, LockOf(guardian), "가디언은 락을 받지 않는다 — D1");
        }

        [Test]
        public void FrontmostCardHolder_DoesNotLock_TheCardPromisesFreshFrontmostEachAttack()
        {
            // 「끝을 보는 눈」 카드 계약이 "매 공격마다 지금의 최전방"이다. 지속 락과 정면 충돌.
            // wantFrontmost 는 락 컴포넌트 **+ 살아있는 슬롯** 둘 다 요구하므로 둘 다 준다.
            var def = CreateDefender(new float3(0f, 0f, 0f));
            _em.AddComponentData(def, new FrontmostAttackLock
            {
                active = false, target = Entity.Null, damageMulSnapshot = 1f, targetIsPriority = false,
            });
            var mods = _em.AddBuffer<DcAttackModSlot>(def);
            mods.Add(new DcAttackModSlot { kind = DcAttackModKind.FrontmostTarget, damageMul = 1.2f });
            CreateEnemy(new float3(1f, 0f, 0f));

            for (int i = 0; i < 5; i++) Tick();

            Assert.AreEqual(Entity.Null, LockOf(def),
                "frontmost 카드 보유자는 지속 락을 받지 않는다 — 제외는 계약이다");
        }

        [Test]
        public void DefenderWithEnemyBehavior_IsLeftToTheEnemyBlock_NotDoubleLocked()
        {
            // 순찰병은 방어유닛이면서 적 AI 스택(EnemyBehavior)을 물려받는다. unit 3 블록이
            // 이미 처리하므로 unit 4 블록은 `!EnemyBehavior` 로 비켜준다.
            //
            // 관측 방법: targetMode = None 을 준다. 그러면 **적 블록도** 게이트에서 걸러지므로
            // (targetMode != None 요구) 락이 아무 데서도 안 걸려야 한다. 누가 unit 4 의
            // `!EnemyBehavior` 게이트를 지우면 여기서 락이 생겨 빨개진다.
            var def = CreateDefender(new float3(0f, 0f, 0f));
            _em.AddComponentData(def, new EnemyBehavior
            {
                targetMode = EnemyTargetMode.None,
                engageMovement = EngageMovement.Halt,
            });
            CreateEnemy(new float3(1f, 0f, 0f));

            for (int i = 0; i < 5; i++) Tick();

            Assert.AreEqual(Entity.Null, LockOf(def),
                "EnemyBehavior 보유 유닛은 unit 4 블록이 건드리지 않는다 (이중 잠금 방지)");
        }

        // ───────── ⑤ 계약 6 — 락은 targetMask 를 다시 본다 ─────────

        [Test]
        public void LockReleases_WhenTheTargetLeavesTheMask()
        {
            // 락 이전엔 bestTarget 이 매 프레임 마스크로 재선정돼 이 상황이 불가능했다.
            // 락이 그 재선정을 건너뛰므로, 런타임 마스크 변경(도발 해제의 previousTargetMask
            // 원복)이 반영되지 않으면 **마스크 밖 대상을 계속 때린다**.
            var def = CreateDefender(new float3(0f, 0f, 0f));
            var foe = CreateEnemy(new float3(1f, 0f, 0f));

            Tick();
            Assert.AreEqual(foe, LockOf(def), "먼저 잠근다");

            // 마스크에서 EnemyUnit 을 뺀다 — 이제 이 대상은 조준 대상이 아니다.
            var st = _em.GetComponentData<AttackState>(def);
            st.targetMask = (int)Faction.BlockingHazard;
            _em.SetComponentData(def, st);
            for (int i = 0; i < 5; i++) Tick();

            Assert.AreEqual(Entity.Null, LockOf(def),
                "마스크 밖으로 나간 대상은 놓는다 — 계약 6");
        }

        [Test]
        public void FacingUnit_DoesNotLock_LaneWitnessIsAFireGateNotATarget()
        {
            var facing = CreateDefender(new float3(0f, 0f, 0f));
            _em.AddComponentData(facing, new DeployedFacing { value = new int2(1, 0) });
            CreateEnemy(new float3(1f, 0f, 0f));

            for (int i = 0; i < 5; i++) Tick();

            Assert.AreEqual(Entity.Null, LockOf(facing), "방향 유닛은 락을 받지 않는다");
        }
    }
}
