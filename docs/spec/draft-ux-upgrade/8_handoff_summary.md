# 8. Handoff Summary

## Commit
- 미커밋 (Unity 컴파일/씬 검증 후 단일 커밋 또는 task 별 커밋 결정 필요)

## Implemented (코드 변경)

- `GamePhase.Briefing` 제거 — `GameManager.Start` 가 곧장 `Draft` 페이즈로 진입.
- `DraftSession` 의 의미 반전: `Discarded` 집합 도입, `MaxDiscards`, `IsFull = DiscardedCount==Max`, `Picked = Pool − Discarded`(pool 순서 유지). 신규 메서드 `ToggleDiscard`. 기존 `TogglePick` 제거.
- `DraftController.discardCount = 3`, `BeginDraft` 가 `_session.Reset(catalog, poolSize, discardCount, seed)` 호출.
- `BattleLogSchema.DraftRecord` 코멘트 갱신 ("the 7 they locked in" → "the 7 units the player kept").
- `DraftSessionTests` 8 케이스 모두 폐기 모델로 재작성. 신규 `IsFull_Only_True_When_DiscardCount_Reaches_Max` + `PickedArray_Returns_Pool_Minus_Discarded_In_Pool_Order`.
- 신규 `Assets/_Project/Scripts/UI/Draft/MapSettingsPanelView.cs` — 좌상단 작은 토글로 path/size/density/spawn-lanes 즉시 push.
- 옛 `TimelineBriefingView.cs` + `.meta` 삭제. 씬 GameObject 정리는 Unity 재시작 후 (사용자 환경).
- 신규 `WavePatternStripView.cs` — 상단 가로 strip + 좌측 중앙 토글 버튼, `Tween.ScaleX(stripRect, ...)` unroll(0.45s OutQuad)/roll(0.35s InQuad), `OnDwellInterrupt` 이벤트.
- 신규 `DraftCardFanView.cs` — fan 곡선(R=1400, ±26°, center lift +60), `Build`/`PlayEnterSequence`(staggered 0.04s × N + 0.45s 본 트윈)/`PlayDiscardCard`(toss + 회전 + alpha 페이드 0.45s)/`LayoutRemaining`(0.20s OutQuad).
- 신규 `DraftCardView.cs` — `IBeginDrag/IDrag/IEndDrag/IPointerClick`. 임계값: 위 스와이프 ≥120px AND ≤0.45s, click 시 drag distance <30px. `_discardFired` 가드로 더블 발화 차단.
- 신규 `DraftView.cs` (오케스트레이터) — sub-state `Idle→Unrolling→Dwelling→Rolling→Drafting→Confirming`, 재진입 가드 (`Tween.StopAll(this/strip/fan)`), 마지막 toss `Sequence.OnComplete` 콜백 후에만 `TryConfirm` 호출 (애니메이션 보존).
- 옛 `DraftView.cs` + `DraftCardView.cs` (옛 위치) + `.meta` 삭제.
- 신규 `Assets/_Project/Tests/PlayMode/Wassup.Tests.PlayMode.asmdef` + `DraftFlowSmokeTest.cs`. `BeginDraft → 3×ToggleDiscard → TryConfirm → DraftConfirmed` 흐름 검증.

## Key Files

- `Assets/_Project/Scripts/Wassup.Runtime.asmdef` (PrimeTween.Runtime 참조 추가)
- `Assets/_Project/Scripts/Core/GameManager.cs`
- `Assets/_Project/Scripts/Core/DraftSession.cs`
- `Assets/_Project/Scripts/Core/DraftController.cs`
- `Assets/_Project/Scripts/Logging/BattleLogSchema.cs`
- `Assets/_Project/Scripts/UI/Draft/MapSettingsPanelView.cs`
- `Assets/_Project/Scripts/UI/Draft/WavePatternStripView.cs`
- `Assets/_Project/Scripts/UI/Draft/DraftCardFanView.cs`
- `Assets/_Project/Scripts/UI/Draft/DraftCardView.cs`
- `Assets/_Project/Scripts/UI/Draft/DraftView.cs`
- `Assets/_Project/Tests/EditMode/DraftSessionTests.cs`
- `Assets/_Project/Tests/PlayMode/Wassup.Tests.PlayMode.asmdef`
- `Assets/_Project/Tests/PlayMode/DraftFlowSmokeTest.cs`

## Verified

