# Goal Stability — 목표지점 안정도 (꿈결 안정도)

**상태: 초안 2026-08-04 (사용자 승인 대기)**

## 목표

목표지점(goal)이 **안정도(Stability)** 를 가진다. 컨셉: 꿈속 세계 "꿈결"의 안정을 지키는 수호 지점 — 적(악몽)은 꿈결의 안정을 무너뜨리려 목표지점을 직접 공격한다.

- **최대 안정도 M = 0 (기본)**: 현행 유지 — 적이 골 셀에 도달하면 유출(스트레스 +1, 소멸).
- **M > 0**: 골이 전투 대상 엔티티가 된다. 적은 유출하지 않고 골 앞에 멈춰 **공성**한다. 모든 적(공격 능력 없는 walk-only 포함)이 골을 공격 가능한 **최후순위 대상**으로 인식한다.
- **안정도 0 = 붕괴**: 골 엔티티가 파괴되고 그 골은 **현행 유출 지점으로 전환**된다 — 공성 중이던 적들이 진입하며 스트레스가 오르는 기존 구조로 이어진다(2026-08-04 사용자 결정. 붕괴 시점 즉시 스트레스 보너스는 없음).

명칭 통일: 사용자-facing/authoring = **안정도**, 구어 = "체력". 심 내부는 기존 `Health` 컴포넌트를 그대로 재사용한다(새 수치 타입을 만들지 않는다).

## 왜 contained feature 인가

- **파괴 가능 blocking hazard 가 완성된 선례다**: `FactionTag + Health + IncomingDamage + LocalTransform` 만으로 `AttackSystem` 후보 스냅샷(`Combat/AttackSystem.cs` QueryBuilder)에 자동 진입한다. 골 엔티티는 이 아키타입의 두 번째 인스턴스.
- **회귀 안전 = 무형 롤아웃**(multi-goal-map 과 같은 결): M=0 이면 골 엔티티를 스폰하지 않으므로 기존 맵은 1비트도 달라지지 않는다. authoring 폴백(배열 부재/길이 불일치 → 전 골 0)으로 기존 5맵 무마이그레이션 통과.
- 점수 산식·스트레스 예산은 **무변경**. 공성 중 스트레스 미누적은 구조적 결과(유출 이벤트가 안 생김)이지 산식 변경이 아니다.

## 작업 단위 목록

| # | 작업 구분 | 문서 | 목적 |
|---|---|---|---|
| 0 | Data/Editor | `0_stability_authoring.md` | `MapDocument.goalMaxStability[]` per-goal 안정도 authoring + 폴백 + 왕복 테스트 + MapPainter |
| 1 | ECS(Units) | `1_goal_entity_and_faction.md` | `Faction.Goal` 신설 + `GoalPoint` 골 엔티티 스폰/teardown (행동 변화 0) |
| 2 | ECS(Movement/Combat) | `2_siege_gate_and_melee.md` | 공성 게이트(유출 억제) + 적 targetMask 개통 + goal 최후순위 타겟팅 → 근접 공성 |
| 3 | ECS(Combat) | `3_ranged_and_walkonly.md` | 원거리(투사체) 골 피격 개통 + walk-only 적 공격 부여 |
| 4 | ECS(Units)+Bridge | `4_collapse_event.md` | 붕괴 이벤트 채널(`GoalCollapsedEventsSingleton`) + 유출 지점 전환 검증 |
| 5 | Presentation | `5_stability_view.md` | 안정도 게이지 + 붕괴 VFX + 씬 wiring |
| 6 | Handoff | `6_handoff_summary.md` | 인계 (종료 시) |

## Feature-wide 계약

