# Modifier Stacking Policy

상태: **완료 2026-07-03** — 클램프 구현·커밋 (Play 재현 확인 + Additive 저작 전환은 후속)

## 배경 / 문제

`ModifierStatsAggregateSystem`의 결합식은 `stat = override ? over : (base + Σadd) × Π(mul)` 이다. `ModifierApplySystem`의 병합 키는 `(source, stat, op, stackId)` 라 **서로 다른 소스**의 같은-스탯 `Multiplicative` modifier는 각각 슬롯을 만들어 **곱연산으로 무한 누적**된다.

Play 검증(2026-07-02, GameLog `session-...130202`)에서 실측: `aggroCapacity=4` Guardian이 Debuffer 다수를 붙잡자 Debuffer의 `DamageMul 0.6 (Multiplicative, 3s)`이 소스별로 곱해져 damageMul이 `0.6² … 0.6⁴`로 감쇠, Guardian 실데미지가 `15 → 0.24`까지 소멸. Guardian 자신의 `BoostNearbyDefenders(×1.3)`는 반대 방향으로 튀어 `24.375`까지 상승. → "한 유닛의 데미지가 타격마다 다름" 증상.

이는 특정 유닛 버그가 아니라 **곱연산 누적에 경계 정책이 없는** 프레임워크 공백이며, 곱연산인 모든 스탯(DamageMul/AttackSpeedMul/DmgTakenMul/MoveSpeedMul)에 동일하게 잠재한다. 버프 방향(×1.3ⁿ)은 런어웨이라 더 위험.

## 표준 해법 (업계 공통)

1. **가산 기본 + 곱연산은 예외** (PoE `increased`/`more`): 결합식은 이미 `(1+Σadd)×Πmul` 로 이 뼈대를 갖췄다. 보통의 버프/디버프를 `Additive`로 저작하면 선형 누적된다.
2. **최종 클램프** (Dota 이동속도 하한·슬로우 저항): 결합 결과에 floor/ceil. 저작 방식과 무관한 안전망.

## 범위

- **본 spec = 2번(최종 클램프)만.** 프레임워크 불변식 확장. 저작 op 변경 없이 무한 누적의 병리(데미지 소멸/버프 런어웨이)를 경계한다.
- **비목표 (후속 밸런스 패스)**: 1번(Debuffer·BoostNearbyDefenders 등을 `Additive`로 저작 전환)은 효과별 수치 변경이라 별도 밸런스 결정. clamp 경계값의 authoring(config SO)도 후속.

## 작업 단위

| 번호 | 구분 | 문서 | 목적 |
|---|---|---|---|
| 0 | 구현 | `0_aggregate_clamp.md` | 순수 결합+클램프 helper + 집계 시스템 배선 + 단위 테스트 |

## Feature-wide 계약

- 클램프 대상은 **배율 스탯 4종** (`damageMul`/`attackSpeedMul`/`dmgTakenMul`/`moveSpeedMul`). `regenPerSec`는 base 0 자원값이라 제외(음수만 방지).
- 정책 경계값(framework 상수, 정상 플레이 미간섭·병리만 차단): damage/attackSpeed/dmgTaken `[0.2, 5]`, moveSpeed `[0.15, 3]` (슬로우가 완전 정지 못 하게 — 정지는 `CcKind.Stun` 담당).
- `Override` op는 클램프 이전에 우선하되, 최종값도 동일 범위로 클램프(저작 실수 방지).

## 후속 후보

- **Additive 저작 전환 밸런스 패스** [M] · Debuffer(`DamageMul 0.6 Mult` → `−0.4 Add`), `BoostNearbyDefenders`(×1.3 → +0.3) 등. 곱연산은 의도된 희귀 효과에만 남김.
- **clamp 경계값 authoring** [S] · config SO로 노출 (밸런싱 필요 시).
