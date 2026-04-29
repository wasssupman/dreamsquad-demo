# Destructible Blocking Hazards — Design

**작성일**: 2026-04-29
**Spec 폴더**: `docs/spec/destructible-blocking-hazards/`

## 목표

차단형 hazard (바위/벽 등) 에 HP 를 부여하여 적이 공격해 부수는 메커닉을 만든다. 동시에 "공격 가능 타겟" 추상화를 ECS 일반 인프라로 진화시킨다 — 디펜더 / 적 / hazard / 미래 props 가 단일 모델 (Faction + targetMask) 로 상호작용.

## 아키텍처 요약

### 데이터 모델

- **공격 타겟팅 일반화**:
  - `Faction` enum [Flags] — Defender / Enemy / BlockingHazard (3 비트, 미래 확장 여지)
  - `FactionTag : IComponentData { Faction value }` — 공격 타겟 후보 식별
  - `AttackState.targetMask : int` — attacker 가 공격 가능한 진영 비트마스크
- **Hazard entity 컴포넌트 조합** (Effects + Units 맥락):
  - `Obstacle` (cc-pipeline 재사용, remainingLife 미사용)
  - `BlockingHazard` (본 spec 신설 마커 + 메타)
  - `BlockingHazardCellsBuffer` (멀티셀 점유)
  - `Health` + `IncomingDamage` buffer (Units 재사용 — IDamageable 의 ECS 표현)
  - `HealthBarState` (재사용)
  - `FactionTag { value = BlockingHazard }`

### 핵심 설계 결정

1. **IDamageable 추상화 = ECS 컴포넌트 조합** — `Health` + `IncomingDamage` + `DamageApplicationSystem` 채널이 이미 entity-agnostic. 별도 인터페이스 신설 X.
2. **Tag 0개 신설 (DamageableTag X)** — 적 자가-타겟 위험. 대신 Faction + targetMask 로 진영 분리.
3. **AttackSystem refactor 최소화** — 두 loop (defender→attacker, enemy→defender) 통합 X. 각 loop 의 target query 만 `(target.faction & attacker.targetMask) != 0` 필터로 변경. 코드 중복 일부 유지 — buff/projectile/CC 분기가 두 loop 다름.
4. **AttackUnitTag/DefenderUnitTag 유지** — Movement/lifecycle/배치 식별에 쓰임. FactionTag 는 공격 타겟팅 전용 병행 추가.
5. **HP-only destruction** — `remainingLife` 미사용. 시간 소멸 메커닉은 zone hazard 의 정체성.
6. **Multi-cell 지원** — `HazardShapeSampler` 재사용 (path-zone-hazards 의 인프라 공유). 큰 바위 / 길이 막는 벽 등 게임감 핵심.
7. **Destruction 알림** — `HazardDestroyedEventsSingleton` (운영 중 NativeQueue 채널들과 동일 패턴, 8번째). cell/worldPos/sourceSO 메타 동봉. enqueue 가 ECB destroy 보다 먼저 (DefenderDeath 패턴).
8. **Visual** — `BlockingHazardPresenter` MonoBehaviour. BattleBridge 가 spawn/destroy 동기. HP bar = `HealthBarState` 인프라 재사용.

### 기존 인프라 재사용 / 진화

| 영역 | 재사용 / 진화 |
|---|---|
| `Health` + `IncomingDamage` + `DamageApplicationSystem` | 그대로 재사용 (entity-agnostic) |
| `DeadTag` + `UnitLifecycleSystem` | 마지막 dead loop (`WithNone<DefenderTile>`) 가 hazard 자동 처리 |
| `Obstacle` + `ObstacleSingleton.blockedCells` | ObstacleLifetimeSystem 만 멀티셀 buffer 지원 확장 |
| `HazardShapeSampler` (path-zone-hazards) | shape → cell list 변환 그대로 재사용 |
| 4 NativeQueue 채널 패턴 | 5번째 (`HazardDestroyedEventsSingleton`) 추가 |
| `HealthBarState` | hazard entity 부착, 인프라 그대로 |

## 검증 질문 (= 종료 조건)

1. 차단 hazard 가 적의 path 를 막고, 적이 자동으로 공격 → HP 0 → 부서짐. 게임감이 의도대로? (feature 가치)
2. Faction + targetMask 도입 후 디펜더↔적 공격 회귀 0? (refactor 안정성)

→ Unit 2 (회귀 게이트), Unit 9 (PlayMode 디버그 spawn → 적이 부수기) 의 사용자 확인이 두 질문에 답한다.

## 후속 후보

- `Taunt` 컴포넌트 (radius 기반 강제 어그로) — 스킬/효과 요소로 도입
- `Faction.Goal` / `Faction.FieldProp` 등 새 진영 (enum 추가)
- AttackSystem 의 두 loop 통합 (코드 중복 분석 후 별도 spec)
- AttackUnitTag/DefenderUnitTag 의 진영 식별 역할 폐기, FactionTag 로 일원화
- Hazard destruction on-effect (부서지면 zone hazard 생성 등 composition)
- BlockingHazardSO 의 Inspector 필드 (mask, hp 추가 필드 등)
- 균열 / 색조 변화 등 HP-비례 visual 변형
- 멀티 hazard 동시 spawn 부하 측정 후 incremental blockedCells 갱신
- 정식 VFX prefab (unity-vfx-authoring)

## 참조

- `docs/spec/cc-pipeline-and-obstacle/` — Obstacle 인프라 source
- `docs/spec/path-zone-hazards/` — HazardShapeSampler / Visual ⊥ Effects 패턴 source
- 본 세션 brainstorming Q1~Q6 — 결정 trace