- **M 은 `MapDocument` 에서만 온다** (하드코딩 금지, 절대 제약 6). per-goal 배열, `goals` 와 index 정렬. 부재/길이 불일치 → 전 골 0 폴백.
- **M=0 골 = 엔티티 미스폰 = 현행 완전 동일.** 모든 소비 지점은 "골 엔티티 존재 여부"로 분기하고 별도 플래그를 두지 않는다 — **붕괴(엔티티 파괴)가 곧 게이트 해제 신호**다.
- **`Faction.Goal` 신설** (2026-08-04 사용자 결정): 적 targetMask 에 OR. 힐러(최저체력)·시너지·아군 지원 시스템은 `Faction.Defender` 만 보므로 골을 자연 배제한다. 골 힐은 후속 후보.
- **goal 은 타겟 최후순위**: 사거리 내 다른 유효 대상(defender/blocking hazard)이 있으면 그쪽을 먼저 친다. 골만 사거리에 있을 때 골을 친다. (`AttackSystem` nearest 스캔과 `EnemyAiStateSystem.HasFireTarget` 미러를 함께 갱신 — 미러 불일치 경고 주석 준수.)
- **공성 = PastGoalTag 억제**: `MovementSystem` 의 골 도달 게이트(`!hunting && !patrolling`)에 세 번째 조건 "그 셀의 골 엔티티가 살아있으면 미부착"을 추가한다. 골 엔티티 존재 판정은 `GoalPoint` 쿼리(맵당 ≤4)로 매 프레임 재구축 — 싱글턴 동기화 없음.
- **모든 적이 골 공격 가능**: 공격 능력 있는 10종은 targetMask OR 로 개통. walk-only 2종(Runner/Swift)은 `AggroAttackProfile` 수치를 재사용한 스폰 시 grant(도발 공격 프로필 선례). 도발과의 병존 규칙은 unit 3.
- **`GoalPoint` 는 Units 소유**: 골 엔티티의 정의·Health·생성/소멸이 Units 관할(유닛 정의/Health/생성·소멸 헌장). Movement/Combat 은 읽기만. 붕괴 이벤트는 `UnitLifecycleSystem` 이 발행(hazard-dead 루프 동형, **general-dead 루프보다 먼저** enqueue→destroy).
- **신규 NativeQueue 1개**: `GoalCollapsedEventsSingleton`(Units→Bridge, 28번째 채널). 생성·drain·Dispose 3종 확인.
- **점수 산식 무변경**: `ScoreRules`/`defeatGoalReachedCount` 그대로. 붕괴 후 유출이 재개되면 기존 스트레스 경로가 그대로 작동한다.
- **골은 CC·모디파이어의 대상이 아니다**: 골 엔티티에 `CcEffect`/`StatModifierSlot` 버퍼를 부여하지 않는다. 현재 모든 CC/모디파이어 생산 경로는 골을 대상으로 삼지 않지만, `CcApplySystem` 은 버퍼 부재 시 crash 하는 전제가 있으므로 **향후 골 대상 CC/디버프를 추가하려면 이 계약부터 재검토**한다(리뷰 residual).
- **FocusUntilDead 는 골을 잠그지 않는다**: 최후순위 계약 유지 — 상세는 unit 2.

## 파이프라인 커버리지

가장 가까운 아키타입 = **해저드(Blocking)** (`docs/reference/object-pipeline-map.md`). 대조:

| 정거장 | 앵커 | 확인 포인트 |
|---|---|---|
| 데이터 SO | `Data/MapGrid/MapDocument.cs` `goalMaxStability[]` (신설) | 전용 SO 없음 — 맵 에셋이 per-goal M 소유. MapPainter 가 bake |
| 스폰 진입점 | `Bridge/BattleBridge.cs` `BuildFlowField` 직후 골 엔티티 생성 (신설) | ★Mono 주도 — ECS request 왕복 없음(스킬 해저드와 동형). teardown = `DestroyEntitiesByType<GoalPoint>` |
| ECS 컴포넌트 (Units) | `Battle/Units/GoalPoint.cs` (신설) + FactionTag{Goal}·Health·IncomingDamage·LocalTransform | 이동 없음 — PathFollowState 미부여. 멀티셀 아님(골 1칸) → BlockingHazardCellsBuffer N/A |
| 시뮬 시스템 | `Battle/Combat/AttackSystem.cs`(피격) · `Battle/Units/DamageApplicationSystem.cs`·`HealthDeathSystem.cs`·`UnitLifecycleSystem.cs`(붕괴 루프) · `Battle/Movement/MovementSystem.cs`(공성 게이트) | 공격 안 함 — AttackState 미부여 |
| 이벤트 큐 | `Battle/Units/GoalCollapsedEventsSingleton.cs` (신규 1) | 생성·drain·Dispose 3종. enqueue 는 DestroyEntity 앞 |
| View/Pool | 기존 골 구조물 프랍(`Core/TilemapMapView.cs` `goalStructureProp`) + 안정도 게이지 | Pool N/A — 맵당 ≤4, 맵 수명과 동일 |
| 체력 표시 | 타일 게이지 재사용 검토(unit-health-display 후속 후보 소화) | ★큐 아님 — Health read-only 폴링 |
| 씬 wiring | BattleBridge 게이지/붕괴 VFX 슬롯 | `unity-feature-wiring` 스킬. unit 5 |

## 리뷰 매칭

- unit 1~4 = ECS 시뮬 변경 → **ecs-reviewer**. unit 0(Data/Editor)·5(Mono 뷰) → 일반 리뷰.

## 후속 후보

- 안정도 잔량 점수화 / 스트레스 예산 재균형 (`docs/reference/score-formula.md` "한계와 점당 점수는 같이 움직여야 한다" 경고 — 공성 전환으로 유출 빈도가 줄면 재균형 검토).
- 골 피격 데미지 넘버 (`DamageApplicationSystem` 의 `AttackUnitTag` 필터 확장).
- 힐러/스킬의 안정도 회복 (골 힐 — Faction 마스크 확장).
- 붕괴 프랍 교체/파괴 상태 아트 (v1 은 VFX 1회 + 유지).
- HUD 전역 안정도 표시 (v1 은 골 위 게이지만).
- 붕괴 시 즉시 스트레스 보너스 knob (현재는 없음 — 사용자 결정 반영).
- 스폰 예고 라인의 공성 반영 (예고 경로 끝 표현 차별화).
