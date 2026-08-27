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
    }
}
