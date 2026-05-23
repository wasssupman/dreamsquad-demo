# Map Grid Generation Spec

**작성일**: 2026-05-22
**상태**: 완료 2026-05-23. 인계 요약은 `9_handoff_summary.md`.
**대체 대상**: `docs/spec/map-system/` 의 11~14 (`ProceduralMapGenerator`/`PathCarver`/`ObstaclePlacer`) + 9 (`MapConnectivity` 의 generator-side 사용). 런타임 컨테이너(`GeneratedMap` struct, `MapTileType` enum)와 `BattleBridge` 의 소비 경로는 유지하고, **생성 알고리즘과 authoring SO 스키마만 새로 정의**한다.

## 목표

N×M 정수 그리드 위에서, 4분면 코너 zone 의 가변 N(2~4) 스폰 → 중앙 1개 도착으로 수렴하는 **다단계 합류 직선 경로** 를 seed 기반으로 절차적 생성한다. 곡선·대각·헥스·복셀은 본 spec 범위 밖. 카메라 고정 + 캐릭터 디테일을 볼 수 있는 화각이 전제다.

검증 질문: "동일 `(seed, W, H, generatorVersion)` 로 동일 맵이 나오고, 그 맵 데이터만 보고 그리드/경로/배치 가능 영역/명당 구조를 재현 렌더할 수 있는가? 그리고 그 맵이 시각·전술 양쪽에서 단조롭지 않은가?"

## 공통 원칙 (Feature-wide 계약)

- **그리드 크기**: 토너먼트 단위로 `(W, H)` 가 정해진다. 허용 프리셋: `30×15`, `20×20`, `10×20`. 가로 ≤ 30, 세로 ≤ 15 (큰 축 기준). 프리셋은 `GenerationSettings` SO 에 보관.
- **좌표·경로 제약**: 셀 = 정수 좌표. 경로는 폭 1, 축 정렬(가로/세로) 직선 세그먼트의 연속. 꺾임은 90°만. 셀당 깊이 1 (= 2×2 path block 금지).
- **도착·스폰 배치 (unit 11)**: 맵을 **6 section** (W≥H → 3×2, W<H → 2×3) 으로 분할. 1 section 을 seed 로 골라 goal, 나머지 5 중 N(2~4) 개를 seed 셔플로 골라 spawn. 각 section 내부는 section anchor (corner section = map corner, edge section = outer edge midpoint) 기준 Chebyshev ≤ `cornerZoneRadius` zone 에서 uniform random. **spawn↔goal Manhattan 거리** ≥ `EffectiveSpawnToGoalMinManhattan(grid)`, spawn↔spawn ≥ 3. N=1 차단.
- **합류 트리**: 사전 설계하지 않는다. 각 스폰을 "기존 path 셀(분기차수 ≤ 2 인 비 spawn/goal 셀)" 에 attach 하는 incremental 방식으로 그린다. 합류 셀(degree ≥ 3)은 **emergent**. 결과적으로 `4→2→1` / `4→3→2→1` / 부분 비대칭 트리가 시드에 따라 자연 발생.
- **최소 지류 길이/꺾임**: 각 spawn→goal 경로의 **path 위 셀 수** ≥ `EffectiveMinBranchCellCount(grid)` (default 8, `max(SO, min(W,H)/2)` 로 scale), **꺾임 수** ≥ `EffectiveMinBranchTurnCount(grid)` (default 3, `max(SO, min(W,H)/3)` 로 scale). 큰 맵일수록 더 구불구불한 path 를 요구. ※ Manhattan 은 placement 단계, branch length/turn 은 actual path 단계.
- **데이터 스키마 단일성**: 절차적 생성 결과와 손수 작성한 맵 데이터가 **완전히 동일한 SO 스키마** 를 가진다. 런타임은 SO → `GeneratedMap` (struct + NativeArray) 으로 빌드해서 소비한다. 생성기 의존 금지.
- **셀 타입(런타임)**: 기존 `MapTileType` {Walk, Place, Env, Deco} 유지. authoring SO 는 동일 enum 을 그대로 사용. **이 spec 의 절차적 생성기는 Walk + Place 2종만 채운다** (모든 비 path 셀 = Place). Env/Deco 베이크는 follow-up theming spec.
- **셀 메타데이터**: `mergeDegree` (path 셀이 만나는 인접 path 수, path 외 셀은 0), `chokepoint` 플래그 (degree ≥ 3 셀), `propLayerId` (장식 풀 인덱스, **본 spec 에선 항상 0 — schema slot 만 확보, 후속 theming spec 이 채운다**). `GeneratedMap` 에 평행 NativeArray 로 확장.
- **초기 상태**: 첫 생성 시 방해물 없음. 런타임 방해물 스폰/HP 파괴/경로 재계산은 본 spec 범위 밖 (`destructible-blocking-hazards` 와 접점).
- **재현성**: `(seed, allowedPresets, generatorVersion)` 튜플 → 같은 결과. (`seed` 가 `allowedPresets % length` 로 그리드를 선택하므로 `(W, H)` 는 결과의 일부지 입력이 아니다.) Validator 실패 시 outer attempt 증가 후 `Unity.Mathematics.Random.CreateFromIndex(HashSeed(seed, attempt, generatorVersion))` 로 재초기화 (collision-safe). outer 기본 600, inner 기본 160 — 둘 다 SO 노출.
- **`generatorVersion` bump 정책**: 알고리즘 또는 SO default 가 바뀔 때 ↑. `MapDocument.authoringSeed ≥ 0` 인 캐시는 기존 generatorVersion 으로 재현 가능해야 하며, version 이 다르면 캐시 미스로 처리.
- **Burst 정책**: 본 spec 의 모든 새 클래스/메서드는 **Managed**. Burst-compile 은 본 spec 안정화 후 별도 spec.

