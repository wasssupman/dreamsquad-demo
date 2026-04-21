# 잔여 이슈 체크리스트

> 페이즈 종료 시점마다 점검되는 문서. 에이전트는 Phase 전환 전 이 파일을 읽고 각 미체크 항목을 사용자에게 보고 후 처리 여부를 묻는다. 결정된 처리는 이 문서에 반영된다.

## 페이즈 종료 프로토콜

1. 에이전트가 본 문서의 미체크 항목을 요약 (심각도 + 소속 카테고리)
2. 각 항목별 질의:
   - **즉시 처리** (현 페이즈 내)
   - **다음 페이즈로 이관** (해당 페이즈 prep 문서로 이동)
   - **drop** (영구 보류)
3. 사용자 응답 반영해 본 문서 업데이트 + 관련 페이즈 prep 이동

---

## A. 버그 / 이상 동작 (해결 이력)

### A1. Tornado 스냅샷 한계 [High → ✅ 2026-04-19 해결]

- **증상 (과거)**: Tornado 캐스트 순간 영역 안에 있던 적만 끌림 효과 받음. duration 중 새로 영역 진입하는 적은 영향 X.
- **해결**: `TornadoPull` per-entity 컴포넌트를 `TornadoField` 캐리어 엔티티로 교체 (PortalLink 패턴). `MovementSystem` 이 매 프레임 live `TornadoField` 엔티티를 쿼리해 범위 내 적에게 pull step 적용. `EffectTickSystem` 이 remaining 만료 시 엔티티 destroy. `BattleBridge.ApplyTornado` 는 per-attacker 반복 제거, 단일 `EffectSpawner.SpawnTornadoField` 호출로 단순화.
- **커밋**: Phase 8 §17 최종 마무리 커밋에 포함.

### A2. 포탈 동선 이상 [High → ✅ 2026-04-20 해결 (Phase 9)]

- **요약 (과거)**: Portal exit 타일이 경로 외부이거나 closest waypoint 가 뒤쪽일 때 텔레포트된 적이 역주행.
- **해결**: Phase 9 flow field 도입으로 `ResolveExitWaypointIndex` / `PortalLink.exitWaypointIndex` 삭제. 텔레포트 직후 현재 cell 의 flow lookup 으로 자율 복귀. P9-12 사용자 Play 회귀 2026-04-21 통과.
- **커밋**: Phase 9 P9-06 migration 커밋.

---

## B. 사용자 Play 검증 (이력)

- [x] **P9-12** Phase 9 Flow Field 회귀 — 2026-04-21 사용자 Play 통과 (BuildFlowField walkable=Path-only fix `006ae2f` 후 검증).
- ~~P7-15 / P8-10 / VFX 카탈로그 검토~~ — Phase 10 종료(2026-04-21) 시 drop 결정.

---

## C. 사용자 에디터 수작업 (이력)

- [x] ~~Phase 9 기준선 녹화~~ — 2026-04-20 skip 결정. P9-12 이진 판정으로 대체.
- ~~Shader Graph 템플릿 (dissolve/glow)~~ — Phase 10 종료(2026-04-21) 시 drop 결정.

---

## D. Phase 9 이관 (맵/길찾기)

모든 항목 Phase 9 에서 해결 완료. 본 카테고리는 이력용으로만 유지.

---

## E. 후속 제안 (Phase 10 종료 시 drop)

2026-04-21 Phase 10 종료 시점에 다음 후속 제안 항목 전부 drop:

- ~~Meteor HDR + bloom 2단계 업그레이드~~
- ~~onPlace 이펙트 VFX (SlowPulse / BoostNearbyDefenders)~~
- ~~Projectile hit sparks~~
- ~~Synergy glow~~
- ~~Enemy death dissolve~~
- ~~BattleBridge.SpawnMeteorWarningVisual → prefab 화~~
- ~~방어 유닛 공격 범위 표시 UI~~ (Phase 9 이관 예정이었으나 Phase 10 에서도 미구현 → drop)

새 후속 제안은 추후 필요 시 재등재.

---

## F. 코드 정리 (이력)

- [x] `PlacementInput` random fallback 제거 (커밋 `37213c2`)
- [x] `PathFollowState.currentWaypointIndex` / `DynamicBuffer<PathWaypoint>` 제거 (Phase 9 P9-05B / P9-09)
- [x] `ResolveExitWaypointIndex` / `PortalLink.exitWaypointIndex` 제거 (Phase 9 P9-06)
- [x] `MapData.paths` `[Obsolete]` 필드 완전 삭제 — Phase 10 종료 시 `paths` 필드/`Paths` property/`PathDefinition` 클래스 + `PrototypeMap.asset` paths 블록 제거
- ~~BattleBridge.SpawnMeteorWarningVisual procedural Quad 제거~~ — Phase 10 종료 drop.
- ~~GridMath CellIndex half-boundary 반올림 테스트 보강~~ — Phase 10 종료 drop.

---

## 상태 요약 (2026-04-21 Phase 10 종료 기준)

| 카테고리 | 미체크 수 | 비고 |
|---|---|---|
| A. 버그 | 0 | A1/A2 모두 해결 |
| B. Play 검증 | 0 | P9-12 통과. P7-15/P8-10/VFX 카탈로그 drop |
| C. 에디터 수작업 | 0 | 전부 drop |
| D. Phase 9 이관 | — | 완료 |
| E. 후속 제안 | 0 | 전부 drop |
| F. 코드 정리 | 0 | 미해결 항목 drop |

**미체크 항목 없음. Phase 11 은 clean slate 로 출발.**

---

**작성 정책**: 본 문서는 페이즈 종료 프로토콜 (1/2/3) 마다 갱신된다. 에이전트는 Phase 종료 선언 전에 반드시 본 문서를 사용자에게 보고.
