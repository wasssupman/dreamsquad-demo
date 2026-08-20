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

        // camera-direction unit 9 — 보드 깊이에 정규화한 피사계 심도(DoF) 거리.
        //
        // gaussianStart/End 를 월드 절대 거리로 저작하면 화면비가 바뀌는 순간 그림이 무너진다.
        // FitDistance 가 aspect 로 카메라 거리를 정하기 때문이다(tanH = tanV·aspect) — 가로가
        // 넓은 기기일수록 카메라가 보드에 더 붙고, 고정 임계값은 화면 밖으로 밀려난다.
        // 실측: 16:9 게임뷰 27.7 → 19.5:9 폰 25.3(-2.45), 블러가 걸리는 영역 화면 상단 25% → 7%.
        //
        // 그래서 임계값을 **보드 깊이 범위 안의 위치**로 저작한다(0 = 보드 앞단, 1 = 뒷단, 1 초과 = 보드 뒤).
        // 보드의 view 공간 깊이 폭은 맵이 정하고 화면비와 무관하므로, 카메라가 당겨지면 임계값도
        // 같이 당겨진다 — "보드 뒤쪽부터 흐려진다"가 모든 화면비에서 같은 그림으로 유지된다.
        //
        // localCorners 는 FitDistance 와 같은 버퍼(카메라 회전 기준 로컬 코너),
        // camDistance 는 실제로 적용된 최종 거리(fit + pullback)다.
        // 보드가 카메라 뒤에 있는 등 프레이밍이 성립하지 않으면 false — 호출부가 DoF 를 건드리지 않는다.
        public static bool DofRange(Vector3[] localCorners, float camDistance, float startT, float endT,
                                    out float start, out float end)
        {
            start = 0f;
            end = 0f;
            if (localCorners == null || localCorners.Length == 0) return false;

            float nearZ = float.MaxValue, farZ = float.MinValue;
            for (int i = 0; i < localCorners.Length; i++)
            {
                float z = localCorners[i].z + camDistance;
                if (z < nearZ) nearZ = z;
                if (z > farZ) farZ = z;
            }
            if (nearZ <= 0f) return false; // 보드가 카메라 뒤 — 프레이밍 실패

            float span = Mathf.Max(1e-3f, farZ - nearZ);
            start = Mathf.Max(0f, nearZ + span * startT);
            // URP 는 end < start 를 방어하지 않는다(램프 폭이 음수가 되면 블러가 뒤집힌다).
            end = Mathf.Max(nearZ + span * endT, start + 1e-3f);
            return true;
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
