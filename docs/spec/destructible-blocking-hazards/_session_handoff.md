# Kickoff Handoff — Destructible Blocking Hazards

**Status**: 브레인스토밍 + spec 작성 완료, 구현 미착수.
**Spec 폴더**: `docs/spec/destructible-blocking-hazards/` (README + 0~10 작업 단위, 본 handoff).
**Design 문서**: `docs/plans/2026-04-29-destructible-blocking-hazards-design.md` (얇은 design, 결정 trace).
**작성**: 2026-04-29.
**다음 작업자**: Codex CLI (구현). 사용자 직접 호출.
**Critic 상태**: 2회 리뷰 — 1차 REJECT (5 갭) → 2차 ACCEPT WITH MINOR FIXES (7 항목 모두 반영).

## 본 spec 의 자리

차단형 hazard (바위/벽 등) 에 HP 부여 → 적이 공격해 부수는 메커닉. 동시에 "공격 가능 타겟" 추상화를 ECS 일반 인프라 (Faction enum + AttackState.targetMask) 로 진화. cc-pipeline-and-obstacle 의 Obstacle 인프라를 진화시키는 형태이며, path-zone-hazards 의 sampler / Visual⊥Effects 패턴을 공유.

## 브레인스토밍 결정 요약

| 결정 | 채택 | 이유 |
|---|---|---|
| 적 공격 시스템 scope | 일반 (target = damageable entity) | 기존 `Health` + `IncomingDamage` 채널이 이미 entity-agnostic — IDamageable 의 ECS 표현 |
| Taunt 메커니즘 | 없음 (path-block 으로 자연 어그로) | 차단 hazard 가 적 정지 → 사거리 안 자동 진입. Taunt radius 는 후속 (스킬/효과) |
| Obstacle 진화 형태 | 단일 Obstacle + 옵셔널 추가 컴포넌트 (HP-only) | cc-pipeline 디버그 큐브 변경 0. Obstacle.remainingLife 는 hazard 에서 미사용 (∞) |
| Cell shape | Multi-cell + HazardShapeSampler 재사용 | 차단 게임감 = 크기. 1×1 은 cc-pipeline 큐브와 시각 차이 없음 |
| Destruction 알림 | NativeQueue 채널 (`HazardDestroyedEventsSingleton`) | 운영 7채널과 동일 패턴, BattleBridge drain → visual destroy + VFX |
| HP bar | HealthBarTag/State 인프라 재사용 | "부수는 진행감" 시각 피드백 = 본 spec 의 검증 게임감 핵심 |
| Tag 디자인 | Faction enum [Flags] + FactionTag IComponentData + AttackState.targetMask | 사용자 의도 = "타입 + 공격 가능 레이어" 의 ECS 표현. DamageableTag 신설 ❌ (적 자가-타겟 위험) |
| AttackSystem refactor | 두 loop 유지 + target snapshot 만 통합 | buff/projectile/CC 분기 차이 보존 → 회귀 위험 ↓ |
| Faction 도입 시점 | 본 spec 에 묶음 (Slow migration 패턴) | hazard = 첫 새 faction 등장 시점. 미루면 query 확장 코드 폐기 |

## 구현 순서 (1 파일 = 1 commit)

```
0 ──▶ 1 ──▶ 2★ ──▶ 3 ──▶ 4 ──▶ 5 ──▶ 6 ──▶ 7 ──▶ 8 ──▶ 9★
```

★ = 사용자 PlayMode manual 확인 필수 게이트.
- **Unit 2** = 회귀 게이트 (디펜더↔적 공격 동작 동일).
- **Unit 9** = feature 게이트 (V1~V6 시나리오 + 게임감).
- **Unit 0/1** = compile-only — 단독 play-test ❌. Unit 2 까지 묶어 검증.

## 절대 보존 (되돌리지 말 것)

- `Health` + `IncomingDamage` + `DamageApplicationSystem` + `DeadTag` + `UnitLifecycleSystem` 의 entity-agnostic 채널 — 추상화 인터페이스 신설 ❌ (CLAUDE.md "구현체 2개 이상일 때만 인터페이스").
- `LocalTransform` writer = MovementSystem 단독 (불변).
- `Obstacle` 컴포넌트 변경 ❌ (cc-pipeline 디버그 큐브 회귀 0).
- `AttackUnitTag` / `DefenderUnitTag` 유지 — Movement / lifecycle / 배치 식별에 쓰임. **FactionTag 는 공격 타겟팅 전용 병행 추가** (두 시스템 공존). 진영 식별 일원화는 후속 spec.
- AttackSystem 의 두 loop **통합 ❌** — Unit 2 는 target snapshot query 만 통합. attacker foreach 의 `WithAll<DefenderUnitTag>` / `WithAll<AttackUnitTag>` 유지.
- 운영 중 NativeQueue 채널 7개 (`GoalReached`, `DefenderDeath`, `MeteorBurst`, `DefenderAttack`, `ProjectileHit`, `EnemyCc`, `HazardRuntime`) lifecycle 영향 ❌. 신설 채널 (`HazardDestroyedEvents`) 만 추가.
- FlowFieldBuilder 는 hazard 점유 cell 기반 재경로 ❌ (cc-pipeline 의 `blockedCells` 정책 유지).
- path-zone hazard cell 과 blocking hazard cell **양립 허용** (zone 효과는 통과형, blocking 은 차단형 — 동시 가능). spawn validation 에 path-zone 충돌 검사 ❌.
- enqueue 가 ECB destroy 보다 **먼저** (DefenderDeath 패턴, UnitLifecycleSystem.cs:67~72).

