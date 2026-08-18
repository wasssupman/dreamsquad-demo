# 7 — 드림캐쳐 부착 (B4)

## 목적

**배치한 유닛에 드림캐쳐를 붙인다**까지 잇고 튜토리얼을 닫는다.

## 변경 대상

- `Assets/_Project/Scripts/UI/Tutorial/FirstRunTutorialController.cs` (스텝 추가 · 완료 기록)

## 구현

B3 이 끝나면 딤을 내리고 `resumeBeforeAttachSeconds` 동안 판을 정상 속도로 돌린 뒤
다시 `Battle` 을 0 으로 잡는다.

**4.1 보드의 캐논 선택.** 대상은 **트레이 셀이 아니라 보드에 배치된 캐논**이다 —
드림캐쳐는 배치된 유닛에만 붙는다(`DcInspectController.SelectDeployed`). unit 6 에서
놓은 그 유닛의 화면 좌표를 감싸는 임시 `RectTransform` 을 구멍으로 열고 포커스 링 +
`"다시 캐논 유닛을 선택해보세요"`. 완료 조건 = `DreamcatcherHandView.SelectionTargetSet`.

그 유닛이 이미 죽었으면(4초+5초 사이에 맞을 수 있다) **살아 있는 배치 유닛 아무나로
대상을 바꾼다.** 하나도 없으면 이 구간을 건너뛰고 튜토리얼을 닫는다.

**4.2 카드 선택.** 유닛 선택이 손패를 연다(`OpenForSelection`). 손패 카드 4장의
`RectTransform` 을 구멍으로 열고 `"하단 드림캐쳐 4개 중 맘에 드는 것을 터치해보세요"`.
완료 조건 = `DreamcatcherHandController.AttachmentsChanged`.

각성 게이지는 시작 20, 부착 비용 20 이라 **정확히 1장** 낼 수 있다. 손패 앞면에
지불 가능한 카드가 하나도 없으면(전량 Squad/Active 라 비용이 안 맞는 경우) 이
구간을 건너뛴다 — 못 내는 카드를 가리키면 막힌다.

**4.3 마무리.** 부착 연출이 끝나고 `attachSettleSeconds` 뒤에 문구만 띄운다(구멍 없음):
`"드림캐쳐를 유닛에게 부착하여 더 강해질 가능성을 열어보세요!"`

**닫기.** 문구가 사라지면 딤을 내리고 시간 리스를 반납한다. `firstRunTutorialDone = true`
로 기록하고 프로필을 저장한다(**여기가 유일한 기록 지점** — unit 0). 그 뒤 판은
3분 만료까지 정상 진행된다. 점수·제출은 손대지 않는다.

## 완료 기준

- compile 통과.
- B3 후 판이 `resumeBeforeAttachSeconds` 만큼 정상 진행되고 다시 멈춘다.
- 보드의 캐논을 탭하면 손패가 열리고 카드 4장만 눌린다.
- 카드를 탭하면 기존 부착 연출이 그대로 나오고, 이어서 마무리 문구가 뜬다.
- 문구가 끝나면 딤이 사라지고 판이 정상 속도로 돌아간다 — 남은 시간 동안 자유 플레이.
- 판을 끝내고 로비로 나갔다 다시 들어오면 튜토리얼이 뜨지 않는다.
- 튜토리얼 중 판을 나가면 다음 판에서 처음(L)부터 다시 뜬다.
- `RESET TUTORIAL` 을 누르면 다시 뜬다.
