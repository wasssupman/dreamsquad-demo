# Destructible Blocking Hazards Spec

**작성일**: 2026-04-29
**연결 문서**: `docs/spec/cc-pipeline-and-obstacle/` (Obstacle 인프라 진화) · `docs/spec/path-zone-hazards/` (HazardShapeSampler / Visual ⊥ Effects 패턴 재사용) · `docs/plans/2026-04-29-destructible-blocking-hazards-design.md` (얇은 design)
**목표**: 차단형 hazard (바위/벽) 에 HP 부여 → 적이 공격해 부수는 메커닉. 동시에 "공격 가능 타겟" 추상화를 ECS 일반 인프라 (Faction + targetMask) 로 진화.

## 상태

구현 완료, PlayMode 사용자 확인 완료 (2026-04-29).

## 구현 문서 목록

| # | 문서 | 목적 |
|---|---|---|
| 0 | `0_faction_and_target_mask.md` | `Faction` enum [Flags] + `FactionTag` IComponentData + `AttackState.targetMask` 필드 추가. **compile-only 게이트 — Unit 1 까지 합쳐 검증, 단독 play-test 금지** (`targetMask=0` default 면 모든 attacker 가 타겟 못 찾음) |
| 1 | `1_faction_spawn_assignment.md` | 디펜더/적/(차후 hazard) spawn 코드 (BattleBridge) 에 FactionTag + targetMask default 부여. **여전히 compile-only — AttackSystem 은 아직 mask 안 봄. Unit 2 와 묶어 회귀 검증** |
| 2 | `2_attack_system_mask_query.md` | AttackSystem 의 **타겟 snapshot query** (line 41/47) 를 `WithAll<FactionTag, Health, LocalTransform>` 로 전환 + 내부 루프에 `(target.faction & attacker.targetMask) != 0` 필터 추가. **attacker foreach (line 80/268) 의 `WithAll<DefenderUnitTag>` / `WithAll<AttackUnitTag>` 는 유지** — 두 loop 의 buff/projectile/CC 분기 차이 보존, 회귀 위험 ↓ (**회귀 게이트**) |
| 3 | `3_blocking_hazard_data_model.md` | `BlockingHazard` IComponentData + `BlockingHazardCellsBuffer` + `BlockingHazardSO` 골격 |
| 4 | `4_obstacle_lifetime_multicell.md` | ObstacleLifetimeSystem 확장 — `BlockingHazardCellsBuffer` 의 모든 cell 을 blockedCells 에 add |
| 5 | `5_hazard_destroyed_event_channel.md` | `HazardDestroyedEventsSingleton` (운영 중 NativeQueue 채널들과 동일 패턴, 8번째) + UnitLifecycleSystem 분기. **enqueue 가 ECB destroy 보다 먼저** (DefenderDeath 패턴, line 67~72) |
| 6 | `6_blocking_hazard_so_and_sample.md` | `BlockingHazardSO` Inspector 필드 + `Hazard_Rock_3x3` 샘플 1종 + placeholder visual prefab |
| 7 | `7_spawn_api.md` | `EffectSpawner.SpawnBlockingHazard(em, BlockingHazardSO, int2)` + `BattleBridge.SpawnBlockingHazardWithVisual`. **충돌 거부 정책**: 샘플된 cell 중 (a) 골 cell, (b) 기존 `ObstacleSingleton.blockedCells`, (c) `DefenderTile.cell`, (d) OOB 와 겹치면 spawn 중단 (`Entity.Null` 반환 + 경고 로그). path-zone 과 같은 cell 중첩은 허용 (zone hazard 와 blocking 은 양립) |
| 8 | `8_presenter_and_healthbar.md` | `BlockingHazardPresenter` MonoBehaviour + `HealthBarState` 부착 + destruction VFX 트리거 (queue drain) |
| 9 | `9_debug_spawn_and_verification.md` | `BattleBridge.DebugSpawnBlockingHazardAt` + Editor 메뉴 + PlayMode 검증 시나리오 (**feature 게이트**) |
| 10 | `10_handoff_summary.md` | 구현 결과 + 검증 로그 + 후속 주의점 |

## 공통 원칙 (feature-wide 계약)

