using NUnit.Framework;
using Unity.Mathematics;
using Wassup.Battle.Combat;
using Wassup.Skills;

namespace Wassup.Tests.EditMode
{
    // 사거리 술어 회귀 — **절대값**을 못박는다(상대 불변식은 `RangePredicateInvariantsTests`).
    //
    // 이 함수의 값어치는 «규칙이 하나»라는 데 있다 — 공격(AttackSystem)·정지
    // (EnemyAiStateSystem)·이동(PatrolAreaMath)이 같은 답을 받아야 «멈추는데 못 때리는»
    // 교착이 안 생긴다(2026-08-12 실측 182프레임).
    //
    // ⚠ **distance-based-range unit 4a 로 자가 바뀌었다.** 전에는 셀 체비셰프 + 조건부 월드
    // 체비셰프였고, 지금은 **몸 기준 간격** 하나다:
    //     v = max(|Δ| − 0.5, 0)  ·  안 ⟺ |v|² ≤ (사거리 + 대상반경)²
    // 아래 「사거리 2의 대각이 빠진다」가 그 변화의 얼굴이다 — 체비셰프의 정사각형이
    // 둥근 모서리가 됐다. 그게 이 spec 이 의도한 것이고, 뒤집으려면 spec 을 먼저 고친다.
    public class AttackReachTests
    {
        // unit 9 — 몸은 이제 **양쪽이 각자 들고 온다**(상수 0.5 은퇴). 일반 유닛의 저작 기본값이
        // 0.25 이고 둘이 만나면 0.5 라, 아래 기대값은 상수 시절과 **전부 같다** — 그게
        // `b = 0.25` 를 고른 이유다(의미는 바로잡고 밸런스는 안 움직인다).
        private const float Body = 0.25f;
        private const float Tile = 1f;
        private static float3 At(float x, float z) => new float3(x, 0f, z);
        private static bool R(float ax, float az, float bx, float bz, int range, float body = Body)
            => AttackReach.InReach(At(ax, az), At(bx, bz), range, Tile, Body, body);
        // unit 9 — 비정수 사거리용. `range` 는 무시되고 `rangeF` 가 쓰인다.
        private static bool R(float ax, float az, float bx, float bz, int range,
                              float body, float rangeF)
            => AttackReach.InReach(At(ax, az), At(bx, bz), rangeF, Tile, Body, body);

        [Test]
        public void Range1_KeepsAllEightNeighbours_IncludingDiagonal()
        {
            // 대각 인접: |Δ|=(1,1) → v=(0.5,0.5) → |v|=0.707 ≤ 1. **자를 바꿔도 유지된다** —
            // 「대각 인접도 사거리 1」은 격자 게임의 기존 계약이고 이 spec 이 지킨다.
            Assert.IsTrue(R(3, 3, 4, 3, 1), "축 인접");
            Assert.IsTrue(R(3, 3, 4, 4, 1), "대각 인접");
            Assert.IsFalse(R(3, 3, 5, 3, 1), "두 칸");
            Assert.IsFalse(R(3, 3, 5, 5, 1), "두 칸 대각");
        }

        [Test]
        public void Range2_LosesTheCorner_ThisIsTheIntendedChange()
        {
            // ★ **자가 바뀐 얼굴.** 옛 셀 체비셰프는 (2,2)를 「사거리 2」로 봤다.
            // 몸 기준: |Δ|=(2,2) → v=(1.5,1.5) → |v|=2.12 > 2 → **빠진다.**
            // 정사각형이던 사거리가 둥근 모서리가 된 것이고, 허용 면적 −9.5% 의 정체다.
            Assert.IsTrue(R(0, 0, 2, 0, 2), "축 2칸은 유지");
            Assert.IsTrue(R(0, 0, 2, 1, 2), "얕은 대각(2,1)은 유지 — v=(1.5,0.5) → 1.58");
            Assert.IsFalse(R(0, 0, 2, 2, 2), "정대각 모서리는 빠진다 — 의도된 변화");
        }

