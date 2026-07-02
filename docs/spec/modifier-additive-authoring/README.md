# Modifier Additive Authoring (Policy B)

상태: **진행 중 (2026-07-03 착수)**

## 배경

`modifier-stacking-policy`(클램프) 후속. 표준 방식을 **방향별**로 적용한다: 증가(버프)는 가산으로 결합(런어웨이 방지), 감소(디버프)는 곱연산+floor 유지(체감감소). 조사 결과 무한 누적은 이미 클램프된 Debuffer 하나뿐이고 self-buff는 용도별 단일 슬롯이나, 버프끼리 곱연산 결합(1.3×1.2)을 가산(1+0.3+0.2)으로 바꿔 PoE식 예측 가능성을 확보한다.

사용자 결정 (2026-07-03): **B** — 버프만 additive, 디버프는 곱연산 유지. shim 없이 자연 델타 저작.

## 정책

집계식 `(1+Σadd)×Π(mul)`은 이미 이 정책을 지원(무변경). 생산 choke-point에서 modifier를 분류한다:

```
op        = multiplier >= 1 ? Additive       : Multiplicative
magnitude = multiplier >= 1 ? (multiplier-1) : multiplier
```

- **증가(≥1)** → Additive 델타. 1스택 값 동일(×1.3 = 1+0.3), 다중 슬롯만 곱→합.
- **감소(<1)** → Multiplicative. 손 안 댐(Debuffer/slow/저항 = 곱연산+floor 유지).

## 범위

- **대상**: BattleBridge 코드 emitter 5종(`EnqueueDamageMul`/`EnqueueAttackSpeedMul`/`EnqueueMoveSpeedMul`/`EnqueueSynergyMul`/`EnqueueStatMul`)을 중앙 헬퍼로 통일해 규칙 적용. 값이 <1인 slow/저항은 규칙이 자동으로 Multiplicative 유지.
- **비목표**: EffectTile(`ApplyEffectTileIfAny`, 3119)은 `EffectTileData.op` 명시 저작을 존중하는 별도 설계 — 이번 규칙 미적용(향후 버프 타일은 Additive 저작 권장, 후속). Debuffer/Zone slow SO는 이미 감소라 무변경.

## 작업 단위

| 번호 | 구분 | 문서 | 목적 |
|---|---|---|---|
| 0 | 계약 (docs) | `0_policy_contract.md` | 증가/감소 분류 규칙 + 단일-스택 등가 확정 |
| 1 | 구현 | `1_authoring_rule_and_helpers.md` | `ModifierAuthoring.FromMultiplier` 순수함수 + 5 헬퍼 라우팅 + 단위 테스트 |
| 2 | 테스트 정합 | `2_test_realignment.md` | 버프를 곱연산으로 재현하던 기존 테스트를 Additive shape으로 갱신 |

## Feature-wide 계약

- 증가는 가산, 감소는 곱연산 — 값(≥1/<1) 기준 분류가 정책의 정의(자의적 분기 아님).
- 감소 경로(Debuffer/slow/저항)는 무변경. 곱연산 diminishing + `modifier-stacking-policy` floor 그대로.
- 단일 스택 값 불변, 다중 버프 슬롯 결합만 곱→합.
- EffectTile은 명시 op 저작 유지(범위 밖).

## 후속 후보

- **버프 EffectTile Additive 저작** [S] · effect tile 중 증가형은 `EffectTileData.op`를 Additive로 저작해 정책 일관성 확보.
