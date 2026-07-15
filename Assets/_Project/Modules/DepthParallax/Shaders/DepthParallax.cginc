#ifndef WASSUP_DEPTH_PARALLAX_INCLUDED
#define WASSUP_DEPTH_PARALLAX_INCLUDED

// lobby-background-parallax unit 0 — 뎁스 패럴랙스 3중 큐의 순수 함수. 이 파일이 산식의 단일 소유자다.
// 유니폼을 선언하지 않는다(호출측 셰이더가 소유) — 그래서 DepthParallax_UI 처럼 3큐를 다 쓰는 셰이더와
// BackgroundDissolve 처럼 Cue A 만 쓰는 셰이더가 같은 수식을 공유할 수 있다. 복붙 금지.
//
// 공통 불변식: tilt==(0,0) 이면 세 함수 모두 0 을 반환한다(rest = no-op).

// ── Cue A — 뎁스 힌지 UV 오프셋 (코어) ──────────────────────────────────────
// 중심 피벗: depthCenter 기준으로 near/far 가 반대로 힌지 → 회전감. 힌지 평면(depth==depthCenter)은 정지.
// depthSign 은 (depth - depthCenter) 뺄셈 *후* 전체 항에 곱한다 — raw depth 에 먼저 곱하면
// 힌지가 [0,1] 밖으로 밀려 극성 반전이 깨진다.
inline float2 DepthParallaxOffset(float2 tilt, float depth, float depthCenter,
                                  float amplitude, float depthSign)
{
    return tilt * (depth - depthCenter) * amplitude * depthSign;
}

// ── Cue B — 클립공간 사다리꼴 delta ────────────────────────────────────────
// ScreenSpaceOverlay 는 perspective divide 가 없어 사다리꼴을 직접 만든다. 코너 부호는 UV0 에서 유도
// (per-vertex 채널 금지 → additionalShaderChannels 스트립 회피).
// 반환값은 quad-local delta — 호출측이 `o.vertex.xy += delta * o.vertex.w` 로 적용한다.
// 반환값이 이미 persp*tilt 스케일이므로 호출측에서 _Persp 를 재곱하면 안 된다.
// 주의: 전체화면 배경처럼 여백이 없는 대상에는 쓰지 말 것(가장자리가 안으로 당겨져 캔버스가 드러남).
inline float2 DepthParallaxTrapezoid(float2 uv, float2 tilt, float persp)
{
    float2 orig = uv * 2 - 1;
    float2 p = orig;
    p.y *= 1 - persp * tilt.x * orig.x;
    p.x *= 1 - persp * tilt.y * p.y;
    return p - orig;
}

// ── Cue C — 틸트축 하이라이트 스윕 ─────────────────────────────────────────
// length(tilt) 게이트라 rest 에서 0. _Time 항 없음(rest no-op 보장).
inline float DepthParallaxHighlight(float2 uv, float2 tilt, float hiWidth, float hiStrength)
{
    float mag  = length(tilt);
    float2 dir = tilt / max(mag, 1e-5);      // mag=0 이어도 NaN 방지(어차피 *mag=0)
    float band = dot(uv - 0.5, dir) + 0.5;   // 틸트축 방향 [0,1] 밴드 좌표
    float t    = (band - mag) / hiWidth;
    return exp(-t * t) * hiStrength * mag;   // sqr((band-mag)/hiWidth) = t*t (NaN-safe)
}

#endif // WASSUP_DEPTH_PARALLAX_INCLUDED
