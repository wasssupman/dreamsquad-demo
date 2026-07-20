# 2 — 챕터 A 인트로 (로비 최초 노출 → START 클릭)

## 목적

로그인 직후 로비가 처음 보이는 순간 안내를 띄우고, 플레이어가 **실제 START 버튼을 눌러** 전투에
진입하게 한다.

## 변경 대상

- `Assets/_Project/Scripts/UI/Outgame/Tutorial/OutgameTutorialController.cs` (신규)

## 구현

`OutgameTutorialOverlay`, `TutorialGuidanceView`, `PlayerProfileSO`, `RectTransform startButton` 을
`SerializeField` 로 받는다. 포커스 대상은 이름 탐색이 아니라 배선한다. 챕터 B(unit 3)와 한 상태머신을 공유한다.

```csharp
private enum Step { None, IntroMessage, IntroFocus, LoadoutMessage, LoadoutFocus }
```

### 진입 — `OnLobbyShown(bool signedIn)`

unit 4 가 `OutgameMenuController.Awake` **말미**(프로필 로드 이후)와 `onSignedIn` 콜백 양쪽에서 호출한다.
멱등이어야 한다.

- `signedIn == false` → `AbortChapter()` 후 반환.
- `_step != Step.None` → 이미 진행 중. 무시.
- `TutorialProgress.ShouldRunLobbyIntro(profileSO)` → 챕터 A 예약. 아니면 unit 3 의 챕터 B 판정.

**Awake 프레임에는 뷰를 만지지 않는다.** `OnLobbyShown` 이 `Start()` 이전에 들어오면 요청만 래치하고,
실제 `Show`/`ShowMessage` 는 컨트롤러 자신의 `Start()`(모든 `Awake` 완료 후)에서 1회 수행한다.
`Start()` 이후에 들어온 호출(= `onSignedIn` 경로)은 즉시 실행한다.

> `[DefaultExecutionOrder]` 로 순서를 맞추는 방식은 금지다. `TutorialGuidanceView.Awake` 의
> `Hide()`(`TutorialGuidanceView.cs:77-81`)는 실행 순서와 무관하게 무조건 돌기 때문에, 컨트롤러가
> 먼저 `ShowMessage` 를 호출하면 방금 띄운 문구가 꺼진다.

### 진행

| 단계 | 화면 | 진행 조건 |
|---|---|---|
| `IntroMessage` | `overlay.Show()` + `SetHoles(null)` + `ShowMessage("악몽이 몰려옵니다. 꿈결특공대, 출동!", showSkip: false)` | `overlay.Tapped` → `IntroFocus` |
| `IntroFocus` | `SetHoles([startButton])` + `FocusUi(startButton)` + `ShowMessage("이 버튼을 눌러 출발!", showSkip: false)` | **실제 START `onClick`** → 종료 |

`IntroFocus` 에서 dim 탭(`Tapped`)은 무시한다 — START 를 눌러야 진행한다.

### 완료 저장 — 버튼 onClick 임시 구독

`IntroFocus` 진입 시 `startButton.GetComponent<Button>().onClick.AddListener(OnFocusedClicked)`,
이탈 시 `RemoveListener`. 런타임 리스너라 인스펙터 persistent call(`OnStartGame`)은 그대로 살아 있고
씬 에셋도 변경되지 않는다.

```csharp
if (!TutorialProgress.CompleteLobbyIntro(profileSO.profile)) { EndChapter(); return; }
TrySaveProfile();   // try/catch + 경고 로그
EndChapter();       // guidance.Hide() → overlay.Hide() → _step = None → 구독 해제
```

**`profileSO.profile` 을 캐시하지 않고 이 시점에 다시 읽는다.** `OutgameMenuController.Awake` 는
`ApplyAuthGate`(L52) 뒤 L68 에서 인스턴스를 교체하므로, 캐시하면 버려질 객체에 쓰게 된다.

전투 진입은 우리가 하지 않는다 — 인스펙터 배선된 `OnStartGame` 이 같은 클릭으로 이미 실행된다.
`OnStartGame` 이 로드아웃 게이트에 걸려 팝업을 띄우거나, 참조 미배선으로 조기 return 해도
(`OutgameMenuController.cs:146-150`) 완료 저장은 유지한다(fail-open). 안내는 이미 소비됐고 팝업이
다음 지시를 이어받는다.

### 이벤트 구독 규약

`overlay.Tapped` / `guidance.SkipRequested` 구독은 **`Awake` 에서 1회, 해제는 `OnDestroy` 에서 1회**
(`LoginAutoImport.cs:33·38` 규약). `OnEnable`/`OnDisable` 쌍은 쓰지 않는다. 핸들러 진입부에서
`_step` 이 예상 단계인지 확인해 중복 발화가 단계를 건너뛰지 못하게 한다.

### 탈출구

- `IntroFocus` 진입 후 `escapeDelaySeconds`(기본 8초) 무진행 → `ShowMessage(같은 문구, showSkip: true)`.
- `Keyboard.current.escapeKey.wasPressedThisFrame`(Android 백키) → Skip 과 동일 취급.
- Skip → 완료 저장 후 안내만 종료. **전투를 시작하지 않는다.**

### `AbortChapter()`

로그아웃(`OnResetAccount` → `ApplyAuthGate(false)`) 등 중단 경로의 단일 창구다.
`guidance.Hide()` + `overlay.Hide()` + **`_step = Step.None`** + 임시 RectTransform 정리 +
onClick 임시 구독 해제. `_step` 을 되돌리지 않으면 재로그인 시 재진입 가드에 걸려 챕터가 영구 봉인된다.

### 참조 누락

`startButton` 이 null 이면 `SetHoles(null)` 로 구멍 없이 표시하고 dim 탭으로 종료 경로를 탄다.
`profileSO` 가 null 이거나 `IsLoadedThisSession == false` 면 아무것도 하지 않는다.

## 완료 기준

- [ ] 컴파일 통과
- [ ] `RESET TUTORIAL` → OutgameScene Play → 로그인 통과 직후 dim + 문구가 뜬다
- [ ] 첫 탭에 문구가 바뀌고 StartButton 만 밝게 뚫린다
- [ ] **dim 영역 탭은 아무 일도 일어나지 않는다**
- [ ] **StartButton 탭에 BattleScene 으로 전환된다** (우리가 아니라 인스펙터 배선이 실행)
- [ ] 전투 후 로비 복귀 시 챕터 A 가 다시 뜨지 않는다
- [ ] 안내 중 SQUAD·DREAMCATCHER·PRESET·HISTORY·DEV 버튼과 로비 캐릭터가 전부 무반응
- [ ] 8초 대기 시 Skip 이 나타나고, 누르면 전투로 가지 않고 안내만 닫힌다
- [ ] 로드아웃 게이트가 걸리는 상태(덱 미충족)에서 진행 → 게이트 팝업이 뜨고, 오버레이는 남지 않으며 로비 조작이 가능하다
- [ ] `IntroFocus` 에서 `RESET ACCOUNT` → 재로그인 시 챕터 A 가 **처음부터 정상 재생**된다
- [ ] `profile.json` 의 `lobbyIntroVersion` 이 `1`
