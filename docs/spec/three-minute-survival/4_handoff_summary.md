# 4 — Handoff (units 0~3 구현, 커밋 전 / units 6~7 추가)

## Commit

- `69bdf7cb` docs(three-minute-survival): 3분 생존·처치 점수 개편 스펙 + 공성 분리
- `a7d1b015` feat(three-minute-survival): units 0~3 — 안정도 패배·처치 점수·이벤트 구동 웨이브

**푸시 안 함**(승인제). Unity 검증은 커밋 이후로 넘겼다 — 아래 "Verified/Follow-up" 참조.

## units 6~7 (2026-08-15 · `1e8c1a90`) — 점수 제출 생값화 + 마감 파이프라인

- **unit 6**: 제출값 인코딩(`1e9 + kill×1000 + 안정도permille`)과 디코딩 3곳을 통째로 제거.
  **서버에 가는 수 = 화면에 보이는 수 = 처치 점수 합**. 안정도는 점수 경로에서 완전히 빠졌고
  동점은 그냥 동점이다. 구 인코딩 기록은 변환하지 않고 원값(10억대)으로 둔다(사용자 결정).
- **unit 7**: 판 마감을 `BattleBridge.EndMatch` 단일 관문으로 수렴 — 종료 5경로가 복붙하던
  의식 6줄이 한 줄씩으로. 취합(`BuildTally` → `MatchTally`) → 기록 → 통보 → 표시.
  `ScoreMath` 는 `MatchTally` 에 흡수·삭제(점수 정본이 둘이 되지 않게). 동작 변경 0.
- 검증: 컴파일 0 에러 · EditMode 56/56 · PlayMode `TallyFlowTest` 초록.
  **Play 육안 확인은 대기**(`6_score_submit_raw.md`·`7_match_end_pipeline.md` 완료 기준 참조).

## Implemented

- **골 안정도**(unit 0): `AttackDeck.goalStabilityMax`(20) − 유출한 적의
  `AttackUnitData.stabilityDamage`(일반 1/엘리트 2/보스 5). 0 = **유일한 패배 조건**.
  스트레스 한계 패배 게이트 제거, 배지는 개수만 표시(`showLimit: false`). 엔드리스도 패배한다.
- **안정도 바**(unit 1): `OverheadBarSkin` enum(Defender/Enemy/GoalStability) + `BarSkin` 3번째
  직렬화 필드 + 수치 라벨. 골 셀마다 1개, `UnitOverheadUiLayer.SetStability(골인덱스, …)`
  (엔티티 풀과 분리된 슬롯). 앵커 = 구조물 시각 앵커 + `goalStabilityBarLift`.
- **웨이브 케이던스**(unit 2): 전멸(`NoQueuedAttackersRemain`) 또는 트리거 후
  `maxWaveIntervalSec`(20초) → 다음 웨이브. `_battleClock` 기준. 작성 플랜은 기존 시각 스케줄
  유지. 수량 곡선 = `ExponentialWaveTotal`(base 5 · growth 1.12 · cap 24 · spacing 0.5).
  당기기 UI 제거(도크 = `웨이브 N / M` + 다음 N초), 클리어 어필·`NextWaveClearReady` 은퇴.
  브리핑 스트립 카드 상한 12.
- **점수**(unit 3): `ScoreMath.Evaluate(killScoreTotal)` — 처치만. `killScore` 재장전
  (1/3/10). ~~제출값 = `1e9 + 처치점수×1000 + 안정도permille`, 표시 3곳 디코딩~~ →
  **unit 6(2026-08-15): 인코딩 폐기, 제출·표시 모두 생값.**
  탤리 제거(스크립트·씬 오브젝트 삭제), 결과 3줄(처치 N기 / 남은 안정도 X/Max (Y%) / 도달 웨이브).
  `ScoreRulesData` 빈 SO 로 은퇴, 로거 `time_score`/`stress_score` 은퇴.

## Key Files

- `Scripts/Bridge/BattleBridge.cs` — 안정도 상태·유출 차감·패배, `QueueDueWaves`(이벤트 구동),
  `SyncGoalStabilityBars`, `EndMatch`/`BuildTally`(unit 7 — 마감 단일 관문)
