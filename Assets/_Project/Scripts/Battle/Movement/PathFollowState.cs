using Unity.Entities;
using Unity.Mathematics;

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

        // traversal-layers unit 2 — 이 유닛이 지날 수 있는 층 비트(`PlacementLayer`).
        //
        // **새 컴포넌트를 만들지 않는다.** `PathFollowState` 는 이미 «움직이는 모든 주체»에
        // 붙어 있고(적 스폰·순찰 방어유닛 스폰 두 곳) sim 이 SO 를 못 읽으므로 스폰 시 1회
        // 주입한다 — 값이 살 자리로 여기가 정확하다.
        //
        // 0 = 미주입(레거시·픽스처). 소비자는 0 을 `Path` 로 읽어 현행을 재현한다.
        public byte traversalLayers;

        // defender-knockback-on-impact unit 0 — 이 유닛이 **마지막으로 자기 힘으로 움직인
        // 방향**(정규화, sim 평면). Movement 소유 쓰기 · 다른 맥락은 RO.
        //
        // 기록 방식은 위 `holdingGround` 와 **같은 자리·같은 철학**이다: 케이스를 열거하지
        // 않고 자기주도 변위를 실제로 적용하는 지점에서만 적는다. 그래서 흐름장·웨이포인트·
        // 추격·복구 어느 경로로 움직였든 값이 맞고, 새 이동 분기가 생겨도 자동으로 편입된다.
        //
        // ★ **흐름장을 대신 읽으면 안 된다.** 넉백 방향을 「그 칸의 기본 흐름」에서 뽑던 것이
        // 이 필드를 만든 이유다 — 비행 적은 웨이포인트를 따라가고 추격 중인 적은 추격장을
        // 따라가므로 그 칸의 기본 흐름은 **그 적의 실제 진행 방향이 아니다.** 대공 유닛의
        // 주 표적이 정확히 그 비행 적이라 이 어긋남은 기능의 핵심에서 틀리는 형태였다.
        //
        // 멈춘 프레임에는 갱신하지 않는다 — 교전 중 정지한 적도 직전 진행 방향을 유지해야
        // 뒤로 밀 수 있다. 0 = 한 번도 움직인 적 없음(스폰 직후 · 합성 픽스처 · 고정 구조물).
        // 소비자는 0 을 「방향 없음 = 밀지 않음」으로 읽는다.
        public float2 lastMoveDir;
        // Phase 9: currentWaypointIndex 제거 — flow field 가 대체
        // Phase 9: tileSize 제거 — FlowFieldSingleton.tileSize 가 단일 소스
    }
}
