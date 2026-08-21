# 2 — 로비 강제 포커스 (L)

## 목적

로그인 후 로비에 처음 들어온 사람에게 **START 말고는 아무것도 안 눌리게** 한다.

## 변경 대상

- `Assets/_Project/Scripts/UI/Outgame/Tutorial/LobbyTutorialStep.cs` (신규)
- `Assets/_Project/Scenes/OutgameScene.unity` (`TutorialTools` 아래 배선)
- `Assets/_Project/Scripts/UI/Outgame/OutgameMenuController.cs` (호출 지점)

## 구현

**도구는 이미 있다.** `OutgameTutorialOverlay`(딤 + 구멍 + `Tapped`) ·
`OutgameTutorialDimLayout` · `OutgameTutorialTapZone` · `TutorialGuidanceView`.
새 위젯을 만들지 않는다(계약 8). 씬의 `TutorialTools/Dim`·`Guidance` 가 그대로 이들이다.

**한 스텝짜리 컨트롤러**를 만든다. 옛 `OutgameTutorialController`(챕터 A~D 8스텝)를
되살리지 않는다 — 지금 필요한 것은 문구 하나 + 구멍 하나다.

```
overlay.SetSortingOrder(guidance.DimSortingOrder)
overlay.SetHoles([startButtonRect]); overlay.Show()
guidance.ShowMessage(IntroText, showSkip: false)
guidance.FocusUi(startButtonRect)
```

`IntroText = "누가 더 많은 악몽을 제거 하는지 시작해 보시죠"` (원문 그대로).

**호출 지점이 계약의 일부다.** 두 곳에서 부른다 — **`Start`**(프로필 로드 이후)와
`ApplyAuthGate`(로그인 직후). 로그인 전에는 띄우지 않는다. 판정은 unit 0 의
`ShouldRun × IsLoadedThisSession`.

### ⚠ `Awake` 에서 띄우면 안 된다 (옛 컨트롤러의 관용구를 버린 자리)

`TutorialGuidanceView.Awake` 는 `BuildCanvas(); Hide();` 를 부른다. `Awake` 안에서 안내를
띄우면 **그 뷰의 `Awake` 가 아직 안 돈 경우 방금 띄운 문구와 포커스 링을 그 `Hide()` 가
지운다.** 딤과 구멍은 다른 컴포넌트(`OutgameTutorialOverlay`)라 남으므로 화면은
«버튼만 포커스되고 텍스트만 없는» 모양이 된다. 셋 중 아무도 `[DefaultExecutionOrder]` 를
갖지 않아 `Awake` 순서 보장이 없다.

**간헐적으로 보였던 이유**: 로그인 화면을 거치는 «차가운 시작» 은 `onSignedIn` 콜백에서
띄워 모든 `Awake` 이후라 멀쩡했고, 세션 복구·판 종료 후 로비 복귀 같은 «따뜻한 시작» 만
`Awake` 안에서 띄워 지워졌다.

`Start` 는 모든 `Awake` 뒤에 돈다. `ApplyAuthGate` 는 `Awake` 첫 줄에서도 불리므로
`_started` 전에는 넘긴다(곧 `Start` 가 띄운다).

### ⚠ 로드아웃 게이트와 겹치면 로비가 잠긴다

START 는 게이트 미충족 시 `LoadoutGatePopup` 을 띄우고 돌아간다. **그 팝업은 자체
캔버스가 없다** — 로비 루트 캔버스(order 0)에 형제로 붙어 뜨므로 딤(1499) **아래로
깔린다**. 그러면 START 를 눌러도 화면이 그대로이고 다른 버튼은 전부 막혀 있어
빠져나갈 길이 없다.

**진입 조건에 `LoadoutGate.Check` 통과를 곱한다.** 게이트를 못 넘는 계정에는 딤을
아예 띄우지 않는다(그 계정은 온보딩보다 편성부터 고쳐야 한다). 신규 계정은
`ProfileStore` 시드로 통과하므로 정상 경로에는 영향이 없다. 위험한 것은 RESET 으로
재실행하는 계정과, 카드/유닛 id 리네임으로 저장 덱 Validate 가 깨진 계정이다.

**딤은 START 를 대신 눌러주지 않는다.** 구멍 밖 탭은 무시한다(`overlay.Tapped` 를
여기서 구독하지 않는다 — 탭으로 넘어가는 스텝이 아니다).

START 가 눌려 씬이 전환되면 오버레이는 씬과 함께 사라진다. **여기서 진행을 저장하지
않는다** — 완료 기록은 B4 정상 종료다(unit 0 · 계약 11).

## 완료 기준

- compile 통과.
- 새 프로필로 로비 진입 → 딤 + START 구멍 + 문구. 스쿼드/드림캐쳐/히스토리 버튼은 눌리지 않는다.
- START 는 눌리고 배틀 씬으로 넘어간다.
- **편성이 게이트를 못 넘는 프로필로 진입 → 딤이 뜨지 않고 로비를 정상적으로 쓸 수 있다.**
- `firstRunTutorialDone=true` 인 프로필로 진입 → 딤 없음, 모든 버튼 즉시 클릭 가능.
- 로그인 전 상태에서는 딤이 뜨지 않는다.
