using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Wassup.Battle.Combat;
using Wassup.Battle.Effects;
using Wassup.Battle.Movement;
using Wassup.Battle.Units;

namespace Wassup.Tests.EditMode
{
    // enemy-detection-range unit 2·4·5 — `DetectionSystem` 의 계약 고정.
    //
    // 이 시스템이 소유한 판정은 「이 적이 지금 방어유닛을 발견했나」 하나이고, 그 답이
    // `MovementSystem` 의 사냥 게이트를 연다. 여기서 잡는 것은 **계약이지 산술이 아니다** —
    // 반경 판정 자체는 `AttackReach`(별도 테스트)가 소유한다.
    //
    // 고정하는 계약:
    //   3  감지 판정은 `AttackReach.InReach` 하나 — 반경만 `detectionRange` 로 바뀐다
    //   4  감지 후보는 「때릴 수 있는」 방어유닛뿐(targetMask · 통행층 · classMask)
    //   11 `AttackState` 없는 적은 감지하지 않는다(fail-closed)
    //   12 무제한(`< 0`)은 **반경 판정만** 건너뛴다 — legal 필터는 그대로 지난다
    //   2  어그로가 감지를 이긴다
    //   +  관성(grace) · 히스테리시스 · 막힘 해제 · 발견 사건 1회
    public class DetectionSystemTests
    {
        private const int W = 40;
        private const int H = 10;
        private const float Dt = 1f / 60f;

        private World _world;
        private EntityManager _em;
        private SimulationSystemGroup _simGroup;
        private Entity _fieldEntity;
        private FlowFieldSingleton _field;
        private NativeQueue<DetectionEvent> _eventQueue;

        [SetUp]
        public void SetUp()
        {
            _world = new World("DetectionSystemTestWorld");
            _em = _world.EntityManager;
            _simGroup = _world.CreateSystemManaged<SimulationSystemGroup>();
            _simGroup.AddSystemToUpdateList(_world.CreateSystem<DetectionSystem>());

            int n = W * H;
            var walkMask = new NativeArray<byte>(n, Allocator.Persistent);
            for (int i = 0; i < n; i++) walkMask[i] = 1;

            // 감지는 흐름장을 안 읽는다 — `tileSize` 하나만 쓴다(월드→타일 환산).
            _field = new FlowFieldSingleton
            {
                flow = new NativeArray<float2>(n, Allocator.Persistent),
                dist = new NativeArray<int>(n, Allocator.Persistent),
                walkMask = walkMask,
                gridSize = new int2(W, H),
                tileSize = 1f,
                origin = float3.zero,
            };

            _eventQueue = new NativeQueue<DetectionEvent>(Allocator.Persistent);

            _fieldEntity = _em.CreateEntity();
            _em.AddComponentData(_fieldEntity, _field);
            _em.AddComponentData(_fieldEntity, new DetectionEventsSingleton { queue = _eventQueue });
        }

        [TearDown]
        public void TearDown()
        {
            _field.flow.Dispose();
            _field.dist.Dispose();
            _field.walkMask.Dispose();
            if (_eventQueue.IsCreated) _eventQueue.Dispose();
            if (_world != null && _world.IsCreated) _world.Dispose();
        }

        private void Step(int frames = 1)
        {
            for (int i = 0; i < frames; i++)
            {
                _world.SetTime(new Unity.Core.TimeData(_world.Time.ElapsedTime + Dt, Dt));
                _simGroup.Update();
            }
        }

        // 몸 반경을 0 으로 둔다 — 「간격 = 중심 거리」가 되어 테스트가 산술이 아니라 계약을 읽는다.
        private Entity MakeDefender(float x, int simId = 1,
                                    Wassup.Data.DefenderClass cls = Wassup.Data.DefenderClass.None)
        {
            var e = _em.CreateEntity();
            _em.AddComponentData(e, new Health { value = 100f, max = 100f });
            _em.AddComponentData(e, new FactionTag { value = Faction.DefenderUnit });
            _em.AddComponentData(e, LocalTransform.FromPosition(new float3(x, 0f, 0.5f)));
            _em.AddComponentData(e, new HitRadius { value = 0f });
            _em.AddComponentData(e, new SimEntityId { value = simId });
            if (cls != Wassup.Data.DefenderClass.None)
                _em.AddComponentData(e, new DefenderClassTag { value = cls });
            return e;
        }

