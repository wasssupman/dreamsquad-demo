# 5 — Handoff Summary

무한 모드 구현 종료 인계 지도. 최신 계약은 README/번호 문서가 우선.

## Commit

- `573da5d3` unit 0 — BattleMode enum + AttackDeck 필드(battleMode, fixedWaveIntervalSec)
- `90d4e45b` unit 1 — 생성기 고정 간격 + EditMode 테스트
- `9babf756` unit 2 — BattleBridge 모드 인지(진입/누수/시간0/리포트)
- `5db07211` unit 3 — Deck_Endless + endlessEncounter 배선 + 패널 토글
- `37eb26d8` unit 4 — 스모크(PlayMode) + 리스크/리워드(EditMode)
- `39194252` 완료 — handoff + README 상태
- `7a6304d3` unit 6 — 무한 모드 누수 HUD 한계/위기색 숨김(개수만, 뷰만)
- (스펙: `5463bb87` 착수, `79d97705` critic 반영)

## Implemented

- `AttackDeck.battleMode`(Main/Endless) enum 하나가 모드 seam. `fixedWaveIntervalSec`(0=기존 파생).
- 생성기: `fixedIntervalSec>0` 이면 `interval=고정값`, `triggerTime[i]=i*interval` 계약 불변.
- BattleBridge: `IsEndless` 로 4분기 — 전용 `endlessEncounter` 진입(공용 풀 밖), 누수 게이트
  `defeatEnabled=!IsEndless`(무한은 안 죽음), `remainingMs=IsEndless?0`(시간축 0), 토너먼트 리포트 스킵.
- 진입: `DevMapOverride.Endless`(PlayerPrefs) + BattleBridge 전용 `endlessEncounter`. mapPool 미변경 →
  랜덤/토너먼트 맵 선택 byte-identical(회귀 0).
- 데이터: `Deck_Endless`(battleMode=Endless, waveSeed 20260807, waveCount 30, 간격 10, timer 180,
  defeatGoalReachedCount 100=스트레스 예산). 메인 `scoreRules`·`ScoreMath` 재사용(엔드리스 전용 SO 없음).
- `DevMapOverridePanel`: ◀▶ 스텝 사이클에 ENDLESS 슬롯(코드만, 새 GO 없음).
- (unit 6) 누수 HUD: 무한 모드는 죽는 한계가 없어 `SetLeakStatus(...,showLimit:!IsEndless)` 로
  "/한계"·위기색 숨기고 누수 개수만 표시. 뷰만 — 점수/예산 불변.

## Key Files

- `Assets/_Project/Scripts/Data/BattleMode.cs`, `AttackDeck.cs`, `WavePatternGenerator.cs`
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` (IsEndless, 선택블록 869~, DrainGoalEvents,
  CalculateBattleScore, ReportMatchResult)
- `Assets/_Project/Scripts/Core/DevMapOverride.cs`, `UI/DevMapOverridePanel.cs`
- `Assets/_Project/Scripts/Data/Decks/Deck_Endless.asset`, `Scenes/BattleScene.unity`(endlessEncounter)
- 테스트: `Tests/EditMode/WaveFixedIntervalTests.cs`, `EndlessScoreTests.cs`,
  `Tests/PlayMode/EndlessModeSmokeTest.cs`

## Verified

- 컴파일 0 에러(MCP 강제 리컴파일). full EditMode **1298/1298**(0 실패, 2 기존 [Ignore] skip).
- PlayMode `EndlessModeSmokeTest` 1/1 — 부팅·30웨이브·10초간격·mapPool 불변·누수무사망.
- EditMode `EndlessScoreTests` 3/3 — 시간0·누수 선형감소·예산 초과 saturation.

## Notes (되돌리면 안 되는 의도)

- **엔드리스는 공용 mapPool 에 절대 넣지 말 것.** 전용 `endlessEncounter` + `DevMapOverride.Endless`
  진입이 토너먼트 회귀 0 의 핵심(critic MAJOR#2). 풀에 넣으면 `seed%count` 가 밀림.
- **누수 예산(`defeatGoalReachedCount`)은 높게(100).** 낮으면 안 죽는 엔드리스에서 예산 초과 누수가
  공짜가 돼 리스크/리워드 붕괴(critic MAJOR#3, `ScoreMath` stress 0 floor).
- 시간축 0 은 `CheckVictory` 비활성 아닌 `remainingMs=IsEndless?0` 한 줄(조기클리어 유지 + soft-lock 회피).
- BattleScene 커밋은 `endlessEncounter` hunk만 수술적 스테이징 — 세션 시작부터 있던 무관한 씬 편집
  (RawImage/GameObject)은 **미커밋 유지**(사용자 소유).

## Follow-up (미착수)

- **개수 기반 엔드리스 패배**(누수 N→종료). v1 무제한만. 추가 시 `!IsEndless`→`!(IsEndless && bool)` + 덱 필드.
- **엔드리스 전용 ScoreRules**(킬 vs 누수 가중). v1 은 메인 재사용.
- **플레이어용 "무한 모드" 선택 버튼**(아웃게임 UI). v1 은 dev 스텝 슬롯만.
- 밸런싱: 킬/스트레스 실튜닝, 웨이브 난이도 곡선(현재 Serpent 풀 재사용).
- 엔드리스 리더보드/기록, 정규 로테이션 편입 정책.
