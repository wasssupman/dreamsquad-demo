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

        // tilted-billboard unit 6 — 배경 프랍 거리(elevation) 기반 틸트각 산출(순수).
        // 퍼스펙티브에서 프랍까지 시선 elevation 이 위치마다 달라 단일 고정각은 한 거리에서만 "선다".
        // 기준각(refElev)은 카메라 pitch(=카메라가 보는 보드 중앙의 elevation) 에서 라이브 도출 →
        // 페이즈마다 카메라 pitch 가 바뀌어도(Draft↔Battle) 자기보정. 중앙 프랍은 항상 baseTilt 유지.
        // baseTilt(타입 기준각) 둘레로 (elev−camPitch)×factor 보정, [min,max] 클램프.
        // camera==null 또는 factor==0 → baseTilt 그대로(고정 폴백). 회전 자체는 Compute(Facing.Tilted) 가 담당.
        public static float ResolveDistanceTilt(float baseTilt, float factor,
            float minTilt, float maxTilt, Camera camera, Vector3 worldPos)
        {
            if (camera == null || factor == 0f) return baseTilt;
            Vector3 camFwd = camera.transform.forward;
            float camPitch = Mathf.Asin(Mathf.Clamp(-camFwd.y, -1f, 1f)) * Mathf.Rad2Deg;
            Vector3 toCam = camera.transform.position - worldPos;
            float horiz = Mathf.Sqrt(toCam.x * toCam.x + toCam.z * toCam.z);
            float elev = horiz < 1e-4f ? 90f : Mathf.Atan2(toCam.y, horiz) * Mathf.Rad2Deg;
            float tilt = baseTilt + (elev - camPitch) * factor;
            return Mathf.Clamp(tilt, minTilt, maxTilt);
        }
    }
}