## 작업 시 주의

### 데이터 모델 / 컨텍스트 경계

- `Faction` enum + `FactionTag` 위치 = `Wassup.Battle.Units` namespace (entity 정체성 = Units 맥락). Combat (AttackSystem) 가 read-only 참조.
- `BlockingHazard` / `BlockingHazardCellsBuffer` 위치 = `Wassup.Battle.Effects`. 쓰기는 `EffectSpawner.SpawnBlockingHazard` 단일 진입점.
- `HazardDestroyedEvent` / Singleton 위치 = `Wassup.Battle.Effects`.

### Burst 호환

- AttackSystem (Unit 2) 의 mask 필터는 단순 `int` 비트 AND — Burst 친화.
- ObstacleLifetimeSystem (Unit 4) 멀티셀 loop 도 Burst 유지 가능.
- `EffectSpawner.SpawnBlockingHazard` (Unit 7) 는 ECB structural change + EntityManager API → 비-Burst 가 자연 (cc-pipeline 의 `CcApplySystem` 전례, Slow migration 시 비-Burst 사유 주석 패턴 참조).

### 시스템 순서 (의존)

`ObstacleLifetimeSystem` (UpdateBefore MovementSystem) → `MovementSystem` → `AttackSystem` (UpdateAfter MovementSystem) → `DamageApplicationSystem` (UpdateAfter AttackSystem) → `UnitLifecycleSystem` (UpdateAfter DamageApplicationSystem)

→ HP=0 → 같은 프레임 destroy. blockedCells 1-frame 잔존 의도 (README 명시). 변경 ❌.

### Unit 7 spawn API 핵심

- `HazardShapeSampler.Sample(shape, origin, radius)` — Square3x3 은 radius 무시 (sampler 내부 hardcoded), `radius:1` 전달.
- `GridMath.CellToWorldCenter(cell, ff.tileSize)` 사용 (NOT `CellToWorld`).
- `ValidateCellsForBlockingHazard(em, cells, ff, out reason)` — FlowFieldSingleton 한 번 fetch 후 인자로 전달.
- 충돌 거부 시 `Entity.Null` + 경고 로그. silent clamp / cell swap ❌.
- `RegisterHazardSO` = idempotent (Dictionary lookup) — 같은 SO 재등록 시 기존 index 반환.
- spawn 컴포넌트 8종: `Obstacle` (remainingLife=∞) + `BlockingHazard` + `BlockingHazardCellsBuffer` (멀티셀) + `Health` + `IncomingDamage` (buffer) + `HealthBarTag` + `HealthBarState{owner=entity}` + `FactionTag{BlockingHazard}` + `LocalTransform`.

### Unit 8 visual

- `HealthBarSystem` (HealthBarSystem.cs:31) 는 `HealthBarTag` 만 query — entity-agnostic. 코드 수정 ❌.
- `BlockingHazardPresenter.Bind(entity)` — bridge 참조 불요 (drain 은 BattleBridge 단방향).

### Unit 9 prerequisite

- `Enemy_Debug_Melee_Attacker.asset` 작성 (attackDamage=5, attackRange=1.5, attackCooldown=1.0) 또는 기존 melee enemy SO 점검 후 활용. 본 unit 시작 전 확인.

## 사용자 확인 protocol

각 unit commit 후:
- **Unit 0, 1, 3, 4, 5, 6, 7, 8, 10**: compile + test 통과 보고 → 사용자에게 "다음 unit 진행해도 됨?" 한 줄 확인.
- **Unit 2 ★**: PlayMode 회귀 검증 (디펜더↔적 공격 / knockback / projectile / synergy 동일). EditMode mask filter 테스트 통과. 사용자 통과 후 다음.
- **Unit 9 ★**: V1~V6 시나리오 사용자 manual 확인 → spec 종료 → `10_handoff_summary.md` 작성 (현재 placeholder) + README 상태 "완료 YYYY-MM-DD" 갱신.

각 unit 완료 후 해당 작업 단위 파일의 "완료 기준" 섹션 하단에 확인 일자 + 커밋 해시 한 줄 추가 (CLAUDE.md 기본 워크플로우).

## 작업 시작점

`docs/spec/destructible-blocking-hazards/0_faction_and_target_mask.md` 를 읽고 그 파일만 가지고 Unit 0 작업 진행. `README.md` 공통 원칙 + 본 handoff 의 "절대 보존" 섹션을 상시 컨텍스트로 유지. 모르면 멈추고 사용자에게 질문.

## 참조 spec (의존)

- `docs/spec/cc-pipeline-and-obstacle/` — Obstacle / blockedCells / EnemyCcEvents 인프라 source. Unit 3/4/7 가 진화.
- `docs/spec/path-zone-hazards/` — `HazardShape` enum / `HazardShapeSampler` / Visual⊥Effects 패턴 source. Unit 6/7/8 이 재사용.
