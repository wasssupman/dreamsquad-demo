# 10 — 첫 판 각성 봉인

## 목적

첫 판에서 각성 버튼을 숨겨 플레이어가 **배치만으로** 승부를 보게 한다. 자리는 빈 채로 남기고,
게이지 충전·덱 회수 같은 로직은 손대지 않는다.

## 변경 대상

- `Assets/_Project/Scripts/UI/Dreamcatcher/AwakeningGaugeView.cs`
- `Assets/_Project/Scripts/UI/Tutorial/FirstSessionTutorialController.cs`

## 구현

### `AwakeningGaugeView.SetSuppressed(bool)` seam

버튼 표시의 소유자는 `AwakeningGaugeView.OnPhaseChanged` 다(`Battle` 이면 `_panel.SetActive(true)`,
그 외엔 false). **튜토리얼이 `SetActive` 를 직접 호출하면 다음 페이즈 전이에서 되돌아온다.**
`GimmickGuideView.SetTutorialSuppressed(bool)` 과 같은 형태로 명시 seam 을 만든다.

```csharp
private bool _suppressed;

public void SetSuppressed(bool suppressed)
{
    if (_suppressed == suppressed) return;
    _suppressed = suppressed;
    ApplyPanelVisibility();   // OnPhaseChanged 와 공유하는 단일 적용 지점
}
```

`OnPhaseChanged` 는 `_panel.SetActive` 를 직접 부르지 말고 `_phase` 필드에 대입한 뒤
`ApplyPanelVisibility()` 를 거치게 한다. 그 안에서 `activate = !_suppressed && _phase == Battle`.
ambient/maxIdle 정지 규칙은 유지한다.

**`_phase` 필드를 신설해야 한다** — 이 뷰는 페이즈를 보관하지 않고 `OnPhaseChanged` 인자만 쓰고 버린다.
`SetSuppressed` 는 인자 없이 호출되므로 읽을 곳이 필요하다. `GameManager.Instance.CurrentPhase`
직독은 금지 — 같은 이벤트 안에서 구독자 순서에 따라 값이 갈린다
(`GimmickGuideView._phase`/`RefreshVisibility` 와 동형).

**게이지·덱 로직은 건드리지 않는다.** `DreamcatcherHandController` 는 ECS 이벤트 기반 순수 로직이라
뷰를 참조하지 않는다. `Pulse()`/`Refresh` 는 이미 `_panel.activeInHierarchy` 를 검사해 연출만 스킵한다.

> 버튼을 숨기면 손패를 여는 유일한 경로(`gaugeView.Toggled`)가 막혀 **카드 사용이 봉인된다.**
> 이건 부작용이 아니라 이 unit 의 목적이다.

### 봉인 판정 — `_awakeningLockedThisMatch`

`FirstSessionTutorialController` 가 소유한다. **Placement 진입 시점**에 결정한다.

- `OnPlacementReady` 초입(기존 early return 들보다 **앞**)에서
  `_awakeningLockedThisMatch = TutorialProgress.ShouldRunCore(profileSO)` 로 판정하고
  `gaugeView?.SetSuppressed(_awakeningLockedThisMatch)`.
- `ShouldRunCore` 를 쓰는 이유: `IsCorePending` 은 Placement 동안 아직 true 다. 참조 누락이나
  affordable 슬롯 부재로 **core 튜토리얼이 fail-open 으로 발동하지 못한 경우에도** 첫 판을 올바르게
  잡는다(`_coreActive` 로 판정하면 그 경로에서 버튼이 보인다).
