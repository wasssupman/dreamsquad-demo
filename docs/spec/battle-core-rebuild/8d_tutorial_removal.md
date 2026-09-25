# 8d — 튜토리얼 전량 제거 (조각 E · 8a2 뒤 · 9 앞)

> **사용자 결정 ④(2026-09-25)**: 「튜토리얼 모두 제거하자」 → 범위 질문에 **「둘 다」**. 전투 첫 판 온보딩과 로비 로드아웃 온보딩을 전부 지운다. 결정 ①(온보딩을 새 씬으로 이전, 8b 에서 구현)은 이 결정으로 **대체**된다. 플레이어 규칙 변화 = **새 계정은 첫 판부터 토너먼트 참가 신청이 나간다**(완료 플래그 게이트 소멸).

## 목적

튜토리얼이라는 기능을 게임에서 없앤다 — 새 씬·로비·프로필·설정 자산·테스트·씬 오브젝트까지. 옛 씬에 묶인 것은 unit 9 가 지우도록 퇴역 집합에 넣는다. 끝나면 `grep -rni 'tutorial\|온보딩\|FirstRun'` 이 **살아남는 코드**(옛 씬 밖)에서 0 이다.

## 변경 대상 (2026-09-25 실측 인벤토리)

| 묶음 | 지운다 | 비고 |
|---|---|---|
| 새 씬 온보딩 | `BattleCoreUnity/Hud/CoreFirstRunGuide.cs`(+.meta) · `BattleCoreScene.unity` 의 가이드 오브젝트·배선 2곳 · `MatchEntry` 의 `MatchEntryKind.Onboarding`·`OnboardingPlan`·`OnboardingConfig`·`ShouldRun` 분기(`:135~140`) · `BattleDriver` 의 온보딩 필드 | 진입 종류는 `Squad`·`TestMode` 만 남는다 |
| 코어 온보딩 칸 | `MatchDefinition.PinnedHandFront`·`BonusPullSuppressed`(8b G11·G12) + `BattleMatch`·`WaveScheduler`·`MatchDefinitionBuilder` 의 배선 · 커맨드 `DamageMaxHealthRatio`(26, `Command.cs`·`CommandPhase.cs`) — **생산자가 온보딩뿐**이었다 | 기본값이라 `configHash` 입력 밖 → 골든 무변. 커맨드 번호 26 은 비워 두지 않고 지운다(코어 커맨드는 append-only 계약이 없다 — 있으면 spec 에 근거를 적고 남긴다). `MatchEntryInputTests`·`CoreMatchEntryTests` 의 온보딩 케이스 삭제 |
| 프로필·로비 게이트 | `PlayerProfile.firstRunTutorialDone` + 배웅 안내 플래그(first-run-tutorial unit 10) · `OutgameMenuController` 의 참가 신청 생략 게이트(`:280~292`)와 배웅 안내 · 개발 트레이 RESET TUTORIAL · `LobbyTutorialStep` | JSON 저장은 모르는 필드를 무시하므로 옛 프로필 파일이 깨지지 않는다 — 테스트로 증언 |
| 로비 온보딩 | `UI/Outgame/Tutorial/`(4 파일) · `OutgameScene.unity` 의 오버레이 오브젝트 8 · `DcInspectPanelView` 의 온보딩 구멍(`ActionRect` 등 온보딩 전용 읽기 창) · `OutgameTutorialDimLayoutTests` | 로비 흐름은 「차단 없음」이 된다 |
| 설정·자산 | `Data/FirstRunTutorialConfig.cs`(+asset) · `TutorialGuidanceStyle_Default.asset` · `WavePlan_FirstRunTutorial.asset` · `WavePlan_Tutorial.asset` · `Deck_Tutorial.asset` · `UI/Tutorial/TutorialGuidance{View,Style}.cs` | ⚠ 옛 `GameManager`·`GimmickRevealConfig`·옛 씬이 `FirstRunTutorialConfig` 를 부른다 — **옛 씬 경로가 unit 9 까지 컴파일돼야** 하므로, 옛 소비자가 남는 타입은 8d 에서 지우지 말고 `retire-set.md` 에 넣는다(구현 3) |
| 옛 씬 온보딩 | `UI/Tutorial/FirstRunTutorialController.cs` · `BattleScene.unity` 오브젝트 7 · 옛 `GameManager` 의 온보딩 분기 | **퇴역 집합으로**(unit 9). 8d 는 손대지 않는다 |
| 테스트 | `FirstRunTutorialGateTests` · `TutorialDragGuidanceTests` · `TutorialGuidanceCopyTests` · `OutgameTutorialDimLayoutTests` · `FirstRunTutorialWavePlanTests` · `CoreMatchEntryTests` 온보딩 3건 | 삭제. 대신 신설 2: 「새 계정 첫 판에 참가 신청이 나간다」 · 「옛 프로필 JSON(플래그 있음)이 그대로 열린다」 |
| 장부·문서 | `rule-holders` G11·G12 → 「삭제(결정 ④)」 · `rules.md` 온보딩 행 · `retire-set.md` 에 옛 온보딩 파일 추가(총계 갱신) · `docs/spec/README.md` 에 `first-run-tutorial`·`first-session-tutorial`·`outgame-tutorial`·`tutorial-*` 5 폴더 「은퇴(결정 ④)」 한 줄씩 · `docs/reference/ingame-flow.md` 의 튜토리얼 언급 | spec 폴더 자체는 이력으로 남긴다(삭제 금지) |

