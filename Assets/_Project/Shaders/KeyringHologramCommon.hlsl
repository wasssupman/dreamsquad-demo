// keyring-unify 2 — 키링 홀로그램 효과 공용 함수.
// 계약(spec README 6): self-contained 순수 float 함수만 — fixed/half 금지(URP HLSL 에 fixed 미정의),
// _Time 비참조(시간은 t 파라미터), UnityCG/URP 헤더 include 금지.
// CGPROGRAM(UGUI, UICordHologram)·HLSLPROGRAM(URP, WorldCordHologram) 양쪽에서 컴파일된다.
#ifndef KEYRING_HOLOGRAM_COMMON_INCLUDED
#define KEYRING_HOLOGRAM_COMMON_INCLUDED

float KeyringHash21(float2 p)
{
    return frac(sin(dot(p, float2(12.9898, 78.233))) * 43758.5453);
}

// 행 글리치 — 길이축 uv(lenUv)로 행을 정하고, 임계 초과 행의 폭 방향 오프셋을 반환(아니면 0).
float KeyringGlitchOffset(float lenUv, float t, float glitchSpeed, float glitchAmount)
{
    float row = floor(lenUv * 48.0);
    float g = KeyringHash21(float2(row, floor(t * glitchSpeed)));
    return (g > 0.92) ? (g - 0.96) * 2.0 * glitchAmount : 0.0;
}

// 그라데이션(colorA→colorB) + 스캔라인 + 플리커 + 이동 펄스 합성 rgb.
// lenUv = 길이축 0..1. 텍스처는 호출측이 샘플(글리치 오프셋 적용 후). flicker 는 알파 감쇠용 out.
float3 KeyringHoloColor(float3 texRgb, float texA, float lenUv, float t,
    float3 colorA, float3 colorB, float intensity,
    float scanDensity, float scanSpeed, float scanStrength,
    float flickerSpeed, float flickerStrength,
    float pulseSpeed, float pulseWidth, float pulseStrength,
    out float flicker)
{
    float3 grad = lerp(colorA, colorB, lenUv);
    float scan = 1.0 - scanStrength * (0.5 + 0.5 * sin(lenUv * scanDensity - t * scanSpeed));
    flicker = 1.0 - flickerStrength * KeyringHash21(float2(floor(t * flickerSpeed), 7.0));
    float pos = frac(t * pulseSpeed);
    float pulse = smoothstep(pulseWidth, 0.0, abs(lenUv - pos)) * pulseStrength;
    return (grad * texRgb * scan + float3(pulse, pulse, pulse) * texA) * intensity * flicker;
}

#endif
