# Phase 9 — Flow Field 길찾기 교체

> Phase 9 는 Phase 0~8 의 waypoint 기반 길찾기 (`DynamicBuffer<PathWaypoint>` + `currentWaypointIndex`) 를 flow field 기반으로 교체한 단계다. Portal / Tornado / 향후 넉백 등 위치 변위 후에도 적이 현재 cell 의 flow lookup 만으로 goal 방향으로 자율 복귀한다. 맵 시스템 개편 2단계 중 1단계 (step1 = 길찾기 엔진 구축, step2 = procedural 맵 생성 및 TileType 재분류는 Phase 10).

---

## 1. 목표

- Waypoint 버퍼 + waypoint index 구조를 제거하고 single goal flow field 로 교체한다.
- Portal exit / Tornado field 해제 후 적이 현재 cell 기준 flow 조회로 자율 복귀한다.
- `MapData` 에 `goalCell` + `spawnCells[]` 필드를 도입해 single-goal + single-spawn 구조를 확정한다.
- `ResolveExitWaypointIndex` / `PortalLink.exitWaypointIndex` 를 삭제해 텔레포트 역주행 이슈를 구조적으로 해소한다.
- tileSize 단일 소스화를 통해 MapView / PlacementInput / VFX 월드 좌표 계산의 출처를 `BattleBridge` 로 통일한다.

### 비목표

- Procedural 맵 생성 파이프라인 및 TileType enum 재분류 (Empty/Walkable/Placeable/Blocked).
- Multi-goal / multi-spawn / 다중 레인 / 테마 / multi-cell obstacle.
- NavMesh / Tile A* / Hybrid 대안 탐색.
- Entities 6.x 전환 및 Unity Editor 6000.4+ 업그레이드 (Phase 9→10 사이 재논의).
- 공격 범위 표시 UI, Meteor procedural Quad prefab 화.

---

## 2. 확정 결정

| 항목 | 구현 결과 |
|---|---|
| Scope | 축 A only — flow field 길찾기 교체 (고정 PrototypeMap 위에서) |
| Goal 모델 | single goal + single spawn (multi-spawn 은 Phase 10) |
| Flow field 재계산 | Static per-play (판 시작 시 BFS 1회) |
| TileType enum | 기존 `Buildable` / `Path` / `Obstacle` 유지 |
| Walkable 판정 | `TileType != Obstacle` (Buildable + Path 모두 walkable) |
| `MapData.paths` 필드 | `[Obsolete]` 표기 + 필드 유지. Phase 10 asset migration 때 삭제 |
| 패키지 버전 | `com.unity.entities 1.4.5` 유지 (6.x 전환 Phase 9→10 사이 재논의) |
| tileSize 출처 | `BattleBridge.tileSize` 가 단일 소스. MapView / PlacementInput 에 Initialize 주입 |

---

## 3. 신규 / 수정 컴포넌트

### 3.1 `FlowFieldSingleton` (Effects)

- `IComponentData` + `IDisposable`.
- 필드: `NativeArray<int2> flow` (Persistent), `int2 size`, `int2 goalCell`, `float tileSize`, `Vector3 origin`.
- 쓰기 소유: Effects 맥락 (`BattleBridge.BuildFlowField` / `TeardownFlowField`).
- 읽기: Movement 맥락 (`MovementSystem`).

### 3.2 `GridMath` (Movement static helper)

- `WorldToCell(float3 world, Vector3 origin, float tileSize, int2 size) → int2`.
- `CellToWorldCenter(int2 cell, Vector3 origin, float tileSize) → float3`.
- 경계 clamp 포함. EditMode 단위 테스트 6종.

### 3.3 `FlowFieldBuilder` (Effects static)

