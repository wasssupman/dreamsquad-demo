# 3 — 배틀 시퀀스 골격

## 목적

B1~B4 가 올라탈 바닥을 만든다: **정지/재개 · 딤+구멍 · 카운트다운 붙잡기 · 스텝 러너**.
이 unit 자체는 안내 문구를 하나도 띄우지 않는다.

## 변경 대상

- `Assets/_Project/Scripts/UI/Tutorial/FirstRunTutorialController.cs` (신규)
- `Assets/_Project/Scripts/UI/PlacementPhaseView.cs` (카운트다운 홀드 훅)
- `Assets/_Project/Scenes/BattleScene.unity` (신규 오브젝트 + 배선)

## 구현

### 정지

`TimeManager.Request(TimeDomain.Battle, 0f, priority: 100)`.

**우선순위 100 이 필수다** — `DcInspectController`(유닛 선택 슬로모)와
`DreamcatcherHandView` 가 같은 도메인을 `priority: 50` 으로 요청한다. 승자 규칙은
우선순위 내림차순이므로 기본 0 으로 잡으면 **B4 에서 유닛을 고르는 순간 판이 0.3배로
다시 흐른다.** 선례는 `MenuPopup` 의 일시정지(100).

`Interaction` 도메인은 건드리지 않는다 — UI·드래그·안내 연출은 계속 흘러야 한다.
(프로젝트 전체에서 `Interaction` 에 스케일을 요청하는 코드는 없다 → 러너의
`DeltaTime(Interaction)` 은 항상 unscaled 이고 타임아웃은 정지 중에도 정상적으로 흐른다.)

**lease 는 구간이 소유한다**(계약 7). 성공·타임아웃·스킵·취소가 전부 같은 해제 지점을
지나고, `OnDisable`/`OnDestroy` 에서도 반납한다. 해제를 성공 경로에만 걸면 스텝 하나를
흘려보낸 순간 판이 0배속으로 남는다 — `EndMatch` 에는 `TimeManager.ResetAll()` 이 없어
이 판 안에서 스스로 풀어야 한다.

### 딤 + 구멍

로비와 같은 `OutgameTutorialOverlay` 를 배틀에서도 쓴다 — Outgame 전용 전제(로비 캔버스/
컨트롤러 참조)가 없고 asmdef 도 하나뿐이라 재사용 가능하다.

⚠ **BattleScene 에는 아직 이 컴포넌트가 없다.** `TutorialGuidance` 오브젝트에 **같이
붙이면 안 된다** — 두 뷰가 각자 `UiCanvasSetup.Ensure` 를 불러 한 sortingOrder 를 다툰다.
**형제 GameObject 를 새로 만든다**(OutgameScene 의 `Dim`/`Guidance` 구조와 동형).

**딤은 전 구간 떠 있다**(계약 5). 정지 여부와 딤은 **별개 축**이다 — 재개 구간
(`onPlaceWatchSeconds` · `resumeBeforeAttachSeconds`)에서도 딤을 유지해 플레이어가
캐논이나 각성 카드를 먼저 소비하지 못하게 한다. 그 소비가 곧 B3·B4 의 스킵 조건이다.

### 카운트다운 홀드

`PlacementPhaseView` 는 지금 `OnEnable` 에서 바로 카운트다운을 시작한다. 3초 + 입력
차단이라 그 안에 맵 설명이 들어갈 수 없다.

- `BeginIntroHold()` / `ReleaseIntroHold()` 를 더한다.
- ⚠ **홀드 술어는 `CanFinishPlacement()` 안에 산다.** `TickAutoStart` 는 그 술어에서
  early-return 하며 `_remaining`·`_shownTick` 을 건드리지 않으므로 재진입 자물쇠와
  충돌하지 않는다. `TickAutoStart` 쪽에만 조건을 넣으면 `a1392b4d` 가 고친 함정
  (종료 게이트와 종료 가드가 서로 다른 술어를 봐서 판이 벽돌이 되는 것)이 그대로 돌아온다.
- ⚠ **홀드 상한을 둔다.** 컨트롤러가 죽거나 예외가 나서 `ReleaseIntroHold` 가 안 불리면
  전면 입력 차단막이 올라간 채 카운트다운이 3에서 영원히 멈춘다. 만료 시 자가 해제.

### 스텝 러너

스텝 = `(진입 연출, 완료 조건, 타임아웃)`. 완료 조건은 기존 이벤트 구독이다.
타임아웃(`stepTimeoutSeconds`)이 만료되면 그 스텝을 흘려보내고 다음으로 간다 —
**흘려보낼 때도 딤/구멍/정지 정리는 같은 해제 지점을 지난다.**

**배선(SerializeField)**: `PlayerProfileSO` · `FirstRunTutorialConfig` ·
`TutorialGuidanceView` · `OutgameTutorialOverlay` · `BattleBridge` · `PlacementPhaseView` ·
`DefenderSelector` · `DreamcatcherHandView` · `DreamcatcherHandController` ·
`DcInspectController` · 보드 카메라. 씬 작업이 이 unit 의 절반이다.

## 완료 기준

- compile 통과.
- 튜토리얼 판에서 카운트다운이 홀드에 붙잡혀 시작하지 않고, 풀면 3 · 2 · 1 · GO! 가 정상 진행.
- 홀드 상한이 만료되면 스스로 풀려 판이 시작된다.
- 일반 판(튜토리얼 완료 계정)은 홀드 없이 기존과 동일하게 즉시 카운트다운.
- **정지 중 적이 한 픽셀도 움직이지 않는다 — 유닛을 선택한 상태에서도.**
- 스텝을 타임아웃으로 흘려보내도 딤이 걷히고 판이 정상 속도로 돌아간다.
- 시퀀스 도중 씬을 나가도 다음 판이 정상 속도로 시작한다.
