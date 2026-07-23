# Fluid Paint-Mixing (WebGL 유체 축소 이식)

**상태: 진행 중 (2026-07-23) — unit 0~4 완료. BattleScene 에 Dreamcatcher 유체 backdrop 배선(HandGated, sortingOrder 4). 최종 룩 = 화면 밖에서 스며든 색이 테두리 밴드에만 성기게 분포(중앙 비움, edgeMaskWidth). 사용자 확정. 남은 것: 핸드 오픈 라이브 look 확인 + 실기 perf + push.**

## 상위 목표

PavelDoGreat/WebGL-Fluid-Simulation(MIT)의 **핵심 물감-혼합 유체 룩**을 Unity(URP)로 이식한다.
전체 포팅이 아니라 **축소 솔버**만: advection · divergence · pressure Jacobi · gradient subtract · curl/vorticity · splat.
bloom/sunrays/dithering/dat.gui/스크린샷 등 부가기능은 **전부 제외**.

검증 질문: **"URP 오프스크린 RT 핑퐁 축소 솔버가 모바일에서 진짜 '물감 섞임'으로 읽히는가?"**
각 작업 단위의 완료 기준은 이 질문의 구체 표현이다.

## 작업 단위 목록

| # | 구분 | 문서 | 목적 |
|---|---|---|---|
| 0 | 토대 | `0_config_and_targets.md` | `FluidSimConfig` SO + RT 포맷/핑퐁 헬퍼 + 순수 계산(`FluidMath`) & EditMode 테스트 |
| 1 | 셰이더 | `1_solver_shaders.md` | 원본 7개 커널 + splat + display 를 URP HLSL 프래그먼트로 포팅 (MIT 헤더) |
| 2 | 런타임 | `2_fluid_paint_sim.md` | `FluidPaintSim` MonoBehaviour — Blit 패스 체인 step(dt) + `Splat()` API + 자율 앰비언트 드라이버 |
| 3 | 검증 배선 | `3_scratch_quad_prototype.md` | 스크래치 쿼드/RawImage 에 dye RT 표시 → Play 로 룩 시각 검증 (unity-feature-wiring) |
| 4 | 표면 어댑터 | `4_surface_adapter.md` | 진짜 표면(코스트 셀 배경 vs Dreamcatcher BG)에 얇게 배선. **타겟은 unit 3 확인 후 확정** |

작업 단위 파일은 해당 단위 착수 시점에 작성/구현한다(한 번에 한 파일). 계약이 바뀌면 이 README 를 갱신한다.

## Feature-wide 계약

- **순수 View 계층.** ECS 맥락 무관, `BattleBridge` 무변경. 핵심 솔버는 어떤 시뮬 이벤트에도 의존하지 않는다.
- **하드코딩 수치 금지(제약 6).** sim/dye 해상도, pressure iterations, dissipation, curl 세기, splat 반경·힘, 색 팔레트,
  앰비언트 cadence 는 **전부 `FluidSimConfig` SO** 에서 나온다.
- **솔버 ↔ 표면 분리.** `FluidPaintSim` 이 dye `RenderTexture` 를 소유하고 읽기 전용 프로퍼티로 노출. 표면은 소비만 한다.
- **통합 = `Graphics.Blit` 패스 체인.** MonoBehaviour 의 Update 에서 패스별 offscreen RT-to-RT blit(핑퐁). URP RendererFeature/
  RenderGraph/Compute 를 쓰지 않는다. 패스 순서 = curl → vorticity → divergence → pressure init → pressure ×N → gradient subtract → advect(velocity) → advect(dye).
- **모바일 RT.** velocity=RG16F, pressure/divergence/curl=R16F, dye=RGBA16F(half). bilinear + clamp. 포맷/필터 미지원 시
  `SystemInfo` 체크 후 폴백(정밀도 하향). 해상도·iter 는 config 로 하향 가능.
- **런타임 머티리얼 인스턴스는 직접 정리**(프로젝트 관례 — `CostDisplay`/`DepthParallaxView` 처럼 OnDestroy 에서 Destroy).
- **MIT 고지 보존.** 포팅한 셰이더/스크립트 헤더에 원저작권 + MIT 라이선스 고지.
- **sim-critical 순수 계산만 테스트.** dissipation decay(`1/(1+diss*dt)`), texel size, 핑퐁 인덱스 등 아키텍처-blind 계산은
  `FluidMath` 순수 static 으로 빼 EditMode 테스트(제약 10). GPU 패스 결과 자체는 시각 검증.

## 파이프라인 커버리지 (VFX 아키타입 대조)

> `docs/reference/object-pipeline-map.md` "VFX (one-shot)" 표 대조. 단 이 feature 는 **상시 View 렌더 효과**(스폰되는
> 플레이 오브젝트도, one-shot 트리거도 아님)라 대부분 정거장이 N/A.

| 정거장 | 이 feature | 비고 |
|---|---|---|
| 데이터 SO | `FluidSimConfig` (파라미터 SO) | 플레이 오브젝트 SO 아님 — 솔버 튜닝 파라미터 |
| 스폰 진입점 | **N/A** — 스폰되는 오브젝트 아님 | 씬에 상주하는 View 컴포넌트 |
| ECS 컴포넌트 | **N/A** — 순수 Mono, ECS 무관 | 맥락 경계 미접촉 |
| 시뮬 시스템 | **N/A** — GPU Blit 체인이 시뮬 대신 | ECS System 아님 |
| 이벤트 큐 | **N/A** — 신규 채널 0 | 핵심은 자율 구동. (이벤트 구동 splat 은 후속 후보) |
| View | `FluidPaintSim`(dye RT 소유) + 표면 어댑터(RawImage/머티리얼) | |
| 씬 wiring | `FluidPaintSim` GameObject + config 할당 + 표면 참조 | unity-feature-wiring |

## 후속 후보 (현 스코프 밖)

- **[다음] 핸드 오픈 라이브 확인 + 실기 perf**: BattleScene 배선 완료(HandGated). 실제 핸드를 열어 카드 뒤 유체 look 확인
  (MCP 로 핸드-오픈 상태 재현이 어려워 AlwaysOn 으로 렌더만 검증함) + 안드로이드 실기 프로파일. 색 농도/알파는 실 카드 위에서 조정.
- **이벤트 구동 splat**: 코스트 획득/전투 히트 시 `BattleBridge` → `Splat()` 호출로 색 주입 (현재는 자율 앰비언트만).
- **Bloom/글로우 패스**: 원본의 발광 룩. 모바일 비용 검증 후 별도.
- **두 번째 표면 어댑터**: unit 4 가 택하지 않은 나머지(코스트 셀 ↔ Dreamcatcher BG).
- **터치 인터랙티브**: 표면이 입력을 받으면 포인터 splat (원본 그대로의 놀이 요소).
