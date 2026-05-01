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

- Unity compile error 0.
- Basic/Swift/Tanker 및 신규 적 3종의 공격 데미지가 migration 전과 동등.
- 장거리/빠른 투사체 적이 outputs path 를 사용해도 projectile visual/hit 동작이 유지된다.
- 적이 향후 `ApplyStat`/`ApplyStack` output 을 가질 수 있는 데이터 경로가 열린다.
