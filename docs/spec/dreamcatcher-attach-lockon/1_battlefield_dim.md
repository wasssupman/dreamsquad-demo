# 1 · Battlefield Dim (레이어 A)

## 목적

부착 조준 드래그가 시작되면 전장을 살짝 감광해, 스프라이트·VFX·투사체 난장을 눌러 **화살표 선이 위로 떠오르게** 한다(불편 ①). 손패 슬로모("기획 순간") 위에 얹혀 조준 몰입 강화.

## 변경 대상

- `Assets/_Project/Scripts/UI/Dreamcatcher/DreamcatcherHandView.cs` — dim 패널 소유·페이드
- `Assets/_Project/Scripts/UI/Dreamcatcher/DreamcatcherCardDragSlot.cs` — 드래그 시작/종료·취소에서 dim 토글

## 구현

- `DreamcatcherHandView.BuildCanvas()` 에서 **dim `Image` 1개 생성**: 풀스크린 stretch, `focusConfig.dimColor`, 시작 alpha 0, `raycastTarget=false`.
- **sorting (기술 critic HIGH 반영)**: dim 을 **`SafeAreaRoot` 아래 sibling** 로 둔다 — 손패 패널·드래그 성능 툴팁(`_tooltipRoot`)·게이지는 dim **위**(감광 제외, 회귀 방지). overhead 체력 UI(`UnitOverheadUiLayer` order 3)·world SpriteRenderer 는 dim(캔버스 order 5) 아래라 **감광됨 — "전장 quieting"으로 의도**. draw-order 는 sibling index 로 명시 강제(계약 #3).
- HandView 에 `ShowDim()`/`HideDim()` — `unscaledDeltaTime`(timeScale 상시 1이라 `deltaTime` 과 동일, 방어적 표기) 기반 alpha 페이드(`dimFadeInSec`/`dimFadeOutSec`), 멱등.
- `DreamcatcherCardDragSlot`: 조준 드래그 시작(`OnBeginDrag` 후 조준 모드 확정)에 `ShowDim()`, `OnEndDrag`/`CancelDrag`/취소에 `HideDim()`.
- **생명주기 하드클리어(계약 #10)**: dim 해제를 `EndInteraction` 뿐 아니라 뷰 `Close`/`ForceClose`/`OnDisable`/`OnPhaseChanged` 이탈에도 배선(잔류 금지).

## 완료 기준

- 밀집 배치 Play(또는 오프스크린): 드래그 시작 시 배경이 어두워지고 **화살표가 확연히 떠오름**, 손 떼면 원복.
- **드래그 성능 툴팁·손패 카드·게이지는 감광되지 않음**(sorting 확인). overhead 체력 UI 감광은 의도대로.
- dim 이 조준 판정/입력 비간섭(`raycastTarget=false`). phase 이탈·강제 클로즈에서 dim 잔류 없음. 콘솔 클린.
- 검증 스크린샷은 dim on/off 대비를 **같은 run 안에서** 촬영(cross-run 오탐 주의 — lessons).
