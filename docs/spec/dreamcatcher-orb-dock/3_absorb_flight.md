# 3 · 흡수 비행 (입자가 곧 피규어)

## 목적

킬/아군 사망 위치에서 피규어가 항아리로 **날아와 쌓인다**(입자=피규어, 사용자 결정 2026-07-23).
unit 2a 의 "항아리 위에서 스폰"을 "킬 위치에서 아치 비행 진입"으로 바꾼 통합. "많이 나오는"
= 획득으로 늘어난 목표만큼 여러 피규어가 킬 위치에서 팡 날아온다.

## 변경 대상

- `BattleBridge.Dreamcatcher.cs` — `EnemyKilledAwakening(int)`→`(int, Vector3)`,
  `DefenderDied(Entity, DefenderUnitData)`→`(..., Vector3)`. 사망 view-space 위치 동봉.
- `BattleBridge.cs` — 발화 2곳: enemy `BoardSpace.ToView((Vector3)evt.position)`(기존 예약된
  `EnemyKilledEvent.position` surfacing), defender `GridCellToViewCenter(cell)`.
- `DreamcatcherHandController.cs` — 핸들러 위치 스레드, `AwakeningGainedAt(int applied, Vector3)`
  재노출(Mono 전용).
- `JarFigurePile.cs` — 자동 스폰 제거 → 명시적 `SpawnAtTop`/`RemoveTop`/`Clear`/`ActiveCount`.
- `AwakeningGaugeView.cs` — 흡수 비행 시스템.

## 구현

- **위치 surfacing**: 브리지가 이미 값으로 bake 된 사망 sim 위치를 `BoardSpace.ToView` 로 view-space
  변환해 relay(새 ECS write 아님, 게이트웨이 역할 안). ecs-review: 경계 CLEAN.
- **카운트 대사(desync 방지)**: 목표는 항상 `Gauge`(SoT)에서 파생(`FiguresForGauge`).
  `committed = pile.ActiveCount + _pendingFlights`. 획득 시 `delta = target − committed` 만큼
  비행 발사. 도착·게이지변경마다 `TrimToTarget` 로 재수렴(오버슈트 self-correct). 소비/리셋은
  감소 경로에서 `RemoveTop`.
- **비행**: `AwakeningGainedAt` → 킬 world→screen(`Camera.main`)→SafeAreaRoot-local, 항아리 상단
  world→local 종점으로 아치 비행하는 고스트. 도착 시 `pile.SpawnAtTop`. 고스트 **풀링**(GC 완화).
- **정리**: 전투 이탈/OnDisable 시 `CancelFlights`(generation 무효화) — 진행 비행 중단,
  `_pendingFlights` 오염·전환 중 고아 고스트 차단.
- **폴백**: 패널 비활성·카메라 뒤(z≤0)·무효 좌표 → 즉시 `SpawnAtTop`(비행 없이 카운트 유지).
- Time: 비행·물리 모두 `unscaledDeltaTime`(슬로모/TimeManager 정지 중에도 진행, timeScale 금지 정합).

## 완료 기준

- **compile** 그린. 이벤트 위드닝 파급(컨트롤러·BountyMarkTest·PlacementAuraTest) 갱신.
- **라이브 하네스 검증**: gauge 30→목표 6, 킬 위치에서 고스트 6 비행 → 도착 시 pile 6
  (pending 6→0, 드리프트 없음). 풀 재사용. Battle 이탈 시 pending 6→0·고스트 비활성.
- **투트랙 리뷰 반영**: ecs 경계 CLEAN, HIGH(테스트 위드닝)+MEDIUM(정리·풀링) 수정.
- 실기 비행 육안/다발 킬 clutter 체감은 후속.
