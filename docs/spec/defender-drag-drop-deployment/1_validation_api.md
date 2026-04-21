# Placement Validation API

**작업 구분**: Phase 1

## 목적

D&D hover/drop 에서 배치 가능 여부를 미리 판단할 수 있도록 `BattleBridge` 배치 검증 API 를 분리한다.

## API

```csharp
public bool CanPlaceDefenderAt(
    int tileX,
    int tileY,
    DefenderUnitData unitData,
    out PlacementRejectReason reason);
```

`PlacementRejectReason`:

```csharp
None,
NotRunningOrPlacementClosed,
MissingMap,
OutOfBounds,
NotBuildable,
Occupied,
InvalidUnit,
NotInPickedPool,
InsufficientCost
```

## 규칙

- 이 API 는 상태를 변경하지 않는다.
- cost 를 차감하지 않는다.
- tile 을 점유하지 않는다.
- hover 시 매 frame 호출 가능해야 한다.

## 완료 기준

- invalid reason 이 UI flash/highlight 에 활용 가능하다.
- `PlaceDefenderAs` 와 D&D drop 이 같은 검증 함수를 사용한다.
- 기존 click placement 동작이 유지된다.
