using UnityEngine;

namespace Somnia.Battle.Presentation
{
    // sprite-unit-backend unit 1 — 좌우 반전 판정의 단일 소유자. SpineUnitView.SetFacingByViewDelta 의
    // 판정부를 그대로 옮겼다(2026-08-09 팩팩거림 수정 규칙). 두 백엔드가 **이 함수를 호출만** 한다 —
    // 상수를 한 번 더 적으면 한쪽만 튜닝돼 같은 판에서 두 유닛이 다르게 뒤집힌다(제약 13 이 「두 번
    // 났다」고 기록한 결함 형태). 부호 번역은 각 뷰 몫: Spine 은 ScaleX=+1 이 왼쪽, 스프라이트는 flipX.
    public static class UnitFacing
    {
        public const float MoveEpsilon = 0.001f;
        // 반대 방향 이동이 이 거리(타일)만큼 **누적**돼야 뒤집는다(같은 방향이 나오면 리셋).
        // 0.05 = 타일 5% ≈ 정상 보행 1.5프레임 — 진짜 회두는 여전히 즉각으로 보인다.
        public const float FlipAccum = 0.05f;

        // dx: view-space 가로 델타. facingRight: 지금 오른쪽(+x)을 보는가. immediate: 명시 이벤트(공격 타겟)면
        // 누적 없이 즉시. 반환 = 지금 뒤집어야 하는가. 뒤집는 방향은 호출측이 `dx >= 0` 으로 안다.
        public static bool ShouldFlip(float dx, bool facingRight, bool immediate, ref float pendingAccum)
        {
            if (Mathf.Abs(dx) <= MoveEpsilon) return false;
            bool wantRight = dx >= 0f;
            // 이미 그 방향을 보고 있으면 누적 리셋 — 노이즈가 쌓여 뒤집히는 것을 막는다.
            if (facingRight == wantRight)
            {
                pendingAccum = 0f;
                return false;
            }
            if (!immediate)
            {
                pendingAccum += Mathf.Abs(dx);
                if (pendingAccum < FlipAccum) return false;   // 아직 확신 없음 — 유지
            }
            pendingAccum = 0f;
            return true;
        }
    }
}
