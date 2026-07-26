# 0 — 웨이브 사이 클리어 판정

## 목적

“현재 호출된 몬스터가 모두 사라짐”을 다음 웨이브 버튼 강조 상태로 만든다. 리드인·유출·분산
스폰을 오판하지 않도록 스케줄러와 Units 생명주기의 source of truth를 결합한다.

## 변경 대상

- `Assets/_Project/Scripts/Bridge/BattleBridge.cs`
- `Assets/_Project/Tests/EditMode/NextWaveClearReadyTests.cs` (신규)

## 구현

`BattleBridge`가 Update 말미(스폰·이벤트 drain·승패 판정 뒤)에 아래 상태를 캐시한다.

```text
NextWaveClearReady =
  running
  && generated wave plan
  && nextWaveIndex > 0
  && nextWaveIndex < waveCount
  && pending.Count == 0
  && aliveAttackersQuery.CalculateEntityCount() == 0
```

- `CheckVictory()`와 신규 판정은 private `NoQueuedAttackersRemain()`을 공유한다.
- `pending`은 **이미 QueueWave 된 웨이브의 아직 나오지 않은 적**이다. 2초 리드인과
  `intraWaveSpacingSec` 구간에서 필드가 비어도 false를 유지한다.
- `aliveAttackersQuery`는 `AttackUnitTag`의 실제 잔존 source다. kill과 goal-reached 모두
  엔티티 제거 뒤 0으로 수렴하므로 이벤트를 수동 합산하지 않는다.
- `nextWaveIndex`로 첫 호출 전과 마지막 호출 뒤를 구조적으로 제외한다.
- 강제 연타 시 `pending`과 필드 쿼리가 합집합을 보므로 겹친 호출분이 모두 빌 때까지 false다.
- UI에는 `public bool NextWaveClearReady` 읽기 API만 노출한다. 실제 ECS query는
  `BattleBridge` 안에서만 실행한다.
- `ForceNextWave`, 전투 시작/리셋, 자동 큐잉, 종료에서 캐시를 false로 되돌린다. ECB 반영
  순서에 따른 최대 한 프레임 지연은 허용하되 빠른 오판은 허용하지 않는다.

새 `WaveId` Component, `EnemyKilledEvent`/`GoalReachedEvent` 필드, NativeQueue는 만들지 않는다.
현재 판정에는 웨이브 소속이 필요 없다.

## 완료 기준

- EditMode:
  - 첫 웨이브 리드인/분산 스폰(`pending > 0`, 적 0)은 false.
  - pending 0이어도 `AttackUnitTag`가 하나 남으면 false.
  - 다음 웨이브가 있고 pending/필드가 모두 비면 true.
  - 웨이브 2개 이상 강제 큐잉 시 합집합이 빌 때까지 false.
  - goal 도달 제거와 kill 제거가 모두 같은 true 상태에 도달.
  - 첫 호출 전·마지막 호출 뒤·legacy/정지/리셋은 false.
  - `ForceNextWave()` 직후 false이며 기존 `_waveTimeShift` 테스트는 무회귀.
- `CheckVictory()`와 신규 판정이 같은 `NoQueuedAttackersRemain()`을 사용한다.
- Unity 6000.4.3f1 / Entities 6.4.0 compile green, 신규 채널·Component 0개.

검증 2026-07-26 — EditMode 전체 1,358 pass / 0 fail / 2 skip — commit `8431b891`.
