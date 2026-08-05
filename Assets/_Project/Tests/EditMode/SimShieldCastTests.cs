using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Wassup.Sim;
using Wassup.Sim.Effects;
using Wassup.Sim.Movement;
using Wassup.Sim.Units;

namespace Wassup.Tests.EditMode
{
    /// <summary>
    /// battle-sim-extraction unit 18-G/5 — 실드 캐스트(#19) 이식 + 18-G 클러스터의 오라클.
    ///
    /// 앞 묶음은 구 sim 특성화(`DeathRelayCharacterizationTests.ShieldCastSystemTests`)의
    /// 어서션 복제다. 뒤는 선별자 결정론과 클러스터 배치(캡처 번호 ↔ phase)를 고정한다.
    /// </summary>
    public class SimShieldCastTests
    {
        private static readonly SimInt2 Grid = new SimInt2(12, 12);

        private SimWorld _world;
        private SimChannels _channels;
        private ShieldCastSystem _sut;

        [SetUp]
        public void SetUp()
        {
            _world = new SimWorld(new SimConfig(1u, 1u));
            _channels = new SimChannels();
            _sut = new ShieldCastSystem(_channels);
            _world.SetDeltaTime(0.016f);

            int n = Grid.x * Grid.y;
            var field = _world.Create();
            _world.Set(field, new FlowFieldSingleton
            {
                flow = new SimVec2[n],
                dist = new int[n],
                gridSize = Grid,
                tileSize = 1f,
                origin = default,
                goalCell = new SimInt2(11, 11),
            });
        }

        private SimEntityId Defender(SimInt2 cell, float hp = 10f, float maxHp = 10f)
        {
            var e = _world.Create();
            _world.Set(e, new DefenderUnitTag());
            _world.Set(e, new Health { value = hp, max = maxHp });
            _world.Set(e, SimTransform.FromPosition(new SimVec3(cell.x, 0f, cell.y)));
            _world.AddBuffer<IncomingShield>(e);
            _world.AddBuffer<ShieldSlot>(e);
            return e;
        }

        private void MakeCaster(SimEntityId e, float range, float amount, int targetCount,
                                ShieldTargetFilter filter, float cooldown = 4f)
            => _world.Set(e, new ShieldCastState
            {
                range = range, cooldownDuration = cooldown, cooldownRemaining = 0f,
                amount = amount, targetCount = targetCount, filter = filter,
            });

        private void Tick(float dt = 0.016f)
        {
            _world.SetDeltaTime(dt);
            _sut.Run(_world);
        }

        private int IncomingCount(SimEntityId e) => _world.GetBuffer<IncomingShield>(e).Count;
        private float Cooldown(SimEntityId e) => _world.Get<ShieldCastState>(e).cooldownRemaining;

        // ═════ 구 sim 특성화 복제 ═════════════════════════════════════════════

        [Test]
        public void NoCaster_SelfGate_DoesNothing()
        {
            var d = Defender(new SimInt2(2, 2));
            Assert.DoesNotThrow(() => Tick());
            Assert.AreEqual(0, IncomingCount(d));
        }

        [Test]
        public void Casts_ToSelf_AndResetsCooldown()
        {
            var c = Defender(new SimInt2(2, 2));
            MakeCaster(c, range: 2f, amount: 5f, targetCount: 1, filter: ShieldTargetFilter.Self);
            Tick();

            Assert.AreEqual(1, IncomingCount(c), "자신도 항상 후보다.");
            Assert.AreEqual(5f, _world.GetBuffer<IncomingShield>(c)[0].amount, 1e-5f);
            Assert.AreEqual(c, _world.GetBuffer<IncomingShield>(c)[0].source);
            Assert.AreEqual(4f, Cooldown(c), 1e-5f, "발화 후 쿨다운 리셋.");
        }

