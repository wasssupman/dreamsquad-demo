# 11 — 웨이브 스폰 리드인 (트리거 → 첫 적 2초)

**작업 구분**: 추가 11 (2026-07-26)

## 목적

웨이브 트리거와 **첫 적 등장** 사이에 균일한 리드인(기본 2초)을 넣는다. 지금은
트리거 시각 = 첫 스폰 시각이라 웨이브 전환이 예고 없이 터지고, 스폰 예고선
(`spawn-point-alert`)이 Wave 1·강제 호출에서 창을 확보할 수 없다.

**트리거 그리드는 건드리지 않는다.** `triggerTimeSec = i × interval` 계약(README
"시간 배정", endless-mode 계약 2)과 `_waveTimeShift` 리스케줄(unit 9)은 그대로 두고,
`QueueWave` 의 **스폰 base 시각만** 리드인만큼 민다. 웨이브 간격·간격 보존·연타
재기준·플랜 시각·로그·브리핑 표기는 전부 불변이다.

## 변경 대상

- `Assets/_Project/Scripts/Data/AttackDeck.cs` — `waveSpawnLeadInSec`(신규, 기본 2)
- `Assets/_Project/Scripts/Data/GeneratedWavePlan.cs` — `spawnLeadInSec` 필드(ctor 기본 0)
- `Assets/_Project/Scripts/Data/WavePatternGenerator.cs` — `Generate` 가 덱 값을 플랜에 실음. `FromPlanAsset` 은 0 유지
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — `QueueDueWaves` / `ForceNextWave` 의 `QueueWave` base
- `Assets/_Project/Tests/EditMode/WaveSpawnLeadInTests.cs` — 신규
- `Assets/_Project/Tests/EditMode/WaveKillBudgetPinTests.cs` — 마지막 스폰 pin 에 리드인 반영

## 구현

리드인은 **덱 데이터 → 플랜 → 큐잉** 순으로 흐른다. 스폰 base 를 만드는 지점이
`QueueDueWaves` / `ForceNextWave` / 예보(`TryGetSpawnAlertForecast`) **세 곳**이고,
셋이 **같은 base** 를 써야 한다 — 예보에서 리드인을 빼먹으면 예고선이 실스폰보다
리드인만큼 일찍 사라진다.

```csharp
// AttackDeck
public float waveSpawnLeadInSec = 2f;   // 0 = 기존 동작(트리거 = 첫 스폰)

// GeneratedWavePlan (ctor 마지막 인자, 기본 0f → 기존 호출처 무변경)
public readonly float spawnLeadInSec;

// BattleBridge — 스폰 base 만 밀린다 (세 지점 동일 base)
QueueWave(wave, ScheduledWaveTime(_nextWaveIndex) + SpawnLeadInSec, false, elapsedSec);  // 자동
QueueWave(wave, elapsedSec + SpawnLeadInSec, true, elapsedSec);                          // 강제
FirstSpawnTimesPerLane(wave, ScheduledWaveTime(_nextWaveIndex) + SpawnLeadInSec, ...);   // 예보
```

**`_waveTimeShift` 산식에는 리드인을 넣지 않는다.** `-= ScheduledWaveTime(i) - elapsedSec`
는 트리거 그리드 기준이며, 여기에 리드인을 섞으면 연타마다 2초가 누적 왜곡된다.

**작성 플랜(PerGroupTimeline)은 리드인을 적용받지 않는다.** `wave-authoring-test-mode`
unit 6 이 이미 웨이브 상대 시각 모델(`AuthoredSpawnGroup.triggerTimeSec ∈ [0,duration]`)을
갖고 있어 작성자가 그룹 offset 으로 같은 표현을 한다 — 덱 값을 겹쳐 주면 **이중 가산**이
된다. 그래서 값은 `AttackDeck` 이 아니라 **플랜에 실어** `FromPlanAsset` 에서 0 으로 둔다.

**tail 마진**: 마지막 스폰이 2초 뒤로 간다. `WaveA`(180초, 10~15웨이브, 최대 10마리,
spacing 1) 최악 조합은 "마지막 웨이브가 일반 웨이브 + 10마리" = 트리거 167.1 + 9 + 2 =
**약 178.1s / 180s**. 보스 웨이브는 보스 1 + 호위 3~4 = 4~5마리라 오히려 짧다. pin
테스트가 이 마진을 계속 지키도록 리드인을 포함해 계산해야 한다.

## 완료 기준

- **EditMode 신규** (`WaveSpawnLeadInTests`):
  - `Generate(deck)` 플랜의 `spawnLeadInSec` = 덱 값. 음수는 0 클램프.
  - 트리거 그리드 불변: `waves[i].triggerTimeSec == i × interval` (리드인과 무관).
  - `FromPlanAsset` 플랜의 `spawnLeadInSec == 0` (작성 플랜 이중 가산 금지).
  - 리드인 2 로 큐잉하면 pending 첫 스폰 = `트리거 + 2`, 리드인 0 이면 기존과 동일.
  - `ForceNextWave` 후 `_waveTimeShift` 가 리드인에 오염되지 않는다(다음 웨이브 큐잉 시각 = 호출 시점 + 원래 간격).
- **회귀**: `WaveForceRescheduleTests` 5건 · `WavePatternGeneratorTests` · `WaveFixedIntervalTests` · `WaveSpawnForecastTests` green. EditMode 전체 무회귀.
- **pin 갱신**: `WaveKillBudgetPinTests.LastSpawn_FitsInsideTheTimeLimit` 이 `plan.spawnLeadInSec` 를 더한 뒤에도 `< timerDurationSec`.
- **Play**: 콘솔의 `Wave N queued` 시각 + 2초에 첫 적이 등장한다. 웨이브 간격·`Next Wave` 연타 동작 불변.
- **범위 밖**: 예고선이 Wave 1·강제 웨이브에 뜨게 하는 것은 이 unit 이 아니다 → `spawn-point-alert/3_alert_for_every_wave.md`(예보 소스를 큐잉된 웨이브로). 리드인만으로는 예보가 `_nextWaveIndex` 기준이라 그대로 스킵된다.

## 함께 갱신하는 문서

- `docs/spec/wave-pattern/README.md` — "시간 배정" 에 리드인 한 줄. 계약 "lane 배정은 `localIndex % laneCount`" 는 3+ lane 실제 규약(`deckIndex % laneCount`)과 다르므로 같이 정정.
- `docs/reference/map-wave-balancing.md` — "스폰 템포" 에 knob 추가.
