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

- [x] compile 통과. 신규 `.cs` 라 `refresh_unity` 는 `scope=all` 로 (아니면 cascading CS0246)
- [x] `read_console` 에 에러 없음
- [x] EditMode 전체 통과 (1091 / 0 실패)
- [x] Play 검증: 처치 시 `_killScoreTotal` 증가
- [x] Play 검증: **유출은 `_killScoreTotal` 을 올리지 않는다**
- [x] Play 검증: 재시작 후 `_killScoreTotal` = 0

확인: 2026-07-20

### Play 실증 기록

매 프레임 샘플러(`EditorApplication.update` 자기정리 람다)로 추적했다.
**단발 프로브는 창을 놓친다** — 첫 시도에서 판이 끝나고 되감긴 상태를 읽어 "리셋 때문에 0인지
애초에 안 쌓인 건지" 구분이 안 됐다.

**유출 (디펜더 0기 배치):**
```
900 샘플 전부 killScoreTotal = 0,  그 사이 goal 30 → 74 (44기 유출)
```
44마리가 골인했는데 킬점수는 1점도 안 붙었다.

**처치 (스나이퍼 12기, 웨이브 당기기 없이 자연 진행):**
```
clock=1.70s  goal=0  killScore=100  hud=10
clock=2.01s  goal=0  killScore=200  hud=20
clock=5.12s  goal=0  killScore=300  hud=30
clock=7.86s  goal=0  killScore=400  hud=40
clock=12.39s goal=0  killScore=500  hud=50
```
처치 1기당 정확히 +100(잡몹 `killScore`), HUD(+10)와 1:1 대응.

**주의 — 오염된 판을 실측으로 오인하기 쉽다.** 중간에 `killScore=0` 인 판이 나왔는데,
`hud._targetScore = 0` / `log.kills = 0` 으로 대조해 **처치 자체가 0건**이었음을 확인했다
(이전 판 잔여 적이 골 근처에 남아 즉시 유출된 케이스). 씬을 새로 로드해야 깨끗하다.

> 아직 점수 산식에 연결하지 않는다. 누적만 하고 `CalculatePlayerScore()` 는 현행 유지 — 다음 단위에서 교체한다.
