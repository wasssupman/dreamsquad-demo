# 0. 스폰 예보 순수 함수 — lane 산식 공유 추출 + per-lane 첫 스폰 시각

## 목적

"웨이브 w 가 lane L 에 첫 적을 몇 초에 내보내는가"를 순수 함수로 계산한다.
현재 lane 산식 `EffectiveSpawnIndex` 는 `BattleBridge` private 인데, 예보가 같은
산식을 써야 런타임 스폰과 절대 어긋나지 않으므로 공유 위치로 추출한다(제약 10 +
호출처 2곳 재사용 요건 충족).

## 변경 대상

- `Assets/_Project/Scripts/Data/WavePatternGenerator.cs` — `EffectiveSpawnIndex` 이관(public static) + `DeckIndexStride` 상수 + `FirstSpawnTimesPerLane` 신설
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — private `EffectiveSpawnIndex` 삭제, 이관본 호출. `waveIndex * 1000` 매직넘버를 `DeckIndexStride` 로 교체
- `Assets/_Project/Tests/EditMode/WaveSpawnForecastTests.cs` — 신규

## 구현

`WavePatternGenerator` 에 추가:

```csharp
public const int DeckIndexStride = 1000; // QueueWave 의 waveIndex*1000 관례를 단일화

public static int EffectiveSpawnIndex(int authoredIndex, int deckIndex, int laneCount)
// BattleBridge 에서 그대로 이관 (laneCount<=2 는 authoredIndex clamp, 그 외 deckIndex % laneCount)

// 반환: 길이 laneCount 배열. results[L] = lane L 첫 스폰의 절대 시각, 스폰 없으면 -1
public static float[] FirstSpawnTimesPerLane(
    GeneratedWave wave, float baseTriggerTimeSec, int laneCount, float intraWaveSpacingSec)
```

`FirstSpawnTimesPerLane` 은 내부에서 `ExpandWave(wave, baseTriggerTimeSec, laneCount,
intraWaveSpacingSec)` 로 엔트리를 펼친 뒤, 엔트리 i 의 lane 을
`EffectiveSpawnIndex(entry.spawnIndex, wave.waveIndex * DeckIndexStride + i, laneCount)`
로 구해 lane 별 최소 `triggerTimeSec` 을 취한다. **`QueueWave`→`SpawnUnit` 의 deckIndex
부여(`baseDeckIndex + i`)와 자릿수까지 같은 규약**이어야 한다 — 상수 공유가 그 보증.

## 완료 기준 (EditMode 테스트)

- 3-lane 일반 웨이브(RoundRobin): 첫 3엔트리가 lane 을 모두 커버, 시각 = base + {0, 1, 2}×spacing 의 순열.
- 보스 웨이브(보스 선봉 + 호위): 보스가 속한 lane 의 첫 시각 = base.
- `laneCount = 1, 2` clamp 경로: authoredIndex 기준 배정과 일치.
- PerGroupTimeline(작성 플랜) 웨이브: 그룹 오프셋 반영된 lane 별 최소 시각.
- 스폰 없는 lane → `-1` (엔트리 수 < laneCount 케이스).
- 기존 `WavePatternGeneratorTests`·`BossTests` 그린 유지 (ExpandWave 동작 무변경).
