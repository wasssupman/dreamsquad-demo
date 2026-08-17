using NUnit.Framework;
using Unity.Core;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Wassup.Battle.Combat.Projectile;
using Wassup.Battle.Effects;
using Wassup.Battle.Units;

namespace Wassup.Tests.EditMode
{
    // dreamcatcher-content-4 unit 2 — 관통 페이로드의 **재타격 쿨타임** 축.
    //
    // 두 가지를 고정한다:
    //   ① 판정(PathHitRecord.CanHit) — 순수 케이스 4종
    //   ② 그 판정이 실제 히트 루프에 붙었을 때의 동작. 특히 **무회귀**:
    //      rehitCooldownSec 0 인 기존 방향탄(샷건너·머신거너)은 한 글자도 달라지면 안 된다.
    //
    // ⚠ 이 unit 은 기록 쓰기를 **ECB → RW 버퍼 직접 쓰기**로 옮겼다(ECB 에는 원소 수정
    // 오퍼레이션이 없다). ECB append 는 플레이백까지 지연돼 «방금 추가한 기록이 같은
    // 프레임엔 안 보이는» 성질이 있었고 직접 쓰기는 즉시 보인다 — 그래서 **한 프레임에
    // 여러 victim 을 스치는 관통탄**이 이 전환의 유일한 회귀 표면이다. 아래
    // MultiVictimFrame 테스트가 그 동등성(같은 피해자 수·같은 front-most 순서·같은 예산
    // 소모)을 명시적으로 덮는다.
    public class PathHitRehitCooldownTests
    {
        private World _world;
        private EntityManager _em;
        private SimulationSystemGroup _simGroup;

        [SetUp]
        public void SetUp()
        {
            _world = new World("PathHitRehitCooldownTests");
            _em = _world.EntityManager;
            _simGroup = _world.CreateSystemManaged<SimulationSystemGroup>();
            _simGroup.AddSystemToUpdateList(_world.CreateSystem<ProjectileMoveSystem>());
            _simGroup.AddSystemToUpdateList(_world.CreateSystem<ProjectileHitSystem>());

            _em.AddComponentData(_em.CreateEntity(), new FlowFieldSingleton
            {
                tileSize = 1f,
                gridSize = new int2(64, 64),
                origin = float3.zero,
            });
        }

        [TearDown]
        public void TearDown() => _world?.Dispose();

        private void Tick(float dt = 0.1f)
        {
            _world.SetTime(new TimeData(_world.Time.ElapsedTime + dt, dt));
            _simGroup.Update();
        }

        private Entity CreateEnemy(float x)
        {
            var e = _em.CreateEntity();
            _em.AddComponentData(e, LocalTransform.FromPosition(new float3(x, 0f, 0f)));
            _em.AddComponentData(e, new FactionTag { value = Faction.EnemyUnit });
            _em.AddComponentData(e, new Health { value = 500f, max = 500f });
            _em.AddBuffer<IncomingDamage>(e);
            _em.AddComponent<AttackUnitTag>(e);
            return e;
        }

        // +X 로 천천히 나아가며 훑는 PathHit 탄. hitThreshold 를 크게 잡아 x=1,2,3 의 적이
        // **여러 프레임에 걸쳐 계속 스윕 안에 남게** 한다 — 기록이 없으면 매 프레임 재타격되는
        // 상황이라, 기록/쿨타임이 실제로 일하고 있는지가 그대로 드러난다.
        private Entity CreateSweeper(float rehitCooldownSec, int pierce,
                                     float maxDistance = 10f, float hitThreshold = 5f)
        {
            var e = _em.CreateEntity();
            _em.AddComponentData(e, LocalTransform.FromPosition(new float3(0.5f, 0f, 0f)));
            _em.AddComponent<ProjectileTag>(e);
            _em.AddComponentData(e, new ProjectileState
            {
                movement = MovementKind.DirectionalLinear,
                payload = PayloadKind.PathHit,
                direction = new float2(1f, 0f),
                origin = float3.zero,
                maxDistance = maxDistance,
                prevPos = new float3(0.5f, 0f, 0f),
                damage = 20f,
                speed = 1f,
                hitThreshold = hitThreshold,
                pierceRemaining = pierce,
                rehitCooldownSec = rehitCooldownSec,
            });
            // 브리지 드레인이 PathHit 스폰 시 붙이는 버퍼(BattleBridge:4703).
            _em.AddBuffer<PathHitRecord>(e);
            return e;
        }

