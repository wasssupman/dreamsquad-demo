# 1 — BackgroundDissolve 에 패럴랙스 통합

## 목적

로비 배경 앞 Image 의 머티리얼은 디졸브가 점유 중이라 모듈 머티리얼을 붙일 수 없다.
디졸브 셰이더가 `DepthParallax.cginc` 를 include 해 **Cue A 만** 얻게 한다.

## 변경 대상

- Modify: `Assets/_Project/Shaders/Background_Dissolve_UI.shader`

## 구현

- `#include "../Modules/DepthParallax/Shaders/DepthParallax.cginc"` (경로는 실제 상대경로로 확인).
- 프로퍼티 추가: `_DepthTex`(2D,"gray"), `_Tilt`(Vector,(0,0,0,0)), `_Amplitude`(Float 0.015),
  `_DepthCenter`(Float 0.5), `_DepthSign`(Float 1). **`_Persp`/`_HiStrength` 는 추가하지 않는다**
  (README 계약: 전체화면 배경은 Cue B/C 금지).
- frag 진입부에서 UV 를 한 번 밀고, 이후 **모든 기존 샘플이 그 UV 를 쓰게** 한다:
  ```hlsl
  float depth = tex2D(_DepthTex, i.uv).r;
  float2 uv = i.uv + DepthParallaxOffset(_Tilt.xy, depth, _DepthCenter, _Amplitude, _DepthSign);
  fixed4 color = tex2D(_MainTex, uv) * i.color;
  float noise  = tex2D(_NoiseTex, uv * _NoiseScale).r;   // 디졸브 노이즈도 같은 UV 로 따라가야 함
  ```
  **주의**: 디졸브의 노이즈/원형 확산 계산이 `i.uv` 를 쓰는 곳이 여러 군데다. 패턴이 배경과 함께
  움직여야 자연스러우므로 **샘플링 UV 는 전부 시프트된 `uv` 로 통일**한다. 단 `_Center`/`_MaxRadius`
  기반 반경 계산과 `_ClipRect`(`worldPosition`)는 **원본 좌표 유지**(마스킹/확산 중심이 흔들리면 안 됨).
- rest no-op: `_Tilt=0` → offset 0 → 기존 디졸브와 픽셀 동일.

## 완료 기준

- 컴파일 클린, 셰이더 supported.
- **회귀 검증(하드)**: `_Tilt=0` 에서 로비 낮/밤 디졸브 전환이 **기존과 동일**하게 동작
  (5개 TransitionStyle 중 최소 기본값 `RadialWithGoldenTint` + `NoiseDissolve` 육안/오프스크린 확인).
  원형 확산 중심이 캐릭터 위치를 정확히 따라가는지 확인(좌표 미시프트 확인).
- `_Tilt≠0` 에서 배경이 뎁스에 따라 밀리고, 디졸브 파면도 함께 따라감.
