using UnityEngine;

namespace Wassup.Presentation
{
    // camera-direction unit 8 — 보드가 화면에 다 들어오는 카메라 거리 계산 (plain in/out,
    // EditMode 테스트 대상). 회전은 건드리지 않고 거리만 구한다 — pitch/FOV 는 씬이 소유.
    public static class CameraFramingMath
    {
        // 카메라를 `center - forward * t` 에 둘 때 모든 코너가 프러스텀에 들어오는 최소 t.
        //
        // localCorners = R⁻¹(corner - center) — 카메라 회전의 역으로 돌린 보드 코너.
        // 그 좌표계에서 카메라를 t 만큼 물리면 코너의 view z 는 (local.z + t) 가 되므로
        //   |x| ≤ (z + t)·tanH,  |y| ≤ (z + t)·tanV
        // 에서 t ≥ |x|/tanH - z, t ≥ |y|/tanV - z. 코너 전체의 최댓값이 답이다.
        //
        // 바운딩 구 근사(radius / sin(fov/2))와 달리 pitch 로 납작해진 보드에서 여백이
        // 과하게 남지 않는다. margin 은 결과에 곱한다(1 = 딱 맞음).
        public static float FitDistance(Vector3[] localCorners, float tanH, float tanV, float margin)
        {
            if (localCorners == null || localCorners.Length == 0) return 0f;
            tanH = Mathf.Max(1e-4f, tanH);
            tanV = Mathf.Max(1e-4f, tanV);

            float t = 0f;
            for (int i = 0; i < localCorners.Length; i++)
            {
                var c = localCorners[i];
                t = Mathf.Max(t, Mathf.Abs(c.x) / tanH - c.z);
                t = Mathf.Max(t, Mathf.Abs(c.y) / tanV - c.z);
            }
            return Mathf.Max(0f, t) * Mathf.Max(0.01f, margin);
        }

        // 수직 FOV(도) + aspect → (tanH, tanV). aspect 가 비정상이면 16:9 로 폴백한다
        // (Camera.aspect 는 게임뷰가 0 폭일 때 0 이 될 수 있다).
        public static void FrustumTangents(float verticalFovDeg, float aspect, out float tanH, out float tanV)
        {
            if (aspect < 0.01f) aspect = 16f / 9f;
            tanV = Mathf.Tan(Mathf.Max(1f, verticalFovDeg) * 0.5f * Mathf.Deg2Rad);
            tanH = tanV * aspect;
        }

        // camera-direction unit 10 — 상태 레시피를 절대 포즈로 푼다.
        //
        // 델타가 아니다. 상태끼리 공유하는 기준점이 없어서 한 상태를 튜닝해도 다른 상태가
        // 움직이지 않는다. 구 구조(씬 캡처 포즈 + 페이즈 델타)는 기준을 건드리면 전부 딸려 왔다.
        //
        // 레시피가 없으면 false — 호출부가 "현재 포즈 유지" 로 처리한다(구 FindPhasePose
        // null → hold 의 자리를 대신한다).
        public static bool SolveStatePose(
            Vector3 target, Wassup.Data.CameraStateFraming framing, Bounds boardWorld, float aspect,
            out Vector3 pos, out Quaternion rot, out float fov, Vector3[] cornerBuffer = null)
        {
            pos = Vector3.zero;
            rot = Quaternion.identity;
            fov = 0f;
            if (framing == null) return false;

            fov = framing.fov;
            // yaw/roll 없음 — 이 게임의 보드는 화면 정면 고정이다.
            rot = Quaternion.Euler(framing.pitchDeg, 0f, 0f);
            FrustumTangents(fov, aspect, out float tanH, out float tanV);

            float d = framing.fitToBoard
                ? FitDistance(LocalCorners(boardWorld, rot, cornerBuffer), tanH, tanV, framing.fitMargin)
                : framing.fixedDistance;

            // 대상을 화면 세로 screenY 에 두는 보정은 **카메라 로컬 up** 으로 민다.
            // 월드 Y 로 밀면 대상까지의 시야 깊이가 함께 변해 판의 크기가 흔들리고, 화면비마다
            // 다른 그림이 된다(절대 거리 저작이 무너진 unit 9 와 같은 계열의 함정).
            // 로컬 up 은 정면 축과 직교하므로 깊이 d 가 보존된다 — 대상은 자리만 옮긴다.
            float u = -(framing.screenY - 0.5f) * 2f * d * tanV;
            pos = target - (rot * Vector3.forward) * d + (rot * Vector3.up) * u;
            return true;
        }

        // Bounds 8코너를 카메라 회전 기준 로컬로 변환. 호출부가 배열을 재사용할 수 있도록
        // 버퍼를 받는다(길이 8 미만이면 새로 만든다).
        public static Vector3[] LocalCorners(Bounds world, Quaternion camRot, Vector3[] buffer = null)
        {
            if (buffer == null || buffer.Length < 8) buffer = new Vector3[8];
            var inv = Quaternion.Inverse(camRot);
            var e = world.extents;
            int i = 0;
            for (int sx = -1; sx <= 1; sx += 2)
                for (int sy = -1; sy <= 1; sy += 2)
                    for (int sz = -1; sz <= 1; sz += 2)
                        buffer[i++] = inv * new Vector3(sx * e.x, sy * e.y, sz * e.z);
            return buffer;
        }
    }
}
