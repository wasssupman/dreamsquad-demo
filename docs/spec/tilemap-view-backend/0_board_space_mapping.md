# 0. BoardSpace 매핑 헬퍼

## 목적

sim 공간(rect XZ 월드)과 view 공간 사이의 변환을 담당하는 단일 헬퍼를 만든다. 위치와 **방향 벡터** 모두 이 헬퍼를 경유한다. 이후 모든 작업 단위가 이 계약 위에서 동작한다.

## 변경 대상

- 신규: `Assets/_Project/Scripts/Core/BoardViewMode.cs`
- 신규: `Assets/_Project/Scripts/Core/BoardSpace.cs`
- 신규: `Assets/_Project/Tests/EditMode/BoardSpaceTests.cs`

## 구현

- `BoardViewMode` enum: `Legacy3D = 0`, `TilemapRect = 1`, `TilemapIso = 2`.
- `BoardSpace`: MonoBehaviour 계층 전용 static 클래스 (ECS/Burst 에서 호출 금지).
  - `Configure(BoardViewMode mode, float3 simOrigin, float tileSize, GridLayout grid)` — BattleBridge 맵 빌드 시 1회 설정. Tilemap 모드에서 `grid` 는 필수(씬의 Grid 컴포넌트), Legacy3D 는 null 허용. 정적 상태 쓰기는 이 한 곳만.
  - `float3 ToView(float3 simWorld)` — 프레젠테이션 write 경계에서 호출.
    - `Legacy3D`: identity.
    - Tilemap 모드: sim 월드 → 셀 연속 좌표 (`(simWorld - simOrigin) / tileSize`) → **주입된 `GridLayout` 의 `CellToLocalInterpolated` + `LocalToWorld`** 로 view 월드. iso 수식을 직접 하드코딩하지 않는다 — 셀↔월드 정합의 권위는 Grid (README 계약).
    - sim Y(높이: 점프/낙하/부유 연출)는 view 의 화면상 위 방향(Y)에 가산 보존.
  - `float3 ToSim(float3 viewWorld)` — 입력(레이캐스트 히트) 경계에서 호출. `WorldToLocal` + `LocalToCellInterpolated` 역방향.
  - `float3 ToViewVector(float3 simDir)` — 방향 벡터 변환 (translation 없이 위 변환의 선형부만). facing/투사체 회전/cast 방향용.
  - `Plane RaycastPlane()` — 모드별 입력 평면 (`Legacy3D`: XZ ground, Tilemap 모드: XY, viewOrigin 통과).
- sim 셀↔sim 월드는 기존 `GridMath` 가 계속 담당. `BoardSpace` 는 GridMath 의 셀 규약을 모른 채 연속 좌표만 다룬다.

## 완료 기준

> ✅ 검증 2026-06-14 (Unity MCP, EditMode) — `Core/BoardViewMode.cs` + `Core/BoardSpace.cs` + `BoardSpaceTests.cs`
> 신규. `Wassup.Tests.EditMode.BoardSpaceTests` **7 total / 7 passed / 0 failed**(Legacy identity, Rect·Iso 라운드트립,
> Iso 축→마름모 대각 Grid 직접 비교, Rect·Iso 셀중심 정합, RaycastPlane). 컴파일 에러 0, 기존 코드 무변경. 커밋: 4bd8cff

- EditMode 테스트 (테스트에서 GameObject+Grid 를 직접 생성해 주입):
  1. Legacy3D 에서 `ToView`/`ToSim`/`ToViewVector` 가 identity.
  2. 세 모드 모두 `ToSim(ToView(p)) ≈ p` 라운드트립 (Rect/Iso 는 각 cellLayout 의 Grid 주입).
  3. TilemapIso 에서 sim 셀 (1,0)→(0,1) 방향이 서로 다른 마름모 대각으로 매핑 (Grid 결과와 일치 — 상수 기대값이 아닌 `CellToLocalInterpolated` 직접 비교).
- Unity compile 0 errors. 기존 코드 무변경 (신규 파일만).
