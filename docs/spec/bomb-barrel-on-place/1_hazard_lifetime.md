# 1 — 설치물 수명 (만료도 「부서짐」이다)

> **은퇴 (2026-08-23, unit 7).** 길막 설치물의 시한은 폐기됐다 — 배럴은 부서져야만 사라지고
> 터진다. `BlockingHazardSO.lifetime` 과 `ObstacleLifetimeSystem` 의 길막 루프 둘 다 제거됐다.
> 되살릴 거라면 **둘을 같이** 되살려야 한다(한쪽만이면 저작해도 아무 일이 안 일어난다).
> 첫 루프(장판형 해저드 수명)는 그대로 살아 있다 — 시간으로 사라지는 것은 장판이지 벽이 아니다.


## 목적

길막 설치물에 수명을 주고, **만료를 파괴와 같은 문으로 보낸다.** 그래야 unit 0 의 폭발이
「적이 부쉈다」와 「시간이 다했다」 둘 다를 자동으로 덮는다. 아무도 안 때려도 배럴이
반드시 터지므로 배치 페이즈(적 0마리)에 놓아도 스킬이 사라지지 않는다.

## 변경 대상

- `Assets/_Project/Scripts/Battle/Effects/BlockingHazardSO.cs` — `float lifetime`
- `Assets/_Project/Scripts/Battle/Effects/EffectSpawner.cs` — `Obstacle.remainingLife` bake
- `Assets/_Project/Scripts/Battle/Effects/ObstacleLifetimeSystem.cs` — 배럴 틱
- `Assets/_Project/Tests/EditMode/` — 수명 만료 단언

## 구현

- **SO**: `float lifetime` (0 또는 음수 = 무한). 기존 에셋은 0 으로 역직렬화 = **현행 무한
  유지**(무회귀).
- **bake**: `SpawnBlockingHazard` 가 `remainingLife = lifetime > 0 ? lifetime : ∞`.
  지금은 `float.PositiveInfinity` 하드코딩이다.
- **틱 위치**: `ObstacleLifetimeSystem` 의 **두 번째 루프**(이미 `BlockingHazard` +
  `OccupiedCellsBuffer` 를 순회하며 차단 칸을 모은다)에 `RefRW<Obstacle>` 를 더해 틱한다.
  첫 루프는 `WithNone<OccupiedCellsBuffer>` 라 길막 설치물이 애초에 안 들어온다 —
  그게 지금 수명이 안 도는 이유다.
- **⚠ 만료 시 `DestroyEntity` 가 아니라 `DeadTag`** (계약 4). 첫 루프는 만료에 즉시 파괴하는데
  그 관례를 따라가면 폭발이 건너뛰어진다. 파괴는 `UnitLifecycleSystem` 이 이미 하고, 거기가
  파괴 알림(연출)을 내는 곳이기도 하다 — 두 경로가 같은 출구를 쓴다.
- **⚠ 만료된 배럴은 그 순회에서 `blockedCells.Add` 를 건너뛴다.** 「`DeadTag` 를 붙이면
  `WithNone<DeadTag>` 가 알아서 제외한다」는 **틀렸다** — ECB 는 루프가 끝난 뒤 재생되므로
  지금 순회 중인 엔티티는 그 프레임 필터에서 안 빠진다(리뷰 지적). 첫 루프가 이미 올바른
  형태를 보여준다: 만료면 셀을 안 넣고 처분만 한다. 두 번째 루프도 같게 —
  만료면 `continue` 로 셀 추가를 건너뛴 뒤 ECB 로 `DeadTag`.

## 완료 기준

- [x] compile 0 에러.
- [x] EditMode: `lifetime` 0 설치물은 영원히 남는다(무회귀) · 값 있으면 그 초 뒤 `DeadTag` ·
      **만료된 프레임에 이미 차단 칸에서 빠진다**(위 ⚠ 의 회귀 핀) ·
      만료가 unit 0 의 폭발을 유발한다.
      ⚠ 연결 단언은 두 시스템의 실행 순서에 의존한다 — 순서를 강제하거나 `DeadTag` 를 손으로
      붙여 unit 0 을 격리 검증한다(리뷰 지적).
- [x] 전체 EditMode 회귀 없음.

확인 2026-08-22 · Play 에서 수명 만료 경로도 실측(`-barrel f13745` 직후 캐리어 1) — 두 사망 원인이
같은 문으로 나가는 것이 라이브에서 성립한다.
⚠ **구현 중 발견한 회귀**: 수명 틱을 차단 칸 루프에 합쳐 `Obstacle` 를 요구했더니, `Obstacle` 없이
`BlockingHazard` + 버퍼만 가진 방벽(battle-structures)이 쿼리에서 빠져 **통행을 안 막게** 됐다.
`StructureSpawnAndBreachTests` 가 잡았고 루프를 둘로 나눠 해결했다. 다시 합치지 말 것.
