using UnityEngine;

namespace Wassup.UI
{
    // placement-thumb-occlusion unit 1 — 배치 판정 포인터 오프셋의 순수 정책 함수.
    // 아키텍처 타입 미참조(Vector2/Mathf 만) → EditMode 테스트 대상.
    //
    // 소비처가 둘이라 뽑았다: DefenderDragPlacementController(트레이 D&D · armed 보드 제스처)와
    // DefenderRelocationController(목적지 지정). 두 컨트롤러가 같은 산식을 각자 들고 있으면
    // 한쪽만 튜닝돼 "같은 손가락 이동인데 하이라이트가 다르게 앞서간다"가 된다.
    public static class PlacementPointerOffset
    {
        // 이동량 비례 램프. 승격 임계에서 0 → 임계보다 rampPx 만큼 더 끌면 1.
        //
        // **시간 램프가 아닌 이유**: 임계(기본 16px)만 넘겨도 시간이 흐르면 풀 오프셋(≈65px)까지
        // 올라간다 → 손가락이 멈춰 있는데 하이라이트가 계속 위로 기어오른다. 게다가 16px 승격이
        // 65px 점프를 만드는 4배 증폭이 그대로 남는다. 이동량에 묶으면 하이라이트가 손가락이
        // 움직인 만큼만 앞서가므로 증폭이 구조적으로 사라지고, 리드가 시간이 아니라 조작에 붙는다.
        //
        // rampPx <= 0 = 램프 없음(승격 즉시 풀 오프셋).
        public static float Ramp(float travelPx, float thresholdPx, float rampPx)
        {
            if (rampPx <= 0f) return 1f;
            return Mathf.Clamp01((travelPx - thresholdPx) / rampPx);
        }

        // 배치 판정 포인터 = 실제 포인터 + 화면 up × (offset × ramp).
        // **파생값이지 치환이 아니다** — UI 레이캐스트·탭/드래그 임계 비교·press 보드 가드는
        // 호출부가 계속 실제 좌표로 수행한다.
        public static Vector2 Apply(Vector2 rawScreen, float offsetPx, float ramp01)
            => rawScreen + Vector2.up * (offsetPx * ramp01);
    }
}
