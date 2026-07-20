namespace Wassup.Battle.Effects
{
    // season-gimmick-clockout unit 3 — 메테오 barrage 1회 요청 (Effects→Bridge 큐 원소).
    // ResignationThresholdSystem 이 사직서 임계 소모 시 enqueue. BattleBridge 가 drain 해
    // Walk 셀 meteorCount 곳에 SkyFall×TileAoe 메테오를 순차 cast(unit 4).
    // meteorCount 만 트리거 시점 config 값 baked — 나머지 cast 파라미터(damage/range/warning/
    // stagger/projectile)는 BattleBridge 가 배정 기믹 SO(ClockOutGimmickData)에서 읽는다.
    // plain struct (IComponentData 아님) — DefenderDeathEvent 등 큐 원소 전례.
    public struct MeteorBarrageRequest
    {
        public int meteorCount;
    }
}
