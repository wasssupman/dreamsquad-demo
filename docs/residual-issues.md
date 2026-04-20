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

## A. 버그 / 이상 동작

### A1. Tornado 스냅샷 한계 [High → ✅ 2026-04-19 해결]

- **증상 (과거)**: Tornado 캐스트 순간 영역 안에 있던 적만 끌림 효과 받음. duration 중 새로 영역 진입하는 적은 영향 X.
- **해결**: `TornadoPull` per-entity 컴포넌트를 `TornadoField` 캐리어 엔티티로 교체 (PortalLink 패턴). `MovementSystem` 이 매 프레임 live `TornadoField` 엔티티를 쿼리해 범위 내 적에게 pull step 적용. `EffectTickSystem` 이 remaining 만료 시 엔티티 destroy. `BattleBridge.ApplyTornado` 는 per-attacker 반복 제거, 단일 `EffectSpawner.SpawnTornadoField` 호출로 단순화.
- **커밋**: Phase 8 §17 최종 마무리 커밋에 포함.

### A2. 포탈 동선 이상 [High → ✅ 2026-04-20 해결 (Phase 9)]

- **요약 (과거)**: Portal exit 타일이 경로 외부이거나 closest waypoint 가 뒤쪽일 때 텔레포트된 적이 역주행.
- **해결**: Phase 9 flow field 도입으로 `ResolveExitWaypointIndex` / `PortalLink.exitWaypointIndex` 삭제. 텔레포트 직후 현재 cell 의 flow lookup 으로 자율 복귀. P9-12 사용자 Play 회귀로 최종 확인 예정.
- **커밋**: Phase 9 P9-06 migration 커밋 (PortalLink.exitWaypointIndex 제거).

---

## B. 사용자 Play 검증 대기

- [ ] **P7-15** Phase 7 회귀 — 드래프트 2종 스킬 패널 / Tornado·Meteor·Portal 동작 / Restart·Redraft
- [ ] **P8-10** Phase 8 Spine/VFX 회귀 — defender Spine 상태 전환 + 5종 VFX prefab 시각
- [x] **P9-12** Phase 9 Flow Field 회귀 — Portal 텔레포트 후 자율 복귀 / Tornado field 해제 후 자율 복귀 / Goal cell 도달 시 `PastGoalTag` 부여. 2026-04-21 사용자 Play 통과 (BuildFlowField walkable=Path-only fix `006ae2f` 후 검증 완료). P9-11 기준선 녹화는 skip.
- [ ] **VFX 카탈로그 10개 검토/승인** — `.claude/skills/unity-vfx-authoring/common-skill-vfx-reference.md`

---

## C. 사용자 에디터 수작업 대기

- [ ] **Shader Graph 템플릿 제작** — dissolve/glow 2종. `.claude/skills/unity-vfx-authoring/templates/` 에 `.shadergraph` 덮어쓰기. `material-settings-reference.md` 가이드 참고.
- [x] ~~Phase 9 기준선 녹화~~ — 사용자 결정으로 skip. Phase 9 검증은 P9-12 이진 판정으로 대체.

---

## D. Phase 9 이관 (맵/길찾기)

모든 항목 `docs/phase9-prep.md` 로 이관 완료. 본 문서에선 포인터만 유지.

- Flow Field 기반 길찾기 재설계 (Phase 9 주 테마)
- 이슈 A (포탈 동선) / B (Tornado 자율 복귀) / C (다중 레인 확장) — Phase 9 해결 경로 명시됨
- P9-01 ~ P9-07 작업 분해 초안

---

## E. 후속 제안 / 미확정 (drop 여지 있음)

- **Meteor HDR + bloom 2단계 업그레이드** — 현 씬 URP bloom Volume 없음. 필요 시 post-processing 도입 후 HDR 로 전환
- **onPlace 이펙트 VFX** (SlowPulse / BoostNearbyDefenders 의 시각 피드백)
- **Projectile hit sparks**
- **Synergy glow** — Shader Graph glow 템플릿 활용 후보
- **Enemy death dissolve** — Shader Graph dissolve 템플릿 활용 후보
- **BattleBridge.SpawnMeteorWarningVisual → prefab 화** — 현재 procedural Quad. VFX 파이프라인 일관성 위해 후속
- **방어 유닛 공격 범위 표시 UI** — 우측 중앙(SkillBar 아래) 홀드 버튼, 홀드 중 모든 placed defender 의 attackRange ring 일괄 노출. 홀드 시작에 aim race 초기화(`RaiseAimCanceled` + `InspectHold=true`). `RangeOverlayController` MB + LineRenderer pool 설계 완료, Phase 9 이관 확정 (2026-04-19)

---

## F. 코드 정리 (후속 저비용)

- [x] `PlacementInput` random fallback 제거 (커밋 `37213c2`)
- [ ] `BattleBridge.SpawnMeteorWarningVisual` procedural Quad 제거 검토 (prefab 화 여부와 연계)
- [x] `PathFollowState.currentWaypointIndex` / `DynamicBuffer<PathWaypoint>` 제거 (Phase 9 P9-05B / P9-09)
- [x] `ResolveExitWaypointIndex` / `PortalLink.exitWaypointIndex` 제거 (Phase 9 P9-06)
- [ ] `MapData.paths` `[Obsolete]` 필드 완전 삭제 — Phase 10 asset migration (PrototypeMap.asset → GeneratedMap) 시점
- [ ] Task 2 MEDIUM: `GridMath` `CellIndex` 및 half-boundary 반올림 EditMode 테스트 보강 (Phase 10 맵 파이프라인 정착 후 재검토)

---

## 상태 요약 (2026-04-20 Phase 9 종료 기준)

| 카테고리 | 미체크 수 | 비고 |
|---|---|---|
| A. 버그 | 0 | A1 Phase 8 §17, A2 Phase 9 P9-06 에서 해결 |
| B. 사용자 Play | 3 | P7-15 / P8-10 / P9-12 (사용자 Play 확인) |
| C. 에디터 수작업 | 1 | Shader Graph 템플릿. Phase 9 기준선 녹화는 skip 결정 (2026-04-20) |
| D. Phase 9 | — | Phase 9 완료. 이후 이관은 `docs/phase10-prep.md` |
| E. 후속 제안 | 7 | 1~6 keep (Phase 10 이후 판단), 7 (공격 범위 UI) Phase 10+ 이관 |
| F. 코드 정리 | 2 | PathWaypoint / Portal exit index 삭제 완료. 남은 항목 Phase 10 asset migration 및 테스트 보강 |

---

**작성 정책**: 본 문서는 페이즈 종료 프로토콜 (1/2/3) 마다 갱신된다. 에이전트는 Phase 종료 선언 전에 반드시 본 문서를 사용자에게 보고.
