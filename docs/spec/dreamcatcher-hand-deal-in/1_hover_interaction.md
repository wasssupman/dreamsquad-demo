# 1 — 풀 StS 호버

## 목적

카드 위에 커서를 올리면 그 카드가 살아나고 손패가 반응한다: 들어올림 + 확대 + 회전 펴짐 +
최상단 + 양옆 카드 밀어냄(scatter). 손패 "생동감"의 결정적 요소. unit 0 스프링 위에 얹는다.

## 변경 대상

- `Assets/_Project/Scripts/UI/Dreamcatcher/DreamcatcherHandView.cs`
- `Assets/_Project/Scripts/UI/Dreamcatcher/DreamcatcherCardDragSlot.cs` (포인터 enter/exit 보고)

## 구현

1. **호버 감지**: DragSlot 에 `IPointerEnterHandler`/`IPointerExitHandler` 추가(이미 드래그 이벤트 보유)
   → `handView.SetHovered(index)` / `SetHovered(-1)` 콜백. HandView 는 `_hoveredIndex` 보유.
2. **호버 타겟 계산**(unit 0 스프링 target 만 조작, rect 직접 X): State==Hand 이고 전이/드래그 없을 때
   슬롯별 target 재계산:
   - 호버 슬롯: `targetPos = base + (0, hoverRaise)`, `targetScale = hoverScale`, `targetRotZ = 0`(펴짐),
     sibling = 최상단(`SetAsLastSibling`).
   - 이웃: `Δ = i - hovered`; `targetPos.x += sign(Δ) * scatter * falloff(|Δ|)` (falloff 예: `1/|Δ|` 또는 근접 2장만).
   - 그 외: base.
   - `_hoveredIndex < 0` 이면 전원 base + sibling 복원(base = i 순).
3. **드래그/타겟팅 공존**: 드래그 시작 시 호버 해제(`SetHovered(-1)`) + 스프링 skip 계약 유지. 호버는
   드래그 불가 카드(`!usable`/빈 슬롯)엔 최소 반응 또는 무반응(정책: 사용 가능 카드만 풀 호버, dim 카드는 살짝만).
4. **튜닝 SerializeField**: `hoverRaise=40f`, `hoverScale=1.28f`, `scatter=42f`, `scatterNeighbors=2`.

## 완료 기준

- compile 성공, 콘솔 CS 에러 0.
- Play — 카드 호버 시 그 카드가 솟아오르며 확대·수직으로 펴지고 최상단으로, 양옆이 밀려남. 커서 이탈 시 아치 복귀.
- 호버↔드래그 전환이 매끄러움(드래그 시작에 호버 튐 없음, 취소 시 정상 복귀).
- 빠르게 여러 카드 훑어도 스프링이 튀지 않고 따라옴(anti-flicker: 직접 rect 조작 없이 target 만).