        [Test]
        public void CooldownTicks_WithoutCasting()
        {
            var c = Defender(new SimInt2(2, 2));
            MakeCaster(c, 2f, 5f, 1, ShieldTargetFilter.Self);
            Tick();                                   // 발화 + 쿨다운 4
            _world.GetBuffer<IncomingShield>(c).Clear();

            Tick(1f);
            Assert.AreEqual(0, IncomingCount(c), "쿨다운 중엔 발화하지 않는다.");
            Assert.AreEqual(3f, Cooldown(c), 1e-5f);
        }

        [Test]
        public void RangeGate_IsChebyshevTiles()
        {
            var c = Defender(new SimInt2(2, 2));
            MakeCaster(c, range: 1f, amount: 5f, targetCount: 8, filter: ShieldTargetFilter.All);
            var near = Defender(new SimInt2(3, 3));    // 체비셰프 1
            var far = Defender(new SimInt2(5, 2));     // 체비셰프 3
            Tick();

            Assert.AreEqual(1, IncomingCount(near), "대각선도 거리 1.");
            Assert.AreEqual(0, IncomingCount(far));
        }

        [Test]
        public void SkipsTargetsAlreadyAtOrAboveTheAmount_FromTheSameSource()
        {
            var c = Defender(new SimInt2(2, 2));
            MakeCaster(c, 2f, amount: 5f, targetCount: 8, filter: ShieldTargetFilter.All);
            var t = Defender(new SimInt2(3, 2));
            _world.GetBuffer<ShieldSlot>(t).Add(new ShieldSlot { source = c, value = 5f });
            Tick();

            Assert.AreEqual(0, IncomingCount(t), "같은 출처가 이미 5 이상이면 스킵.");
            // ⚠ 캐스터 자신은 여전히 후보다(All 필터) — 스킵된 것은 t 하나뿐이고,
            //   VFX 도 정확히 그만큼만 빠진다.
            var granted = _channels.ShieldGranted.Drain();
            Assert.AreEqual(1, granted.Count, "자기 부여만 남는다");
            Assert.AreEqual(new SimVec3(2f, 0f, 2f), granted[0].position, "스킵된 t 에는 VFX 가 없다");
        }

        [Test]
        public void DoesNotSkip_WhenTheExistingSlotIsFromAnotherSource()
        {
            var c = Defender(new SimInt2(2, 2));
            MakeCaster(c, 2f, amount: 5f, targetCount: 8, filter: ShieldTargetFilter.All);
            var other = Defender(new SimInt2(9, 9));
            var t = Defender(new SimInt2(3, 2));
            _world.GetBuffer<ShieldSlot>(t).Add(new ShieldSlot { source = other, value = 99f });
            Tick();

            Assert.AreEqual(1, IncomingCount(t), "출처가 다르면 교차 합산 대상이다.");
        }

        [Test]
        public void CooldownResets_EvenWhenNothingWasGranted()
        {
            var c = Defender(new SimInt2(2, 2));
            MakeCaster(c, 2f, amount: 5f, targetCount: 8, filter: ShieldTargetFilter.All);
            _world.GetBuffer<ShieldSlot>(c).Add(new ShieldSlot { source = c, value = 99f }); // 자기도 스킵
            Tick();

            Assert.AreEqual(0, IncomingCount(c));
            Assert.AreEqual(4f, Cooldown(c), 1e-5f, "대상 유무와 무관하게 쿨다운은 리셋된다.");
        }

        [Test]
        public void DeadOrPendingDefenders_AreNeitherCastersNorTargets()
        {
            var c = Defender(new SimInt2(2, 2));
            MakeCaster(c, 2f, 5f, 8, ShieldTargetFilter.All);
            var dead = Defender(new SimInt2(3, 2));
            _world.Set(dead, new DeadTag());
            var pending = Defender(new SimInt2(2, 3));
            _world.Set(pending, new PendingDeployment());
            Tick();

            Assert.AreEqual(0, IncomingCount(dead));
            Assert.AreEqual(0, IncomingCount(pending));
        }

