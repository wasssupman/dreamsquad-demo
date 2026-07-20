# 6 — 인계 요약

## Commit

| 해시 | 내용 |
|---|---|
| `57bffb73` · `cd166ff5` · `6cfc093e` | 설계 README + 작업 단위 0~5 문서 |
| `d82d4cdb` · `67237e66` | unit 0 — `ScoreRulesData` SO + `AttackUnitData.killScore` |
| `48d1541b` | unit 1 — `ScoreMath` 순수 함수 + 테스트 19건 |
| `6c6d7bb3` | unit 2 — 킬점수 ECS 채널 |
| `46357d80` | unit 3 — 산식 교체 + 로그 3축 |
| `52887032` | unit 4 — 결과 화면 3축 분해 |
| `50aed532` | unit 5 — 유출 한계 30 → 10 |

관련: `645d72ec` (`result-screen-ranking-ui` — 결과 화면 2컬럼 재설계, 이 spec 의 unit 4 가 얹힌 토대)

## Implemented

- 최종 점수가 **예산 소모 모델**로 교체됐다: `시간 + 스트레스 + 킬`. 현행 `경과초×10 − 유출×50` 삭제
- 시간점수 = 남은시간ms × 초당점수 / 1000, **패배 시 0** (산식의 유일한 분기)
- 스트레스점수 = (한계 − 누적) × 점당점수. 누적 = 유출 + 몽마의 계약 선불
- 킬점수 = 실제 처치분. **유출된 적은 주지 않는다** — 유출이 스트레스·킬 두 축을 동시에 깎는다
- 배점은 전부 `ScoreRules.asset` (초당 100 / 점당 900). 미배선 시 `LogError` + 기본값 폴백
- 배틀로그 `result` 에 `time_score` / `stress_score` / `kill_score` 추가 (`score` 는 총점 유지)
- 결과 화면 좌측에 "얻은 점수 / 예산" 3행 표시. 킬만 분모 없음
- 유출 한계 10 → 현재 만점 구성 `18,000 + 9,000 + 10,300 = 37,300`

## Key Files

- `Assets/_Project/Scripts/Core/ScoreMath.cs` — 산식 전부. `using` 0개, int 전용
- `Assets/_Project/Scripts/Data/ScoreRulesData.cs` + `Data/Config/ScoreRules.asset`
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — `CalculateBattleScore`, `_killScoreTotal`, 종료 3종
- `Assets/_Project/Scripts/Battle/Units/KillScore.cs` · `EnemyKilledEvent.cs` · `DamageApplicationSystem.cs`
- `Assets/_Project/Tests/EditMode/ScoreMathTests.cs` · `WaveKillBudgetPinTests.cs`

## Verified

- EditMode **1091 통과 / 0 실패** (기존 1072 + 신규 19). 스킵 2건은 기존 `[Ignore]`
- 콘솔 에러 0
- Play — 유출 44기 / 900 샘플 전부 `killScoreTotal = 0`
- Play — 처치 1기당 정확히 +100, HUD(+10)와 1:1 대응
- Play — Bridge 출력 == `ScoreMath` 손계산 (세 축, 합 == 총점)
- Play — 유출 한계 10 적용 후 HUD `0 / 10`, 무유출 스트레스 9,000

## Notes (되돌리면 안 되는 것)

1. **한계와 점당점수는 곱해서 예산이다.** 한계를 30으로 되돌리면 예산이 27,000 으로 튄다.
   반드시 `stressScorePerPoint` 를 함께 조정할 것 (30 × 300 = 9,000).
2. **`stressLimit` 은 `deck.defeatGoalReachedCount` 원본값**이다. `EffectiveLeakLimit()`(계약 차감 후)이
   아니다. 이 실수는 컴파일도 되고 테스트도 통과한다 — Play 대조로만 잡힌다(unit 3 문서 참조).
3. **`victory_timeout` 은 `defeated: false`** 다. true 를 넘기면 스트레스점수까지 죽는다.
   남은 시간이 0 이라 시간축은 이미 자동으로 0 이다.
4. **`_killScoreTotal` 리셋은 `_battleClock` 이 0 이 되는 모든 지점**에 있어야 한다
   (`BeginPlacement`/`StartBattle`/`StopBattle` 3곳). `_goalReachedCount` 는 1곳뿐이라 비대칭이고,
   teardown 없는 `StartBattle` 재호출에서 실제로 발산하는 걸 검증 중 목격했다.
5. **킬 만점을 상수로 박지 말 것.** 스폰 구성 의존이라 `waveSeed` 가 0이 되면 8,700~16,200 으로 흔들린다.
6. `ScoreMath` 의 `max(0, ...)` clamp 는 정상 경로에서 도달 불가지만 `defeatGoalReachedCount ≤ 0`
   오저작 방어다. 지우지 말 것.

## Follow-up

- **유출 한계 10 의 플레이 감각 미확인.** 근거 없는 시작값이다. 조정 시 위 Notes 1 준수
- ~~`BotScoreGenerator.cs` 미사용~~ → **삭제됨** (2026-07-21). Phase 5(`c785ce6b`)의 가설 검증용 더미였고, 실서버 랭킹 도입 + 대기 상태 폴백 전환으로 존재 이유가 사라졌다. 복원이 필요하면 `c785ce6b` 에서 꺼낼 수 있다
- 점수 재검증(서버 재계산·무효 플래그) — README 후속 후보. 결정론적 재시뮬은 고정 타임스텝이 선결
- 라이브 HUD 점수(처치당 +10) ↔ 최종 점수 통합 — 현재 같은 로그 안에서 `score_events[]` 와 `result.score` 가 다르다. 의도된 상태
- 결과 화면에서 남은 시간·유출 **원값**이 빠졌다. 점수로부터 역산 가능하나 직접 표시를 원하면 별도 판단

> 파이프라인 맵(`docs/reference/object-pipeline-map.md`) 갱신 불필요 — 새 아키타입/정거장 없이
> 기존 `SpawnUnit` 베이크에 컴포넌트 하나가 추가됐을 뿐이다(`AwakeningReward` 와 동일 성격).
