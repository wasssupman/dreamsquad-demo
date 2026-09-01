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
        // 체비셰프(킹 무브) 거리. 이 게임의 「N칸 안」은 전부 이 자다 —
        // 유일한 예외가 전방 발사의 유클리드다(포트의 `RangeMetric`).
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
        public static int RangeToTiles(float range) => (int)(range + 0.5f);

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

        // ── 사거리 술어 (distance-based-range unit 4a · rev 2) ──────────────
        //
        // **몸과 몸 사이의 빈틈**이 자다:
        //
        //     v = max(|Δ| − halfExtent, 0)          ← 성분별. 몸의 **사각 부분**만 뺀다
        //     안 ⟺ |v| ≤ range + SelfBodyRadius + targetBodyRadius
        //
        // ⚠ **rev 2 에서 0.5 가 뺄셈에서 덧셈으로 옮겨왔다**(사용자 지적 2026-08-31).
        // rev 1 은 공격자의 한 칸을 **정사각형**으로 봐서 `max(|Δ| − 0.5, 0)` 이었다. 그러면
        // 경계가 「직선 4개 + 호 4개」가 되고 **둘레의 13.7% 가 직선**이다 — 하필 상하좌우
        // 정중앙이라 눈이 곡률 끊김을 바로 잡아낸다(「원이 아니라 라운딩된 사각형」).
        // 반지름 비(대각/축 = 1.046)로는 이 문제가 안 보인다 — **틀린 것을 재고 있었다.**
        //
        // 지금은 한 칸을 **내접원(반지름 0.5)** 으로 본다 → 1×1 끼리는 `|Δ| ≤ R + 0.5` = **진짜 원**.
        // 바뀌지 않은 것: 축 방향 사거리(R+0.5 그대로) · 사거리 1의 여덟 이웃(1.414 ≤ 1.5).
        // 바뀐 것: 얕은 대각 칸 일부가 빠진다(R≥3 에서 (3,2)·(4,3) 등).
        //
        // ⚠ **`halfExtent` 는 죽지 않았다.** 다칸 유닛의 몸은 여전히 사각이고(반폭 `(w−1)/2`),
        // 거기에 0.5 원이 더해지는 형태다 — 1×1 이면 halfExtent 가 0 이라 순수 원이 된다.
        // 셰이더도 같은 식이라 파라미터 값만 달라진다(`_HalfExtent`=0, `_Range`=R+0.5).
        //
        // **왜 처음부터 원이 아니었나**: rev 1 의 0.5 는 옛 `CellSlackTiles`(격자 정의에서 나온
        // 슬랙)를 그대로 보존하려던 값이라 «칸 = 사각형» 을 따라갔다. 사거리는 **거리**이므로
        // 몸을 원으로 보는 편이 뜻에도 맞다.
        //
        // ⚠ **sqrt 를 쓰지 않는다.** 제곱 비교로 끝난다 — sim 핫 경로이고 부동소수 반올림이
        // 한 번 덜 끼는 편이 결정론에 유리하다.
        //
        // 단위는 **타일**이다. 월드 좌표를 넣지 말 것 — 호출부가 `tileSize` 로 나눠서 준다.
        // ⚠ **unit 9 에서 이 상수가 둘로 갈렸다.** 원래 하나(`SelfBodyRadiusTiles = 0.5`)가
        // 두 가지 뜻을 겸직했고, 「몸은 유닛별 저작」으로 가는 순간 그 둘이 서로 다른 값이 된다:
        //
        //   · **사거리** — 「공격자의 몸」. 이제 `bodyRadius` 저작에서 온다(일반 0.25).
        //     상수가 아니므로 여기 없다. 술어가 인자로 받는다.
        //   · **광역**   — 「후보 **칸**의 반폭」. 폭발은 점이고 후보가 칸이라 칸의 크기가
        //     붙는다. 칸은 언제나 1타일이므로 **0.5 로 남는다.**
        //
        // ⚠ **광역을 유닛 몸(0.25)으로 바꾸지 말 것.** 반경 1 폭발이 대각을 통째로 잃어
        // **십자 모양**이 된다(1.414 > 1.25) — rev 1 에서 순수 원으로 갔다가 되돌린 그 회귀다.
        public const float CellHalfWidthTiles = 0.5f;

        // 오늘 전 유닛이 1×1 이라 사각 반폭은 0 이다(`FootprintWidthCells => 1`,
        // 방어유닛 저작도 1×1 로 철회됨). 다칸 유닛이 실제로 생기면 `(w−1)*0.5` 를 넘긴다.
        public const float SelfHalfExtentTiles = 0f;

        // **술어 본문은 여기 하나뿐이다.** 다칸 몸을 명시로 받는 형태이고, 아래 `InBodyReach` 는
        // 오늘의 저작(전 유닛 1×1 → 반폭 0)을 넣은 **특수화**일 뿐이다.
        // ⚠ 본문을 복제하지 말 것 — 사거리 술어가 두 벌이 되는 것이 이 spec 이 없애려던 문제다.
        //
        // 오늘 이 오버로드의 호출부는 없다(전 유닛 1×1). **테스트가 계약을 진다** —
        // `halfExtent` 가 살아 있다는 것, 그리고 다칸 몸은 원이 아니라는 것.
        public static bool InBodyReachWithHalfExtent(float dxTiles, float dzTiles,
                                                     float halfExtentTiles,
                                                     float rangeTiles,
                                                     float selfBodyRadiusTiles,
                                                     float targetBodyRadiusTiles)
        {
            // `Unity.Mathematics` 를 부르지 않는다 — 이 파일은 그 참조 없이 컴파일되고,
            // M1 에서 netstandard 로 옮길 때 의존이 하나 적을수록 좋다.
            float vx = (dxTiles < 0f ? -dxTiles : dxTiles) - halfExtentTiles; if (vx < 0f) vx = 0f;
            float vz = (dzTiles < 0f ? -dzTiles : dzTiles) - halfExtentTiles; if (vz < 0f) vz = 0f;
            // unit 9 — **오차 보정이 없다.** 양쪽 몸이 저작에서 오고 그 수치를 그대로 믿는다.
            float reach = rangeTiles + selfBodyRadiusTiles + targetBodyRadiusTiles;
            return vx * vx + vz * vz <= reach * reach;
        }

        // 오늘의 저작을 넣은 특수화. 1×1 이라 반폭 0 → `|Δ| ≤ range + 내몸 + 상대몸` = **원**.
        public static bool InBodyReach(float dxTiles, float dzTiles, float rangeTiles,
                                       float selfBodyRadiusTiles, float targetBodyRadiusTiles)
            => InBodyReachWithHalfExtent(dxTiles, dzTiles, SelfHalfExtentTiles,
                                         rangeTiles, selfBodyRadiusTiles, targetBodyRadiusTiles);

    }
}
