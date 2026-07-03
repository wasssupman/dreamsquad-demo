# unit-health-display — handoff summary

> 세션 인계 지도. 최신 계약은 README + 번호 문서가 우선. 구현 상세는 코드/커밋.

## Commit

- `74b3807` unit 0 — 구 ECS 헬스바 제거 + `DamageNumberEvent`(entity/hpRatio)
- `c32b056` unit 1 — 적 저체력 틴트 + `HealthDisplayStyle` SO / `1119489` 리팩(Health.ComputeRatio) / `7c3820f` 씬 영속화
- `f51e41e` unit 2 — 적 피격 마이크로바
- `ca37995` unit 3 — 방어유닛 타일 테두리 게이지
- `d35db12` 투트랙 리뷰 반영 (생명주기·중복·테스트)
- docs: `343d808 c4e7569 cbc44d3 b34c463 a3c89a6 f311345 820e535`

## Implemented

- 정보 비대칭 체력 표기: **적** = 피격 마이크로바(transient) + 저체력 몸 틴트(ambient), **방어유닛** = 점유 타일 테두리 게이지(persistent, 만피 숨김).
- 구 ECS 헬스바(`HealthBar*`, 렌더 게이트된 죽은 코드) 완전 제거.
- `DamageNumberEvent` 에 `entity`/`hpRatio`(HP 정산 후, 막타=0) — 데미지숫자 + 마이크로바 공용 채널.
- `HealthDisplayStyle` SO = 체력 표기 시각 파라미터 단일 소스(틴트/바/게이지 전부). 하드코딩 수치 없음.
- 마이크로바·게이지는 **절차적 스프라이트**(흰 스프라이트 shared) — 프리팹/셰이더그래프 없음.
- ECS 경계 불변: HP read-only 접근은 BattleBridge 창구 2경로(이벤트 drain + `SyncMonoUnitViews` 폴링)만.

## Key Files

- `Assets/_Project/Scripts/Data/HealthDisplayStyle.cs` — 모든 시각 파라미터 + `SafeRatio01`/`ComputeRatio`(Health)/Evaluate* .
- `Assets/_Project/Scripts/Presentation/` — `SpineUnitView`/`QuadUnitView`(SetHealthTint), `EnemyHitBarView`/`EnemyHitBarSpawner`, `TileHealthGaugeView`/`TileHealthGaugeLayer`.
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — `EvaluateEnemyHealthTint`, `DrainDamageNumberEvents`/`ResolveEnemyViewTransform`, `SyncMonoUnitViews` defender 게이지 폴링, 사망 Hide, teardown/BeginPlacement Clear.
- `Assets/_Project/Scripts/Battle/Units/{Health,DamageNumberEvent,DamageApplicationSystem}.cs` — hpRatio 계약.
- Tests: `HealthRatioTests`, `HealthDisplayStyleTests`, `HealthDisplayBarGaugeTests`.

## Verified

- compile 0. EditMode **458 중 457 통과** — 유일 실패 `ObstaclePlacerTests`(31 vs ≥36)는 이 spec 과 무관한 **사전 실패**(placer/BoardSpace 미변경, 확인됨).
- Play(스크린샷): 적 틴트 원색→창백→검붉음 · 마이크로바 등장/fill·색/hold→fade→pool 재활용 · 방어유닛 게이지 만피숨김/피격 부분 테두리/색전이/만피복귀 숨김. HitBar·Gauge 콘솔 에러 0.
- 씬 배선(healthDisplayStyle / enemyHitBarSpawner / tileHealthGaugeLayer) BattleScene 영속화 완료.
- 투트랙 리뷰(unit0+1, unit2+3) 양측 APPROVE — MEDIUM 반영, YAGNI 기각.

## Notes (되돌리면 안 되는 의도)

- `DamageNumberEvent.hpRatio` 는 **HP 정산 후** enqueue(막타=0) — DamageApplicationSystem enqueue 위치가 `newHp` 계산 뒤인 것이 계약.
- 뷰 좌표: 이벤트 position=sim, 렌더=view. 마이크로바/게이지 좌표는 `BoardSpace.ToView` 경유(sim≠view).
- 게이지/마이크로바 생명주기: `_active`⊕`_idle` 불변식. teardown 에서 spawner.Clear + layer.Clear, BeginPlacement 에서 layer.Clear(_defenderByTile.Clear 와 co-locate).
- 만피 숨김(게이지)·`_dying` 중 틴트 유지(Spine)는 의도.

## Follow-up

- **킬 포어캐스트 마크** [M] — IncomingDamage + 비행 투사체 예약 데미지 ≥ 잔여 HP 스컬 마크.
- **체력 표기 poll 효율화** [S] — 적/방어유닛 매 프레임 Health 조회+뷰 write, last-ratio 캐시로 skip.
- **타일 게이지 시각 폴리시** [S] — pad SO화, 코너 갭, 연속 SDF 셰이더 교체.
- **blocking hazard 체력 표시** / **상태이상 틴트 합성** / **웨이브 압력 게이지** / **보스 상시 바** — README 후속 후보.
- (검증 미완) unit 3 ③ 사망 시 게이지 제거는 코드/동일 Hide 경로 확인, 실기 킬 라이브 스크린샷은 미수행.
