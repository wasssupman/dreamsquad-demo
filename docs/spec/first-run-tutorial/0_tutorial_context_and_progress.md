# 0 — 진행 저장 · 실행 판정 · 타이밍 SO

## 목적

튜토리얼이 **언제 도는지**와 **얼마나 기다리는지**를 한 자리에 만든다. 이후 unit 은
전부 이 둘을 읽기만 한다.

## 변경 대상

- `Assets/_Project/Scripts/Core/Profile/PlayerProfile.cs`
- `Assets/_Project/Scripts/Data/FirstRunTutorialConfig.cs` (신규)
- `Assets/_Project/Data/Config/FirstRunTutorialConfig.asset` (신규)
- `Assets/_Project/Scripts/UI/Outgame/OutgameMenuController.cs` (`OnResetTutorial` 본체)
- `Assets/_Project/Tests/EditMode/FirstRunTutorialGateTests.cs` (신규)

## 구현

**진행 필드 1개.** `PlayerProfile` 에 `public bool firstRunTutorialDone;` 를 더한다.
마이그레이션 없음 — `JsonUtility` 가 없는 키를 `false` 로 읽는다(`schemaVersion` 불변).

**판정은 순수 함수.** `FirstRunTutorialGate.ShouldRun(PlayerProfile profile)` =
`profile != null && !profile.firstRunTutorialDone`. 호출부는 여기에
`profileSO.IsLoadedThisSession` 을 곱한다 — 미로드 프로필의 빈 인스턴스가 `false` 로
읽혀 이미 튜토리얼을 본 유저에게 다시 뜨는 것을 막는다. (세션 가드는 SO 상태라
순수 함수로 못 겨눈다. `FirstMatchTournamentBypassTests` 와 같은 형태.)

**`matchesPlayed` 를 읽지 않는다** (계약 2). 그 필드는 「첫 판은 토너먼트에 올리지
않는다」의 유일한 신호이고, 튜토리얼을 그 위에 얹으면 한쪽을 끄는 순간 다른 쪽이
같이 꺼진다.

**완료 기록 시점은 B4 종료.** 로비 스텝만 보고 끄지 않는다 — 판을 중간에 나간
사람은 다음 판에서 처음부터 다시 본다(의도).

**타이밍 SO.** `FirstRunTutorialConfig` 에 초 단위 값과 저작 좌표를 담는다. 문구는
컨트롤러의 `const` 다(옛 `OutgameTutorialController` 관용구).

| 필드 | 기본 | 뜻 |
|---|---|---|
| `briefingHoldSeconds` | 1.2 | 맵 설명에서 한 면(가능/불가)을 보여주는 시간 |
| `briefingCycles` | 2 | 가능 ↔ 불가 왕복 횟수 |
| `goalMessageSeconds` | 2.5 | "게임 목표" 문구 노출 |
| `battleFreezeAtSeconds` | 4 | 전투 시작 후 첫 정지까지 |
| `onPlaceWatchSeconds` | 2 | 배치 후 정지를 풀어 배치 스킬을 보여주는 시간 |
| `resumeBeforeAttachSeconds` | 5 | B3 종료 후 다시 정지할 때까지 |
| `attachSettleSeconds` | 2 | 부착 연출 후 마무리 문구까지 |
| `stepTimeoutSeconds` | 20 | 스텝이 응답을 못 받았을 때 흘려보내는 상한(계약 10) |
| `targetCell` | (저작) | B3 에서 캐논을 놓게 할 칸. 고정 맵이라 좌표를 못 박는다 (unit 6) |

**RESET TUTORIAL 버튼 본체.** `OnResetTutorial` 이 `firstRunTutorialDone = false` 로
되돌리고 프로필을 저장한다. 지금은 로그만 찍는 껍데기다(`e18d419e`).
`matchesPlayed` 는 그대로 둔다.

## 완료 기준

- compile 통과.
- EditMode: 새 프로필 → `ShouldRun` 참 / `firstRunTutorialDone=true` → 거짓 /
  `null` → 거짓.
- `matchesPlayed` 를 어떤 값으로 바꿔도 `ShouldRun` 이 변하지 않는다(겸직 금지 단언).
- 기존 프로필 JSON(필드 없음)을 읽어도 예외 없이 `ShouldRun` 참.
- 개발 트레이 `RESET TUTORIAL` → 로그 + `profile.json` 의 `firstRunTutorialDone` 이 `false`.
