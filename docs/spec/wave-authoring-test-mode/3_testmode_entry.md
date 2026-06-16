# 3 — 테스트 모드 진입 (Config + Context + GameManager 분기)

## 목적

아웃게임 "테스트 모드" 진입 시 배틀씬이 드래프트를 스킵하고, 작성 플랜 + 디펜더 프리셋으로 전투를 시작하게 한다. `StartSquadMatch` 를 미러하는 최상위 비파괴 분기.

## 변경 대상

- 신규: `Assets/_Project/Scripts/Data/TestModeConfig.cs` — 디펜더 프리셋 + 플랜 카탈로그 SO.
- 신규: `Assets/_Project/Scripts/Core/TestModeContext.cs` — static carry-in.
- 신규(에셋): `Assets/_Project/Data/Config/TestModeConfig.asset`.
- `Assets/_Project/Scripts/Core/GameManager.cs` — `Start` 최상위 테스트 분기 + `StartTestModeMatch`.

## 구현

### TestModeConfig (SO)
- `DefenderUnitData[] defenderPreset` — 드래프트 스킵 시 반입 디펜더.
- `WavePlanAsset[] planCatalog` — 아웃게임 피커(unit 4)에 노출할 플랜 목록.

### TestModeContext (static)
- `bool Active`, `WavePlanAsset Plan`, `DefenderUnitData[] DefenderPreset`.
- `Set(plan, preset)` / `Clear()`. GameManager 비영속이라 씬 경계를 static 으로 넘기고 `Start` 가 1회 소비.

### GameManager.Start 분기 (squad 보다 우선)
```
if (TestModeContext.Active && battleBridge != null) { StartTestModeMatch(); return; }
```
`StartTestModeMatch`: plan/preset 읽고 `TestModeContext.Clear()` →
`SetMapGenerationOptions(Default)` → `PrepareDraftMap()` → `SetAuthoredWavePlan(plan)` →
preset 있으면 `SetDefenderPool(preset)` → 스킬 `Roll()`+`SetSkillLoadout` →
`MapSetupRequested?.Invoke()` else `PlacementRequested?.Invoke()`. (StartSquadMatch 미러)

## 완료 기준

- 컴파일 0 에러, EditMode green(기존 유지).
- `TestModeContext.Active=false` 면 기존 squad→draft→fallback 분기 무변경(비파괴).
- 테스트 모드 진입 시 드래프트 UI 없이 placement 로 진행 + 작성 플랜이 bridge 에 set 됨.
- 실제 아웃게임 버튼/피커 배선 + Play 검증은 unit 4. 여기선 분기 컴파일 + reflection in-memory 진입 확인까지.

---

*완료 확인*: 2026-06-16 — 컴파일 0, EditMode 326 pass/0 fail(회귀 0). TestModeConfig 에셋 참조 정상(디펜더 4 + 플랜 1, 8웨이브). 실 Play 진입은 unit 4. 커밋 `__PENDING__`.