- `Build(MapData map, Allocator allocator) → NativeArray<int2>` — goal cell 에서 시작하는 BFS.
- walkable = `TileType != Obstacle`. 각 cell 에 대해 "다음 cell 방향 (int2 step)" 저장. 도달 불가 cell 은 `int2.zero`.
- EditMode 테스트 3종 (single path, blocked path, disconnected region) — `try/finally` dispose.

### 3.4 `MapData` 확장

- 신규 필드: `int2 goalCell`, `int2[] spawnCells` (Phase 9 는 index 0 만 사용).
- `paths` 필드는 `[System.Obsolete("Unused since Phase 9. Removed in Phase 10 asset migration.", error: false)]` 표기.

### 3.5 `PathFollowState` 축소 + `PathWaypoint` 삭제

- `PathFollowState.currentWaypointIndex` 제거. 현재 위치 (`float3 position`) + `reachedGoal` 플래그만 유지.
- `PathWaypoint.cs` 파일 완전 삭제 (`DynamicBuffer<PathWaypoint>` 제거).

### 3.6 `MovementSystem` 재작성

- 매 프레임 `FlowFieldSingleton` 을 읽어 각 attacker 의 현재 cell 의 flow step 을 조회.
- step 방향으로 `normalizesafe` 이동. Tornado field pull 은 기존 로직 유지.
- goal cell 도달 시 `PastGoalTag` 부여 + `GoalReachedEventsSingleton` enqueue.
- EditMode 테스트 3개 migration + 신규 통합 테스트 1개.

---

## 4. BattleBridge 역할

- `BuildFlowField(MapData)`: 판 시작 시 호출. 멱등성 보장 — 기존 singleton 이 있으면 `TeardownFlowField` 선행 후 재생성. `Allocator.Persistent` 할당은 try/catch 로 보호.
- `TeardownFlowField()`: Restart / teardown 시 호출. `NativeArray` dispose + singleton entity 제거.
- `GridToWorldCenter(int2 cell)`: VFX 4개 사이트 (Meteor / Portal entry / Portal exit / placement) 의 월드 좌표 계산을 단일 helper 로 통일.
- `MapView.Initialize(tileSize, origin)` / `PlacementInput.Initialize(tileSize, origin)`: Scene 자동 와이어링 경로에서 tileSize 주입.
- `PortalLink` 생성 시 `exitWaypointIndex` 필드 제거 (구조적으로 필요 없음).

---

## 5. 로그 / 디버그

- `BattleBridge.BuildFlowField`: "Phase9 FlowField built: size={w}x{h}, goal={gx,gy}, reachable={n}" 1회 로그.
- `MapView.BuildPathLines` 제거 — waypoint LineRenderer 시각화 삭제.
- `FlowFieldBuilder` 는 array length assert + 경로 도달 불가 시 `Debug.LogWarning` 1회.

---

## 6. 작업 결과

- [x] P9-01 — `MapData.goalCell` / `spawnCells` 필드 + PrototypeMap single-goal/single-spawn 편집 + `paths` [Obsolete].
- [x] P9-02 — `GridMath.WorldToCell` / `CellToWorldCenter` + EditMode 테스트 ×6.
- [x] P9-03 data — `FlowFieldSingleton` `IComponentData` + `IDisposable`.
- [x] P9-03 wiring — `BattleBridge.BuildFlowField` / `TeardownFlowField` 수명 관리 (idempotent + try/catch).
- [x] P9-04 — `FlowFieldBuilder` BFS 순수 함수 + EditMode 테스트 ×3 (try/finally dispose).
- [x] P9-05A — `MovementSystem` flow-field 재작성 + EditMode 테스트 3개 migration + 신규 통합 테스트.
- [x] P9-05B — `PathFollowState` 축소 + `BattleBridge` 적 스폰 → `SpawnCells[0]` migration.
- [x] P9-06 — `PortalLink.exitWaypointIndex` 제거 + `ResolveExitWaypointIndex` 삭제 + 파급 4소스 migration.
- [x] P9-07 — `MapView.BuildPathLines` 제거.
- [x] P9-08 — `BattleBridge.GridToWorldCenter` helper + VFX 4사이트 통일.
- [x] P9-09 — `PathWaypoint.cs` 파일 삭제.
- [x] P9-10 — tileSize 단일 소스화 (BattleBridge → MapView/PlacementInput 주입) + Scene 자동 와이어링.
- [ ] P9-11 — 기준선 Play 녹화 (사용자 수작업 skip 결정, 2026-04-20).
- [ ] P9-12 — PlayMode 회귀 확인 (Portal 동선 / Tornado 자율 복귀 / Goal 도달, 사용자 작업 대기).

