# 5 — 선택 → 배치 → 배치 스킬 (B3)

## 목적

**유닛을 뽑아 적들 머리 위에 놓는다**를 손으로 해보게 하고, 그 결과(배치 스킬)를
눈으로 보여준다.

## 변경 대상

- `Assets/_Project/Scripts/UI/Tutorial/FirstRunTutorialController.cs` (스텝 추가)
- `Assets/_Project/Scripts/UI/DefenderSelector.cs` (특정 유닛 슬롯 조회)

## 구현

GO! 부터 `battleFreezeAtSeconds` 까지 **딤은 계속 떠 있다**(계약 5). 이 창을 열어두면
플레이어가 캐논을 먼저 놓아(`maxOnBoard: 1`) 또는 다른 유닛으로 코스트를 써서
아래 스텝이 통째로 스킵 조건에 걸린다.

`battleFreezeAtSeconds` 가 지나면 `Battle` 도메인을 0 으로 잡는다(우선순위 100).

### 3.1 유닛 터치

캐논 슬롯의 `RectTransform` 을 구멍으로 열고 포커스 링 + `"유닛을 터치 해보세요"`.

기존 `TryGetAffordableTutorialSlot` 은 "지금 살 수 있는 아무 슬롯"을 주는 **추천** API 라
여기에 못 쓴다. 캐논을 **지목**해야 하므로 `TryGetSlotRect(DefenderUnitData, out RectTransform)`
을 더한다(`_slotVisuals` 의 `data`/`rect` 로 5줄). 두 API 는 성격이 다르다.

⚠ **완료 조건은 신호 둘을 다 받는다.** `Armed` 는 트레이 셀 **탭**(tap-to-place)에서만
울린다. 주 경로인 드래그는 `UserDragStarted`/`DragBegan` 을 낸다. `Armed` 만 기다리면
드래그로 배치한 사람은 3.1 에서 타임아웃까지 멈춰 있고 3.2 의 `PlacementCommitted` 가
먼저 도착해 스텝 순서가 깨진다.

⚠ **지불 판정은 정지 전에 한다.** 정지 중에는 코스트도 배치 쿨타임도 회복되지 않는다
(`CostRuntime`/`PlacementCooldownRuntime` 이 `DeltaTime(Battle)` 를 쓴다). 「기다리면
가능해진다」가 없으므로, 정지 시점에 캐논이 **소진(`maxOnBoard`)·쿨타임·코스트 부족**
중 하나라도 걸리면 **아예 정지하지 않고 이 구간을 건너뛴다**(그리고 계약 11 에 따라
완료로 기록하지 않는다).

### 3.2 배치

`"적들의 머리위에 캐논을 배치 해보세요!"` (원문 그대로). Duel 은 Ground 배치 가능 칸이
전부 적이 걷는 Walk 타일이라 이 문장이 **문자 그대로 성립한다.**

배치 가능 칸 전체를 하이라이트하고 **어느 칸에 놓든 통과**시킨다. 지정된 한 칸을
강제하지 않는다 — 이유 둘:

- 원문은 영역 지시이지 특정 칸 지시가 아니다.
- **딤+구멍으로는 드롭 칸을 제한할 수 없다.** 트레이→보드 드래그는 이미 시작된 UGUI
  드래그라 딤 이미지를 통과하고, 드롭 셀은 보드 레이캐스트가 정한다(`CommitPlacementAt`
  에는 UI 필터가 없다). 구멍을 하나만 뚫어도 실제로는 아무 칸에나 놓인다 — 완료 기준으로
  삼으면 통과 못 하는 조항이 된다.

완료 조건 = `PlacementCommitted`.

⚠ 캐논은 배치 컷신(`DeployCutscenePlayer`, sortingOrder 20050)을 갖고 있어 안내
(1500) 위를 잠깐 덮는다. 시간은 unscaled 라 정지에 걸리지 않는다 — 3.3 문구는 컷신이
끝난 뒤에 띄운다.

### 3.3 배치 스킬 관람

배치가 확정되면 **정지를 푼다**(딤은 유지). `onPlaceWatchSeconds` 동안 배치 스킬이
실제로 적을 때리는 것을 보여준 뒤 다시 정지하고
`"강력한 배치스킬들을 활용하여 전황을 유리하게 이끌어 보세요"`.

이 재개가 이 구간의 핵심이다 — 정지한 채로 문구만 띄우면 "전황을 유리하게"가 말뿐이
된다. 배치 스킬 발동 자체는 기존 경로(`TriggerDeploymentOnPlaceSkill`)가 한다.
튜토리얼은 **아무것도 발동시키지 않는다**(계약 1).

## 완료 기준

- compile 통과.
- GO! 부터 정지까지 딤이 유지되어 플레이어가 유닛을 미리 놓을 수 없다.
- 정지 시점에 **적이 화면에 보인다**(`battleFreezeAtSeconds` 실측 튜닝의 판정 기준).
- 캐논 슬롯만 눌리고, 탭·드래그 **어느 쪽으로 시작해도** 3.1 이 통과된다.
- 배치 확정 → 시간이 흐르며 배치 스킬이 적을 때리는 것이 보인다 → 다시 멈추고 문구.
- 캐논이 소진/쿨타임/코스트 부족이면 정지 없이 이 구간을 건너뛰고, 그 판은 완료로
  기록되지 않는다.
- `stepTimeoutSeconds` 동안 아무것도 안 눌러도 다음으로 넘어가고 딤·정지가 정리된다.
