// salvaged from Assets/_Project/Scripts/Core/GimmickSelection.cs (battle-core-rebuild unit 4)
// 이식 시 바뀐 것: 네임스페이스만(`Somnia.Battle.Core` → `Somnia.Battle.BattleCore`). 산식 동일.
namespace Somnia.Battle.BattleCore
{
    // 풀에서 결정론적으로 기믹 1개 인덱스를 고른다. 순수 함수 — 같은 (count, seed) → 같은 index.
    // seed 는 `MatchSeed.DeriveGimmickSeed` 산출값(int)을 uint 로 캐스트해 넘긴다.
    public static class GimmickSelection
    {
        // poolCount <= 0 → -1 (배정 없음). `Unity.Mathematics.Random` 은 0 시드를 금지하므로 방어.
        public static int PickIndex(int poolCount, uint seed)
        {
            if (poolCount <= 0) return -1;
            var rng = new Unity.Mathematics.Random(seed == 0u ? 1u : seed);
            return (int)(rng.NextUInt() % (uint)poolCount);
        }
    }
}
