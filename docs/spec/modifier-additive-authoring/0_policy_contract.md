# 0. Policy Contract

## 목적

증가/감소 분류 규칙과 단일-스택 등가를 확정한다. docs only.

## 규칙

modifier 생산 시 최종 배율(multiplier)로 분류:

| 조건 | op | magnitude | 의미 |
|---|---|---|---|
| `multiplier >= 1` (증가/버프) | `Additive` | `multiplier - 1` | +30% → +0.3 델타. 다중 슬롯 합산 |
| `multiplier < 1` (감소/디버프) | `Multiplicative` | `multiplier` | ×0.6 그대로. 다중 슬롯 곱연산(체감감소)+floor |

- **단일 스택 등가**: 어느 쪽이든 1스택 결과는 전과 동일. 증가 `(1 + (m-1)) = m`, 감소 `1 × m = m`.
- **경계 `multiplier == 1`**: 증가 분기(Additive 0.0 = identity). 감소 분기여도 Multiplicative 1.0 = identity라 결과 동일 — 무해.
- **집계**: `(1 + Σadd) × Π(mul)` + `modifier-stacking-policy` 클램프. 무변경.

## 결합 예시

- 버프 2개(1.3, 1.2): 이전 `1.3×1.2 = 1.56` → 이후 `1 + 0.3 + 0.2 = 1.5`.
- 디버프 2개(0.6, 0.6): `0.36` (불변).
- 혼합(버프 1.3, 디버프 0.6): `(1+0.3) × 0.6 = 0.78`.

## 적용/비적용

- **적용**: BattleBridge `Enqueue*` 헬퍼(on-place·synergy·dreamcatcher·skill). 규칙이 slow/저항(값<1)을 자동으로 Multiplicative 유지.
- **비적용(명시 op 저작 경로)**: EffectTile(`EffectTileData.op` 존중), SO output `AttackOutput.op`(AttackSystem/ProjectileHit forward — Debuffer 감소는 무영향). **이 경로들의 버프는 op=Additive 로 저작해야 정책 일관** — 곱연산 버프로 저작하면 런어웨이 우회. (구조적 비적용이지 데이터 우연이 아님.)

## 불변식 (merge-key)

병합 키 = `(source, stat, op, stackId)`. op 가 값(1.0 경계)으로 정해지므로, **한 `(stat, stackId, source)` 채널은 단방향으로 유지**해야 한다 — 1.0 을 넘나드는 값을 섞으면 Additive/Multiplicative 두 슬롯이 공존해 refresh-멱등이 깨지고 슬롯이 축적된다. 현재 전 채널 단방향(확인됨). 신규 효과 추가 시 준수(예: haste/slow 를 같은 `EnqueueMoveSpeedMul` stackId=0 로 섞지 말 것).

## 완료 기준

- [x] 규칙·등가 확정 — 사용자 승인 2026-07-03 (B 선택)
