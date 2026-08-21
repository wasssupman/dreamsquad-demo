# unit 0 — 인게임 스텝 + 소비처 훅 제거

## 목적

첫 판 튜토리얼 시퀀스를 통째로 걷고, 그것 때문에 다른 뷰에 박혀 있던 훅을 뽑는다. 배치·기믹·각성 뷰가 튜토리얼을 **모르는** 상태로 되돌아간다.

## 변경 대상

**삭제**
- `Assets/_Project/Scripts/UI/Tutorial/FirstSessionTutorialController.cs` + `.Awakening` + `.BattleHud` + `.EffectTile` + `.GimmickReveal` (5파일, 1,329줄)
- `Assets/_Project/Tests/PlayMode/FirstSessionTutorialSmokeTest.cs` · `Assets/_Project/Tests/EditMode/EffectTileAnchorTests.cs` · `Assets/_Project/Tests/EditMode/AwakeningSealRelayTests.cs`(파일 전체가 봉인 릴레이 하나만 검증한다)
- `BattleScene.unity` 의 `FirstSessionTutorialController` 오브젝트/컴포넌트

**훅 제거**
- `UI/PlacementPhaseView.cs` — `BeginTutorialGate` / `UnlockTutorialStart` / `EndTutorialGate` · `_tutorialHold` / `_tutorialStartUnlocked` · `profileSO` 필드(+ 씬 배선)
- `UI/GimmickPhaseView.cs` — `TutorialHoldEntered` / `TutorialHoldReleased` · `_tutorialMode` / `_holding` / `_holdStartedAt` · `ShouldRunCore` 스킵 분기
- `UI/GimmickPhaseView.cs` — `profileSO` 필드(+ 씬 배선). 위 분기를 지우면 유일한 사용처 두 곳이 같이 사라져 고아가 된다.
- **각성 봉인 체인 4단** — 쓰는 쪽이 튜토리얼 하나뿐이라 통째로 죽는다. 절반만 지우면 «영원히 false 인 릴레이»가 남는다:
  `AwakeningGaugeView.SetSuppressed` / `IsSuppressed` / `_suppressed` → `DreamcatcherHandView.AwakeningSealedThisMatch` → `DcInspectController.SealedThisMatch()` 와 그 호출 2곳(`Update` 가드 · `OnBoardTapped` 가드)
- `Tests/EditMode/TutorialDragGuidanceTests.cs` — 게이트 케이스만(레이아웃 케이스는 유지)

## 구현

**`PlacementPhasePolicy` 축소.** 인자 3개 중 둘이 사라진다:

```csharp
// before: CanFinish(tutorialHold, tutorialStartUnlocked, placementInteractionBlocked)
// after:
public static bool CanFinish(bool placementInteractionBlocked) => !placementInteractionBlocked;
```

호출부 3곳(`CanFinishPlacement` · `RefreshStartAvailability` · `TickAutoStart`)이 따라 줄어든다. **`TickAutoStart` 의 게이트는 `CanFinishPlacement()` 호출 형태를 유지한다** — 술어가 단순해질 뿐, 「종료를 거절할 상태면 시간도 흘리지 않는다」는 불변식은 그대로다(느슨하게 바꾸면 `_shownTick` 자물쇠가 자가치유를 막아 판이 벽돌이 된다 — `match-intro-phase-toggles` handoff 참조).

**계약 6 개정(README 계약 3).** `UseAutoStart(placementPhaseEnabled, tutorialCore)` → `tutorialCore` 인자 제거:

```csharp
public static bool UseAutoStart(bool placementPhaseEnabled) => !placementPhaseEnabled;
```

`match-intro-phase-toggles/README.md` 계약 6 과 `0_*.md` 를 **같은 커밋에서** 개정한다(그 spec 은 완료 상태이므로 «tutorial-content-teardown unit 0 이 개정» 이라고 출처를 남긴다).

**각성 봉인 제거 순서**: `DcInspectController` 의 두 가드를 먼저 걷고(`SealedThisMatch()` 는 `false` 폴백이 이미 정상 동작 경로라 제거해도 의미가 안 바뀐다) → 릴레이 프로퍼티 → 게이지의 `SetSuppressed`/`IsSuppressed`/`_suppressed` 순으로 뽑는다.

**`GimmickPhaseView`**: 리빌은 이제 **항상** 재생된다(첫 판 스킵 없음). `Play()` 직전의 `TutorialProgress.ShouldRunCore` 분기를 지우면 `gimmick == null || config == null` 만 스킵 조건으로 남는다.

**`AwakeningGaugeView`**: `SetSuppressed` 를 지우면 표시 판정이 페이즈 하나로 단순해진다. 봉인 상태로 굳지 않는지 확인할 것.

## 완료 기준

- 컴파일 통과. EditMode 두 lane 그린(삭제한 테스트 제외).
- Play(첫 판 프로필): 안내 말풍선·딤·포커스가 **한 번도** 뜨지 않는다. 배치 30초가 홀드 없이 흐르고 START 가 정상 노출.
- Play(`placementPhaseEnabled=false`): **첫 판에서도** 3초 자동 시작(계약 3 = 플래그가 곧 진실).
- Play: 기믹 리빌이 첫 판에도 재생된다.
- Play: 각성 버튼이 첫 판에 봉인되지 않는다. 드림캐쳐 **선택(인스펙트)** 도 첫 판에 정상 동작한다(봉인 가드 제거 회귀 확인 — `DcInspectController`).
- 콘솔 에러 0. 씬에 미싱 스크립트 참조 없음.
