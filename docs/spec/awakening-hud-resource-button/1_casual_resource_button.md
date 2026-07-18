# 1 — 캐주얼 각성 자원 버튼

## 목적

기존 장식형 오브를 큰 숫자 중심의 캐주얼 액션 버스트 버튼으로 교체한다. 플레이 중
획득하는 각성치가 쌓이고 소비되는 자원이라는 감각과 드림캐쳐 덱 진입 행동을 한 버튼 안에서
전달한다.

## 변경 대상

- `Assets/_Project/Art/UI/AwakeningBurstFrame.png(.meta)`
- `Assets/_Project/Scripts/UI/Dreamcatcher/AwakeningGaugeView.cs`
- `Assets/_Project/Scenes/BattleScene.unity`

## 구현

- ImageGen 생성 에셋: 두꺼운 네이비 외곽, 보라 젤리 본체, 청록/노랑의 둥근 색 블록.
  텍스트·숫자·아이콘·상징은 에셋에 굽지 않고 중앙은 완전히 비운다.
- 꿈·별·밤·달·구름·깃털·마법·타로·천체 문법을 금지한다.
- 중앙 TMP 숫자를 76px로 가장 크게 배치한다. 아래의 `/100` 또는 `MAX!`만 보조 위계다.
- 중앙 원형 face를 Mask로 자르고 `Image.Type.Filled/Vertical` 액체면으로 `Gauge/GaugeMax`를
  표시한다. 수면 캡슐은 현재 높이를 보조한다.
- `GaugeChanged`는 숫자·액체 높이·본체 발광을 갱신하고 숫자 punch, 획득 `+N`, 젤리 squash를 표시한다.
- 최초 100 도달은 1회 burst, 이후에는 1.15초 간격의 작은 ready bounce를 사용한다.
- 0값은 휴면 톤, 손패 open은 청록 선택 톤으로 상태를 구분한다. 상시 pulse는 두지 않는다.
- Sprite 미할당 시 `UiRoundedSprite.MakeCircle` 폴백으로 기능을 보존한다.

## 완료 기준

- 새 Sprite가 UI 타입·alpha transparency·mipmap off로 임포트되고 씬 슬롯에 배선된다.
- 값 0/중간/최대에서 숫자와 액체면의 상태가 일치한다.
- 버튼 클릭으로 기존 Dreamcatcher 손패가 열리고 다시 닫힌다.
- 컴파일/콘솔 오류 0.
