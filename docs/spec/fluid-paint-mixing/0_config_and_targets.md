# Unit 0 — Config + RenderTargets + 순수 계산

## 목적

유체 솔버의 **토대**를 놓는다: 튜닝 파라미터 SO, RenderTexture 핑퐁 세트 헬퍼, 아키텍처-blind 순수 계산.
아직 시각 결과는 없다 — 이후 unit(셰이더·런타임)이 이 위에 얹힌다.

## 변경 대상

- `Assets/_Project/Scripts/Data/FluidSimConfig.cs` (신규 SO)
- `Assets/_Project/Scripts/Presentation/Fluid/FluidMath.cs` (신규 순수 static)
- `Assets/_Project/Scripts/Presentation/Fluid/FluidRenderTargets.cs` (신규 런타임 헬퍼)
- `Assets/_Project/Tests/EditMode/FluidMathTests.cs` (신규 EditMode 테스트)
- `Assets/_Project/Data/Fluid/FluidSimConfig.asset` (기본값 인스턴스 — Unity 가동 시 생성)

## 구현

### FluidSimConfig (Wassup.Data)
`[CreateAssetMenu(menuName="Wassup/FluidSimConfig")]`. 필드(전부 인스펙터 튜닝, 하드코딩 금지):
- 해상도: `simResolution`(짧은 변, 기본 128), `dyeResolution`(기본 256 — 원본 1024는 모바일 과함)
- 솔버: `pressureIterations`(20), `velocityDissipation`(0.2), `densityDissipation`(1.0), `pressure`(0.8), `curl`(30)
- splat: `splatRadius`(0.25, 정규화), `splatForce`(6000)
- 앰비언트: `ambientSplatsPerSecond`(0=자동 없음), `palette`(Color[] — 비면 랜덤 HSV)
- 정밀도: `preferHalfFloat`(미지원 시 자동 폴백)

### FluidMath (Wassup.Presentation, 순수 static — EditMode 테스트 대상)
- `Vector2Int CalcResolution(int target, float aspect)` — 원본 `getResolution` 이식. 짧은 변=target,
  긴 변=round(target×정규화aspect). aspect≥1(가로 긴)→(max,min), 아니면 (min,max).
  **방어**: target<1→1, aspect 비유한·≤0→1(정사각 폴백) (ARM64 캐스트 함정, `FlipbookMath` 선례).
- `Vector2 TexelSize(Vector2Int res)` — (1/w, 1/h). 0 나눗셈 방어. 셰이더 이웃 샘플 오프셋용(2+ 소비자, sim-critical).

### FluidRenderTargets (Wassup.Presentation, 런타임 헬퍼 — 순수 아님, RenderTexture 소유)
- `Allocate(FluidSimConfig cfg, int surfaceW, int surfaceH)`: aspect=W/H 로 sim/dye 해상도 산출,
  포맷 선택 후 RT 할당. bilinear + clamp + no-mip.
- 필드: velocity(핑퐁 쌍), dye(쌍), pressure(쌍), divergence(단일), curl(단일).
- 포맷(+SystemInfo 폴백): velocity=RGHalf→RGFloat→ARGBHalf · pressure/divergence/curl=RHalf→RFloat→RGHalf ·
  dye=ARGBHalf→ARGB32. `preferHalfFloat=false` 면 full-float 우선.
- `SwapVelocity/SwapDye/SwapPressure()`, `Release()`, texel size 노출.

## 완료 기준

- [x] `Wassup.Runtime` 컴파일 클린 (전체 EditMode 1277 테스트 빌드·실행 → 빌드 성공)
- [x] `FluidMathTests` EditMode 전 케이스 통과 (8/8 — landscape/portrait/square, 반올림, 비유한 aspect 폴백, texel 0-guard)
- [x] `FluidSimConfig.asset` 기본값 인스턴스 생성 (`Assets/_Project/Data/Fluid/FluidSimConfig.asset`)
- [x] `FluidRenderTargets` 컴파일 통과 (실제 할당·핑퐁 시각 검증은 unit 2/3 에서 구동 확인)

**완료 확인 2026-07-23**: TDD(RED: CS0103 → GREEN: 8/8) · 전체 EditMode 1275 passed / 0 failed / 2 skipped(기존 Ignore).
