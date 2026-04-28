# 10. Handoff Summary — Draft UX Upgrade (스펙 종료)

## Commit
- 미커밋 (Unity Editor 정상화 → `error CS` 0 확인 → 단일 커밋 또는 task별 커밋)

## Implemented (전체 스펙 완료 목록)

### Core (task 1–2)
- `GamePhase.Briefing` 제거 — `GameManager.Start` 가 곧장 Draft 진입.
- `DraftSession` 폐기 모델: `Discarded` 집합, `MaxDiscards=3`, `IsFull`, `Picked = Pool − Discarded`. `ToggleDiscard` 신규, `TogglePick` 제거.
- `DraftController.discardCount = 3`, `BattleLogSchema.DraftRecord` 코멘트 갱신.
- `DraftSessionTests` 10케이스 전면 재작성.

### UI — Wave Strip (task 4 → 9번 재디자인 → 이번 세션)
- **레이아웃**: 헤더 anchor (0.5, 1) 상단 기준 y=−100 (100px 낙하). 카드 `ScrollRect(1600px) → Viewport(Mask) → Content(HorizontalLayoutGroup)` 좌정렬, 수평 스크롤.
- **Unroll()**: overlay dim → 헤더 OutBounce 낙하 + shake → 카드 staggered alpha fade-in → group pulse.
- **FadeIn() 신규**: 토글 재등장 시 단순 alpha tween (위치 유지).
- **Roll()**: 헤더·카드 물리적 오프스크린 fly-out (InCubic, alpha 없음), overlay만 fade. `SnapHidden()` → overlay `SetActive(false)` (fan input 차단 버그 수정).
- **토글 동작**: `Hidden → FadeIn()` (DwellInterrupt 없음), `Shown → Roll()`.

### UI — Draft Card Fan (task 5)
- FanRoot anchor (0.5, 0), 10장 런타임 빌드.
- `PlayEnterSequence`: staggered slide-in (0.04s × N + 0.45s OutQuad).
- `LayoutRemaining`: 생존 카드 0.20s OutQuad 재배치.
- **PlayDiscardCard 속도 분기**:
  - `vel > 200 px/s`: `exitDir = vel.normalized`, `endRot = -atan2(vel.x, vel.y)`, `Ease.Linear` (진행 방향 정렬, 등속).
  - `vel ≤ 200`: random xJitter ± 80, `Ease.OutCubic` (클릭/느린 스와이프).
- **버그 수정**: `_cards.Remove(card)` 시퀀스 시작 전으로 이동 → `LayoutRemaining`이 toss 트윈을 덮어쓰던 문제 제거.

### UI — DraftCardView (task 6)
- `IBeginDrag/IDrag/IEndDrag/IPointerClick`. 스와이프 ≥120px AND ≤0.45s → 폐기.
- `_discardFired` 더블 발화 가드, `_dragHappened` drag/click 혼동 가드.
- `_lastVelocity` 매 프레임 갱신 (`canvasDelta / unscaledDeltaTime`), `LastVelocity` 프로퍼티 노출.

### UI — DraftView 오케스트레이터 (task 7)
- Sub-state `Idle → Unrolling → Dwelling → Rolling → Drafting → Confirming`.
- `dwellSeconds = 2.0f`. 재진입 가드 (`Tween.StopAll` + SnapHidden + CancelInProgress).
- 마지막 toss `Sequence.OnComplete` 후 자동 `TryConfirm`.
- Fan: strip Roll 완료 후에만 `SetActive(true)` + Build + PlayEnterSequence.

### 기타
- `MapSettingsPanelView` 좌상단 토글 (task 3).
- `TimelineBriefingView` 삭제.
- `Wassup.Runtime.asmdef` PrimeTween.Runtime 참조 추가.
- `Wassup.Tests.PlayMode.asmdef` + `DraftFlowSmokeTest` 신규.

## Key Files

```
Assets/_Project/Scripts/Core/GameManager.cs
Assets/_Project/Scripts/Core/DraftSession.cs
Assets/_Project/Scripts/Core/DraftController.cs
Assets/_Project/Scripts/Logging/BattleLogSchema.cs
Assets/_Project/Scripts/UI/Draft/WavePatternStripView.cs
Assets/_Project/Scripts/UI/Draft/DraftCardFanView.cs
Assets/_Project/Scripts/UI/Draft/DraftCardView.cs
Assets/_Project/Scripts/UI/Draft/DraftView.cs
Assets/_Project/Scripts/UI/Draft/MapSettingsPanelView.cs
Assets/_Project/Tests/EditMode/DraftSessionTests.cs
Assets/_Project/Tests/PlayMode/DraftFlowSmokeTest.cs
Assets/_Project/Scripts/Wassup.Runtime.asmdef
```

## Verified

- `Editor.log` grep `error CS` — Wassup/_Project 관련 에러 0건 (확인 시점: 이전 세션).
- PrimeTween API 전수 확인 완료 (task 0).
- `DraftSessionTests` 10케이스 compile-level 작성 완료.
- Unity Editor 미연결 상태 — PlayMode 테스트 실행 및 Play smoke는 Editor 정상화 후.

## Notes

- Roll() 에서 header/cardGrid alpha를 트윈하지 않으므로 SnapHidden() 시 alpha=1 상태. SnapHidden이 즉시 0으로 리셋하나 두 요소가 화면 밖(y=150, y=700)이어서 시각적으로 무해.
- ScrollRect Viewport Mask Image color=(1,1,1,0.01) — alpha=0이면 Mask 비작동.
- 카드 진행 방향 회전: `−atan2(vel.x, vel.y)` — 카드 local Y가 속도 벡터 방향을 향함.

## Follow-up (씬 wiring — Unity 정상화 후)

1. `Editor.log` `error CS` 0 확인.
2. 씬에서 구 `TimelineBriefing` GameObject 제거.
3. `DraftView` GameObject: missing-script 정리 → `Wassup.UI.Draft.DraftView` 부착 + SerializeField (`controller`, `strip`, `fan`, `mapSettings`, `dwellSeconds=2.0`) 연결.
4. 자식 GO 추가: `WavePatternStrip` (`WavePatternStripView`, deck 연결), `DraftCardFan` (`DraftCardFanView`), `MapSettingsPanel` (`MapSettingsPanelView`).
5. Test Runner PlayMode → `DraftFlowSmokeTest` PASS.
6. Play smoke: 시작 → 헤더 낙하 → 카드 좌정렬 fade-in → 2s dwell → 오프스크린 Roll → fan 등장 → 카드 3장 폐기 → Placement 진입.
7. 완료 후 커밋 해시를 본 파일 Commit 섹션에 기재.
