using UnityEngine;

namespace Wassup.Presentation
{
    // camera-direction unit 0 — 카메라 포즈 합성 순수 수학 (plain in/out, EditMode 테스트 대상).
    // 델타는 전부 "base 포즈 기준 카메라 로컬 축" 해석: localPos 는 base 회전 축으로 변환해 더하고,
    // pitch/roll 은 base 기준 right/forward 둘레 회전. 값 자체는 아키텍처를 모른다.
    // (unit 11 부터 base = 현재 카메라 상태의 레시피 해. 그 전에는 씬에서 캡처한 홈 포즈였다.)
    public struct CameraPoseDelta
    {
        public Vector3 localPos; // 카메라 로컬 축 위치 오프셋
        public float pitchDeg;   // base right 축 회전
        public float yawDeg;     // base up 축 회전 (unit 5 — 드래그 포커스 lookat)
        public float rollDeg;    // base forward 축 회전
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
                yawDeg = a.yawDeg + b.yawDeg,
                rollDeg = a.rollDeg + b.rollDeg,
                fovDelta = a.fovDelta + b.fovDelta,
            };
        }

        // 드래그 포커스 복귀 진행도. 초반을 빠르게 빼고 착지 직전에는 감속해, 선형 fade보다
        // 반응성·정착감을 함께 준다. 입력 범위 밖은 항등/완료로 고정한다.
        public static float EaseOutCubic01(float t01)
        {
            float t = Mathf.Clamp01(t01) - 1f;
            return t * t * t + 1f;
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

        // camera-direction unit 16 — 셰이크 유효 세기. 입력이 둘이다: **한 방**(임펄스 — 세기와
        // 길이를 받아 envelope 으로 잦아든다)과 **계속 흔들리는 상태**(지속 레벨 — 오르내림을
        // 호출처가 소유한다). 성격이 달라 하나로 합치지 않는다.
        //
        // 둘이 겹칠 때는 **더하지 않고 max** 다. 합치면 두 출처가 동시에 울릴 때 진폭이 상한을
        // 넘어 멀미가 난다 — 이 한 줄이 이 함수가 존재하는 이유다(회귀 테스트 대상).
        // 임펄스 감쇠는 킥과 같은 envelope 을 쓴다(duration<=0 = 그 채널 끔 규약도 함께 계승).
        public static float ShakeWeight(
            float impulseStrength, float impulseRemaining, float impulseDuration, float heat01)
        {
            float impulse = impulseStrength * KickEnvelope(impulseRemaining, impulseDuration);
            return Mathf.Max(impulse, Mathf.Clamp01(heat01));
        }

        // camera-direction unit 16 rev2 — 셰이크 한 방의 **재발동 규칙** (코드 리뷰 반영).
        //
        // 「세기는 max, 길이는 새 값」 은 안 된다. 셰이크는 길이를 **호출처가** 주기 때문에
        // 그 조합이 어느 호출처도 저작하지 않은 임펄스를 만든다 — 말파이트(1.0/0.35s) 직후
        // 샷건맨(0.6/0.18s)이면 「세기 1.0 을 0.18초 더」가 되어 샷건맨의 짧고 약한 한 방이
        // 말파이트 세기로 울린다. 최악은 강하고 짧은 것 뒤에 약하고 긴 것이 와서
        // 「세기 1.0 을 2초」가 되는 경우다.
        // (줌 펄스에는 이 문제가 없다 — 길이 출처가 config 하나뿐이라 hold 가 늘어나지 않는다.
        //  **그래서 그 규약을 그대로 복사하면 안 된다.** 초판이 복사해서 이 결함이 났다.)
        //
        // 그래서 임펄스를 **통째로** 비교한다: 새 한 방의 시작 세기가 지금 울리고 있는 유효
        // 세기 이상이면 갈아끼우고, 아니면 진행 중인 것을 건드리지 않는다. 세기도 길이도
        // 섞이지 않으므로 화면에 나오는 임펄스는 항상 «누군가 저작한 그대로» 다.
        public static bool ShouldReplaceShakeImpulse(
            float currentStrength, float currentRemaining, float currentDuration, float newStrength)
        {
            float currentEffective = currentStrength * KickEnvelope(currentRemaining, currentDuration);
            return Mathf.Clamp01(newStrength) >= currentEffective;
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

        // camera-direction unit 12 — 배치 커서 추종.
        //
        // 드래그 포커스와 **같은 입력(스크린 NDC)**을 쓰되 델타 해석이 다르다. FocusDelta 는
        // "포인터 방향으로 전진 + 부분 lookat" 이고, 이쪽은 "화면을 커서 쪽으로 민다" —
        // 회전을 건드리지 않아 보드 좌표감이 유지되고 판 전체가 화면에 남는다.
        //
        // 입력이 NDC 인 것이 계약이다. 커서를 월드에 투영하면 안 된다 — 지금 그 커서 쪽으로
        // 기울고 있는 카메라로 투영하면 같은 픽셀 아래 월드 점이 매 프레임 움직여
        // "카메라 이동 → 대상 이동 → 카메라 이동" 되먹임이 된다(스프링은 진동을 숨길 뿐이다).
        //
        // depth = base 포즈에서 보드 중앙까지의 시야 깊이. 그 깊이에서 화면 절반이 담는
        // 세계 크기가 depth·tan 이므로, lead 1 이면 커서 지점이 정확히 화면 중앙에 온다.
        public static CameraPoseDelta PanDelta(
            Vector2 ndc, float fovDeg, float aspect, float weight, float lead, float depth)
        {
            if (weight <= 0f || lead == 0f || depth <= 0f) return default;
            float tanV = Mathf.Tan(fovDeg * 0.5f * Mathf.Deg2Rad);
            float tanH = tanV * Mathf.Max(0.01f, aspect);
            float k = lead * weight * depth;
            return new CameraPoseDelta
            {
                localPos = new Vector3(ndc.x * k * tanH, ndc.y * k * tanV, 0f),
            };
        }

        // unit 5 rev 3 — 드래그 포커스 델타. 입력은 **터치 스크린 NDC**(-1..1, 중앙 0) —
        // 월드/카메라 포즈 비의존이라 "카메라 회전→보드 재계산→타겟 이동" 되먹임 루프가
        // 원천적으로 없다. base FOV/aspect 로 포인터 ray 의 base-로컬 방향을 복원해
        // dolly(포인터 방향 전진) + 부분 lookat + 스와이프 리드(NDC 속도) + FOV 를 만든다.
        // ndc/ndcVel 은 호출부(Director)가 스프링-댐핑으로 스무딩한 값 — 리드 속도 = 스프링 속도.
        public static CameraPoseDelta FocusDelta(
            Vector2 ndc, Vector2 ndcVel, float baseFovDeg, float aspect,
            float weight, float dolly, float fovDelta, float lookWeight,
            float leanPerSpeed, float leanMaxDeg)
        {
            if (weight <= 0f) return default;

            // 되먹임은 사라졌지만 각 증폭 상한으로 유지(풀 lookat 은 배치 좌표감 파괴).
            lookWeight = Mathf.Clamp(lookWeight, 0f, 0.5f);

            float tanV = Mathf.Tan(baseFovDeg * 0.5f * Mathf.Deg2Rad);
            float tanH = tanV * Mathf.Max(0.01f, aspect);
            Vector3 dirLocal = new Vector3(ndc.x * tanH, ndc.y * tanV, 1f).normalized;

            // 부분 lookat: base forward(+z) 대비 포인터 ray 방향의 yaw/pitch 풀각 → lookWeight 블렌드.
            // 주의: 이 분해(Ry·Rx)는 Compose 적용 순서(yaw 먼저)의 전치라 두 각이 동시에 클 때
            // O(yaw×pitch) 교차항 오차가 있다 — 부분 블렌드(≤0.5)에선 무시 가능, 풀 lookat 금지.
            float yawFull = Mathf.Atan2(dirLocal.x, dirLocal.z) * Mathf.Rad2Deg;
            float pitchFull = -Mathf.Asin(Mathf.Clamp(dirLocal.y, -1f, 1f)) * Mathf.Rad2Deg;

            // 스와이프 리드: NDC 속도(스프링 속도) → 이동 방향으로 시선이 앞서감. 정지 시 0.
            float leadYaw = Mathf.Clamp(ndcVel.x * leanPerSpeed, -leanMaxDeg, leanMaxDeg);
            float leadPitch = Mathf.Clamp(-ndcVel.y * leanPerSpeed, -leanMaxDeg, leanMaxDeg);

            return new CameraPoseDelta
            {
                localPos = dirLocal * (dolly * weight),
                yawDeg = (yawFull * lookWeight + leadYaw) * weight,
                pitchDeg = (pitchFull * lookWeight + leadPitch) * weight,
                fovDelta = fovDelta * weight,
            };
        }

        // base 포즈 ⊕ 델타 → 절대 포즈. 델타 항등이면 base 그대로.
        // FOV 는 [fovMin, fovMax] 클램프 — 상태 화각+펄스가 SO 튜닝만으로 위험 FOV 가
        // 되지 않도록 코드 계약으로 차단 (spec README).
        public static void Compose(
            Vector3 basePos, Quaternion baseRot, float baseFov, in CameraPoseDelta delta,
            float fovMin, float fovMax,
            out Vector3 pos, out Quaternion rot, out float fov)
        {
            pos = basePos + baseRot * delta.localPos;
            rot = Quaternion.AngleAxis(delta.rollDeg, baseRot * Vector3.forward)
                * Quaternion.AngleAxis(delta.pitchDeg, baseRot * Vector3.right)
                * Quaternion.AngleAxis(delta.yawDeg, baseRot * Vector3.up)
                * baseRot;
            fov = Mathf.Clamp(baseFov + delta.fovDelta, fovMin, fovMax);
        }
    }
}