        // 재타격 창의 시계는 투사체 자기 시계(ProjectileState.elapsed)이고, 그것을 굴리는
        // 것은 **이동 arm** 이다. 궤도 arm 은 굴리지만 이 픽스처가 쓰는 DirectionalLinear
        // arm 은 굴리지 않으므로, 테스트가 그 역할을 대신해 시계만 밀어 준다.
        private void AdvanceProjectileClock(Entity projectile, float seconds)
        {
            var state = _em.GetComponentData<ProjectileState>(projectile);
            state.elapsed += seconds;
            _em.SetComponentData(projectile, state);
        }

        private int DamageCount(Entity enemy) => _em.GetBuffer<IncomingDamage>(enemy).Length;

        // ── ① 판정 (순수 케이스) ──────────────────────────────────────────────

        [Test]
        public void CanHit_ZeroCooldown_RejectsEveryRepeatForever()
        {
            var victim = _em.CreateEntity();
            var records = _em.AddBuffer<PathHitRecord>(_em.CreateEntity());

            Assert.IsTrue(PathHitRecord.CanHit(records, victim, now: 0f, cooldown: 0f, out int index));
            Assert.AreEqual(-1, index, "미기록 피해자는 슬롯이 없다");

            records.Add(new PathHitRecord { value = victim, nextHitAt = 0f });

            Assert.IsFalse(PathHitRecord.CanHit(records, victim, now: 0f, cooldown: 0f, out index));
            Assert.AreEqual(0, index);
            Assert.IsFalse(PathHitRecord.CanHit(records, victim, now: 9999f, cooldown: 0f, out index),
                "쿨타임 0 은 시간이 아무리 흘러도 피해자당 영구 1회 — 기존 방향탄 동작");
        }

        [Test]
        public void CanHit_PositiveCooldown_OpensOnlyOnceTheWindowElapsed()
        {
            var victim = _em.CreateEntity();
            var records = _em.AddBuffer<PathHitRecord>(_em.CreateEntity());
            records.Add(new PathHitRecord { value = victim, nextHitAt = 0.5f });

            Assert.IsFalse(PathHitRecord.CanHit(records, victim, now: 0.49f, cooldown: 0.5f, out int index),
                "창이 열리기 전엔 거절");
            Assert.AreEqual(0, index, "거절이어도 갱신할 슬롯은 알려준다");

            Assert.IsTrue(PathHitRecord.CanHit(records, victim, now: 0.5f, cooldown: 0.5f, out index),
                "창 시각 정각도 허용(경계 포함)");

            // 호출부가 하는 일 — 슬롯을 **갱신**한다(추가가 아니다).
            records[index] = new PathHitRecord { value = victim, nextHitAt = 1f };

            Assert.AreEqual(1, records.Length, "갱신은 기록을 늘리지 않는다");
            Assert.IsFalse(PathHitRecord.CanHit(records, victim, now: 0.6f, cooldown: 0.5f, out index),
                "갱신 뒤에는 다음 창까지 다시 거절");
            Assert.IsTrue(PathHitRecord.CanHit(records, victim, now: 1f, cooldown: 0.5f, out index));
        }

        [Test]
        public void CanHit_UnrecordedVictim_IsAllowedRegardlessOfCooldown()
        {
            var recorded = _em.CreateEntity();
            var stranger = _em.CreateEntity();
            var records = _em.AddBuffer<PathHitRecord>(_em.CreateEntity());
            records.Add(new PathHitRecord { value = recorded, nextHitAt = 999f });

            Assert.IsTrue(PathHitRecord.CanHit(records, stranger, now: 0f, cooldown: 0f, out int index));
            Assert.AreEqual(-1, index);
            Assert.IsTrue(PathHitRecord.CanHit(records, stranger, now: 0f, cooldown: 0.5f, out index));
            Assert.AreEqual(-1, index);
        }

        // ── ② 무회귀 (rehitCooldownSec == 0 — 샷건너·머신거너 경로) ───────────

