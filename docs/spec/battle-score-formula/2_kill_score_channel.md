# 2 — 킬점수 채널 (스폰 베이크 → 이벤트 → Bridge 누적)

## 목적

처치한 적의 `killScore` 를 ECS 에서 Bridge 로 실어 보내 누적한다.
드레인 시점엔 엔티티가 이미 파괴돼 있으므로 **enqueue 시점에 값을 이벤트에 박아야** 한다 (계약 6).

`AwakeningReward` 가 정확히 같은 문제를 같은 방식으로 이미 풀고 있다 — 그대로 따라간다.

## 변경 대상

- 신규 `Assets/_Project/Scripts/Battle/Units/KillScore.cs`
- 수정 `Assets/_Project/Scripts/Battle/Units/EnemyKilledEvent.cs`
- 수정 `Assets/_Project/Scripts/Battle/Units/DamageApplicationSystem.cs`
- 수정 `Assets/_Project/Scripts/Bridge/BattleBridge.cs`

## 구현

### KillScore 컴포넌트

`AwakeningReward.cs` 를 그대로 본떠 만든다 — Units 맥락 소유, 스폰 베이크에서 1회 쓰기,
`DamageApplicationSystem` 이 읽어 이벤트에 찍는다.

```csharp
public struct KillScore : IComponentData { public int value; }
```

### 이벤트 페이로드

`EnemyKilledEvent` 에 `public int killScore;` 를 **맨 뒤에** 추가한다 (기존 필드 순서 불변 — `awakeningReward`, `entity` 가 그렇게 붙어 있다).

### 스폰 베이크

`BattleBridge.cs:5228` 의 `AwakeningReward` 부착 바로 아래에 무조건 부착한다.
0 도 허용해서 lookup 을 분기 없이 유지하는 것도 동일하다.

```csharp
_em.AddComponentData(entity, new KillScore { value = Mathf.Max(0, entry.unitType.killScore) });
```

### enqueue 시 스탬프

`DamageApplicationSystem` 에 `_killScoreLookup` 을 추가하고 (`_awakeningRewardLookup` 선례, `:58` 근처에서 `Update`),
`:203` 의 `Enqueue` 에 `killScore = ...` 를 채운다. 컴포넌트가 없으면 0.

### Bridge 누적

`DrainEnemyKilledEvents()` (`:2829`) 에서 `_killScoreTotal += evt.killScore;` 로 누적한다.
필드는 `_goalReachedCount` 옆에 둔다.

**리셋은 계약 9를 따른다** — `_battleClock` 이 0이 되는 **모든** 지점에서 함께 0이 되어야 한다:
`BeginPlacement()` (`:1104` 근처), `StartBattle()` (`:1151`), `StopBattle()` (`:1399`).
`_goalReachedCount` 처럼 `BeginPlacement` 에만 두면 teardown 없는 재시작에서 시계와 발산한다.

라이브 HUD 점수(`scoreHud?.OnEnemyKilled`)는 **건드리지 않는다** (계약 12).

## 완료 기준

- [ ] compile 통과. 신규 `.cs` 라 `refresh_unity` 는 `scope=all` 로 (아니면 cascading CS0246)
- [ ] `read_console` 에 에러 없음
- [ ] EditMode 전체 통과
- [ ] Play 검증: 전투 진입 → 적 몇 기 처치 → `_killScoreTotal` 이 처치한 적들의 `killScore` 합과 일치 (reflection 조회)
- [ ] Play 검증: 적을 **유출**시키면 `_killScoreTotal` 이 **안 오른다** — 이 스펙의 핵심 전제(README "유출은 두 축을 동시에 깎는다")를 실증하는 확인이다
- [ ] Play 검증: 재시작(RESTART) 후 `_killScoreTotal` 이 0으로 돌아온다

> 아직 점수 산식에 연결하지 않는다. 누적만 하고 `CalculatePlayerScore()` 는 현행 유지 — 다음 단위에서 교체한다.
