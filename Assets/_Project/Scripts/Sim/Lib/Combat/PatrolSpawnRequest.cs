namespace Wassup.Sim.Combat
{
    /// <summary>
    /// battle-sim-extraction unit 18-I/2 arm C/2 — 순찰병 소환 요청(Combat→소비 지점).
    /// 구 `PatrolSpawnRequest` 이식.
    ///
    /// **채널을 만들지 않는다** — <see cref="ProjectileRequestCarrier"/> 와 같은 **전용 캐리어
    /// 엔티티** 관용구다. 공격 루프에서 스폰을 요청하는 관용구가 이미 그 자리에 있다.
    ///
    /// ⚠ <see cref="ownerCell"/> 은 **소환사 셀 그대로**다. walk 셀 스냅은 소비 지점(18-K)이
    /// 한다 — 걸을 수 있는 칸인지는 맵을 보는 쪽만 아는 사실이라 sim 규칙이 아니다.
    /// 그리고 그 셀은 초회 게이트의 판정 중심과 **같은 값**이어야 한다(아래).
    /// </summary>
    public struct PatrolSpawnRequest
    {
        public SimEntityId owner;
        public SimInt2 ownerCell;
        public int patrolDataIndex;
        public int leashTileRadius;
    }

    /// <summary>
    /// 캐리어 표식. 드레인이 통째로 파괴하고, 매치 경계 정리도 이 타입으로 건다
    /// (드레인 사이에 전투가 멈춘 낙오분 회수 — 투사체 캐리어가 같은 이유로 등재돼 있다).
    /// </summary>
    public struct PatrolRequestCarrier { }
}
