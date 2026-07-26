# 4. Handoff Summary — 모든 웨이브 예고 (+ 스폰 리드인)

> 2026-07-26 작성. 최신 계약은 README 와 번호 문서가 우선한다. 이 문서는 지도다.
> **두 spec 의 짝 unit 을 한 문서로 묶었다** — `wave-pattern/11`(리드인)과 이 spec 의 unit 3
> (예보 소스 전환)은 한 커밋이고 서로 없으면 성립하지 않는다.

## Commit

- `ee9b7cb4` feat(wave-pattern,spawn-point-alert): 웨이브 스폰 리드인 2초 + 모든 웨이브 예고 (unit 11 · 3)
  - 두 unit 을 쪼개지 않은 이유: `ForceNextWave` 영역에서 같은 hunk 를 공유하고, unit 3 테스트가
    unit 11 의 창을 전제로 한다 — 분리하면 중간 커밋이 red.

## Implemented

- **웨이브 트리거와 첫 적 등장 사이에 2초 리드인.** `AttackDeck.waveSpawnLeadInSec`(기본 2) →
  `GeneratedWavePlan.spawnLeadInSec` → `QueueWave` 의 스폰 base. 전 덱 적용(엔드리스 포함, 사용자 결정 A).
- **트리거 그리드는 불변**: `triggerTimeSec = i × interval`, `_waveTimeShift` 리스케줄, 플랜 시각,
  브리핑 스트립 표기, `wave_started` 로그 시각 전부 그대로. 밀리는 것은 스폰뿐이다.
- **모든 웨이브가 예고를 받는다.** 예보를 `_nextWaveIndex` 예측에서 **`QueueWave` 가 큐잉 시점에
  1회 계산**하는 방식으로 바꿨다 → Wave 1(배틀 0초)과 `Next Wave` 당김도 리드인만큼의 창을 갖는다.
- 예보는 실스폰과 **같은 인자로 같은 순수 함수**(`FirstSpawnTimesPerLane`)를 호출한다 — unit 0 의
  "예보 = 실스폰" 보증을 그대로 유지하면서 예측을 제거했다.
- 예측을 버려 **BattleBridge 순 −13줄**: 캐시 키(`_spawnAlertForecastWaveIndex`), stale-serve
  휴리스틱, `ForceNextWave` 캐시 무효화, 게터의 4중 게이트가 사라졌다.
- **뷰 변경 0줄.** `SpawnAlertPresenter`·씬 값(`leadSec` 2.5) 그대로 — 실효 리드 = `min(leadSec, 리드인)` = 2초.
- 작성 플랜(`WavePlanAsset`/PerGroupTimeline)은 리드인 미적용(그룹 상대 시각으로 이미 표현 가능 —
  겹치면 이중 가산). legacy `deck.spawns` 경로는 `QueueWave` 를 안 지나 예고도 없음(기존 동작).

## Key Files

- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — `SpawnLeadInSec`, `QueueDueWaves`/`ForceNextWave`
  스폰 base, `QueueWave` 의 예보 채우기, `TryGetSpawnAlertForecast`(서빙만)
- `Assets/_Project/Scripts/Data/AttackDeck.cs` · `GeneratedWavePlan.cs` · `WavePatternGenerator.cs`
- `Assets/_Project/Tests/EditMode/WaveSpawnLeadInTests.cs`(8) · `SpawnAlertForecastTests.cs`(5)
- `Assets/_Project/Tests/EditMode/WaveKillBudgetPinTests.cs` — 마지막 스폰 pin 에 리드인 반영
- 계약: `docs/spec/wave-pattern/11_wave_spawn_lead_in.md` · `docs/spec/spawn-point-alert/3_alert_for_every_wave.md`

## Verified

- EditMode **1356건: 1354 pass / 0 fail / 2 known-skip**(기존 ModifierFramework 2건).
- PlayMode 54건: 실패는 **문서화된 사전 실패 세트 + 순서 의존 플레이크**. `DreamstoneCarryInSmokeTest`
  단독 실행 시 기존 Gift↔Placement drift 1건만 남는다(전체 실행에서만 PrimeTween 로그 변형으로 번짐).
  웨이브 직결 `EndlessModeSmokeTest`·`MovementIntegritySmokeTest` 통과. 근거: `dreamcatcher-attach-requirement/6_handoff_summary.md:41`, `outgame-login-gate/2_handoff_summary.md:41`.
- **사용자 Play 확인 2026-07-26** — Wave 1(0초 선 → 2초 첫 적) · 일반 웨이브 · `Next Wave` 당김 · 종료 잔상 없음.

## Notes (되돌리면 안 되는 판단)

- **`_waveTimeShift` 산식에 리드인을 넣지 않는다.** `-= ScheduledWaveTime(i) - elapsedSec` 는 트리거
  그리드 기준이다. 섞으면 강제 호출 연타마다 리드인이 누적 왜곡된다.
- **예보 base 는 `QueueWave` base 와 반드시 같은 값.** 리드인을 예보에서 빼먹으면 예고선이
  실스폰보다 리드인만큼 먼저 사라진다(구현 중 실제로 걸렸던 지점).
- **리드인은 플랜에 싣는다**(덱을 소비자가 직접 읽지 않는다). `intraWaveSpacingSec` 가 이미 같은
  이유로 플랜에 있고, authored/seed 정책을 생산자(`FromPlanAsset` = 0)에서 끝내 소비자를 분기 없이 둔다.
- **`_pending` 전체 스캔 예보는 기각됐다.** 같은 lane 의 2·3번째 유닛(= spacing × laneCount 간격)마다
  창이 다시 열려 라인이 웨이브 내내 점멸한다 — "웨이브당 1회 안내"가 "스폰당 안내"로 변질된다.
- **tail 마진**: 리드인이 마지막 스폰을 2초 밀어 최악(15웨이브 롤) 약 178.1s/180s 다. 리드인이나
  웨이브 수를 올릴 때 `WaveKillBudgetPinTests.LastSpawn_FitsInsideTheTimeLimit` 를 반드시 확인한다.
- 강제 호출 연타 시 예보는 **최신 큐잉 웨이브** 기준(직전 웨이브 잔여 lane 예고를 대체) — 같은
  순간 큐잉이라 시각 차가 사실상 없어 의도된 단순화다.

## Follow-up

- **안내의 표현 보강** — 현재 안내는 경로 라인 하나에 전적으로 의존한다(웨이브 번호·보스 여부·
  소리 없음). 다음 레버는 로직이 아니라 표현이며 `NextWaveDock`·보스 크림슨 배너와의 역할 중복
  판단이 선행. README 후속 후보 참조.
- 리뷰에서 "선택(빼도 무영향)"으로 판정한 2건 유지 중: `EveryShippedDeck_CarriesALeadIn` 데이터 pin,
  `StopBattle` 의 `_spawnAlertForecast = null`(계약 9 대칭성).
- 예고선 실기기(Android) 성능은 여전히 미측정(unit 1 부터 이월).
- PlayMode 스위트 순서 오염은 이 spec 밖 — 백로그 "PlayMode 스모크 위생".
