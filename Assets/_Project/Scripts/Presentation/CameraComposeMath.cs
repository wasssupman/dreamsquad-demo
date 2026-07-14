using UnityEngine;

namespace Wassup.Presentation
{
    // camera-direction unit 0 — 카메라 포즈 합성 순수 수학 (plain in/out, EditMode 테스트 대상).
    // 델타는 전부 "홈 포즈 기준 카메라 로컬 축" 해석: localPos 는 홈 회전 축으로 변환해 더하고,
    // pitch/roll 은 홈 기준 right/forward 둘레 회전. 값 자체는 아키텍처를 모른다.
    public struct CameraPoseDelta
    {
        public Vector3 localPos; // 카메라 로컬 축 위치 오프셋
        public float pitchDeg;   // 홈 right 축 회전
        public float rollDeg;    // 홈 forward 축 회전
        public float fovDelta;

        public static CameraPoseDelta Identity => default;
    }

    public static class CameraComposeMath
    {
        public static CameraPoseDelta Add(in CameraPoseDelta a, in CameraPoseDelta b)
        {
            return new CameraPoseDelta
            {
                localPos = a.localPos + b.localPos,
                pitchDeg = a.pitchDeg + b.pitchDeg,
                rollDeg = a.rollDeg + b.rollDeg,
                fovDelta = a.fovDelta + b.fovDelta,
            };
        }

        // 킥 감쇠 envelope: 남은시간 비율 k 의 k² (빠른 decay). 구 CameraImpactKick 이식.
        public static float KickEnvelope(float remaining, float duration)
        {
            if (duration <= 0f) return 0f;
            float k = Mathf.Clamp01(remaining / duration);
            return k * k;
        }

        // 킥 순간 델타: 아래로 내리꽂는 위치 + 미세 pitch/roll (방향 고정 — 결정론, 랜덤 셰이크 아님).
        // pitch/roll 은 단일 노브(rotAmp)로 잠긴 쌍 — 독립 튜닝이 필요해지면 config 에 축별 진폭을 신설한다.
        public static CameraPoseDelta KickDelta(float magnitude, float posAmp, float rotAmp)
        {
            float rot = rotAmp * magnitude;
            return new CameraPoseDelta
            {
                localPos = new Vector3(0f, -posAmp * magnitude, 0f),
                pitchDeg = rot,
                rollDeg = rot,
            };
        }

        // 홈 포즈 ⊕ 델타 → 절대 포즈. 델타 항등이면 홈 그대로.
        public static void Compose(
            Vector3 homePos, Quaternion homeRot, float homeFov, in CameraPoseDelta delta,
            out Vector3 pos, out Quaternion rot, out float fov)
        {
            pos = homePos + homeRot * delta.localPos;
            rot = Quaternion.AngleAxis(delta.rollDeg, homeRot * Vector3.forward)
                * Quaternion.AngleAxis(delta.pitchDeg, homeRot * Vector3.right)
                * homeRot;
            fov = homeFov + delta.fovDelta;
        }
    }
}
