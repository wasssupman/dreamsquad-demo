using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using Wassup.Battle.Combat.Projectile.Emission;

namespace Wassup.Tests.EditMode
{
    // on-place-skill-rework unit 1 → distance-based-range unit 18 재작성.
    // 후보 반경 필터의 순수 계약 — 자는 이제 **사거리 술어와 동일**(연속, 위치+양쪽 몸).
    //
    // 고정하는 것:
    //  ① `rangeTiles <= 0` 은 **전량 통과 + 원본 순서 보존** — 기존 패턴(융단폭격·불나방떼)
    //     이 이 arm 을 타므로 무회귀의 근거다.
    //  ② 반환은 **항상 원본 풀 index** — 스코프 지역 index 가 새어 나가면 emitter 잠금
    //     경로(`IndexOf(poolEntities, …)`)와 index 공간이 섞인다.
    //  ③ 도달 = range + 내몸 + 상대몸 (`InBodyReach` 와 같은 답).
    public class PatternScopeTests
    {
        private static int Run(float2[] posT, float2 host, float range, float hostR,
                               float[] radii, out int[] outIdx)
        {
            var src = new NativeArray<float2>(posT, Allocator.Temp);
            var rad = new NativeArray<float>(radii ?? new float[posT.Length], Allocator.Temp);
            var buf = new NativeArray<int>(posT.Length, Allocator.Temp);
            int n = PatternScope.FilterByReach(src, host, range, hostR, rad, buf);
            outIdx = new int[n];
            for (int i = 0; i < n; i++) outIdx[i] = buf[i];
            src.Dispose(); rad.Dispose(); buf.Dispose();
            return n;
        }

        [Test]
        public void RangeZero_PassesEverythingInOriginalOrder()
        {
            var pos = new[] { new float2(9f, 9f), new float2(0f, 0f), new float2(3f, 4f) };
            int n = Run(pos, float2.zero, 0f, 0.5f, null, out var idx);
            Assert.AreEqual(3, n);
            CollectionAssert.AreEqual(new[] { 0, 1, 2 }, idx, "순서까지 원본 그대로여야 한다");
        }

        [Test]
        public void NegativeRange_AlsoPassesEverything()
        {
            var pos = new[] { new float2(5f, 5f), new float2(-2f, 7f) };
            Assert.AreEqual(2, Run(pos, float2.zero, -3f, 0f, null, out _));
        }

        // 도달 = range + 내몸 + 상대몸. 사거리 술어(InBodyReach)와 같은 답이어야 한다.
        [Test]
        public void Reach_AddsBothBodies_AndIsBoundaryInclusive()
        {
            var host = new float2(5f, 5f);
            // range 2 + host 0.5 + body 0.25 = 2.75 상한.
            var pos = new[]
            {
                new float2(5f, 5f),      // 0.0  — 안
                new float2(7.7f, 5f),    // 2.7  — 안(경계 직전)
                new float2(7.8f, 5f),    // 2.8  — 밖
                new float2(5f, 7.75f),   // 2.75 — 경계 포함
            };
            var radii = new[] { 0.25f, 0.25f, 0.25f, 0.25f };
            int n = Run(pos, host, 2f, 0.5f, radii, out var idx);
            Assert.AreEqual(3, n);
            CollectionAssert.AreEqual(new[] { 0, 1, 3 }, idx, "원본 index 로 반환한다");
        }

        // 몸이 크면 더 멀리서도 걸린다 — 사거리와 같은 물성(보스가 더 잘 걸린다).
        [Test]
        public void BiggerBody_IsCaughtFromFurtherAway()
        {
            var host = float2.zero;
            var pos = new[] { new float2(3.0f, 0f), new float2(3.0f, 0f) };
            var radii = new[] { 0.0f, 0.6f };   // range 2 + host 0.5 → 점은 2.5 밖, 몸 0.6 은 3.1 안
            int n = Run(pos, host, 2f, 0.5f, radii, out var idx);
            Assert.AreEqual(1, n);
            Assert.AreEqual(1, idx[0]);
        }
    }
}