        [Test]
        public void ZeroCooldown_PierceBudget_StopsAtFrontMostVictims()
        {
            var near = CreateEnemy(1f);
            var mid = CreateEnemy(2f);
            var far = CreateEnemy(3f);
            var proj = CreateSweeper(rehitCooldownSec: 0f, pierce: 2);

            Tick();

            Assert.AreEqual(1, DamageCount(near), "가장 앞의 적부터 예산을 쓴다");
            Assert.AreEqual(1, DamageCount(mid));
            Assert.AreEqual(0, DamageCount(far), "예산 2 를 다 쓴 뒤의 적은 스윕 안에 있어도 무피해");
            Assert.IsFalse(_em.Exists(proj), "예산 소진 = 소멸(바운스 없음)");
        }

        // ECB → 직접 쓰기 전환의 **동등성** 케이스. 한 프레임에 여러 victim 을 스치는
        // 관통탄이 전과 같은 수의 피해자를 때리고 같은 순서(front-most 우선)로 예산을 쓴다.
        // 기록이 이제 같은 프레임에 즉시 보이지만, 후보는 이미 목록에서 빠졌으므로 한 프레임
        // 안에서 같은 적을 두 번 때리지 않는다.
        [Test]
        public void ZeroCooldown_MultiVictimFrame_RecordsFrontMostFirst_AndNeverRepeats()
        {
            var near = CreateEnemy(1f);
            var mid = CreateEnemy(2f);
            var far = CreateEnemy(3f);
            var proj = CreateSweeper(rehitCooldownSec: 0f, pierce: 9);

            Tick();

            Assert.AreEqual(1, DamageCount(near));
            Assert.AreEqual(1, DamageCount(mid));
            Assert.AreEqual(1, DamageCount(far));
            Assert.AreEqual(20f, _em.GetBuffer<IncomingDamage>(near)[0].amount, 1e-3f);
            Assert.AreEqual(6, _em.GetComponentData<ProjectileState>(proj).pierceRemaining,
                "3명 관통 = 예산 3 소모");

            var records = _em.GetBuffer<PathHitRecord>(proj);
            Assert.AreEqual(3, records.Length);
            Assert.AreEqual(near, records[0].value, "기록 순서 = 맞힌 순서 = front-most 우선");
            Assert.AreEqual(mid, records[1].value);
            Assert.AreEqual(far, records[2].value);

            // 다음 프레임에도 셋 다 여전히 스윕 반경 안에 있다 — 기록이 유일한 방어선이다.
            Tick();

            Assert.AreEqual(1, DamageCount(near), "기록된 피해자는 다음 프레임에도 재타격되지 않는다");
            Assert.AreEqual(1, DamageCount(mid));
            Assert.AreEqual(1, DamageCount(far));
            Assert.AreEqual(3, _em.GetBuffer<PathHitRecord>(proj).Length, "기록도 늘지 않는다");
            Assert.AreEqual(6, _em.GetComponentData<ProjectileState>(proj).pierceRemaining,
                "때리지 않았으면 예산도 그대로");
        }

        // ── ③ 재타격 (rehitCooldownSec > 0 — 궤도 화염구) ─────────────────────

        [Test]
        public void Rehit_BeforeWindowElapses_DoesNotHitAgain()
        {
            var enemy = CreateEnemy(1f);
            var proj = CreateSweeper(rehitCooldownSec: 0.5f, pierce: 1);

            Tick();
            Assert.AreEqual(1, DamageCount(enemy));
            Assert.AreEqual(0.5f, _em.GetBuffer<PathHitRecord>(proj)[0].nextHitAt, 1e-4f,
                "창은 투사체 자기 시계 기준 now + cooldown");

            Tick(); // 시계를 밀지 않았다 = 창이 아직 안 열렸다
            Assert.AreEqual(1, DamageCount(enemy), "창이 열리기 전엔 스윕 안에 있어도 무피해");
        }

