namespace Wassup.Skills
{
    // skill-layer-foundation unit 5 — 도메인이 쓰는 순수 계산.
    //
    // Runtime 쪽에 같은 일을 하는 것들이 있다(`GridMath.ChebyshevDistance`·
    // `RangeToTiles`, `AoeTargetCap.SelectNearest`). 그걸 못 부르는 이유는 이 어셈블리가
    // Entities 를 참조하지 않아서다 — 그쪽은 `NativeArray` 시그니처를 쓴다.
    //
    // ⚠ 그래서 **알고리즘이 둘**이다. 이전이 끝난 지금도 Runtime 쪽 소비자는 0 이
    // 아니다 — 스킬 밖(공격 해결·투사체·해저드)이 여전히 그쪽을 쓴다. 즉 이 이중성은
    // 「이전이 끝나면 사라지는 것」이 아니라 **asmdef 경계가 있는 한 남는 것**이다.
    // 그래서 **둘이 같은 답을 내야 한다** — 어긋나면 같은 「N칸 안」이 경로에 따라
    // 다른 대상을 고른다.
    // `SkillMathParityTests` 가 그것을 고정한다 — **실재하는 테스트다**(리뷰가
    // 「주석은 있는데 테스트가 없다」를 잡아서 만들었다).
    public static class SkillMath
    {
        // 체비셰프(킹 무브) 거리 — **격자 통계·배치 인접 전용으로 남았다.** 전투 판정의 「N칸 안」은
        // 전부 원(`InBodyReach`)이다(distance-based-range · attach-range-preview 0a). 광역 도형의 사각 자는
        // 은퇴했고(`RangeMetric.Chebyshev` Obsolete), 술어 본체 `BodyOverlapsSquare` 만 보존한다.
        public static int ChebyshevDistance(int ax, int ay, int bx, int by)
        {
            int dx = ax > bx ? ax - bx : bx - ax;
            int dy = ay > by ? ay - by : by - ay;
            return dx > dy ? dx : dy;
        }

        // 사거리 → 타일 수. **`GridMath.RangeToTiles` 와 규칙이 같아야 한다.**
        //
        // ⚠ half-away-from-zero 반올림이다(`math.round` 의 banker's rounding 회피).
        // 처음에 `(int)(range / tileSize)` 로 **버림**을 썼다가 리뷰가 잡았다 —
        // 오늘은 저작 사거리가 전부 정수라 답이 같지만, 소수 하나가 시트로 들어오는
        // 순간 이전한 스킬과 안 한 스킬이 다른 대상을 고른다.
        //
        // ⚠ tileSize 를 안 받는다 — 원본도 안 받는다(사거리가 이미 타일 단위 축이다).
        // 나누는 순간 두 구현이 갈린다.
        // unit 9 — `GridMath.RangeToTiles` 와 같은 규칙(**ceil**, 격자 전용).
        // `math` 를 안 부르는 이유는 이 어셈블리가 Unity.Mathematics 없이 컴파일되기 때문.
        public static int RangeToTiles(float range)
            => range <= 0f ? 0 : (int)range + ((int)range == range ? 0 : 1);

        // 거리² 오름차순으로 가까운 것부터 `cap` 개를 고른다.
        //
        // **결정론이 계약이다.** 거리가 같으면 **인덱스가 작은 쪽**이 이긴다 —
        // 그래야 같은 판이 같은 답을 낸다. 삽입 정렬인 이유는 n 이 작기 때문이다
        // (반경 안 후보는 수십 개 수준이고, 발동은 초당 수 회다).
        //
        // `cap <= 0` = 전부. 투사체 폭발과 같은 규약이다.
        public static int SelectNearest(float[] distSq, int count, int cap, int[] into)
        {
            int want = cap <= 0 ? count : (cap < count ? cap : count);
            if (want > into.Length) want = into.Length;

            int n = 0;
            for (int i = 0; i < count; i++)
            {
                float d = distSq[i];
                // 넣을 자리 찾기 — 동률이면 **뒤에** 넣어 인덱스 작은 쪽을 보존한다.
                int pos = n;
                while (pos > 0 && distSq[into[pos - 1]] > d) pos--;
                if (pos >= want) continue;

                int last = n < want ? n : want - 1;
                for (int k = last; k > pos; k--) into[k] = into[k - 1];
                into[pos] = i;
                if (n < want) n++;
            }
            return n;
        }

