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

- [x] compile 통과, `read_console` 클린
- [x] EditMode 전체 통과 (1091 / 0 실패)
- [x] `scoreRules` 씬 배선 (BattleScene). 미배선 시 `LogError` + 기본값 폴백 경로 유지
- [x] Play: Bridge 산출 == `ScoreMath` 손계산 (세 축 전부, 합 == 총점)
- [x] Play: 패배 → 시간점수 0
- [x] Play: 배틀로그에 세 축이 찍히고 합이 `score` 와 같다

확인: 2026-07-20

### Play 실증 기록

**입력 전달 검증** — 비자명한 상태를 주입해 Bridge 출력과 손계산을 대조했다.
```
입력: remainMs=169003  goal=3 penalty=2 kill=7200  rawLimit=30
Bridge : time=16900 stress=22500 kill=7200 total=46600
손계산 : time=16900 stress=22500 kill=7200 total=46600   → 일치, 합==총점
```

**계약 8 실증** — 한계에 `EffectiveLeakLimit()`(계약 차감 후, 30−2=28)를 썼다면 stress=20,700 이
나왔을 것이다. 실제 22,500 이므로 **원본값을 쓰고 있다**. 이 실수는 컴파일도 되고 테스트도
통과하므로(둘 다 유효한 int) 이 대조가 유일한 방어선이다.

**패배 게이트 실증** — 같은 상태에서 플래그만 바꿔 대조:
```
defeated=false: time=16900 → total 46600
defeated=true : time=0     → total 29700   (스트레스·킬은 그대로)
```
게이트가 없으면 패배에 16,900점이 붙는다. 플래그는 **시간축만** 끈다.

**로그** — `outcome=defeat score=0 time=0 stress=0 kill=0`, 세 축 합 == `score`.

> 실전 판으로 4종 결과를 각각 재현하려 했으나 드래프트 풀이 판마다 달라 디펜더 배치가
> 불안정했다(`NotInPickedPool` / `NotBuildable`). 결과 조합 자체는 `ScoreMathTests` 가 이미
> 망라하므로, 여기서는 **Bridge 가 올바른 입력을 넘기는가**로 검증 축을 좁혔다.
