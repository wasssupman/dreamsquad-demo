# 3 — 첫 각성 상황별 힌트

## 목적

각성을 미리 설명하지 않고, 전투 중 현재 손패의 카드가 실제로 사용 가능해진 순간에만 버튼과 사용법을
한 번 알려준다. 카드 사용은 강제하지 않는다.

## 변경 대상

- `Assets/_Project/Scripts/UI/Tutorial/FirstSessionTutorialController.cs`
- `Assets/_Project/Scripts/UI/Dreamcatcher/AwakeningGaugeView.cs`
- `Assets/_Project/Scripts/UI/Dreamcatcher/DreamcatcherHandView.cs` (읽기 seam 확인/최소 보강)

## 구현

컨트롤러는 핵심 튜토리얼과 독립적으로 `DreamcatcherHandController.GaugeChanged`와 `HandChanged`를
구독한다. Battle 진입에도 즉시 재평가한다. Battle 페이즈이고 `Hand()` 중 `CanUse(entryId)`가 true인
카드가 하나 이상이며 힌트 버전이 pending이고 이번 판에 아직 제시하지 않았을 때만:

1. 각성 버튼을 펄스하고 `드림캐쳐 사용 준비 완료!`를 표시한다.
2. 첫 문구와 추가 펄스는 3~4초 뒤 자동으로 숨긴다. 프로필은 아직 완료하지 않으며 같은 판에는 재노출하지 않는다.
3. 플레이어가 기존 버튼을 눌러 `DreamcatcherHandView.HandOpened`가 발행되면 첫 usable 슬롯을 가리키며
   `포커스된 카드를 원하는 캐릭터로 끌어보세요!`를 표시한다.
4. 실제 Hand 상태와 usable 슬롯을 확인한 이 시점에 힌트 완료를 저장하고 짧은 style 시간 뒤 자동으로 숨긴다.

게이지 최대치가 아니라 **현재 손패에서 비용을 낼 수 있는 카드 존재**가 조건이다. `CanUse`는 타겟 존재가
아닌 비용 게이트라는 현재 의미를 유지한다. 카드 타입/타겟 규칙은 나열하지 않고 기존
카드 드래그의 화살표·대상 틴트·범위 프리뷰가 이어서 설명한다. 플레이어가 카드를 쓰지 않거나 손패를
닫아도 게임을 막지 않는다.

`AwakeningGaugeView`는 기존 버튼 hit Rect를 read-only로 노출한다. `DreamcatcherHandView`는 Refresh/Open
경계 뒤 `HandOpened`를 발행한다. `Slots`의 공개 slot rect/usable 정보를 재사용하고 계층 이름 검색은 하지
않는다. Battle 이탈·뷰 disable·핵심 튜토리얼 진행 중에는 힌트를 숨기며, 핵심 종료 후 조건을 다시 평가한다.

## 완료 기준

- [ ] 사용 가능한 카드가 없으면 게이지가 올라도 힌트가 뜨지 않는다.
- [ ] 첫 usable 순간에 버튼 한 곳만 강조되고 전투 입력은 계속 가능하다.
- [ ] 버튼을 누르지 않으면 3~4초 뒤 안내가 숨고 같은 판에는 다시 뜨지 않는다.
- [ ] 손패를 열면 usable 카드 하나만 가리키고 카드 타입 종합 설명은 없다.
- [ ] 손패 오픈 시 완료가 저장되어 다음 판에는 다시 뜨지 않는다.
- [ ] 핵심 튜토리얼과 동시 표시되지 않으며 Battle 이탈 시 즉시 정리된다.
- [ ] 카드 사용/취소/슬로모/손패 순환 기존 동작에 변경이 없다.

확인: 2026-07-19 · 구현 커밋 `da398417`
