# PrototypeMap Migration Rules

**작업 구분**: Phase 10A

## 목적

기존 `PrototypeMap.asset` 의 `TileType` byte array 를 Phase 10 `MapTileType` 으로 재해석하는 매핑 규칙을 명시. Phase 10A 는 코드 매핑만 (asset YAML 은 건드리지 않음), Phase 10B 이후 (필요 시) asset 재작성.

## 변경 대상

- `Assets/_Project/Scripts/Data/BattleMapBuilder.cs` — 이미 `3_map_builder_fixture.md` 에 매핑 구현 포함
- 주석 / 문서화만 이 task 의 scope

## 매핑 규칙

| Phase 9 `TileType` | Phase 10 `MapTileType` | 근거 |
|---|---|---|
| `Buildable (0)` | `Place (1)` | defender 배치 가능 영역 — 의미 동일 |
| `Path (1)` | `Walk (0)` | 적 이동 영역 — 의미 동일 |
| `Obstacle (2)` | `Deco (3)` | Phase 9 에서 적 차단용 (walkable=Path-only). Phase 10 에선 배경 오브젝트 (시각 차단, flow 비walkable) — 역할 계승 |
| 대응 없음 | `Env (2)` | Phase 10 신설. PrototypeMap 에는 Env 타일 없음 |

## PrototypeMap 의 환경 타일 부재

현재 `PrototypeMap.asset` 의 byte array 에는 0/1/2 값만 있음. `MapTileType.Env` 타일은 procedural 생성 (Phase 10B) 또는 맵툴 수동 지정에서 처음 등장. Phase 10A 검증은 Walk/Place/Deco 3 타입만 활성.

## MapData legacy asset 처리 방침

- Phase 10A 동안 `MapData.cs` 및 `PrototypeMap.asset` **유지**. fixture 용도
- `MapData.paths` 는 Phase 9 에서 `[Obsolete]` 표기됨. Phase 10A 에서도 건드리지 않음 (Phase 10B migration task 15 에서 삭제 판단)
- `MapData.goalCell`, `MapData.spawnCells` 는 `BattleMapBuilder.BuildFromFixture` 에서 읽어 `GeneratedMap.goal`, `GeneratedMap.spawns` 로 변환
- Phase 10B procedural 생성 완전 도입 후 `MapData` / `PrototypeMap.asset` 은 **테스트 fixture** 로 용도 축소 또는 삭제 (cleanup 시점은 Phase 10B 종료 시 판단)

## Phase 10B multi-spawn 전환 시 PrototypeMap 수정

현재 `PrototypeMap.spawnCells` 는 1개. Phase 10A task 9 (multi-spawn 연결성 테스트) 에서 2+ spawn 이 필요하면 `PrototypeMap.asset` 의 `spawnCells` 를 2~3개로 확장 (예: Path B 의 시작점 (0,2) 를 spawn 으로 추가). 이 수정은 task 9 에서 실제 진행.

## 완료 기준

- `BattleMapBuilder.MapTile(TileType)` 메서드에 위 매핑 주석으로 명시.
- EditMode 테스트: PrototypeMap 의 `tiles[]` byte array 로부터 `BuildFromFixture` 결과가 Walk 셀 수 / Place 셀 수 / Deco 셀 수가 기존 Path/Buildable/Obstacle 분포와 일치.
- 문서 `docs/spec/map-system/8_prototype_map_migration.md` (이 파일) 존재.
