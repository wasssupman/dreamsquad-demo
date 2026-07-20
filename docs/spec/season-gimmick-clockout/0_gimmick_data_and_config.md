# 0. 기믹 데이터 + config + 주입 seam

## 목적

"집에 가도 되나요?" 기믹의 **데이터 토대**를 세운다. 기믹 SO 타입 + blittable config + BattleBridge 주입 branch. 이후 유닛(2·3·4)이 config 를 self-gate 로 소비한다. 기존 Burnout/RedBull 기믹 프레임과 동형.

## 변경 대상

- `Assets/_Project/Scripts/Data/Gimmick/ClockOutGimmickData.cs` — 신규 `GimmickData` 서브클래스(수치 노브 + 메테오 ProjectileData ref)
- `Assets/_Project/Scripts/Battle/Effects/ClockOutGimmickConfig.cs` — 신규 blittable `IComponentData`
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — `CreateGimmickConfigIfActive` 에 ClockOut branch + teardown 2곳에 config 제거

## 구현

1. **`ClockOutGimmickData : GimmickData`** (`Wassup.Data`, 상속 2단계 상한 준수). 수치 전부 SO 노브:
   - `clockOutSeconds`(10) · `resignationThreshold`(5) · `meteorCount`(3) · `meteorDamage` · `meteorTileRange` · `meteorWarningSec` · `meteorStaggerSec`
   - `ProjectileData meteorProjectile` (managed — SkyFall×TileAoe 뷰. config 에 안 들어가고 unit 4 cast 시 BattleBridge 가 직접 읽음)
2. **`ClockOutGimmickConfig`** (`Wassup.Battle.Effects`, blittable): 위 수치의 사본(ProjectileData 제외). 존재 = 기믹 활성 → 후속 시스템 `RequireForUpdate` self-gate.
3. **BattleBridge 주입** (`CreateGimmickConfigIfActive`): Burnout/RedBull branch 뒤에 `else if (_assignedGimmick is ClockOutGimmickData cd)` 추가 → 수치 복사해 `ClockOutGimmickConfig` 엔티티 생성. teardown-first 2곳(`CreateGimmickConfigIfActive` 선두 + `DestroyEcsInfrastructureEntities`)에 `DestroyEntitiesByType<ClockOutGimmickConfig>()` 추가(멱등).

## 완료 기준

- compile 0 에러 (`dotnet build Wassup.Runtime.csproj` 또는 Unity 리컴파일 콘솔 클린).
- `ClockOutGimmickConfig` 는 아직 어떤 시스템도 소비하지 않으므로 런타임 무영향(주입돼도 no-op) — 회귀 0.
- 기믹 asset 인스턴스(`Gimmick_ClockOut.asset`) 생성 + `gimmickPool` 등록은 **unit 5(wiring)** 소관. 이 유닛은 타입·branch까지.

확인 2026-07-16 — Unity 재컴파일(MCP refresh) 콘솔 에러/경고 0. (dotnet build 는 신규 파일 미등재 stale-csproj 오탐이라 Unity 리컴파일로 검증.)