        [Test]
        public void ZeroRange_ReachesWithinOwnBody()
        {
            // 사거리 0 = 「내 몸에 닿는 것」. 자기 자리는 물론, 자기 상자 안(±0.5)이면 닿는다.
            // 광역·자가버프가 이 성질에 기댄다.
            Assert.IsTrue(R(3, 3, 3, 3, 0));
            Assert.IsTrue(R(3, 3, 3.5f, 3, 0), "상자 경계");
            Assert.IsFalse(R(3, 3, 4, 3, 0), "한 칸 밖");
        }

        [Test]
        public void TileLockedPair_KeepsItsSlack()
        {
            // 타일 고정 유닛은 칸 안에서 한쪽만 밀린다. 종전 상한이 1.49칸이었고
            // 몸 기준에서도 |Δ|=1.49 → v=0.99 ≤ 1 로 **닿는다.**
            Assert.IsTrue(R(3, 3, 4.49f, 3, 1));
            Assert.IsFalse(R(3, 3, 4.51f, 3, 1), "1.51 → v=1.01 → 빠진다");
        }

        [Test]
        public void ContinuousPair_TwoTileSeparation_IsRejected()
        {
            // ★ 이 spec 의 원래 증상: 셀은 인접(1칸)인데 실제 1.98칸이라 사거리 1이
            // 2칸처럼 보이던 자리. 이제 셀을 아예 안 보므로 구조적으로 사라진다.
            Assert.IsFalse(R(2.51f, 3f, 4.49f, 3f, 1));
        }

        [Test]
        public void SameMetricRegardlessOfWhoAsks()
        {
            // ⚠ **`bothContinuous` 가 사라진 것이 이 unit 의 핵심이다.** 전에는 같은 두 위치가
            // 「누가 묻느냐」에 따라 다르게 판정됐다(타일 고정은 셀만, 연속은 셀+월드).
            // 지금은 입력이 위치뿐이라 그 갈림이 **표현 불가능**하다 — 이 테스트는 그 사실을
            // 시그니처로 못박는다(인자가 없으니 다르게 물을 방법이 없다).
            Assert.IsTrue(R(3f, 3f, 4.4f, 3f, 1));
            Assert.IsTrue(R(3.4f, 3f, 3.9f, 3f, 1));
        }

        [Test]
        public void TargetBody_WidensReach_ByExactlyItsRadius()
        {
            // 대상의 몸은 **사거리에 더해진다**(원과 상자의 민코프스키 합).
            // 큰 몸 = 큰 표적 — unit 3 이 만든 축이고 unit 9 가 양쪽으로 대칭화했다.
            //
            // ⚠ **unit 9 로 절대값이 이동했다**(의도는 그대로): 상한 = 사거리 + 내몸 + 상대몸.
            // 자기 몸이 상수 0.5 → 저작 0.25 로 줄어 0.9 몸 대상의 상한이 2.4 → **2.15** 다.
            // 「대상 몸이 정확히 그 반지름만큼 넓힌다」는 성질은 아래 두 쌍이 진다.
            Assert.IsFalse(R(0, 0, 2.3f, 0, 1, body: 0f), "대상이 점이면 상한 1.25 — 2.3 은 밖");
            Assert.IsTrue(R(0, 0, 1.2f, 0, 1, body: 0f), "점 대상도 상한 1.25 안은 닿는다");
            // 몸 0.9 를 주면 상한이 **정확히 0.9 만큼** 올라간다: 1.25 → 2.15.
            Assert.IsTrue(R(0, 0, 2.1f, 0, 1, body: 0.9f), "몸 0.9 → 상한 2.15 안");
            Assert.IsFalse(R(0, 0, 2.3f, 0, 1, body: 0.9f), "2.3 은 그 상한 밖 — 딱 0.9 만 늘었다");
        }

