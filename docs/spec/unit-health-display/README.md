# unit-health-display — 적/방어유닛 체력 표기

> 상태: 진행 중. unit 0(`74b3807`) · unit 1(`c32b056`+리팩`1119489`+씬`7c3820f`) · unit 2(`f51e41e`) 완료 · unit 3 대기.

## 배경 / 문제

현재 화면에 보이는 체력 표시가 **없다**. ECS 헬스바(`HealthBarSystem`/`Tag`/`State` + `BattleBridge.CreateHealthBar`)는 tilemap 뷰 전환 때 Entities Graphics 렌더 컴포넌트 생성이 게이트되어(`BattleBridge.cs:2378`) 보이지 않는 엔티티만 만든다. 이번 스펙은 관성적 "머리 위 바 복원"이 아니라 백지 재설계다. 설계 배경: `docs/plans/2026-07-03-unit-health-display-design.md`.

## 검증 질문

바 없이 체력이 읽히는가 — 적은 피격 순간(마이크로바)과 몸 상태(틴트)로, 방어유닛은 점유 타일 테두리로, 화면이 어수선해지지 않으면서 한눈에 구분되는가?

## 모델 — 정보 비대칭

**"방어유닛은 타일이 말하고, 적은 맞는 순간만 말한다."**

- 적 = transient(피격 시 마이크로바, ~1초 페이드) + ambient(저체력 컬러 틴트). 상시 표시 없음.
- 방어유닛 = persistent(점유 타일 테두리 게이지). 단 full HP 는 숨김.

## 공통 원칙 / 계약 (2026-07-03)

- **ECS 경계 불변**: HP 값이 Mono 로 나오는 경로는 BattleBridge 2개뿐 — ① `DamageNumberEvent` drain(피격 순간), ② `SyncMonoUnitViews` 폴링(read-only Health). 뷰/스포너/레이어는 ECS 직접 접근 금지.
- **`DamageNumberEvent` 확장 계약**: `entity`(Entity) + `hpRatio`(float) 추가. hpRatio 는 해당 프레임 데미지·힐 **정산 후** `clamp(value/max, 0, 1)`. 발행은 기존대로 `DamageApplicationSystem`(Units 소유 맥락) 안, 적(`AttackUnitTag`) 전용 유지.
- **시각 파라미터 단일 소스**: 신규 SO `HealthDisplayStyle` (틴트 Gradient, 바 크기/hold/fade, 게이지 색 램프/hideWhenFull). 하드코딩 금지.
- **좌표 규약**: 이벤트 position 은 sim 좌표, 렌더는 view 좌표. 마이크로바 앵커는 유닛 뷰 transform(이미 view) 우선, 뷰 부재 시 `BoardSpace.ToView(evt.position)` fallback (`DamageNumberView.cs:43` 선례).
- **정렬**: 마이크로바 = 캐릭터 위·데미지 숫자(32000) 아래 고정 오더. 타일 게이지 = 그림자(`ShadowOrder=-5`) 위·캐릭터(양수) 아래.
- **구 ECS 헬스바는 삭제** (대체가 아니라 죽은 코드 제거). blocking hazard 의 바 호출도 함께 사라짐 — hazard 체력 표시는 후속 후보.
- 마이크로바는 같은 적 연속 피격 시 **스택 금지** — 기존 바 갱신 + 타이머 리셋 (entity 키 dict).

## 작업 단위

| # | 작업 | 문서 | 목적 |
|---|---|---|---|
| 0 | 구 ECS 헬스바 제거 + 이벤트 계약 확장 | `0_remove_ecs_bar_extend_event.md` | 죽은 코드 삭제 + `DamageNumberEvent` entity/hpRatio. **동작 무변경.** compile |
| 1 | `HealthDisplayStyle` SO + 적 저체력 틴트 | `1_style_so_enemy_tint.md` | SO 토대 + Spine/Quad 틴트 램프. Play 스크린샷 |
| 2 | 적 피격 마이크로바 | `2_enemy_hit_microbar.md` | 스포너/풀/뷰 + drain 연결 + 씬 배선. Play 스크린샷 |
| 3 | 방어유닛 타일 테두리 게이지 | `3_defender_tile_gauge.md` | 셰이더그래프 + 레이어/뷰 + 폴링 + 씬 배선. Play 스크린샷 |
| 4 | handoff | `4_handoff_summary.md` | feature 종료 시 작성 |

## 후속 후보

- **킬 포어캐스트 마크** [M] · `IncomingDamage` 버퍼 + 비행 투사체 예약 데미지 ≥ 잔여 HP 인 적에 스컬 마크. 바가 못 주는 의사결정 정보. 투사체 데미지 귀속 필요.
- **blocking hazard 체력 표시** [S] · unit 0 에서 hazard 의 (안 보이던) 바 호출 제거 — 시각 니즈 생기면 타일 게이지 재사용 검토.
- **상태이상 틴트 합성 규칙** [S] · Slow/버프 틴트가 생기면 헬스 틴트와 곱연산 합성 규격화.
- **웨이브 압력 게이지** [M] · 상단 HUD 에 웨이브 총 잔여 HP. 비동기 토너먼트 관전 정보.
- **보스/엘리트 상시 바** [S] · 위협 개체만 승격 표시.
- **적 틴트 poll 효율화** [S] · (투트랙 리뷰 M1) `SyncMonoUnitViews` 가 매 프레임 적마다 `GetComponentData<Health>` + gradient + `SetHealthTint`(만피=白 no-op 포함). 배치 `ToComponentDataArray<Health>` 또는 last-ratio 캐시로 full-HP 반복 write 제거. 현재 sync point 신규 아님·GC 없음이라 비블로커, 적 수 급증 시 착수.
