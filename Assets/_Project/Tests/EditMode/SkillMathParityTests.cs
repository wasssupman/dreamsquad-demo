using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using Wassup.Battle.Combat;
using Wassup.Battle.Movement;
using Wassup.Skills;

namespace Wassup.Tests.EditMode
{
    // skill-layer-migration — 도메인 순수 코어와 Runtime 순수 코어가 **같은 답을 내는지**.
    //
    // ⚠ 이 파일이 존재하는 이유가 부끄럽다: `SkillMath` 주석이 「`SkillMathParityTests` 가
    // 그것을 고정한다」고 **주장하는데 그 테스트가 없었다.** 투트랙 리뷰가 잡았고, 그
    // phantom 그물이 막았어야 할 불일치가 실제로 있었다 —
    // `GridMath.RangeToTiles` 는 반올림인데 `SkillMath` 가 버림이었다.
    //
    // 이전이 끝나면 Runtime 쪽 소비자가 0 이 되고 이 테스트도 은퇴한다. 그때까지는
    // **두 구현이 공존**하므로, 갈리면 이전한 스킬과 안 한 스킬이 다른 대상을 고른다.
    public class SkillMathParityTests
    {
        [Test]
        public void RangeToTiles_MatchesGridMath_AcrossDecimals()
        {
            // 오늘 저작은 전부 정수라 답이 같다. 이 테스트가 지키는 것은 **소수가 들어올 때**다.
            float[] samples = { 0f, 0.4f, 0.5f, 0.6f, 1f, 1.4f, 1.5f, 2.5f, 3.49f, 3.5f, 7.99f, 12f };
            foreach (var r in samples)
                Assert.AreEqual(GridMath.RangeToTiles(r), SkillMath.RangeToTiles(r),
                    $"사거리 {r} 에서 갈렸다 — 이전한 스킬과 legacy 가 다른 반경을 본다");
        }

        [Test]
        public void ChebyshevDistance_MatchesGridMath()
        {
            int2[] pts = { new(0, 0), new(3, 1), new(-2, 5), new(7, 7), new(-4, -9) };
            foreach (var a in pts)
                foreach (var b in pts)
                    Assert.AreEqual(GridMath.ChebyshevDistance(a, b),
                        SkillMath.ChebyshevDistance(a.x, a.y, b.x, b.y),
                        $"{a} → {b} 에서 갈렸다");
        }

        // ⚠ **cap > 0 일 때만 parity 가 계약이다.** cap <= 0 은 「전부」인데 두 구현의
        // 반환 **순서**가 다르다(Runtime=인덱스순, 도메인=거리순). 오늘 cap<=0 소비자가
        // 없어서 무해하고, 생기면 그때 순서 계약을 정한다 — 지금 맞추면 어느 쪽이 옳은지
        // 모르는 채로 한쪽을 박제하게 된다.
        [Test]
        public void SelectNearest_MatchesAoeTargetCap_WhenCapped()
        {
            float[][] cases =
            {
                new[] { 4f, 1f, 9f, 1f, 16f },        // 동률 포함
                new[] { 1f, 2f, 3f },
                new[] { 5f },
                new[] { 2f, 2f, 2f, 2f },             // 전부 동률
                new[] { 9f, 7f, 5f, 3f, 1f },         // 역순
            };
            int[] caps = { 1, 2, 3, 5, 10 };

            foreach (var distSq in cases)
                foreach (var cap in caps)
                {
                    using var native = new NativeArray<float>(distSq, Allocator.Temp);
                    var expected = new NativeList<int>(Allocator.Temp);
                    AoeTargetCap.SelectNearest(native, cap, ref expected);

                    var actual = new int[distSq.Length];
                    int n = SkillMath.SelectNearest(distSq, distSq.Length, cap, actual);

                    Assert.AreEqual(expected.Length, n,
                        $"cap {cap}, 후보 {distSq.Length} — 고른 수가 다르다");
                    // 동률 tie-break 가 같아야 한다: **인덱스가 작은 쪽이 이긴다.**
                    // 그래야 같은 판이 같은 답을 낸다.
                    for (int i = 0; i < n; i++)
                        Assert.AreEqual(expected[i], actual[i],
                            $"cap {cap}, {i}번째 선택이 다르다 — 동률 규칙이 갈렸다");
                    expected.Dispose();
                }
        }
    }
}
