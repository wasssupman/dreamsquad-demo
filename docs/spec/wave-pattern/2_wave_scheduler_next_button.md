# Wave Scheduler And Next Wave

**작업 구분**: Phase 2

## 목적

생성된 wave plan 을 3분 구간에 균등 배정된 시간마다 자동 호출하고, 화면 오른쪽 하단 `Next Wave` 버튼으로 다음 wave 를 즉시 호출할 수 있게 한다.

## 런타임 구조

```csharp
private GeneratedWavePlan _wavePlan;
private int _nextWaveIndex;
private readonly List<PendingSpawnEntry> _pending = new();
```

흐름:

```text
StartBattle()
  _wavePlan = WavePatternGenerator.Generate(...)
  _nextWaveIndex = 0
  QueueDueWaves(0)

Update()
  QueueDueWaves(elapsed)
  DrainPendingSpawnEntries()

Next Wave button
  ForceNextWave()
```

## Next Wave 버튼

위치:

- 오른쪽 하단
- 전투 중에만 표시
- briefing/draft/placement/result 에서는 숨김 또는 비활성

동작:

```text
ForceNextWave()
  if not running: return
  if _nextWaveIndex >= _wavePlan.waves.Count: disable button
  QueueWave(_wavePlan.waves[_nextWaveIndex], currentElapsed)
  _nextWaveIndex++
```

중복 방지:

- 이미 자동 호출된 wave 는 `_nextWaveIndex` 증가로 다시 호출되지 않는다.
- 버튼 연타는 다음 wave 들을 순서대로 앞당긴다.
- 버튼 연타 rate limit 은 없다. 이 버튼은 다음 예정 wave 를 강제로 당기는 조작이며, 추가 wave 를 만들지 않는다.
- 남은 wave 가 없으면 버튼은 disabled.

## 스폰 분산

한 wave 의 10~15마리를 한 frame 에 생성하지 않는다.

```text
PendingSpawnEntry.triggerTimeSec = baseTime + localIndex * intraWaveSpacingSec
```

동일 wave 내부 순서:

```text
A,B,A,B... 이후 한쪽 수량이 끝나면 남은 타입을 이어서 스폰
```

## lane 배정

- generated wave 는 wave 내 local spawn index 를 사용해 lane round-robin 분산.
- `GeneratedMap.spawns.Length` 가 1이면 0번 lane.
- `GeneratedMap.spawns.Length >= 2` 이면 `localIndex % laneCount`.
- legacy `AttackDeck.spawns` 는 기존 `SpawnEntry.spawnIndex` fallback 정책을 유지한다.

## 완료 기준

- 전투 시작 시 wave 1 이 0초 기준으로 호출된다.
- 이후 wave plan 의 `triggerTimeSec` 에 맞춰 다음 wave 가 자동 호출된다.
- `Next Wave` 클릭 시 다음 wave 가 즉시 호출된다.
- 10~15개 wave 모두 3분 안에 자동 호출 대상이다.
- 같은 wave 가 자동/수동으로 중복 호출되지 않는다.
- 모든 wave 호출 후 버튼 disabled.
