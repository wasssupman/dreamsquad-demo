using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Wassup.Battle.Effects
{
    // season-gimmick-overwork unit 4 — 픽업 주기 스폰 상태 싱글턴.
    // candidateCells: 스폰 후보 = 이동/배치 타일영역(Walk∪Place). **BattleBridge 소유** —
    //   Persistent 할당, BuildPickupSpawnState 생성 / TeardownFlowField dispose (맵 lifecycle 공유,
    //   FlowFieldSingleton 동형). Effects 는 읽기만.
    // elapsed/rng: cadence + 결정론 RNG. PickupSpawnSystem(Effects)이 mutate.
    //   rng seed = MatchSeed.DerivePickupSeed(matchSeed) — 판마다 재현 가능.
    public struct PickupSpawnState : IComponentData
    {
        public NativeArray<int2> candidateCells;
        public float elapsed;
        public Random rng;

        public bool IsCreated => candidateCells.IsCreated;

        public void Dispose()
        {
            if (candidateCells.IsCreated) candidateCells.Dispose();
        }
    }
}