        // ── unit 9: 사거리는 **연속 반지름**이다 ────────────────────────────
        // 시트의 `2` 는 「타일 2개 거리」이고 `0.1` 은 「타일 길이의 0.1」이다(사용자 결정
        // 2026-09-01). 정수일 이유가 없다 — 양자화(`(int)(r+0.5)`)가 판정 경로에서 빠졌다.
        [Test]
        public void FractionalRange_IsNotQuantized()
        {
            // 2.5 는 3 으로 반올림되지 않는다. 상한 = 2.5 + 0.25 + 0.25 = 3.0.
            Assert.IsTrue(R(0, 0, 2.95f, 0, 0, body: Body, rangeF: 2.5f), "2.95 ≤ 3.0");
            Assert.IsFalse(R(0, 0, 3.05f, 0, 0, body: Body, rangeF: 2.5f), "3.05 > 3.0 — 3 으로 반올림됐다면 닿았을 것");

            // 아주 작은 반지름도 살아 있다. 옛 `(int)(0.1+0.5)` 는 **0** 이었다.
            Assert.IsTrue(R(0, 0, 0.55f, 0, 0, body: Body, rangeF: 0.1f), "0.55 ≤ 0.1+0.5");
            Assert.IsFalse(R(0, 0, 0.65f, 0, 0, body: Body, rangeF: 0.1f), "0.65 > 0.6");
        }

        // 격자 파생값은 **덮는 쪽**이다 — 모자라면 쏠 수 있는 칸이 BFS 소스에서 빠져
        // 적이 더 멀리서 멈춘다(unit 4c 가 고친 동결의 사촌).
        [Test]
        public void GridDerivedTiles_CeilsToCover()
        {
            Assert.AreEqual(3, Wassup.Battle.Movement.GridMath.RangeToTiles(2.4f), "2.4 → 3 (덮는다)");
            Assert.AreEqual(3, Wassup.Battle.Movement.GridMath.RangeToTiles(3f), "정수는 그대로");
            Assert.AreEqual(1, Wassup.Battle.Movement.GridMath.RangeToTiles(0.1f), "0.1 → 1, 0 이 아니다");
            // 두 벌이 갈리면 이전한 스킬과 안 한 스킬이 다른 칸을 고른다.
            for (float r = 0f; r <= 5f; r += 0.1f)
                Assert.AreEqual(Wassup.Battle.Movement.GridMath.RangeToTiles(r),
                                Wassup.Skills.SkillMath.RangeToTiles(r), $"r={r:F1} 에서 두 구현이 갈렸다");
        }

        [Test]
        public void LongRange_ScalesLinearly()
        {
            Assert.IsTrue(R(0, 0, 4.4f, 0, 4), "v=3.9 ≤ 4");
            Assert.IsFalse(R(0, 0, 4.6f, 0, 4), "v=4.1 > 4");
        }

        [Test]
        public void TileSize_IsTheOnlyWorldConversion()
        {
            // 술어는 타일 단위만 안다. 월드→타일 환산은 `AttackReach` 한 곳에서만 일어난다.
            Assert.IsTrue(AttackReach.InReach(At(0, 0), At(2.9f, 0), 1, 2f, Body, Body), "2칸 타일: 2.9/2=1.45 → v=0.95");
            Assert.IsFalse(AttackReach.InReach(At(0, 0), At(3.1f, 0), 1, 2f, Body, Body), "3.1/2=1.55 → v=1.05");
        }

        [Test]
        public void IsSymmetric_WhenBodiesMatch()
        {
            // 비대칭이면 «A는 B를 때리는데 B는 A를 못 때리는» 상태가 생긴다.
            // ⚠ 몸이 다르면 **의도적으로 비대칭**이다(큰 몸은 맞기 쉽고 때리기는 같다) —
            // 그래서 이 단언은 「같은 몸끼리」로 좁혀져 있다.
            var pa = At(2.3f, 7.1f); var pb = At(4.2f, 5.8f);
            Assert.AreEqual(AttackReach.InReach(pa, pb, 2, Tile, Body, Body),
                            AttackReach.InReach(pb, pa, 2, Tile, Body, Body));
        }