        [Test]
        public void Rehit_AfterWindowElapses_HitsAgain_AndUpdatesSlotInPlace()
        {
            var enemy = CreateEnemy(1f);
            var proj = CreateSweeper(rehitCooldownSec: 0.5f, pierce: 1);

            Tick();
            Assert.AreEqual(1, DamageCount(enemy));

            AdvanceProjectileClock(proj, 0.5f);
            Tick();
            Assert.AreEqual(2, DamageCount(enemy), "창이 열리면 같은 적을 다시 때린다");
            Assert.AreEqual(1, _em.GetBuffer<PathHitRecord>(proj).Length,
                "**갱신이지 추가가 아니다** — 버퍼가 바퀴마다 자라면 안 된다");
            Assert.AreEqual(1f, _em.GetBuffer<PathHitRecord>(proj)[0].nextHitAt, 1e-4f);

            AdvanceProjectileClock(proj, 0.5f);
            Tick();
            Assert.AreEqual(3, DamageCount(enemy));
            Assert.AreEqual(1, _em.GetBuffer<PathHitRecord>(proj).Length);
            Assert.AreEqual(1.5f, _em.GetBuffer<PathHitRecord>(proj)[0].nextHitAt, 1e-4f);

            Assert.AreEqual(1, _em.GetComponentData<ProjectileState>(proj).pierceRemaining,
                "계약 3 — 재타격 탄은 관통 예산을 소모하지 않는다");
        }

        // 계약 3 의 강한 핀: 관통 예산이 0 이어도 재타격 탄은 때리고 살아남는다.
        // 예산을 게이트로 읽었다면 여기서 아무도 못 맞히고 즉시 소멸했을 것이다.
        [Test]
        public void Rehit_WithoutPierceBudget_StillHitsEveryone_AndSurvives()
        {
            var near = CreateEnemy(1f);
            var mid = CreateEnemy(2f);
            var far = CreateEnemy(3f);
            var proj = CreateSweeper(rehitCooldownSec: 0.5f, pierce: 0);

            Tick();

            Assert.IsTrue(_em.Exists(proj), "재타격 탄의 종료 조건은 수명뿐이다");
            Assert.AreEqual(1, DamageCount(near));
            Assert.AreEqual(1, DamageCount(mid));
            Assert.AreEqual(1, DamageCount(far), "예산 없이도 스친 전원을 때린다");
            Assert.AreEqual(0, _em.GetComponentData<ProjectileState>(proj).pierceRemaining,
                "예산은 읽지도 쓰지도 않는다");

            var records = _em.GetBuffer<PathHitRecord>(proj);
            Assert.AreEqual(3, records.Length);
            Assert.IsTrue(PathHitRecord.Contains(records, near));
            Assert.IsTrue(PathHitRecord.Contains(records, mid));
            Assert.IsTrue(PathHitRecord.Contains(records, far), "셋 다 각자의 재타격 창을 얻는다");
        }

        [Test]
        public void Rehit_EndsAtLifetime_ResolvingItsFinalSweep()
        {
            var enemy = CreateEnemy(1f);
            // 첫 프레임에 사거리 끝(0.55)을 넘어선다 → 이동 arm 이 impactReached 를 세운다.
            var proj = CreateSweeper(rehitCooldownSec: 0.5f, pierce: 0, maxDistance: 0.55f);

            Tick();

            Assert.AreEqual(1, DamageCount(enemy), "마지막 스윕은 정상 해결하고 나서 사라진다");
            Assert.IsFalse(_em.Exists(proj), "수명이 끝나면 재타격 탄도 소멸한다");
        }

        // ── ③ 궤도 × 재타격 end-to-end (content-4 리뷰 M5) ──────────────────────
        //
        // 위 케이스들은 전부 DirectionalLinear 픽스처 + `AdvanceProjectileClock` 수동 시계다.
        // 그래서 **이 feature 의 헤드라인 동작** — 「궤도 arm 이 elapsed 를 굴려 재타격 창이
        // 열린다」 — 를 지나가는 테스트가 하나도 없었다. 그 연결이 끊기면 화염구는 바퀴당
        // 1타로 **조용히 퇴화**하고 EditMode 는 전부 초록이다. 여기서 수동 시계를 쓰지 않는
        // 것이 이 케이스의 존재 이유다.
        // pierce 기본 1 = 탄 SO 의 pierceCount 를 드레인이 싣는 프로덕션 모양. 재타격이
        // 정상 동작하는 한 이 값은 읽히지 않는다(계약 3) — 비정상 경로의 상한일 뿐이다.
        private Entity CreateOrbiter(float radius, float angularSpeed, float lifetime,
                                     float rehitCooldownSec, float hitThreshold = 0.55f,
                                     int pierce = 1)
        {
            var start = Orbit.Position(float3.zero, radius, angularSpeed, 0f);
            var e = _em.CreateEntity();
            _em.AddComponentData(e, LocalTransform.FromPosition(start));
            _em.AddComponent<ProjectileTag>(e);
            _em.AddComponentData(e, new ProjectileState
            {
                movement = MovementKind.OrbitAroundPoint,
                payload = PayloadKind.PathHit,
                origin = float3.zero,          // 궤도 중심
                maxDistance = radius,          // 궤도 반경
                speed = angularSpeed,          // 각속도(rad/s)
                flightTime = lifetime,         // 지속 = 유일한 종료 조건
                prevPos = start,
                damage = 20f,
                hitThreshold = hitThreshold,
                pierceRemaining = pierce,
                rehitCooldownSec = rehitCooldownSec,
            });
            _em.AddBuffer<PathHitRecord>(e);
            return e;
        }

