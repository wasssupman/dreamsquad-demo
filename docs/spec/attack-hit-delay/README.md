# attack-hit-delay — 공격 시작 후 타격 판정 지연

> 상태: 완료 2026-06-29 (units 0~2 · Play 검증 PASS · 양트랙 리뷰 APPROVE).

## 배경 / 문제

현재 공격은 발사 시점(쿨다운 0 + 사거리 안)에 **애니메이션과 데미지가 동시**(`AttackSystem`). 와인드업(예비동작)이 없어 스윙이 닿기 전에 데미지가 들어간다. 애니메이션 프레임 동기(Spine event)는 보류 — 대신 **시간(초) 기반 지연**으로 직관적으로 처리.

## 검증 질문

공격 시작 후 `hitDelaySec` 초가 지나 타격이 판정·적용되는가? `hitDelaySec=0` 이면 현행 즉시 그대로인가?

## 모델

"공격 시작 → `hitDelaySec` 후 타격 판정." (이동=타겟 도달 모델의 연장)
- **공격 시작(T)**: 애니메이션 트리거 + 쿨다운 리셋 + (적)이동 정지.
- **타격 판정(T + hitDelaySec)**: 사거리/타겟 **재판정** + 데미지/투사체/넉백.
- `hitDelaySec = 0` → 시작=판정 동시(현행 즉시).

## 공통 원칙 / 결정 (2026-06-29)

- `AttackState` 에 **runtime**(`hitDelayRemaining`) + **config**(`hitDelaySec`). (standoff 와 같은 패턴.)
- **쿨다운 기산 = 공격 시작(T).** 지연 중 새 공격 시작 안 함.
- **T+N 판정 = 재판정**(그 순간 사거리 안 최근접). 시작 타겟은 애니메이션 facing 용.
- melee(데미지)·projectile(투사체 스폰) **fire 레벨 균일** 적용. hazard caster/taunt 는 기본 0.

## 작업 단위

| # | 작업 | 문서 | 목적 |
|---|---|---|---|
| 0 | 필드 plumbing | `0_field_plumbing.md` | `AttackState`/`AttackUnitData`/`DefenderUnitData` hitDelaySec + baking. **기본 0, 동작 무변경.** compile |
| 1 | AttackSystem 재구성 | `1_attacksystem_split.md` | fire 분리(start/resolve) + 지연 tick. Play 검증 |
| 2 | 배치 지연 (deploy delay) | `2_deploy_delay.md` | `DefenderUnitData.deployDelaySec` → 배치 시 초기 `cooldownRemaining`. 그동안 idle(자동). AttackSystem 무수정. compile |

## 후속 후보

- **애니메이션 타격 프레임 동기** [M] · 시간(초) 대신 Spine 타격 event 로 판정. 현재는 시간 근사.
- **hazard caster cast delay** [S] · 캐스트류 별도 지연(현재 0).
- **unit 1 선결** (투트랙 리뷰 2026-06-29 B-L1/L2): unit 1 에서 `hitDelayRemaining` 를 실제 소비할 것 + `TauntAttackGrantSystem` 의 granted `AttackState` 에 `hitDelaySec` 세팅(현재 default 0, authoring 경로 없음). M1(standoff/발사 metric, aggro-standoff 후속)도 AttackSystem 손볼 때 같이 검토.
