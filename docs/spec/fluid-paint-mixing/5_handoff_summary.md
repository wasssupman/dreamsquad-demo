# Handoff — Fluid Paint-Mixing (units 0~4)

세션 인계 지도. 최신 계약은 README/번호 문서가 우선.

## Commit (branch `feature/fluid-paint-mixing`, 로컬 — 미푸시)

- `0b2af3c1` docs: spec 신설
- `83e7d3f3` unit 0 — FluidSimConfig + RenderTargets + FluidMath(TDD 8/8)
- `aa2cc97b` unit 1 — 솔버 셰이더 9패스 URP 포팅(MIT)
- `bf77e77a` unit 2 — FluidPaintSim 구동 루프 + Splat/앰비언트
- `974b02a3` unit 3 — 스크래치 씬 Play 시각 검증 통과
- `91a7812b` unit 4 — DreamcatcherFluidBackdrop 컴포넌트

## Implemented

- WebGL-Fluid-Simulation(MIT) 핵심 솔버를 URP 오프스크린 RT 핑퐁으로 이식 — advection/divergence/curl/vorticity/pressure Jacobi/gradient subtract/splat/clear/display 9패스.
- `FluidPaintSim`: 매 프레임 Graphics.Blit 체인 step(dt), `Splat(uv,vel,color)` API, 자율 앰비언트 + seedSplats, dye 안정 출력 핸들(`DyeTexture`).
- 모든 튜닝값은 `FluidSimConfig.asset`(하드코딩 0). RT 포맷 SystemInfo 폴백, half-float 기본.
- 스크래치 씬 Play 에서 여러 색이 밀고 섞이는 물감-유체 확인(스크린샷) — 핵심 검증 질문 YES.
- `DreamcatcherFluidBackdrop`: 핸드 오픈(슬로모=GPU 여유) 중만 구동·페이드하는 backdrop 어댑터(공유 파일 무수정, State 폴링).

## Key Files

- `Assets/_Project/Scripts/Presentation/Fluid/` — FluidPaintSim / FluidRenderTargets / FluidMath / FluidPaintView
- `Assets/_Project/Shaders/Fluid/` — FluidSolver.shader(+.cginc) / FluidSolver.mat
- `Assets/_Project/Data/FluidSimConfig.cs` + `Assets/_Project/Data/Fluid/FluidSimConfig.asset`
- `Assets/_Project/Scripts/UI/Dreamcatcher/DreamcatcherFluidBackdrop.cs`
- `Assets/_Project/Scenes/FluidScratch.unity` — 재검증용(열고 Play)
- 테스트: `Assets/_Project/Tests/EditMode/FluidMathTests.cs`

## Verified

- 컴파일 클린(전체 EditMode 1275 passed / 0 failed / 2 skipped-기존Ignore).
- FluidMath TDD RED(CS0103)→GREEN 8/8.
- unit 3 스크래치 Play: 콘솔 에러 0 + 물감-혼합 룩 스크린샷.
- unit 4 BattleScene 배선 완료(DreamcatcherFluidBackdropCanvas, sortingOrder 4, HandGated). AlwaysOn 으로 실 보드 위 유체 렌더 Play-스크린샷 검증 후 HandGated 확정.
- 앰비언트 룩 반복 튜닝(사용자 피드백): 방울 터짐/깜빡임 → 연속 유동 → 가장자리 유입 → **최종: 테두리 밴드에만 분포**
  (Display `_EdgeMask`가 중앙 비움 + 변-접선 흐름). 값은 전부 FluidSimConfig.asset(edgeMaskWidth/ambientColorAmount/ambientFlow…). 사용자 확정.
- **미완**: 실제 핸드-오픈 상태에서의 gated look 라이브 확인(MCP 로 핸드 여는 게 어려움) + 실기 perf.

## Notes (되돌리면 안 됨)

- 순수 View 계층 — ECS/BattleBridge 무접촉. 맥락 경계 미침범.
- 텍스처는 named uniform 으로만(`_MainTex` 미사용) — Blit 마다 SetTexture. 패스 인덱스 순서 = 셰이더 SubShader 순서(변경 금지).
- Advection 은 수동 bilerp(모바일 half-float 선형필터 미지원 대비) — 되돌리지 말 것.
- 셰이더/머티리얼은 에셋 참조(`Shader.Find` 금지 — 빌드 스트리핑 사고 이력).
- `execute_code` 는 이 환경에서 막힘(mono 경로 길이) — 씬 자동화는 MenuItem 우회.

## Follow-up

- **unit 4 BattleScene 실배선**(다음): Dreamcatcher 캔버스 아래 backdrop GO 배치 + 핸드 오픈 시 카드 뒤 유체 라이브 확인 + 실기 perf 프로파일. 공유 씬 아트 배치라 사용자와 함께.
- 이벤트 구동 splat(코스트/히트 → Splat), bloom 글로우, 코스트 셀 배경 변형, 터치 인터랙티브 — README 후속 후보 참조.
- push 는 사용자 승인 후.
