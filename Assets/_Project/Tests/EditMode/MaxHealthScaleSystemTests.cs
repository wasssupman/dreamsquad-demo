// battle-sim-extraction unit 18-C/2 — **특성화 테스트(구 sim)**.
//
// `MaxHealthScaleSystem` 은 오라클이 **0** 이다(계획서 §증인 3). 순수 코어
// `Health.ScaleMax` 는 `HealthScaleMaxTests` 6건이 덮고 있지만, 그것은 **산식**만 본다.
// 이식이 실제로 틀리는 자리는 산식이 아니라 **시스템 골격**이다:
// lazy attach 조건 · baseMax 캡처 시점 · appliedMul 래치 · mul<=0 가드 · 중간 Playback.
// 아래 7건은 그 골격만 박제한다(산식 재검증은 하지 않는다 — 중복이다).
//
// `ModifierStats` 는 손으로 세팅한다. 상류(`ModifierStatsAggregateSystem`)를 끼우면 clamp·
// dirty 게이트가 섞여 이 시스템의 계약이 흐려진다 — 상류의 증인은 `ModifierFrameworkTests` 다.
using NUnit.Framework;
using Unity.Core;
using Unity.Entities;
using Wassup.Battle.Effects;
using Wassup.Battle.Units;

namespace Wassup.Tests.EditMode
{
    public class MaxHealthScaleSystemTests
    {
        private World _world;
        private EntityManager _em;
        private SimulationSystemGroup _simGroup;

        [SetUp]
        public void SetUp()
        {
            _world    = new World("MaxHealthScaleSystemTests");
            _em       = _world.EntityManager;
            _simGroup = _world.CreateSystemManaged<SimulationSystemGroup>();
            // `RequireForUpdate` 가 **없다** — 이 시스템은 게이트 없이 매 틱 돈다(18-B 이식 시 주의).
            _simGroup.AddSystemToUpdateList(_world.CreateSystem<MaxHealthScaleSystem>());
        }

        [TearDown]
        public void TearDown() => _world?.Dispose();

        // ── Helpers ───────────────────────────────────────────────────────────────

        private Entity CreateUnit(float value, float max, float maxHealthMul)
        {
            var e = _em.CreateEntity();
            _em.AddComponentData(e, new Health { value = value, max = max });
            _em.AddComponentData(e, new ModifierStats
            {
                damageMul     = 1f, attackSpeedMul = 1f, dmgTakenMul  = 1f,
                regenPerSec   = 0f, moveSpeedMul   = 1f, damageVsCcMul = 1f,
                maxHealthMul  = maxHealthMul,
            });
            return e;
        }

        private void SetMul(Entity e, float mul)
        {
            var s = _em.GetComponentData<ModifierStats>(e);
            s.maxHealthMul = mul;
            _em.SetComponentData(e, s);
        }

        private void SetHp(Entity e, float value)
        {
            var h = _em.GetComponentData<Health>(e);
            h.value = value;
            _em.SetComponentData(e, h);
        }

        private void Tick()
        {
            _world.SetTime(new TimeData(_world.Time.ElapsedTime + 0.016f, 0.016f));
            _simGroup.Update();
        }

        // ── 1·2. lazy attach 조건 ─────────────────────────────────────────────────
        // 부착은 `mul > 0 && mul != 1` 인 **첫 프레임**에만. 이 조건이 넓어지면 전 유닛이
        // MaxHealthScaleState 를 갖게 되고, 그 컴포넌트 유무는 상태 해시에 그대로 나간다.

        [Test]
        public void MulExactlyOne_NeverAttachesState()
        {
            var e = CreateUnit(value: 70f, max: 100f, maxHealthMul: 1f);

            Tick();

            Assert.IsFalse(_em.HasComponent<MaxHealthScaleState>(e),
                "배율이 1 이면 상태를 붙이지 않는다 — 대다수 유닛이 이 경로다.");
            var h = _em.GetComponentData<Health>(e);
            Assert.AreEqual(70f, h.value, 1e-5f);
            Assert.AreEqual(100f, h.max, 1e-5f);
        }

        [Test]
        public void MulZero_UninitializedGuard_NeverAttaches()
        {
            // 스폰 init 이 base 1 을 넣기 **전**의 한 프레임을 흉내낸다(BattleBridge 2곳).
            // 이 가드가 없으면 그 프레임에 baseMax 를 잡고 max 를 1 HP 로 깎아버린다.
            var e = CreateUnit(value: 100f, max: 100f, maxHealthMul: 0f);

            Tick();

            Assert.IsFalse(_em.HasComponent<MaxHealthScaleState>(e),
                "mul<=0 은 미초기화로 보고 부착하지 않는다.");
            Assert.AreEqual(100f, _em.GetComponentData<Health>(e).max, 1e-5f);
        }

        // ── 3. 중간 Playback — 부착과 적용이 같은 프레임 ──────────────────────────

