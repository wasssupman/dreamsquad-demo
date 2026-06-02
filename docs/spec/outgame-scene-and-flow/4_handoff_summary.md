# 4 — Handoff Summary (A 완료)

A(`outgame-scene-and-flow`) 구현 종료. 다음 작업자(B/C/D)를 위한 인계 지도. 최신 계약은 README + 번호 문서 우선.

## Commit

- 0 `9c99565` — PlayerProfile 데이터 모델 + 유닛 id + 카탈로그
- 1 `ca2ed2a` — ProfileStore JSON 영속 + 테스트
- 2 `7c63366` — OutgameScene + 메인 메뉴
- 3 `122e986` — 씬 전환 + GameManager 비영속화 + smoke

## Implemented

- 부팅 씬 = `OutgameScene`(빌드 index 0). `BattleScene`(1) 은 게임 시작 버튼으로 로드.
- 메인 메뉴 3버튼(START GAME / SQUAD / DREAMCATCHER) + 스쿼드·드림캐쳐 placeholder 패널(상호배타 토글).
- 영속: `ProfileStore`(static) ↔ `persistentDataPath/profile.json`. 기본 프로필 = 카탈로그 전체 `ownedUnitIds`(15), schemaVersion 1.
- 씬 간 캐리어: `PlayerProfileSO` 에셋(`Assets/_Project/Data/PlayerProfile.asset`). OutgameMenuController.Awake 에서 로드·주입.
- 유닛 안정 ID: `DefenderUnitData.id` 15개 백필(`scout`/`ranger`/…), `DefenderCatalog`(id→SO).
- GameManager 비영속(`DontDestroyOnLoad` 제거) — 복귀 시 파기, 재진입 시 1개(누수 없음).
- BattleScene `MenuReturnCanvas`(MENU 버튼, sortingOrder 1000) → 언제든 OutgameScene 복귀.

## Key Files

- `Assets/_Project/Scripts/Core/Profile/{PlayerProfile,PlayerProfileSO,ProfileStore}.cs`
- `Assets/_Project/Scripts/Core/SceneNames.cs`
- `Assets/_Project/Scripts/Data/DefenderCatalog.cs`, `DefenderUnitData.cs`(id 필드)
- `Assets/_Project/Scripts/UI/Outgame/{OutgameMenuController,ReturnToMenuButton}.cs`
- `Assets/_Project/Scenes/OutgameScene.unity`, `BattleScene.unity`(MenuReturnCanvas)
- `Assets/_Project/Data/{DefenderCatalog,PlayerProfile}.asset`

## Verified

- compile clean (read_console 0 error).
- EditMode `ProfileStoreTests` 3/3.
- PlayMode 3/3 (`OutgameFlowSmokeTest` 포함) — Outgame→Battle→메인 왕복.
- Play(MCP): profile.json 생성(15 units), 패널 토글, GameManager 비영속/무누수.

## Notes

- **되돌리지 말 것**: GameManager 비영속(2-씬 전제). `PlayerProfileSO.selectedSquadId == ""/null` 이면 BattleScene 은 **기존 드래프트 폴백** — 의도된 A 비파괴. 드래프트 제거는 C.
- UI 라벨 **영문**: 프로젝트에 한글 TMP 폰트 부재(LiberationSans only). 한글화는 후속 로컬라이즈.
- `SquadSave`/`DeckSave` 는 **빈 stub** — B/C 가 필드 확장. 지금 과설계 금지.
- 기존 결함(미수정, 범위 밖): `BattleScene/DraftView` 누락 스크립트(slot 1) → 로드 시 에러 로그. smoke 는 `LogAssert.ignoreFailingMessages` 로 허용.

## Follow-up (다음 후보)

- **B `squad-loadout`**: 유닛 class 라벨(ranger/guardian/bruiser) 추가 → 7슬롯 편성 UI(스쿼드 패널 내용 채우기) → `PlayerProfile.squads` 저장 → `selectedSquadId` 설정 → BattleScene 반입(7+3 중 랜덤7). GameManager.Start 에 squad 분기 추가.
- **C `ingame-dreamcatcher`**: 준비단계 드래프트 단계 제거 → 드림캐쳐 선택(첫 배치 3중1 + 5웨이브 3중1) + `StatModifierApplyEvent` 채널로 효과 적용.
- **D `dreamcatcher-deck-builder`**: 드림캐쳐 패널에 10장 세이브덱 빌더 → `PlayerProfile.dreamcatcherDecks`.
- 한글 TMP 폰트 도입(로컬라이즈).
