# 1. 인트로 라우팅 이전 + 덱 조합 단일화

## 목적

매치 인트로의 진입 소유권을 `GiftPhaseView` → `GimmickPhaseView` 로 옮기고, 덱 조합을 Placement 진입 단일 경로로 되돌린다. 이 unit 이 끝나면 **선물 페이즈는 실행되지 않는다**(코드는 아직 남아 있으나 아무도 구독하지 않는 죽은 코드). 라우팅·덱·enum 이 서로 물려 있어 **한 커밋으로 원자적으로** 처리한다.

## 변경 대상

- `Assets/_Project/Scripts/UI/GimmickPhaseView.cs`
- `Assets/_Project/Scripts/Core/Dreamcatcher/DreamcatcherHandController.cs`
- `Assets/_Project/Scripts/Core/GameManager.cs` (`GamePhase` enum)
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` (SerializeField 1개 + `EnterPlacementOrGift`)
- `Assets/_Project/Scenes/BattleScene.unity` (참조 재배선)
- `Assets/_Project/Tests/PlayMode/PresetCarryInTest.cs`
- `Assets/_Project/Tests/EditMode/SkillLoadoutControllerTests.cs` (L223 케이스)

## 구현

### 라우팅

`GimmickPhaseView` 에 `[SerializeField] PlacementPhaseView placementPhaseView` 를 추가하고, `OnEnable/OnDisable` 에서 `GameManager.PlacementRequested` 를 구독한다. 진입 API 는 콜백 파라미터를 버리고 `public void BeginIntro()` 로 바꾼다 — 내부에서 `_onDone = placementPhaseView.BeginPlacementPhase` 를 세팅하는 형태로, **`_onDone` 필드와 `Finish()` 의 "어떤 경로로든 정확히 한 번" 보장(`OnDisable` 포함)은 구조를 그대로 둔다**(계약 2).

스킵 3조건(기믹 미배정 / `config` 미배선 / `TutorialProgress.ShouldRunCore`)과 `SetPhase(Gimmick)` 을 `Play()` **앞**에 두는 순서 제약은 전부 현행 유지. 후자의 이유는 해당 코드 주석에 있다(튜토리얼 `Hide()` 가 말풍선을 앞질러 지우는 문제).

`sortingOrder` 기본값(20)과 그 툴팁의 "선물 연출(30)과 배치 HUD(7) 사이" 서술을 갱신한다 — 선물 레이어가 사라지므로 "배치 HUD(7) 위" 로 족하다.

`BattleBridge` 의 `_giftPhaseView` 를 `_gimmickPhaseView` 로 교체하고 `EnterPlacementOrGift()` → `EnterPlacementOrIntro()` 로 개명, 호출을 `BeginIntro()` 로 바꾼다. 이 경로는 result-screen-lobby-exit unit 0 이후 **호출처가 없는 dormant** 상태지만 되살릴 때를 위해 배선은 이관한다(기존 주석의 의도 유지).

**드래프트 경로는 만들지 않는다**(계약 1).

### 덱 조합

`DreamcatcherHandController`:

- `OnPhaseChanged` 에서 `GamePhase.Gift` 분기 삭제. `Placement` 진입 시 **항상** 덱을 구성한다.
- `BuildFallbackDeck()` → `BuildDeck()` 로 개명하고 정상 경로로 승격. 내용은 그대로(저장덱 10 + `AppendActiveCards` 2, `MatchSeed` 단일 셔플).
- 삭제: `BuildGiftDeck` · `ResolveRimGift` · `_giftKind` · `_giftDeckComposed` · `GiftDeckReady` 이벤트 · 공개 API `GiftKind`/`GiftBaseCards`/`GiftAddedCards`/`GiftFinalOrder()` · `giftConfig` SerializeField.
- 유지: `_giftBaseCards` → `_baseCards` 로 **개명만**. `LogDeck` 이 토너먼트 리포트의 "고른 덱"(baseIds)으로 계속 읽는다 — 지우면 `TournamentMatchReporter.PersistMatchDeck` 이 선물 뺀 덱 정보를 잃는다.

### enum · 직렬화 에셋

`GamePhase` 에서 `Gift` 제거. **이 enum 은 int 로 직렬화된다** — `CameraDirectionConfig.asset` 의 `CameraPhasePose.phase`(4개)와 `breathPhases`(3개)를 같은 커밋에서 옮긴다: `phase 1/3/4/5 → 1/2/3/4`, `breathPhases 1,3,4 → 1,2,3`. 옮기지 않으면 배치 포즈가 전투 포즈로, 전투가 결과로 밀리고 브리딩이 배치에서 꺼진 채 결과창에서 켜진다. `GameManager` 의 enum 주석에 이 경고를 남긴다(옛 "직렬화 없음" 주석은 폐기).

### 테스트

 `PresetCarryInTest` 의 `GamePhase.Gift` 단언과 그 위 선물 관련 주석을 정리해 `Placement` 도달만 단언한다.

`SkillLoadoutControllerTests` 의 `ResolveRimGift_Excludes_Hidden_Cards_From_Pool_And_Fallback`(L223)을 **같은 커밋에서 삭제**한다. 이 케이스는 리플렉션으로 `ResolveRimGift` private 메서드를 잡으므로 메서드가 사라지면 리플렉션이 null 을 반환해 실패한다. 그 케이스가 지키던 "숨김 카드 제외"는 림 풀에만 있던 규칙이라 대체 케이스를 만들지 않는다 — 덱빌더 쪽 `visible == 0` 필터는 unit 0 이 손대지 않고 그대로 남는다.

## 완료 기준

- [ ] compile 성공, 콘솔 에러 0
- [ ] Play: 매치 시작 → **기믹 리빌 → 배치**. 선물 연출이 뜨지 않는다
- [ ] 배치 진입 시 손패 = 저장덱 10 + Active 2 = **12장**, `LogDeck` 의 baseIds 가 저장덱 10장
- [ ] 첫 판(core 튜토리얼 pending)은 기믹 리빌도 스킵하고 배치 직행
- [ ] 기믹 미배정(BattleConfig 비활성) 시에도 배치에 정확히 한 번 도달
- [ ] PlayMode `PresetCarryInTest` 그린, EditMode 전체 그린 (테스트 어셈블리 컴파일 포함 — `SkillLoadoutControllerTests` 케이스 삭제를 빠뜨리면 어셈블리 전체가 깨진다)