        private Entity MakeEnemy(float x, float detectionRange,
                                 bool withAttack = true,
                                 int targetMask = (int)Faction.DefenderUnit,
                                 int classMask = -1)
        {
            var e = _em.CreateEntity();
            _em.AddComponentData(e, new Health { value = 50f, max = 50f });
            _em.AddComponentData(e, new FactionTag { value = Faction.EnemyUnit });
            _em.AddComponentData(e, LocalTransform.FromPosition(new float3(x, 0f, 0.5f)));
            _em.AddComponentData(e, new HitRadius { value = 0f });
            _em.AddComponentData(e, new SimEntityId { value = 100 });
            _em.AddComponentData(e, new EnemyAiState { value = AiState.Marching });
            _em.AddComponentData(e, new DetectionRange { tiles = detectionRange });
            _em.AddComponentData(e, new DetectedTarget { target = Entity.Null });
            _em.AddComponentData(e, new EnemyTargetFilter { classMask = classMask, priorityClass = -1, factionMask = targetMask });
            if (withAttack)
                _em.AddComponentData(e, new AttackState { range = 1f, targetMask = targetMask });
            return e;
        }

        private DetectedTarget Detected(Entity e) => _em.GetComponentData<DetectedTarget>(e);

        // ── unit 2 · 획득 ────────────────────────────────────────────────────────

        [Test]
        public void 반경_안의_방어유닛을_발견한다()
        {
            var d = MakeDefender(2f);
            var e = MakeEnemy(0f, detectionRange: 3f);
            Step();

            var got = Detected(e);
            Assert.AreEqual(1, got.hunting, "간격 2칸 < 반경 3칸인데 감지가 안 걸렸다");
            Assert.AreEqual(d, got.target, "발견한 대상이 그 방어유닛이어야 한다");
        }

        [Test]
        public void 반경_밖의_방어유닛은_발견하지_않는다()
        {
            MakeDefender(4f);
            var e = MakeEnemy(0f, detectionRange: 3f);
            Step();
            Assert.AreEqual(0, Detected(e).hunting, "간격 4칸 > 반경 3칸인데 감지가 걸렸다");
        }

        // 계약 12 — 무제한은 **반경 판정만** 건너뛴다.
        [Test]
        public void 무제한_감지는_반경을_안_본다()
        {
            MakeDefender(20f);
            var e = MakeEnemy(0f, detectionRange: -1f);
            Step();
            Assert.AreEqual(1, Detected(e).hunting, "무제한인데 20칸 밖을 못 봤다");
        }

        // 계약 12 의 나머지 절반 — 무제한이어도 legal 필터는 지난다.
        [Test]
        public void 무제한이어도_마스크_밖은_발견하지_않는다()
        {
            MakeDefender(5f);
            var e = MakeEnemy(0f, detectionRange: -1f, targetMask: (int)Faction.DefenderCore);
            Step();
            Assert.AreEqual(0, Detected(e).hunting,
                "무제한은 «반경만» 건너뛴다 — targetMask 는 그대로 지나야 한다(계약 12)");
        }

        // 계약 4 — 못 때리는 방어유닛은 후보가 아니다.
        [Test]
        public void 클래스_마스크_밖은_발견하지_않는다()
        {
            MakeDefender(2f, cls: Wassup.Data.DefenderClass.Guardian);
            int rangerOnly = 1 << (int)Wassup.Data.DefenderClass.Ranger;
            var e = MakeEnemy(0f, detectionRange: 3f, classMask: rangerOnly);
            Step();
            Assert.AreEqual(0, Detected(e).hunting, "classMask 밖 방어유닛을 발견했다(계약 4)");
        }

