# 4 — Handoff Summary

battle-hud-score-timer-menu 구현 종료 인계. 최신 계약은 README + 번호 문서 우선.

## Commit
- `16fb59ed` feat(battle-hud): 스코어 중앙 강조 + 타이머 NextWaveDock 이관 — score-timer-menu 0~1
- (작업 2~3 + 레이아웃 수정) feat(battle-hud): 메뉴 팝업 + 웨이브 스트립 + "!" 제거
- (별도) fix(cost): CostRuntime 리젠을 Battle 시간도메인으로 — 정지/감속 반영

## Implemented
- 스코어가 상단 중앙을 크게 차지(y−24, 값 104pt, 캡션 34pt). 중앙 타이머 은퇴.
- 남은 시간 → 우하단 `NextWaveDock`(2단: 타이머 + NEXT WAVE 버튼, dimmed 백킹 판).
- `BattleBridge` 는 NextWave UI 를 만들지 않음. 읽기 getter(`NextWaveAvailable/HasNext/Number`)만 노출, `ForceNextWave()` 유지.
- 메뉴 버튼 → 정지형 `MenuPopup`(`TimeManager` Battle=0). [재개] 재개, [나가기] 아웃게임. 한글 버튼(Jua SDF 동적 글리프).
- 팝업이 `WavePatternStripView` `FadeIn()/Roll()` 구동. 배틀 중 "!" 토글 완전 제거(draft·squad 공용이라 드래프트 재열람도 은퇴).
- 팝업 레이아웃: 중복 dim 제거(스트립 오버레이가 유일 dim), 스트립 `SetSortingOverride(950)` 로 팝업(960) 아래 안 깔리게, 버튼 화면 하단 배치.
- 코스트 리젠이 Battle 도메인 스케일 반영(정지 시 0, 슬로우모 비례).
- (작업 5) 점수 UI 배지화: 절차적 라운드 플레이트(SDF 9-slice) + 골드 테두리 + "SCORE" 골드 탭. 배지를 화면 우상단 모서리(cornerPadding 36px)로 이동.

## Key Files
- `Assets/_Project/Scripts/UI/NextWaveDock.cs` (신규) — 우하단 타이머+웨이브 dock
- `Assets/_Project/Scripts/UI/MenuPopup.cs` (신규) — 정지형 팝업
- `Assets/_Project/Scripts/UI/ScoreHudView.cs` — 레이아웃 상향
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — NextWave UI 제거 + getter
- `Assets/_Project/Scripts/UI/Draft/WavePatternStripView.cs` — "!" 제거 + `SetSortingOverride`
- `Assets/_Project/Scripts/UI/Outgame/ReturnToMenuButton.cs` / `SquadPrepView.cs` / `Draft/DraftView.cs`
- `Assets/_Project/Scripts/Core/CostRuntime.cs` — 시간도메인 픽스
- `Assets/_Project/Scenes/BattleScene.unity` — NextWaveDock/MenuPopup 배선, TimerDisplay 제거
- 삭제: `Assets/_Project/Scripts/UI/TimerDisplay.cs`

## Verified
- compile 0에러. Play 진입 0예외.
- 스코어 y−24/104pt, dock 타이머 카운트다운 + NEXT WAVE 버튼, 중앙 타이머 없음.
- 팝업 OPEN→Battle scale 0·strip Unrolling·strip override@950·popup@960·버튼 하단, CLOSE→scale 1·override 해제.
- 코스트: 정상 +2/s, 정지 +0, 0.3x +0.6.
- WaveToggle GameObject 부재. 사용자 Play 확인 통과(레이아웃 3종 포함).

## Notes
- 씬 저장 시마다 `DamageNumberSpawner.sparkColorBoost:2.2` 재유입(HEAD 미직렬화·코드 기본값). 매 커밋서 해당 라인 제거함 — damage-number 소관, 근본해결은 별도 1회 chore. `[[project_battlescene_save_readds_sparkcolorboost]]`
- 정지/감속은 반드시 `TimeManager`(Battle 도메인) 경유. `Time.timeScale` 은 1 고정. `[[project_time_manager_timescale_ban]]`
- 스트립은 자체 Canvas 없이 DraftView 캔버스(order 5)에 렌더 → 팝업용 `SetSortingOverride` 로 부스트하는 구조. 되돌리지 말 것.
- 세션 중 무관 dirty(폰트 asset 동적 베이크, QualitySettings 등)는 커밋에서 제외함.

## Follow-up
- (선택) 드래프트 카드픽 단계 웨이브 재열람 창구 — 필요 시 별도 검토(현재 인트로 1회 노출만).
- (선택) sparkColorBoost 재유입 근본 chore 커밋.
- 다음: 개선안 원안의 "상단 통합 바(edge-to-edge)" 형태는 이번에 보류 — README 후속 후보 참조.
