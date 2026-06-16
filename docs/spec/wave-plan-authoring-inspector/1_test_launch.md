# 1 — 테스트 런치 (버튼 + 부트스트랩)

## 목적

인스펙터에서 "▶ Test this plan" 한 번으로 그 `WavePlanAsset` 을 BattleScene Play 로 띄운다. Play 진입 시 도메인 리로드로 static 이 초기화되므로 `SessionState` 로 플랜을 캐리하고 `EnteredPlayMode` 에서 `TestModeContext.Set` 한다.

## 변경 대상

- 신규: `Assets/_Project/Editor/WavePlanTestLauncher.cs` (`[InitializeOnLoad]`).
- `Assets/_Project/Editor/WavePlanAssetEditor.cs` — 상단에 테스트 버튼 추가.

## 구현

### WavePlanTestLauncher
- `LaunchInPlayMode(WavePlanAsset plan)`: 플랜 GUID 를 `SessionState("WavePlanTest.guid")` 저장 → `SaveCurrentModifiedScenesIfUserWantsTo()` → `OpenScene(BattleScene)` → `EditorApplication.isPlaying = true`.
- static ctor 에서 `playModeStateChanged` 구독. `EnteredPlayMode` 시 SessionState GUID 읽어 플랜 로드 → `Wassup.Core.TestModeContext.Set(plan, null)` → 키 제거. (디펜더는 GameManager 저장 스쿼드 반입.)

### WavePlanAssetEditor 버튼
- OnInspectorGUI 상단: `EditorApplication.isPlaying` 이면 비활성. 클릭 → `WavePlanTestLauncher.LaunchInPlayMode(target)`.

## 완료 기준

- 컴파일 0.
- Play 검증: SessionState 캐리 → EnteredPlayMode 가 GameManager.Start 보다 먼저 `TestModeContext` 세팅 → BattleScene 이 작성 플랜으로 진입(`_authoredPlan`/`_usingAuthoredPlan`/endless). 1회 소비 후 키 제거.
- 아웃게임 TEST MODE 피커 경로 무변경(병행).

---

*완료 확인*: 2026-06-17 — 컴파일 0. **타이밍 수정**: `playModeStateChanged(EnteredPlayMode)` 는 GameManager.Start 보다 늦어 부적합 → `TestModeContext` 에 `[RuntimeInitializeOnLoadMethod(BeforeSceneLoad)]` 훅(#if UNITY_EDITOR) 으로 소비. Play 검증: SessionState arm → BeforeSceneLoad 가 Start 전 무장 → GameManager 소비(Active=False), `_authoredPlan=Sample Test Plan`, phase=Placement, StartBattle 시 usingAuthored/timer=0/waves=8. 새 에러 0. 커밋 `__PENDING__`.