## 작업 단위

| # | 작업 구분 | 문서 | 목적 |
|---|---|---|---|
| 0 | Schema | `0_map_data_schema.md` | 새 `MapDocument` SO (authoring) + `GeneratedMap` 메타 확장. authoring ↔ runtime 라운드트립. |
| 1 | Settings | `1_generation_settings.md` | `GenerationSettings` SO: 그리드 프리셋, 정책 enum, min 길이/꺾임, attempt 상한, generatorVersion. |
| 2 | Placement | `2_goal_and_spawn_placement.md` | Goal (중앙 ±2) + Spawn (4분면 corner zone + distance 룰) 시드 결정. |
| 3 | Builder | `3_incremental_path_builder.md` | L/U 후보 라우터 + attach-to-existing-path. isValidRoute 의 "attach 점에서만 접촉" 룰. 2×2 block 회피. |
| 4 | Validator | `4_generation_validator.md` | 연결성 / degree / 2×2 / 지류 min 길이·꺾임 / reject & re-seed 루프. |
| 5 | Bake | `5_cell_classification.md` | path 외 셀을 Place/Env/Deco 로 베이크 + `mergeDegree`/`chokepoint`/`propLayerId` 채우기. |
| 6 | Integrate | `6_battlebridge_handoff.md` | 새 `MapDocument` → `GeneratedMap` builder, `BattleBridge` 의 fixture 경로와 어댑터. |
| 7 | Tooling | `7_editor_debug_view.md` | seed 고정 재현 + 셀 메타 시각화 + attempts 카운터. |
| 8 | Tests | `8_editmode_tests.md` | 결정성/제약/스키마 라운드트립, 양극단 그리드 케이스. |
| 9 | Handoff | `9_handoff_summary.md` | 인계 요약 (구현 완료 시점에 채움). |

## 알고리즘 선택

**채택**: "스폰 → 기존 path attach" incremental 빌더. 참고 impl 은 [`_reference_algorithm.md`](./_reference_algorithm.md) 의 TS 코드를 C#/Unity 로 포팅 — 단위 3 implementer 가 직접 참조.

핵심 골격 (단위 3 에서 상세):
1. `pickGoal()` — 중앙 체비셰프 ≤ 2 안에서 seed 로 1셀.
2. `pickSpawns()` — 4분면 corner zone × 활성 분면 N 개에서 distance 룰 만족하는 셀 N 개.
3. 첫 번째 스폰 → goal 직접 라우팅 (L/U 후보 셔플 시도).
4. 나머지 스폰 → 기존 path 의 attach 후보(degree ≤ 2) 중 하나에 라우팅.
5. 각 라우팅은 `isValidRoute` (new 셀은 attach 점에서만 path 와 접촉) 통과 시 채택.
6. 모든 스폰 라우팅 성공 → validator → 통과 시 베이크, 실패 시 outer attempt 증가.

이 방식은 합류 셀을 사전 결정하지 않기 때문에 토폴로지 다양성이 자연스럽게 확보되며, "직선 + 90°" 제약을 후보 생성 단계에서 만족시킨다.

## 후속 후보 (본 spec 범위 밖)

- Edge goal 모드 / mixed goal 모드 — 디펜스 흐름 자체가 달라지는 결정. 본 spec 안정화 후 별도 spec.
- 핸드크래프트 맵 에디터 (동일 SO 로 손수 입력) — 본 spec 의 스키마가 확정된 뒤 별도 spec.
- 헥스/불규칙/복셀 등 비격자 토폴로지 실험 — 별도 spec.
- 런타임 방해물 스폰 시 경로 재계산 정책 — `destructible-blocking-hazards`, `path-zone-hazards` 접점에서 별도 다룸.
- 시각 테마/prop 다양화 — `seasonal-map-backdrop`, `background-props` 접점.
- 카메라 화각/타일 월드 스케일 — 별도 결정.
- 기존 `map-system/11~14` 의 `ProceduralMapGenerator`/`PathCarver`/`ObstaclePlacer` 코드 제거 — 본 spec 단위 6 안정화 후 별도 cleanup spec.
