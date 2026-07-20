# 3 — 챕터 B 로드아웃 (첫 판 복귀 → 스쿼드/드림캐쳐 클릭)

## 목적

첫 판을 마치고 로비로 돌아온 순간, 편성을 바꿀 수 있다는 사실을 알리고 **플레이어가 실제로 패널을
열어보게** 한다. 아웃게임 튜토리얼은 여기서 끝난다.

## 변경 대상

- `Assets/_Project/Scripts/UI/Outgame/Tutorial/OutgameTutorialController.cs` (unit 2 에서 이어 작성)

## 구현

`SerializeField` 로 `RectTransform squadButton`, `RectTransform dreamcatcherButton` 을 추가로 받는다.

### 진입

unit 2 의 `OnLobbyShown` 에서 챕터 A 판정이 false 일 때 이어서 검사한다.

```csharp
if (TutorialProgress.ShouldRunLobbyLoadoutHint(profileSO)) → 챕터 B 예약
```

전투 복귀는 OutgameScene 을 `LoadSceneMode.Single` 로 재로드하므로 `Awake` 경로가 다시 탄다.

> **복귀 경로에서는 `Awake` 훅이 유일한 진입점이다.** `UserSession` 은 static 이라 이미 signed-in 이고,
> `LoginPanelView.Start()` 는 `if (UserSession.IsSignedIn) return`(`LoginPanelView.cs:52`)으로 즉시
> 반환해 `onSignedIn` 이 재발화하지 않는다. `PlayerProfileSO.IsLoadedThisSession` 도 세션 토큰이
> `SubsystemRegistration` 에서만 증가하므로(`PlayerProfileSO.cs:19-32`) 씬 재로드로는 무효화되지 않는다.
> 따라서 unit 4 의 훅이 프로필 로드 **이후**에 있어야 한다는 요구가 여기서 load-bearing 하다.

### 게이트 의미 주의

`ShouldRunLobbyLoadoutHint` 가 요구하는 core 완료의 실제 의미는 **"인게임 core 튜토리얼이 발동하고
Battle 페이즈에 도달했다"**이다. `FirstSessionTutorialController` 는 `_coreActive` 일 때만 완료를
저장하고(`cs:263-273`), `_coreActive` 는 참조 누락이나 affordable 슬롯 부재에서 fail-open 으로
켜지지 않는다(`cs:102-121`). 그 경로를 탄 플레이어는 전투를 몇 판 하든 **챕터 B 를 영원히 보지 못한다.**

데모 범위에서는 이 재사용을 유지한다(사용자 결정 2026-07-21). 독립 신호로의 교체는 README 후속 후보.
이 실패 모드 때문에 문구는 **점수·결과 화면을 전제하지 않는다** — 중도 이탈한 플레이어도 자연스럽게 읽힌다.

### 진행

| 단계 | 화면 | 진행 조건 |
|---|---|---|
| `LoadoutMessage` | `overlay.Show()` + `SetHoles(null)` + `ShowMessage("더 잘 막고 싶다면, 함께 싸울 유닛과 카드를 손봐보세요.", showSkip: false)` | `overlay.Tapped` → `LoadoutFocus` |
| `LoadoutFocus` | `SetHoles([squadButton, dreamcatcherButton])` + 합집합 링 + `ShowMessage("스쿼드와 드림캐쳐에서 바꿀 수 있어요!", showSkip: false)` | **두 버튼 중 하나의 `onClick`** 또는 dim 탭 → 종료 |

챕터 A 와 달리 `LoadoutFocus` 에서 **dim 탭도 종료시킨다** — "아무거나 클릭하면 끝"이 요청 사양이다.
버튼을 누른 경우엔 인스펙터 배선된 `OnOpenSquad`/`OnOpenDreamcatcher` 가 같은 클릭으로 실행되어
패널이 실제로 열리고, 우리는 완료 저장 후 오버레이만 걷는다.

두 버튼은 **왼쪽 세로 1열**에 24px 간격으로 쌓여 있다(`SquadButton` y −300, `DreamcatcherButton` y −552,
둘 다 x 48, 180×228). unit 1 의 스캔라인 차집합이 그 간격을 폭 전체 dim 조각으로 남기므로 두 버튼이
분리되어 보이고, 위아래의 `PresetButton`(y −48)·`HistoryButton`(y −804)은 어둡게 남아 대비가 생긴다.

### 포커스 링

`TutorialGuidanceView.FocusUi` 는 대상 1개만 받는다. 두 버튼을 감싸는 **합집합 사각형**을 담은 그래픽
없는 임시 `RectTransform` 을 오버레이 `FullBleedRoot` 아래에 만들어 링 대상으로 넘긴다
(`FocusUi` 는 world corners 만 읽으므로 캔버스가 달라도 무방).

**수명**: 컨트롤러가 소유한다. 필드로 1개만 보관해 재사용하고, 챕터 종료·`AbortChapter`·`OnDestroy`
세 경로 모두에서 `guidance.ClearFocus()` 후 `Destroy` + null 대입한다. `TutorialGuidanceView` 는
대상을 소유하지 않으며 `ClearFocus()` 는 참조만 끊는다(`cs:102-107`).

둘 중 하나만 유효하면 그 하나만 뚫고 링도 그 하나에 건다. 둘 다 null 이면 구멍 없이 표시하고
dim 탭으로 종료한다(fail-open).

### 알려진 한계 — 링/홀 정렬

`FocusUi` 는 대상을 `_safeRoot` 로컬로 변환한 뒤 safe rect 안쪽 20px 로 클램프한다
(`TutorialGuidanceView.cs:272-289`, `SafeEdgePadding = 20f`). dim 홀은 `FullBleedRoot` 기준이라
클램프가 없다. 노치 기기 가로 모드에서 x=48 인 왼쪽 열 버튼은 safe inset 에 걸려 **링이 안쪽으로
밀려 홀과 어긋날 수 있다.** unit 4 의 실기기 QA 에서 정렬을 별도 항목으로 확인한다.

### 종료

```csharp
if (!TutorialProgress.CompleteLobbyLoadoutHint(profileSO.profile)) { EndChapter(); return; }
TrySaveProfile();
EndChapter();
```

`profileSO.profile` 은 이 시점에 다시 읽는다. 캐시한 인스턴스에 쓰면, 플레이어가 안내대로 스쿼드나
덱을 편집해 저장하는 순간 라이브 인스턴스가 `lobbyLoadoutHintVersion = 0` 으로 디스크를 덮어
챕터 B 가 부활한다 — 안내 문구가 정확히 그 행동을 유도하므로 발현 확률이 사실상 1이다.

## 완료 기준

- [ ] 컴파일 통과
- [ ] `RESET TUTORIAL` → 챕터 A 통과 → 전투 → 로비 복귀 시 챕터 B 가 뜬다
- [ ] 첫 탭에 문구가 바뀌고 SquadButton·DreamcatcherButton **둘만** 밝게 뚫린다
- [ ] 두 버튼 **사이의 24px 간격이 어둡게 남아** 두 버튼이 분리되어 보인다
- [ ] PresetButton·HistoryButton·StartButton 은 계속 어둡게 덮여 있다
- [ ] SquadButton 탭 → **스쿼드 패널이 실제로 열리고** 오버레이는 사라진다
- [ ] dim 영역 탭 → 패널 없이 오버레이만 사라지고 로비 조작이 정상 복귀한다
- [ ] **챕터 B 종료 직후 스쿼드를 편집·저장하고 로비 재진입 → 챕터 B 가 재출현하지 않는다**
- [ ] `profile.json` 의 `lobbyLoadoutHintVersion` 이 `1`