        // ── 사거리 술어 (distance-based-range unit 4a · rev 3) ──────────────
        //
        // **몸과 몸 사이의 빈틈**이 자다. rev 3(2026-09-01 외부 세션)에서 몸이 **원 하나**로
        // 회귀했다 — 본문과 이력은 아래 `InBodyReach` 헤더 참조.
        // ⚠ **unit 9 에서 이 상수가 둘로 갈렸다.** 원래 하나(`SelfBodyRadiusTiles = 0.5`)가
        // 두 가지 뜻을 겸직했고, 「몸은 유닛별 저작」으로 가는 순간 그 둘이 서로 다른 값이 된다:
        //
        //   · **사거리** — 「공격자의 몸」. 이제 `bodyRadius` 저작에서 온다(일반 0.25).
        //     상수가 아니므로 여기 없다. 술어가 인자로 받는다(rev 3: 방어유닛은 파생식).
        //   · **광역**   — 「후보 **칸**의 반폭」. 폭발은 점이고 후보가 칸이라 칸의 크기가
        //     붙는다. 칸은 언제나 1타일이므로 **0.5 로 남는다.**
        //
        // ⚠ **광역을 유닛 몸(0.25)으로 바꾸지 말 것.** 반경 1 폭발이 대각을 통째로 잃어
        // **십자 모양**이 된다(1.414 > 1.25) — rev 1 에서 순수 원으로 갔다가 되돌린 그 회귀다.
        public const float CellHalfWidthTiles = 0.5f;

        // attach-range-preview 0a(리뷰 H-1) — `RangeMetric` 이 술어에 더하는 **시전자 쪽 반폭**.
        // 어댑터(`EcsSkillContext.Collect`)와 페이크(`TestSkillContext`)가 **같은 함수**를 부른다 — 페이크가
        // 매핑을 재구현하면 폴백 방향이 갈려(페이크 fail-open / 라이브 fail-closed) 도메인 테스트가 초록인데
        // 라이브가 다르다. 반환 false = 은퇴한/미지의 자 → 호출부는 **후보 0** 으로 접는다(fail-closed).
        //   AreaCircle → 칸 반폭(광역: 반경 N + 0.5 + 대상 몸) · Euclidean → 0(사거리·탄 비행 거리: N + 대상 몸).
        public static bool TryShapeHalfWidth(RangeMetric metric, out float halfWidthTiles)
        {
            switch (metric)
            {
                case RangeMetric.AreaCircle: halfWidthTiles = CellHalfWidthTiles; return true;
                case RangeMetric.Euclidean: halfWidthTiles = 0f; return true;
                default: halfWidthTiles = 0f; return false;
            }
        }

        // **표준 소형 상대의 몸 반지름** = 적 티어 「소」. `AttackUnitData` 의 저작
        // 기본값과 같은 값이고(드리프트는 `RangeDisplayContractTests` 가 잡는다),
        // **표기가 「누구를」 기준으로 그리는지의 답**이기도 하다.
        //
        // ⚠ 링·프리뷰는 대상을 **점으로 보면 안 된다.** 도달 = 사거리 + 내몸 + 상대몸인데
        // 상대를 0 으로 두면 화면이 실제보다 0.25칸 좁아지고, 사거리 1 에서는 **대각 4칸이
        // 통째로 빠진다**(1.414 > 1.25). 「대각 인접도 사거리 1」은 이 게임의 오래된 계약이라
        // 화면이 그것을 부정하면 규칙을 **틀리게** 가르치는 것이다(unit 5 의 존재 이유).
        // 그래서 표기는 **표준 1×1 상대**를 가정해 그린다. 보스처럼 몸이 큰 상대는 링보다
        // 멀리서도 맞는데, 그건 대상 마크(unit 7)가 말한다.
        public const float StandardBodyRadiusTiles = 0.25f;

