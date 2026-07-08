# 5 — 배틀 HUD phase 게이팅 + 결과 스탯 표기

## 목적

(사용자 요청 2026-07-08) 결과(리더보드) 단계에서 오른쪽 하단 dock(남은시간 + NEXTWAVE)이 계속 떠 있는 문제를 없애고, 남은시간·누수 수량을 결과 팝업에 표기한다.

## 변경 대상

- `Assets/_Project/Scripts/UI/NextWaveDock.cs` — 표시 조건을 phase 기반으로 전환
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — game-over 시 `Result` phase 진입 + 결과 스탯 전달
- `Assets/_Project/Scripts/UI/ResultScreen.cs` — 결과 스탯 라인

## 구현

- **dock = Battle 전용**: `NextWaveDock` 이 `DraftConfirmed/PlacementRequested/DraftStarted` 이벤트 대신 `GameManager.PhaseChanged` 를 구독해 `GamePhase.Battle` 일 때만 표시(`ScoreHudView` 선례, lazy `EnsureSubscribed`). 안 쓰게 된 `draftController` 필드 제거. → Draft/Placement/Result 에서 숨김.
- **game-over → Result phase**: 지금껏 선언만 되고 미사용이던 `GamePhase.Result` 를 `BattleBridge.ReportMatchResult`(승/패 3경로 공통 tail)에서 `SetPhase(GamePhase.Result)` 로 진입. Battle-gated HUD(dock·ScoreHud·CostDisplay·SkillBar)가 일괄 비활성화된다(결과 dim 2000 이 덮으므로 시각 변화 없음, 논리적 정합). RESTART 은 `BeginPlacementPhase` 로 `Result → Placement → Battle` 재전환 → 재표시.
- **결과 스탯**: `ResultScreen.ShowVictory/ShowDefeat` 에 `(int score, float remainingSec, int leaks)` 오버로드 추가. 헤더 "YOUR SCORE" 아래 `TIME m:ss   LEAKS n` 라인(뮤트 스틸색, stats 미전달 시 숨김). 누수 = `_goalReachedCount`, 남은시간 = game-over 시점 `_timerDuration - _battleClock`(`RemainingBattleSeconds()`; `victory_timeout` 은 0). 기존 무인자/score-only 오버로드는 stats 없이 유지.

## 완료 기준

- [x] compile 0 에러
- [x] 프리뷰: 결과 팝업 헤더에 `TIME 0:42 LEAKS 3` 렌더, 행과 안 겹침
- [x] 인게임(사용자 확인): 게임 종료 시 dock 사라짐 + 리더보드에 TIME/LEAKS 표기, RESTART 시 dock 복귀

확인: 2026-07-08 · 사용자 인게임 확인 통과.
