# Spec — Enemy Behavior Components

> 상태: 완료 2026-06-18 (Unit 0~4 + 6 적 근접 AoE) · handoff: `5_handoff_summary.md`
> 적 거동을 SO enum 필드로 명시 선택 → ECS 컴포넌트로 bake. 외형(mesh/spine)과 기능(공격/타게팅/이동)을 분리.

## 목표

적의 **거동(behavior)을 데이터로 조립**한다. `enemyClass`는 라벨/태그로 강등하고, 공격 방식·타게팅 모드·공격 중 이동·공격필터를 `AttackUnitData` 의 enum 필드로 명시 선택한다. 같은 외형이라도 SO 필드만 바꾸면 다른 기능이 되고, 한 클래스 안에서 변형(sub-variant)이 자유롭게 생긴다.

이전 spec(`enemy-class-system`, `aggro-targeting`)에서 후속으로 미뤘던 "클래스 거동 정식화 — 클래스는 상위 축, 행동은 sub-variant"의 구현.

## 검증 질문

> "외형을 바꾸지 않고 SO enum 필드만으로 walk-only / 근접 집중공격 / 이동사격 / 정지사격 적을 만들 수 있는가? enemyClass 에 박힌 거동 하드코딩이 사라졌는가?"

## feature-wide 계약 (load-bearing)

1. **거동은 SO enum 필드가 결정.** `enemyClass` 는 라벨일 뿐 거동을 파생하지 않는다. (Shooter→Ranger 필터 하드코딩 제거)
2. **거동 축 4개**: `attackMethod`(None/Melee/Projectile), `targetMode`(None/Nearest/FocusUntilDead), `aimMode`(StopToAttack/MoveAndShoot), `targetFilter`(classMask+priorityClass).
3. **attackMethod 가 attack 컴포넌트 부착을 결정**: None → `AttackState` 없음(walk-only). Melee → AttackState+outputs. Projectile → +ProjectileRef. (Tanker walk-only 여부도 이 enum 값일 뿐, 특수 케이스 아님.)
4. **거동 컴포넌트는 Combat 맥락 소유**: `EnemyBehavior{targetMode,aimMode}`, `FocusTarget{current}`, 기존 `EnemyTargetFilter`. AttackSystem(Combat)이 읽고 `FocusTarget` 을 쓴다.
5. **타게팅 우선순위**: 어그로(Aggroed) override > FocusUntilDead 고정 > Nearest+filter. 어그로 계약(aggro-targeting)을 깨지 않는다.
6. **aimMode 가 정지 여부를 결정**: StopToAttack 이고 `movePauseOnAttackSec > 0` 일 때 그 시간만큼 이동정지 / MoveAndShoot → 정지 안 함.
7. **적 전용.** 디펜더(`DefenderUnitData`)는 무관.
8. **수치는 SO authored.** 하드코딩 금지.
9. **FocusUntilDead lock 은 타겟이 죽을 때까지 유지**(사거리 밖이어도). 단 fire 경로엔 사거리 검사가 없으므로 focus 블록에서 **사거리 안일 때만 발사**(밖이면 발사 보류·lock 유지). 무효(사망/디스폰)면 nearest+filter 로 재선정.
10. **bake 방어**: `attackMethod` Melee/Projectile 이라도 `outputs` 가 비면 walk-only(AttackState 미부착) — 데미지-0 공격자 생성 금지. (미마이그레이션 기본값 안전)
11. **마이그레이션은 faithful/intentional 구분**: Basic·Needler·Rootcaster 는 의도된 거동 변경(focus-fire 등), 나머지는 현행 재현.

## 작업 단위

| 파일 | 작업 | 문서 | 목적 |
|---|---|---|---|
| 0 | enum + 컴포넌트 + SO 필드 | `0_enums-and-fields.md` | attackMethod/targetMode/aimMode, EnemyBehavior/FocusTarget, SO 필드 |
| 1 | SO 마이그레이션 | `1_so-migration.md` | **bake 전에** 6종 거동 필드 기입(중간 regression 방지) |
| 2 | BattleBridge bake | `2_bridge-baking.md` | attackMethod 분기(방어적), 필터 SO 이전, enemyClass 하드코딩 제거 |
| 3 | AttackSystem 소비 | `3_attacksystem-behavior.md` | FocusUntilDead(사거리 게이팅) + aimMode 정지 게이팅 |
| 4 | 테스트 + handoff | `4_tests_and_handoff.md` | EditMode(focus/사거리/walk-only/filter) + handoff |
| 6 | 적 근접 AoE | `6_enemy-aoe.md` | attackTargetCount SO 노출 + bake, 근접 적 2+ (Basic/Tanker=2) |

## 비목표 / 후속 후보

- **적 신규 클래스/유형 추가** (이 spec 은 기존 거동의 데이터화만; 새 적은 별도).
- **도발/어그로 수치, 밸런싱** — 밸런싱 spec.
- **디펜더 거동 컴포넌트화** — 필요 시 별도 spec.
- **공격 중 이동의 세부(kiting 경로 등)** — MoveAndShoot 는 "정지 안 함"까지만.
