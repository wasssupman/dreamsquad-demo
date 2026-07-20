# 3 — Bridge 통합 (산식 교체 + 로그)

## 목적

`CalculatePlayerScore()` 를 `ScoreMath.Evaluate` 호출로 교체하고, 종료 3종 경로에 연결한다.
배틀로그에 세 축을 남겨 나중에 서버가 재계산할 수 있게 한다 (재검증 자체는 이 스펙 범위 밖).

**이 단위에서 점수가 실제로 바뀐다.** 앞 단위들은 전부 준비였다.

## 변경 대상

- 수정 `Assets/_Project/Scripts/Bridge/BattleBridge.cs`
- 수정 `Assets/_Project/Scripts/Logging/BattleLogger.cs`
- 수정 `Assets/_Project/Scripts/Logging/BattleLogSchema.cs`

## 구현

### 산식 교체

`CalculatePlayerScore()` (`:3741`) 를 `BattleScore` 를 반환하도록 바꾼다. 총점만 쓰는 호출부가
아니라 세 축을 다 넘겨야 하므로(unit 4) struct 반환이 맞다.

```csharp
private ScoreMath.BattleScore CalculateBattleScore(bool defeated)
```

입력:

| ScoreMath 인자 | 출처 |
|---|---|
| `remainingMs` | `Mathf.RoundToInt(RemainingBattleSeconds() * 1000f)` (`:3688`, 이미 0 clamp) |
| `stressAccrued` | `_goalReachedCount + _leakAllowancePenalty` |
| `stressLimit` | `deck.defeatGoalReachedCount` **원본값** (계약 8 — `EffectiveLeakLimit()` 아님) |
| `killScoreTotal` | unit 2 의 `_killScoreTotal` |
| `defeated` | 호출부가 넘김 |
| 초당/점당 점수 | `ScoreRulesData` SO 참조 (`[SerializeField]` 로 Bridge 에 물림) |

SO 가 null 이면 점수가 전부 0이 되어 조용히 망가진다. **null 이면 `Debug.LogError` 후 기본값(100/900)으로 진행**한다.

### 종료 3종 연결

세 지점 모두 같은 모양으로 바꾼다. `defeated` 인자만 다르다.

- `DrainGoalEvents` (`:3673`) → `defeated: true`
- `CheckTimer` (`:3698`) → `defeated: false`
- `CheckVictory` (`:3717`) → `defeated: false`

`victory_timeout` 은 `RemainingBattleSeconds()` 가 이미 0 이라 `defeated: false` 로도 시간점수가 0이다
(README "분기가 하나뿐인 이유"). 억지로 true 를 넘기지 말 것 — 스트레스점수까지 죽는다.

`ReportMatchResult(score.total)` 로 서버에는 총점만 보낸다 (기존 API 계약 불변).

### 리셋 정렬 재확인

unit 2 에서 `_killScoreTotal` 리셋을 3곳에 넣었다. 이 단위에서 산식이 실제로 그 값을 읽게 되므로,
**리셋이 빠진 경로가 있으면 여기서 처음 증상이 난다** (재시작 시 이전 매치 킬점수가 얹힘).
Play 재시작 검증을 완료 기준에 둔 이유다.

### 배틀로그

`BattleLogSchema` 의 result 레코드에 세 축을 추가한다: `time_score`, `stress_score`, `kill_score`.
기존 `score` 는 총점으로 유지한다 (서버·기존 분석이 읽는 필드).

`BattleLogger.SetScore(int)` 를 세 축을 받는 오버로드로 확장한다. **기존 시그니처를 지우지 말 것** —
`AddScoreEvent` 경로(`:341`)가 여전히 `result.score` 를 쓴다.

> 알려진 불일치: `score_events[]` 는 처치당 +10 의 라이브 HUD 누적이고 `result.score` 는 최종 산식이라
> 같은 로그 안에서 두 값이 다르다. 계약 12(HUD 표시 전용 존치)의 귀결이며 의도된 상태다.

## 완료 기준

- [ ] compile 통과, `read_console` 클린
- [ ] EditMode 전체 통과
- [ ] Play: 무유출 전멸 승리 → 총점이 `시간 + 스트레스 + 킬` 로 손계산과 일치
- [ ] Play: 유출 1회 후 승리 → 스트레스가 점당점수만큼, 킬이 그 적 `killScore` 만큼 **동시에** 줄어든다
- [ ] Play: 패배 → 시간점수 0, 스트레스점수 0, 킬점수만 남는다
- [ ] Play: 타임아웃 생존 → 시간점수 0, 스트레스·킬 정상
- [ ] Play: 재시작 후 점수가 이월되지 않는다
- [ ] 배틀로그 JSON 에 세 축이 찍히고 합이 `score` 와 같다
