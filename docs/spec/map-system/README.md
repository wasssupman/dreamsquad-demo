# Map System Spec (Phase 10)

**작성일**: 2026-04-21
**연결 문서**: `docs/plans/2026-04-21-map-system-design.md`, `docs/phase10-prep.md`
**목표**: Phase 9 flow field 엔진 위에 타일 4종 데이터 모델 + seed 기반 procedural 생성 + 테마 오브젝트 배치 시스템을 구축한다.

## 구현 문서 목록

### Phase 10A — Data 모델 + Infra

| 번호 | 작업 구분 | 문서 | 목적 |
|---|---|---|---|
| 0 | Enum | `0_tile_types.md` | `MapTileType` 4종 (Walk/Place/Env/Deco) |
| 1 | SO | `1_map_generation_settings.md` | gridSize + seed 설정 SO |
| 2 | Struct | `2_generated_map_struct.md` | runtime GeneratedMap + Dispose |
| 3 | Builder | `3_map_builder_fixture.md` | PrototypeMap → GeneratedMap 변환 |
| 4 | Integration | `4_battlebridge_integration.md` | BattleBridge owner + Initialize 주입 |
| 5 | FlowField | `5_flow_field_walk_only.md` | walkmask = MapTileType.Walk |
| 6 | Placement | `6_placement_input.md` | 배치 판정 = MapTileType.Place |
| 7 | MapView | `7_mapview_4tile_materials.md` | 4 타입별 cube Material |
| 8 | Migration | `8_prototype_map_migration.md` | byte array 재해석 규칙 |
| 9 | Connectivity | `9_multispawn_connectivity.md` | BFS 검증 + fallback 직선 맵 |
| 10 | Tests | `10_editmode_tests.md` | Phase 10A EditMode 테스트 |

### Phase 10B — Procedural + 테마

| 번호 | 작업 구분 | 문서 | 목적 |
|---|---|---|---|
| 11 | Generator | `11_procedural_generator.md` | `Generate(seed, gridSize, theme, generatorVersion)` API |
| 12 | Algorithm | `12_path_carve_algorithm.md` | 각 spawn 독립 Manhattan walk + BFS |
| 13 | Theme | `13_map_theme_data.md` | MapThemeData SO (2필드) |
| 14 | Placer | `14_obstacle_placer.md` | 단일 셀 배치, Walk/Place 비침범 |
| 15 | Deck | `15_attackdeck_spawnindex.md` | SpawnEntry.spawnIndex migration |
| 16 | Logging | `16_seed_logging.md` | seed + generatorVersion 로그 |
| 17 | Manual | `17_manual_map_input.md` | 맵툴 예약 data shape |
| 18 | Regression | `18_playmode_regression.md` | PlayMode 3회 비교 + fallback |
| 19 | Integration | `19_battlebridge_10b_integration.md` | BattleBridge Phase 10B 필드 + 최종 orchestration (Codex C-8 대응) |

## 공통 원칙

- 타일 타입은 mutually exclusive (한 타일 = 한 역할).
- GeneratedMap 은 runtime-only, 디스크 저장 없음. 같은 seed 로 재생성하면 동일.
- Walk 타일만 flow field walkable, Place 타일만 defender 배치 허용.
- Env/Deco 는 Phase 10 에서 시각 구분만 — 효과 동작은 Phase 11.
- RNG: `Unity.Mathematics.Random(seed)` (Burst-safe). `UnityEngine.Random` 금지.
- Procedural 실패 시 fallback 하드코딩 직선 맵 (freeze 방지).
- Phase 10 맵 크기는 X×Y 가변 (기본 20×20). 모든 로직이 gridSize 파라미터 사용 (하드코딩 제거).
