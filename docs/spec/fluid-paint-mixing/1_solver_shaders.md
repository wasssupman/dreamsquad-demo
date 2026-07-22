# Unit 1 — 솔버 셰이더 포팅

## 목적

PavelDoGreat/WebGL-Fluid-Simulation(MIT)의 핵심 GLSL 커널을 URP HLSL 프래그먼트로 1:1 포팅한다.
`Graphics.Blit` 패스 체인으로 돌릴 **단일 멀티패스 셰이더** + 공용 include. 이 unit 은 컴파일까지만 검증
(단독 시각 결과 없음 — unit 2 가 구동).

## 변경 대상

- `Assets/_Project/Shaders/Fluid/FluidCommon.cginc` (신규 — 공용 정점/구조체/텍셀 uniform)
- `Assets/_Project/Shaders/Fluid/FluidSolver.shader` (신규 — 멀티패스 솔버)
- `Assets/_Project/Shaders/Fluid/FluidSolver.mat` (신규 — 셰이더 참조 에셋; unit 2 가 `[SerializeField]` 로 잡아
  `Shader.Find` 스트리핑 사고를 피한다. 여기서 생성 = 컴파일 검증도 겸함)

## 구현

`Shader "Wassup/Fluid/FluidSolver"`. 모든 패스 공통 렌더 상태: `Cull Off · ZWrite Off · ZTest Always · Blend Off`
(솔버는 값을 덮어쓴다 — 알파블렌드 아님). CGPROGRAM + `UnityCG.cginc` (Graphics.Blit 오프스크린 경로에 견고,
`UnityObjectToClipPos` 로 풀스크린).

**패스 인덱스 계약** (unit 2 가 이 번호로 Blit):

| # | 패스 | 입력(named tex) | 주요 uniform |
|---|---|---|---|
| 0 | Advection | `_Source`, `_Velocity` | `_Dt`, `_Dissipation`, `_TexelSize`(sim), `_DyeTexelSize`(source) — 수동 bilerp(모바일 필터링 안전) |
| 1 | Divergence | `_Velocity` | `_TexelSize` (경계 반사) |
| 2 | Curl | `_Velocity` | `_TexelSize` |
| 3 | Vorticity | `_Velocity`, `_Curl` | `_CurlStrength`, `_Dt`, `_TexelSize` |
| 4 | Pressure (Jacobi) | `_Pressure`, `_Divergence` | `_TexelSize` |
| 5 | GradientSubtract | `_Pressure`, `_Velocity` | `_TexelSize` |
| 6 | Splat | `_Target` | `_SplatPoint`, `_SplatColor`(xyz), `_SplatRadius`, `_AspectRatio` |
| 7 | Clear | `_Target` | `_ClearValue` (pressure init = ×PRESSURE) |
| 8 | Display | `_Source` | (dye 복사 — 후속에서 tonemap 여지) |

- 이웃 UV(vL/vR/vT/vB)는 정점에서 `_TexelSize` 오프셋으로 계산(divergence/curl/vorticity/pressure/gradientSubtract).
- 텍스처는 named uniform 으로만 읽는다(`_MainTex` 미사용). unit 2 가 패스마다 `SetTexture` 후 Blit.
- 경계 처리는 원본 그대로: divergence 가 화면 밖 속도를 반사(-C).

## MIT 라이선스

두 파일 상단에 원저작권(Pavel Dobryakov) + MIT 고지 보존.

## 완료 기준

- [x] `FluidSolver.shader` 임포트 클린 (Unity 콘솔 에러/경고 0)
- [x] 셰이더가 `Wassup/Fluid/FluidSolver` 로 조회됨 — `FluidSolver.mat` 이 에러 폴백 아닌 실제 셰이더로 바인딩
- [x] 값·per-pass 렌더 검증은 unit 2/3 구동 시 (이 unit 은 컴파일 게이트)

**완료 확인 2026-07-23**: 9개 패스 셰이더 임포트 에러 0 · 머티리얼 바인딩 확인. 런타임 값은 unit 3 Play 검증.
