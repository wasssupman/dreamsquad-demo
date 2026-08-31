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

        // ── 사거리 술어 (distance-based-range unit 4a) ──────────────────────
        //
        // **자가 바뀐 지점이다.** 전에는 두 단계였다 — 셀 체비셰프(1차) + 연속↔연속일 때만
        // 월드 체비셰프(2차). 그 구조가 만든 문제 셋:
        //   ① 「사거리 안」의 뜻이 **누가 묻느냐에 따라 달랐다** — 타일 고정 유닛은 셀만,
        //      연속 유닛은 셀+월드. 같은 두 유닛의 같은 거리가 경로에 따라 다르게 판정됐다.
        //   ② 몸이 없었다. 전부 중심점 대 중심점이라 스프라이트가 1.89배인 보스가
        //      몸통을 관통당해도 무판정이었다.
        //   ③ 셀 체비셰프는 **칸 경계에서 튄다** — 반 칸 움직였을 뿐인데 판정이 뒤집힌다.
        //
        // 새 자는 **몸과 몸 사이의 빈틈**이다:
        //
        //     v = max(|Δ| − 0.5, 0)        ← 성분별. 공격자 몸(1칸 정사각)의 반폭을 뺀다
        //     안 ⟺ |v|² ≤ (사거리 + 대상반경)²
        //
        // 읽는 법: 공격자를 **한 칸짜리 상자**로 보고 그 상자에서 대상 중심까지의 거리를 잰다.
        // 대상의 몸(`targetBodyRadius`)은 사거리에 더한다 — 원과 상자의 민코프스키 합이라
        // 「큰 몸 = 큰 표적」이 공짜로 나온다.
        //
        // **대각이 왜 여전히 닿나**: 대각 인접은 |Δ|=(1,1) → v=(0.5,0.5) → |v|=0.707 ≤ 1.
        // 사거리 1 의 여덟 이웃은 전부 유지된다. 다만 **사거리 2 의 대각**은 |v|=(1.5,1.5) →
        // 2.12 > 2 로 **빠진다** — 체비셰프가 정사각형이던 것이 둥근 모서리가 된 것이고,
        // 이 spec 이 의도한 변화다(허용 면적 −9.5%).
        //
        // **0.5 는 공격자에게만 붙는다.** 대상 쪽에도 반폭을 주면 유닛↔유닛 간격이 N+1.0 이 되어
        // 오늘(N+0.5)보다 **넓어진다** — 전 유닛이 1×1 이라(`FootprintWidthCells => 1`,
        // 방어유닛 저작도 1×1 로 철회됨) 대상 반폭이 정의상 0 이기 때문이다. 다칸 유닛이
        // 실제로 생기면 그때 `(w−1)*0.5` 를 이 뺄셈에 더한다 — **지금 인자로 만들지 않는다**
        // (항상 0 인 인자는 읽는 사람에게 「변한다」는 거짓 신호다).
        //
        // ⚠ **sqrt 를 쓰지 않는다.** 제곱 비교로 끝난다 — sim 핫 경로이고, 부동소수 반올림이
        // 한 번 덜 끼는 편이 결정론에 유리하다.
        //
        // 단위는 **타일**이다. 월드 좌표를 넣지 말 것 — 호출부가 `tileSize` 로 나눠서 준다.
        public const float SelfHalfWidthTiles = 0.5f;

        public static bool InBodyReach(float dxTiles, float dzTiles,
                                       float rangeTiles, float targetBodyRadiusTiles)
        {
            // `Unity.Mathematics` 를 부르지 않는다 — 이 파일은 그 참조 없이 컴파일되고,
            // M1 에서 netstandard 로 옮길 때 의존이 하나 적을수록 좋다.
            float vx = (dxTiles < 0f ? -dxTiles : dxTiles) - SelfHalfWidthTiles; if (vx < 0f) vx = 0f;
            float vz = (dzTiles < 0f ? -dzTiles : dzTiles) - SelfHalfWidthTiles; if (vz < 0f) vz = 0f;
            float reach = rangeTiles + targetBodyRadiusTiles;
            return vx * vx + vz * vz <= reach * reach;
        }

    }
}
