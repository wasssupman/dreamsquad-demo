# tutorial-content-teardown

> 상태: 작성됨 2026-08-18 — 구현 전. 전면 개편에 앞서 **튜토리얼 콘텐츠(페이즈·스텝)를 전부 걷어내고 도구만 남긴다.**

## 상위 목표

튜토리얼을 전면 재설계하기로 했다(사용자 결정 2026-08-18). 재설계 전에 **지금까지 제작된 모든 페이즈·스텝을 제거**하고, 안내를 그리는 **도구 계층은 남긴다**. 새 시퀀스는 빈 자리 위에서 새로 짠다.

## 검증 질문

1. **"첫 판부터 마지막 판까지 안내가 하나도 뜨지 않는가?"** — 인게임·로비 양쪽.
2. **"튜토리얼이 다른 뷰의 동작을 더 이상 바꾸지 않는가?"** — 배치 카운트다운·기믹 리빌·각성 버튼이 튜토리얼을 모른다.
3. **"안내를 그리는 도구는 그대로 살아 있는가?"** — 재설계가 `TutorialGuidanceView`/`OutgameTutorialOverlay` 를 코드 복원 없이 바로 쓸 수 있다.
4. **"기존 세이브로 들어와도 유령 안내나 유령 규칙이 없는가?"** — 프로필에 남은 옛 진행값이 아무 동작도 유발하지 않는다.

## 남기는 것 / 걷는 것

| | 대상 | 비고 |
|---|---|---|
| **남긴다(도구)** | `TutorialGuidanceView`(642) · `TutorialGuidanceStyle`(SO+에셋) | 딤·구멍 포커스·말풍선·월드 마커·탭 캐처 |
| | `OutgameTutorialOverlay`(280) · `OutgameTutorialDimLayout`(123) · `OutgameTutorialTapZone`(17) | 로비 오버레이 도구 |
| | `OutgameTutorialDimLayoutTests` · `TutorialDragGuidanceTests` 의 레이아웃 케이스 | 도구의 회귀 그물 |
| **걷는다(콘텐츠)** | `FirstSessionTutorialController` **5파일**(1,329) — `CoreStep{Goal,Pick,Place,WaitingAim,ClassHint,Start}` · 각성 · 배틀HUD · 효과타일 · 기믹리빌 | unit 0 |
| | `OutgameTutorialController`(531) — `Step{IntroMessage,IntroFocus,SquadFocus,DeckFocus,KeyringFocus,KeyringSettling,StartFocus,HistoryFocus}` | unit 1 |
| | 소비처 훅 — `PlacementPhaseView` 게이트 3 · `GimmickPhaseView` 홀드 이벤트 2 · **각성 봉인 체인 4단**(`AwakeningGaugeView.SetSuppressed`/`IsSuppressed` → `DreamcatcherHandView.AwakeningSealedThisMatch` → `DcInspectController.SealedThisMatch` 2곳) | unit 0 |
| | `Assets/_Project/Editor/FirstSessionTutorialMenu.cs` — RESET 에디터 메뉴 | unit 1 |
| | 진행 저장 — `TutorialProgress` 스텝 상수/판정 API · `PlayerProfile` 튜토리얼 필드 12 · `ProfileStore.ResetTutorialProgressAt` · RESET TUTORIAL 버튼 | unit 2 |

## feature-wide 계약

1. **도구는 호출자가 0이 되어도 남는다.** 제약 8("구현체 2개 이상일 때만")의 명시적 예외이며 근거는 사용자 결정이다 — 재설계가 이 도구를 다시 쓴다. 이 예외는 **도구 계층에만** 적용되고 컨트롤러·훅·진행 저장에는 적용되지 않는다.
2. **훅은 남기지 않는다** (사용자 결정 2026-08-18). 다른 뷰의 상태를 바꾸는 튜토리얼 전용 API 는 전부 제거하고, 그 뷰들은 튜토리얼을 **모르는** 상태로 되돌아간다. 재설계는 자기 설계에 맞는 훅을 그때 다시 판다.
3. **`match-intro-phase-toggles` 계약 6 을 개정한다.** 그 계약의 튜토리얼 예외 두 겹은 **둘 다 소멸**한다 — ① 첫 판 예외(`ShouldRunCore`)는 지울 수밖에 없다. `ShouldRunCore` 는 컨트롤러가 아니라 **프로필 버전**을 보므로 콘텐츠를 지워도 계속 참을 반환해, 두면 «튜토리얼이 없는데 첫 판만 30초 배치»라는 유령 규칙이 남는다. ② 튜토리얼 홀드 정지는 홀드를 거는 주체가 사라져 무의미하다. 결과: **`placementPhaseEnabled` 플래그가 곧 진실**이 된다.
4. **진행 저장은 초기화한다** (사용자 결정: "일단 프로그레스 관련은 초기화하고 추후에 새로 제작"). 스텝별 버전 상수·`ShouldRunXxx`/`CompleteXxx`·프로필 필드를 걷는다. 새 스키마는 재설계가 자기 이름으로 잡는다.
5. **«첫 판» 개념은 튜토리얼과 함께 죽지 않는다 — 신호를 갈아 끼운다.** `TutorialProgress.ShouldRunCore` 에는 튜토리얼과 무관한 두 번째 소비자가 있다: `OutgameMenuController.OnStartGame`(`:216`)이 **첫 판은 토너먼트 참가 신청 자체를 보내지 않는다**(`tutorial-offline-match` 계약 1, 커밋 `1ed41336`). 그 우회는 서버 `complete` 500 을 피하려는 것이고 **서버는 아직 안 고쳐졌다**. 술어를 그냥 지우면 컴파일이 깨지고, 그 컴파일 에러를 «if 블록 삭제»로 고치면 그 서버 버그가 조용히 되살아난다. → 이 판정을 **`profile.matchesPlayed == 0`** 으로 옮긴다. `matchesPlayed` 는 `GameManager` 가 매 판 올리는 **매치 이력**이고 튜토리얼 진행이 아니라서 `ResetAll` 도 건드리지 않는다(그 파일 주석이 명시). 「이 계정의 첫 판인가」를 튜토리얼 상태를 경유하지 않고 직접 말하므로, 우회의 의도에 더 가깝다. **동작 변경 없음**이 완료 기준이다.
6. **저장 안전 게이트는 남긴다.** `PlayerProfileSO.IsLoadedThisSession`(BattleScene 직접 Play 가 `profile.json` 을 덮어쓰는 것을 막는 가드)은 튜토리얼 전용이 아니다.
7. **기존 세이브는 마이그레이션하지 않는다.** 제거된 키는 로드 시 무시되고 다음 저장에서 사라진다 — 그게 "초기화"의 실현이다. `schemaVersion` 은 올리지 않는다(읽는 쪽이 사라지는 것이지 의미가 바뀌는 게 아니다).
8. **삭제는 호출자 → 피호출자 순서로.** unit 0·1 이 호출자를 걷고 unit 2 가 저장을 걷는다. 역순이면 중간 커밋이 컴파일되지 않는다.
9. **각 유닛은 독립 revert 가능**해야 한다. 인게임(0)과 로비(1)는 서로를 모르므로 순서를 바꿔도 컴파일된다.