- `Scripts/Core/MatchTally.cs` — 판 성적 값(순수). unit 7 이 `ScoreMath.cs` 를 흡수·삭제
- `Scripts/Data/WavePatternGenerator.cs` — `ExponentialWaveTotal`, 스폰 창 경고
- `Scripts/Data/AttackDeck.cs` · `AttackUnitData.cs` — 신규 필드 4개
- `Scripts/Presentation/UnitOverheadView.cs` · `UnitOverheadUiLayer.cs` · `Data/UnitOverheadUiStyle.cs`
- `Scripts/UI/NextWaveDock.cs`(재작성) · `ResultScreen.cs` · `LeaderboardList.cs` ·
  `Outgame/TournamentHistoryPanel.cs` · `ScoreHudView.cs`
- 에셋: `Data/Enemies/*.asset`(12) · `Scripts/Data/Decks/Deck_*.asset`(7) + WaveA/B(안정도만)
- 씬: `Scenes/BattleScene.unity` — ScoreTallyView 오브젝트 삭제 + `burstScoreThreshold` 4

## Verified

- **컴파일만**: `dotnet build` — Wassup.Runtime / Tests.EditMode / Tests.PlayMode /
  Assembly-CSharp **오류 0**.
- **Unity 미검증**(사용자 지시: 리모트라 Play 검증 생략). EditMode/PlayMode 실행 안 함,
  Play 스모크 안 함, 밸런스 측정 안 함.
- ⚠ **연결된 Unity MCP 는 다른 클론**(`D:/projects/dreamsquad-demo-new/dreamsquad-demo`,
  master)에 붙어 있다. 이 레포로 열어야 검증이 성립한다.

## Notes (되돌리면 안 되는 의도)

- **ECS 변경 0.** 안정도는 브리지 소유 값이다. 적이 골에서 살아남아 타워를 때리는 지속 피해
  모델은 `docs/spec/goal-tower-siege/`(미착수) — 그 spec 의 "함정 12개" 를 읽지 않고 착수하면
  리뷰에서 잡힌 결함(미러 산술·뷰 despawn·원거리 정지·투사체 진영 하드코딩·보스 hunting)을
  그대로 반복한다.
- **스폰 창 불변식**을 깨면 전멸 진행이 조용히 죽는다. 수량 상한↑ ⇒ spacing↓.
- **`ForceNextWave` 를 no-op 으로 만들지 말 것** — PlayMode 스모크 3개가 판 진행 동력으로 쓴다.
- ~~**제출값 오프셋**(1e9)을 지우면 구 기록이 10~30 짜리 가짜 점수로 디코딩된다.~~
  → **unit 6 이 인코딩 자체를 제거**했다(디코딩도 없으니 가짜 점수 경로가 없다).
  안정도를 다시 점수에 섞지 말 것 — 그게 이 unit 의 요청이었다.
- 튜토리얼 패배 문구는 `ShowsStressLimit` 가드가 자동 생략한다 — 사용자 작성 문구를 건드리지 않았다.
- 스트레스·몽마의 계약은 **의도적으로 남겼다**(사용자 결정). 계약 코스트가 사실상 0 이 되는
  부작용은 README 후속 후보.

## Follow-up

1. **Unity 검증**: EditMode 전량 + `GoalStabilityTest`/`TallyFlowTest`/`EndlessModeSmokeTest` +
   Play 스모크(안정도 바 위치·도크 표기·전멸 진행·20초 상한).
2. **거동 미검증 테스트**: `SpawnAlertForecastTests`·`WaveSpawnForecastTests`·
   `WavePatternGeneratorTests`·`WaveSpawnLeadInTests` 는 컴파일만 확인했다 — 그리드 전제를
   깔고 있어 실행 시 깨질 수 있다.
3. **밸런스 측정**: 3분 도달 웨이브 수(목표 10~16), 안정도 20 의 적정성, 곡선 base/growth/cap.
4. **에셋·씬 잔재**: `ScoreRules.asset`(빈 SO), 덱 asset 의 `fixedWaveIntervalSec` orphan 키.
5. 파이프라인 맵(`docs/reference/object-pipeline-map.md`)은 갱신 대상이 아니다(신규 플레이
   오브젝트 없음) — `goal-tower-siege` 착수 시 갱신.
