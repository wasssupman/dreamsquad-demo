using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using Wassup.Battle.Combat.Projectile.Emission;
using Wassup.Data;

namespace Wassup.Tests.EditMode
{
    // projectile-emission-pattern unit 0 → distance-based-range unit 18 재작성.
    // 타겟 선택 결정론 — 순위 축은 **simId 오름차순**(구 row-major 셀 키에서 교체.
    // 셀 키는 격자 없이는 정의되지 않는다). 스냅샷/청크 순서와 무관해야 리플레이가 성립한다.
    public class PatternTargetingTests
    {
        private static int Select(float2[] posT, int[] simIds, PatternSelectionRule rule,
                                  int fireCount, float2 host = default)
        {
            using var na = new NativeArray<float2>(posT, Allocator.Temp);
            using var ids = new NativeArray<int>(simIds, Allocator.Temp);
            return PatternTargeting.Select(na, ids, rule, fireCount, host);
        }

        [Test]
        public void Nearest_PicksTheClosestCandidate_ByContinuousDistance()
        {
            var pos = new[] { new float2(8f, 8f), new float2(3f, 3f), new float2(1f, 5f) };
            var ids = new[] { 10, 20, 30 };
            Assert.AreEqual(1, Select(pos, ids, PatternSelectionRule.Nearest, 0, new float2(2f, 2f)));
        }

        [Test]
        public void Nearest_IsIndependentOfSnapshotOrder()
        {
            var a = new[] { new float2(8f, 8f), new float2(3f, 3f), new float2(1f, 5f) };
            var aIds = new[] { 10, 20, 30 };
            var b = new[] { new float2(1f, 5f), new float2(8f, 8f), new float2(3f, 3f) };
            var bIds = new[] { 30, 10, 20 };
            int ra = Select(a, aIds, PatternSelectionRule.Nearest, 0, new float2(2f, 2f));
            int rb = Select(b, bIds, PatternSelectionRule.Nearest, 0, new float2(2f, 2f));
            Assert.AreEqual(aIds[ra], bIds[rb], "청크 순서가 흔들려도 같은 대상이어야 리플레이가 성립한다");
        }

        [Test]
        public void Nearest_TieBreaksByLowerSimId()
        {
            // 완전 동거리 — 먼저 스폰된(낮은 simId) 쪽이 이긴다. AttackSystem unit 2 와 같은 축.
            var pos = new[] { new float2(3f, 2f), new float2(1f, 2f) };
            var ids = new[] { 7, 4 };
            Assert.AreEqual(1, Select(pos, ids, PatternSelectionRule.Nearest, 0, new float2(2f, 2f)));
        }

        [Test]
        public void RoundRobin_CyclesInSimIdOrder_RegardlessOfSnapshotOrder()
        {
            var pos = new[] { new float2(1f, 1f), new float2(5f, 5f), new float2(2f, 9f) };
            var ids = new[] { 30, 10, 20 };   // simId 순위: idx1(10) → idx2(20) → idx0(30)
            Assert.AreEqual(1, Select(pos, ids, PatternSelectionRule.RoundRobin, 0));
            Assert.AreEqual(2, Select(pos, ids, PatternSelectionRule.RoundRobin, 1));
            Assert.AreEqual(0, Select(pos, ids, PatternSelectionRule.RoundRobin, 2));
            Assert.AreEqual(1, Select(pos, ids, PatternSelectionRule.RoundRobin, 3));
        }

        [Test]
        public void Shuffle_IsDeterministic_AndInRange()
        {
            var pos = new[] { new float2(1f, 1f), new float2(5f, 5f), new float2(2f, 9f), new float2(0f, 4f) };
            var ids = new[] { 1, 2, 3, 4 };
            for (int fire = 0; fire < 16; fire++)
            {
                int first = Select(pos, ids, PatternSelectionRule.DeterministicShuffle, fire);
                int second = Select(pos, ids, PatternSelectionRule.DeterministicShuffle, fire);
                Assert.AreEqual(first, second, "같은 fireCount 는 항상 같은 결과");
                Assert.That(first, Is.InRange(0, pos.Length - 1));
            }
        }

        [Test]
        public void Shuffle_IsIndependentOfSnapshotOrder()
        {
            var a = new[] { new float2(1f, 1f), new float2(5f, 5f), new float2(2f, 9f) };
            var aIds = new[] { 11, 22, 33 };
            var b = new[] { new float2(2f, 9f), new float2(1f, 1f), new float2(5f, 5f) };
            var bIds = new[] { 33, 11, 22 };
            for (int fire = 0; fire < 8; fire++)
            {
                int ra = Select(a, aIds, PatternSelectionRule.DeterministicShuffle, fire);
                int rb = Select(b, bIds, PatternSelectionRule.DeterministicShuffle, fire);
                Assert.AreEqual(aIds[ra], bIds[rb], $"fire={fire}: 순위 축이 simId 라 스냅샷 순서 무관");
            }
        }

