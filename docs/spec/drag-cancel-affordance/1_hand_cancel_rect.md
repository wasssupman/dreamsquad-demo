# 1 — 드림캐쳐: 취소 rect 를 보이는 부채에 맞추고 힌트 표시

## 목적

"손패로 되돌리면 취소" 규칙이 **눈에 보이는 손패와 같은 크기**가 되게 하고, 취소 존 안에 있을 때
그 사실을 배너로 알린다.

## 변경 대상

- `Assets/_Project/Scripts/UI/Dreamcatcher/DreamcatcherHandView.cs` — CancelZone rect + 힌트 배너
- `Assets/_Project/Scripts/UI/Dreamcatcher/DreamcatcherCardDragSlot.cs` — 판정 3곳을 새 rect 로

## 구현

### A. CancelZone — 패널 자식 rect

`BuildCanvas` 의 패널 생성 직후, `_panel` 자식으로 그림 없는 `RectTransform` 을 만든다.

```
anchorMin = anchorMax = pivot = (0.5, 0)      // 패널과 같은 하단 중앙 기준
anchoredPosition = (0, 0)
sizeDelta = (panelWidth, cancelZoneHeight)     // 기본 310
```

`cancelZoneHeight` 기본 **310** 근거: 조준 중 카드 top 로컬 y = `handBaseY 16 + arcHeight 46 +
CardH 230 × 드래그 확대 1.08 = 310.4`. 즉 **사용자가 보는 카드 부채의 실제 상단**이다(패널 232 는
부채를 60px 못 덮었다 — hand-drag-clearance 후속 후보의 그 불일치).

**패널 자식이어야 한다** — 하강(210px)이 패널 `anchoredPosition` 하나로 일어나므로 자식은 자동 승계된다.
`HandPanelRect` 를 읽던 3곳이 그대로 새 rect 를 읽으면 hand-drag-clearance 계약 1(취소 rect 는 패널
위치가 단일 소유)이 유지된다.

하강 후 취소 존 top = `32 − 210 + 310 = 132` — README 실측표의 "조준 중 카드 top 132" 와 정확히 같고,
가장 큰 맵의 보드 하단 모서리(167)보다 **아래**다. 즉 취소 존이 커져도 최하단 행 부착은 계속 가능하다
(hand-drag-clearance 가 푼 문제를 되돌리지 않는다).

`HandCancelRect` 프로퍼티로 노출하고, null 이면 `HandPanelRect` 로 폴백한다.

### B. 판정 3곳 교체

`DreamcatcherCardDragSlot` 의 `insideHand` 계산 3곳(`OnEndDrag` / 포탈 출구 `Update` /
`UpdateBriefingStatus`)을 `_view.CancelRect` 로 통일한다. 뷰가 폴백을 소유하므로 슬롯 쪽 분기는 없다.

### C. 취소 예고 — 기존 브리핑 상태 줄 하나 (rev3)

**새 표면을 만들지 않는다.** 조준 중 상단 중앙 브리핑이 이미 `insideHand` 를 분기해
`<color=#FF9B8A>여기서 놓으면 취소</color>` 를 상시 표시한다(`StatusFor`). 그게 유일한 취소 문자 채널이다.

rev1 에서는 손패 위에 `✕ 놓으면 취소` 힌트 배너를 함께 띄웠으나 **rev3 에서 삭제했다**
(사용자 결정 2026-07-30): 같은 문장을 두 곳에 그리는 중복이었고 카드 이름 띠를 가렸다. 유닛
트레이 배너를 지운 것과 같은 판단이다 — 취소 상태당 표면 하나.

지워진 것: `_cancelHint` GO · `SetCancelHint` · 슬롯의 호출 3곳. 남는 것은 **판정 rect** 뿐이다.

### D. 건드리지 않는 것

- 패널 배경(`_backing`) 크기·색 — 딜인 페이드가 `color.a` 를 소유한다. 배너는 별도 GO 라 안 싸운다.
- 하강량/스프링, 카메라 헤드룸, 툴팁.
- `HandPanelRect` 프로퍼티 자체(다른 소비처가 생길 수 있어 유지).

## 완료 기준

- [x] 컴파일 통과, CS 에러 0
- [x] EditMode 전량 통과(신규 실패 0) · PlayMode 신규 실패 0
- [x] Play — 카드를 들고 **보이는 카드 위**에서 떼면 취소된다(예전엔 상단 60px 이 취소가 아니었다)
- [x] Play — 취소 존에 들어가면 상단 브리핑이 취소 문구로 바뀌고 나가면 되돌아온다
- [x] Play — 커밋 / ESC / 손패 닫기 후 취소 문구가 남지 않는다
- [x] Play — 큰 맵(Serpent/Twin/Spiral) **최하단 행 유닛 부착이 여전히 된다**(hand-drag-clearance 회귀 없음)
- [x] Play — 포탈 2탭 대기 중에도 손패 탭이 취소로 동작한다

확인: 2026-07-30 사용자 Play 확인 통과. 구현 커밋 `c377b60f`(units 0~1) · `ec5e9c05`(unit 2 철회) ·
`c61aa51c`(unit 0 rev2 + unit 3) · `ffd6ae28`(rev3 배너 삭제) · 리뷰 반영분은 `fbcac2db` 안
(병행 세션 인덱스에 쓸림 — `be073d33` 참조).
