# 8 — 선물 튜토리얼 오케스트레이션 + guidance elevated 모드

## 목적

홀드 도달 이벤트를 받아 기존 튜토리얼 말풍선으로 문구를 표시하고, 셔플 홀드 통과 시 완료를
저장한다. 말풍선 canvas 를 선물 패널 위로 올리는 elevated 모드를 추가한다.

## 변경 대상

- `Assets/_Project/Scripts/UI/Tutorial/TutorialGuidanceView.cs`
- `Assets/_Project/Scripts/UI/Tutorial/FirstSessionTutorialController.cs`

## 구현

**elevated 모드**: `TutorialGuidanceView`의 canvas sortingOrder 는 10으로 `GiftPanel`(30)에
가려진다. `SetElevated(bool)`을 추가해 canvas sortingOrder 를 10 ↔ 40 으로 전환한다(선물 패널 위,
메뉴 팝업과의 관계는 unit 9 QA 에서 확인). `Hide()`는 order 를 건드리지 않는다 — 원복은
오케스트레이터의 종료 경로가 명시적으로 수행한다.

**컨트롤러**: `GiftPhaseView giftView` SerializeField 를 추가하고 `TutorialHoldEntered` /
`TutorialHoldReleased`를 구독한다. giftView 미배선이면 경고 후 생략(fail-open — view 의 홀드는
view 탭만으로도 진행되므로 문구만 빠진다).

- `HoldEntered(Reveal)`: `guidance.SetElevated(true)` + 문구 표시(`showSkip: false`).
  문구: `"{kind}의 선물은 내 덱 {base}장에 더해 꿈결의 집행자들이 {added}장의 추가 드림캐쳐를 제공합니다."`
  kind 는 `handController.GiftKind`(루시드/림), 수량은 `GiftBaseCards.Count` /
  `GiftAddedCards.Count` 실측치 — 문구가 데이터와 어긋나지 않는다.
- `HoldReleased(Reveal)`: 문구 숨김(elevated 유지 — 스택 수렴은 짧다).
- `HoldEntered(Shuffle)`: 문구 `"{base}장 + {added}장의 카드가 무작위로 섞여서 덱 순서가 배정됩니다."`
- `HoldReleased(Shuffle)`: **완료 저장 지점**(2026-07-20 사용자 결정) —
  `TutorialProgress.CompleteGiftTutorial` + `TrySaveProfile()`(기존 try/catch seam 재사용).
  문구 숨김 + `SetElevated(false)`.

**정리 경로**: `OnPhaseChanged`가 Gift → 다른 페이즈 전환을 보면(정상 종료 포함) gift 안내가
떠 있을 경우 문구 숨김 + de-elevate 한다. `OnDisable`도 동일. 핵심 안내(`_coreActive`)와 선물
튜토리얼은 페이즈가 달라 동시 활성 불가 — 상호 간섭 방어 코드는 만들지 않는다.

기믹 안내(`GimmickGuideView`)는 Placement 소속이라 Gift 페이즈와 겹치지 않는다 — suppress 불필요.

## 완료 기준

- [x] compile clean.
- [ ] 홀드 1·2에서 말풍선이 선물 패널 위에 보이고, 탭 진행은 말풍선에 가로채이지 않는다
      (Skip 버튼 비노출 = raycast 대상 없음).
- [ ] 문구의 kind·수량이 실제 선물 종류·카드 수와 일치한다(루시드/림 양쪽).
- [ ] 셔플 홀드 해제 시 완료가 저장되어 다음 판부터 일반 연출로 노출된다.
- [ ] 흡수/배치 진입 후 말풍선·elevated 상태가 남지 않는다(핵심 안내 order 10 회귀 없음).
- [ ] 저장 실패 시 경고 후 연출·게임 진행이 계속된다.

구현: 2026-07-20 · 커밋 `9e75c0ae` · `SetElevated`(10↔40) + 컨트롤러 홀드 구독 + 셔플 해제 저장. 씬 `giftView` 배선 완료.
