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
            Assert.IsTrue(Wassup.Skills.SkillCone.IsInCone(Origin, new float2(2f, 0f), Right, CosSq(50f), 3f));
        }

        // 부호 가드 회귀 방지 — 이게 빠지면 제곱이 부호를 잃어 **등 뒤에 대칭 콘**이 생긴다.
        [Test]
        public void Cone_Excludes_DirectlyBehind()
        {
            Assert.IsFalse(Wassup.Skills.SkillCone.IsInCone(Origin, new float2(-2f, 0f), Right, CosSq(50f), 3f),
                "등 뒤가 포함됐다 — dp > 0 부호 가드가 사라졌다");
        }

        [Test]
        public void Cone_Excludes_Perpendicular()
        {
            Assert.IsFalse(Wassup.Skills.SkillCone.IsInCone(Origin, new float2(0f, 2f), Right, CosSq(50f), 3f));
        }

        [Test]
        public void Cone_Excludes_BeyondRange_EvenWhenAngleFits()
        {
            Assert.IsTrue(Wassup.Skills.SkillCone.IsInCone(Origin, new float2(3f, 0f), Right, CosSq(50f), 3f));
            Assert.IsFalse(Wassup.Skills.SkillCone.IsInCone(Origin, new float2(3.5f, 0f), Right, CosSq(50f), 3f),
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
            Assert.IsFalse(Wassup.Skills.SkillCone.IsInCone(Origin, diag, Right, CosSq(40f), 5f), "반각 40° → 대각 제외");
            Assert.IsTrue(Wassup.Skills.SkillCone.IsInCone(Origin, diag, Right, CosSq(50f), 5f), "반각 50° → 대각 포함");
        }

        // 비행 적은 sim 좌표가 평면이라 바로 아래 유닛과 XZ 가 겹칠 수 있다. 그때 방향이
        // 무의미해지므로 부호 가드가 조용히 제외하지 않도록 명시 분기가 있다.
        [Test]
        public void Cone_Includes_SameSpot()
        {
            Assert.IsTrue(Wassup.Skills.SkillCone.IsInCone(Origin, Origin, Right, CosSq(50f), 3f),
                "같은 자리가 제외됐다 — 드래곤 바로 아래 유닛이 브레스를 안 맞는다");
        }

        // 비대칭 술어의 인자 순서 고정 — from/to 를 뒤집으면 결과가 뒤집혀야 한다.
        // (반경 판정은 대칭이라 기존 단언이 이런 실수를 못 잡는다.)
        [Test]
        public void Cone_IsDirectional_SwappingFromAndToFlipsResult()
        {
            var a = Origin;
            var b = new float2(2f, 0f);
            Assert.IsTrue(Wassup.Skills.SkillCone.IsInCone(a, b, Right, CosSq(50f), 3f));
            Assert.IsFalse(Wassup.Skills.SkillCone.IsInCone(b, a, Right, CosSq(50f), 3f),
                "인자 순서를 뒤집었는데 같은 결과다 — 방향 술어가 아니다");
        }

        // ── 광역 멤버십의 모양 (distance-based-range unit 4b) ──────────────
        //
        // `IsInTileRange`(체비셰프·정사각형)는 **격자 통계 전용**으로 남았고, 광역의 정본은
        // `IsInRadius` 다 — 사거리 술어와 **같은 본체**(`SkillMath` 공통 본문, 진입점만 형이 다르다).
        // 아래 셋이 그 계약이다: 반경 1은 여덟 이웃 전부 · 반경 2는 정대각만 빠짐 ·
        // 두 함수가 실제로 다르다.

        [Test]
        public void Radius1_KeepsAllEightNeighbours()
        {
            // ⚠ **순수 원(`dx²+dy² ≤ r²`)으로 쓰면 여기가 무너진다** — 대각 칸의 중심거리가
            // 1.41 이라 반경 1 폭발이 **십자 모양**이 된다. 0.5 는 「공격자의 몸」이 아니라
            // **「칸의 반폭」**이고, 후보는 점이 아니라 한 변 1의 사각형이다.
            var c = new int2(10, 10);
            for (int dy = -1; dy <= 1; dy++)
            for (int dx = -1; dx <= 1; dx++)
                Assert.IsTrue(TileAoe.IsInRadius(new int2(c.x + dx, c.y + dy), c, 1),
                    $"반경 1 이 이웃 ({dx},{dy}) 을 잃었다 — 폭발이 십자가 됐다");
            Assert.IsFalse(TileAoe.IsInRadius(new int2(c.x + 2, c.y), c, 1), "두 칸은 밖");
        }

        [Test]
        public void Radius2_LosesOnlyTheTrueCorner()
        {
            var c = new int2(10, 10);
            // rev 2 산식: |Δ| ≤ r + 0.5. (rev 1 의 `max(|Δ|−0.5,0) ≤ r` 이 아니다 — 주석이
            // 그쪽에 남아 있었고, 반경 2 는 두 식의 답이 우연히 같아 초록인 채로 거짓말했다.)
            Assert.IsTrue(TileAoe.IsInRadius(new int2(12, 10), c, 2), "축 2칸 — 2.0 ≤ 2.5");
            Assert.IsTrue(TileAoe.IsInRadius(new int2(12, 11), c, 2), "얕은 대각 — 2.236 ≤ 2.5");
            Assert.IsFalse(TileAoe.IsInRadius(new int2(12, 12), c, 2), "정대각 — 2.83 > 2.5");
        }

        [Test]
        public void SquareAndRoundedDiffer_AtTheCorner()
        {
            // 두 함수가 **실제로 다르다**는 사실 자체를 고정한다. 같아지면 둘 중 하나가
            // 잘못 바뀐 것이고, 그때 `DefenderDensity`(보스 착지)가 조용히 움직인다.
            var c = new int2(0, 0); var corner = new int2(2, 2);
            Assert.IsTrue(TileAoe.IsInTileRange(corner, c, 2), "체비셰프는 모서리를 포함");
            Assert.IsFalse(TileAoe.IsInRadius(corner, c, 2), "둥근 쪽은 제외");
        }

        [Test]
        public void TargetBody_WidensTheBlast()
        {
            // 큰 몸은 폭발에 더 잘 걸린다 — unit 3 의 축이 광역에도 흐른다.
            var c = new int2(0, 0); var far = new int2(3, 0);   // v=2.5
            Assert.IsFalse(TileAoe.IsInRadius(far, c, 2));
            Assert.IsTrue(TileAoe.IsInRadius(far, c, 2, targetBodyRadiusTiles: 0.9f), "상한 2.9 ≥ 2.5");
        }


        // ── rev 2 가 **실제로 바꾼 구간**은 R≥3 이다 ────────────────────────
        //
        // ⚠ 위 단언들은 전부 반경 1·2 인데 **그 둘은 rev 1 과 rev 2 의 답이 같다**
        // (축은 두 식이 동일하고, 반경 2 의 정대각은 양쪽 다 밖). 즉 rev 2 가 좁힌 구간의
        // 커버리지가 **0 이었다**(리뷰 H-4).
        //
        // 두 식의 관계: rev 1 은 대각에서 `(0.5,0.5)` 를 빼므로 실효 여유가 **0.707**,
        // rev 2 는 스칼라 0.5 → **rev 2 ⊂ rev 1, 축에서만 등호.**
        //
        // 영향받는 저작은 오늘 **하나**다 — `Projectile_NightmareBarrage`(`impactTileRange: 3`,
        // 45칸 → 37칸, −17.8%). 드림캐쳐 액션 카드라 플레이어가 직접 쓰고, **코퍼스 덱에 없어
        // 골든이 못 본다.** 그래서 여기가 유일한 그물이다.
        [Test]
        public void Radius3_IsWhereRev2ActuallyNarrowed()
        {
            var c = new int2(10, 10);
            Assert.IsTrue(TileAoe.IsInRadius(new int2(13, 10), c, 3), "축 3칸 — 3.0 ≤ 3.5");
            Assert.IsTrue(TileAoe.IsInRadius(new int2(13, 11), c, 3), "(3,1) — 3.162 ≤ 3.5");
            Assert.IsTrue(TileAoe.IsInRadius(new int2(12, 12), c, 3), "(2,2) — 2.83 ≤ 3.5");
            // ★ rev 1 이면 안이었다(v=(2.5,1.5)=2.92 ≤ 3). rev 2 에서 빠진 바로 그 칸.
            Assert.IsFalse(TileAoe.IsInRadius(new int2(13, 12), c, 3),
                "(3,2) — 3.606 > 3.5. **rev 1 에서는 안이었다** — 이 단언이 rev 2 를 고정한다");
            Assert.IsFalse(TileAoe.IsInRadius(new int2(13, 13), c, 3), "정대각 — 4.24 > 3.5");
        }

        // ⚠ **반경 1 의 안전 여유가 rev 2 에서 0.293 → 0.086 으로 줄었다**(리뷰 M-5).
        // 저작된 AoE 의 절대다수(반경 1, 20건)가 이 여유 위에 서 있고,
        // 칸 반폭 상수 `CellHalfWidthTiles`(유닛 몸 아님)를 0.5 미만으로 만지는 순간(예: 0.4 → 1.414 > 1.4)
        // **반경 1 폭발이 전부 십자로 붕괴한다.** 그 하한을 여기서 못박는다.
        [Test]
        public void SelfBodyRadius_HasAHardFloor_OrRadius1BlastsCollapse()
        {
            Assert.GreaterOrEqual(Wassup.Skills.SkillMath.CellShapePaddingTiles, 0.4143f,
                "√2 − 1 = 0.4142 미만이면 반경 1 폭발이 대각 이웃을 잃고 **십자 모양**이 된다 "
                + "(unit 4b 가 이미 한 번 밟은 함정). 저작된 반경 1 AoE 20건이 여기 달려 있다.");
        }

        // ─── 착지 예고(궁극기 강습) — **점 + 거리** (2026-09-07 사용자 결정) ───
        //
        // 착지 좌표를 중심으로 한 **원 하나**를 그린다(운석과 같은 함수 `PinSkillTelegraph`).
        // 반경 = `N + 칸 반폭`, **대상 몸은 안 더한다** — 그건 그 유닛의 **그림자**가 말한다.
        //
        // ⚠ **칸을 열거하던 시절의 테스트는 전부 지웠다.** 「점에서 계산한 칸 집합」은 「점 + 거리」를
        //   이산 격자로 **양자화**하는 것이고, 그 양자화가 몸 있는 유닛을 예고 밖에서 맞게 만들었다
        //   (실측 32곳). 매체가 원이 되면서 그 오차가 **정의상 사라진다** — 아래가 그 증명이다.
        private const float LandingRing = 2f + 0.5f;   // 라이브 N=2 · `PinCenteredRange` 의 반경

        [Test]
        public void LandingTelegraphRing_RadiusIsRangePlusCellHalfWidth()
        {
            Assert.AreEqual(LandingRing, 2f + Wassup.Skills.SkillMath.CellShapePaddingTiles, 1e-6f,
                "링 반경이 `N + 칸 반폭` 이 아니다 — 이 값이 판정의 원점 항과 갈리면 "
                + "아래 「그림자가 링에 닿으면 걸린다」가 더는 판정식과 동치가 아니다");
        }

        // ★ **이 게임이 표기에서 몸을 빼도 되는 이유** — 사용자 결정 2026-09-02 D6.
        //
        //     |Δ| ≤ N + 0.5 + 대상 몸   ⟺   반경 «대상 몸» 인 그림자가 반경 «N + 0.5» 인 링에 닿는다
        //
        // 좌변은 피해 판정(`ReachFromImpact`, 자리형이라 원점 0 → 칸 반폭 승격), 우변은 플레이어가
        // 화면에서 읽는 것이다. **둘이 동치**라서 링 하나로 모든 몸 크기를 정확히 가르칠 수 있다.
        // 칸 채움에는 이 성질이 없다 — 걸치는 띠가 이진값으로 접히기 때문이다.
        [TestCase(0f)]      // 점(몸 없는 가상 대상)
        [TestCase(0.25f)]   // 적 소
        [TestCase(0.5f)]    // 버스터즈 1×2 · 적 중
        [TestCase(1.0f)]    // 2×2 (로스터 25종) · 캐논
        [TestCase(1.5f)]    // 배스티온 3×2
        public void ShadowTouchingTheRing_IsExactlyTheDamagePredicate(float bodyR)
        {
            for (int step = 0; step <= 80; step++)
            {
                float d = step * 0.1f;   // 중심에서의 거리(타일)
                bool hit = Wassup.Skills.SkillMath.ReachFromImpact(d, 0f, 2, 0f, bodyR);
                bool shadowTouchesRing = d <= LandingRing + bodyR;
                Assert.AreEqual(shadowTouchesRing, hit,
                    $"몸 {bodyR} · 거리 {d:0.0} — 「그림자가 링에 닿는다」와 피해 판정이 갈렸다. "
                    + "이 동치가 깨지면 링 하나로 몸을 가르칠 수 없고, 표기에 몸을 다시 들여야 한다");
            }
        }

        // ⚠ **「옛 무경고 표본이 링에서는 읽힌다」 테스트는 지웠다**(리뷰 M-1).
        //   단언 넷 중 둘이 `3.0f <= LandingRing + 1.5f` 같은 **컴파일 타임 상수 산술**이라
        //   프로덕션 심볼을 하나도 안 지났고, 나머지 둘은 위 `ShadowTouchingTheRing` 의
        //   커버리지 부분집합이었다(bodyR 1.5·1.0 에서 이미 전 거리 스윕). 정보 증분 ≈ 0.
        //   대조군(타일 매체)이 삭제돼 「32곳이 0 이 됐다」를 코드로 표현할 방법이 없다 —
        //   그 서사는 `24_landing_telegraph_round.md` 가 진다.

    }
}
