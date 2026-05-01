# 1. AttackUnit Outputs Unification

## 목적

적 `AttackUnitData` 도 defender 와 같은 `AttackOutput[]` 모델을 사용하게 만든다. 적 공격의 damage/projectile/향후 debuff 를 같은 dispatch 경로로 표현해 attacker 종류별 schema 분기를 줄인다.

## 변경 대상

- `Assets/_Project/Scripts/Data/AttackUnitData.cs`
- `Assets/_Project/Scripts/Battle/Combat/AttackSystem.cs`
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs`
- `Assets/_Project/Scripts/Data/Units/*.asset`
- 신규/수정 EditMode 테스트: enemy outputs dispatch

## 구현

1. `AttackUnitData` 에 `AttackOutput[] outputs` 를 추가한다. 필드 의미는 `DefenderUnitData.outputs` 와 동일하다.
2. enemy spawn/authoring 변환에서 outputs buffer 를 부착한다.
3. 기존 enemy damage 값은 `outputs=[{Damage, magnitude=damage}]` 로 asset migration 한다.
4. projectile enemy 는 기존 projectile 참조를 유지하되, hit 효과는 outputs dispatch 와 충돌하지 않게 정리한다. projectile 자체는 delivery 방식, outputs 는 hit 결과로 본다.
5. `AttackSystem` 의 defender/enemy 별 damage enqueue 분기를 공통 outputs dispatch 로 줄인다.

## 완료 기준

- [x] Unity compile error 0.
- [x] Basic/Tanker 및 신규 Needler/Rootcaster 의 damage 값이 `outputs[{Damage}]` 로 migration 됐다. Swift/Runner 는 `outputs: []` 로 no-attack 정책을 유지한다.
- [x] 장거리/빠른 투사체 적은 projectile 참조를 유지하고, projectile entity 가 shooter outputs snapshot 을 들고 hit 시 dispatch 한다.
- [x] 적이 향후 `ApplyStat`/`ApplyStack` output 을 가질 수 있는 데이터 경로가 열렸다.

검증:
- 2026-05-01: full EditMode 181 total / 179 passed / 2 ignored, failed 0.
- 2026-05-01: Play Mode enter/exit smoke, console error 0.
