# 8 — 매치 카운터 + 챕터 D 진행 토큰

## 목적

챕터 D(unit 9)가 "두 번째 판 이후" 를 **독립 신호**로 판정할 수 있게 매치 카운터를 만든다.
기존 챕터가 쓰는 "앞 챕터 완료" 체인은 백로그가 이미 결함으로 지적한 형태다 — 선행 안내가
fail-open 경로를 타면 뒤 안내가 **영영 발화하지 못한다**. 이 유닛은 그 지적의 절반을 해소한다.

이 유닛만 착지하면 카운터가 늘고 저장될 뿐 **아무도 읽지 않는다**.

## 변경 대상

- `Assets/_Project/Scripts/Core/Profile/PlayerProfile.cs`
- `Assets/_Project/Scripts/Core/GameManager.cs`
- `Assets/_Project/Scripts/Core/Profile/TutorialProgress.cs`
- `Assets/_Project/Tests/EditMode/TutorialProgressTests.cs`

## 구현

**카운터**: `public int matchesPlayed;` 를 프로필에 추가한다. **튜토리얼 진행 토큰이 아니다** —
`TutorialProgress.ResetAll`/`ResetAllInJson` 에 **넣지 않는다**. `RESET TUTORIAL` 후에도 카운터가
남아야 안내를 다시 보려고 두 판을 더 뛸 필요가 없고, 의미상으로도 매치 이력은 튜토리얼 진행이
아니다.

**증가 지점**: 종료 경로 **둘** 다. `GameManager.RecordMatchPlayed()` 를 판당 1회 래치로 두고
`SetPhase(GamePhase.Result)`(결과 화면까지 본 판)와 `MenuPopup.OnExit`(나가기)에서 부른다.
나가기 호출은 **`SceneTransition.Go` 앞**이어야 한다 — 전환 뒤엔 `GameManager` 가 이미 파괴돼
기록이 유실된다.

**세는 기준 = "히스토리에 남는 판"** 이다(2026-08-01 정정). 처음엔 "완주한 판" 으로 잡고 중도
이탈을 뺐는데 그건 틀렸다 — `나가기` 는 `Result` 를 거치지 않지만 `AbandonMatch` 가 0점으로
마감해 **그 판도 자기 엔트리로 히스토리에 남는다**(`TournamentMatchReporter.cs:272-274`).
챕터 D 가 가르치는 대상이 바로 그 히스토리이므로, 카운터와 안내가 같은 것을 세야 한다.
이 오판정 때문에 두 판을 뛰고도 카운터가 1 에 머물러 챕터 D 가 안 뜨는 결함이 실제로 났다.

저장은 `profileSO` 가 **이번 세션에 로드된 경우에만** 한다(`IsLoadedThisSession`). BattleScene
직접 Play 는 프로필이 없으므로 건너뛴다 — 튜토리얼 컨트롤러들의 `TrySaveProfile` 과 같은 가드다.
**단 조용히 빠지지 않고 로그를 남긴다**: 조기 return 이 무음이면 "가드에 막혔나 / 아예 안
불렸나" 를 데이터로 구분할 수 없고, 위 결함을 실제로 진단하지 못한 이유가 그것이었다.
저장 실패는 경고만 남기고 판 흐름을 막지 않는다.

**`ProfileSaver` 주입 seam을 둔다.** 이 프로젝트의 프로필 쓰기 주체는 전부 갖고 있는 패턴이고
(`FirstSessionTutorialController`·`OutgameTutorialController`·`SquadCharacterPageController`),
없으면 증가 로직에 테스트를 붙일 때 개발자의 실제 `profile.json` 을 재작성하게 된다.

**세는 기준 = "완주한 배틀"** 이다. Test Mode 도 엔드리스도 포함한다. `ReportMatchResult` 가
`IsEndless` 를 배제하는 것과 대칭이 아닌 건 의도다 — 그쪽은 토너먼트 집계라 모드를 가려야 하고,
이쪽은 "판을 해봤나" 라는 경험 신호다. 덧붙여 Test Mode 는 **가릴 수도 없다**: `TestModeContext`
는 배치 전 `StartTestModeMatch`(`GameManager.cs:362`)에서 1회 소비돼 Result 시점엔 이미
`Active == false` 이므로, 거기에 가드를 넣으면 아무 일도 하지 않는 죽은 코드가 된다.
(QA 관점에선 오히려 이롭다 — Test Mode 로 두 판을 돌리면 챕터 D 를 바로 관찰할 수 있다.)

**진행 토큰**: `lobbyHistoryHintVersion` + `IsLobbyHistoryHintPending` + `CompleteLobbyHistoryHint`.
이건 **튜토리얼 토큰이므로** `ResetAll`/`ResetAllInJson` 의 `changed` 표현식에 **양쪽 다** 넣는다
(unit 6·17 함정). 게이트 함수(`ShouldRunLobbyHistoryHint`)는 unit 9 가 만든다 — 계정 조건까지
얽히므로 소비자와 같은 유닛에 두는 편이 읽기 쉽다.

## 완료 기준

- [ ] compile clean.
- [ ] `TutorialProgressTests` 신규: 토큰 pending 기본값 · `Complete…` 멱등 · `ResetAll`/
      `ResetAllInJson` 이 이 토큰만 다를 때도 `changed == true`.
- [ ] **`matchesPlayed` 는 `ResetAll` 이 건드리지 않는다**는 것을 테스트로 고정한다(반대 방향의
      회귀 — 누가 "튜토리얼 토큰이니까" 하고 리셋에 넣는 것을 막는다).
- [ ] EditMode 전체 실패 0.
- [ ] `GameManagerMatchCountTests`: 결과 화면 경로 · **나가기 경로** · 두 신호가 겹쳐도 1회 ·
      기존 값에 누적 · 미로드 프로필은 증가·저장 없음 · 저장 실패 fail-open.
- [ ] 판을 끝까지 한 번 진행하면 `profile.json` 의 `matchesPlayed` 가 1 늘어난다.
- [ ] **나가기로 끝낸 판도** 1 늘어난다.
