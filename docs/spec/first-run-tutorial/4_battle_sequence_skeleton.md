# 4 — 배틀 시퀀스 골격

## 목적

B1~B4 가 올라탈 바닥을 만든다: **정지/재개 · 딤+구멍 · 카운트다운 붙잡기 · 스텝 러너**.
이 unit 자체는 안내 문구를 하나도 띄우지 않는다.

## 변경 대상

- `Assets/_Project/Scripts/UI/Tutorial/FirstRunTutorialController.cs` (신규)
- `Assets/_Project/Scripts/UI/PlacementPhaseView.cs` (카운트다운 홀드 훅)
- `Assets/_Project/Scenes/BattleScene.unity` (`TutorialGuidance` 오브젝트에 배선)

## 구현

**정지.** `TimeManager.Request(TimeDomain.Battle, 0f)` 로 리스를 잡고, 풀 때 반납한다.
`Time.timeScale` 금지(계약 6). `Interaction` 도메인은 건드리지 않는다 — UI·드래그·
안내 연출은 계속 흘러야 한다.

**딤 + 구멍.** 로비와 같은 `OutgameTutorialOverlay` 를 배틀에서도 쓴다(이름만 Outgame
이고 순수 UI 도구다). 구멍 대상은 스텝이 준다: 트레이 셀 `RectTransform`, 손패 카드,
혹은 보드 위 좌표를 감싸는 임시 `RectTransform`.

**⚠ 강제는 정지 중에만.** 시간이 흐르는 동안 입력을 막으면 그 사이 새는 적이
플레이어 책임이 된다. 재개 구간(`onPlaceWatchSeconds` · `resumeBeforeAttachSeconds`)
에서는 딤을 내린다.

**카운트다운 홀드.** `PlacementPhaseView` 는 지금 `OnEnable` 에서 바로 카운트다운을
시작한다. 3초 + 입력 차단이라 그 안에 맵 설명이 들어갈 수 없다. `tutorial-content-teardown`
이 걷어낸 홀드 훅과 **같은 성격의 것이 다시 필요하다**:

- `BeginIntroHold()` — 카운트다운 시작을 보류한다(`_remaining` 을 흘리지 않는다).
- `ReleaseIntroHold()` — 보류를 풀고 3 · 2 · 1 · GO! 를 정상 진행한다.

옛 구현에서 배운 것 하나: **홀드 술어와 종료 가드는 같은 것을 봐야 한다**
(`a1392b4d` — 카운트다운 종료 게이트가 종료 가드와 다른 술어를 봐서 판이 벽돌이
될 수 있었다). 홀드 중에는 `_shownTick` 을 건드리지 않는다.

**스텝 러너.** 스텝 = `(진입 연출, 완료 조건, 타임아웃)`. 완료 조건은 기존 이벤트
구독이다 — `DefenderDragPlacementController.Armed`/`PlacementCommitted`,
`DreamcatcherHandView.SelectionTargetSet`, `DreamcatcherHandController.AttachmentsChanged`.
타임아웃(`stepTimeoutSeconds`)이 만료되면 그 스텝을 흘려보내고 다음으로 간다(계약 10).
러너는 `Interaction` 시계(unscaled)로 센다 — 자기가 `Battle` 을 멈춰놓고 그 시계를
기다리면 영영 안 온다.

**정리.** 씬 종료·판 종료·시퀀스 완료 어느 쪽으로 끝나도 리스를 반납하고 딤을
내린다. 리스가 남으면 판이 멈춘 채로 남는다.

## 완료 기준

- compile 통과.
- 튜토리얼 판에서 카운트다운이 홀드에 붙잡혀 시작하지 않고, 풀면 3 · 2 · 1 · GO! 가 정상 진행.
- 일반 판(튜토리얼 완료 계정)은 홀드 없이 기존과 동일하게 즉시 카운트다운.
- 정지 중 적·유닛이 멈추고 UI 애니메이션은 계속 돈다.
- 시퀀스를 강제 종료(씬 이탈)해도 다음 판이 정상 속도로 시작한다.