        // ═════ 부여 → 병합 (조각 두 개가 만나는 자리) ═════════════════════════

        [Test]
        public void Grant_IsAppendOnly_MergeHappensInDamageResolve()
        {
            // ⚠ #19 는 append 만 한다 — 슬롯은 #34 가 드레인할 때까지 비어 있다.
            var c = Defender(new SimInt2(2, 2));
            MakeCaster(c, 2f, amount: 5f, targetCount: 1, filter: ShieldTargetFilter.Self);
            Tick();

            Assert.AreEqual(0, ShieldMath.Sum(_world.GetBuffer<ShieldSlot>(c)), "아직 병합 전");

            _world.AddBuffer<IncomingDamage>(c);
            new DamageApplicationSystem(_channels).Run(_world);

            Assert.AreEqual(5f, ShieldMath.Sum(_world.GetBuffer<ShieldSlot>(c)), 1e-5f, "#34 가 병합한다");
            Assert.AreEqual(0, IncomingCount(c), "부여 버퍼는 드레인 후 비워진다");
        }

        [Test]
        public void GrantedVfx_CarriesTargetPosition_NotCasterPosition()
        {
            var c = Defender(new SimInt2(2, 2));
            MakeCaster(c, 2f, amount: 5f, targetCount: 8, filter: ShieldTargetFilter.All);
            Defender(new SimInt2(3, 2));
            Tick();

            var events = _channels.ShieldGranted.Drain();
            Assert.AreEqual(2, events.Count, "자신 + 이웃");
            CollectionAssert.AreEquivalent(
                new[] { new SimVec3(2f, 0f, 2f), new SimVec3(3f, 0f, 2f) },
                events.Select(e => e.position).ToArray());
        }

        // ═════ ShieldTargeting 선별자 ════════════════════════════════════════

        private static List<ShieldCandidate> Cands(params (float d, float hp)[] xs)
            => xs.Select(x => new ShieldCandidate { distanceSq = x.d, effectiveHpRatio = x.hp }).ToList();

        [Test]
        public void Select_Self_TakesOnlySelfIndex_AndIgnoresTargetCount()
        {
            var results = new List<int>();
            ShieldTargeting.Select(ShieldTargetFilter.Self, targetCount: 5, selfIndex: 2,
                Cands((9f, 0.1f), (1f, 0.2f), (5f, 0.9f)), results);
            CollectionAssert.AreEqual(new[] { 2 }, results);
        }

        [Test]
        public void Select_Self_WithoutSelfInCandidates_SelectsNothing()
        {
            var results = new List<int>();
            ShieldTargeting.Select(ShieldTargetFilter.Self, 5, selfIndex: -1, Cands((1f, 0.1f)), results);
            Assert.AreEqual(0, results.Count);
        }

        [Test]
        public void Select_All_IsNearestFirst()
        {
            var results = new List<int>();
            ShieldTargeting.Select(ShieldTargetFilter.All, targetCount: 2, selfIndex: 0,
                Cands((9f, 0.1f), (1f, 0.9f), (4f, 0.5f)), results);
            CollectionAssert.AreEqual(new[] { 1, 2 }, results, "거리² 오름차순");
        }

        [Test]
        public void Select_MinHealth_IsLowestEffectiveRatioFirst()
        {
            var results = new List<int>();
            ShieldTargeting.Select(ShieldTargetFilter.MinHealth, targetCount: 2, selfIndex: 0,
                Cands((1f, 0.9f), (9f, 0.2f), (4f, 0.5f)), results);
            CollectionAssert.AreEqual(new[] { 1, 2 }, results, "거리는 보지 않는다");
        }

