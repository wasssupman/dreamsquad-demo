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

        // unit 22 — 후보는 이제 **칸이 아니라 몸**이다(pos + bodyRadius). 표준 잡몹 0.25.
        static AggroCandidate C(int cx, int cz, bool aggroed)
            => At(cx, cz, aggroed);

        static AggroCandidate At(float x, float z, bool aggroed, float bodyRadius = 0.25f)
            => new AggroCandidate { pos = new float3(x, 0, z), bodyRadius = bodyRadius, aggroed = aggroed };

        // 기존 케이스의 공격자 = 1×1(몸 0.5) · tileSize 1. 옛 칸 게이트(≤2.5)와 같은 답을 내는
        // 대역이라 아래 단언들은 그대로 유효하다(도달 = 2 + 0.5 + 0.25 = 2.75).
        static int Select(NativeArray<AggroCandidate> cands, NativeArray<int> outIdx,
                          int held, int capacity, float rangeTiles = 2f, float selfBodyRadius = 0.5f)
            => AggroTargeting.SelectTargets(float3.zero, rangeTiles, 1f, selfBodyRadius,
                                            held, capacity, cands, outIdx);

        [Test]
        public void SelectTargets_FreeSlot_PrefersFreshOverNearerAggroed()
        {
            var cands = Cands(
                C(1, 0, aggroed: true),   // idx0: 근접(dist1) but 이미 어그로
                C(2, 0, aggroed: false)); // idx1: 원거리(dist2) 신규
            var outIdx = new NativeArray<int>(1, Allocator.Temp);

            int n = Select(cands, outIdx, held: 0, capacity: 4);

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

            int n = Select(cands, outIdx, held: 4, capacity: 4);

            Assert.AreEqual(1, n);
            Assert.AreEqual(0, outIdx[0], "상한 차면 겹친 어그로 팩(최근접) 정리");
            cands.Dispose(); outIdx.Dispose();
        }

        [Test]
        public void SelectTargets_ExcludesOutOfRange()
        {
            var cands = Cands(C(5, 0, aggroed: false)); // dist 5 > tileRange 2
            var outIdx = new NativeArray<int>(1, Allocator.Temp);

            int n = Select(cands, outIdx, held: 0, capacity: 4);

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

            int n = Select(cands, outIdx, held: 0, capacity: 4);

            Assert.AreEqual(2, n);
            Assert.AreEqual(1, outIdx[0], "신규 먼저");
            Assert.AreEqual(0, outIdx[1], "남은 슬롯은 어그로 적으로 채움");
            cands.Dispose(); outIdx.Dispose();
        }

        [Test]
        public void SelectTargets_EmptyCandidates_ReturnsZero()
        {
            var cands = new NativeArray<AggroCandidate>(0, Allocator.Temp);
            var outIdx = new NativeArray<int>(2, Allocator.Temp);
            int n = Select(cands, outIdx, held: 0, capacity: 4);
            Assert.AreEqual(0, n, "후보 0 → 선정 0");
            cands.Dispose(); outIdx.Dispose();
        }

        [Test]
        public void SelectTargets_ZeroMaxTargets_ReturnsZero()
        {
            var cands = Cands(C(1, 0, false));
            var outIdx = new NativeArray<int>(0, Allocator.Temp); // maxTargets 0
            int n = Select(cands, outIdx, held: 0, capacity: 4);
            Assert.AreEqual(0, n, "maxTargets 0 → 선정 0");
            cands.Dispose(); outIdx.Dispose();
        }

        [Test]
        public void SelectTargets_DoesNotExceedMaxTargets()
        {
            var cands = Cands(
                C(1, 0, false), C(1, 1, false), C(2, 0, false)); // 3 후보 모두 사거리 내
            var outIdx = new NativeArray<int>(2, Allocator.Temp); // 상한 2

            int n = Select(cands, outIdx, held: 0, capacity: 4);

            Assert.AreEqual(2, n, "maxTargets 초과 금지");
            cands.Dispose(); outIdx.Dispose();
        }

        // unit 22 — **몸 크기를 테스트 축으로 세운다.** 이 파일의 모든 픽스처가 정수 칸 위의
        // 1×1 유닛이었고, 그 대역에서는 옛 칸 술어와 몸 술어의 답이 같아서 결함이 숨었다
        // (배스티온이 「공격은 하는데 피해 0」이 될 때까지). 도달은 몸에 비례해야 한다.
        [Test]
        public void SelectTargets_ReachScalesWithAttackerBody()
        {
            // 사거리 1 근접 가디언 · 후보는 1.75타일 = 배스티온 몸(1.5) + 잡몹 몸(0.25),
            // 즉 **몸이 정확히 맞닿는 지점**이다.
            var cands = Cands(At(1.75f, 0f, aggroed: false));
            var outIdx = new NativeArray<int>(1, Allocator.Temp);

            int wide = Select(cands, outIdx, held: 0, capacity: 4, rangeTiles: 1f, selfBodyRadius: 1.5f);
            Assert.AreEqual(1, wide,
                "몸 1.5 가디언은 자기 몸에 맞닿은 적을 고른다 — 발사 게이트가 때린다고 한 그 적이다");

            // 몸 0.5 유닛의 도달은 1 + 0.5 + 0.25 = 1.75 로 **경계 포함**이라, 밖을 보려면 그 너머를 쓴다.
            var far = Cands(At(1.9f, 0f, aggroed: false));
            int slim = Select(far, outIdx, held: 0, capacity: 4, rangeTiles: 1f, selfBodyRadius: 0.5f);
            Assert.AreEqual(0, slim,
                "몸 0.5 유닛에게 1.9 는 사거리 밖 — 도달이 몸에 비례하지 않으면 이 둘이 같아진다");
            int wideFar = Select(far, outIdx, held: 0, capacity: 4, rangeTiles: 1f, selfBodyRadius: 1.5f);
            Assert.AreEqual(1, wideFar, "같은 1.9 가 몸 1.5 가디언에게는 도달 안이다(2.75)");
            far.Dispose();

            cands.Dispose(); outIdx.Dispose();
        }
    }
}
