# 6 — 선택 → 배치 → 배치 스킬 (B3)

## 목적

**유닛을 뽑아 길목에 놓는다**를 손으로 해보게 하고, 그 결과(배치 스킬)를 눈으로
보여준다.

## 변경 대상

- `Assets/_Project/Scripts/UI/Tutorial/FirstRunTutorialController.cs` (스텝 추가)
- `Assets/_Project/Scripts/UI/DefenderSelector.cs` (특정 유닛 슬롯 조회)

## 구현

전투 시작 후 `battleFreezeAtSeconds` 가 지나면 `Battle` 도메인을 0 으로 잡는다.

**3.1 유닛 터치.** 캐논 슬롯의 `RectTransform` 을 구멍으로 열고 포커스 링 +
`"유닛을 터치해보세요"`. 완료 조건 = `DefenderDragPlacementController.Armed`.

기존 `TryGetAffordableTutorialSlot` 은 "지금 살 수 있는 아무 슬롯"을 주는 추천 API 라
여기에 못 쓴다. **캐논을 지목**해야 하므로 `TryGetSlotRect(DefenderUnitData, out RectTransform)`
을 더한다. 두 API 는 성격이 다르다 — 하나는 추천, 하나는 지목이다.

정지 시점의 코스트는 10(시작값, 상한 10)이고 캐논은 5라 지불 가능하다. 그래도
**슬롯이 소진/비용 초과면 이 스텝을 건너뛴다** — 못 누르는 것을 가리키면 튜토리얼이 막힌다.

**3.2 배치.** 목표 칸 하나만 구멍으로 열고 `"적들이 몰려오는 길목에 캐논을 배치해보세요"`.
완료 조건 = `PlacementCommitted`.

목표 칸은 **저작 값**이다 — `FirstRunTutorialConfig` 의 `targetCell`(Vector2Int). 고정
맵이라(unit 1) 좌표를 못 박을 수 있다. 런타임에 "경로에 가장 가까운 배치 가능 칸"을
계산하지 않는다: 규칙이 지형과 함께 조용히 어긋나고, 어긋난 순간 튜토리얼이 막힌다.

**⚠ 문구는 "머리 위"가 아니다.** 캐논은 `placementLayers: Ground` 라 경로 타일 위에는
놓을 수 없다. 배치 스킬(SkyStrike)이 하늘에서 떨어져 적을 때리므로 "길목"으로 충분하다.

**3.3 배치 스킬 관람.** 배치가 확정되면 **딤을 내리고 정지를 푼다.**
`onPlaceWatchSeconds` 동안 배치 스킬이 실제로 적을 때리는 것을 보여준 뒤 다시 정지하고
`"강력한 배치 스킬들을 활용하여 전황을 유리하게 이끌어 보세요"`.

이 재개가 이 스텝의 핵심이다 — 정지한 채로 문구만 띄우면 "전황을 유리하게"가 말뿐이
된다. 배치 스킬 발동 자체는 기존 경로(`TriggerDeploymentOnPlaceSkill`)가 한다.
튜토리얼은 **아무것도 발동시키지 않는다**(계약 1).

## 완료 기준

- compile 통과.
- 전투 `battleFreezeAtSeconds` 후 적·유닛이 멈추고 캐논 슬롯만 눌린다.
- 캐논을 탭하면 목표 칸만 열리고, 다른 칸에는 놓이지 않는다.
- 배치 확정 → 시간이 흐르며 배치 스킬이 적을 때리는 것이 보인다 → 다시 멈추고 문구.
- 캐논이 소진/비용 초과 상태면 이 구간을 건너뛰고 B4 로 간다(막히지 않는다).
- `stepTimeoutSeconds` 동안 아무것도 안 눌러도 다음으로 넘어간다.
