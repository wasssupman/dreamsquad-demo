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
