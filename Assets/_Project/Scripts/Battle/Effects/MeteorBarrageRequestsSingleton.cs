using Unity.Collections;
using Unity.Entities;

namespace Wassup.Battle.Effects
{
    // season-gimmick-clockout unit 3 — 메테오 barrage 요청 채널(Effects→Bridge).
    // ResignationThresholdSystem enqueue → BattleBridge drain(unit 4)이 SkyFall×TileAoe cast.
    // BattleBridge 가 큐 수명 소유(생성/dispose/singleton 파괴) — 기존 RequestsSingleton 전례.
    public struct MeteorBarrageRequestsSingleton : IComponentData
    {
        public NativeQueue<MeteorBarrageRequest> queue;
    }
}
