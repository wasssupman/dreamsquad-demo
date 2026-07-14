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

        // unit 1 — 비행 보간: 컴포넌트별 unclamped lerp. 진행도(0~1)는 호출부가 클램프하고,
        // 이징 커브의 오버슈트(>1)는 그대로 통과시킨다(백-이즈 연출 허용).
        public static CameraPoseDelta Lerp(in CameraPoseDelta a, in CameraPoseDelta b, float t)
        {
            return new CameraPoseDelta
            {
                localPos = Vector3.LerpUnclamped(a.localPos, b.localPos, t),
                pitchDeg = Mathf.LerpUnclamped(a.pitchDeg, b.pitchDeg, t),
                rollDeg = Mathf.LerpUnclamped(a.rollDeg, b.rollDeg, t),
                fovDelta = Mathf.LerpUnclamped(a.fovDelta, b.fovDelta, t),
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

        // unit 2 — 킬 스트릭 셰이크 델타: 위상(0~1 누적) 기반 sin 합성. 같은 위상 → 같은 값
        // (결정론, seeded RNG 아님). weight = heat × 비행 감쇠 가중치.
        public static CameraPoseDelta ShakeDelta(
            float phaseX01, float phaseY01, float weight, float posAmp, float rotAmp)
        {
            float sx = Mathf.Sin(phaseX01 * 2f * Mathf.PI);
            float sy = Mathf.Sin(phaseY01 * 2f * Mathf.PI);
            return new CameraPoseDelta
            {
                localPos = new Vector3(sx * posAmp * weight, sy * posAmp * weight, 0f),
                rollDeg = sx * rotAmp * weight,
            };
        }

        // unit 3 — 브리딩 파동 1개의 델타: 위상(0~1) 기반 sin. 같은 위상 → 같은 값(결정론).
        // 절대 시각이 아니라 호출부가 누적·wrap 한 위상을 받는다(장세션 float 정밀도 — spec).
        public static CameraPoseDelta BreathWaveDelta(
            float phase01, Vector2 posWeight, float pitchWeight, float weight,
            float posAmp, float rotAmp)
        {
            float s = Mathf.Sin(phase01 * 2f * Mathf.PI) * weight;
            return new CameraPoseDelta
            {
                localPos = new Vector3(s * posWeight.x * posAmp, s * posWeight.y * posAmp, 0f),
                pitchDeg = s * pitchWeight * rotAmp,
            };
        }

        // 홈 포즈 ⊕ 델타 → 절대 포즈. 델타 항등이면 홈 그대로.
        // FOV 는 [fovMin, fovMax] 클램프 — 페이즈 델타+펄스가 SO 튜닝만으로 위험 FOV 가
        // 되지 않도록 코드 계약으로 차단 (spec README).
        public static void Compose(
            Vector3 homePos, Quaternion homeRot, float homeFov, in CameraPoseDelta delta,
            float fovMin, float fovMax,
            out Vector3 pos, out Quaternion rot, out float fov)
        {
            pos = homePos + homeRot * delta.localPos;
            rot = Quaternion.AngleAxis(delta.rollDeg, homeRot * Vector3.forward)
                * Quaternion.AngleAxis(delta.pitchDeg, homeRot * Vector3.right)
                * homeRot;
            fov = Mathf.Clamp(homeFov + delta.fovDelta, fovMin, fovMax);
        }
    }
}