        [Test]
        public void Orbit_DrivesItsOwnClock_SoRehitWindowActuallyOpens()
        {
            // 링 위(반경 1, 위상 0)에 적을 둔다 — 구슬이 한 바퀴 돌 때마다 스친다.
            var enemy = CreateEnemy(1f);
            // ω = 2π rad/s → 1초에 한 바퀴. 쿨타임 0.5초 < 1바퀴라 «바퀴마다 1타» 가 상한이다.
            var proj = CreateOrbiter(radius: 1f, angularSpeed: math.PI * 2f,
                                     lifetime: 3.05f, rehitCooldownSec: 0.5f);

            for (int i = 0; i < 30; i++) Tick(0.1f);   // 3초

            // 수동 시계 없이 **이동 arm 이 굴린 elapsed** 로 창이 열려야 나오는 수다.
            Assert.GreaterOrEqual(DamageCount(enemy), 3,
                "3초/3바퀴면 최소 바퀴당 1타는 들어간다 — 1타뿐이면 궤도가 elapsed 를 안 굴린 것이다");
            Assert.LessOrEqual(DamageCount(enemy), 6,
                "쿨타임이 창을 막지 못하면 프레임마다 맞아 30타가 된다");
        }

        // content-5 (2026-08-17) — **주인이 사라지면 구슬도 사라진다.** content-4 는 반대를
        // 계약으로 적었고(자기 수명을 산다) 화면에서 «빈 자리에서 혼자 도는 구슬» 이 됐다.
        // 퇴근도 같은 경로다 — 퇴근은 엔티티를 파괴하므로 이 판정 하나가 둘 다 덮는다.
        [Test]
        public void Orbit_DespawnsWhenItsOwnerIsGone()
        {
            var host = _em.CreateEntity();
            _em.AddComponentData(host, LocalTransform.FromPosition(float3.zero));

            var enemy = CreateEnemy(1f);
            var proj = CreateOrbiter(radius: 1f, angularSpeed: math.PI * 2f,
                                     lifetime: 5f, rehitCooldownSec: 0.5f);
            var st = _em.GetComponentData<ProjectileState>(proj);
            st.owner = host;
            _em.SetComponentData(proj, st);

            for (int i = 0; i < 5; i++) Tick(0.1f);
            Assert.IsTrue(_em.Exists(proj), "주인이 살아 있는 동안은 돈다");

            _em.DestroyEntity(host);          // 사망/퇴근 = 엔티티 소멸
            Tick(0.1f);
            Assert.IsFalse(_em.Exists(proj), "주인이 사라지면 수명이 남아도 즉시 사라진다");
        }

        // 주인을 안 실은 궤도(테스트 픽스처·브리지 캐스트)는 종전대로 수명까지 산다 —
        // owner 가 Entity.Null 이면 판정을 건너뛴다(무회귀).
        [Test]
        public void Orbit_WithoutOwner_StillLivesItsFullLifetime()
        {
            var proj = CreateOrbiter(radius: 1f, angularSpeed: math.PI * 2f,
                                     lifetime: 1.05f, rehitCooldownSec: 0.5f);
            for (int i = 0; i < 10; i++) Tick(0.1f);
            Assert.IsTrue(_em.Exists(proj), "owner 미지정은 종전 동작");
            Tick(0.1f);
            Assert.IsFalse(_em.Exists(proj), "그래도 수명은 지킨다");
        }

