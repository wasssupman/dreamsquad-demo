# 0. Defender Outputs Asset Migration

## 목적

모든 defender SO 의 legacy `attack.damage` 의존을 `outputs=[{Damage, magnitude}]` 로 이전한다. 이후 `AttackSystem` 의 defender legacy damage fallback 을 제거해 defender 공격 효과의 source of truth 를 `AttackOutput[]` 로 통일한다.

## 변경 대상

- `Assets/_Project/Scripts/Data/DefenderUnitData.cs`
- `Assets/_Project/Scripts/Battle/Combat/AttackSystem.cs`
- `Assets/_Project/Data/Defenders/*.asset`
- 관련 EditMode 테스트: `Assets/_Project/Tests/EditMode/*Attack*`

## 구현

1. defender SO 전수 확인: `outputs[]` 가 비어 있고 `attack.damage > 0` 인 항목을 목록화한다.
2. 각 SO 에 `outputs` 1개를 추가한다: `kind=Damage`, `magnitude=attack.damage`, 나머지 필드는 기본값.
3. `BattleBridge` 또는 authoring 변환 경로에서 `outputs[]` 가 비어 있을 때 자동 생성하던 fallback 이 있으면 제거한다.
4. `AttackSystem` 에서 defender `outputs[]` 없음 + `attack.damage` fallback 으로 `IncomingDamage` 를 넣는 분기를 제거한다.
5. `attack.damage` 필드는 serialized 호환 또는 editor 표시용으로 남겨도 되지만 runtime source-of-truth 로 쓰지 않는다. 제거 여부는 별도 asset cleanup 으로 미룬다.

## 완료 기준

- Unity compile error 0.
- 모든 기존 defender 가 `AttackOutputKind.Damage` output 을 가진다.
- Play smoke: 기존 defender 1~2종이 적에게 주는 데미지가 이전과 동등.
- Healer 의 `Heal` output 동작이 유지된다.
- Attack output log 에 migrated defender 의 `Damage` output 이 기록된다.
