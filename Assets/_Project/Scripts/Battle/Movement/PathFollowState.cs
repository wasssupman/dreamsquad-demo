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

        // continuous-agent-movement unit 13 — "이번 프레임 자기주도 이동을 하지 않았다"(1 = 정지).
        //
        // MovementSystem 이 매 프레임 **자기 결정을 기록**하고 AgentSeparationSystem 이 읽는다.
        // 술어를 겹침 해소 쪽에서 재구현하지 않기 위한 seam 이다 — "시뮬이 이 유닛을 멈췄나"는
        // Standoff / Engaging-Halt / Pulse-타격중 / CC 잠금 / 순찰 dir 0 / 고립 셀로 흩어져 있어
        // 두 벌로 두면 반드시 갈린다(벽 술어 중복과 같은 부류).
        //
        // 기록 방식은 **케이스 열거가 아니라 결과 관찰**이다: 진입 시 1, 자기주도 변위를 실제로
        // 적용하는 지점에서만 0. 새 continue 경로가 생겨도 자동으로 "정지"에 편입된다.
        //
        // 소비: 정지한 유닛은 겹침 밀어냄의 **전진 성분을 거부**한다 — 안 그러면 뒤 무리가
        // 교전 중인 적을 경로 따라 4~9타일 밀어 나른다(실측).
        public byte holdingGround;
        // Phase 9: currentWaypointIndex 제거 — flow field 가 대체
        // Phase 9: tileSize 제거 — FlowFieldSingleton.tileSize 가 단일 소스
    }
}