        [Test]
        public void EmptyPool_ReturnsMinusOne()
        {
            Assert.AreEqual(-1, Select(new float2[0], new int[0], PatternSelectionRule.RoundRobin, 0));
            Assert.AreEqual(-1, Select(new float2[0], new int[0], PatternSelectionRule.DeterministicShuffle, 3));
            Assert.AreEqual(-1, Select(new float2[0], new int[0], PatternSelectionRule.None, 0));
        }

        [Test]
        public void None_DoesNotSelect_FromNonEmptyPool()
        {
            var pos = new[] { new float2(1f, 1f), new float2(2f, 2f) };
            Assert.AreEqual(-1, Select(pos, new[] { 1, 2 }, PatternSelectionRule.None, 0));
        }

        // 리뷰 M2 — 재작성 때 소실된 핀 복원: 셔플이 단순 회전으로 퇴화하거나
        // 특정 후보로 편향되지 않는지(성질), 그리고 Nearest 의 빈 풀.
        [Test]
        public void Shuffle_IsNotAPlainRotation()
        {
            var pos = new[] { new float2(1f, 1f), new float2(5f, 5f), new float2(2f, 9f), new float2(0f, 4f) };
            var ids = new[] { 1, 2, 3, 4 };
            bool diverged = false;
            int prev = Select(pos, ids, PatternSelectionRule.DeterministicShuffle, 0);
            for (int fire = 1; fire < 16 && !diverged; fire++)
            {
                int cur = Select(pos, ids, PatternSelectionRule.DeterministicShuffle, fire);
                int rot = Select(pos, ids, PatternSelectionRule.RoundRobin, fire);
                int rotPrev = Select(pos, ids, PatternSelectionRule.RoundRobin, fire - 1);
                // 회전이면 (prev→cur) 스텝이 round-robin 스텝과 항상 같다 — 한 번이라도 다르면 회전 아님.
                if ((cur - prev + pos.Length) % pos.Length != (rot - rotPrev + pos.Length) % pos.Length)
                    diverged = true;
                prev = cur;
            }
            Assert.IsTrue(diverged, "셔플이 round-robin 회전으로 퇴화했다");
        }

        [Test]
        public void Shuffle_CoversEveryCandidate_OverManyShots()
        {
            var pos = new[] { new float2(1f, 1f), new float2(5f, 5f), new float2(2f, 9f), new float2(0f, 4f) };
            var ids = new[] { 1, 2, 3, 4 };
            var seen = new bool[pos.Length];
            for (int fire = 0; fire < 64; fire++)
                seen[Select(pos, ids, PatternSelectionRule.DeterministicShuffle, fire)] = true;
            for (int i = 0; i < seen.Length; i++)
                Assert.IsTrue(seen[i], $"후보 {i} 가 64발 동안 한 번도 안 뽑혔다 — 편향");
        }

        [Test]
        public void Nearest_EmptyPool_ReturnsMinusOne()
        {
            Assert.AreEqual(-1, Select(new float2[0], new int[0], PatternSelectionRule.Nearest, 0, float2.zero));
        }

        [Test]
        public void NegativeFireCount_RoundRobin_DoesNotThrow()
        {
            var pos = new[] { new float2(1f, 1f), new float2(2f, 2f) };
            int r = Select(pos, new[] { 1, 2 }, PatternSelectionRule.RoundRobin, -5);
            Assert.That(r, Is.InRange(0, 1));
        }
    }

    // 리뷰 M2 — 재작성 때 소실된 전수성 핀 복원. 새 MovementKind 를 추가하고 분류를
    // 잊으면 여기서 빨개진다(KnownKindCount 드리프트 감시).
    public class MovementBindingTests
    {
        [Test]
        public void ClassifiesEveryKnownKind()
        {
            var kinds = System.Enum.GetValues(typeof(Wassup.Battle.Combat.Projectile.MovementKind));
            Assert.AreEqual(MovementBinding.KnownKindCount, kinds.Length,
                "MovementKind 가 늘었다 — MovementBinding.Of 분류와 KnownKindCount 를 같이 갱신하라");
            foreach (Wassup.Battle.Combat.Projectile.MovementKind k in kinds)
                Assert.DoesNotThrow(() => MovementBinding.Of(k));
        }
    }
}
