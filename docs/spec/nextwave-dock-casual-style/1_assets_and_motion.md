# 1 — 캐주얼 도크 에셋과 모션

## 변경 대상

- `Assets/_Project/Art/UI/NextWaveDockFrame.png(.meta)`
- `Assets/_Project/Art/UI/NextWaveButtonFace.png(.meta)`
- `Assets/_Project/Scripts/UI/NextWaveDock.cs`

## 구현

- ImageGen 프레임: 넓은 둥근 네이비 플레이트, 보라 젤리 외곽, 청록/노랑의 둥근 색 블록.
- ImageGen 버튼: 넓고 통통한 청록 젤리 버튼, 네이비 하단 그림자와 보라/노랑 포인트.
- 두 에셋 모두 투명 배경, 무텍스트, 무아이콘, 무상징이다.
- 프레임 Sprite가 없으면 기존 절차 backing/button 색상으로 기능을 보존한다.
- 타이머 숫자에 진한 외곽선, 버튼 라벨에 네이비 외곽선을 적용한다.
- `EventTrigger`로 pointer down/up/exit 눌림 반응을 View 내부에서 구성한다.

## 완료 기준

- Sprite importer: UI Sprite, alpha transparency, mipmap off.
- disabled 상태에서도 `웨이브 없음`을 읽을 수 있고 버튼이 눌릴 수 없음을 명확히 보인다.
- 컴파일/Console error 0.
