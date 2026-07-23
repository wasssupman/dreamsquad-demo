# Unit 2 — FluidPaintSim (Blit 패스 체인 런타임)

## 목적

매 프레임 솔버를 구동하는 View 계층 MonoBehaviour. `FluidRenderTargets` + `FluidSolver.mat` 를 물려
step(dt) Blit 체인을 돌리고, `Splat()` API 와 자율 앰비언트 드라이버를 제공한다. dye 결과를 안정 핸들로 노출.

## 변경 대상

- `Assets/_Project/Scripts/Presentation/Fluid/FluidPaintSim.cs` (신규 MonoBehaviour)
- `Assets/_Project/Scripts/Presentation/Fluid/FluidRenderTargets.cs` (보강 — Display 출력 RT + 초기 clear)

## 구현

`FluidPaintSim : MonoBehaviour` (Wassup.Presentation):
- SerializeField: `FluidSimConfig config`, `Material solverMaterial`(FluidSolver.mat), `Vector2Int referenceSize`(RT 종횡비 산출용, 기본 512²)
- OnEnable: 머티리얼 인스턴스화(HideAndDontSave) + `_targets.Allocate`. OnDisable: Release + 인스턴스 Destroy (프로젝트 관례)
- `RenderTexture DyeTexture => _targets.Display` (핑퐁 무관 고정 핸들)
- `public void Splat(Vector2 uv, Vector2 velocityDelta, Color color)` — velocity/dye 각각 splat 패스 후 swap. 반경은 `splatRadius/100` × aspect 보정
- `public void SetSurfaceSize(int w,int h)` — 크기 바뀌면 재할당(어댑터용)
- Update: dt=min(realDt, 1/60) 로 Step; `EmitFlow` — 방출기가 변에 붙어 접선으로 흐르며 색·힘 주입(터짐/깜빡임 아님). Display 패스가 `_EdgeMask`(config.edgeMaskWidth)로 중앙을 비워 **테두리 밴드에만 분포**. seed 는 속도 없는 색 얼룩(config.seedSplats)

**Step(dt) 패스 순서** (원본 step 그대로): Curl → Vorticity(swap) → Divergence → Pressure init(Clear ×PRESSURE, swap) → Pressure Jacobi ×N(swap each) → GradientSubtract(swap) → Advect velocity(swap) → Advect dye(swap) → Display(dye→안정 출력).

- 텍스처는 named uniform(`SetTexture(id, rt)`)으로 패스마다 지정 후 `Graphics.Blit(read, write, mat, pass)`. `_TexelSize`=sim 텍셀 고정, advection 만 `_DyeTexelSize`=소스 텍셀.
- 하드코딩 수치 0 — 전부 `config` 에서.

## 완료 기준

- [x] `Wassup.Runtime` 컴파일 클린 (FluidMathTests 8/8 → 어셈블리 빌드 성공)
- [x] Play 진입 시 예외/에러 없이 Step 루프 실행 (unit 3 스크래치 씬 Play, 콘솔 에러 0)
- [x] EditMode 회귀 없음

**완료 확인 2026-07-23**: seedSplats 추가(콜드 스타트 방지). Play 에서 Step 루프 정상 — unit 3 스크린샷으로 시각 확인.
`FluidRenderTargets` 에 Display(안정 출력) RT + 생성 후 clear 보강.
