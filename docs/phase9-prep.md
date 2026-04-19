# Phase 9 이관 체크리스트 — 맵 / 길찾기

> Phase 9 본격 착수 대상만 담은 좁은 스코프 문서. VFX 잔여·사용자 Play 확인·코드 정리는 Phase 8 내에서 모두 처리하고 넘긴다. **맵과 맵 길찾기 관련 이슈만** 이 문서에 남긴다.

---

## 1. Phase 9 주 테마 — Flow Field 기반 길찾기 재설계

### 왜

- 현재 Waypoint 기반 (`DynamicBuffer<PathWaypoint>` + `currentWaypointIndex`) 구조는 **변위 발생 시 자율 복귀 불가**
- 맵 고도화 (2-row 이상, 다중 레인, 분기) 시 수작업 waypoint 유지 비용 급증
- 포탈/Tornado/향후 넉백 등 위치가 임의로 바뀌는 스킬이 구조적으로 불편

### 기술 방향 (Phase 8 결정)

Flow Field 채택. 대안과의 비교:

| 옵션 | 결론 |
|---|---|
| NavMesh + NavMeshAgent | **배제** — MonoBehaviour 전용, ECS/Burst 비친화, agent 당 쿼리 비용, 타일 좌표계 불일치 |
| Tile A* per-agent | 가능 but 변위 때마다 recompute 부담 |
| **Flow Field** | **최선** — ECS/Burst 친화, O(1) lookup, 변위 자연 복귀, 맵 1회 재계산 |
| Hybrid (waypoint corridor + flow field fallback) | 기존 authoring 보존 원할 시 옵션 |

---

## 2. 핵심 알려진 이슈 (Phase 9 에서 해결)

### 이슈 A — 포탈 동선 이상 (사용자 보고)

- **증상**: Portal 로 텔레포트된 적이 역주행하거나 엉뚱한 방향으로 이동
- **원인**: `BattleBridge.ResolveExitWaypointIndex` 의 closest+1 fallback 이 "이미 지나친 waypoint" 를 다음 목표로 지정 가능. exit 타일이 경로 외부일 때 특히.
- **임시 회피**: exit 타일을 경로 상 정확한 waypoint cell 로만 지정 (UX 제약)
- **Phase 9 해결**: flow field 도입 → teleport 직후 현재 타일의 flow 읽기로 자동 복귀. `ResolveExitWaypointIndex` / `PortalLink.exitWaypointIndex` 함수/필드 삭제 대상

### 이슈 B — 변위 후 자율 복귀 부재

- **증상**: Tornado 해제 후 적이 끌림 중심에서 다음 waypoint 까지 직선 이동 (기계적 느낌)
- **원인**: `PathFollowState.currentWaypointIndex` 는 Tornado 동안 고정, 위치만 바뀜
- **Phase 9 해결**: flow field 로 현재 위치 기준 다음 방향 계산, waypoint index 개념 자체 제거

### 이슈 C — 다중 레인 / 경로 확장 부담

- **증상**: 현재 `map.Paths` 중 `AttackDeck.spawnEntry.pathId` 지정 하나만 사용. 2-row 이상 맵에서 waypoint 수작업 복제 필요
- **Phase 9 해결**: flow field 는 "모든 타일에서 goal 까지의 방향" 이므로 spawn 위치만 달라지면 자동 분기

---

## 3. 작업 분해 초안 (P9-NN)

- [ ] **P9-01** — `PathfindingGrid` SO + BFS/Dijkstra goal field 계산. EditMode 단위 테스트 (직선 / 다중 레인 / 장애물 우회)
- [ ] **P9-02** — `FlowFieldSingleton` (NativeArray 보관) + BattleBridge 에서 맵 로드 시 계산 + 싱글톤 생성
- [ ] **P9-03** — `MovementSystem` flow field 기반으로 교체. 기존 `PathFollowState`/`PathWaypoint` 버퍼 제거. `currentWaypointIndex` 개념 삭제
- [ ] **P9-04** — `BattleBridge.ResolveExitWaypointIndex` 삭제, `PortalLink.exitWaypointIndex` 필드 제거, Movement 에서 포탈 직후 즉시 flow field 재조회
- [ ] **P9-05** — Tornado 풀림 직후 flow field 복귀 확인, 임의 변위 테스트 (넉백 프로토타입 가능)
- [ ] **P9-06** — (선택) Waypoint corridor + field fallback 하이브리드 — 레벨디자인 의도 보존 원할 시
- [ ] **P9-07** — PlayMode 회귀 + EditMode 테스트 통과 확인

---

## 4. 착수 전 결정 필요

- 다중 goal 지원 여부 (현재 단일 goal)
- 기존 수작업 waypoint 자산 유지 vs 전량 대체
- 동적 장애물 (실시간 막힘) 지원 범위

---

## 5. 착수 전 기준선 Play 녹화 (회귀 판정용)

Phase 9 재설계 전 현재 상태에서 **의도적 동선 이상** 3케이스 기록:

1. Portal: exit 타일을 경로 위 / 경로 옆 / 경로에서 먼 곳 → 각 동선 촬영
2. Tornado: 해제 후 적 이동 방향 기록
3. 단일 유닛 start→goal 평상시 진행 (기준선)

이 자료가 Phase 9 flow field 전환 후 회귀 판정 근거.

---

## 6. 착수 흐름 제안

1. 본 문서 재검토 + scope 확정
2. `docs/PHASE9.md` 작성 (Flow Field 설계 상세)
3. Codex 2-round 리뷰 (Phase 8 VFX 패턴)
4. 구현 + waypoint 기반 코드 제거
5. Play 회귀 + 5장 기준선 비교

---

**작성**: 2026-04-19  
**스코프**: 맵 + 맵 길찾기 한정. 그 외 Phase 7/8 잔여는 Phase 8 종료 전 모두 처리하고 이관.
