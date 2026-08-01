# 10 — 인계 요약 (units 8~9: 챕터 D 히스토리)

`7_handoff_summary.md`(챕터 C) 이후분. 최신 계약은 README + 번호 문서 우선.

## Commit

| 해시 | 내용 |
|---|---|
| `98952437` | docs — units 8~9 스펙 (비목표 뒤집기 근거 포함) |
| `98c315d1` | unit 8 — `matchesPlayed` 카운터 + 챕터 D 진행 토큰 |
| `3c996168` | unit 9 — 챕터 D(히스토리 버튼) |

## Implemented

- **챕터 D**: 두 판을 끝낸 뒤 로비 도착 시 히스토리 버튼에 dim 통과구멍 포커스 +
  `히스토리에서 지난 판의 기록을 볼 수 있어요!`. **실제로 버튼을 눌러야** 종료된다.
- 게이트는 `matchesPlayed >= 2` 라는 **독립 신호**다 — 앞 챕터 완료를 체인하지 않는다.
- **게스트 미노출**: 히스토리 버튼이 `HasAccount` 게이트로 비활성이라 챕터가 아예 열리지 않는다.
- `GameManager.SetPhase` 가 `Result` 전이에서 `matchesPlayed` 를 1회 증가·저장한다.

## Key Files

- `UI/Outgame/Tutorial/OutgameTutorialController.cs` — `Step.HistoryFocus` (분기 5곳)
- `Core/GameManager.cs` — `RecordMatchPlayed`
- `Core/Profile/PlayerProfile.cs` · `TutorialProgress.cs`
- `Scenes/OutgameScene.unity` — `historyButton → HistoryButton` RectTransform(fileID 2126287054)

## Verified

- 컴파일 0 · EditMode **1790 중 실패 0**(신규 4건).
- 씬 배선 실측(`SerializedObject`): `historyButton` → `HistoryButton`, `Button` 존재,
  형제 배선 4개(start·squad·dreamcatcher·keyring) 무손상.
- `GameManager.profileSO` 배선 확인 — 다른 소비자와 같은 GUID.
- **씬을 열지 않고 디스크 YAML 을 편집했다**(OutgameScene 미로드 상태). 검증만 additive
  open → 무저장 close. 씬 저장이 남의 미저장 WIP 를 굽는 사고를 피하는 경로다.
- 코드 리뷰(Codex): CRITICAL/HIGH 0. MEDIUM 1건(Test Mode 카운트)은 아래대로 의도로 확정.

## Notes (되돌리면 안 되는 것)

- **`matchesPlayed` 는 `TutorialProgress.ResetAll` 대상이 아니다.** 튜토리얼 진행이 아니라 매치
  이력이다. 넣으면 `RESET TUTORIAL` 후 챕터 D 를 보려고 두 판을 다시 뛰어야 한다. 역방향 회귀
  테스트 `ResetAll_DoesNotClearMatchesPlayed` 가 이걸 고정한다.
- **`lobbyHistoryHintVersion` 은 반대로 반드시 `ResetAll`/`ResetAllInJson` 양쪽에 있어야 한다**
  (unit 6·17 함정).
- **챕터 D 는 앞 챕터 완료를 체인하지 않는다.** 형제 게이트의 `!IsCorePending` 형태는 백로그가
  이미 결함으로 지적했다 — 선행 안내가 fail-open 경로를 타면 뒤 안내가 영영 발화 못 한다.
- **계정 조건을 `TutorialProgress` 에 넣지 말 것.** 진행 정책 순수 함수에 세션 상태를 끌어들이면
  EditMode 테스트가 전역 상태에 묶인다. 게스트 차단은 컨트롤러의 **대상 활성 검사**가 겸한다.
- **`EnterStep(HistoryFocus)` 의 `overlay.Show()` 를 빼지 말 것.** 포커스에서 시작하는 챕터는
  이전 챕터의 `Hide()` 로 DimRoot 가 비활성인 채라, 켜지 않으면 말풍선만 뜨고 dim 이 안 나온다
  (챕터 C 가 남긴 함정).
- **대상이 없거나 비활성이면 챕터를 아예 열지 않는다.** dim 만 띄우면 dim 탭이 무반응이라
  8초 Skip 이 뜰 때까지 로비가 통째로 잠긴다. 완료를 저장하지 않으므로 계정을 만들면 다음
  복귀에서 정상 노출된다.
- **`RecordMatchPlayed` 에 `TestModeContext.Active` 가드를 넣지 말 것.** 그 토큰은 배치 전
  `StartTestModeMatch`(`GameManager.cs:362`)에서 소비돼 Result 시점엔 이미 false 다 — 넣으면
  아무 일도 안 하는 죽은 코드가 된다. Test Mode 도 "완주한 배틀" 로 센다(의도).

## Follow-up

- **사용자 Play 확인 미완** — `9_history_chapter.md` 의 완료 기준 참조. QA 는 Test Mode 로 두 판을
  돌리면 바로 챕터 D 에 도달할 수 있다(위 Notes 참조).
- 백로그의 **"챕터 B 게이트를 독립 신호로 교체"** 는 이제 절반만 남았다 — `matchesPlayed` 가
  생겼으므로 B 도 같은 신호로 옮길 수 있다.
- 온보딩 총량이 로비 4챕터로 늘었다. `first-session-tutorial` 의 **총량 다이어트** 후보와 함께 볼 것.
