# AttackSystem Loop Unification — Handoff Summary

**완료일**: 2026-04-29
**상태**: 구현 완료 + EditMode 회귀 0 + PlayMode 회귀 게이트 사용자 확인 통과.

## Commit

| 범위 | 해시 | 설명 |
|---|---|---|
| spec docs | `1b188ea` | docs: add spec — README + Unit 0 + handoff placeholder + _session_handoff. Critic 1회 ACCEPT WITH MINOR FIXES (3 항목 반영) |
| Unit 0 (impl) | `ccc2873` | feat: unified attacker loop — 두 loop 통합, defenderTagLookup 도입, 변수명 일괄 rename, AttackSystemUnifiedLoopTests 11 케이스 |

## Implemented

- AttackSystem.cs 의 두 attacker loop (defender / enemy) → 단일 loop 통합
- attacker query: `WithAll<AttackState, LocalTransform> WithNone<PendingDeployment>` (단일)
- attacker tag (`DefenderUnitTag` / `AttackUnitTag`) attacker query 에서 제거 → defender 식별은 `defenderTagLookup.HasComponent(attackerEntity)` 분기
- DefenderAttackEvent enqueue (Spine animation 트리거) 가 defender 만 — 정책 보존
- Knockback CC (DefenderCcData) 가 defender 만 — 정책 보존
- Buff (DamageBoost / CooldownReduction / Synergy) / Projectile (ProjectileRef) 분기 = 기존 HasComponent lookup 그대로
- 변수명 일괄: `defenderEntity` → `attackerEntity`, `defPos` → `atkPos` (AoE inner-loop self-exclusion 포함)
- AttackUnitTag/DefenderUnitTag 의 다른 사용처 (Movement / lifecycle / DefenderTile / DefenderAttackEvent emit / HealthBar 식별) 변경 0
- 코드 줄 수 감소: 344 → 306 (-38)

## Key Files

Combat/: AttackSystem.cs (두 loop → 단일)

Tests/: AttackSystemUnifiedLoopTests.cs (11 케이스)

## Verified

- 컴파일 + Burst 활성
- EditMode 155/155 통과 (회귀 0 + 신규 unified loop 테스트 11/11)
- PlayMode 사용자 확인 통과 (U1~U8 시나리오 — 디펜더 projectile/melee/AoE/knockback/synergy/DamageBoost/CooldownReduction, 적 → 디펜더/hazard 공격, DefenderAttackEvent enqueue 정책 동일)
- LocalTransform writer 단독 = MovementSystem
- 콘솔 에러 0

## Notes

- **EditMode 155 vs destructible-blocking-hazards 시점 149**: 본 spec 진입 시점에 카운트가 144 였을 가능성 (테스트 정리/재배치). 신규 11 추가 후 155. 회귀 게이트 통과 자체는 영향 0.
- **defenderTagLookup**: `DefenderUnitTag` empty struct 의 `ComponentLookup<>.HasComponent` 가 Burst 친화 (archetype 체크). 추가 lookup 1개 외 부하 영향 미미.
- **DefenderAttackEvent.defender 필드 이름 보존**: 이벤트 구조체의 필드 이름은 `defender` 그대로 — 의미상 적은 emit 안 하니 일관. 변경 시 drain 측 (BattleBridge) 영향 → 본 spec 범위 밖.

## Follow-up

- AttackUnitTag / DefenderUnitTag 의 진영 식별 역할 완전 폐기 (Movement / lifecycle / 배치 / DefenderAttackEvent 식별을 FactionTag 기반으로 일원화) — 별도 spec, 영향 범위 큼
- Faction 추가 진영 (Goal / FieldProp / Totem) — 새 attacker 진영 도입 시
- Attacker 진영별 별도 SystemGroup 분리 (defender / enemy) — 부하 측정 후
- AttackState refactor (damage / range / cooldown / mask 외 추가 필드) — 새 attack 메커닉 도입 시
- 적 ProjectileRef 도입 시 projectile spawn 분기 정책 명시 (현재는 적 미부착 가정)
