# 1 — 공통 체력바 View

## 목적

방어/적이 동일한 수명주기와 레이아웃 계산을 사용하되, 프레임·폭·색·두께로 즉시 구분되는 상시 체력바를 만든다.

## 변경 대상

- `Assets/_Project/Scripts/Presentation/UnitOverheadUiLayer.cs`
- `Assets/_Project/Scripts/Presentation/UnitOverheadView.cs`
- `Assets/_Project/Scripts/Presentation/{SpineUnitView,QuadUnitView}.cs`

## 구현

- entity별 View 풀, frame reconcile, 사망/despawn/teardown 회수.
- 절차 생성 텍스처: 얇은 검정 외곽선, 네이비/차콜 2-tone 프레임, 1px 상단 highlight와 내부 shadow.
- 실제 전투 줌에서 읽히는 컴팩트 비율을 유지하고 과한 장갑/금속 장식은 넣지 않는다.
- fill gradient + delayed-damage trail. 만피 상시 저alpha.
- 실제 renderer screen bounds top-center와 reference px 레이아웃 사용.

## 완료 기준

- 방어/적이 같은 View 타입을 사용하고 스타일 변형만 다르다.
- 화면 가장자리와 카메라 pitch 변화에도 수평 오프셋 drift 없이 5px 간격을 유지한다.
