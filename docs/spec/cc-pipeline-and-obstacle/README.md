# CC Pipeline & Obstacle Spec

**작성일**: 2026-04-28
**연결 문서**: 없음 (새 spec)
**목표**: 적 이동에 가벼운 ECS 임펄스를 도입하여 (1) 디펜더 공격 넉백, (2) 디펜더 배치 시 밀어내기, (3) 디버그-spawn 큐브에 적이 막혀 멈추는 동작을 구현한다. 기존 `SlowEffect` 도 통일된 `CcEffect` buffer 로 마이그레이션하여 Stun/Root/Reverse/Pull/Push 등 향후 CC 확장이 enum + switch case 추가만으로 가능하도록 한다.

## 상태

**완료 2026-04-29** — Unit 0~9 전체 구현, PlayMode 시나리오 1~4 사용자 확인, 133/133 EditMode 테스트 통과.

## 구현 문서 목록

| 작업 구분 | 문서 | 목적 |
|---|---|---|
| 0 | `0_cc_data_model.md` | `CcKind` enum, `CcEffect` IBufferElementData, `EnemyCcEvents` 큐 싱글턴 |
| 1 | `1_cc_apply_decay_systems.md` | `CcApplySystem` (큐→buffer merge) + `CcDecaySystem` (tick + remove) |
| 2 | `2_slow_migration.md` | `SlowEffect` 제거, `EffectSpawner.ApplySlow` 시그니처 유지하며 내부만 buffer 로. `EffectTickSystem` Slow 루프 삭제. MovementSystem 가 buffer 읽기로 전환 (**회귀 게이트**) |
| 3 | `3_impulse_movement_compose.md` | MovementSystem switch 에 `CcKind.Impulse` 케이스 추가 |
| 4 | `4_defender_so_fields.md` | DefenderSO 에 knockback / on-place push 5필드 추가 (default 0) |
| 5 | `5_combat_knockback_hook.md` | CombatSystem 데미지 적용 후 SO `knockbackDistance > 0` 면 CcEvent enqueue |
| 5b | `5b_path_wall_trim.md` | path-wall trim patch — 경로 외 셀 차단 (`MovementCellTrim.cs` 신설). FlowField walkability source of truth. Unit 8 가 확장 사용 |
| 6 | `6_on_place_push_hook.md` | 배치 파이프라인에서 SO `onPlacePushDistance > 0` 면 radius 안 적들에 enqueue |
| 7 | `7_obstacle_data_and_lifetime.md` | `Obstacle` 컴포넌트, `ObstacleSingleton`, `ObstacleLifetimeSystem` |
| 8 | `8_movement_obstacle_trim.md` | 5b 의 `MovementCellTrim` 을 확장하여 큐브 셀도 wall 로 취급. trim 알고리즘은 5b 그대로 재사용 |
| 9 | `9_debug_spawn_entry.md` | `EffectSpawner.SpawnObstacle` + BattleBridge 디버그 메서드 + Editor 메뉴 (**feature 게이트**) |

## 공통 원칙 (feature-wide 계약)

- Effects 맥락 내 displacement/multiplier 형 CC 패밀리는 단일 `DynamicBuffer<CcEffect>` + `CcKind` enum 으로 통일한다.
- `CcEffect.vector` 는 displacement-form CC (Impulse 등) 가 채운다. `CcEffect.scalar` 는 multiplier-form CC (Slow 등) 가 채운다. kind 별 슬롯 사용 컨벤션은 `0_cc_data_model.md` 의 표를 참조.
- `LocalTransform` writer 는 MovementSystem 단독이다 (불변).
- `CcEffect` buffer 와 `ObstacleSingleton.blockedCells` 는 Effects 맥락 소유. Movement 는 read-only.
- CC 의 외부 진입점은 `EffectSpawner.ApplyCc` / `EnqueueCcEvent` 로 통일. 기존 `EffectSpawner.ApplySlow` 시그니처는 thin wrapper 로 보존하여 BattleBridge 등 호출자 무수정.
- Obstacle 은 단일 셀 (1×1) 점유, 시간 기반 소멸. HP/Taunt 없음.
- MovementSystem 의 cell trim 은 flow 변위와 impulse 변위 모두에 적용된다. 차단 대상 = (경로 외 셀 ∪ 큐브 셀). **경로 walkability 의 source of truth 는 FlowField — 영벡터 셀 = 벽, goal 셀은 예외, OOB 도 wall** (Unit 5b 에서 확정).
- FlowFieldBuilder 는 `ObstacleSingleton` 을 참조하지 않는다 (큐브 때문에 재경로 안 함).
- 적은 본 spec 범위에서 공격 능력을 가지지 않는다 (HP/Taunt/적 공격 시스템은 후속 후보).

## 검증 질문 (= 종료 조건)

1. 넉백 / 배치 push / 큐브 차단의 게임감이 의도대로 나오는가? (feature 가치)
2. Slow 회귀 없이 `CcEffect` buffer 통일이 안정적으로 동작하는가? (refactor 안정성)

→ Unit 2 (Slow 회귀), Unit 5b (경로 안에 갇힘), Unit 9 (큐브 + knockback × cube) 의 PlayMode 사용자 확인이 두 질문에 답한다.

## 후속 후보 (현 spec 범위 밖)

- 적의 큐브 공격 (HP + Taunt) — 별도 spec (적 공격 시스템 신설)
- 멀티셀 큐브 (큐브 시각 크기 ≠ 점유 셀 크기 분리)
- 적-적 분산/충돌 (여러 적이 큐브 앞에 겹쳐 보임 처리)
- 큐브 spawn 의 실제 게임 통합 (디펜더 능력 / 카드 / UI)
- Stun / Root / Reverse / Pull / Push 등 추가 `CcKind` (enum + switch case 만 추가)
- Presentation 흔들림 VFX (큐브 닿은 적의 시각 연출 강화)
- `blockedCells` incremental 갱신 (큐브 수 ↑ 시 부하 측정 후)
