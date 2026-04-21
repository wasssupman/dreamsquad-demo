# Drag Session

**작업 구분**: Phase 2

## 목적

Defender slot 에서 drag 를 시작하고, drag 중인 unit/context 를 session 으로 보관한다.

## 변경 대상

- New: `Assets/_Project/Scripts/UI/DefenderDragPlacementController.cs`
- New: `Assets/_Project/Scripts/UI/DefenderDragSlot.cs`
- Modify: `Assets/_Project/Scripts/UI/DefenderSelector.cs`

## 흐름

```text
DefenderDragSlot.OnBeginDrag
  -> DefenderDragPlacementController.BeginDrag(unit)

OnDrag
  -> screen position 을 world/tile 로 변환

OnEndDrag
  -> valid hover tile 이면 drop
  -> invalid 면 cleanup + reject flash
```

## Session 상태

- unit
- preview GameObject
- hover tile
- hover valid flag

## 완료 기준

- 7개 defender slot 에 drag handler 가 붙는다.
- drag 중 click placement 는 임시 비활성화된다.
- drag cancel/drop 후 session 이 cleanup 된다.
