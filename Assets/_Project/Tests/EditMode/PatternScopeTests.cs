using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using Wassup.Battle.Combat.Projectile.Emission;
using Wassup.Data;

namespace Wassup.Tests.EditMode
{
    // on-place-skill-rework unit 1 — 후보 반경 필터의 순수 계약.
    //
    // 두 가지를 고정한다:
    //  ① `tileRange <= 0` 은 **전량 통과 + 원본 순서 보존** — 기존 패턴(보스 융단폭격·
    //     나이트메어 미사일)이 이 arm 을 타므로 무회귀의 근거다.
    //  ② 반환은 **항상 원본 풀 index** — 스코프 지역 index 가 새어 나가면 emitter 의 잠금
    //     경로(`IndexOf(poolEntities, …)`)와 index 공간이 섞여 엉뚱한 칸을 때린다.
    public class PatternScopeTests
    {
        private static int Run(int2[] cells, int2 host, int range, out int[] outIdx)
        {
            var src = new NativeArray<int2>(cells, Allocator.Temp);
            var buf = new NativeArray<int>(cells.Length, Allocator.Temp);
            int n = PatternScope.Filter(src, host, range, buf);
            outIdx = new int[n];
            for (int i = 0; i < n; i++) outIdx[i] = buf[i];
            src.Dispose(); buf.Dispose();
            return n;
        }

        // 무회귀 핀 — 기존 패턴은 scope 0 이고 맵 전체를 후보로 본다.
        [Test]
        public void RangeZero_PassesEverythingInOriginalOrder()
        {
            var cells = new[] { new int2(9, 9), new int2(0, 0), new int2(3, 4) };
            int n = Run(cells, new int2(0, 0), 0, out var idx);
            Assert.AreEqual(3, n);
            CollectionAssert.AreEqual(new[] { 0, 1, 2 }, idx, "순서까지 원본 그대로여야 한다");
        }

        [Test]
        public void NegativeRange_AlsoPassesEverything()
        {
            var cells = new[] { new int2(5, 5), new int2(-2, 7) };
            Assert.AreEqual(2, Run(cells, new int2(0, 0), -3, out _));
        }

        // Chebyshev — 대각도 같은 거리다(타일 격자 관례, AuraPulse 와 동일).
        [Test]
        public void Range_UsesChebyshevAndIsBoundaryInclusive()
        {
            var host = new int2(5, 5);
            var cells = new[]
            {
                new int2(5, 5),   // 0 — 같은 칸
                new int2(7, 7),   // 2 — 대각 경계
                new int2(8, 5),   // 3 — 밖
                new int2(3, 6),   // 2 — 안
                new int2(5, 8),   // 3 — 밖
            };
            int n = Run(cells, host, 2, out var idx);
            CollectionAssert.AreEqual(new[] { 0, 1, 3 }, idx, $"반경 2 안은 3개여야 한다(실제 {n})");
        }

        // 1:1 사양 핀 — 같은 칸에 적이 둘이면 미사일도 둘이다.
        [Test]
        public void SameCellCandidates_AreNotDeduplicated()
        {
            var cells = new[] { new int2(2, 2), new int2(2, 2), new int2(2, 2) };
            int n = Run(cells, new int2(2, 2), 1, out var idx);
            Assert.AreEqual(3, n,
                "셀 중복을 제거하면 같은 칸의 적 하나가 공짜로 산다 — fan-out 은 적 1기당 1발이다");
            CollectionAssert.AreEqual(new[] { 0, 1, 2 }, idx);
        }

        // 반환값이 원본 index 라는 것 — 앞쪽 후보가 전부 탈락해도 남은 것의 번호가 유지된다.
        [Test]
        public void ReturnsOriginalPoolIndices_NotLocalOnes()
        {
            var cells = new[]
            {
                new int2(50, 50), new int2(60, 60),   // 0,1 — 밖
                new int2(1, 1),                        // 2  — 안
                new int2(70, 70),                      // 3  — 밖
                new int2(0, 2),                        // 4  — 안
            };
            int n = Run(cells, new int2(1, 1), 2, out var idx);
            Assert.AreEqual(2, n);
            CollectionAssert.AreEqual(new[] { 2, 4 }, idx,
                "지역 index(0,1)를 반환하면 emitter 의 잠금 경로와 index 공간이 섞인다");
        }

        [Test]
        public void NoCandidateInRange_ReturnsZero()
        {
            var cells = new[] { new int2(20, 20), new int2(30, 30) };
            Assert.AreEqual(0, Run(cells, new int2(0, 0), 2, out _));
        }

        [Test]
        public void EmptyPool_ReturnsZero()
        {
            Assert.AreEqual(0, Run(new int2[0], new int2(0, 0), 3, out _));
        }

        // ── TryToSpec 복사 핀 ────────────────────────────────────────────────
        // 이 함수가 PatternSpec 의 **유일한 writer** 다. SO 에만 필드를 더하고 여기를 잊으면
        // spec 이 조용히 0/false 가 되어 «맵 전체 폭격 + 단일 타겟» 으로 동작한다.
        [Test]
        public void TryToSpec_CopiesScopeAndFanOut()
        {
            var barrel = ScriptableObject.CreateInstance<ProjectileData>();
            var so = ScriptableObject.CreateInstance<ProjectilePatternData>();
            so.barrel = barrel;
            so.shots = new[] { new ProjectileShotStep { directionT = 0f, intervalAfterPreviousSec = 0f } };
            so.selection = PatternSelectionRule.RoundRobin;
            so.scopeTileRange = 2;
            so.fanOutToAllCandidates = true;

            Assert.IsTrue(so.TryToSpec(7, out var spec), "유효한 패턴이어야 한다");
            Assert.AreEqual(2, spec.scopeTileRange, "scopeTileRange 가 복사되지 않았다");
            Assert.IsTrue(spec.fanOutToAllCandidates, "fanOutToAllCandidates 가 복사되지 않았다");

            Object.DestroyImmediate(so);
            Object.DestroyImmediate(barrel);
        }

        // 기본값은 기존 동작이어야 한다 — 저작하지 않은 모든 기존 asset 의 계약.
        [Test]
        public void TryToSpec_DefaultsAreLegacyBehaviour()
        {
            var barrel = ScriptableObject.CreateInstance<ProjectileData>();
            var so = ScriptableObject.CreateInstance<ProjectilePatternData>();
            so.barrel = barrel;
            so.shots = new[] { new ProjectileShotStep { directionT = 0f, intervalAfterPreviousSec = 0f } };
            so.selection = PatternSelectionRule.RoundRobin;

            Assert.IsTrue(so.TryToSpec(0, out var spec));
            Assert.AreEqual(0, spec.scopeTileRange, "기본 0 = 맵 전체");
            Assert.IsFalse(spec.fanOutToAllCandidates, "기본 false = shot 당 후보 1개");

            Object.DestroyImmediate(so);
            Object.DestroyImmediate(barrel);
        }
    }
}
