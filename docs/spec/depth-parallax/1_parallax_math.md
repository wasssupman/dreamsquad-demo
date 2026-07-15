# 1 — 순수 패럴랙스 수학 + 테스트

## 목적

틸트/뎁스 → UV 오프셋 결정과 틸트 스프링 스텝을 아키텍처-blind 순수 함수로 둔다(제약 10). 회귀
방지 EditMode 테스트로 rest no-op·중심 피벗·감쇠 수렴을 고정한다.

## 변경 대상

- New: `Assets/_Project/Modules/DepthParallax/Runtime/DepthParallaxMath.cs`
- New: `Assets/_Project/Modules/DepthParallax/Tests/EditMode/Wassup.DepthParallax.Tests.asmdef`
- New: `Assets/_Project/Modules/DepthParallax/Tests/EditMode/DepthParallaxMathTests.cs`

## 구현

- **`static class DepthParallaxMath`** (namespace `Wassup.DepthParallax`):
  - `static Vector2 UvOffset(Vector2 tilt, float depth, float depthCenter, float amplitude, float depthSign)`
    → `tilt * (depth - depthCenter) * amplitude * depthSign`. (중심 피벗 = near/far 반대로 힌지.
    **부호는 힌지 뺄셈 *후* 전체 항에 곱한다** — raw 에 먼저 곱하면 힌지가 범위 밖으로 밀려 깨짐.
    셰이더 Cue A 와 같은 순서로 계약 일치. 산식을 여기서 결정·테스트.)
  - `static void SpringStep(ref Vector2 pos, ref Vector2 vel, Vector2 target, float spring,
    float damping, float maxSpeed, float dt)` — `KeyringSim.SpringStep(Vector2)` 의 **포트**
    (임계감쇠 적분 + maxSpeed 클램프). 무의존 경계라 참조 불가 → 복사. (README 공통원칙 참조.)
- **테스트 asmdef**: `references: ["Wassup.DepthParallax", "UnityEngine.TestRunner",
  "UnityEditor.TestRunner"]`, `includePlatforms: ["Editor"]`, `optionalUnityReferences: ["TestAssemblies"]`.
  위치는 모듈 내부(테스트가 모듈과 함께 이식됨). 프로젝트 공용 EditMode 폴더와 별개.
- **테스트**:
  - `UvOffset(tilt=0)` → `Vector2.zero`(rest no-op 산식 보장).
  - `depth==depthCenter` → 오프셋 0(힌지 평면 정지).
  - `depth>center` 와 `depth<center` 오프셋 부호 반대(중심 피벗).
  - `depthSign=-1` 결과가 `depthSign=+1` 결과의 정확한 부호 반전(극성 플립 보장 — 셰이더 순서 회귀 잠금).
  - `SpringStep` 이 target 으로 수렴(N 스텝 후 |pos-target|<eps), maxSpeed 클램프 동작.

## 완료 기준

- `run_tests` EditMode 그린(신규 테스트 전부 통과). 기존 테스트 수 회귀 없음.
- `DepthParallaxMath` 가 `UnityEngine` 외 의존 없음(순수 static).
- rest no-op·중심 피벗 부호 테스트가 명시적으로 존재(불변식 회귀 잠금).