        // **술어 본문은 여기 하나뿐이다** — rev 3(2026-09-01 외부 세션): 몸 = 원, edge-to-edge.
        //
        //     안 ⟺ |Δ|² ≤ (range + selfR + targetR)²
        //
        // 반경의 출처: 방어유닛 = footprint `가로/2` **파생식**(rev 2026-09-04 — 열만, 행 배제 · 저작 없음) ·
        // 적 = 티어 저작(소 0.25 / 중 0.5 / 대 1.0 / 보스 개별, unit 13) · 구조물 = 점유 내접원.
        //
        // 이력(같은 자리에서 세 번 바뀌었다 — 네 번째가 오면 여기부터 읽을 것):
        //   rev 1  `max(|Δ|−0.5, 0)` — 칸을 정사각으로. 경계 13.7% 가 직선이라 눈에 걸렸다.
        //   rev 2  사각 반폭 ⊕ 원(`InBodyReachWithHalfExtent`) — 다칸 몸을 사각으로.
        //          **폐기 사유**: 몸 크기·모양이 유닛마다 달라 「사거리 N」의 실거리가 상대별
        //          무한 조합이 됐고, 링이 그 조합을 그릴 수 없었다(최대 67% 과소 표기).
        //          원 + 파생식이면 캐리어(그림자·링)가 판정과 1:1 이 된다(계약 1 rev 3).
        //          BC1055 우회(`float2` 반폭 인자)도 사각 항과 함께 소멸했다.
        //   rev 3  원 하나(현행). 「그림자가 진실이다」 — 그림자 반경 = targetR,
        //          링 반경 = range + selfR 이라 「그림자가 링에 닿으면 안」이 판정식과 동치다.
        //
        // ⚠ **sqrt 를 쓰지 않는다.** 제곱 비교로 끝난다 — sim 핫 경로이고 부동소수 반올림이
        // 한 번 덜 끼는 편이 결정론에 유리하다.
        // ⚠ 단위는 **타일**이다. 월드 좌표를 넣지 말 것 — 호출부가 `tileSize` 로 나눠서 준다.
        // ⚠ unit 9 — **오차 보정 항이 없다.** 양쪽 몸이 저작/파생에서 오고 그 수치를 그대로 믿는다.
        public static bool InBodyReach(float dxTiles, float dzTiles, float rangeTiles,
                                       float selfBodyRadiusTiles, float targetBodyRadiusTiles)
        {
            float reach = rangeTiles + selfBodyRadiusTiles + targetBodyRadiusTiles;
            return dxTiles * dxTiles + dzTiles * dzTiles <= reach * reach;
        }

        // unit 14 (결정 4 폐기, 2026-09-01 외부 세션) — 광역 **사각 도형**의 몸 걸침.
        // 도형 밖 거리(사각 SDF)가 대상 몸 반경 이하이면 걸친 것이다(자동 민코프스키 확장).
        // `halfTiles` = 도형 반폭 — 칸 단위 조준은 `range + CellHalfWidthTiles`(range 1 = 3×3).
        // 몸이 점(0)이면 「접지점 ∈ 사각」으로 정확히 퇴화한다 = 종전 셀 멤버십과 같은 집합.
        // ⚠ 원 도형은 함수가 따로 없다 — `InBodyReach(dx, dz, r, 0, targetR)` 가 그 식이다.
        public static bool BodyOverlapsSquare(float dxTiles, float dzTiles,
                                              float halfTiles, float bodyRadiusTiles)
        {
            float vx = (dxTiles < 0f ? -dxTiles : dxTiles) - halfTiles; if (vx < 0f) vx = 0f;
            float vz = (dzTiles < 0f ? -dzTiles : dzTiles) - halfTiles; if (vz < 0f) vz = 0f;
            return vx * vx + vz * vz <= bodyRadiusTiles * bodyRadiusTiles;
        }
    }
}
