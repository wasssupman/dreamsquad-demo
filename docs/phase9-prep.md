# Phase 9 착수 체크리스트 — Flow Field 길찾기 교체 (축 A 한정)

> 2026-04-19 브레인스토밍 + Codex 2차 리뷰 결과 원 Phase 9 scope 를 축소. Procedural 맵 생성 / 테마 / multi-cell obstacle / TileType enum 재분류는 **Phase 10 으로 전량 이관** — `docs/phase10-prep.md` 참조. 본 Phase 9 는 flow field 길찾기 교체 **only**.
>
> 설계 상세: `docs/plans/2026-04-19-phase9-flow-field-design.md`

---

## 1. Phase 9 주제 — Flow Field on Fixed PrototypeMap

### 왜

- 현재 Waypoint 기반 (`DynamicBuffer<PathWaypoint>` + `currentWaypointIndex`) 구조는 **변위 발생 시 자율 복귀 불가**
- Portal / Tornado / 향후 넉백 등 위치 임의 변경 스킬이 구조적으로 불편
- 증상 3종:
  1. 포탈 동선 이상 (`ResolveExitWaypointIndex` 의 closest+1 fallback, `map.Paths[0]` 만 스캔)
  2. Tornado 해제 후 기계적 직선 복귀 (waypoint index 고정, 위치만 변동)
  3. 다중 레인 확장 시 waypoint 수작업 복제 부담

Flow field 채택으로 **적 현재 cell → field lookup** 구조에서 위 3종을 일괄 해결.

### 기술 방향

Flow Field (이전 phase9-prep 에서 이미 확정). 대안 비교 (NavMesh, Tile A*, Hybrid) 는 phase9-flow-field-design.md §1 에 이관.

### 범위

- 고정된 `PrototypeMap.asset` 위에서 동작 (procedural 생성 없음)
- `TileType` enum 그대로 유지 (`Buildable` / `Path` / `Obstacle`) — 재분류 없음
- `MapData` 에 `goalCell` + `spawnCells[]` 필드 추가
- PrototypeMap 을 **single-goal + single-spawn** 으로 단순화 (Path B 는 Phase 10 multi-spawn 에서 부활)
- 현재 `com.unity.entities 1.4.5` 유지 (§5 패키지 버전 미결)

### 비범위 (Phase 10 이관)

- Procedural 맵 생성 파이프라인
- 테마 + multi-cell obstacle 자산 시스템
- TileType enum 재분류 (Empty/Walkable/Placeable/Blocked)
- 다중 레인 / multi-goal / multi-spawn
- 환경효과 (화산, 바람) — Phase 11+

전부 `docs/phase10-prep.md` 에 스펙 이관 완료.

---

## 2. 핵심 해결 이슈

### 이슈 A — 포탈 동선 이상

- **원인**: `BattleBridge.ResolveExitWaypointIndex` (cs:581~602) 의 closest+1 fallback, 그리고 `map.Paths[0]` 만 스캔 → Path B 적에게 Path A index 주입
- **Phase 9 해결**: flow field 도입 → teleport 직후 현재 cell 의 flow 조회로 자동 복귀. `ResolveExitWaypointIndex` 메서드 + `PortalLink.exitWaypointIndex` 필드 삭제

### 이슈 B — 변위 후 자율 복귀 부재

- **원인**: `PathFollowState.currentWaypointIndex` 가 Tornado 동안 고정, 위치만 변동
- **Phase 9 해결**: flow field 로 현재 cell 기준 다음 방향 계산. `currentWaypointIndex` 개념 자체 제거

### 이슈 C — PortalLink 제거가 파급되는 소스 4개 (Codex M-10)

- `Assets/_Project/Scripts/Battle/Effects/PortalLink.cs` — 필드 제거
- `Assets/_Project/Scripts/Battle/Effects/EffectSpawner.cs` — `SpawnPortal` 시그니처 수정
- `Assets/_Project/Scripts/Battle/Movement/MovementSystem.cs` — index 덮어쓰기 코드 제거
- `Assets/_Project/Scripts/Battle/Effects/EffectTickSystem.cs` — portal lifetime 영향 없음 검증
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — `ResolveExitWaypointIndex` 호출/메서드 삭제

---

