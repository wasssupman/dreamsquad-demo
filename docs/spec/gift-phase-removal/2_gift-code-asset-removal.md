# 2. 선물 코드 · 에셋 · 씬 오브젝트 삭제

## 목적

unit 1 에서 죽은 코드가 된 선물 페이즈 일체를 제거한다. Play 검증이 끝난 뒤 지우는 순서라 되돌릴 여지를 한 단계 남겨 둔다.

## 변경 대상

삭제 (`.meta` 짝 반드시 동반):

| 종류 | 경로 |
|---|---|
| 코드 | `Assets/_Project/Scripts/UI/Dreamcatcher/GiftPhaseView.cs` (838줄) |
| 코드 | `Assets/_Project/Scripts/UI/Dreamcatcher/GiftCardWidget.cs` |
| 코드 | `Assets/_Project/Scripts/UI/Dreamcatcher/GiftPhaseLayout.cs` |
| 코드 | `Assets/_Project/Scripts/Core/Dreamcatcher/GiftDeckComposer.cs` |
| 코드 | `Assets/_Project/Scripts/Data/Dreamcatcher/GiftConfig.cs` (`GiftKind` enum 포함) |
| 에셋 | `Assets/_Project/Data/Dreamcatcher/GiftConfig_Default.asset` |
| 테스트 | `Assets/_Project/Tests/EditMode/GiftDeckComposerTests.cs` |
| 테스트 | `Assets/_Project/Tests/EditMode/GiftPhaseLayoutTests.cs` |

테스트 케이스 편집 (파일 삭제가 아니라 케이스 제거):

- `Assets/_Project/Tests/EditMode/DreamcatcherCatalogSyncTests.cs` — `RimGift_LiveCatalogPool_PicksTwoDistinctSubconscious`(L175 부근)가 `GiftDeckComposer.PickRim` 을 직접 호출한다. **같은 커밋에서 이 케이스를 삭제**하지 않으면 `GiftDeckComposer.cs` 삭제와 동시에 EditMode 어셈블리 전체가 컴파일 실패한다. 같은 파일의 `SubconsciousPool_MatchesCursedRoster`(6장 로스터 단언)와 카드별 계약 케이스는 **유지** — 카테고리 값과 카드 내용은 이번 스펙에서 바뀌지 않는다.

씬/참조 정리:

- `Assets/_Project/Scenes/BattleScene.unity` — `GiftPhaseView` GameObject(L3439 부근)와 그 컴포넌트 제거
- `BattleBridge` 인스펙터에 unit 1 에서 교체한 `_gimmickPhaseView` 가 실제로 물려 있는지 확인
- `GimmickPhaseView` 의 `placementPhaseView` 참조 배선 확인

## 구현

1. 파일 삭제 전 `GiftKind`·`GiftConfig`·`GiftPhaseLayout` 잔여 참조를 전수 검색해 0 인지 확인한다. unit 1·3 을 마쳤다면 남는 참조가 없어야 한다.
2. 씬에서 오브젝트를 지운 뒤 저장한다. **컴포넌트만 지우고 GameObject 를 남기지 않는다** — missing script 잔재가 생긴다.
3. `.meta` 를 같이 스테이징한다. 경로 지정 `git add` 시 `.meta` 가 빠지면 다른 머신에서 GUID 가 재생성되어 씬 참조가 깨진다.
4. `csproj` 는 파일을 명시 나열하므로, Unity 재임포트 없이 `dotnet build` 로 검증하면 삭제된 파일에 대해 CS2001 오탐이 날 수 있다. Unity 컴파일을 정본으로 본다.

## 완료 기준

- [ ] `Gift` 문자열 전수 검색 시 남는 것이 `PlayerProfile.giftTutorialVersion` 계열(계약 7)뿐
- [ ] Unity 컴파일 성공, 콘솔 에러 0 (missing script 경고 포함 0)
- [ ] 씬 로드 후 `GiftPhaseView` GameObject 부재, `BattleBridge`/`GimmickPhaseView` 참조 정상
- [ ] Play: 매치 시작 → 기믹 리빌 → 배치, 손패 12장 (unit 1 검증 재현)
- [ ] EditMode/PlayMode 전체 그린 — `DreamcatcherCatalogSyncTests` 는 `RimGift_*` 케이스만 빠지고 나머지 로스터/카드 계약 케이스는 여전히 그린
- [ ] `git status` 에서 삭제된 `.cs` 와 `.cs.meta` 가 짝으로 스테이징됨
