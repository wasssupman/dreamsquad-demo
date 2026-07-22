# Fluid Paint-Mixing 이식 — 설계 노트 (얇은 브레인스토밍 결과물)

> 실제 구현 상세는 `docs/spec/fluid-paint-mixing/` 에 있다. 이 문서는 목표·결정·포인터만 담는다.

## 목표

PavelDoGreat/WebGL-Fluid-Simulation(MIT)의 **핵심 "물감이 섞이는" 유체 룩만** Unity로 이식한다.
전체 이식이 아니라 **축소 솔버**만 가져온다 — bloom/sunrays/dithering/dat.gui/스크린샷 등 부가기능 전부 제외.

## 확정된 결정 (브레인스토밍)

1. **구현 방식 = 진짜 (축소) 솔버, RenderTexture 핑퐁** (흉내 flow-warp 아님). 원본 GLSL 프래그먼트 패스를
   URP HLSL 로 1:1 포팅해 비압축성 투영(divergence→pressure Jacobi→gradient subtract)으로 진짜 소용돌이·혼합을 낸다.
2. **통합 방식 = MonoBehaviour + 오프스크린 RT 로의 `Graphics.Blit` 패스 체인** (URP RendererFeature/RenderGraph 아님,
   Compute 아님). 순수 View 계층, ECS 무관, BattleBridge 무변경. URP 에서 오프스크린 RT-to-RT blit 은 안전.
3. **솔버를 화면 용처와 분리**한다: 자립 `FluidPaintSim` 이 dye RenderTexture 를 만들고, 표면(코스트 UI / Dreamcatcher BG)은
   그 RT 를 소비만 하는 **얇은 어댑터**.
4. **프로토타입은 스크래치 쿼드**에 먼저 띄워 룩을 검증한 뒤 진짜 표면에 배선한다.

## 성능 예산 (모바일)

sim 해상도 128~160 · dye 해상도 그 2배 · pressure Jacobi 10~20회 · half-float RT(RG/R/RGBA16F).
국소 소형(코스트 셀)은 안전, 풀스크린 상시 BG 는 전투 시뮬과 GPU 경합 → 실기 프로파일 필수.

## 발견된 제약 (진짜 표면 확정 시 반영)

- **코스트 물통(CostWell)은 fill 게이지 의미가 강함** — 자체 액체 셰이더(`CostWell_UI.shader`) + 기포/파형 juice 보유.
  물감-소용돌이는 fill 의미와 충돌하므로, 코스트 셀에 쓰려면 "well 뒤 배경 스월" 정도로 제한하거나 Dreamcatcher BG 를 택한다.
  → **진짜 표면은 unit 3(프로토타입) 확인 후 확정.**

## 라이선스

원본 MIT. 포팅한 셰이더/스크립트 헤더에 원저작권 + MIT 고지를 보존한다.

## 포인터

- 이식 대상 원본: https://github.com/PavelDoGreat/WebGL-Fluid-Simulation (`script.js` 단일 파일)
- 기존 절차적 액체 셰이더 참고: `Assets/_Project/Shaders/PlacementLiquidTile.shader`, `CostWell_UI.shader`
- 코스트 UI: `Assets/_Project/Scripts/UI/CostDisplay.cs`
- 파이프라인 대조: `docs/reference/object-pipeline-map.md` (VFX 아키타입)
