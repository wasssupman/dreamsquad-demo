# 3 — 처형타 (execution_strike): EventTarget×HpBelow(25%) × AttackN × HeavyStrike

## 목적

대상 게이트(EventTarget)의 짝 카드 — "HP 25% 이하인 적에게는 공격 피해 ×2". 마무리 일격 판타지. 대상 게이트를 v1 에 여는 결정(2026-07-25)의 실수요.

## 변경 대상

- `Assets/_Project/Data/Dreamcatcher/Card_ExecutionStrike.asset` (신규) + 카탈로그 등록
- 카드 수 검증 3종 갱신

## 구현

unit 1 위에서 **코드 0줄 데이터** (HeavyStrike pre-scan 게이트는 unit 1 범위):

- id `execution_strike` · displayName `처형타` · axis All · type Unit · category Unique
- mechanics[0]:
  - trigger `{ kind: AttackN, period: 1, gate: HpBelow, gateSubject: EventTarget, gateValue: 0.25 }`
  - payload `{ kind: HeavyStrike, magnitude: 2.0 }`
- description: formatter 미러 (예상: `HP 25% 이하인 적에게 공격마다 → 그 공격 피해 ×2` — HeavyStrike 기존 문안에 게이트 접두)
- 주의 계약: HeavyStrike 는 발동 공격의 **전 victim**(cleave/splash)에 배율 적용이 기존 사양 — 게이트 판정은 primary(bestTarget) 기준이고 배율은 기존대로 전 victim. 이 비대칭(빈사 primary 를 때리면 splash 도 ×2)은 v1 수용, 문안은 primary 기준.
- 판정 시점: 이번 공격의 데미지 적용 **전** HP 기준 (unit 1 계약 — "때리기 전에 빈사인지 본다").
- 밸런스 노트: period 1 + ×2 는 사실상 "처형 존" — 수치는 Play 튜닝 대상 (배율 1.5 후퇴 여지).

## 완료 기준

- [ ] EditMode 전체 green
- [ ] e2e: HP 26% 더미 공격 = 평타 데미지, HP 24% 더미 공격 = ×2 데미지 (게이트 경계 동작 확인)
- [ ] 콘솔 경고 없음
