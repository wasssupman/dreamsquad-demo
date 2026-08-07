using Unity.Entities;

namespace Wassup.Battle.Movement
{
    public struct PathFollowState : IComponentData
    {
        public float speed;
        // continuous-agent-movement unit 3 — 원형 충돌 반지름(월드 단위).
        // 0 이면 기존 점 충돌(셀 경계 clamp)로 동작한다 — 픽스처 보호 + 회귀 시 스위치.
        // 컴포넌트에 둔 것은 나중에 유닛별로 달라질 여지를 미리 만들지 않고도 열어두기
        // 위함이다. 값은 BattleBridge 의 agentRadiusTiles knob 에서 스폰 시 주입되며,
        // 현재는 전원 같은 값이 들어간다.
        public float radius;
        // Phase 9: currentWaypointIndex 제거 — flow field 가 대체
        // Phase 9: tileSize 제거 — FlowFieldSingleton.tileSize 가 단일 소스
    }
}
