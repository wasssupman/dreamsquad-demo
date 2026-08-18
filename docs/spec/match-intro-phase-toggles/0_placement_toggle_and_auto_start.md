# unit 0 — 배치 페이즈 토글 + 3초 자동 시작

## 목적

`BattleConfig` 플래그 하나로 배치 페이즈를 끈다. 끄면 3초 카운트다운(입력 불가) 후 전투가 자동으로 시작된다. 켜져 있으면 현행과 완전히 동일하다.

## 변경 대상

- `Assets/_Project/Scripts/Data/BattleConfig.cs` — 필드 2개 추가
- `Assets/_Project/Scripts/Core/GameManager.cs` — `BattleConfig` 읽기 전용 프로퍼티
- `Assets/_Project/Scripts/UI/PlacementPhaseView.cs` — 자동 시작 분기 · 입력 차단막 · `PlacementPhasePolicy.UseAutoStart`
- `Assets/_Project/Data/Config/BattleConfig.asset` — `gimmickEnabled: 0`(사용자 결정), `placementPhaseEnabled: 1`(현행 유지)
- `Assets/_Project/Tests/EditMode/TutorialDragGuidanceTests.cs` — `UseAutoStart` 케이스

## 구현

**BattleConfig** — 기믹 토글 바로 아래.

```csharp
[Tooltip("배치 페이즈 on/off. false = 3초 카운트다운(입력 불가) 후 자동 전투 시작.")]
public bool placementPhaseEnabled = true;

[Tooltip("placementPhaseEnabled=false 일 때 자동 시작까지의 카운트다운(초).")]
public float autoStartCountdownSeconds = 3f;
```

**GameManager** — `CostConfig` 프로퍼티 옆에 `public BattleConfig BattleConfig => battleConfig;`.

**PlacementPhaseView** — `BeginPlacementPhase()` 안에서 갈라진다. 계약 3 대로 **진입 묶음은 건드리지 않는다**:

```csharp
bool tutorialCore = TutorialProgress.ShouldRunCore(profileSO);
_autoStart = PlacementPhasePolicy.UseAutoStart(cfg != null && cfg.placementPhaseEnabled, tutorialCore);
float duration = _autoStart ? (bcfg != null ? bcfg.autoStartCountdownSeconds : 3f)
                            : (costCfg != null ? costCfg.placementPhaseDuration : 30f);
```

`_autoStart` 일 때:

- START 버튼 래퍼를 켜지 않는다 — `RefreshStartAvailability` 가 조기 반환한다(`_autoStart` 면 항상 unavailable).
- **전면 raycast 블로커**를 켠다. 패널 최상위에 `Image`(alpha 0, `raycastTarget=true`, anchor stretch). 트레이·손패 드래그가 시작조차 못 하고, 클릭 배치가 되살아나도 `IsPointerOverGameObject()` 가 이 블로커에 걸린다.
- `Update()` 의 `IsPlacementInteractionBlocked` 분기는 `_autoStart` 에서 도달 불가다(입력이 없다). 다만 **튜토리얼 홀드는 도달 가능하다** — `TickAutoStart` 는 매 프레임 `PlacementPhasePolicy.CanFinish(_tutorialHold, _tutorialStartUnlocked, false)` 로 게이트하고 거짓이면 시간을 흘리지 않는다. 계약 6이 배제하는 것은 **첫 판**뿐이고, 효과 타일 안내(`FirstSessionTutorialController.EffectTile`)는 두 번째 판 이후라 `ShouldRunCore=false` 에서 게이트를 건다.
- `FinishPlacement()` 는 그대로 호출한다(계약 4). 블로커는 여기서 즉시 끈다 — 전투가 시작된 뒤에도 입력이 막혀 있으면 안 된다.

**PlacementPhasePolicy** — 같은 파일 하단, `CanFinish` 옆.

```csharp
public static bool UseAutoStart(bool placementPhaseEnabled, bool tutorialCore)
    => !placementPhaseEnabled && !tutorialCore;
```

`profileSO` 는 `PlacementPhaseView` 에 아직 없다 — `SerializeField` 추가 후 씬 배선(`GimmickPhaseView` 와 같은 참조). **미배선이면 `ShouldRunCore` 가 false 를 반환해 fail-open(자동 시작 정상 동작)** 이므로 이 프로젝트의 참조 누락 계약과 어긋나지 않는다.

## 완료 기준

- 컴파일 통과. EditMode 코어 어셈블리 그린(`UseAutoStart` 4조합 포함).
- `placementPhaseEnabled=true`(기본): Play 진입 → 30초 카운트다운 · START 버튼 · 트레이 드래그 배치 전부 현행 그대로.
- `placementPhaseEnabled=false`: Play 진입 → 3초 후 자동 전투 시작. 그 3초 동안 **트레이 드래그·손패 탭이 먹지 않는다**. 전투 시작 후에는 정상적으로 배치된다(블로커 해제 확인).
- 자동 시작으로 들어간 판에서 트레이 슬롯이 채워져 있고 코스트가 리젠된다(계약 3 회귀 확인).
- **두 번째 판**(효과 타일 안내가 뜨는 판)에서 플래그를 꺼도 안내가 3초에 잘리지 않는다 — 카운트다운이 멈췄다가 탭 이후 재개된다(계약 6 ②).
- 콘솔에 `[GameManager] gimmick=none` (에셋 `gimmickEnabled: 0` 확정) + 리빌 오버레이 미노출.