## 작업 단위

| 파일 | 작업 구분 | 문서 | 목적 |
|---|---|---|---|
| 0 | 인게임 제거 | `0_ingame_steps_and_hooks.md` | 첫 판 시퀀스 5파일 + 소비처 훅 3곳 + 계약 6 개정 |
| 1 | 로비 제거 | `1_lobby_steps.md` | 로비 챕터 컨트롤러 + 메뉴 참조 + RESET 버튼 |
| 2 | 저장 초기화 | `2_progress_reset.md` | `TutorialProgress` 스텝 API + 프로필 필드 |
| 3 | handoff | `3_handoff_summary.md` | 종료/인계 요약 |

## 계획 비평 반영 (2026-08-18)

Codex 계획 critic 이 「unit 0·1 이 아닌 **제3의 파일**이 이미 쓰고 있다」 패턴으로 3건을 실증했다. 셋 다 계획에 반영됐다.

- `OutgameMenuController.cs:216` — 토너먼트 우회(위 계약 5). **가장 시급했다**: 컴파일 에러를 나이브하게 고치면 최근 고친 서버 버그가 되살아난다.
- `Tests/EditMode/AwakeningSealRelayTests.cs` — 삭제 대상 `SetSuppressed` 를 직접 호출(unit 0).
- `Assets/_Project/Editor/FirstSessionTutorialMenu.cs` — 삭제 대상 `ResetTutorialProgressAt` 호출. **Editor 어셈블리**라 깨지면 Play·테스트·빌드가 전부 막힌다(unit 1).

그 외: `GimmickPhaseView.profileSO` 고아 필드(unit 0), 진행 필드는 10개가 아니라 **12개**이며 `lobbyIntroVersion` 은 이름에 Hint/Tutorial 이 없어 grep 관용구로 놓치기 쉽다(unit 2).

「이상 없음」으로 확인된 것: 삭제 순서의 컴파일 안전성 · 씬 배선은 각 1개뿐 · 도구 계층이 컨트롤러 타입을 컴파일 타임에 참조하지 않음(계약 1 성립) · 튜토리얼 맵과 `TryGetAffordableTutorialSlot` 을 범위 밖으로 둔 판단 · `JsonUtility` 가 미지 키를 조용히 버리므로 마이그레이션 불필요.

## 파이프라인 커버리지

N/A — UI 컨트롤러·프로필 필드 제거만 다룬다. 플레이 오브젝트나 생성→렌더 경로 변경 없음.

## 범위 밖 (건드리지 않는다)

- **`MapDocument_Tutorial.asset` · `Deck_Tutorial`** — 「튜토리얼 맵」은 안내 스텝이 아니라 **맵 풀 엔트리**(10웨이브에 전 적 유형이 나오는 결정론 슬롯)다. 전용 테스트도 있다(`MapDocumentPoolDevEntriesTests` · `WaypointRoutingLiveTest`). 재설계가 이 맵을 쓸지는 그때 판단한다.
- **`DefenderSelector.TryGetAffordableTutorialSlot`** — 상태를 바꾸지 않는 **읽기 질의**이고 `BoardLimitTrayStateTest` 가 독립적으로 쓴다. 훅이 아니라 조회라 남긴다(이름의 "Tutorial" 은 재설계 때 정리 후보).
- 튜토리얼 **재설계 자체**. 이 spec 은 자리를 비우는 것까지다.

## 후속 후보

- `TutorialGuidanceStyle` 의 사라진 스텝 전용 필드(`classHintFallbackSeconds` 등) 정리 — 재설계가 무엇을 쓸지 정해진 뒤.
- 도구 계층의 이름 재정리(`Tutorial*` → 안내 도구를 뜻하는 이름) — 재설계와 함께.
- 온보딩 없는 상태의 첫 판 이탈률 관측 — 재설계의 기준선이 된다.
