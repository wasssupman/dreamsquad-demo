# 2 — Handoff Summary (wave-plan-authoring-inspector)

## Commit

- `89b8064` 0 WavePlanAsset 경량 작성 CustomEditor
- `b165d5c` 1 'Test this plan' 런치 (버튼 + BeforeSceneLoad 캐리 훅)
- 문서 해시: `89b8064` 직후 docs 커밋 · 본 커밋(1 해시 + handoff + README 완료)

## Implemented

- `WavePlanAssetEditor`([CustomEditor]) — 웨이브/그룹 추가·삭제·복제(InsertArrayElementAtIndex)·이동(MoveArrayElement), 인라인 편집(unit/triggerTimeSec/count), 검증 HelpBox(경고만), 읽기전용 타임라인 바(그룹 마커), 플랜 요약(웨이브수/총길이/총스폰). SerializedProperty 기반(Undo/dirty 정상).
- 인스펙터 상단 "▶ Test this plan" 버튼 → `WavePlanTestLauncher.LaunchInPlayMode`.
- `WavePlanTestLauncher`(에디터) — 플랜 GUID 를 `SessionState("WavePlanTest.guid")` 적고 BattleScene Open + Play.
- 소비는 `TestModeContext.ApplyEditorTestCarry`([RuntimeInitializeOnLoadMethod(BeforeSceneLoad)], #if UNITY_EDITOR) — GameManager.Start 보다 먼저 SessionState 읽어 `TestModeContext.Set(plan, null)`. (playModeStateChanged 는 Start 보다 늦어 부적합 — 검증으로 확인.)

## Key Files

- `Assets/_Project/Editor/WavePlanAssetEditor.cs` (인스펙터 + 테스트 버튼)
- `Assets/_Project/Editor/WavePlanTestLauncher.cs` (런치 트리거)
- `Assets/_Project/Scripts/Core/TestModeContext.cs` (BeforeSceneLoad 캐리 훅 — #if UNITY_EDITOR)

## Verified

- 컴파일 0, 콘솔 에러 0(기존 BattleScene "missing script"만 잔존, 무관).
- `Editor.CreateEditor` → `Wassup.Editor.WavePlanAssetEditor` 등록 확인. SerializedProperty add/group/dup/move/delete 무예외.
- 테스트 런치 Play: SessionState arm → BeforeSceneLoad 가 Start 전 무장 → GameManager 소비(Active=False) → `_authoredPlan=Sample Test Plan`, phase=Placement, StartBattle usingAuthored/timer=0/waves=8.

## Notes

- **신규 에디터 .cs 는 import 지연** — `refresh_unity scope=scripts` 만으론 Assembly-CSharp-Editor 에 안 잡힘. `scope=all force` 후 인식(CreateEditor 가 GenericInspector→정상 전환으로 확인). 다음 에디터 스크립트 추가 시 주의.
- **타이밍**: 에디터→Play 캐리는 반드시 `RuntimeInitializeOnLoadMethod(BeforeSceneLoad)` 로 소비. `playModeStateChanged(EnteredPlayMode)` 는 scene Start 이후라 늦음(실측).
- 런타임 결합 최소화: 캐리 훅은 `TestModeContext` 안에 `#if UNITY_EDITOR` 로 격리 — 빌드 strip.

## Follow-up

- 타임라인 **드래그 이동** 핸들(현재 읽기전용 시각화).
- 전용 EditorWindow, import/export, 적 썸네일 프리뷰.
- (무관) BattleScene "missing script (Unknown)" 정리, 타 씬 한글 UI 라벨 전수 영문화.