        [Test]
        public void CellRange_IsStillChebyshev_ForTheMovementLayerOnly()
        {
            // `InCellRange` 는 사거리 판정이 아니라 **격자 계층의 자**로 남았다 —
            // 추격 필드 소스 수집이 셀 디스크라(결정 4) 순찰 이동이 그와 같은 자를 봐야 한다.
            Assert.IsTrue(AttackReach.InCellRange(new int2(0, 0), new int2(2, 2), 2), "체비셰프는 모서리를 포함");
            Assert.IsFalse(R(0, 0, 2, 2, 2), "같은 자리를 사거리 술어는 제외 — 두 자가 다르다는 사실 자체를 고정");
        }

        // ── rev 2: 1×1 끼리는 **진짜 원**이다 ──────────────────────────
        //
        // rev 1 은 한 칸을 정사각형으로 봐서 경계가 「직선 4개 + 호 4개」였다. 둘레의 13.7% 가
        // 직선이고 하필 상하좌우 정중앙이라 **「원이 아니라 라운딩된 사각형」으로 읽혔다**
        // (사용자 지적 2026-08-31). 반지름 비(대각/축 1.046)로는 안 잡히는 문제였다 —
        // 눈이 보는 것은 반지름이 아니라 **곡률이 끊기는 지점**이다.
        //
        // 그래서 이 테스트는 반지름 비가 아니라 **모든 방향에서 도달 반지름이 같은가**를 본다.
        // 직선 구간이 하나라도 생기면 그 방향의 반지름이 달라져 바로 빨개진다.
        [Test]
        public void OneByOne_ReachBoundary_IsACircle_NoFlatSides()
        {
            const int range = 4;
            float expected = range + Body + Body;   // 4.5 — 양쪽 몸 합이 상수 시절 0.5 와 같다
            for (int deg = 0; deg < 360; deg += 5)
            {
                float rad = deg * math.PI / 180f;
                float cx = math.cos(rad), cz = math.sin(rad);
                // 경계 바로 안쪽은 닿고, 바로 바깥쪽은 안 닿아야 한다 — **모든 방향에서**.
                Assert.IsTrue(R(0, 0, cx * (expected - 0.02f), cz * (expected - 0.02f), range),
                    $"{deg}° 에서 경계 안쪽이 안 닿는다 — 이 방향의 반지름이 {expected} 보다 작다");
                Assert.IsFalse(R(0, 0, cx * (expected + 0.02f), cz * (expected + 0.02f), range),
                    $"{deg}° 에서 경계 바깥이 닿는다 — 이 방향의 반지름이 {expected} 보다 크다 "
                    + "(직선 구간이 생긴 것이다 = 라운딩된 사각형으로 돌아갔다)");
            }
        }

