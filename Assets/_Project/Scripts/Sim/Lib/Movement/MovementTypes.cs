namespace Wassup.Sim.Movement
{
    /// <summary>
    /// battle-sim-extraction unit 18-E/5 — 경로 추종 상태. 구 `PathFollowState` 이식.
    /// `speed` 하나만 남았다 — waypoint 인덱스는 flow field 가, tileSize 는
    /// `FlowFieldSingleton` 이 대체했다.
    ///
    /// ⚠ **보유 = "이동체"** 라는 술어로도 쓰인다(#5 `ZoneApply` 의 대상 조건). 다만 그것만으로
    /// "적" 을 뜻하지는 않는다 — 거점 순찰 아군도 이걸 갖는다. 그래서 존은 진영을 **명시로** 본다.
    /// </summary>
    public struct PathFollowState
    {
        public float speed;
    }

    /// <summary>
    /// 마지막 웨이포인트를 지났다(유출 대기). 구 `PastGoalTag` 이식.
    /// ⚠ 이 태그가 붙으면 `MovementSystem` 의 이동 루프에서 **빠진다**(WithNone) —
    /// 파괴는 #41 의 PastGoal 루프가 하고 그건 `AttackUnitTag` 를 요구한다.
    /// </summary>
    public struct PastGoalTag { }

    /// <summary>
    /// 거점 순찰의 앵커. 구 `PatrolAnchor` 이식.
    /// `tileRadius` 는 **체비셰프 박스 반경**이다(원이 아니다).
    /// </summary>
    public struct PatrolAnchor
    {
        public SimInt2 cell;
        public int tileRadius;
    }
}
