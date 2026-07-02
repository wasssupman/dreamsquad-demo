# 1. Authoring Rule + Helper Routing

## 목적

증가/감소 분류 규칙을 순수 함수로 만들고, BattleBridge 5 헬퍼를 그 함수를 쓰는 단일 choke-point로 통일한다.

## 변경 대상

- `Assets/_Project/Scripts/Battle/Effects/Modifiers/ModifierAuthoring.cs` (신규, static, runtime asm)
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` (5 헬퍼: EnqueueDamageMul/AttackSpeedMul/MoveSpeedMul/SynergyMul/StatMul → 중앙 헬퍼 경유)
- `Assets/_Project/Tests/EditMode/ModifierAuthoringTests.cs` (신규)

## 구현

- `ModifierAuthoring.FromMultiplier(float multiplier, out CombineOp op, out float magnitude)`:
  `multiplier >= 1f` → `(Additive, multiplier - 1f)`, else `(Multiplicative, multiplier)`.
- BattleBridge 내부 중앙 헬퍼 `EnqueueStatModifier(target, stat, multiplier, duration, stackId)`가 `FromMultiplier`로 op/magnitude 결정 후 enqueue. 기존 5 헬퍼는 이 중앙 헬퍼로 위임(stat/stackId/duration만 각자 전달). synergy stackId=1·duration=∞, dreamcatcher stackId param, on-place stackId=0 규약 유지.
- 호출부(on-place·synergy·dreamcatcher·skill) 시그니처 무변경 — 여전히 multiplier 전달.

## 완료 기준

- [ ] `ModifierAuthoringTests` 통과: 1.3→(Additive,0.3) / 1.0→(Additive,0.0) / 0.6→(Multiplicative,0.6) / 0.0→(Multiplicative,0.0)
- [ ] compile 오류 없음
- [ ] 전체 EditMode 스위트 — unit 2 전까지는 기존 테스트가 여전히 통과(직접 enqueue라 무영향), unit 2에서 shape 갱신
