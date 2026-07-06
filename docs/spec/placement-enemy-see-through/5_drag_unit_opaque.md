# 5 — 드래그 유닛 불투명 + 최상단 소팅

**작업 구분**: wiring (프리뷰)

## 목적

적이 반투명해지는 동안 **배치 중인 유닛(드래그 프리뷰)** 은 불투명하게 유지해 최상단 초점이 되게 한다.
(적만 뒤로 물리고, 지금 놓는 유닛은 선명하게.)

## 변경 대상

- `Assets/_Project/Scripts/UI/DefenderDragPlacementController.cs` (`BuildSession` / 폴백 프리뷰)

## 구현

- Spine 프리뷰: `SetPreviewAlpha(skeleton, 0.62f)` → **`1f`** (불투명).
- 폴백 캡슐 프리뷰: `color.a = 0.55f` → **`1f`**.
- 소팅: 프리뷰 Spine renderer 는 이미 `BoardSortOrder.DragPreviewOrder`(**20000**) — 보드 요소(프랍/유닛/투사체)
  중 최상단. **추가 변경 불필요.** 게다가 unit 1 의 적 transparent 전환(ZWrite off)으로 적이 더 이상
  프리뷰를 depth 로 가리지 않아, 프리뷰가 반투명 적 위에 확실히 그려진다.

## 주의 (의도된 설계 변경)

- keyring-cord-preview / placement-drag-preview-polish 의 **반투명 실루엣(0.62)** 을 불투명으로 바꾸는 것.
  "유령 실루엣" → "선명한 배치 유닛" 으로의 의식적 전환. 되돌리려면 이 값만 원복.

## 완료 기준 (Play)

- 드래그 중 배치 유닛(프리뷰)이 **불투명**하게 보인다(0.62 반투명 아님).
- 반투명해진 적과 겹쳐도 배치 유닛이 **위에 선명하게** 그려진다(뒤 적이 비쳐 프리뷰를 흐리지 않음).
- 링/줄(키링) 표시는 기존과 동일.