        [Test]
        public void Orbit_LifetimeIsTheOnlyTerminator_PierceNeverConsumed()
        {
            var enemy = CreateEnemy(1f);
            var proj = CreateOrbiter(radius: 1f, angularSpeed: math.PI * 2f,
                                     lifetime: 1.05f, rehitCooldownSec: 0.5f);

            for (int i = 0; i < 10; i++) Tick(0.1f);   // 1초 — 아직 수명 안 끝남
            Assert.IsTrue(_em.Exists(proj), "관통 예산을 소모하지 않으므로 스쳐도 살아 있다");
            Assert.AreEqual(1, _em.GetComponentData<ProjectileState>(proj).pierceRemaining,
                "계약 3 — 재타격 레짐은 예산을 읽지도 쓰지도 않는다(1 로 태워도 안 깎인다)");

            Tick(0.1f);                                // 수명 초과
            Assert.IsFalse(_em.Exists(proj), "유일한 종료 조건은 수명이다");
        }

        // 리뷰 M1 — 기록 버퍼가 없으면 재타격을 켜지 않는다(fail-open 차단).
        [Test]
        public void Rehit_WithoutRecordBuffer_DegradesToOncePerVictim()
        {
            var enemy = CreateEnemy(1f);
            var proj = CreateOrbiter(radius: 1f, angularSpeed: math.PI * 2f,
                                     lifetime: 3.05f, rehitCooldownSec: 0.5f);
            _em.RemoveComponent<PathHitRecord>(proj);   // 기록 없는 탄(프로덕션엔 없는 상태)

            for (int i = 0; i < 30; i++) Tick(0.1f);

            Assert.AreEqual(1, DamageCount(enemy),
                "기록이 없으면 «적당 1회» 로 안전 퇴화한다 — 매 프레임 타격(fail-open)이 아니다");
            Assert.IsFalse(_em.Exists(proj), "예산을 다 쓴 탄은 사라진다 — 불멸 탄이 남지 않는다");
        }

        // ── ③ 왕복(부메랑) × 넉백 — dreamcatcher-content-5 units 1·2 ──────────
        //
        // 궤도와 같은 이유로 **수동 시계를 쓰지 않는다**: 이 궤적도 자기 elapsed 를 굴려야
        // 재타격 창이 열리고, 그 연결이 끊기면 부메랑은 다리당 1타로 조용히 퇴화한다.

        private Entity CreateBoomerang(float maxDistance, float speed, float rehitCooldownSec,
                                       float knockbackSpeed = 0f, float knockbackDuration = 0f,
                                       float hitThreshold = 0.55f, int pierce = 1)
        {
            var e = _em.CreateEntity();
            _em.AddComponentData(e, LocalTransform.FromPosition(float3.zero));
            _em.AddComponent<ProjectileTag>(e);
            _em.AddComponentData(e, new ProjectileState
            {
                movement = MovementKind.BoomerangReturn,
                payload = PayloadKind.PathHit,
                origin = float3.zero,            // 발사점 = 귀환점
                direction = new float2(1f, 0f),  // 발사 축(불변)
                maxDistance = maxDistance,
                speed = speed,
                prevPos = float3.zero,
                damage = 20f,
                hitThreshold = hitThreshold,
                pierceRemaining = pierce,
                rehitCooldownSec = rehitCooldownSec,
                knockbackSpeed = knockbackSpeed,
                knockbackDuration = knockbackDuration,
            });
            _em.AddBuffer<PathHitRecord>(e);
            return e;
        }

        // 넉백은 Combat→Effects 큐로 나간다. 이 픽스처의 월드엔 그 싱글턴이 없으므로
        // (히트 시스템이 옵셔널 게이트로 조용히 건너뛴다) 넉백을 볼 테스트만 만들어 쓴다.
        private Unity.Collections.NativeQueue<EnemyCcEvent> CreateCcQueue()
        {
            var q = new Unity.Collections.NativeQueue<EnemyCcEvent>(
                Unity.Collections.Allocator.Persistent);
            _em.AddComponentData(_em.CreateEntity(), new EnemyCcEventsSingleton { queue = q });
            return q;
        }