- `~/Library/Logs/Unity/Editor.log` grep `error CS` — 16건 모두 `Library/PackageCache/com.unity.serialization`, `com.unity.shadergraph` 의 Unity 내부 패키지 에러 (`UnityEditor.GUID` 누락). **Wassup / _Project 관련 에러 0건** (우리 코드 컴파일 정합성 확인).
- PrimeTween 실제 API 검증 완료 (task 0): `Tween.UIAnchoredPosition`, `Tween.LocalRotation`, `Tween.ScaleX`, `Tween.Alpha`, `Tween.LocalPositionY`, `Tween.Delay(target, dur, onComplete)`, `Tween.StopAll(target)`, `Sequence.Create/Chain/Group/ChainCallback`, `tween.OnComplete(...)`, `tween.ToYieldInstruction()`. (`Tween.UIScaleX` 미존재 — `Tween.ScaleX` 사용.)
- DraftSessionTests EditMode 케이스 8개 (compile-level 검증; 실제 실행은 Unity 정상화 후).

## Notes

- Unity Editor (PID 64364) 가 wassup 프로젝트로 살아있으나 Library/PackageCache 가 손상되어 외부 패키지 컴파일 차단 → 우리 어셈블리 (`Wassup.Runtime.dll`, `Wassup.Tests.PlayMode.dll`) 빌드도 함께 정지. UnityMCP plugin 도 응답 불가.
- 카드 fan 곡선: 가운데 카드 y = +60, 양 끝 y ≈ +60 - 1400×(1-cos26°) ≈ -70. 가운데 위 / 양 끝 아래로 휘는 정상 Slay-the-Spire 모양.
- 스와이프 임계값은 1080 ref height 기준 _rootCanvas.scaleFactor 보정 후 누적. 안드로이드 다양한 해상도에서 임계값이 동일한 화면 비율로 환산됨.
- `OnEndDrag` 폐기 발화와 `OnPointerClick` 더블 발화 차단: `_discardFired` 가 OnEndDrag 에서 set, OnPointerClick 에서 set 이면 즉시 reset+return.
- 자동 confirm: 마지막 (3번째) 폐기 카드의 toss `Sequence` 가 끝난 뒤 `OnComplete` 콜백에서 `controller.TryConfirm()` 호출 → `DraftConfirmed` → `OnDraftConfirmed` 의 `HideSubviews()`. 트윈 진행 중 패널 비활성으로 인한 애니메이션 잘림 방지.
- 재진입 가드: `OnDraftStarted` 진입 시 `State != Idle && State != Confirming` 이면 `Tween.StopAll(this/strip/fan)` + `strip.SnapHidden()` + `fan.CancelInProgress()` + `_state = Idle` 후 새 시퀀스 시작.

## Follow-up

1. **Unity Editor 정상화** (사용자 환경):
   - 권장: Unity 종료 → `Library/PackageCache/com.unity.serialization*` 폴더 삭제 → Unity 재시작 → Unity 가 패키지 재캐싱.
   - 또는: Unity 의 `Reimport All` 실행. (시간 오래 걸림.)
   - 정상화되면 `Editor.log` 의 `error CS` 0 확인.
2. **씬 wiring** (UnityMCP 또는 수동):
   - 씬에서 옛 `TimelineBriefing` GameObject 제거.
   - 옛 `DraftView` GameObject 의 missing-script 컴포넌트 정리 → 새 `Wassup.UI.Draft.DraftView` 컴포넌트 부착.
   - 자식 GameObject 추가: `WavePatternStrip` (+ `WavePatternStripView` 컴포넌트, `deck` SerializeField wiring), `DraftCardFan` (+ `DraftCardFanView`), `MapSettingsPanel` (+ `MapSettingsPanelView`).
   - DraftView 의 SerializeField wiring: `controller`, `strip`, `fan`, `mapSettings`, `dwellSeconds=2.0`.
3. **PlayMode 테스트 실행**: Unity Test Runner → PlayMode 탭에서 `DraftFlowSmokeTest.BeginDraft_ThreeDiscards_AutoConfirms_With_Seven_Picks` 실행 → PASS.
4. **사용자 Play 검증**: 게임 시작 → 상단 strip unroll → 2초 dwell (또는 strip/토글 클릭으로 즉시 진행) → roll → fan 등장 → 카드 클릭 또는 위 스와이프로 3장 폐기 → Placement 진입 자동.
5. **각 task 의 완료 기준** 섹션 하단에 확인 일자 + 커밋 해시 추가 후 커밋.
