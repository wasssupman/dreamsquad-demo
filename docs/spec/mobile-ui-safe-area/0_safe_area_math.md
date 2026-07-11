# 0 — Safe Area 순수 좌표 계약

## 목적

물리 픽셀 기준 `Screen.safeArea`를 Canvas용 정규화 anchor로 바꾸는 로직을 Unity UI 계층과 분리한다. notch 방향, 하단 gesture inset, full-screen fallback을 EditMode에서 결정론적으로 검증한다.

## 변경 대상

- 신규 `Assets/_Project/Scripts/UI/Layout/UiSafeAreaMath.cs`
- 신규 `Assets/_Project/Tests/EditMode/UiSafeAreaMathTests.cs`

## 구현

- 입력은 plain `Rect safeArea`, `Vector2 screenSize`, 출력은 `anchorMin/anchorMax` 값 구조체로 둔다.
- `x/screenWidth`, `y/screenHeight`, `(x+width)/screenWidth`, `(y+height)/screenHeight`를 계산하고 0~1로 clamp한다.
- screen width/height가 0 이하이거나 safe rect가 유효하지 않으면 `(0,0)~(1,1)` full-screen으로 폴백한다.
- left/right notch와 bottom gesture inset을 동시에 표현할 수 있어야 한다.
- 런타임 타입, Scene object, singleton을 참조하지 않는 순수 static 계산으로 유지한다.

## 완료 기준

- [x] EditMode: 1920×1080 full rect가 full anchor를 반환한다.
- [x] EditMode: 좌/우 cutout 입력이 대응하는 x anchor를 반환한다.
- [x] EditMode: bottom gesture inset 입력이 양의 `anchorMin.y`를 반환한다.
- [x] EditMode: 잘못된 screen/safe rect가 NaN 없이 full-screen으로 폴백한다.
- [x] 컴파일 에러 0, 기존 테스트 회귀 없음.

사용자 진행 승인 2026-07-11 · 구현 커밋 `38ca7295` — 시각 확인 가능한 unit까지 연속 진행 요청. EditMode 680개 중 678 통과, 실패 0, 기존 Ignore 2.
