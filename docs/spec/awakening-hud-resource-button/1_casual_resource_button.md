# 1 — 캐주얼 각성 자원 버튼

## 목적

기존 반투명 사각 패널+가로 막대를 큰 숫자 중심의 캐주얼 꿈 오브로 교체한다. 플레이 중
획득하는 각성치가 쌓이고 소비되는 자원이라는 감각과 드림캐쳐 덱 진입 행동을 한 버튼 안에서
전달한다.

## 변경 대상

- `Assets/_Project/Art/UI/AwakeningDreamOrbFrame.png(.meta)`
- `Assets/_Project/Scripts/UI/Dreamcatcher/AwakeningGaugeView.cs`
- `Assets/_Project/Scenes/BattleScene.unity`

## 구현

- ImageGen 생성 에셋: 둥근 보라 꿈 오브, 두꺼운 크림/골드 테두리, 상단 달, 하단 짧은
  파스텔 깃털. 중앙은 숫자를 위한 넓은 다크 네이비 영역. 텍스트는 에셋에 굽지 않는다.
- 중앙 TMP 숫자를 72px로 가장 크게 배치한다. 작은 `드림캐쳐` 라벨은 보조 위계다.
- 외곽 충전 링은 `Image.Type.Filled/Radial360`으로 `Gauge/GaugeMax`를 표시한다. 링은 숫자를
  가리지 않고 0에서도 약한 트랙이 남는다.
- `GaugeChanged`는 숫자·링·오브 발광을 갱신하고 숫자 punch와 획득 `+N`을 표시한다.
- 0값은 휴면 톤, 손패 open은 청록 선택 톤으로 상태를 구분한다. 상시 pulse는 두지 않는다.
- Sprite 미할당 시 `UiRoundedSprite.MakeCircle` 폴백으로 기능을 보존한다.

## 완료 기준

- 새 Sprite가 UI 타입·alpha transparency·mipmap off로 임포트되고 씬 슬롯에 배선된다.
- 값 0/중간/최대에서 숫자와 링의 상태가 일치한다.
- 버튼 클릭으로 기존 Dreamcatcher 손패가 열리고 다시 닫힌다.
- 컴파일/콘솔 오류 0.
