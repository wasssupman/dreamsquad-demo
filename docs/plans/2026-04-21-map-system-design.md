# Map System Design (Phase 10A + 10B)

**작성일**: 2026-04-21
**스코프**: Phase 10 맵 시스템 재설계 전체. Phase 9 flow field 엔진 위에 얹는 데이터 모델 교체 + procedural 생성 + 테마 레이어.
**구현 스펙**: `docs/spec/map-system/` (분산 구조, 19개 파일).

## 목표

사용자 6 bullet 요구사항 구현:
1. 매 판 맵은 seed 기반 랜덤 procedural 생성
2. 맵 크기 X×Y 유동, 기본 20×20
3. Multi-line + single-goal 디폴트
4. 사용자 수동 이동타일 지정 OR 랜덤 생성
5. 이동타일 기반 맵 테마 디자인 오브젝트 배치
6. 타일 4종: 이동 / 방어배치 / 환경 / 배경 오브젝트

## 아키텍처 요약

```
BattleBridge (MonoBehaviour owner)
  ├── MapGenerationSettings SO — gridWidth/Height/defaultSeed (기본 20×20)
  ├── GeneratedMap runtime struct (Dispose)
  │     - NativeArray<MapTileType> tiles (Walk/Place/Env/Deco)
  │     - int2[] spawns, int2 goal
  │     - int seed, int generatorVersion
  │
  ├── ProceduralMapGenerator.Generate(seed, gridSize, theme, generatorVersion) → GeneratedMap
  │     - path carve: 각 spawn 별 독립 randomized Manhattan walk + BFS validation
  │     - fallback: 3회 실패 시 하드코딩 직선 맵
  │
  ├── ManualMapInput struct (맵툴 예약 data shape)
  │     - BattleBridge.BuildFromManual(input) → GeneratedMap
  │
  ├── MapView.Initialize(GeneratedMap, tileSize) + InstantiateObstacles(GeneratedMap, theme) — 4 타일 cube + obstacle prefab
  ├── PlacementInput.Initialize(GeneratedMap) — MapTileType.Place 판정
  └── FlowFieldBuilder walkmask = MapTileType == Walk
```

## 주요 결정 (브레인스토밍 Q 결과)

| Q | 결정 |
|---|---|
| Q-B | ECS 주입 없음. FlowFieldSingleton 만 유지, GeneratedMap 은 MonoBehaviour 보유 |
| Q-I | Unity.Mathematics.Random (Burst-safe, Xorshift128) |
| Q-6 | Path carve = 각 spawn 독립 randomized Manhattan walk + BFS post-validation |
| Q-C | AttackDeck.SpawnEntry.spawnIndex (int) 으로 migration |
| Q-F | Q-C 에 종속 — deck 에 명시된 spawnIndex 로 분배 |
| Q-K | ManualMapInput struct (gridSize/walkCells/placeCells/spawns/goal) |

## Phase 분할

- **Phase 10A** (spec 파일 0~10): data 모델 + infra. PrototypeMap fixture 위에서 새 4-enum + GeneratedMap + multi-spawn 검증
- **Phase 10B** (spec 파일 11~18): procedural 생성 + 테마 오브젝트 배치 + AttackDeck migration

## 구현 상세

전부 `docs/spec/map-system/` 참조:

| 번호 | 작업 단위 | Phase |
|---|---|---|
| README | 개요 | — |
| 0 | MapTileType enum | 10A |
| 1 | MapGenerationSettings SO | 10A |
| 2 | GeneratedMap runtime struct | 10A |
| 3 | BattleMapBuilder (fixture → struct) | 10A |
| 4 | BattleBridge 통합 | 10A |
| 5 | FlowFieldBuilder Walk-only | 10A |
| 6 | PlacementInput Place-only | 10A |
| 7 | MapView 4-tile Material | 10A |
| 8 | PrototypeMap migration 규칙 | 10A |
| 9 | Multi-spawn BFS 연결성 + fallback | 10A |
| 10 | EditMode tests (10A) | 10A |
| 11 | ProceduralMapGenerator | 10B |
| 12 | Path carve algorithm v1 | 10B |
| 13 | MapThemeData SO | 10B |
| 14 | ObstaclePlacer (단일 셀) | 10B |
| 15 | AttackDeck.spawnIndex migration | 10B |
| 16 | Seed/version logging | 10B |
| 17 | ManualMapInput struct | 10B |
| 18 | PlayMode regression | 10B |
| 19 | BattleBridge Phase 10B Integration | 10B |

## Phase 11+ 이관 확정

환경효과 실제 동작 / multi-cell obstacle 시스템 / 테마 확장 자동화 / defender 경제 리밸런스 / 맵툴 실제 구현 / multi-goal 지원.

## 참조

- Phase 9 종료 스펙: `docs/PHASE9.md`
- 이관 스펙: `docs/phase10-prep.md`
- 잔여 이슈: `docs/residual-issues.md`
- TRD 제약: `docs/TRD.md`
