# Kickoff Handoff — AttackSystem Loop Unification

**Status**: spec 작성 완료, 구현 미착수.
**Spec 폴더**: `docs/spec/attack-system-loop-unify/` (README + Unit 0 + handoff placeholder).
**작성**: 2026-04-29.
**다음 작업자**: Codex CLI (구현). 사용자 직접 호출.
**전제 spec**: `docs/spec/destructible-blocking-hazards/` (Faction + targetMask 도입, target snapshot query 통합 완료, 커밋 `3f5ab31`).

## 본 spec 의 자리

`AttackSystem.cs` 의 두 attacker loop (defender / enemy) 를 단일 loop 로 통합. 코드 위생 + 미래 새 attacker 진영 추가의 일반화. 게임 동작 변화 0 (순수 refactor + 회귀 게이트).

## 핵심 결정 요약

| 결정 | 채택 | 이유 |
|---|---|---|
| Attacker query | `WithAll<AttackState, LocalTransform> WithNone<PendingDeployment>` (단일) | 두 loop 의 차이는 모두 lookup HasComponent 로 환원 가능. tag 만이 분리 이유였음 |
| Defender 식별 | `defenderTagLookup.HasComponent(attackerEntity)` | DefenderAttackEvent enqueue 분기. tag 자체를 attacker query 에서 빼고 lookup 으로 |
| AttackUnitTag/DefenderUnitTag | 다른 용도 (Movement/lifecycle/배치/event emit) 로 유지 | 진영 식별 일원화는 별도 spec (영향 범위 큼) |
| Buff/projectile/CC 분기 | 기존 HasComponent lookup 패턴 그대로 | 적은 해당 컴포넌트 미부착이라 자동 skip — 동작 동일 |
| 회귀 검증 | EditMode unified loop 테스트 + PlayMode 사용자 확인 (회귀 게이트) | cc-pipeline Slow migration / destructible-blocking-hazards Unit 2 패턴 |

## 절대 보존 (되돌리지 말 것)

- `AttackState.targetMask` 정책 (defender = `Enemy`, 적 = `Defender | BlockingHazard`) — 본 spec 변경 X.
- `LocalTransform` writer = MovementSystem 단독 (불변).
- `AttackUnitTag` / `DefenderUnitTag` 다른 사용처 — Movement (Unit lifecycle) / DefenderTile / DefenderAttackEvent emit / HealthBar 식별 등에 그대로 쓰임. 본 spec 은 *attacker query* 역할만 폐기.
- DefenderAttackEvent enqueue 가 defender 만 — Spine animation 트리거 정책 보존.
- Knockback CC (DefenderCcData) 가 defender 만 — 적 knockback CC 미발동 정책 보존.
- AoE (attackTargetCount > 1) 가 defender 만 의미 있음 — 적은 1 default. 본 spec 변경 X.
- Projectile (ProjectileRef) 가 defender 만 — 적은 미부착. 본 spec 변경 X.
- 운영 중 NativeQueue 채널 7개 lifecycle 변경 X.

## 작업 시 주의

### 통합 시 주의 분기

- `WithNone<PendingDeployment>` 는 **단일 query 에 추가** — defender 의 배치 대기 제외 정책 보존. 적은 PendingDeployment 미부착이라 무관.
- `defenderTagLookup` 만 추가 — `DamageBoost` / `CooldownReduction` / `SynergyBuff` / `ProjectileRef` / `DefenderCcData` 는 이미 lookup 상태 유지.
- variable 이름: 통합 후 `defenderEntity` → `attackerEntity` 일괄 변경. 코드 의미 명확.
- AoE branch (desiredCount > 1) 의 hitMask 로직 그대로 보존 — 적이 AoE 가지면 자동 합류 (현재는 미사용).

### Burst 호환

- `defenderTagLookup` 추가는 Burst 친화 — `IComponentData` empty struct 의 lookup 도 Burst 컴파일.
- foreach loop 자체는 ISystem + `[BurstCompile]` 유지.

### 회귀 검증 시나리오

기존 destructible-blocking-hazards V1~V6 + 본 spec 추가 시나리오:

| # | 시나리오 | 기대 결과 |
|---|---|---|
| U1 | 디펜더 (Archer, ProjectileRef 보유) → 적 공격 | projectile spawn → hit → 적 사망 동일 |
| U2 | 디펜더 (Bastion, melee) → 적 공격 (AoE attackTargetCount=2) | 두 적 동시 hit 동일 |
| U3 | 디펜더 (knockback DefenderCcData) → 적 공격 | EnemyCcEvent enqueue + 적 knockback 동일 |
| U4 | 디펜더 (Synergy / DamageBoost / CooldownReduction 보유) | 데미지 / 쿨다운 동일 |
| U5 | 적 (Enemy_Debug_Melee_Attacker) → 디펜더 공격 | 디펜더 IncomingDamage + 사망 동일 |
| U6 | 적 → hazard 공격 | hazard HP 감소 + 부서짐 동일 (destructible V1 동치) |
| U7 | 디펜더의 Spine attack animation 트리거 | DefenderAttackEvent enqueue → Pool drain → animation 재생 동일 |
| U8 | 적 fire 시 DefenderAttackEvent **enqueue 0** | 적은 attack event emit 안 함 (회귀 검증) |

## 사용자 확인 protocol

각 unit commit 후:
- **Unit 0 ★ (회귀 게이트)**: PlayMode U1~U8 시나리오 사용자 manual 확인. EditMode unified loop 테스트 통과. 사용자 통과 후 spec 종료.
- **Unit 1 (handoff)**: 구현 후 채우고 commit.

각 unit 완료 후 해당 작업 단위 파일의 "완료 기준" 섹션 하단에 확인 일자 + 커밋 해시 한 줄 추가.

## 작업 시작점

`docs/spec/attack-system-loop-unify/0_unified_attacker_loop.md` 를 읽고 그 파일만 가지고 Unit 0 작업 진행. 통합 코드 골격 + 잠재 회귀 벡터 표 + 검증 시나리오 다 명시. README.md 의 공통 원칙 + 본 handoff 의 "절대 보존" 섹션을 상시 컨텍스트로 유지.

## 참조 spec (의존)

- `docs/spec/destructible-blocking-hazards/` — Faction + targetMask + target snapshot 통합 source. 본 spec 은 그 후속 (attacker side 통합).
- `docs/spec/cc-pipeline-and-obstacle/` — Slow migration (Unit 2) 패턴 — 동일 회귀 게이트 방식.
