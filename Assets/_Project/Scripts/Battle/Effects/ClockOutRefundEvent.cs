namespace Wassup.Battle.Effects
{
    // season-gimmick-clockout unit 6 — 퇴근 1회당 코스트 환급 요청 (Effects→Bridge 큐 원소).
    // ClockOutSystem 이 퇴근 시 enqueue → BattleBridge 가 drain 해 CostRuntime.AddCost(amount)
    // (기존 코스트 지급 패스 재사용). amount = 트리거 시점 config.costRefund baked.
    // plain struct (IComponentData 아님) — DefenderDeathEvent/MeteorBarrageRequest 등 큐 원소 전례.
    public struct ClockOutRefundEvent
    {
        public int amount;
    }
}
