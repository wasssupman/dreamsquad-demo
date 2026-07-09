# Unit 10 — 상태 재설계: AggroCapacity 신설 · AggroProvider 폐기 · 베이크

> 근접 모델의 `AggroProvider`(획득 필드 앵커) 프레이밍 폐기. 히트 모델에선 가디언은 "무언가를 provide"하지 않고 그냥 capacity 를 가진 방어 유닛.

## 목적

가디언 표식 + capacity 상태를 히트 모델에 맞게 재정의하고, 베이크 진입점(BattleBridge)을 갱신한다.

## 변경 대상

- (폐기) `Assets/_Project/Scripts/Battle/Effects/AggroProvider.cs`
- (신규) `Assets/_Project/Scripts/Battle/Effects/AggroCapacity.cs`
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` (3247~ 베이크)
- `Assets/_Project/Scripts/Data/DefenderUnitData.cs` (`aggroRange` 폐기)
- `Assets/_Project/Data/Defenders/Defender_Guardian.asset` (`attackTargetCount 1→≥2`)

## 구현

**`AggroCapacity { int max; int held; }`** — Effects 소유.
- `max`: 베이크 시 `DefenderUnitData.aggroCapacity` 에서. **컴포넌트 존재 자체가 "가디언" 표식** (별도 태그·range 없음).
- `held`: Effects(`AggroStateSystem`)가 매 틱 재계산(증감 아닌 full recompute → drift 없음). 베이크 시 0.

**BattleBridge 베이크**: 기존 `if (unitData.aggroCapacity > 0)` 분기에서 `AggroProvider{capacity,range}` 대신 `AggroCapacity{max=aggroCapacity, held=0}` 부착. `aggroRange` 참조 제거.

**`DefenderUnitData.aggroRange` 폐기**: 히트 모델에선 획득 범위 = 공격 사거리라 무의미. **삭제 전 프로젝트 전역 `grep aggroRange` 로 잔여 참조 확인**(critic M3). 프리릴리스라 직렬화 마이그레이션 위험 낮음.

**Guardian 데이터**: `attackTargetCount 1→≥2` (멀티타겟으로 신규 팩 흡수, 사용자 결정). Bastion 은 이미 3.

## 완료 기준

- [ ] `AggroProvider` 참조 0 (컴파일). `aggroRange` 참조 0(grep).
- [ ] 가디언 배치 → `AggroCapacity{max=4, held=0}` 부착(Play reflection 조회).
- [ ] Fighter/Ranger(aggroCapacity=0) → `AggroCapacity` 미부착.
- [ ] Guardian.asset `attackTargetCount ≥ 2`.

완료: 2026-07-09 (컴파일 클린, Guardian 2로 상향 / 커밋 `b84b6887`)