        // 계약 11 — 무기 없이 구워진 적은 감지하지 않는다.
        [Test]
        public void AttackState_없는_적은_감지하지_않는다()
        {
            MakeDefender(2f);
            var e = MakeEnemy(0f, detectionRange: 3f, withAttack: false);
            Step();
            Assert.AreEqual(0, Detected(e).hunting, "무기 없는 적이 감지했다(계약 11 fail-closed)");
        }

        // 계약 2 — 어그로가 감지를 이긴다.
        [Test]
        public void 어그로된_적은_감지하지_않는다()
        {
            var guardian = MakeDefender(2f);
            var e = MakeEnemy(0f, detectionRange: 3f);
            _em.AddComponentData(e, new Aggroed { guardian = guardian, remainingTime = 0f });
            Step();
            Assert.AreEqual(0, Detected(e).hunting, "어그로 중인데 감지가 걸렸다(계약 2)");
        }

        [Test]
        public void 동거리_후보는_낮은_simId_가_뽑힌다()
        {
            var far = MakeDefender(2f, simId: 9);
            var near = MakeDefender(-2f, simId: 3);   // 같은 거리, 낮은 simId
            var e = MakeEnemy(0f, detectionRange: 3f);
            Step();
            Assert.AreEqual(near, Detected(e).target, "동거리는 낮은 simId 가 이겨야 한다(결정론)");
            Assert.AreNotEqual(far, Detected(e).target);
        }

        // ── unit 4 · 유지와 관성 ─────────────────────────────────────────────────

        [Test]
        public void 대상이_죽으면_1초간_사냥을_유지한다()
        {
            var d = MakeDefender(2f);
            var e = MakeEnemy(0f, detectionRange: 3f);
            Step();
            Assert.AreEqual(1, Detected(e).hunting);

            _em.DestroyEntity(d);
            Step();
            Assert.AreEqual(1, Detected(e).hunting, "대상 사망 직후에도 관성으로 사냥을 유지해야 한다");
            Assert.AreEqual(Entity.Null, Detected(e).target, "대상은 비워야 한다");

            Step(70);   // > 1초
            Assert.AreEqual(0, Detected(e).hunting, "관성이 만료되면 사냥을 놓아야 한다");
        }

        [Test]
        public void 관성_중_새_후보가_생기면_즉시_채택한다()
        {
            var d = MakeDefender(2f);
            var e = MakeEnemy(0f, detectionRange: 3f);
            Step();
            _em.DestroyEntity(d);
            Step(10);   // 관성 진행 중
            Assert.AreEqual(1, Detected(e).hunting);

            var d2 = MakeDefender(-2f, simId: 5);
            Step();
            Assert.AreEqual(d2, Detected(e).target, "관성 중 새 후보를 즉시 채택해야 한다");
            Assert.AreEqual(0f, Detected(e).graceRemaining, 1e-4f, "채택하면 관성이 꺼져야 한다");
        }

        // 이미 문 대상은 더 가까운 후보가 생겨도 안 바뀐다 — 아니면 둘 사이에서 대상이 튄다.
        [Test]
        public void 유지_중에는_더_가까운_후보로_갈아타지_않는다()
        {
            var far = MakeDefender(2.5f, simId: 1);
            var e = MakeEnemy(0f, detectionRange: 3f);
            Step();
            Assert.AreEqual(far, Detected(e).target);

            MakeDefender(1f, simId: 2);   // 더 가깝다
            Step();
            Assert.AreEqual(far, Detected(e).target, "유지 임계 안이면 대상이 바뀌면 안 된다");
        }

        // ── unit 4 · 막힘 해제 ───────────────────────────────────────────────────

        [Test]
        public void 사냥_중_2초간_못_움직이면_감지를_놓는다()
        {
            MakeDefender(2f);
            var e = MakeEnemy(0f, detectionRange: 3f);
            // Movement 소유 값(RO) — 「자기주도 변위가 0 이었다」
            _em.AddComponentData(e, new PathFollowState { speed = 1f, radius = 0.25f, holdingGround = 1 });
            Step();
            Assert.AreEqual(1, Detected(e).hunting, "막히기 전에는 감지가 서 있어야 한다");

            Step(130);   // > 2초
            Assert.AreEqual(0, Detected(e).hunting, "2초간 못 갔으면 감지를 놓아야 한다");
            Assert.Greater(Detected(e).suppressRemaining, 0f, "해제 뒤 재감지를 억제해야 한다");
        }

