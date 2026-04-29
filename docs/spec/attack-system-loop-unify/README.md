# AttackSystem Loop Unification Spec

**작성일**: 2026-04-29
**연결 문서**: `docs/spec/destructible-blocking-hazards/` (본 spec 의 후속 — Unit 2 가 target snapshot 만 통합, attacker loop 분리는 유지). 회귀 게이트 패턴은 cc-pipeline-and-obstacle 의 Slow migration (Unit 2) 와 동일.
**목표**: AttackSystem.cs 의 두 attacker loop (defender→target / enemy→target) 를 단일 loop 로 통합. attacker 진영별 분기는 모두 ComponentLookup.HasComponent 기반 자연 분기로 처리. 미래 새 attacker 진영 (토템 / 자동포 등) 추가 시 tag + targetMask 만 부여하면 자동 합류.

## 상태

작성 완료, 구현 대기 (2026-04-29).

## 구현 문서 목록

| # | 문서 | 목적 |
|---|---|---|
| 0 | `0_unified_attacker_loop.md` | AttackSystem.cs 의 두 loop 통합. attacker query = `WithAll<AttackState, LocalTransform> WithNone<PendingDeployment>`. attacker tag (DefenderUnitTag) 와 attacker-only 컴포넌트 (DamageBoost / CooldownReduction / Synergy / ProjectileRef / DefenderCcData) 는 ComponentLookup HasComponent 분기. **회귀 게이트** |
| 1 | `1_handoff_summary.md` | 구현 결과 + 검증 로그 + 후속 주의점 |

## 공통 원칙 (feature-wide 계약)

- **단일 attacker loop** — `AttackState + LocalTransform + WithNone<PendingDeployment>` 보유한 모든 entity 가 attacker. 진영 식별은 `FactionTag` (target query 만 — 본 spec 은 attacker query 변경).
- **Attacker 분기 = HasComponent lookup** — 각 attacker 진영별 동작 차이는 attacker entity 의 컴포넌트 보유로 자연 분기. 새 분기 추가 시 새 컴포넌트 + lookup 분기.
- **AttackUnitTag/DefenderUnitTag 의 attacker query 역할 폐기** — 본 spec 후 두 tag 는 Movement / lifecycle / 배치 / DefenderAttackEvent 식별 등의 역할만. 공격 query 는 attacker query 단일.
- **AttackState.targetMask = source of truth** — attacker 가 공격 가능한 진영. defender = `Faction.Enemy`, 적 = `Faction.Defender | Faction.BlockingHazard`. 본 spec 은 mask 정책 변경 X.
- **회귀 0 보장** — 통합 후 디펜더↔적 공격 / projectile / knockback / synergy / AoE 동작이 통합 전과 동일해야 함. PlayMode 회귀 게이트 + EditMode 단위 테스트로 검증.
- **DefenderAttackEvent enqueue 분기** — defender 만 enqueue (Spine animation 트리거). lookup 분기 = `defenderTagLookup.HasComponent(attackerEntity)` (또는 FactionTag.value == Defender).
- **Burst 호환 유지** — 추가 lookup 1~2개 (DefenderUnitTag tag lookup). switch / HasComponent 는 Burst 친화.

## 검증 질문 (= 종료 조건)

1. **회귀 안정성**: 통합 후 디펜더 적 공격 / 적 디펜더 공격 / hazard 공격 / projectile / knockback / synergy / AoE 동작이 통합 전과 동일. → Unit 0 회귀 게이트 (PlayMode 사용자 확인 + 기존 EditMode 테스트 149/149 회귀 0).
2. **코드 위생**: AttackSystem.cs 의 코드 줄 수 ↓, 두 loop 중복 제거. 미래 새 attacker 진영 추가가 tag + mask 부여만으로 가능. → Unit 0 코드 리뷰.

## 후속 후보 (현 spec 범위 밖)

- AttackUnitTag/DefenderUnitTag 의 진영 식별 역할 완전 폐기 (Movement / lifecycle / DefenderAttackEvent 등 다른 사용처도 FactionTag 로 일원화) — 별도 spec, 영향 범위 큼
- Faction 추가 진영 (Goal / FieldProp / Totem) — hazard 외 새 attacker 진영이 실제 도입되는 시점
- Attacker 진영별 별도 SystemGroup 분리 (defender SimulationGroup / enemy SimulationGroup) — 부하 측정 후
- AttackState refactor (damage / range / cooldown / mask 외 추가 필드 검토) — 새 attack 메커닉 도입 시
