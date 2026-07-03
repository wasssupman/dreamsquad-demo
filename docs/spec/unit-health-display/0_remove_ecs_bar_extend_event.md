# 0 — 구 ECS 헬스바 제거 + DamageNumberEvent 계약 확장

## 목적

보이지 않는 ECS 헬스바(죽은 코드)를 삭제하고, `DamageNumberEvent` 에 마이크로바용 필드를 추가한다. **동작·시각 무변경** (신규 필드는 unit 2 전까지 아무도 안 읽음). units 1~2 의 토대.

## 변경 대상

- 삭제: `Assets/_Project/Scripts/Battle/Units/HealthBar/` 3파일 (`HealthBarSystem.cs`/`HealthBarTag.cs`/`HealthBarState.cs`) + .meta
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — `CreateHealthBar`(:2362) + 호출 3곳(defender `CreateDefenderEntity` :2969 / blocking hazard :3110 / enemy `SpawnUnit` :3617) + `_healthBarRenderGateLogged`(:202) + `DestroyEntitiesByType<HealthBarTag>()`(:382) + using(:17)
- `Assets/_Project/Scripts/Presentation/BoardSortOrder.cs` — `HealthBarOffset`(:9, 외부 사용처 없음 확인됨)
- `Assets/_Project/Scripts/Battle/Combat/Projectile/ProjectileHitSystem.cs` — :55 주석의 HealthBar 언급 정리
- `Assets/_Project/Scripts/Battle/Units/DamageNumberEvent.cs` — `entity` + `hpRatio` 필드
- `Assets/_Project/Scripts/Battle/Units/DamageApplicationSystem.cs` — 발행 지점 이동 + hpRatio 계산

## 구현

- `DamageNumberEvent` 에 `public Entity entity;` + `public float hpRatio;` 추가. 주석에 계약 명시: hpRatio = 해당 프레임 데미지·힐 정산 후 `clamp(newHp/max, 0, 1)`, max<=0 이면 0.
- `DamageApplicationSystem` 의 enqueue(:71)는 현재 HP 갱신(:94) **전**에 있다 → enqueue 를 `newHp` 계산 후로 이동해 정산 후 ratio 를 싣는다. `amount = totalDamage`(post-mitigation)·`AttackUnitTag` 필터·`totalDamage > 0` 조건은 그대로.
- `DrainDamageNumberEvents`(BattleBridge :1918)는 무수정 — 신규 필드 미사용으로 동작 보존.

## 완료 기준

- compile 0 에러 (HealthBar 타입 잔여 참조 grep 0).
- EditMode 전체 무회귀.
- 동작 변화 0 — 헬스바는 원래 안 보였고, 데미지 숫자 위치/값 현행 유지.

— 완료 확인 2026-07-03 · 커밋 `74b3807` (병렬 legacy-removal 과 HealthBar 삭제가 겹쳐 그 세션이 격리 커밋). compile 0, EditMode 434 중 433 통과 — 유일 실패 `ObstaclePlacerTests` 는 이 변경/legacy-removal 양쪽과 무관한 사전 실패(placer 는 obstaclePrefabs/minPlaceableRatio 만 읽고, 제거된 43 필드는 전부 텍스처 비주얼).