        // ★ 리뷰 H1 회귀 가드 — `holdingGround` 는 **「CC 잠금」도 함께 접는다**(그 필드 문서가
        // 직접 열거한다). 그것만 보면 자장가(`Card_ShieldLull` 지속 2.5초 > 임계 2초) 한 번에
        // 감지가 풀리고 5초간 억제된다 = **플레이어가 CC 를 쓸수록 적이 사냥을 그만둔다.**
        [Test]
        public void 행동정지_CC_중에는_막힘이_누적되지_않는다()
        {
            MakeDefender(2f);
            var e = MakeEnemy(0f, detectionRange: 3f);
            _em.AddComponentData(e, new PathFollowState { speed = 1f, radius = 0.25f, holdingGround = 1 });
            var cc = _em.AddBuffer<CcEffect>(e);
            cc.Add(new CcEffect { kind = CcKind.Sleep, remainingTime = 5f });

            Step(200);   // > 2초(임계) — CC 가 아니었다면 진작 풀렸어야 한다
            Assert.AreEqual(1, Detected(e).hunting,
                "행동정지 CC 는 «막힘» 이 아니라 «묶임» 이다 — 막힘 해제가 걸리면 안 된다");
        }

        // ★ 리뷰 H2 회귀 가드 — 무제한 사냥(보스·보너스)은 「전멸시켜야 골에 간다」가 저작된
        // 성질이다. 타이머가 그것을 취소하면 감지가 패배 통로의 조절기가 된다.
        [Test]
        public void 무제한_감지는_막힘_해제에서_면제된다()
        {
            MakeDefender(2f);
            var e = MakeEnemy(0f, detectionRange: -1f);
            _em.AddComponentData(e, new PathFollowState { speed = 1f, radius = 0.25f, holdingGround = 1 });

            Step(200);   // > 2초
            Assert.AreEqual(1, Detected(e).hunting, "무제한 사냥은 막힘 해제 대상이 아니다");
            Assert.AreEqual(0f, Detected(e).suppressRemaining, 1e-4f, "억제도 걸리면 안 된다");
        }

        [Test]
        public void 움직이고_있으면_막힘이_누적되지_않는다()
        {
            MakeDefender(2f);
            var e = MakeEnemy(0f, detectionRange: 3f);
            _em.AddComponentData(e, new PathFollowState { speed = 1f, radius = 0.25f, holdingGround = 0 });
            Step(130);
            Assert.AreEqual(1, Detected(e).hunting, "이동 중인데 막힘 해제가 걸렸다");
        }

        // ── unit 5 · 발견 사건 ───────────────────────────────────────────────────

        [Test]
        public void 발견_사건은_전이에서_한_번만_난다()
        {
            MakeDefender(2f);
            var e = MakeEnemy(0f, detectionRange: 3f);
            Step();
            Assert.AreEqual(1, _eventQueue.Count, "0→1 전이 프레임에 1건이어야 한다");

            Step(30);   // 계속 사냥 중
            Assert.AreEqual(1, _eventQueue.Count, "유지 프레임에는 추가 사건이 없어야 한다");

            var ev = _eventQueue.Dequeue();
            Assert.AreEqual(100, ev.enemySimId);
            Assert.AreEqual(1, ev.targetSimId, "트레이스용 대상 id 가 실려야 한다");
        }

        [Test]
        public void 관성을_거쳐_다시_물어도_사건이_늘지_않는다()
        {
            var d = MakeDefender(2f);
            var e = MakeEnemy(0f, detectionRange: 3f);
            Step();
            Assert.AreEqual(1, _eventQueue.Count);

            _em.DestroyEntity(d);
            Step(10);
            MakeDefender(-2f, simId: 5);
            Step(10);
            Assert.AreEqual(1, _eventQueue.Count,
                "관성 중에는 hunting 이 1로 유지돼 전이가 없다 — 연속 사냥은 표식 1회");
        }
    }
}
