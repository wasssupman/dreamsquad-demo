# AttackSystem Loop Unification — Handoff Summary

**완료일**: TBD (구현 완료 후 채움)
**상태**: 작성 대기 — Unit 0 구현 + 회귀 검증 후 본 파일 갱신.

## Commit

| 범위 | 해시 | 설명 |
|---|---|---|
| spec docs | TBD | docs: add spec |
| Unit 0 (impl) | TBD | feat: unified attacker loop + regression test |

## Implemented (구현 후 작성)

- AttackSystem.cs 의 두 attacker loop (defender / enemy) → 단일 loop 통합
- attacker tag 분기 → `ComponentLookup.HasComponent` 기반 자연 분기 (`defenderTagLookup` 추가)
- AttackUnitTag/DefenderUnitTag 의 attacker query 역할 제거 (다른 사용처 — Movement / lifecycle / 배치 / DefenderAttackEvent emit — 은 그대로 유지)
- 코드 줄 수 감소 확인 (구체 수치는 구현 후 기록)

## Key Files (구현 후 작성)

Combat/: AttackSystem.cs (두 loop → 단일)

Tests/: AttackSystemUnifiedLoopTests

## Verified (구현 후 작성)

- 컴파일 + Burst 활성
- EditMode N/N 통과 + 기존 149/149 회귀 0
- PlayMode 사용자 확인 통과 — 디펜더 적 공격 / 적 디펜더 공격 / 적 hazard 공격 / projectile / melee / knockback / synergy / DamageBoost / AoE 모두 동일
- LocalTransform writer 단독 = MovementSystem
- 콘솔 에러 0

## Notes (구현 중 발생 시 작성)

- (회귀 / 의도 / 경계 조건 — 구현 중 발견되는 것들)

## Follow-up

- AttackUnitTag / DefenderUnitTag 의 진영 식별 역할 완전 폐기 (Movement / lifecycle / 배치 / DefenderAttackEvent 식별을 FactionTag 기반으로 일원화) — 별도 spec, 영향 범위 큼
- Faction 추가 진영 (Goal / FieldProp / Totem) — 새 attacker 진영 도입 시
- Attacker 진영별 별도 SystemGroup 분리 (defender SimulationGroup / enemy SimulationGroup) — 부하 측정 후
- AttackState refactor (damage / range / cooldown / mask 외 추가 필드 검토) — 새 attack 메커닉 도입 시
- 적 ProjectileRef 도입 시 projectile spawn 분기 정책 명시 (현재는 적 미부착 가정)
