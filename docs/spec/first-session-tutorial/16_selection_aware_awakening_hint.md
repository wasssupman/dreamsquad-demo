# 16 — 각성 안내를 두 개의 문으로 (오픈 경로별 부착 안내)

> 추가 2026-07-30. unit 15 선행 필수(첫 판 경계가 흐리면 이 unit 의 검증이 성립하지 않는다).

## 목적

손패를 여는 경로가 **항아리 + 유닛 선택** 둘로 늘었는데 안내는 항아리만 가리킨다. 그리고
`HandOpened` 는 `OpenRoutine()` 끝에서 **오픈 경로를 구분하지 않고** 발화하므로
(`DreamcatcherHandView.cs:1041`), 선택으로 연 플레이어에게도 드래그 문구가 뜨고 그 시점에
`awakeningHintVersion` 이 저장돼 **탭 즉발을 영영 배우지 못한다**
(`selection-hand-attach/README.md` 후속 후보 · critic L4 · unit 5 시나리오 12).

**신규 스텝은 만들지 않는다.** 온보딩 총량이 이미 안내 14비트·22줄, 순수 해제 탭 5회로
경고 상태다(`docs/spec/README.md` Follow-up Backlog → 온보딩 총량 [M]). 기존 3단계의
**문구와 분기만** 바꾼다.

## 변경 대상

- `Assets/_Project/Scripts/UI/Tutorial/FirstSessionTutorialController.cs` (유일)

신규 씬 배선 0 · 신규 이벤트 0 — 컨트롤러가 이미 `handView` 를 들고 있고
`DreamcatcherHandView.InSelectionMode` 는 public 이다.

## 구현

### A. 0단계 — 두 개의 문

| | 문구 |
|---|---|
| 현재 | `여기서 드림캐쳐 덱을 열어보세요` |
| 개정 | `항아리를 누르거나 캐릭터를 탭하면`<br>`드림캐쳐 덱이 열립니다` |

포커스 링은 **항아리 그대로**다 — 링이 첫 번째 문을 시연하고 문구가 두 번째 문을 알린다.
표시 타이밍·한 프레임 지연·패널 활성 재확인·`_awakeningIntroShownThisBattle` 은 전부 불변
(unit 12 의 되돌림 금지 항목).

**A단계 문구는 손대지 않는다.** 현 튜닝(`gaugeStart 20` · 카드 비용 전부 20)에서 Battle 진입
즉시 `HasAffordableCard()` 가 참이라 A 는 `OnPhaseChanged` 안에서 **동기로** 뜨고, 다음 프레임
0단계 코루틴이 같은 배너를 덮어써 **사실상 한 프레임만 존재한다**. 여기에 새 정보를 넣으면
아무도 못 읽는다. 구조 자체는 unit 12 의 설계대로 동작 중이므로 이 spec 에서 고치지 않는다
(후속 후보).

### B. B단계 — 오픈 경로로 분기

`OnHandOpened` 에서 `handView.InSelectionMode` 하나로 갈린다.

- **일반 오픈**(`!InSelectionMode`) — 기존 문구 유지:
  `포커스된 카드를 원하는 캐릭터로 끌어보세요!`
- **선택 오픈**(`InSelectionMode`) — 신규:
  `카드를 탭하면 이 캐릭터에 바로 부착됩니다`
  `왼쪽에서 능력치와 부착 상태를 볼 수 있어요`

**포커스는 두 경우 모두 usable 슬롯 그대로**다. 좌측 패널로 옮기지 않는다 — 지시의 대상은
카드이고, 패널은 문구가 가리키기만 해도 눈에 들어오는 위치다(사용자 결정 2026-07-30).

`InSelectionMode` 는 `SelectionTarget != Entity.Null` 파생값이고, 컨트롤러가
`SetSelectionTarget` → `OpenForSelection` 순으로 부르므로(`DcInspectController.cs:325-326`)
`HandOpened` 발화 시점엔 이미 확정돼 있다. 래치 경로(`TickPendingSelectionOpen`)도 같은
`Open()` 을 지나므로 동일하다.

### C. 건드리지 않는 것

나머지 가드(`_awakeningOfferedThisBattle` · `_awakeningArmedThisBattle` · 페이즈 ·
usable 슬롯 탐색 · `ShouldRunAwakeningHint`)와 **저장 시점 · disarm · `OnCardPeeked` 조기
해제 · `_cardInstructionShowing` 수명**은 전부 그대로다. 검증된 상태머신에 손대지 않는 것이
이 unit 의 설계 제약이다.

**완료 저장은 첫 B 1회**다. 한 판에 한쪽 문만 쓰면 반대쪽 문구는 못 본다 — 두 문구가 각자
완결된 사용법이라 어느 쪽을 봐도 막히지 않는다. 판당 경로별 1회로 늘리려면
`_awakeningArmedThisBattle` 래치와 `ShouldRunAwakeningHint` 가드를 함께 재설계해야 해서
이 spec 범위 밖이다(후속 후보).

