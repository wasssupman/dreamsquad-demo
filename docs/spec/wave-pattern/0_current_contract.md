# Current Attack Pattern Contract

**작업 구분**: Phase 0

## 목적

현재 공격 패턴은 `AttackDeck.spawns` 에 작성된 정적 `SpawnEntry` 목록을 시간순으로 스폰한다. 이 문서는 새 wave generator 가 대체해야 할 기존 계약과 호환 경계를 고정한다.

## 현재 구조

```text
AttackDeck
  deckId
  spawns: List<SpawnEntry>
    triggerTimeSec
    unitType
    spawnIndex
  defeatGoalReachedCount
  timerDurationSec
```

런타임 흐름:

```text
BattleBridge.StartBattle()
  _pending = deck.spawns
  _timerDuration = deck.timerDurationSec

BattleBridge.Update()
  elapsed >= SpawnEntry.triggerTimeSec 이면 SpawnUnit(entry)
```

## 교체 방향

정적 `SpawnEntry` 직접 작성 방식은 유지하되, 기본 실행 경로는 seed 기반 `WavePlan` 생성으로 전환한다.

권장 구조:

```text
AttackDeck
  deckId
  attackUnitPool: AttackUnitData[]
  waveSeed
  minWaveCount = 10
  maxWaveCount = 15
  waveIntervalSec = timerDurationSec / (waveCount - 1)
  minUnitsPerWave = 10
  maxUnitsPerWave = 15
  defeatGoalReachedCount
  timerDurationSec = 180
```

기존 `spawns` 는 fallback 또는 legacy fixture 로 남길 수 있다.

## 완료 기준

- 현재 정적 deck 구조가 어디서 소비되는지 확인 완료.
- 새 wave plan 이 기존 `SpawnUnit` 경로를 재사용할 수 있는 경계가 명확하다.
- legacy `spawns` 를 즉시 삭제하지 않아도 구현 가능한 계획이다.
