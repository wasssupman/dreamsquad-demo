# 0 — DepthParallax.cginc 추출 + 셰이더 리팩터

## 목적

패럴랙스 산식을 모듈의 공유 `.cginc` 로 빼서 `DepthParallax_UI` 와 (unit 1의) `BackgroundDissolve`
양쪽이 `#include` 하게 한다. **수식은 모듈이 단일 소유** — 복붙 금지.

## 변경 대상

- New: `Assets/_Project/Modules/DepthParallax/Shaders/DepthParallax.cginc`
- Modify: `Assets/_Project/Modules/DepthParallax/Shaders/DepthParallax_UI.shader` (include 로 전환)

## 구현

- **`DepthParallax.cginc`** — 유니폼 선언 없이 **순수 함수만** 노출(호출측이 유니폼 소유):
  ```hlsl
  #ifndef WASSUP_DEPTH_PARALLAX_INCLUDED
  #define WASSUP_DEPTH_PARALLAX_INCLUDED

  // Cue A — 뎁스 힌지 UV 오프셋. 부호는 (depth-center) 뺄셈 *후* 전체 항에.
  // tilt==0 또는 depth==center 면 0 (rest no-op / 힌지 평면 정지).
  inline float2 DepthParallaxOffset(float2 tilt, float depth, float depthCenter,
                                    float amplitude, float depthSign)
  {
      return tilt * (depth - depthCenter) * amplitude * depthSign;
  }

  // Cue B — 클립공간 사다리꼴 delta(quad-local [-1,1] 코너 → 변형 delta).
  inline float2 DepthParallaxTrapezoid(float2 uv01, float2 tilt, float persp) { ... }

  // Cue C — 틸트축 하이라이트 밴드(length(tilt) 게이트).
  inline float DepthParallaxHighlight(float2 uv, float2 tilt, float hiWidth, float hiStrength) { ... }
  #endif
  ```
  기존 `DepthParallax_UI.shader` 의 Cue A/B/C 본문을 **동작 변경 없이** 그대로 옮긴다.
- **`DepthParallax_UI.shader`**: 인라인 수식을 지우고 `#include "DepthParallax.cginc"` + 함수 호출로
  치환. 유니폼(`_Tilt`/`_Amplitude`/…) 선언과 UGUI 스캐폴드는 셰이더에 그대로 남긴다.
- **동작 무변경이 목표**. 리팩터일 뿐 기능 추가 없음.

## 완료 기준

- 컴파일 클린(`read_console`), 셰이더 `isSupported=True`.
- **회귀 검증(하드)**: depth-parallax unit 3 의 rest no-op 하네스 재실행 → `_Tilt=0` vs `UI/Default`
  **diff=0** 유지, `_Tilt≠0` 시 변화 있음(효과 살아있음).
- 실 Guardian/Ranger 아트 tilt 프리뷰가 리팩터 전과 육안 동일(배틀 컷신 무회귀).
- EditMode `DepthParallaxMathTests` 6/6 유지(수학 계약 불변).