---

## 7. 종료 조건

- EditMode 52/52 pass (기존 + 신규 테스트).
- Unity 컴파일 0 errors / 0 warnings.
- Flow field 가 판 시작 시 1회 BFS 로 초기화.
- Portal 텔레포트 / Tornado field 해제 후 적이 flow lookup 으로 자율 복귀.
- Goal cell 도달 시 `PastGoalTag` 부여 + `GoalReachedEventsSingleton` enqueue.
- P9-12 사용자 Play 회귀 통과 시 Phase 9 검증 완료.

---

## 8. TRD 금지 패턴 준수

- `FlowFieldSingleton` 쓰기 = Effects (BattleBridge), 읽기 = Movement (MovementSystem). 맥락 경계 준수.
- 하드코딩 수치 없음 — goalCell / spawnCells 는 `MapData` SO 주도.
- 상속 계층 없음 — `struct` + `static class` 구성.
- 인터페이스 추상화 없음 (구현체 1개).
- GameManager 외 추가 singleton MonoBehaviour 도입 없음.
- Component 쓰기는 소유 맥락 내에서만 수행.

---

## 9. Codex 2차 리뷰 반영 (품질 기록)

- **CRITICAL #1** — `BuildFlowField` 멱등성: `TeardownFlowField` 선행 + `Allocator.Persistent` try/catch 로 Restart 재진입 시 leak 방지.
- **HIGH #2 / #3** — 기존 MovementSystem EditMode 테스트 3개 migration + 통합 테스트 신규 추가.
- **HIGH #4** — Task 8 을 8A / 8B / 8C 로 분할해 **매 commit 시점 컴파일 + 기존 테스트 전부 통과** 를 보장.
- **HIGH #5** — `FlowFieldBuilderTests` 에서 `try/finally` dispose 강제.
- **추가 (execution-time)** — `FlowFieldBuilder` array length assert, `MovementSystem` `normalizesafe`, `MapView` / `PlacementInput` loud null guard (Initialize 누락 시 명시적 Error).

---

## 10. Phase 10 로 이관

- Procedural 맵 생성 (path carve + theme + multi-cell obstacle).
- `TileType` enum 재분류 (Empty / Walkable / Placeable / Blocked).
- Multi-goal / multi-spawn / 다중 레인.
- `AttackDeck.SpawnEntry.pathId` → `spawnTileIndex` migration.
- `GeneratedMap` 주입 경로 (`BattleBridge` 단일 owner, `BattleMapSpec` adapter).
- `MapData.paths` `[Obsolete]` 필드 완전 삭제 (asset migration 과 동반).
- Unity Editor 6000.4+ 업그레이드 + Entities 6.x 전환.

상세 — `docs/phase10-prep.md`.

---

## 11. 잔여 / 사용자 확인 대기

- P9-12 PlayMode 회귀 (Portal 동선 / Tornado 복귀 / Goal 도달) — `docs/residual-issues.md` 추적.
- P9-11 기준선 녹화는 사용자 결정으로 skip.

---

**문서 버전**: v1.0 (구현 스펙 통합)
**상태**: 구현 완료. Unity 컴파일 0 에러 / EditMode 52/52 pass. P9-12 사용자 Play 회귀 대기.
**작성**: 2026-04-20
