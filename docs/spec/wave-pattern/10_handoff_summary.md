# 10. Handoff Summary — wave-pattern unit 9 (강제 호출 리스케줄)

> 2026-07-22 작성. 최신 계약은 README 와 `9_force_wave_reschedule.md` 가 우선한다. 이 문서는 지도다.
> units 0~5 인계는 `5_handoff_summary.md`, units 6~7 은 `8_handoff_summary.md`.

## Commit

- `dc80a3fc` fix(wave-pattern): unit 9 — Next Wave 가 남은 웨이브를 함께 앞당기도록
- `132b34fa` docs(wave-pattern): unit 9 완료 확인 — 사용자 Play 통과

## Implemented

- **강제 호출 리스케줄**: `ForceNextWave` 가 해당 웨이브만 당기던 것을, 남은 웨이브 전체를 균일 오프셋(`_waveTimeShift`)으로 이동하도록 수정. 웨이브 간 간격이 보존돼 다음 웨이브는 "호출 시점 + 원래 간격"에 나온다. 연타하면 매 호출마다 그 시점으로 재기준.
- **새 기능 아님**: README 의 기존 계약("연타는 남은 wave 들을 순서대로 앞당긴다")과 구현이 어긋난 불일치 수정이었다.
- **플랜 불변**: `_wavePlan.waves[i].triggerTimeSec` 은 손대지 않는다(브리핑 스트립·로그의 source of truth). 런타임 오프셋만 이동.
- **단일 창구 `ScheduledWaveTime()`**: 스케줄을 읽는 세 지점(자동 큐잉 `QueueDueWaves`, 강제 호출 `ForceNextWave`, 스폰 예고 `TryGetSpawnAlertForecast`)이 전부 이 메서드를 경유.
- **리셋 계약 9**: `_battleClock` 이 0 이 되는 두 지점(`StopBattle`, `TryInitializeGeneratedWaves`)에서 `_waveTimeShift` 도 0.

## Key Files

- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — `_waveTimeShift` 필드, `ScheduledWaveTime()`, `ForceNextWave` 오프셋 갱신, 세 읽기 지점
- `Assets/_Project/Tests/EditMode/WaveForceRescheduleTests.cs` — 리플렉션 픽스처(ECS world 불요), 5케이스
- `docs/spec/wave-pattern/9_force_wave_reschedule.md` — 계약·완료 기준

## Verified

- EditMode 1173 통과(실패 0, 스킵 2 = 기존 known-ignore). `WaveForceRescheduleTests` 5건 신규 그린.
- **뮤테이션 검증**: 리그 사본에만 오프셋 갱신 1줄 제거 → 동작 테스트 3건이 정확히 실패, 대조군 2건(기준선·플랜 불변) 통과. vacuous 아님 실증 후 원복.
- 사용자 Play 확인 2026-07-22 — 강제 호출 후 다음 웨이브 자연 연결 통과.

## Notes (되돌리면 안 되는 판단)

- **오프셋 갱신은 `_nextWaveIndex++` 앞에서.** 밀 대상 = 지금 강제 호출하는 웨이브의 예정 시각이라, 인덱스 증가 후 계산하면 한 칸 밀린 값을 쓴다.
- **간격 상수(`waveIntervalSec`) 재계산이 아니라 오프셋 이동.** 웨이브마다 길이가 다른 작성 플랜(`WavePlanAsset`, PerGroupTimeline)에서도 각 웨이브 고유 간격이 유지되게 하려는 의도. 상수로 바꾸면 작성 플랜이 깨진다.
- **예고선을 같이 옮기지 않으면** 예고와 실제 스폰이 어긋난다 — `TryGetSpawnAlertForecast` 도 반드시 `ScheduledWaveTime` 경유.
- **전투 타이머는 함께 당기지 않는다**(의도). 웨이브를 몰아 부르면 그만큼 일찍 전멸시켜 판이 빨리 끝나는 게 자연스러운 결과.

## Follow-up

- **조기 클리어 시간점수 보상 과다** [M] — 이 수정으로 "빨리 불러 빨리 끝내기"가 성립. `timeScorePerSecond:100`×180초 = 시간점수 18,000(총예산의 ~48%)이고 `ForceNextWave` 는 예정 시각 도달을 검사하지 않아 시작 직후 연타로 전 웨이브를 쏟을 수 있다. 인지된 밸런싱 이슈 → `docs/spec/README.md` Follow-up Backlog 로 이관. 제동 후보: 호출 쿨다운 / "예정 −N초부터만" 게이트 / 시간점수 상한·곡선화.
