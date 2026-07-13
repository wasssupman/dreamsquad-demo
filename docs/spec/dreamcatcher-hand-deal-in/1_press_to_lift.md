# 1 — 눌러서 들기 (press-to-lift, 모바일)

## 목적

카드 상호작용 역동감을 **터치-네이티브**로 준다. hover(포인터 enter/exit)는 모바일(Android)에서
쓸 수 없으므로, **누르면(press) 들리는** 방식으로 트리거를 바꾼다: 카드를 손가락/마우스로 누르면
그 카드가 솟아오르며 확대·수직으로 펴지고 이웃이 갈라진다(하스스톤 모바일식). unit 0 스프링 위에 얹는다.

## 변경 대상

- `Assets/_Project/Scripts/UI/Dreamcatcher/DreamcatcherCardDragSlot.cs` (press/release 보고)
- `Assets/_Project/Scripts/UI/Dreamcatcher/DreamcatcherHandView.cs` (focus target)

## 구현

1. **트리거 = press**: DragSlot 이 `IPointerDownHandler`/`IPointerUpHandler` 구현 →
   `handView.SetFocus(index)` / `ClearFocus(index)`. (`IPointerEnter/Exit` 는 제거 — 데스크톱 전용이라
   모바일에서 오도.) PointerDown 은 BeginDrag 보다 먼저 발화 → 누르면 즉시 들리고, 드래그로 이어지면
   드래그 비주얼이 이어받는다. tap(down+up, 무드래그)= 잠깐 들었다 안착(peek).
2. **focus target**(unit 0 스프링 target 만 조작): 뷰가 `_focusIndex` 보유.
   - focus 슬롯: `targetPos = base + (0, focusRaise)`, `targetScale = usable ? focusScale : 1.06f`,
     `targetRotZ = 0`(펴짐), sibling 최상단.
   - 이웃: `Δ = i - focus`; `targetPos.x += sign(Δ)·scatter/|Δ|` (근접 `scatterNeighbors` 장).
   - 그 외/‐1: base.
3. **가드**: `SetFocus` 는 State==Hand & !Transitioning & !AnyInteractionActive & 실제 카드 슬롯일 때만.
   `BeginDrag` 시 `SetFocus(-1)`(이웃 복귀), 드래그 슬롯은 스프링/focus skip(DragSlot 소유). 오버랩 카드
   press 경합은 last-press-wins(`ClearFocus` 는 현재 focus 일 때만 해제).
4. **모바일 실용**: focusRaise 는 카드가 손가락 위로 떠서 가려지지 않을 만큼(≥ 카드높이 일부). 실기 튜닝.
5. **튜닝 SerializeField**: `focusRaise=40f`, `focusScale=1.28f`, `scatter=42f`, `scatterNeighbors=2`.

## 완료 기준

- compile 성공, 콘솔 CS 에러 0.
- Editor(마우스 누름) / 실기(터치) 모두 — 카드를 **누르면** 솟아오르며 확대·펴짐·최상단, 이웃 밀려남.
  떼면 아치로 스프링 복귀.
- press→드래그 전환 매끄러움(드래그 시작에 focus 튐 없음), 드래그 취소 복귀 정상.
- 빈 슬롯/드래그 중 다른 카드는 focus 안 됨(critic 반영).
