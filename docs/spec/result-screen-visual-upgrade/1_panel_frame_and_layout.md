# 1 — 패널 셸 + 3영역 앵커 레이아웃

## 목적

결과 팝업의 셸을 게임 무드로 재구성하고, RESTART 버튼이 리스트 중앙에 겹치던 레이아웃 결함을 제거한다.

선행: unit 0 (`UiRoundedSprite`).

## 변경 대상

- `Assets/_Project/Scripts/UI/ResultScreen.cs` — `BuildCanvas()` 재작성, 직렬화 필드 추가

## 구현

> **배경 번복(2026-07-08)**: 시즌 아트 풀스크린은 배틀 보드를 덮어 "개판" → 폐기. backdrop = `UiOverlay.Dim` 단색 오버레이만. 팔레트는 `private static readonly` 코드 상수(직렬화 필드 0).

- **팔레트 상수**: `goldColor = (1, 0.78, 0.28, 1)`, `navyFill = (0.05, 0.06, 0.10, 0.98)`, `defeatColor = (1, 0.42, 0.42, 1)`.
- **레이어 순서**(뒤→앞): ① `Dim` 풀스크린 Image(`UiOverlay.Dim`, 기존 팝업 톤) → ② `ResultPanel`.
- **패널**: center 앵커, `sizeDelta ≈ (760, 940)`. 배경 = `UiRoundedSprite.Make(radius=32, border=4, navyFill, goldColor*0.95a)` (Sliced). `VerticalLayoutGroup` 제거 — 자식은 앵커로 직접 배치.
- **3영역**(패널 자식, 앵커 기반):
  - **헤더**(top 앵커, 높이 ~150): 골드 탭 배너(`UiRoundedSprite.Make(radius=대, 0, goldColor, goldColor)`, Sliced) 위에 `VICTORY`/`DEFEAT` 라벨(Bold+SmallCaps+characterSpacing, VICTORY=네이비 글자/골드 배경, DEFEAT=`defeatColor`). 그 아래 `YOUR SCORE  918` 서브라인.
  - **리스트 영역**(stretch 앵커: 헤더 하단~푸터 상단, 좌우 인셋): `RectMask2D` + 세로 `VerticalLayoutGroup`(spacing≈10, `childControlHeight=true`, `childForceExpandHeight=false`, `childForceExpandWidth=true`, `childControlWidth=true`). unit 2 가 여기에 행을 채운다. 10행이 영역 높이에 들어가도록 행 높이 산정(≈52px).
  - **푸터**(bottom 앵커, 높이 ~120): RESTART 버튼을 패널 하단에 고정. `UiRoundedSprite.Make` 라운드 버튼 배경(골드) + 네이비 글자. **버튼이 리스트 위로 떠오르지 않음** — 리스트 영역은 푸터 top 까지만 stretch.
- 헤더 라벨 텍스트는 `ShowResult(resultText, playerScore)` 에서 세팅(기존 흐름 유지). 서브라인 스코어도 여기서.
- `UiLayer.Apply(gameObject)` 유지.

## 완료 기준

- [ ] compile 통과
- [ ] Play: 결과 팝업이 dim 오버레이 위 네이비 프레임 + 골드 헤더 탭으로 렌더, RESTART 가 하단 바에 고정되어 리스트와 겹치지 않음

확인: 2026-07-08 — 프리뷰 하네스(ScreenSpaceCamera 캡처)로 dim 팝업 + 3영역 레이아웃(RESTART 하단 고정) 렌더 확인. 인게임 최종 확인 대기.
