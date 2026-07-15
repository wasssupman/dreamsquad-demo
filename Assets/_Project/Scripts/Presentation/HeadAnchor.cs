using UnityEngine;

namespace Wassup.Presentation
{
    // 유닛 머리 위 뱃지/팝업(아이콘 스트립·상태연출·히트바·데미지 숫자)의 공용 리프트.
    //
    // 함정: 오프셋을 월드 +Y 로 주면 안 된다. 전투 카메라는 원근 투영(FOV 40)에 pitch
    // 55°(Draft 40°) 라, 월드 up 은 카메라 공간에서 (0, cosθ, -sinθ) 로 분해된다 →
    // 위로만 가는 게 아니라 카메라 쪽으로 당겨져 view_z 가 h·sinθ 만큼 줄고,
    // screen_x = f·view_x/view_z 이므로 그만큼 화면 x 가 확대된다. 즉 뱃지가 화면
    // 중앙에서 바깥으로 밀리고, 중앙에서 먼 유닛일수록 심하다(보드 끝 ≈57px@1080w,
    // 오프셋 2.6 기준). 중앙 유닛은 view_x≈0 이라 멀쩡해 보여서 놓치기 쉽다.
    //
    // 카메라 up 은 시선축과 직교라 view_z 가 변하지 않는다 → 어느 타일이든 머리 위
    // 같은 화면 거리를 유지하고, 페이즈별 pitch 변화(Draft 40°↔Battle 55°)에도
    // 높이가 cosθ 로 흔들리지 않는다. 이미 카메라 빌보드인 뷰들과 평면도 일치한다.
    //
    // 주의: 이 함수를 쓰는 오프셋 값은 카메라 평면 기준이라 구 월드 기준 값보다 작다.
    // 등가식 k = h·cosθ·view_z/(view_z − h·sinθ) (55°/23u 기준 ≈ 0.63배).
    // 월드 기준으로 눈 튜닝한 값을 그대로 옮기면 뱃지가 너무 높이 뜬다.
    public static class HeadAnchor
    {
        // basePos 를 카메라 평면에서 offset 만큼 띄운 위치. offset.x = 카메라 right,
        // offset.y = 카메라 up. cam == null 이면 월드 폴백(뷰가 카메라를 못 찾은 경우).
        public static Vector3 Lift(Vector3 basePos, Vector3 offset, Camera cam)
            => basePos + (cam != null ? cam.transform.rotation * offset : offset);
    }
}
