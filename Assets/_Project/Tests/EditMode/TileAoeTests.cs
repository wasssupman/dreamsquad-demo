using NUnit.Framework;
using Unity.Mathematics;
using Wassup.Battle.Combat;

namespace Wassup.Tests.EditMode
{
    // Boundary coverage for the tile-AOE membership primitive (unit 2 of
    // projectile-trajectory-payload). This is the gameplay-critical "who gets hit"
    // calc, so the tileRange boundary is pinned exactly.
    public class TileAoeTests
    {
        [Test]
        public void Center_Is_In_Range_At_Zero()
        {
            Assert.IsTrue(TileAoe.IsInTileRange(new int2(5, 5), new int2(5, 5), 0));
        }

        [Test]
        public void Chebyshev_Diagonal_Counts_As_One()
        {
            Assert.AreEqual(1, TileAoe.TileDistance(new int2(0, 0), new int2(1, 1)));
            Assert.IsTrue(TileAoe.IsInTileRange(new int2(1, 1), new int2(0, 0), 1));
        }

        [Test]
        public void Boundary_Is_Inclusive()
        {
            // exactly tileRange away on an axis → in range
            Assert.IsTrue(TileAoe.IsInTileRange(new int2(3, 0), new int2(0, 0), 3));
            Assert.IsTrue(TileAoe.IsInTileRange(new int2(0, 3), new int2(0, 0), 3));
            // exactly tileRange away diagonally → in range (Chebyshev)
            Assert.IsTrue(TileAoe.IsInTileRange(new int2(3, 3), new int2(0, 0), 3));
        }

        [Test]
        public void Just_Outside_Is_Excluded()
        {
            Assert.IsFalse(TileAoe.IsInTileRange(new int2(4, 0), new int2(0, 0), 3));
            // Chebyshev distance of (3,4) is 4 > 3 — the larger axis governs
            Assert.IsFalse(TileAoe.IsInTileRange(new int2(3, 4), new int2(0, 0), 3));
        }

        [Test]
        public void Negative_Offsets_Are_Symmetric()
        {
            Assert.AreEqual(2, TileAoe.TileDistance(new int2(-2, 1), new int2(0, 0)));
            Assert.IsTrue(TileAoe.IsInTileRange(new int2(-2, -2), new int2(0, 0), 2));
            Assert.IsFalse(TileAoe.IsInTileRange(new int2(-3, 0), new int2(0, 0), 2));
        }

        [Test]
        public void Range_Zero_Hits_Only_The_Center_Cell()
        {
            Assert.IsTrue(TileAoe.IsInTileRange(new int2(7, 7), new int2(7, 7), 0));
            Assert.IsFalse(TileAoe.IsInTileRange(new int2(8, 7), new int2(7, 7), 0));
        }

        // ── elite-enemy-tier unit 1 — 부채꼴(콘) 멤버십 ─────────────────────────
        // 드래곤 화염 브레스의 유일한 신규 수학. 여기가 그 계약의 정본이다.

        private static float CosSq(float halfAngleDeg)
        {
            float c = math.cos(math.radians(halfAngleDeg));
            return c * c;
        }

        private static readonly float2 Origin = new float2(0f, 0f);
        private static readonly float2 Right = new float2(1f, 0f);

        [Test]
        public void Cone_Includes_StraightAhead()
        {
            Assert.IsTrue(TileAoe.IsInCone(Origin, new float2(2f, 0f), Right, CosSq(50f), 3f));
        }

        // 부호 가드 회귀 방지 — 이게 빠지면 제곱이 부호를 잃어 **등 뒤에 대칭 콘**이 생긴다.
        [Test]
        public void Cone_Excludes_DirectlyBehind()
        {
            Assert.IsFalse(TileAoe.IsInCone(Origin, new float2(-2f, 0f), Right, CosSq(50f), 3f),
                "등 뒤가 포함됐다 — dp > 0 부호 가드가 사라졌다");
        }

        [Test]
        public void Cone_Excludes_Perpendicular()
        {
            Assert.IsFalse(TileAoe.IsInCone(Origin, new float2(0f, 2f), Right, CosSq(50f), 3f));
        }

        [Test]
        public void Cone_Excludes_BeyondRange_EvenWhenAngleFits()
        {
            Assert.IsTrue(TileAoe.IsInCone(Origin, new float2(3f, 0f), Right, CosSq(50f), 3f));
            Assert.IsFalse(TileAoe.IsInCone(Origin, new float2(3.5f, 0f), Right, CosSq(50f), 3f),
                "사거리 밖인데 각도만으로 포함됐다");
        }

        // 대각 방향의 내적²/거리² = 0.5 다. cos²40° ≈ 0.587 > 0.5 → 제외,
        // cos²50° ≈ 0.413 < 0.5 → 포함. **저작 45° 는 이 경계에 정확히 걸려** 부동소수 비교가
        // 동전 던지기가 되고, 이 프로젝트는 비동기 토너먼트 동일 시뮬을 요건으로 두면서
        // Android·iOS·에디터를 동시에 타깃한다 → 저작 초기값을 50° 로 잡은 근거가 이것이다.
        [Test]
        public void Cone_Diagonal_FlipsAcrossTheAuthoringBoundary()
        {
            var diag = new float2(2f, 2f);
            Assert.IsFalse(TileAoe.IsInCone(Origin, diag, Right, CosSq(40f), 5f), "반각 40° → 대각 제외");
            Assert.IsTrue(TileAoe.IsInCone(Origin, diag, Right, CosSq(50f), 5f), "반각 50° → 대각 포함");
        }

        // 비행 적은 sim 좌표가 평면이라 바로 아래 유닛과 XZ 가 겹칠 수 있다. 그때 방향이
        // 무의미해지므로 부호 가드가 조용히 제외하지 않도록 명시 분기가 있다.
        [Test]
        public void Cone_Includes_SameSpot()
        {
            Assert.IsTrue(TileAoe.IsInCone(Origin, Origin, Right, CosSq(50f), 3f),
                "같은 자리가 제외됐다 — 드래곤 바로 아래 유닛이 브레스를 안 맞는다");
        }

        // 비대칭 술어의 인자 순서 고정 — from/to 를 뒤집으면 결과가 뒤집혀야 한다.
        // (반경 판정은 대칭이라 기존 단언이 이런 실수를 못 잡는다.)
        [Test]
        public void Cone_IsDirectional_SwappingFromAndToFlipsResult()
        {
            var a = Origin;
            var b = new float2(2f, 0f);
            Assert.IsTrue(TileAoe.IsInCone(a, b, Right, CosSq(50f), 3f));
            Assert.IsFalse(TileAoe.IsInCone(b, a, Right, CosSq(50f), 3f),
                "인자 순서를 뒤집었는데 같은 결과다 — 방향 술어가 아니다");
        }
    }
}