        [Test]
        public void Boomerang_DrivesItsOwnClock_AndHitsOnBothLegs()
        {
            // 편도 4 · 속도 8 → 왕복 1초. 경로 위 x=1 의 적은 나갈 때/돌아올 때 스친다
            // (두 타격 간격 = 2*(4-1)/8 = 0.75초 > 쿨타임 0.3).
            var enemy = CreateEnemy(1f);
            var proj = CreateBoomerang(maxDistance: 4f, speed: 8f, rehitCooldownSec: 0.3f);

            for (int i = 0; i < 10; i++) Tick(0.1f);   // 1초 = 정확히 왕복

            Assert.AreEqual(2, DamageCount(enemy),
                "나갈 때 한 번, 돌아올 때 한 번 — 1타면 elapsed 가 안 굴러간 것이다");
        }

        // 끝쪽 적은 두 통과가 겹쳐 **한 번만** 맞는다. 버그가 아니라 기하의 결과이며
        // 저작(쿨타임)이 그 경계를 정한다 — 경계식을 여기 고정한다.
        [Test]
        public void Boomerang_FarEnemy_IsHitOnce_BecauseThePassesCoincide()
        {
            var far = CreateEnemy(3.9f);
            CreateBoomerang(maxDistance: 4f, speed: 8f, rehitCooldownSec: 0.3f);

            for (int i = 0; i < 10; i++) Tick(0.1f);

            Assert.AreEqual(1, DamageCount(far),
                "경계 = maxDistance - (쿨타임*속도)/2 = 4 - 1.2 = 2.8 타일. 그 바깥은 1타다");
        }

        [Test]
        public void Boomerang_DespawnsAfterRoundTrip_NotBeforeAndNotNever()
        {
            var proj = CreateBoomerang(maxDistance: 4f, speed: 8f, rehitCooldownSec: 0.3f);

            for (int i = 0; i < 9; i++) Tick(0.1f);    // 0.9초 — 아직 귀환 전
            Assert.IsTrue(_em.Exists(proj), "왕복 전에 사라지면 안 된다");

            Tick(0.1f);                                 // 1.0초 = 왕복 완료
            Assert.IsFalse(_em.Exists(proj), "왕복 완료가 유일한 종료 조건이다");
        }

        // 이 spec 의 헤드라인 — **나갈 때 밀고 돌아올 때 당긴다.** 다리를 판별하는 상태가
        // 코드에 없으므로, 이것이 참이라는 증거는 이 테스트뿐이다.
        [Test]
        public void Boomerang_Knockback_PushesOutboundThenPullsBack()
        {
            var q = CreateCcQueue();
            try
            {
                var enemy = CreateEnemy(1f);
                CreateBoomerang(maxDistance: 4f, speed: 8f, rehitCooldownSec: 0.3f,
                                knockbackSpeed: 1.6f, knockbackDuration: 0.25f);

                for (int i = 0; i < 10; i++) Tick(0.1f);

                Assert.AreEqual(2, q.Count, "타격마다 넉백 하나 — 피해 없는 프레임은 밀지 않는다");
                var first = q.Dequeue();
                var second = q.Dequeue();

                Assert.AreEqual(CcKind.Impulse, first.effect.kind);
                Assert.AreEqual(enemy, first.target);
                Assert.Greater(first.effect.vector.x, 0f, "나갈 때는 진행 방향(+X)으로 민다");
                Assert.Less(second.effect.vector.x, 0f, "돌아올 때는 반대(-X)로 딸려온다");
                Assert.AreEqual(1.6f, math.length(first.effect.vector), 1e-3f,
                    "세기 = 거리÷시간(드레인이 환산해 실은 속도)");
                Assert.AreEqual(0.25f, first.effect.remainingTime, 1e-4f);
            }
            finally { q.Dispose(); }
        }

        // 무회귀 — 넉백 미저작(0)이면 이벤트가 **하나도** 나가지 않는다.
        [Test]
        public void Knockback_ZeroAuthoring_EmitsNothing()
        {
            var q = CreateCcQueue();
            try
            {
                CreateEnemy(1f);
                CreateBoomerang(maxDistance: 4f, speed: 8f, rehitCooldownSec: 0.3f);

                for (int i = 0; i < 10; i++) Tick(0.1f);

                Assert.AreEqual(0, q.Count, "기존 관통탄(샷건너 등)은 한 글자도 달라지면 안 된다");
            }
            finally { q.Dispose(); }
        }
    }
}
