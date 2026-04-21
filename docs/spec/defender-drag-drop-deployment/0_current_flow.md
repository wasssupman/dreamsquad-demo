# Current Flow

**작업 구분**: Phase 0

## 목적

현재 defender 배치 흐름을 고정하고 D&D 전환 기준을 정한다.

## 현재 구조

- `DefenderSelector` 가 7개 picked defender slot 을 표시한다.
- slot click 은 `GameManager.SelectedDefender` 를 설정한다.
- `PlacementInput` 은 pointer click 을 grid cell 로 변환한다.
- `BattleBridge.PlaceDefenderAs(tileX, tileY, selected)` 가 즉시 defender entity 를 생성한다.

## 문제

- 배치가 click-to-place 라서 의도성이 약하다.
- 배치 순간의 preview/hover 피드백이 부족하다.
- deploy animation/on-place skill sequence 를 삽입할 명확한 pending 상태가 없다.

## 전환 목표

```text
Drag slot
  -> drag silhouette
  -> tile hover highlight
  -> valid drop
  -> pending deployment entity
  -> deployment presentation
  -> on-place skill
  -> combat activation
```

## 완료 기준

- 기존 click placement 소비 경로가 문서화된다.
- D&D 가 대체해야 할 UI/API 경계가 명확하다.
- fallback click path 는 당장 제거하지 않는다.