## 구현

1. **새 씬·로비·프로필부터** — 컴파일이 깨지는 순서로 지우지 말고, 소비자(씬 배선·테스트) → 생산자(스크립트·자산) 순. 씬은 에디터로 오브젝트를 지우고 저장(YAML 손편집 금지 — 열린 씬 Reload 모달 함정). 씬 diff 는 지운 오브젝트 외 0.
2. **코어 칸·커맨드 제거 뒤** 헤드리스 3종 + 골든 `Verify` 무변. `HandDeck.Begin(pinnedFront)`·`WaveScheduler` 의 억제 분기는 온보딩이 유일한 생산자였음을 grep 으로 확인하고 지운다. 옛 씬의 `SetBonusPullSuppressed`(옛 브리지·옛 테스트)는 옛 것이라 그대로 둔다.
3. **옛 소비자가 남는 타입**(`FirstRunTutorialConfig`·`TutorialGuidance*` 가 옛 `GameManager`·`BattleScene` 에서 불리면) 은 지우지 않고 `retire-set.md` 의 `retire` 블록에 추가하고 **새 코드에서의 참조만 0** 으로 만든다. `Retire.Check` 로 「지워도 남는 코드가 안 부른다」를 증명. 총계 줄 갱신.
4. **참가 신청**: 게이트를 지운 뒤 `OutgameMenuController` 가 새 계정에서도 `BeginMatch`/참가 신청을 내는지 PlayMode 코어(또는 EditMode) 테스트 1. 8b 의 「온보딩 완주 → 다음 판 참가 신청」 테스트는 삭제.
5. **프로필 호환**: 옛 프로필 JSON 에 `firstRunTutorialDone`·배웅 플래그가 있어도 로드가 성공하고 다른 필드가 보존되는 EditMode 테스트 1(`JsonUtility` 무시 동작 증언).
6. `check_ledgers.py` 기본·`--owners`·`--retire-assets` exit 0 · 가지치기 뒤 `Retire.Check` 0.

## 이식 제외

| 옛 기능 | 처분 | 근거 |
|---|---|---|
| 전투 첫 판 온보딩(B1~B5 · 60초 판 · 첫 손패 고정 · 보너스 억제 · 첫 유닛 체력 낮추기) | 제거 | 사용자 결정 ④ |
| 로비 로드아웃 온보딩(4스텝 차단 오버레이 · 배웅 안내) | 제거 | 사용자 결정 ④ 「둘 다」 |
| 완료 플래그 게이트(미완주 계정은 참가 신청 생략) | 제거 → 첫 판부터 참가 | 결정 ④ 의 귀결(플레이어 규칙 변화, 사용자 확인됨) |

## 완료 기준

- [ ] 살아남는 코드(옛 씬 경로·`retire-set` 밖)에서 `grep -rni 'tutorial\|온보딩\|FirstRun\|Onboarding'` = 0(주석 이력 줄 제외 — 있으면 「결정 ④로 제거」 한 줄로 정리).
- [ ] `OutgameScene`·`BattleCoreScene` 에 튜토리얼 오브젝트·배선 0 · 씬 diff 는 삭제만.
- [ ] 새 계정 첫 판 참가 신청 테스트 초록 · 옛 프로필 JSON 호환 테스트 초록.
- [ ] 헤드리스 3종 · 골든 무변(`Verify`) · `check_ledgers.py` 3종 exit 0 · `Retire.Check` 0 · EditMode 선행 2 외 빨강 0 · PlayMode 코어 초록(온보딩 3건 삭제만큼 줄어든다) · 옛 부분집합 38/38(리로드 직후·코어 lane 앞) · CLI 63/63.
- [ ] `core-reviewer` APPROVE → 플레이 4차에 합류(로비에 차단 오버레이가 없고, 새 씬에 안내가 없다).
