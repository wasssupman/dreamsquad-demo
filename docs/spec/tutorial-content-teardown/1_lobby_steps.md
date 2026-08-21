# unit 1 — 로비 스텝 제거

## 목적

로비 차단형 온보딩(챕터 A/B/C/D)을 걷는다. 로비는 처음 진입한 플레이어에게도 아무것도 가로막지 않는 화면이 된다.

## 변경 대상

**삭제**
- `Assets/_Project/Scripts/UI/Outgame/Tutorial/OutgameTutorialController.cs` (531줄, `Step{IntroMessage,IntroFocus,SquadFocus,DeckFocus,KeyringFocus,KeyringSettling,StartFocus,HistoryFocus}`)
- `Assets/_Project/Tests/EditMode/OutgameTutorialChapterCTests.cs`
- `Assets/_Project/Editor/FirstSessionTutorialMenu.cs` — `[MenuItem("Wassup/Tutorial/Reset First Session Tutorial")]`. 이 메뉴 하나만을 위한 파일이라 통삭제. ⚠ **Editor 어셈블리**(Assembly-CSharp-Editor)라 여기서 컴파일이 깨지면 Play·테스트·빌드가 전부 막힌다 — Play 회귀보다 파급이 크다.
- `OutgameScene.unity` 의 `OutgameTutorialController` 오브젝트/컴포넌트

**참조 제거**
- `UI/Outgame/OutgameMenuController.cs` — `outgameTutorial` 필드 + 그 호출부, `OnResetTutorial()` 과 RESET TUTORIAL 버튼 배선

**남긴다**
- `OutgameTutorialOverlay` · `OutgameTutorialDimLayout` · `OutgameTutorialTapZone` (도구, 계약 1)
- `OutgameTutorialDimLayoutTests` (도구의 회귀 그물)

## 구현

컨트롤러가 유일한 소비자였던 오버레이는 **씬에 오브젝트로 남긴다**(비활성). 재설계가 다시 배선하지 않아도 되도록 두는 것이고, 활성화 주체가 없으므로 화면에는 나타나지 않는다. 오버레이 오브젝트까지 지우면 도구를 남긴 의미가 절반 사라진다.

`ResetTutorialProgressAt` 의 호출자는 **셋**이다 — `OutgameMenuController.OnResetTutorial()`(로비 메뉴), `Editor/FirstSessionTutorialMenu.cs`(에디터 메뉴), `TutorialProgressTests`(unit 2 소관). 앞의 둘을 여기서 걷어야 unit 2 가 API 를 지울 수 있다(계약 8).

`OnResetTutorial()` 은 진행값을 되돌리는 디버그 버튼이다. 되돌릴 진행값이 unit 2 에서 사라지므로 여기서 같이 걷는다 — 단 `ProfileStore.ResetTutorialProgressAt` **API 자체의 제거는 unit 2 소관**이다(호출자 → 피호출자 순서, 계약 7).

## 완료 기준

- 컴파일 통과. EditMode 두 lane 그린(삭제한 테스트 제외).
- Play(신규 프로필로 로비 진입): 딤·포커스·말풍선 없음. 스쿼드/덱/키링/START/히스토리 버튼이 처음부터 전부 눌린다.
- 메뉴에 RESET TUTORIAL 이 없고, 남은 메뉴 항목이 정상 동작한다.
- 콘솔 에러 0. 씬에 미싱 스크립트 참조 없음.
