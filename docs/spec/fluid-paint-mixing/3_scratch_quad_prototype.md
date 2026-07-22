# Unit 3 — 스크래치 프로토타입 (시각 검증 게이트)

## 목적

FluidPaintSim 을 스크래치 씬에 배선해 **"물감이 섞이는가"** 를 눈으로 검증한다. 이 unit 이 feature 의 핵심
검증 질문에 답하는 지점 — 여기서 룩을 확인한 뒤 진짜 표면(unit 4)을 정한다.

## 변경 대상

- `Assets/_Project/Scripts/Presentation/Fluid/FluidPaintView.cs` (신규 — dye RT 를 RawImage/Renderer 에 물리는 얇은 어댑터, unit 4 재사용)
- `Assets/_Project/Scenes/FluidScratch.unity` (신규 스크래치 씬 — Camera + Canvas + 풀스크린 RawImage + FluidPaintSim GO)

## 구현

- `FluidPaintView`: sim.DyeTexture 를 `rawImage.texture` 또는 `targetRenderer` MPB 텍스처에 LateUpdate 로 물린다.
- 스크래치 씬(UnityMCP 로 구성·저장):
  - `Main Camera` (기본), `Directional Light`
  - `Canvas`(ScreenSpaceOverlay) > `RawImage`(풀스크린 stretch)
  - `FluidSim` GO: `FluidPaintSim`(config=FluidSimConfig.asset, solverMaterial=FluidSolver.mat, referenceSize=화면비) + `FluidPaintView`(sim=self, rawImage=위 RawImage)
  - 앰비언트가 켜져 있어 입력 없이도 색이 계속 주입·소용돌이친다.

## 완료 기준 (unity-feature-wiring)

- [x] `FluidPaintView` 컴파일 클린
- [x] 씬 YAML 에 FluidPaintSim/FluidPaintView refs 非-null (config/solverMaterial/sim/rawImage 모두 fileID/guid 확인)
- [x] **Play 검증**: 콘솔 에러 0 + 여러 색(파랑·청록·자홍·초록·노랑)이 밀고 섞이며 소용돌이치는 유체 확인
- [x] 핑크/검정/단색 아님 — 진짜 비압축성 컬·이류·혼합 확인

**완료 확인 2026-07-23**: 스크래치 씬 `FluidScratch.unity`(ScreenSpaceCamera 캔버스 + 풀스크린 RawImage) Play →
게임뷰 캡처로 물감-혼합 룩 확인. 검증 후 일회용 빌더 에디터 스크립트는 삭제(씬만 잔존).

## 참고 (재검증)

`Assets/_Project/Scenes/FluidScratch.unity` 를 열고 Play 하면 앰비언트 유체가 재생된다. 앰비언트 세기·색은
`FluidSimConfig.asset`(ambientSplatsPerSecond / palette)에서 조정.
