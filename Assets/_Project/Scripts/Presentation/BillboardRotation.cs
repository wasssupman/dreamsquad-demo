using UnityEngine;

namespace Wassup.Presentation
{
    // tilemap-world-surround 13 — Billboard / PropBillboard 가 중복 구현하던 빌보드 회전 수학의 단일 소유자.
    // 순수 함수(모드/틸트/카메라/위치 → Quaternion). 대상(self vs visualRoot) 선택과 enum→Facing 매핑,
    // 카메라 fetch/캐싱은 호출측 책임. 여기는 회전값만 만든다.
    // 반환 null = "이번 프레임 회전 갱신 안 함"(None / 카메라 없음 / YAxis 퇴화 방향) — 기존 컴포넌트 동작 보존.
    public static class BillboardRotation
    {
        public enum Facing { None, Tilted, YAxis, Camera }

        // worldPos = 회전 대상의 월드 위치(YAxis 방향 계산용). camera 는 YAxis/Camera 에서만 필요(그 외 null 허용).
        public static Quaternion? Compute(Facing facing, float tiltAngle, Camera camera, Vector3 worldPos, bool flip180)
        {
            Quaternion rot;
            switch (facing)
            {
                case Facing.Tilted:
                    rot = Quaternion.Euler(tiltAngle, 0f, 0f);
                    break;
                case Facing.YAxis:
                {
                    if (camera == null) return null;
                    Vector3 dir = worldPos - camera.transform.position;
                    dir.y = 0f;
                    if (dir.sqrMagnitude < 0.0001f) return null;
                    rot = Quaternion.LookRotation(dir.normalized, Vector3.up);
                    break;
                }
                case Facing.Camera:
                    if (camera == null) return null;
                    rot = camera.transform.rotation;
                    break;
                default: // None
                    return null;
            }

            if (flip180) rot *= Quaternion.Euler(0f, 180f, 0f);
            return rot;
        }
    }
}
