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

### A1. Tornado 스냅샷 한계 [High]

- **증상**: Tornado 캐스트 순간 영역 안에 있던 적만 끌림 효과 받음. duration 중 새로 영역 진입하는 적은 영향 X.
- **원인**: `BattleBridge.ApplyTornado` 가 `_aliveAttackersQuery.ToEntityArray` 로 **cast-time 스냅샷** 만 순회해 각 entity 에 `TornadoPull` component 부착. 지속 tick 시스템 없음. 신규 진입 적은 `TornadoPull` 못 받음.
- **기대 동작**: duration 동안 영역에 있는 **모든** 적이 지속적으로 끌림 (연속 영역 효과)
- **해결 방향**: `TornadoField` 엔티티 싱글턴을 cast 시 생성(MeteorPending 패턴 유사) + `TornadoFieldSystem` 매 프레임 범위 내 적에게 `TornadoPull` 갱신. `EffectTickSystem` 이 remaining 만료 시 field 엔티티 destroy.
- **대상 페이즈**: Phase 8 내 처리 가능(스코프 한정) 또는 Phase 9 이관 후 flow field 와 함께 재설계
- **영향 범위**: Tornado 스킬. SlowField 도 동일 스냅샷 패턴이나 의도된 "spread once" 로 볼 수 있어 버그 아님. Tornado 만 "지속 field" 의미가 강함.

### A2. 포탈 동선 이상 [High → Phase 9 이관 확정]

- **요약**: Portal exit 타일이 경로 외부이거나 closest waypoint 가 뒤쪽일 때 텔레포트된 적이 역주행.
- **세부**: `docs/phase9-prep.md` §2 이슈 A 참조.
- **상태**: Phase 9 flow field 도입 시 자연 해결 → 본 문서에선 **완료 처리**, Phase 9 추적.

---

## B. 사용자 Play 검증 대기

- [ ] **P7-15** Phase 7 회귀 — 드래프트 2종 스킬 패널 / Tornado·Meteor·Portal 동작 / Restart·Redraft
- [ ] **P8-10** Phase 8 Spine/VFX 회귀 — defender Spine 상태 전환 + 4종 VFX prefab 시각
- [ ] **P8-11** defender 10종 skin 할당 (현재 Archer=Lamb 만)
- [ ] **VFX 카탈로그 10개 검토/승인** — `.claude/skills/unity-vfx-authoring/common-skill-vfx-reference.md`

---

## C. 사용자 에디터 수작업 대기

- [ ] **Shader Graph 템플릿 제작** — dissolve/glow 2종. `.claude/skills/unity-vfx-authoring/templates/` 에 `.shadergraph` 덮어쓰기. `material-settings-reference.md` 가이드 참고.
- [ ] **Phase 9 기준선 녹화** — Portal / Tornado / 평상시 동선 3 케이스. Phase 9 착수 전 준비.

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

- [ ] `BattleBridge.SpawnMeteorWarningVisual` procedural Quad 제거 검토 (prefab 화 여부와 연계)
- [ ] Phase 9 이후 `PathFollowState.currentWaypointIndex` / `DynamicBuffer<PathWaypoint>` 제거
- [ ] `ResolveExitWaypointIndex` / `PortalLink.exitWaypointIndex` 제거 (Phase 9 P9-04)

---

## 상태 요약 (2026-04-19 기준)

| 카테고리 | 미체크 수 | 비고 |
|---|---|---|
| A. 버그 | 1 (A1) | A2 는 Phase 9 확정 이관 |
| B. 사용자 Play | 4 | 모두 본인 작업 |
| C. 에디터 수작업 | 2 | 사용자 여유 있을 때 |
| D. Phase 9 | — | 포인터만 |
| E. 후속 제안 | 5 | drop 여지 포함 |
| F. 코드 정리 | 3 | 저우선 |

---

**작성 정책**: 본 문서는 페이즈 종료 프로토콜 (1/2/3) 마다 갱신된다. 에이전트는 Phase 종료 선언 전에 반드시 본 문서를 사용자에게 보고.
