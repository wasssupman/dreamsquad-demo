# 1 — 동상 (frostbite): AttackN × Ice 스택 (3스택 슬로우 / 5스택 동결)

## 목적

공격마다 얼음 스택을 쌓아 임계에서 CC 로 전환되는 카드. `StackKind.Ice` 는 enum·틱 시스템(`StackModifierTickSystem`)에 존재하지만 ThresholdRule SO 가 없어 미사용. 출혈(Bleed = 1스택부터 즉발 DoT)과 정체성 분리: **동상 = 누적 → CC**. 코드 0줄 — SO 2개 authoring + 씬 배선.

## 변경 대상

- `Assets/_Project/Data/Dreamcatcher/StackModifier_Ice.asset` (신규 — `StackModifier_Bleed.asset` 원형)
- `Assets/_Project/Data/Dreamcatcher/Card_Frostbite.asset` (신규)
- `DreamcatcherCardCatalog.asset` (등록)
- **씬 배선**: BattleBridge 의 `stackModifierAuthoring` 배열에 Ice SO 추가 — 이게 없으면 `BuildStackThresholdRegistry` 가 규칙을 못 찾아 스택이 조용히 무효과. UnityMCP 로 배선 + 검증까지 (unity-feature-wiring 스킬 대상)

## 구현

`StackModifier_Ice.asset`:

- kind Ice(2) · maxStack 5 · perAppDuration 4 · policy RefreshAll
- thresholds (atStack 오름차순 계약):
  1. `{ atStack: 3, mode: Edge, derivedKind: ApplyStat, stat: MoveSpeedMul, op: Multiplicative, magnitude: 0.6, duration: 1.5 }` — 3스택 도달 시 이속 ×0.6 슬로우 1.5초 (스택 유지)
  2. `{ atStack: 5, mode: Consume, derivedKind: ApplyStun, magnitude: 1.0 }` — 5스택 도달 시 동결 1초 + 스택 5 소모 → 재누적 사이클

`Card_Frostbite.asset`:

- id `frostbite` · displayName `동상` · axis All · type Unit
- mechanics[0]: trigger `{ AttackN, period: 1 }` / payload `{ ApplyStackToTarget(11), stackKind: Ice(2), magnitude: 1(스택 수), duration: 4(스택당 지속), tileRange: 5(maxStack 상한) }`
- description: `공격마다 얼음 1중첩 — 3중첩: 둔화, 5중첩: 동결 1초` ("둔화" 표기는 CcEffect 아님 주의가 **카드 문안 금지어**였던 맥락과 다름 — 여기선 실제로 MoveSpeedMul 이므로 그대로 서술 가능)
- art: null

수치는 전부 초안 — 특히 period 1(매 공격 1스택)은 공속 빠른 유닛에서 동결 사이클이 과할 수 있어 Play 튜닝 대상.

**v1 제외**: 오버헤드 스택 아이콘 (`OverheadStackKind` 는 Fatigue/Heat 전용, Bleed 도 미표시 선례) — README 후속 후보.

## 완료 기준

- [x] EditMode 전체 green
- [ ] Play smoke: 부착 유닛이 같은 적을 연타 → 3타에서 감속, 5타에서 1초 정지 후 스택 리셋 재누적 확인. `stackModifierAuthoring` 배선 누락 시의 무효과가 아님을 로그/거동으로 확인

구현 커밋 bc27201e (2026-07-25). 씬 배선은 BattleScene.unity YAML append 로 반영(1줄, 씬 재오픈 필요). CardText 에 AttackN period-1 문안 특례("공격마다") 포함. Play smoke 대기.
