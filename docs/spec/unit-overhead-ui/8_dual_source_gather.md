# 8. 듀얼소스 gather [ECS-read/bridge]

## 목적

유닛별 활성 스택을 BattleBridge 가 RO 수집해 오버헤드 뷰에 전달. 소스 = A(듀얼): `StackModifierSlot`(피로도 등) + `HeatAccrual`(열기).

## 변경 대상

- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — `GatherOverheadStacks`(재사용 버퍼) + `TryMapOverheadStackKind` + 두 `SetUnit` 호출부(enemy·defender) 전달.
- `Assets/_Project/Scripts/Presentation/UnitOverheadUiLayer.cs` — `stackIcons`(SerializeField) + `SetUnit` 스택 인자 + `view.Show` 전달.

## 구현

- `GatherOverheadStacks(entity)`: `_overheadStackScratch`(재사용, 프레임 GC 회피) clear → `StackModifierSlot` 버퍼 순회(stackCount>0, `TryMapOverheadStackKind` 매핑되는 것만) + `HeatAccrual.stacks>0` → `Heat`. 반환 = 재사용 버퍼(SetUnit→view.Show 가 동프레임 동기 소비하므로 안전).
- `TryMapOverheadStackKind`: Battle.StackKind→OverheadStackKind. 현재 `Fatigue`만(나머지 후속). 미매핑 false.
- `SyncMonoUnitViews` 의 enemy(false)·defender(true) `SetUnit` 둘 다 `GatherOverheadStacks(entity)` 전달 → 열기는 적/아군 공통, 피로도는 defender.
- Layer: `stackIcons` registry 를 `view.Show` 로 함께 전달(뷰가 kind→sprite 해석). 미할당 = 아이콘 생략.

## 완료 기준

- Unity 재컴파일 CS 에러 0. ✅
- 맥락 경계: BattleBridge 만 ECS RO(StackModifierSlot/HeatAccrual/Health) 읽기(제약 1). Presentation 은 plain DTO(kind+count)만.
- 스택 없으면 빈 리스트 → 스택행 무표시(dormant).
