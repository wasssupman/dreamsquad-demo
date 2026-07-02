# effect-tiles — 배치 타일 효과 타일 (버프/디버프)

> 상태: units 0~2 완료 2026-07-02 (`f07f1a6`·`b1c191d`·`c8ee5c2` · EditMode 6/6 · Play 검증 PASS) → **unit 4 진행**(다중 stat + 신규 타일). 신규 ECS 시스템 0개. 2-렌즈 스펙 리뷰 GO-WITH-CHANGES 반영.

## 배경 / 목표

Place 타일 위에 오버레이되는 **효과 타일**. 그 셀에 방어 유닛을 배치하면 타일에 맞는 버프/디버프를 유닛에게 부여한다. 배치 선택에 리스크/리워드 축을 추가한다.

## 검증 질문

맵 생성 시 seed 결정론으로 Place 셀에 효과 타일이 표시되고, 그 셀에 유닛을 배치하면(즉시/드래그 두 경로 모두) 해당 효과가 기존 modifier 파이프라인으로 정확히 1회 부여되는가?

## 초기 효과 세트 (수치는 SO — 조정 가능)

| id | 효과 | StatKind | 성격 |
|---|---|---|---|
| effect_tile_damage | 공격력 +25% | DamageMul ×1.25 | 버프 |
| effect_tile_attack_speed | 공속 +20% | AttackSpeedMul ×1.2 | 버프 |
| effect_tile_fragile | 받는 피해 +25% | DmgTakenMul ×1.25 | 디버프 |

리뷰 확인: 3종 모두 방어유닛이 실제 소비 — DamageMul/AttackSpeedMul(`AttackSystem`), DmgTakenMul(`DamageApplicationSystem` + aggro 로 적이 방어유닛 공격).

## feature-wide 계약

1. **신규 ECS 시스템/컴포넌트 0개.** 효과 부여 = 기존 `StatModifierApplyEventsSingleton` 큐. BattleBridge 의 기존 `EnqueueStatMul` 헬퍼(`BattleBridge.cs:2362`) 재사용.
2. **modifier 슬롯 네임스페이스(M1)**: `source = 배치된 유닛(target)` + **전용 `EffectTileStackId = 2`** (기존 규약: on-place=0 · 시너지=1 · 드림캐쳐=100+). `Entity.Null`/stackId 0 금지 — merge-key 충돌 방지 + revocation 후속 대비. duration = `float.PositiveInfinity`(시너지 관용, `:2328`).
3. **효과 타일 상태는 bridge-side** — `Dictionary<Vector2Int, EffectTileData>`. sim(ECS/GeneratedMap/FlowField) 무변경. 적 유닛 무영향.
4. **`AddEffectTile(cell, data)` 단일 진입점** (BattleBridge). dict 등록 + View 페인트 + 점유 셀 즉시 적용(순서 무관 불변식 — 현재는 후속 런타임 생성 루트에서만 도달, 주석 명시). 맵 빌드 seed 선정은 첫 client — 드림캐쳐/유닛 능력도 같은 진입점.
5. **셀 선정 = 순수 static** `EffectTilePlacer.SelectCells(in GeneratedMap, seed, count)`. Place 셀만·중복 없음·`math.max(1, seed)` 가드(Random 0 panic)·prop 배치와 decorrelate(`seed ^ 상수`). 같은 맵 = 양측 동일(match-seed 일관: 맵은 draft 에서 빌드되어 battle 까지 유지, `DeriveMapSeed` 확인됨).
6. **비주얼 = 런타임 생성 효과 타일맵** (grid 하위, anchor 0.5/0.5, sorting −15: ground −20 과 overlay −10 사이, cast off). 기존 `overlayTilemap` 공유 금지(hover/reject SetTile/null 충돌 — 리뷰 확인). 런타임 생성 = 씬 저장 이슈 회피. `Clear()` 가 효과 타일맵도 비움. **페인트는 `Initialize`(Clear 포함) 이후 실행.**
7. **부여 훅 = 기존 exactly-once 수렴점** — `TriggerOnPlaceAndSynergy`(즉시)/`TriggerDeploymentOnPlaceSkill`(드래그) + `_onPlaceTriggeredEntities` 가드(모든 배치 경로 커버 — 리뷰 확인).
8. 효과 정의 = `EffectTileData : ScriptableObject`. Legacy3D 모드에선 미동작(`tilemapMapView` null 가드) — Tilemap-only.

## 작업 단위 (리뷰 M2: 데이터/비주얼 분할)

| # | 작업 | 문서 | 목적 |
|---|---|---|---|
| 0 | 데이터 + 셀 선정 (순수) | `0_data_and_selection.md` | EffectTileData SO + EffectTilePlacer.SelectCells + EditMode 결정론 테스트. compile+테스트 |
| 1 | 비주얼 + 맵빌드 배선 | `1_visual_and_wiring.md` | 런타임 효과 타일맵 + AddEffectTile + 맵빌드 배선 + 에셋 3종. Play 시각 |
| 2 | 배치 훅 + 검증 | `2_placement_hook.md` | 두 경로 → modifier enqueue(stackId=2). EditMode 통합 + Play 검증. ✅ |
| 3 | handoff | `3_handoff_summary.md` | units 0~2 인계 지도 |
| 4 | 다중 stat + 신규 타일 | `4_multi_stat_and_new_tiles.md` | `effects[]` 배열 확장 + 글래스캐논(×1.4/×1.4) + 재생(Additive) + 종류 배정 rng 화 |

## 후속 후보

- **런타임 생성 루트** [M] · 드림캐쳐/유닛 능력이 전투 중 `AddEffectTile` 호출. 진입점 이미 대응 — 트리거/UX 만 신규.
- **효과 타일 제거/만료 + revocation** [S] · `RemoveEffectTile(cell)` + stackId=2 슬롯 식별 제거(M1 덕에 가능).
- **유닛 제거/재배치 시 revocation** [S] · 방어 유닛 제거 기능 생기면 영구 duration 재검토.
- **셀당 다중 효과 스택** [S] · 현재 셀당 1개.
- **효과 타일 tooltip/드래그 미리보기 UI** [S].
- **EffectTileSetConfig SO** [S] · BattleBridge 의 effectTiles[]/count 필드를 config SO 로 이관(리뷰 m6, 선택).

### 아이데이션 이관 (2026-07-02, 미채택분)

- **적 경로 타일** [M] · Walk 셀 늪(감속)/가시(도트) — `path-zone-hazards` Zone hazard 를 맵 빌드 시 저작 배치. 효과 타일의 공격면 쌍.
- **사거리 타일** [M] · `RangeMul` StatKind 신규(enum+aggregate+AttackSystem 사거리 소비 1곳). 배치 퍼즐 직결.
- **속성 부여 타일** [M] · 타일 위 유닛 공격이 Poison/Fire 스택 부여 — PoisonCaster 의 attack→stack 파이프라인 재사용, 배치 시 bridge 가 AttackState 스택 파라미터 세팅.
- **환급 타일** [S] · 배치 시 `CostRuntime.AddCost` 보너스.
- **직군 공명 타일** [S] · DefenderClass 조건부 배율.
- **미스터리 타일** [S] · 배치 순간 seed 롤 효과 확정.
- **시너지 증폭 타일** [S] · `EnqueueSynergyMul` 개입.
- **킬 보상 타일** [M] · EnemyKilledEvents killer 귀속 필요.
- (대형) 승급/연쇄/웨이브 반응/어그로 타일 [L].
