namespace Wassup.Skills
{
    // skill-layer-migration unit 5b — **누구에게 실드를 줄까.**
    //
    // `Wassup.Battle.Effects.ShieldTargeting` 에서 이사했다(`SkillAim` 과 같은 이사).
    // 규칙도 상수도 부등호도 그대로이고 **그릇만** 바뀐다 — 도메인은 `NativeArray` 를
    // 모르므로 plain 배열 + 개수로 받는다.
    public enum SkillShieldFilter : byte
    {
        Self = 0,       // 자기만
        Nearest = 1,    // 가까운 순
        MostHurt = 2,   // 실효 체력(HP+실드)이 낮은 순
    }

    public static class SkillShieldSelect
    {
        // 고른 후보의 인덱스를 `into` 에 담고 개수를 돌려준다.
        //
        // ⚠ **동률은 인덱스 오름차순이 이긴다**(strict `<`). 그것이 결정론의 축이고,
        // 비동기 토너먼트의 양쪽 시뮬이 같은 답을 내는 이유다 — 바꾸면 조용히 갈린다.
        public static int Select(
            SkillShieldFilter filter, int targetCount, int selfIndex,
            float[] distanceSq, float[] effectiveHpRatio, int candidateCount, int[] into)
        {
            if (filter == SkillShieldFilter.Self)
            {
                if (selfIndex < 0 || selfIndex >= candidateCount) return 0;
                into[0] = selfIndex;
                return 1;
            }

            int count = targetCount < candidateCount ? targetCount : candidateCount;
            if (count <= 0) return 0;
            if (count > into.Length) count = into.Length;

            // 선택 정렬: 매 회 미선택 중 최소 키를 뽑는다. 후보 수가 격자 상한이라
            // O(C×N) 이 충분히 작다.
            var picked = new bool[candidateCount];
            int n = 0;
            for (int k = 0; k < count; k++)
            {
                int best = -1;
                float bestKey = float.MaxValue;
                for (int i = 0; i < candidateCount; i++)
                {
                    if (picked[i]) continue;
                    float key = filter == SkillShieldFilter.Nearest
                        ? distanceSq[i]
                        : effectiveHpRatio[i];
                    if (key < bestKey)
                    {
                        bestKey = key;
                        best = i;
                    }
                }
                if (best < 0) break;
                picked[best] = true;
                into[n++] = best;
            }
            return n;
        }
    }
}
