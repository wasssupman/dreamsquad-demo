# Seeded Wave Plan

**작업 구분**: Phase 1

## 목적

정해진 seed 로 3분 플레이용 wave 목록을 생성한다. 같은 seed 는 같은 wave 구성과 같은 스폰 순서를 만든다.

## 데이터 모델

```csharp
public readonly struct GeneratedWavePlan
{
    public readonly int seed;
    public readonly int generatorVersion;
    public readonly float timerDurationSec;
    public readonly float waveIntervalSec;
    public readonly IReadOnlyList<GeneratedWave> waves;
}

public readonly struct GeneratedWave
{
    public readonly int waveIndex;
    public readonly float triggerTimeSec;
    public readonly AttackUnitData unitA;
    public readonly int countA;
    public readonly AttackUnitData unitB;
    public readonly int countB;
    public readonly int totalCount;
}
```

권장 위치:

- New: `Assets/_Project/Scripts/Data/GeneratedWavePlan.cs`
- New: `Assets/_Project/Scripts/Data/WavePatternGenerator.cs`

## 생성 규칙

입력:

```text
seed
timerDurationSec = 180
minWaveCount = 10
maxWaveCount = 15
minUnitsPerWave = 10
maxUnitsPerWave = 15
attackUnitPool = 현재 구현된 AttackUnitData[]
```

규칙:

1. `Unity.Mathematics.Random(seed)` 사용.
2. waveCount 는 10~15 사이에서 seed 기반 랜덤.
3. waveCount 확정 후 `waveIntervalSec = timerDurationSec / waveCount` 로 계산한다.
4. wave trigger time 은 `waveIndex * waveIntervalSec` 이다. 마지막 wave 는 `timerDurationSec` 보다 앞에 예약된다.
5. 각 wave 는 공격 유닛 풀에서 서로 다른 2종을 랜덤 선택한다.
6. wave totalCount 는 10~15 사이에서 seed 기반 랜덤.
7. `countA` 는 1 이상, `countB` 는 1 이상이 되도록 분배한다.
8. 한 wave 내 세부 스폰 시점은 wave 시작 후 짧은 burst window 로 분산한다.
9. 한 wave 내 unit A/B 는 deterministic interleave 로 펼친다. `A,B,A,B...` 순서이며 한쪽 수량이 먼저 끝나면 남은 타입을 이어서 스폰한다.

기본 세부 스폰 간격:

```text
intraWaveSpacingSec = 0.35초
```

## wave 수와 시간 배정

```text
10 waves: interval = 180 / 10 = 18.00s, trigger = 0..162s
15 waves: interval = 180 / 15 = 12.00s, trigger = 0..168s
```

이 방식이면 10~15개 wave 모두 3분 안에 자동 호출되고 마지막 wave 가 행동할 시간을 가진다. `Next Wave` 는 다음 예정 wave 를 현재 시점으로 앞당길 뿐, 총 wave 수를 늘리지 않는다.

## AttackDeck 확장

```csharp
public bool useGeneratedWaves = true;
public int waveSeed = 0;
public int waveGeneratorVersion = 1;
public AttackUnitData[] attackUnitPool;
public int minWaveCount = 10;
public int maxWaveCount = 15;
public int minUnitsPerWave = 10;
public int maxUnitsPerWave = 15;
public float intraWaveSpacingSec = 0.35f;
```

현재 구현에서 `waveSeed == 0` 은 `1` 로 resolve 한다. session seed 파생은 아직 도입하지 않는다. 재현성을 위해 최종 resolved seed 를 로그에 남긴다.
`waveIntervalSec` 는 저장 필드가 아니라 생성 결과에서 계산된 값으로 기록한다.

## 완료 기준

- 같은 seed + 같은 attackUnitPool 에서 wave summary 가 동일하다.
- 다른 seed 에서 wave summary 가 달라질 수 있다.
- unit pool 이 2종 미만이면 명확한 error log 후 legacy `spawns` fallback 을 사용한다.
- 모든 wave 가 2종 유닛을 가진다.
- 모든 wave totalCount 가 10~15 범위다.
- 모든 wave triggerTimeSec 가 0 이상, timerDurationSec 미만 범위에 들어온다.
- 생성 결과를 spawn entry list 로 펼칠 수 있다.