- 해제: **`OnDisable` 에서만** `_awakeningLockedThisMatch = false` + `SetSuppressed(false)`.
  다음 매치는 `OnPlacementReady` 가 lock 을 매번 다시 판정해 덮으므로 그것으로 충분하다.
  페이즈 이탈에서 `SetSuppressed(false)` 를 부르면, 튜토리얼 핸들러가 gaugeView 보다 먼저 도는 경우
  캐시된 `_phase`(아직 Battle)로 패널을 한 번 켰다 끄는 왕복(`StartAmbient`→`StopAmbient`)이 생긴다.
  **`EndCore` 에서도 풀지 않는다** — Skip 으로 건너뛰어도 첫 판인 것은 변함없다.

### 각성 힌트도 함께 억제

`EvaluateAwakeningHint()` 초입에 `if (_awakeningLockedThisMatch) return;` 를 넣는다.

이게 없으면 첫 판 Battle 에서 힌트가 **없는 버튼을 가리킨다.** `OnPhaseChanged(Battle)` 는
`CompleteCoreProgress()` → `EndCore()` 로 `_coreActive` 를 내린 **직후** `EvaluateAwakeningHint()` 를
부르므로 기존 `_coreActive` 가드가 통과해 버리고, `_awakeningOfferedThisBattle = true` 가
`gaugeView.Pulse()` 보다 먼저 세팅되어 힌트가 조용히 "소모"된다.

> **`TutorialProgress.ShouldRunAwakeningHint` 에 `!IsCorePending` 을 추가하는 방식은 동작하지 않는다.**
> 같은 핸들러에서 `CompleteCore` 가 먼저 실행돼 그 시점엔 이미 pending 이 false 다.

### fail-open 계정의 영구 봉인 차단 (CRITICAL)

`CompleteCoreProgress()` 는 지금 `_coreActive == true` 뒤에만 도달 가능하다(`:258` Skip 가드,
`:269` Battle 가드). 참조 누락·affordable 슬롯 부재로 코어 안내가 fail-open 된 계정은
`firstBattleTutorialVersion` 이 **영원히 0** 이라 `ShouldRunCore` 가 항상 참이 되고,
이 unit 의 lock 이 **매 판 각성 버튼을 영구히 숨긴다.** 그 계정의 드림캐쳐가 통째로 사라진다.

→ `OnPhaseChanged(Battle)` 에서 **`_coreActive` 와 무관하게** `CompleteCoreProgress()` 를 호출한다.
안내가 발동하지 못했어도 "첫 판"은 소비된 것으로 본다. `EndCore` 만 `_coreActive` 조건을 유지한다.

같은 결함이 선물 튜토리얼(`ShouldRunGiftTutorial`)과 로비 챕터 B(`ShouldRunLobbyLoadoutHint`)에도
있었으므로 이 수정이 셋을 함께 고친다.

## 완료 기준

- [ ] 컴파일 통과
- [ ] **OutgameScene 에서 시작해** `RESET TUTORIAL` → 첫 판 Placement/Battle 내내 각성 버튼이 보이지 않고 **우하단이 빈 자리**로 남는다
- [ ] 첫 판에서 다른 HUD(NextWaveDock 등)가 1픽셀도 움직이지 않는다
- [ ] 첫 판에서 각성 힌트 문구·포커스 링이 전혀 뜨지 않는다
- [ ] 첫 판에서 적을 처치해도 게이지는 정상 누적된다(로그 또는 reflection 으로 `handController.Gauge` 확인)
- [ ] 첫 판 튜토리얼을 **Skip** 해도 그 판 내내 버튼이 계속 숨겨져 있다
- [ ] 두 번째 판 Battle 에서 버튼이 정상 노출된다
- [ ] **fail-open 회귀**: 코어 안내가 발동하지 못한 상태로 첫 전투에 진입해도
      `firstBattleTutorialVersion` 이 `1` 로 저장되고, 두 번째 판에서 버튼이 정상 노출된다
- [ ] BattleScene 직접 Play 는 `IsLoadedThisSession == false` 라 lock 이 안 걸린다(정상) —
      이 경로로 검수하면 안 된다
- [ ] PlayMode `FirstSessionTutorialSmokeTest` 회귀 없음
