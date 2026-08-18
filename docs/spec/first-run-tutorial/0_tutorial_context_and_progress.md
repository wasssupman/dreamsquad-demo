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
마이그레이션 없음 — `JsonUtility` 가 없는 키를 이니셜라이저 값(`false`)으로 남긴다
(`schemaVersion` 불변).

**판정.** `ShouldRun(PlayerProfile profile)` = `profile != null && !profile.firstRunTutorialDone`.
소비자가 로비 스텝과 배틀 컨트롤러 **둘**이라 공용 자리가 필요하지만, 선례
(`OutgameMenuController.IsFirstMatch` — 소비 클래스의 `internal static`)를 넘어서는
타입을 세우지 않는다(제약 8). `FirstRunTutorialConfig` 옆의 작은 `static` 하나로 충분하다.

호출부는 여기에 `profileSO.IsLoadedThisSession` 을 곱한다 — 미로드 프로필의 빈
인스턴스가 `false` 로 읽혀 이미 튜토리얼을 본 유저에게 다시 뜨는 것을 막는다.
(세션 가드는 SO 상태라 순수 함수로 못 겨눈다. `FirstMatchTournamentBypassTests` 와 같은 형태.)

**`matchesPlayed` 를 읽지 않는다** (계약 2).

**완료 기록 시점은 B4 정상 종료뿐이다.** 로비 스텝만 보고 끄지 않고, **B3·B4 가 스킵/
타임아웃으로 끝난 판도 기록하지 않는다**(계약 11). 판을 중간에 나간 사람도 다음 판에서
처음부터 다시 본다.

**타이밍 SO.**

| 필드 | 기본 | 뜻 |
|---|---|---|
| `briefingHoldSeconds` | 1.2 | 맵 설명에서 한 면(가능/불가)을 보여주는 시간 |
| `briefingCycles` | 2 | 가능 ↔ 불가 왕복 횟수 |
| `goalMessageSeconds` | 2.5 | "게임목표" 문구 노출 |
| `battleFreezeAtSeconds` | 4 | 전투 시작 후 첫 정지까지. **적이 화면에 들어와 있어야 의미가 생기므로 실측 튜닝 대상** |
| `onPlaceWatchSeconds` | 2 | 배치 후 정지를 풀어 배치 스킬을 보여주는 시간 |
| `resumeBeforeAttachSeconds` | 5 | B3 종료 후 다시 정지할 때까지 |
| `attachSettleSeconds` | 2 | 부착 연출 후 마무리 문구까지 |
| `stepTimeoutSeconds` | 20 | 스텝이 응답을 못 받았을 때 흘려보내는 상한(계약 11) |

`targetCell` 은 두지 않는다 — B3 는 지정 칸을 강제하지 않는다(unit 5).

**배선**: 컨트롤러의 `[SerializeField]` 하나면 된다. `GameManager` 에 노출할 이유가 없다
(`TutorialGuidanceView.style` 과 같은 형태).

**RESET TUTORIAL 버튼 본체.** `OnResetTutorial` 이 `firstRunTutorialDone = false` 로
되돌리고 `ProfileStore.Save(profile)` 를 부른다. 지금은 로그만 찍는 껍데기다(`e18d419e`).

⚠ **옛 구현과 다른 점을 알고 버린다**: 옛 RESET 은 프로필 JSON 에서 튜토리얼 토큰만
패치했다("전체 재직렬화를 피해 새 클라이언트가 쓴 계정 필드를 잃지 않으려는 것").
`ProfileStore.Save` 는 통 재직렬화다. 지금은 클라이언트가 하나뿐이고 개발 버튼이라
받아들인다. 계정 필드를 외부에서 쓰는 주체가 생기면 그때 토큰 패치로 되돌린다.
`matchesPlayed` 는 그대로 둔다.

## 완료 기준

- compile 통과.
- EditMode: 새 프로필 → `ShouldRun` 참 / `firstRunTutorialDone=true` → 거짓 / `null` → 거짓.
- `matchesPlayed` 를 어떤 값으로 바꿔도 `ShouldRun` 이 변하지 않는다(겸직 금지 단언).
- 기존 프로필 JSON(필드 없음)을 읽어도 예외 없이 `ShouldRun` 참.
- 개발 트레이 `RESET TUTORIAL` → 로그 + `profile.json` 의 `firstRunTutorialDone` 이 `false`.
