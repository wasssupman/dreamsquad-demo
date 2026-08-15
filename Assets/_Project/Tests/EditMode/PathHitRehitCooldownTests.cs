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
    }
}