## 3. 작업 분해

상세 — `docs/plans/2026-04-19-phase9-flow-field-design.md` §6. 요약:

- P9-01 ~ P9-04 — 인프라 (MapData 필드, GridMath, FlowFieldSingleton, FlowFieldBuilder)
- P9-05 ~ P9-06 — MovementSystem 재작성 + PortalLink migration
- P9-07 ~ P9-10 — 주변 정리 (MapView/tileSize/GridToWorldCenter/Goal 판정)
- P9-11 ~ P9-12 — 회귀 녹화 + PlayMode 검증

---

## 4. 기준선 Play 녹화 (Phase 9 코드 변경 전)

회귀 판정용 **기준선 3 케이스** 녹화:

1. Portal: exit 타일이 경로 위 / 경로 옆 / 경로 밖 — 각 동선
2. Tornado: 해제 후 적 이동 방향
3. 단일 유닛 start → goal 평상시 진행

Phase 9 완료 후 동일 시나리오 재녹화하여 비교.

---

## 5. 착수 전 결정 (확정)

### 5.1 Unity Editor / Entities 패키지 버전 — **Phase 9→10 사이 재논의 (연기)**

결정: **현재 환경 (Unity `6000.3.5f2` + `com.unity.entities 1.4.5`) 에서 Phase 9 를 진행**. Unity Editor 6000.4+ 업그레이드 + Entities 6.x 전환은 **Phase 9 완료 후 Phase 10 착수 전 재논의** 한다.

- 현 상태: Unity `6000.3.5f2` + `com.unity.entities 1.4.5`
- CLAUDE.md / TRD.md 의 "Entities 6.x" 표기는 **향후 목표** 를 기재한 것이며, 현 구현은 1.4.5 위에서 수행됨을 본 문서로 확인
- 설계 (`docs/plans/2026-04-19-phase9-flow-field-design.md`) 는 1.4 / 6.x 공통 API (`ISystem / SystemAPI / EntityCommandBuffer / DynamicBuffer / NativeQueue`) 만 사용 → 업그레이드 여부와 독립적으로 적용 가능
- Phase 9 → 10 사이 재논의 시점 근거: Phase 10 의 procedural 맵 생성이 Entities 6.x 신규 API 를 실질적으로 활용할 여지가 있고, Phase 10 scope 가 더 커서 업그레이드 리스크 상각이 쉬움

**Phase 9 착수 시점에는 P9-00 선행 작업 없음**. 바로 P9-01 부터 시작.

### 5.2 `MapData.paths` 필드 처리 — **(a) 확정**

결정: `[Obsolete]` 표기 + 필드 유지, **Phase 10 asset migration 때 MapData → GeneratedMap 전환과 함께 삭제**.

- Phase 9 에서 `MapData.paths` 는 MapView / BattleBridge 어디서도 읽지 않음 (P9-06 / P9-07 작업으로 참조 전량 제거)
- 필드 자체는 asset schema 보존을 위해 유지 (즉시 제거 시 PrototypeMap.asset YAML 에 잔여 `paths: ` 블록이 남아 경고 발생)
- `[System.Obsolete("Unused since Phase 9. Removed in Phase 10 asset migration.", error: false)]` 표기로 실수 방지

---

## 6. 착수 흐름

1. ✅ 본 문서 + `docs/plans/2026-04-19-phase9-flow-field-design.md` 확정 (2026-04-19)
2. §5.1 Unity Editor 버전 / §5.2 paths 필드 처리 결정 (사용자)
3. 기준선 Play 녹화
4. writing-plans 스킬로 구현 계획 세분화 → P9-01 ~ P9-12 순차 구현
5. P7-15 / P8-10 / P9-11~P9-12 Play 회귀 통과
6. `docs/PHASE9.md` 구현 종료 스펙 작성 + 커밋
7. Phase 10 브레인스토밍 착수 — `docs/phase10-prep.md` 기반

---

**작성**: 2026-04-19 (Codex 2차 리뷰 반영 rewrite)  
**스코프**: Flow field 길찾기 교체 한정. 맵 시스템 재설계 전체 → `docs/phase10-prep.md`.  
**설계**: `docs/plans/2026-04-19-phase9-flow-field-design.md`.
