# Unit 1 — 기존 6종 SO 거동 필드 채우기

## 목적

bake 스위치(Unit 2)가 거동 필드를 소비하기 **전에** 6종 적 SO 에 거동 값을 명시해, 중간 커밋에서 미마이그레이션 기본값(Melee)으로 인한 regression(러너/슈터 오작동)을 막는다. (Critic C1)

## 변경 대상

- `Assets/_Project/Data/Enemies/Enemy_*.asset` (6종) — Unit 0 에서 추가한 필드에 값 기입

## 구현 (거동 필드 값)

**faithful = 현행 재현 / intentional = 의도된 변경** 명시.

| 적 | attackMethod | targetMode | aimMode | priorityClass | 분류 |
|---|---|---|---|---|---|
| Runner | None | None | StopToAttack | None | faithful (walk-only) |
| Swift | None | None | StopToAttack | None | faithful (walk-only) |
| Tanker | **Melee** | Nearest | StopToAttack | None | faithful (현 dmg 20 유지; walk-only 원하면 None 한 줄 변경 — 특수케이스 아님) |
| Needler | Projectile | FocusUntilDead | MoveAndShoot | None | **intentional** (이동사격; Nearest→Focus) |
| Rootcaster | Projectile | FocusUntilDead | StopToAttack | Ranger | **intentional** (정지캐스트 + Ranger 우선; 하드코딩 대체) |
| Basic | Melee | FocusUntilDead | StopToAttack | None | **intentional** (Nearest→focus-fire) |

- `targetClassMask` 전부 `Everything` 기본.
- **Runner/Swift 의 `aggroAttackDamage`(도발) 값은 그대로 유지** — 어그로 도발 경로는 attackMethod 와 무관(Unit 2 에서 미변경).
- Tanker `attackMethod` 기본값은 **Melee 로 고정**(현행 dmg 보존). None 은 의도적 walk-only 토글로만.

## 완료 기준

- [x] 6종 .asset 거동 필드 reflection 확인 (Runner None/None/Stop, Tanker Melee/Nearest, Needler Proj/Focus/MoveAndShoot, Rootcaster Proj/Focus/Stop/Ranger, Basic Melee/Focus).
- [x] 이 시점엔 bake 미변경(Unit 2 전)이므로 런타임 동작 불변 — 데이터만 기입.
- [x] 컴파일/역직렬화 회귀 없음(필드 추가만).

완료: 2026-06-18 / 커밋 해시 `<unit1-commit>`
