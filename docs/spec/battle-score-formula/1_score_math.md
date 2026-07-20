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

점수 산식이 웨이브 구성에 대해 기대하는 **구조 불변식**만 검증한다:

- `waveSeed` 비0 (모든 플레이어 동일 스케줄 — 점수 비교의 전제)
- 같은 시드 → 같은 결과 (생성기에 비결정 요소 없음)
- 보스가 `bossWaveInterval` 주기에만 편성됨
- 모든 유닛이 `killScore > 0`
- 마지막 스폰이 제한시간 안에 있음

> **초기 버전은 실측값(수량 배열·킬 예산 10,300·마지막 스폰 163.40s)을 그대로 못박았고, 그건
> 틀렸다.** 계약 7이 이미 "킬 만점은 런타임 누적"이라고 정하고 있어서, 밸런스가 바뀌어도 점수
> 시스템은 아무것도 안 깨지는데 테스트만 빨개진다. 실제로 `wave-pattern` 밸런싱 머지에서
> 즉시 깨졌다(63 → 72기). 방어 가치 없이 마찰만 남아 축소했다.

## 완료 기준

- [x] compile 통과
- [x] `ScoreMathTests` 전 케이스 통과 (14건)
- [x] `WaveKillBudgetPinTests` 통과 (5건) — 실패하면 README 실측 절이 틀린 것이므로 **테스트가 아니라 README 를 고친다**
- [x] `ScoreMath.cs` 에 `using` 이 하나도 없다. `float`/`Mathf` 미사용 (`MatchSeed.cs` 와 동일 수준)
- [x] 기존 EditMode 전체 통과 — **1091 / 0 실패** (1072 + 신규 19)

확인: 2026-07-20

> 시간 계산은 `usableMs / 1000 * 초당점수 + usableMs % 1000 * 초당점수 / 1000` 로 쪼갰다.
> `ms × 초당점수 / 1000` 과 결과가 같되(`TimeScore_MatchesDirectFormula` 로 고정) 곱하기 전에
> 초 단위를 떼어내 오버플로 여지를 없앤다. 현행 180초면 직접 곱해도 int 에 들어가지만,
> 제한시간이 길어지면 그 여유가 사라진다.

> `ObstaclePlacerTests` 는 이 스펙과 무관하게 사전 실패 중일 수 있다. 실패 목록에 있으면 무시하고,
> 다회 Play 후 거짓 실패가 의심되면 `RequestScriptReload` 후 재실행한다.
