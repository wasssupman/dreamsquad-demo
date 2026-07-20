# 1 — ScoreMath 순수 함수 + 테스트

## 목적

점수 산식 전체를 아키텍처 무참조 순수 static 함수 하나에 넣고 EditMode 테스트로 고정한다.
**승패 규칙(패배 시 시간점수 0)도 이 안에 둔다** — Bridge 에 흩으면 테스트로 잡히지 않는다 (계약 3).

## 변경 대상

- 신규 `Assets/_Project/Scripts/Core/ScoreMath.cs`
- 신규 `Assets/_Project/Tests/EditMode/ScoreMathTests.cs`
- 신규 `Assets/_Project/Tests/EditMode/WaveKillBudgetPinTests.cs`

`Core/MatchSeed.cs` 가 같은 성격(순수 static + `MatchSeedTests`)의 선례다. 새 asmdef 를 만들지 않는다.

## 구현

### ScoreMath

```csharp
public readonly struct BattleScore
{
    public readonly int time, stress, kill, total;
}

public static BattleScore Evaluate(
    int remainingMs,        // 남은 전투 시간 (음수 입력도 방어)
    int stressAccrued,      // _goalReachedCount + _leakAllowancePenalty
    int stressLimit,        // deck.defeatGoalReachedCount 원본값
    int killScoreTotal,     // 실제 처치분 누적
    bool defeated,
    int timeScorePerSecond,
    int stressScorePerPoint)
```

- `time = defeated ? 0 : max(0, remainingMs) * timeScorePerSecond / 1000`
- `stress = max(0, stressLimit - stressAccrued) * stressScorePerPoint`
- `kill = max(0, killScoreTotal)`
- `total = time + stress + kill`

`long` 을 쓰지 않는다 — 최악 180,000 × 100 = 18,000,000 이고 오버플로 임계는 초당 11,930 이다 (unit 0 의 Range 로 막힘).

`max(0, ...)` clamp 는 정상 경로에선 절대 걸리지 않는다 (README "분기가 하나뿐인 이유" 참조).
`stressLimit <= 0` 인 덱 오저작 방어용이므로 **지우지 말 것**.

### 테스트 — ScoreMathTests

최소한 아래를 덮는다:

| 케이스 | 기대 |
|---|---|
| 만점 (180,000ms / 누적 0 / 한계 10 / 킬 10,300) | time 18,000, stress 9,000, total 37,300 |
| 패배 (defeated=true, 누적 10, 한계 10) | time 0, stress 0, total = kill 만 |
| 버팀 승리 (remainingMs 0) | time 0, stress·kill 정상 |
| 승리 최소 스트레스 (누적 = 한계 − 1) | stress = 900 (0 이 아님) |
| 계약 9회 (누적 9, 한계 10) | stress = 900 |
| 절삭 (remainingMs 9, 초당 100) | time 0 — 10ms 미만은 버려진다 |
| 방어: 한계 0, 누적 1 | stress 0 (음수 아님) |
| 방어: remainingMs 음수 | time 0 |

### 테스트 — WaveKillBudgetPinTests

README 가 적은 고정 시드 스케줄을 실행으로 pin 한다. `WavePatternGenerator.Generate(deck, 20260720)` 을
호출해 아래를 검증한다. 기대값은 `Unity.Mathematics.Random` (xorshift, `random.cs:670` `NextState`)
을 오프라인 재현해 산출했고 독립 검증도 일치했다. **테스트가 실제 생성기를 호출하므로 재현 스크립트는
필요 없다** — 아래 값이 안 맞으면 생성기나 덱 파라미터가 바뀐 것이다:

- `waves.Count == 10`
- 웨이브별 `totalCount` = `[5, 5, 8, 8, 5, 7, 6, 8, 8, 5]`
- 보스 웨이브는 index 4, 9 (`nightmareMechanics.Length > 0` 인 유닛 포함)
- 총 스폰 65기 = 잡몹 63 + 보스 2
- 마지막 스폰 시각 = 163.40s (`ExpandWave` 최대 `triggerTimeSec`, 부동소수라 `Assert.AreEqual(..., 0.01f)`)

이 테스트는 산식이 아니라 **README 문서값의 회귀 방지**다. 킬 만점은 런타임 누적이므로(계약 7)
이 값이 바뀐다고 산식이 깨지지는 않지만, 바뀌면 README 예산 표를 고쳐야 한다는 신호가 된다.

## 완료 기준

- [ ] compile 통과
- [ ] `ScoreMathTests` 전 케이스 통과
- [ ] `WaveKillBudgetPinTests` 통과 — 실패하면 README 실측 절이 틀린 것이므로 **테스트가 아니라 README 를 고친다**
- [ ] `ScoreMath.cs` 에 `UnityEngine` / `Unity.Entities` using 이 없다 (`MatchSeed.cs` 와 동일 수준)
- [ ] 기존 EditMode 전체 통과

> `ObstaclePlacerTests` 는 이 스펙과 무관하게 사전 실패 중일 수 있다. 실패 목록에 있으면 무시하고,
> 다회 Play 후 거짓 실패가 의심되면 `RequestScriptReload` 후 재실행한다.
