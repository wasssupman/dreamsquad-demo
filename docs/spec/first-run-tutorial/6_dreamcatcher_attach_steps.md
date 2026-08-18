# 6 — 드림캐쳐 부착 (B4) · 튜토리얼 닫기

## 목적

**배치한 유닛에 드림캐쳐를 붙인다**까지 잇고 튜토리얼을 닫는다.

## 변경 대상

- `Assets/_Project/Scripts/UI/Tutorial/FirstRunTutorialController.cs` (스텝 추가 · 완료 기록)

## 구현

B3 이 끝나면 정지를 풀고 `resumeBeforeAttachSeconds` 동안 판을 정상 속도로 돌린다.
**딤은 유지한다** — 이 창을 열면 플레이어가 각성 카드를 먼저 써버릴 수 있고, 게이지
여유는 정확히 0(시작 20 / 부착 비용 20)이라 그 즉시 아래 스텝이 사라진다.

그 뒤 다시 `Battle` 을 0 으로 잡는다(우선순위 100 — 이게 없으면 유닛을 고르는 순간
판이 0.3배로 흐른다).

### 4.1 보드의 캐논 선택

대상은 **트레이 셀이 아니라 보드에 배치된 캐논**이다 — 드림캐쳐는 배치된 유닛에만 붙는다.
unit 5 에서 놓은 유닛의 화면 좌표를 감싸는 임시 `RectTransform` 을 구멍으로 열고
포커스 링 + `"다시 캐논 유닛을 선택 해보세요"` (원문 그대로).

좌표는 기존 API 로 얻는다: `BattleBridge.TryGetDeployedEntity(DefenderUnitData, out Entity)`
→ `TryGetUnitViewAnchor(Entity, out Transform)`. (배치 칸을 알고 있으면
`GridCellToViewCenter(Vector2Int)` 도 있다.)

완료 조건 = `DreamcatcherHandView.SelectionTargetSet`.

⚠ 플레이어 보드 탭이 타는 경로는 `DcInspectController.HandleTap → TryPick → Select` 다
(`SelectDeployed` 는 소진된 트레이 셀 탭 전용이라 이 흐름이 아니다 — 둘 다 결국
`SetSelectionTarget` 에 도달하므로 완료 조건 자체는 같다).

⚠ **이미 그 유닛이 선택돼 있으면 재탭이 «닫기»가 된다**(`entity == _selected` → `CloseByIntent`,
`SelectionTargetSet` 미발화). B4 진입 시 기존 선택을 먼저 해제하거나, 진입 시점에
이미 선택돼 있으면 4.1 을 즉시 성공 처리한다.

⚠ 그 유닛이 죽었으면(재개 5초 사이에 맞을 수 있다) **살아 있는 배치 유닛 아무나로
대상을 바꾸고 문구도 그 유닛 이름으로 바꾼다** — "캐논"이라 말하며 다른 유닛을 가리키지
않는다. 살아 있는 유닛이 하나도 없으면 이 구간을 건너뛴다.

### 4.2 카드 선택

유닛 선택이 손패를 연다(`OpenForSelection`). 카드 `RectTransform` 은 이미 공개돼 있다
(`DreamcatcherHandView.Slots` → `CardSlot.rect`) — 새 API 가 필요 없다.

⚠ **구멍은 «지금 부착 가능한» 카드에만 뚫는다.** 판정은 `CardSlot.Playable`
(`usable && !attachBlocked`) **그리고 `CardType != Active`**.

액티브 카드는 함정이 둘이다: 즉발 탭이 거절되고("이 카드는 끌어서 사용하세요"), 커밋
경로가 `SpendAndRecycle` 이라 **`AttachmentsChanged` 를 발화하지 않는다** → 각성 20 을
쓰고도 안내가 안 넘어간다. 게다가 딤이 보드를 막고 있어 끌 곳도 없다.
(현재 `DreamcatcherDeck_Default` 10장은 전부 Unit 타입이라 우연히 안 터지지만, 덱 저작
한 번으로 되살아나는 잠복 결함이다.)

문구: `"하단 드림캐쳐 4개중 맘에 드는것을 터치 해보세요"` (원문 그대로).
열 수 있는 카드가 4장보다 적으면 문구의 "4개"가 화면과 어긋나므로, 그때는 숫자 없는
대체 문구를 쓴다.

⚠ **완료 조건은 `AttachmentsChanged` 그대로가 아니다.** 이 이벤트는 **유닛 사망/퇴근
회수**로도 울린다 — 재개 구간에 부착이 하나라도 있었고 그 호스트가 죽으면 카드를
안 골랐는데 스텝이 완료된다. 조건을 **「부착 등록부가 늘어난 경우」** 로 좁힌다.

지불 가능성 자체는 안전하다: 게이지 시작 20 · 부착 비용 20 이라 정지 시점에 최소
1장은 항상 낼 수 있고, 킬로 게이지가 오르면 더 낼 수 있다 — 딤으로 미리 소비를 막는
한(계약 5).

### 4.3 마무리

부착 연출이 끝나고 `attachSettleSeconds` 뒤에 문구만 띄운다(구멍 없음):
`"드림캐쳐를 유닛에게 부착하여 더 강해질 가능성을 열어보세요!"` (원문 그대로).

### 닫기

문구가 사라지면 딤을 내리고 시간 lease 를 반납한다(계약 7 의 공용 해제 지점).

**완료 기록은 여기서만 한다.** `firstRunTutorialDone = true` + `ProfileStore.Save`.
⚠ **B3 또는 B4 가 스킵/타임아웃으로 끝났으면 기록하지 않는다**(계약 11) — 1회성이라
기록해버리면 핵심을 한 번도 못 본 계정이 다시 볼 기회를 영영 잃는다.

그 뒤 판은 Duel 의 3분 만료까지 정상 진행된다(`Deck_Duel.timerDurationSec: 180`).
점수·제출은 손대지 않는다.

## 완료 기준

- compile 통과.
- B3 후 판이 `resumeBeforeAttachSeconds` 만큼 정상 진행되고, 그동안 딤이 유지돼
  플레이어가 카드를 미리 쓸 수 없다.
- 다시 정지했을 때 **유닛을 선택해도 적이 움직이지 않는다**(우선순위 100 검증).
- 보드의 캐논을 탭하면 손패가 열리고 **부착 가능한 Unit 카드에만** 구멍이 뚫린다.
- 액티브 카드가 손패 앞면에 있어도 스텝이 막히지 않는다.
- 카드를 탭하면 기존 부착 연출이 나오고 이어서 마무리 문구가 뜬다.
- 부착 없이 호스트가 죽어 `AttachmentsChanged` 가 울려도 스텝이 넘어가지 않는다.
- 문구가 끝나면 딤이 사라지고 판이 정상 속도로 돌아간다 — 남은 시간 동안 자유 플레이.
- 판이 3분에 스스로 종료되고 결과 화면이 뜬다.
- 정상 완료 후 로비 재진입 → 튜토리얼이 뜨지 않는다.
- **스킵/타임아웃으로 끝난 판 → 다음 판에서 처음(L)부터 다시 뜬다.**
- `RESET TUTORIAL` 을 누르면 다시 뜬다.