        [Test]
        public void Select_Ties_GoToTheLowerIndex()
        {
            // ⚠ 비동기 토너먼트가 양측에서 같은 시뮬을 돌린다 — 이 결정론이 판정의 일치다.
            var results = new List<int>();
            ShieldTargeting.Select(ShieldTargetFilter.All, targetCount: 2, selfIndex: 0,
                Cands((5f, 0.1f), (5f, 0.1f), (5f, 0.1f)), results);
            CollectionAssert.AreEqual(new[] { 0, 1 }, results);
        }

        [Test]
        public void Select_TargetCount_IsClampedToCandidateCount_AndNonPositiveSelectsNothing()
        {
            var results = new List<int>();
            ShieldTargeting.Select(ShieldTargetFilter.All, targetCount: 9, selfIndex: 0,
                Cands((1f, 0.1f), (2f, 0.2f)), results);
            Assert.AreEqual(2, results.Count);

            ShieldTargeting.Select(ShieldTargetFilter.All, targetCount: 0, selfIndex: 0,
                Cands((1f, 0.1f)), results);
            Assert.AreEqual(0, results.Count, "이전 결과가 남지 않는다(내부 Clear)");
        }

        [Test]
        public void MinHealthFilter_CountsShieldIntoEffectiveHp()
        {
            // 실드를 이미 두른 아군은 "덜 급한" 것으로 밀린다 — 계약 6.
            var c = Defender(new SimInt2(2, 2), hp: 10f, maxHp: 10f);
            MakeCaster(c, range: 3f, amount: 5f, targetCount: 1, filter: ShieldTargetFilter.MinHealth);
            var hurtButShielded = Defender(new SimInt2(3, 2), hp: 2f, maxHp: 10f);
            _world.GetBuffer<ShieldSlot>(hurtButShielded)
                  .Add(new ShieldSlot { source = _world.Create(), value = 7f }); // 유효 0.9
            var mildlyHurt = Defender(new SimInt2(4, 2), hp: 5f, maxHp: 10f);    // 유효 0.5

            Tick();

            Assert.AreEqual(0, IncomingCount(hurtButShielded), "실드 포함 유효 HP 가 더 높다");
            Assert.AreEqual(1, IncomingCount(mildlyHurt));
        }

        // ═════ 클러스터 배치 ═════════════════════════════════════════════════

        [Test]
        public void Cluster_OwnsSevenCaptureNumbers_AcrossFivePhases()
        {
            var steps = new DamageCluster(new SimChannels()).Steps().ToList();

            CollectionAssert.AreEqual(new[] { 11, 12, 19, 34, 35, 36, 41 },
                steps.Select(s => s.Order).ToArray(), "캡처 번호 오름차순");
            Assert.AreEqual(5, steps.Select(s => s.Phase).Distinct().Count());
        }

        [Test]
        public void Cluster_KeepsMarkingAndDestructionInDifferentPhases()
        {
            // ⚠ 이 클러스터의 핵심 계약 — 압축하면 사망 창이 사라진다.
            var steps = new DamageCluster(new SimChannels()).Steps().ToList();
            SimPhase PhaseOf(int order) => steps.First(s => s.Order == order).Phase;

            Assert.Less((int)PhaseOf(34), (int)PhaseOf(35), "마킹(#34) < 창(#35)");
            Assert.Less((int)PhaseOf(11), (int)PhaseOf(35), "안전망 마킹(#11) < 창");
            Assert.Less((int)PhaseOf(36), (int)PhaseOf(41), "창(#36) < 파괴(#41)");
            Assert.AreEqual(SimPhase.Destruction, PhaseOf(41), "유일한 파괴자는 P12 에 산다");
        }

        [Test]
        public void Cluster_PutsShieldGrantBeforeShieldMerge()
        {
            var steps = new DamageCluster(new SimChannels()).Steps().ToList();
            SimPhase PhaseOf(int order) => steps.First(s => s.Order == order).Phase;

            Assert.Less((int)PhaseOf(19), (int)PhaseOf(34),
                "부여(#19 P5)가 병합(#34 P9)보다 앞이라 같은 틱에 흡수된다");
        }
    }
}
