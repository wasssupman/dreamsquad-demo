using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using Wassup.Battle.Combat;

namespace Wassup.Tests.EditMode
{
    // aggro-targeting Unit 9 — 정의 계층 순수함수 회귀 고정. 아키텍처 타입 없이
    // primitive/NativeArray 입력만으로 정책·기하를 검증한다.
    public class AggroPolicyTests
    {
        // ── AggroPolicy.CanAcquire ──────────────────────────────────────────
        [Test]
        public void CanAcquire_FreeSlot_NotAggroed_True()
            => Assert.IsTrue(AggroPolicy.CanAcquire(held: 0, capacity: 4, alreadyAggroed: false));

        [Test]
        public void CanAcquire_Full_False()
            => Assert.IsFalse(AggroPolicy.CanAcquire(held: 4, capacity: 4, alreadyAggroed: false));

        [Test]
        public void CanAcquire_AlreadyAggroed_False_EvenWithRoom()
            => Assert.IsFalse(AggroPolicy.CanAcquire(held: 0, capacity: 4, alreadyAggroed: true));

        // ── AggroPolicy.ShouldRelease ───────────────────────────────────────
        [Test]
        public void ShouldRelease_AliveGuardian_False()
            => Assert.IsFalse(AggroPolicy.ShouldRelease(guardianAlive: true));

        [Test]
        public void ShouldRelease_DeadGuardian_True()
            => Assert.IsTrue(AggroPolicy.ShouldRelease(guardianAlive: false));

        // ── AggroTargeting.SelectTargets ────────────────────────────────────
        // 가디언 셀/위치 원점, 사거리 2. A=근접이지만 이미 어그로, B=원거리지만 신규.
        static NativeArray<AggroCandidate> Cands(params AggroCandidate[] a)
            => new NativeArray<AggroCandidate>(a, Allocator.Temp);

        static AggroCandidate C(int cx, int cz, bool aggroed)
            => new AggroCandidate { cell = new int2(cx, cz), pos = new float3(cx, 0, cz), aggroed = aggroed };

        [Test]
        public void SelectTargets_FreeSlot_PrefersFreshOverNearerAggroed()
        {
            var cands = Cands(
                C(1, 0, aggroed: true),   // idx0: 근접(dist1) but 이미 어그로
                C(2, 0, aggroed: false)); // idx1: 원거리(dist2) 신규
            var outIdx = new NativeArray<int>(1, Allocator.Temp);

            int n = AggroTargeting.SelectTargets(
                int2.zero, float3.zero, tileRange: 2, held: 0, capacity: 4, cands, outIdx);

            Assert.AreEqual(1, n);
            Assert.AreEqual(1, outIdx[0], "여유 있으면 근접 어그로 적보다 신규 적 우선");
            cands.Dispose(); outIdx.Dispose();
        }

        [Test]
        public void SelectTargets_Full_PicksNearestRegardlessOfAggro()
        {
            var cands = Cands(
                C(1, 0, aggroed: true),   // idx0: 근접
                C(2, 0, aggroed: false)); // idx1: 원거리
            var outIdx = new NativeArray<int>(1, Allocator.Temp);

            int n = AggroTargeting.SelectTargets(
                int2.zero, float3.zero, tileRange: 2, held: 4, capacity: 4, cands, outIdx);

            Assert.AreEqual(1, n);
            Assert.AreEqual(0, outIdx[0], "상한 차면 겹친 어그로 팩(최근접) 정리");
            cands.Dispose(); outIdx.Dispose();
        }

        [Test]
        public void SelectTargets_ExcludesOutOfRange()
        {
            var cands = Cands(C(5, 0, aggroed: false)); // dist 5 > tileRange 2
            var outIdx = new NativeArray<int>(1, Allocator.Temp);

            int n = AggroTargeting.SelectTargets(
                int2.zero, float3.zero, tileRange: 2, held: 0, capacity: 4, cands, outIdx);

            Assert.AreEqual(0, n, "사거리 밖 후보 제외");
            cands.Dispose(); outIdx.Dispose();
        }

        [Test]
        public void SelectTargets_FreshFirst_ThenFillsWithAggroed()
        {
            var cands = Cands(
                C(1, 0, aggroed: true),   // idx0: 근접 어그로
                C(2, 0, aggroed: false)); // idx1: 원거리 신규
            var outIdx = new NativeArray<int>(2, Allocator.Temp); // maxTargets 2

            int n = AggroTargeting.SelectTargets(
                int2.zero, float3.zero, tileRange: 2, held: 0, capacity: 4, cands, outIdx);

            Assert.AreEqual(2, n);
            Assert.AreEqual(1, outIdx[0], "신규 먼저");
            Assert.AreEqual(0, outIdx[1], "남은 슬롯은 어그로 적으로 채움");
            cands.Dispose(); outIdx.Dispose();
        }

        [Test]
        public void SelectTargets_DoesNotExceedMaxTargets()
        {
            var cands = Cands(
                C(1, 0, false), C(1, 1, false), C(2, 0, false)); // 3 후보 모두 사거리 내
            var outIdx = new NativeArray<int>(2, Allocator.Temp); // 상한 2

            int n = AggroTargeting.SelectTargets(
                int2.zero, float3.zero, tileRange: 2, held: 0, capacity: 4, cands, outIdx);

            Assert.AreEqual(2, n, "maxTargets 초과 금지");
            cands.Dispose(); outIdx.Dispose();
        }
    }
}
