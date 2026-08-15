# 4. Handoff Summary

## Commit

- `4c09d6b1` feat(gift-phase-removal): unit 0 — 무의식 카드 일반 덱 승격
- (unit 1~3 통합 커밋 — 해시는 커밋 후 기재)

## Implemented

- **선물 페이즈 제거.** `GamePhase.Gift`·`GiftPhaseView`(838줄)·`GiftCardWidget`·`GiftPhaseLayout`·`GiftDeckComposer`·`GiftConfig`(+`GiftKind`)·`GiftConfig_Default.asset` 삭제.
- **`GimmickPhaseView` 가 매치 인트로 진입 소유자.** `GameManager.PlacementRequested` 를 직접 구독하고, `BeginIntro()` 로 진입해 연출/스킵 어느 경로로든 스스로 `BeginPlacementPhase()` 를 호출한다. `_onDone` 의 "정확히 한 번" 보장 구조는 그대로.
- **덱 조합은 Placement 진입 단일 경로.** `BuildFallbackDeck` → `BuildDeck` 승격, 매 배치 진입마다 재구성. 저장덱 10 + Active 2 = 12장, `MatchSeed` 단일 셔플.
- **루시드/림 분기 폐지.** 추가 2장의 출처는 `SkillLoadoutController.Picked` 뿐.
- **무의식 카드 일반 덱 승격**(unit 0) — 덱 페이지/프리셋의 `category == Subconscious` 제외 제거.
- **선물 튜토리얼 챕터 제거.** `FirstSessionTutorialController.Gift.cs` 삭제, `TutorialProgress` 의 Gift API 4개 제거.
- **씬 배선**: `GiftPhaseView` GameObject 삭제, `BattleBridge._gimmickPhaseView` 교체, `GimmickPhaseView.placementPhaseView` 신규 배선.

## Key Files

- `Assets/_Project/Scripts/UI/GimmickPhaseView.cs` — 진입 소유자(`BeginIntro`)
- `Assets/_Project/Scripts/Core/Dreamcatcher/DreamcatcherHandController.cs` — `BuildDeck`
- `Assets/_Project/Scripts/Core/GameManager.cs` — `GamePhase` enum + **직렬화 경고 주석**
- `Assets/_Project/Data/Camera/CameraDirectionConfig.asset` — enum 값 마이그레이션
- `Assets/_Project/Scenes/BattleScene.unity` — 배선

## Verified

- Unity 컴파일 콘솔 에러 0 / 경고 0 (missing script 없음)
- EditMode 2393개 — 실패 4건은 **전부 기존 맵 복도 폭 단언**(`MultiGoalPoolSeparationTests`, map-rework 소관). 이번 변경이 만든 실패 0. 선물 테스트 25개가 제거되어 총계가 2418→2393.
- 씬 리로드 후 `GimmickPhaseView.placementPhaseView` → PlacementPhaseView, `BattleBridge._gimmickPhaseView` → GimmickPhaseView 확인
- **Play smoke 는 미실행** — 원격 세션이라 사용자가 pull 받아 확인한다.

## Notes (되돌리면 안 되는 것)

- ⚠ **`GamePhase` 는 int 로 직렬화된다.** `CameraDirectionConfig.asset` 의 `CameraPhasePose.phase`(4개) + `breathPhases`(3개). Gift 제거로 `1/3/4/5 → 1/2/3/4`, `1,3,4 → 1,2,3` 으로 **같은 커밋에서 옮겼다**. 계획 단계의 "직렬화 참조 없음" 판단은 틀렸었다(네임스페이스 접두사 때문에 grep 이 놓침). 앞으로 이 enum 을 건드리면 반드시 그 에셋을 함께 본다 — `GameManager` 주석에 경고를 남겨 뒀다.
- **units 1~3 은 한 커밋**이다. `GiftPhaseView` 와 튜토리얼 파일이 제거 대상 API 를 직접 참조해서 어떤 순서로 나눠도 중간 커밋이 컴파일되지 않는다.
- **희생계약(`sub_incubus_pact`)은 카탈로그 미등록**이라 unit 0 이후에도 덱 페이지에 뜨지 않는다 — 2026-08-08 사용자 결정(유출 허용치가 goal-tower-siege 이후 무비용)이며 이번 스펙이 뒤집지 않았다. 실제 노출은 5장.
- 재시작 경로(`BattleBridge.EnterPlacementOrIntro`)는 여전히 **dormant**(호출처 없음). 배선만 이관했다.
- 첫 판 기믹 리빌 스킵(`ShouldRunCore`)은 현행 유지.
- `PlayerProfile.giftTutorialVersion` 필드와 `ResetAll`/`ResetAllInJson` 의 초기화는 **의도적으로 남겼다**(하위호환 + 기존 세이브 정리).

## Follow-up

- **Play smoke**: 시작 → 기믹 리빌 → 배치, 손패 12장, 재시작 동일 순서, 덱 페이지 무의식 5장 추가 가능 + Squad 캡 거절.
- **카메라 포즈 육안 확인**: 배치/전투 카메라 포즈와 브리딩이 의도대로인지 — enum 마이그레이션이 맞았는지 눈으로 보는 것이 가장 확실하다.
- **PlayMode 테스트 결과** 확인(작성 시점 실행 중).
- 무의식 카드 밸런스 재조정(README 후속 후보).