- **공격 타겟팅 단일 모델**: 모든 attackable entity = `Health` + `IncomingDamage` buffer + `FactionTag` 보유. attacker = `AttackState.targetMask` 보유. 타겟 후보 필터 = `(target.faction & attacker.targetMask) != 0`. AttackUnitTag/DefenderUnitTag 의 진영 식별 역할은 본 spec 후 사용 X (단 다른 역할 — Movement/lifecycle/배치 식별 — 으로 유지).
- **IDamageable 추상화 = 컴포넌트 조합** — C# 인터페이스 신설 X. `Health` + `IncomingDamage` + `DamageApplicationSystem` 채널이 entity-agnostic.
- **Hazard entity = `Obstacle` + `BlockingHazard` + Health + ...** — cc-pipeline 의 Obstacle 인프라 진화 형태. cc-pipeline 디버그 큐브 (Obstacle only) 와 본 spec hazard (Obstacle + BlockingHazard + Health + ...) 가 컴포넌트 조합으로 구분.
- **HP-only destruction** — `Obstacle.remainingLife` 미사용 (∞). HP 0 → DeadTag → UnitLifecycleSystem 자동 destroy. 다음 프레임 ObstacleLifetimeSystem 이 blockedCells 재구축.
- **Multi-cell** — `BlockingHazardCellsBuffer` + `HazardShapeSampler` (path-zone 재사용). 적 공격 사거리 = 가장 가까운 점유 cell 의 worldPosition 거리.
- **Destruction 알림** — `HazardDestroyedEventsSingleton` 신설 (운영 중 NativeQueue 채널들과 동일 패턴). cell/worldPos/SO 메타 동봉. BattleBridge drain → visual destroy + VFX. enqueue 가 ECB destroy 보다 먼저 (UnitLifecycleSystem 의 DefenderDeath 분기와 동일).
- **Visual ⊥ Effects** — `BlockingHazardPresenter` (Presentation 계층). ECS hazard entity 와 직접 의존 X. BattleBridge 만 매개. path-zone 의 HazardPresenter 패턴과 동일 철학.
- **Hazard 는 반격 X** — AttackState 미부착, targetMask 0. 일방향 damage sink.
- **Taunt 메커니즘 없음** — 차단 (path-block) 으로 자연스러운 공격 유도. 적이 막혀 정지 → 사거리 안 자동 진입 → 공격. Taunt radius 등은 후속 (스킬/효과 요소).
- **Hazard entity 의 `LocalTransform.Position`** = 샘플된 cell 들의 *기하 중심* (center) 의 worldPosition. HP bar 렌더 anchor + 적 공격 사거리 시각 reference 로 쓰임. 단 적 공격 사거리 *판정* 은 가장 가까운 점유 cell 거리 (BlockingHazardCellsBuffer 순회).
- **1-frame blockedCells 잔존** — hazard 가 HP 0 → DamageApplicationSystem(이번 프레임) → DeadTag → UnitLifecycleSystem(이번 프레임) → entity destroy. ObstacleLifetimeSystem 은 이번 프레임 이미 실행됨 → 다음 프레임에 blockedCells 재구축. 즉 **hazard 부서진 직후 1 프레임은 blockedCells 에 죽은 cell 잔존**. Movement 가 그 1 프레임 동안 막힘 — 게임감상 무시 가능 (16ms). 명시 의도.
- **Scope rationale** — Faction 인프라 (Unit 0~2) 는 본 spec 의 11 unit 중 3개 (~30%). hazard 가 첫 새 faction 등장 시점이라 인프라 일반화 적기. 미루면 query 확장 코드 (`WithAny<DefenderUnitTag, BlockingHazard>`) 가 곧 폐기 — cc-pipeline Slow migration (Unit 2) 와 동일 패턴.

## 검증 질문 (= 종료 조건)

1. **게임감**: 차단 hazard 가 적 path 를 막고, 적이 자동으로 공격 → HP 0 → 부서짐. 부수는 동안 HP bar 가 시각 피드백. → Unit 9 PlayMode 사용자 확인.
2. **회귀 안정성**: Faction + targetMask 도입 후 디펜더↔적 공격 동작 동일. → Unit 2 회귀 게이트.

## 후속 후보 (현 spec 범위 밖)

- `Taunt` 컴포넌트 (radius 기반 어그로) — 스킬/효과 요소로 도입 시
- `Faction.Goal` / `Faction.FieldProp` / `Faction.Totem` 등 새 진영 (enum 추가)
- AttackSystem 의 두 loop 통합 (코드 중복 분석 후 별도 spec)
- AttackUnitTag/DefenderUnitTag 의 진영 식별 역할 폐기, FactionTag 일원화
- Hazard destruction on-effect (부서지면 zone hazard 생성 / 폭발 등 composition)
- BlockingHazardSO 의 추가 Inspector 필드 (반격 능력 / on-destroy effects)
- 균열 / 색조 변화 등 HP-비례 visual 변형 (mesh swap 또는 shader)
- 정식 VFX prefab (unity-vfx-authoring)
- 멀티 hazard 동시 spawn 부하 측정 후 incremental blockedCells 갱신
- Hazard 종 다양화 (Wall_1x3 / Cross / 큰 바위 등 shape 다양)
- Hazard SO 의 mask 필드 추가 — 디펜더가 일부 hazard 만 공격 가능 등 디자인 변형
