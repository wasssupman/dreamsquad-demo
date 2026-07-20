using Unity.Collections;
using Unity.Entities;

namespace Wassup.Battle.Effects
{
    // season-gimmick-clockout unit 6 — 퇴근 코스트 환급 채널(Effects→Bridge).
    // ClockOutSystem enqueue → BattleBridge drain 이 GameManager.CostRuntime.AddCost 로 지급.
    // BattleBridge 가 큐 수명 소유(생성/dispose/singleton 파괴) — 기존 EventsSingleton 전례.
    public struct ClockOutRefundEventsSingleton : IComponentData
    {
        public NativeQueue<ClockOutRefundEvent> queue;
    }
}
