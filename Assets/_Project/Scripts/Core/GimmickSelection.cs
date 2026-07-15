namespace Wassup.Core
{
    // gimmick-match-integration unit 1 — pool 에서 결정론적으로 기믹 1개 인덱스 선택.
    // 순수 함수(값 입력 → 값 출력, 아키텍처 무관): 같은 (count, seed) → 같은 index.
    // seed 는 MatchSeed.DeriveGimmickSeed 산출값(int)을 uint 로 캐스트해 넘긴다.
    public static class GimmickSelection
    {
        // poolCount <= 0 → -1 (배정 없음). Unity.Mathematics.Random 은 0 시드 금지라 방어.
        public static int PickIndex(int poolCount, uint seed)
        {
            if (poolCount <= 0) return -1;
            var rng = new Unity.Mathematics.Random(seed == 0u ? 1u : seed);
            return (int)(rng.NextUInt() % (uint)poolCount);
        }
    }
}
