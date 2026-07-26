# 3. 모든 웨이브에 예고 — 예보 소스를 "큐잉된 웨이브"로

**작업 구분**: 추가 3 (2026-07-26) · **선행**: `wave-pattern/11_wave_spawn_lead_in.md`

## 목적

"각 웨이브 시작에 앞서 안내한다"를 **예외 없이** 성립시킨다. unit 1 은 예보를 `_nextWaveIndex`
(아직 큐잉되지 않은 다음 웨이브)로 계산했고, 트리거 시각 = 첫 스폰 시각이었다. 그래서
**Wave 1(트리거 0초)과 당긴 웨이브는 창이 성립하지 않아 예고가 없었다**(unit 1 은 이를
"자연 스킵" 계약으로 명시했다).

선행 unit(리드인 2초)이 창을 만들어 주므로, 예보를 **미래 예측이 아니라 큐잉 시점의 사실**로
바꾸면 세 경우(자동·강제·Wave 1)가 같은 경로에서 같은 창을 얻는다.

## 변경 대상

- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — `QueueWave` 가 예보를 채우고, 게터는 서빙만
- `Assets/_Project/Tests/EditMode/SpawnAlertForecastTests.cs` — 신규
- 뷰(`SpawnAlertPresenter`) **무변경**

## 구현

`QueueWave` 는 이미 `wave` · `baseTriggerTimeSec`(트리거 + 리드인) · `laneCount` 를 모두 갖고
있다. 거기서 `FirstSpawnTimesPerLane` 을 1회 호출해 예보를 채운다 — 실스폰 엔트리와 **같은
인자**로 같은 순수 함수를 호출하므로 어긋날 여지가 없다(unit 0 계약 유지).

게터는 서빙만 남는다:

```csharp
if (!_running || _spawnAlertForecast == null) return false;
if (LastSpawnSec(_spawnAlertForecast) <= battleClockSec) return false;  // 마지막 lane 스폰까지 유지
laneFirstSpawnSec = _spawnAlertForecast; return true;
```

**삭제된 것** (예측을 버려서 함께 사라진 로직):

- `_spawnAlertForecastWaveIndex` 캐시 키와 재계산 분기
- "캐시에 미래 스폰이 남아 있으면 계속 서빙" stale-serve 휴리스틱 → 예보 자체가 큐잉된
  웨이브라 `LastSpawnSec` 판정 하나로 충분
- `ForceNextWave` 의 캐시 무효화 2줄 (강제 호출도 이제 예고를 받는다)
- 게터의 `_usingGeneratedWaves` / `_wavePlan` / `_generatedMap` 게이트 — 예보는 `QueueWave` 만
  채우므로 legacy `deck.spawns` 경로는 자연히 null(= 예고 없음, 기존 동작 유지)

리셋은 `_battleClock = 0` 과 짝(계약 9): `TryInitializeGeneratedWaves` · `StopBattle`.

## 계약 변경 (README 반영)

| unit 1 계약 | unit 3 |
|---|---|
| 예보 = 다음 예정 웨이브 **예측** | 예보 = **큐잉된 웨이브의 실제 스폰 base** |
| Wave 1 자연 스킵 | **예고 있음** (창 = 트리거 ~ 트리거+리드인) |
| 강제 호출 자연 스킵 | **예고 있음** (창 = 당긴 시점 ~ +리드인) |

- 강제 호출 연타 시 예보는 **최신 큐잉 웨이브** 기준이다(직전 웨이브의 남은 lane 예고는
  최신 것으로 대체된다). 같은 순간에 큐잉되므로 시각이 사실상 동일 — 의도된 단순화.
- 프레젠터 `leadSec`(씬 2.5)은 리드인(2)보다 크므로 **실효 리드 = min(leadSec, 리드인)** 이다.
  예보가 큐잉 순간에 생겨 그 이전 프레임에는 아무것도 없기 때문. `leadSec` 를 리드인보다
  작게 두면 그만큼 늦게 뜬다(안내 시간 단축).

## 완료 기준

- **EditMode 신규** (`SpawnAlertForecastTests` 5건):
  - 큐잉 전 = 예보 없음.
  - **Wave 1 큐잉 직후 예보 있음**, lane 시각 = `base + i × intraWaveSpacing`(3 lane = 2/3/4초).
  - **강제 호출 직후 예보 있음**, 최초 lane 시각 = 당긴 시점 + 리드인.
  - 마지막 lane 스폰 전까지 유지되고 지나면 사라진다(뒷 lane 조기 소멸 방지 회귀).
  - `_running=false` = 즉시 없음(전투 종료 정리 근거).
- **회귀**: EditMode 전체 무회귀. `WaveSpawnForecastTests`(순수 함수) 그대로 green — 이 unit 은
  호출 위치만 옮겼다.
- **Play**: 모든 웨이브에서 웨이브 큐잉 로그 시각에 예고선이 그어지고 첫 적 등장과 함께
  수렴한다. Wave 1(배틀 시작 0초)과 `Next Wave` 당김 직후도 포함. 마지막 웨이브 이후 잔상 없음.