## 알려진 한계 — `gaugeStart` 의 여유가 0 이다

0단계가 "캐릭터를 탭하면 열립니다"로 **행동을 유도**하는데, B 단계는
`_awakeningOfferedThisBattle`(= A 가 실제로 떴다)를 요구한다(unit 12 의 의도된 가드).

현 튜닝은 `gaugeStart 20` · `costSquad/costUnit/costActive` **전부 20** 이다. Battle 진입 즉시
`20 >= 20` 이라 `HasAffordableCard()` 가 참 → A 가 같은 프레임에 뜨고 B 도 정상 발화한다.
**지금은 문제 없다.** 다만 **여유가 정확히 0** 이라는 점을 기록한다:

| 변경 | 결과 |
|---|---|
| 카드 비용을 21 이상으로 | Battle 진입에 A 가 안 뜬다 |
| `gaugeStart` 를 19 이하로 | 〃 |

둘 중 하나라도 일어나면 0단계 안내를 따라 유닛을 탭했을 때 손패는 열리는데 **아무 문구도
안 뜬다**(카드도 전부 dim) — 시킨 대로 했는데 반응이 없는 상태가 된다. 둘 다 시트 튜너블이다
(`DcSheetImportDto.gaugeStart`/`costUnit`/…). 그때는 0단계 문구를 유도형에서 위치 안내형으로
낮추거나 0·A 순서를 재설계한다. **지금 선제 분기는 만들지 않는다** — 안 쓰는 경로가 된다.

## 부수 효과 — 부착 1회로 세션이 닫힌다

같은 튜닝에서 카드 1장을 쓰면 게이지가 0 이 되어 `UsableCardsExhausted` → 손패 자동 닫힘 +
**선택 해제**까지 간다(`selection-hand-attach` unit 8). 즉 B-선택 문구를 읽고 카드를 탭하는 순간
패널·리티클·손패가 한꺼번에 걷힌다.

문구는 손패가 **열릴 때** 뜨므로 탭 전에 읽힌다 — 안내 자체는 성립한다. 다만 "왼쪽에서 능력치와
부착 상태를 볼 수 있어요" 를 확인하려면 유닛을 다시 탭해야 한다(그때는 카드가 전부 dim 인 채
패널만 뜬다 — 정상 동작). Play 에서 이 연쇄가 어색하지 않은지 체감 확인 항목으로 둔다.

## 검증 준비

`15_first_match_selection_lockout.md` 의 **검증 준비** 절과 공유한다 — 리셋은 로비의
`OnResetTutorial()` 하나뿐이고 5개 플래그를 통째로 되돌린다. **units 15·16 을 함께 구현한 뒤
리셋 1회로 관통 검증한다.**

## 완료 기준

- [ ] compile 클린 · Unity 콘솔 error 0
- [ ] Play 둘째 판 전투 시작: 0단계가 **두 문 문구 2줄**로 뜨고 포커스 링은 항아리에 있다
- [ ] Play: 0단계 상태에서 **항아리로** 열면 `…끌어보세요!` + usable 슬롯 포커스
- [ ] Play: 0단계 상태에서 **유닛 탭으로** 열면 `카드를 탭하면…` 2줄 + usable 슬롯 포커스
- [ ] Play: 그 문구대로 카드를 탭하면 실제로 즉발 부착된다(안내와 동작 일치)
- [ ] Play: 어느 경로로 열든 `awakeningHintVersion` 이 `1` 로 저장돼 다음 판엔 3단계가 안 뜬다
- [ ] Play: 카드 press 시 배너가 조기 해제된다(`OnCardPeeked` 회귀 0)
- [ ] Play 첫 판: 0단계·A·B 어느 것도 뜨지 않는다(unit 10 봉인 + unit 15 회귀 0)
- [ ] Play 셋째 판: 세 단계 모두 뜨지 않는다(저장이 실제로 먹혔다 — unit 12 완료 기준 계승)
- [ ] EditMode 전체 + PlayMode 튜토리얼 스모크 회귀 0

> **자동 테스트 한계**: 분기 조건(`InSelectionMode`)은 실제 선택·손패 오픈이 있어야 참이 되므로
> 기존 `FirstSessionTutorialSmokeTest` 로는 못 덮는다(그 스모크는 `HandOpened` 를 태우지 않는다).
> 문구 상수를 EditMode 로 고정하는 것은 값 복제일 뿐 분기를 검증하지 못한다 — **테스트를 만들지
> 않고 Play 체크리스트에 의존한다는 것을 명시**한다. handoff 13 의 커버리지 gap 과 같은 성격이다.