        [Test]
        public void Attach_AndApply_InTheSameFrame()
        {
            var e = CreateUnit(value: 100f, max: 100f, maxHealthMul: 0.8f);

            Tick();

            Assert.IsTrue(_em.HasComponent<MaxHealthScaleState>(e));
            var st = _em.GetComponentData<MaxHealthScaleState>(e);
            Assert.AreEqual(100f, st.baseMax, 1e-5f, "baseMax 는 **부착 시점의 Health.max** 다.");
            Assert.AreEqual(0.8f, st.appliedMul, 1e-5f);

            var h = _em.GetComponentData<Health>(e);
            Assert.AreEqual(80f, h.max, 1e-5f,
                "중간 Playback — 부착된 그 프레임의 Pass 2 가 이미 적용한다(다음 틱이 아니다).");
            Assert.AreEqual(80f, h.value, 1e-5f, "축소는 value 를 새 max 로 클램프.");
        }

        // ── 4. appliedMul 래치 ────────────────────────────────────────────────────
        // 배율이 그대로면 **재계산하지 않는다**. 매 틱 재적용으로 이식하면 그 사이의 피해가
        // 매 틱 다시 클램프돼(value = min(value, max)) 회복이 되돌려지는 것처럼 보인다.

        [Test]
        public void AppliedMulLatch_DoesNotRecompute_WhileMulUnchanged()
        {
            var e = CreateUnit(value: 100f, max: 100f, maxHealthMul: 0.8f);
            Tick();                       // 80/80 으로 적용

            SetHp(e, 40f);                // 피해
            Tick();                       // 같은 배율 — 아무 일도 없어야 한다

            var h = _em.GetComponentData<Health>(e);
            Assert.AreEqual(40f, h.value, 1e-5f, "배율 무변 → 재계산 없음(피해가 보존된다).");
            Assert.AreEqual(80f, h.max, 1e-5f);
        }

        // ── 5. 복원 — baseMax 로 되돌리되 무료 힐 없음 ────────────────────────────

        [Test]
        public void RestoreToOne_RestoresMaxFromBaseMax_WithoutFreeHeal()
        {
            var e = CreateUnit(value: 100f, max: 100f, maxHealthMul: 0.8f);
            Tick();                       // 80/80

            SetMul(e, 1f);                // 번아웃 해제
            Tick();

            var h = _em.GetComponentData<Health>(e);
            Assert.AreEqual(100f, h.max, 1e-5f, "max 는 baseMax 로 복원된다.");
            Assert.AreEqual(80f, h.value, 1e-5f, "value 는 오르지 않는다 — 무료 힐 없음.");
            Assert.AreEqual(1f, _em.GetComponentData<MaxHealthScaleState>(e).appliedMul, 1e-5f,
                "복원도 래치를 갱신한다(mul==1 이 continue 대상이 아니다).");
        }

        // ── 6. 부착 **이후**의 mul<=0 ─────────────────────────────────────────────
        // Pass 2 에도 같은 가드가 있다. 상류가 순간적으로 0 을 흘려도 max 를 1 HP 로 깎지 않는다.

        [Test]
        public void MulDropsToZeroAfterAttach_Pass2Skips()
        {
            var e = CreateUnit(value: 100f, max: 100f, maxHealthMul: 0.8f);
            Tick();                       // 80/80, appliedMul=0.8

            SetMul(e, 0f);
            Tick();

            var h = _em.GetComponentData<Health>(e);
            Assert.AreEqual(80f, h.max, 1e-5f, "mul<=0 은 Pass 2 도 건너뛴다.");
            Assert.AreEqual(80f, h.value, 1e-5f);
            Assert.AreEqual(0.8f, _em.GetComponentData<MaxHealthScaleState>(e).appliedMul, 1e-5f,
                "건너뛰었으므로 래치도 갱신되지 않는다.");
        }

        // ── 7. baseMax 는 한 번만 잡힌다 ──────────────────────────────────────────
        // 이후 배율은 **원본**에 곱해진다. 매번 현재 max 에 곱하면 누적 오염이 난다
        // (0.8 → 1.5 를 현재값에 곱하면 80×1.5=120, 원본 기준이면 100×1.5=150).

        [Test]
        public void BaseMaxCapturedOnce_LaterMulAppliesToOriginal_NotCurrentMax()
        {
            var e = CreateUnit(value: 100f, max: 100f, maxHealthMul: 0.8f);
            Tick();                       // max 100 → 80, baseMax=100

            SetMul(e, 1.5f);
            Tick();

            Assert.AreEqual(150f, _em.GetComponentData<Health>(e).max, 1e-5f,
                "baseMax(100)×1.5 = 150. 현재 max(80)에 곱했다면 120 — 누적 오염이다.");
            Assert.AreEqual(100f, _em.GetComponentData<MaxHealthScaleState>(e).baseMax, 1e-5f,
                "baseMax 는 재캡처되지 않는다.");
            Assert.AreEqual(80f, _em.GetComponentData<Health>(e).value, 1e-5f,
                "확대는 value 를 올리지 않는다.");
        }
    }
}
