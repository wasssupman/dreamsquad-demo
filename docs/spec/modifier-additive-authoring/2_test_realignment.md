# 2. Test Realignment

## 목적

버프를 곱연산 이벤트로 재현하던 기존 테스트를 새 shape(증가=Additive)으로 갱신한다. 이 테스트들은 "BattleBridge가 만드는 이벤트 shape을 재현"한다고 명시하므로, unit 1 이후 실제 shape과 어긋난다("tests as spec").

## 변경 대상

- `Assets/_Project/Tests/EditMode/EffectIntegrationTests.cs` (`Combat_Applies_SynergyMul_Stacked_With_DamageMul_Via_ModifierStats`)
- `Assets/_Project/Tests/EditMode/EffectTileModifierTests.cs` (`AppliesAndStacksWithOnPlaceAndSynergy`)
- (필요 시) `Assets/_Project/Tests/PlayMode/DreamcatcherCombatDamageTest.cs`

## 구현

- on-place boost / synergy / dreamcatcher DamageMul(버프)을 enqueue하던 곳: `op=Multiplicative, magnitude=m` → `op=Additive, magnitude=m-1`. 기대 결합값을 곱→합으로 갱신.
  - EffectIntegration synergy 테스트: boost ×2 + synergy ×1.3 (`2.6`) → Additive `1.0 + 0.3` → `1 + 1.0 + 0.3 = 2.3`. emittedDamage 10×2.3 = 23.
  - EffectTile 테스트: effect-tile은 명시 op 저작(범위 밖, Multiplicative 유지). on-place/synergy 부분만 Additive로 → 결합 `1.25(mul) × (1 + 0.2 + 0.1) = 1.25 × 1.3 = 1.625`.
- EffectTile 자체가 증가형 버프를 Multiplicative로 재현하는 부분은 "명시 op 저작(범위 밖)"임을 주석으로 남기고 유지.

## 완료 기준

- [ ] 갱신된 테스트가 새 결합값으로 통과
- [ ] 전체 EditMode 스위트 회귀 없음
- [ ] (수동) Play smoke: 버프 2개 이상 겹친 디펜더 데미지가 합산(곱연산 아님)으로 나오는지 GameLog 확인
