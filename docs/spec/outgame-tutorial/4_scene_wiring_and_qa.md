# 4 — 씬 배선과 QA

## 목적

OutgameScene 에 오버레이를 얹고 진입 훅을 연결한 뒤, 두 챕터를 Play 로 검증한다.

## 변경 대상

- `Assets/_Project/Scripts/UI/Outgame/OutgameMenuController.cs`
- `Assets/_Project/Scenes/OutgameScene.unity`

## 구현

### 진입 훅 — 위치가 계약의 일부다

`OutgameMenuController` 에 필드 하나와 호출 두 곳을 추가한다.

```csharp
[SerializeField] private OutgameTutorialController outgameTutorial;
```

호출은 **`Awake()` 말미**, 즉 `profileSO.SetLoadedProfile(...)`(L68)과 `ClosePanels()`(L70) **뒤**에 둔다.

```csharp
ClosePanels();
if (outgameTutorial != null) outgameTutorial.OnLobbyShown(UserSession.IsSignedIn);
```

그리고 `ApplyAuthGate()` 말미에도 같은 호출을 둔다 — 로그인 완료(`onSignedIn`)와 로그아웃
(`OnResetAccount`) 경로를 받기 위해서다. `OnLobbyShown` 은 멱등이다(unit 2).

> **`ApplyAuthGate` 말미에만 두면 안 된다.** `ApplyAuthGate` 는 `Awake` 의 첫 줄(L52)이고 프로필
> 로드는 L68 이다. 그 시점 `profileSO.profile` 은 곧 교체될 인스턴스이며, 전투 복귀 경로에서는
> `onSignedIn` 이 재발화하지 않아 그 한 번이 유일한 호출이 된다(unit 3 참조). 초안이 여기에 훅을
> 걸고 "기존 분기를 건드리지 않는 순수 추가"라고 적은 것은 오류였다.

### 씬 배선

`MenuCanvas` 형제로 GameObject `OutgameTutorial` 을 만들고, 두 뷰는 **각각 자식 GameObject** 에 붙인다.

```
OutgameTutorial          [OutgameTutorialController]   ← Canvas 없음
├─ Dim                   [OutgameTutorialOverlay]      → Canvas order 9
└─ Guidance              [TutorialGuidanceView]        → Canvas order 10
```

**하드 요구 2가지**:

1. `OutgameTutorial` 은 **씬 루트**여야 한다 — 어떤 Canvas 의 자식도 아니어야 한다. 중첩 캔버스에서는
   sortingOrder 가 레이캐스트 우선순위를 결정하지 못하고 마지막 sibling 이 이긴다. 루트 형제 캔버스여야
   `GraphicRaycaster.sortOrderPriority == canvas.sortingOrder` 가 성립해 통과구멍 설계가 동작한다.
2. 두 뷰는 **서로 다른 GameObject** 여야 한다. 둘 다 `UiCanvasSetup.Ensure(gameObject, ...)` 를 자기
   GameObject 에 호출하므로(`TutorialGuidanceView.cs:355` 의 하드코딩 `SortingOrder = 10`), 한
   GameObject 에 얹으면 Canvas 하나를 공유하며 sortingOrder 를 서로 덮어쓴다.

배선 목록:

| 대상 | 값 |
|---|---|
| `profileSO` | `OutgameMenuController` 와 같은 `PlayerProfileSO` 에셋 |
| `overlay` / `guidance` | 자식 `Dim` / `Guidance` 의 두 뷰 |
| `startButton` | `MenuCanvas/SafeAreaRoot/MenuButtons/StartButton` |
| `squadButton` | 〃 `/SquadButton` |
| `dreamcatcherButton` | 〃 `/DreamcatcherButton` |
| `TutorialGuidanceView.style` | `Assets/_Project/Data/Config/TutorialGuidanceStyle_Default.asset` |
| `OutgameMenuController.outgameTutorial` | 위 `OutgameTutorial` |

**커밋 위생**: 씬 저장은 그 시점의 미저장 인메모리 변경까지 함께 베이크한다. 배선 커밋 전에 씬을
스냅샷하고 `git checkout HEAD` 후 이 unit 의 delta 만 재적용해 커밋한다. 커밋 직전 `git diff` 로
사용자가 authoring 중인 카메라·프랍 변경이 섞이지 않았는지 확인한다.

## 완료 기준

- [ ] 컴파일 통과, 콘솔 에러/경고 없음
- [ ] `RESET TUTORIAL` → Play 전체 흐름 1회: 챕터 A 2탭(START 실제 클릭) → 전투 → 복귀 → 챕터 B 2탭 → 종료
- [ ] 두 챕터 모두 안내 중 포커스 대상 **외** 로비 입력이 완전히 차단됨 (버튼·키링 드래그·캐릭터 클릭)
- [ ] 포커스된 버튼은 **실제로 눌린다** (챕터 A → 씬 전환, 챕터 B → 패널 열림)
- [ ] 안내 종료 후 로비 조작이 전부 정상 복귀
- [ ] **양성 대조**: 두 번째 Play 에서 `RESET TUTORIAL` 실행 후 안내가 **다시 뜬다**
      (단순히 "안 뜬다"만 확인하면 훅이 죽은 상태와 구분되지 않는다)
- [ ] 챕터 진행 중 `RESET ACCOUNT` → 로그인 패널 정상 노출, 오버레이 잔류 없음, 재로그인 시 챕터 정상 재생
- [ ] 게스트(`SKIP`) 경로에서도 두 챕터가 동일하게 동작
      (튜토리얼 플래그는 계정이 아니라 디바이스 로컬 `profile.json` 에 귀속된다 — QA 재현은
      `RESET ACCOUNT` 가 아니라 `RESET TUTORIAL` 을 쓴다)
- [ ] 챕터 B 종료 후 스쿼드 편집·저장 → 로비 재진입 시 재출현 없음
- [ ] 화면 회전/해상도 변경 후 홀이 대상 버튼을 계속 따라간다
- [ ] Android 가로 실기기: dim 이 노치·safe area 까지 덮는지, **포커스 링과 홀이 어긋나지 않는지**
      스크린샷으로 확인 (unit 3 의 알려진 한계)
- [ ] EditMode 전체 통과 (기존 테스트 회귀 없음)

> 검증 2026-07-21 · 커밋 `815b38c4`·`1dfc22a3` — 씬 diff 147줄 추가·삭제 0, `OutgameTutorial` 씬 루트 확인,
> EditMode 전체 회귀 없음. 사용자 Play 체감·Android 실기기 QA 대기.
