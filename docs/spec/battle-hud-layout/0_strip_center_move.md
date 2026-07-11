# 0 — 유닛 스트립 bottom-center 이동

## 목적

`DefenderSelector` 의 배치 스트립 패널을 하단 좌측 코너에서 하단 중앙으로 옮겨, 드림캐쳐 핸드(bottom-center (0,32))와 같은 축에서 플립되게 한다. 이 단위가 끝나면 스트립↔핸드 플립이 좌표 점프 없는 제자리 플립이 된다.

## 변경 대상

- `Assets/_Project/Scripts/UI/DefenderSelector.cs` — `BuildCanvas()` 의 `DefenderPanel` RectTransform

## 구현

- 앵커/피벗: `anchorMin=anchorMax=(0.5, 0)`, `pivot=(0.5, 0)` (기존 (0,0) 계열에서 변경)
- `anchoredPosition = (0, 32)` — 핸드 패널과 동일 y. 기존 (40,40) 대체
- `sizeDelta = (912, 120)` 유지
- 슬롯 생성 로직(`RebuildSlots`) / HorizontalLayoutGroup / 드래그 슬롯 배선은 변경 없음
- `DreamcatcherHandView.FlipRoutine` 은 두 패널의 RectTransform 을 그대로 회전시키므로 코드 변경 불필요 — 좌표가 일치하면 자동으로 제자리 플립이 된다

## 완료 기준

- [x] 컴파일 클린 (콘솔 에러 0)
- [x] Play: Placement 진입 시 스트립이 하단 중앙에 표시, 7슬롯 드래그 배치 정상 동작
- [x] Play: 가호(각성) 게이지 버튼으로 스트립↔핸드 플립 시 두 패널이 같은 자리에서 접히고 펼쳐진다 (좌→중앙 점프 없음)
- [x] 웨이브/START 독(bottom-right)과 시각 충돌 없음

사용자 확인 2026-07-11 (Play 스크린샷 + 드래그 감각 확인).