        // ── rev 3 (2026-09-01 외부 세션): 몸 = 원 ─────────────────────────────
        // rev 2 의 사각 몸 테스트 3건(NonSquareBody / BothBodies_HalfExtentsAdd /
        // MultiCellBody_KeepsTheFlatSides)은 `InBodyReachWithHalfExtent` 와 함께 은퇴했다.
        // 원 모델의 계약: 도달은 **전방향 동일**(range + selfR + targetR)이고, 다칸의 크기는
        // 반폭이 아니라 **내접원 반경**(`min(W,H)/2` 파생식)으로 들어온다.
        [Test]
        public void BodiesAdd_AsRadii_Isotropically()
        {
            // 2×2 방어유닛(내접원 1.0)이 소형 적(0.25)을 볼 때: 상한 = 1 + 1.0 + 0.25 = 2.25.
            const float range = 1f, selfR = 1.0f, targetR = 0.25f;
            const float reach = range + selfR + targetR;
            Assert.IsTrue(SkillMath.InBodyReach(reach - 0.05f, 0f, range, selfR, targetR));
            Assert.IsFalse(SkillMath.InBodyReach(reach + 0.05f, 0f, range, selfR, targetR));
            // 전방향 동일 — 대각도 같은 반지름이다(사각 몸의 「변」이 사라졌다).
            float d = reach / 1.41421356f;
            Assert.IsTrue(SkillMath.InBodyReach(d - 0.05f, d - 0.05f, range, selfR, targetR));
            Assert.IsFalse(SkillMath.InBodyReach(d + 0.05f, d + 0.05f, range, selfR, targetR));
        }

        // unit 14 — 사각 도형 몸 걸침. 몸이 점이면 종전 「접지점 ∈ 사각」으로 퇴화하고,
        // 몸이 있으면 그 반경만큼 넓게 걸친다(민코프스키). 사거리와 같은 몸 값을 쓴다.
        [Test]
        public void SkillSquare_BodyOverlap_ExpandsByTargetRadius()
        {
            // 칸 조준 range 1 = 반폭 1.5 정사각. 소형 몸 0.25 는 변 밖 0.2 에서도 걸친다.
            Assert.IsTrue(SkillMath.BodyOverlapsSquare(1.70f, 0f, 1.5f, 0.25f));
            Assert.IsFalse(SkillMath.BodyOverlapsSquare(1.80f, 0f, 1.5f, 0.25f));
            // 모서리 밖 (1.65,1.65): 사각 밖 거리 0.212 ≤ 0.25 — 걸침. (1.72,1.72)=0.311 — 아님.
            Assert.IsTrue(SkillMath.BodyOverlapsSquare(1.65f, 1.65f, 1.5f, 0.25f));
            Assert.IsFalse(SkillMath.BodyOverlapsSquare(1.72f, 1.72f, 1.5f, 0.25f));
            // 2×2 방어유닛(내접원 1.0)이 변 밖 0.9 — 몸이 반쯤 걸친 유닛이 버프를 받는다.
            Assert.IsTrue(SkillMath.BodyOverlapsSquare(2.40f, 0f, 1.5f, 1.0f));
            Assert.IsFalse(SkillMath.BodyOverlapsSquare(2.60f, 0f, 1.5f, 1.0f));
            // 몸 = 점이면 종전 셀 멤버십으로 퇴화.
            Assert.IsTrue(SkillMath.BodyOverlapsSquare(1.49f, 0f, 1.5f, 0f));
            Assert.IsFalse(SkillMath.BodyOverlapsSquare(1.51f, 0f, 1.5f, 0f));
        }

        // 파생식이 정의 그대로인지 — 표(저작)가 아니라 식이라 신규 footprint 에 자동 적용된다.
        [Test]
        public void DerivedBody_IsInscribedCircle_MinOfFootprint()
        {
            var def = UnityEngine.ScriptableObject.CreateInstance<Wassup.Data.DefenderUnitData>();
            try
            {
                Assert.AreEqual(0.5f, def.BodyRadiusTiles, 1e-6f, "1×1 = 내접원 0.5");
                def.footprintWidth = 2; def.footprintHeight = 2;
                Assert.AreEqual(1.0f, def.BodyRadiusTiles, 1e-6f, "2×2 = 1.0");
                def.footprintWidth = 2; def.footprintHeight = 3;
                Assert.AreEqual(1.0f, def.BodyRadiusTiles, 1e-6f,
                    "2×3 = min/2 = 1.0 — 장축 끝은 몸 밖(원 근사, 지각 임계 아래로 수용)");
            }
            finally { UnityEngine.Object.DestroyImmediate(def); }
        }

    }
}
