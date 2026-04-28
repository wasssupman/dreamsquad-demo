# 6. 카드 입력과 폐기 트리거

## 목적

`DraftCardView` 에 EventSystems 인터페이스를 부착하여 클릭 / 위 스와이프(드래그-throw) 두 트리거로 폐기를 발생시킨다. 호버 효과 없음. 임계 미만 드래그는 fan 정위치로 복귀. **OnEndDrag 폐기 후 OnPointerClick 가 같은 프레임 더블 발화하지 않도록 가드.**

## 변경 대상

- `Assets/_Project/Scripts/UI/Draft/DraftCardView.cs` (입력 인터페이스 + 콜백 추가)

## 구현

1. 인터페이스 구현: `IBeginDragHandler`, `IDragHandler`, `IEndDragHandler`, `IPointerClickHandler`. `IPointerEnterHandler` / `IPointerExitHandler` **사용 금지**.
2. 멤버:
   - `RectTransform Rect`
   - `CanvasGroup CanvasGroup`
   - `Vector2 HomePosition`, `Quaternion HomeRotation` (fan view 가 박아줌)
   - `Canvas _rootCanvas` (scaleFactor 보정용; Awake 에서 `GetComponentInParent<Canvas>()`)
   - 드래그 상태: `_dragStartTime`, `_dragStartPos`, `_dragAccum`, `_lastDragDistance`
   - **`bool _discardFired`** — 더블 발화 가드
3. 콜백:
   - `event Action<DraftCardView> Discarded`
4. 라이프사이클:
   - `OnEnable` 또는 fan view 의 `Build` 직후: `_discardFired = false`.
5. `OnBeginDrag(eventData)`:
   - `_dragStartTime = Time.unscaledTime`
   - `_dragStartPos = Rect.anchoredPosition`
   - `_dragAccum = Vector2.zero`
   - `_lastDragDistance = 0f`
   - `_discardFired = false` (새 제스처 시작 시 가드 reset)
   - `transform.SetAsLastSibling()` (드래그 중인 카드를 최상단)
6. `OnDrag(eventData)`:
   - `_dragAccum += eventData.delta / (_rootCanvas != null ? _rootCanvas.scaleFactor : 1f)`
   - `Rect.anchoredPosition = _dragStartPos + _dragAccum`
7. `OnEndDrag(eventData)`:
   - `var duration = Time.unscaledTime - _dragStartTime;`
   - `_lastDragDistance = _dragAccum.magnitude;`
   - `var upDistance = _dragAccum.y;`
   - 폐기 조건: `upDistance >= 120f && duration <= 0.45f`
     - true → `_discardFired = true; Discarded?.Invoke(this);` (이후 fan view 의 PlayDiscardCard 가 toss 처리. 자체 위치 복귀 트윈 호출하지 않음.)
     - false → `Tween.UIAnchoredPosition(Rect, HomePosition, 0.25f, Ease.OutBack)` (PrimeTween 정식 API, task 0 확정).
8. `OnPointerClick(eventData)`:
   - **가드 1**: `if (_discardFired) { _discardFired = false; return; }` — 같은 제스처에서 OnEndDrag 가 폐기를 이미 발화했다면 click 무시.
   - **가드 2**: `if (_lastDragDistance >= 30f) return;` — 드래그가 충분히 이동했으면 click 으로 카운트 안 함 (안드로이드 미세 흔들림 방지).
   - 그 외 → `Discarded?.Invoke(this);`
9. fan view 측 `Discarded` 핸들러:
   - `if (controller.Session.ToggleDiscard(card.Unit))` 성공 → `card.CanvasGroup.blocksRaycasts = false` → `await fan.PlayDiscardCard(card)` → `fan.LayoutRemaining()` → `if (controller.Session.IsFull) orchestrator.RequestAutoConfirm()`.
   - 실패 (`ToggleDiscard` 가 cap 초과로 false) → `Tween.UIAnchoredPosition(card.Rect, card.HomePosition, 0.25f, Ease.OutBack)` 로 정위치 복귀.

## 완료 기준

- 카드 클릭 (드래그 거리 30px 미만, _discardFired=false 상태) → `Discarded` 1회 발화 + toss/fade out + 남은 카드 재배치.
- 카드 위로 드래그 (delta.y ≥ 120px, ≤ 0.45s) → `Discarded` 1회 발화. 같은 프레임에 OnPointerClick 이 추가로 발화돼도 `_discardFired` 가드로 무시되어 폐기 1회만 발생.
- 임계 미만 드래그 종료 시 카드 정위치 OutBack 복귀 (≈0.25s), 폐기 0.
- 호버 효과 없음.
- 폐기 진행 중 `blocksRaycasts = false` 로 추가 입력 차단.
- 컴파일 에러 0.
