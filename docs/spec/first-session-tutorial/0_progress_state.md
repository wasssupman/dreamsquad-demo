# 0 — 튜토리얼 진행 상태

## 목적

첫 판 핵심 안내와 첫 각성 힌트를 각각 한 번만 노출하고, 앱 종료·씬 왕복 후에도 완료 상태를 유지한다.
향후 문구/흐름을 바꿔 다시 노출할 수 있도록 bool 대신 버전 정수를 사용한다.

## 변경 대상

- `Assets/_Project/Scripts/Core/Profile/PlayerProfile.cs`
- `Assets/_Project/Scripts/Core/Profile/PlayerProfileSO.cs`
- `Assets/_Project/Scripts/Core/Profile/TutorialProgress.cs` (신규)
- `Assets/_Project/Scripts/UI/Outgame/OutgameMenuController.cs`
- `Assets/_Project/Editor/FirstSessionTutorialMenu.cs` (에디터 다시 보기 메뉴)
- `Assets/_Project/Tests/EditMode/TutorialProgressTests.cs` (신규)

## 구현

`PlayerProfile`에 아래 additive 필드를 추가한다.

- `firstBattleTutorialVersion`
- `awakeningHintVersion`

구 JSON에 필드가 없으면 `JsonUtility` 기본값 0이므로 migration 없이 미완료로 해석한다. `schemaVersion`은
올리지 않는다. `TutorialProgress`는 현재 버전 상수(각 1), 노출 판정, 완료 기록만 소유하는 순수 static
유틸이다. 저장은 UI 오케스트레이터가 상태 변경 직후 `ProfileStore.Save`로 수행한다.

`PlayerProfileSO`에는 직렬화하지 않는 런타임 플래그 `IsLoadedThisSession`과 `SetLoadedProfile`을 둔다.
Outgame의 정상 `ProfileStore.LoadOrCreate` 경로만 이 플래그를 true로 만들며, 직접 BattleScene Play에서
asset 기본 `profile`이 non-null이어도 `ShouldRun*`은 false다. 저장은 플래그가 true일 때만 시도한다.

`ProfileStore.Save`는 현재 I/O 예외를 호출자에게 전달하므로 오케스트레이터가 try/catch한다. 저장 실패 시
경고를 남기고 모든 hold/안내를 원복해 게임을 계속한다. 메모리 버전 갱신은 유지해 같은 런타임 세션에서
반복 노출하지 않는다.

핵심 안내와 각성 힌트는 독립 상태다. 핵심을 건너뛰어도 이후 각성 힌트는 한 번 노출될 수 있고, 각성
힌트를 먼저 봤다고 핵심 안내를 완료 처리하지 않는다.

반복 QA는 Unity 상단 메뉴 `Wassup > Tutorial > Reset First Session Tutorial`을 사용한다. Edit Mode에서만
활성화되며 두 튜토리얼 버전만 0으로 저장한다. 변경 전 `profile.json.tutorial-reset.bak`을 남기고 스쿼드,
덱, 계정 등 다른 필드는 유지한다. 초기화 후에는 직접 BattleScene Play가 아니라 OutgameScene에서 정상
진입해야 세션 로드 가드를 통과한다.

## 완료 기준

- [x] compile clean.
- [x] EditMode: 신규 프로필은 두 안내 모두 pending.
- [x] 각 완료 메서드는 해당 버전만 갱신하고 다른 상태를 건드리지 않는다.
- [x] 현재 버전 이상이면 다시 노출하지 않는다.
- [x] null 프로필은 두 안내 모두 실행하지 않는다.
- [x] non-null asset 기본 프로필이어도 `IsLoadedThisSession=false`면 안내·저장을 실행하지 않는다.
- [x] 정상 Outgame 로드 후 `IsLoadedThisSession=true`이며 안내 대상이 된다.
- [x] JSON round-trip 후 두 버전 값이 유지된다.
- [x] 기존 프로필 JSON(필드 없음)은 0으로 로드되어 첫 안내 대상이 된다.
- [x] 에디터 메뉴 초기화는 두 튜토리얼 버전만 0으로 만들고 반복 실행해도 안전하다.
