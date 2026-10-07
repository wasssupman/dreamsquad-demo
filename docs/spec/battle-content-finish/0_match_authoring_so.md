# 0 — 판 저작을 씬 필드에서 SO 로

## 목적

**삭제 0 — 이동이다.** `BattleDriver` 가 씬에 직렬화한 「판 저작」 23 필드(48 에셋 참조)를 값 그대로 SO 로 옮긴다. 씬은 **드라이버 1 + SO 참조 몇 개**만 들고, 바깥(somnia)이 `MatchEntryInput` 을 안 주면 그 SO 가 기본값이다. 지금은 편성 하나 바꾸려면 씬을 열어야 하고, 씬 diff 에 저작과 배선이 섞인다.

## 지금 (BattleCoreScene 의 BattleDriver)

| 묶음 | 필드 | 지금 값 |
|---|---|---|
| 모드 | `_mode` | `MatchMode_KillScore3Min` |
| 편성 | `_defenders[8]` · `_dreamstones[0]` | Archer·Guardian·Bastion·ShieldShuttle·Slasher·Malphite·MachineGunner·Cannon |
| 드림캐쳐 | `_cards[12]` · `_cardCatalog` · `_activePool[6]`(SkillData) · `_activeCount=2` · `_activeCards[6]` | Card_* 10 + Active_* 2 / Skill_* 6 / Active_* 6 |
| 웨이브 | `_deck`(모드 폴백) · `_plan`(null) · `_bonus` | Deck_Duel · BonusWaveData |
| 맵 | `_mapPool`(모드 폴백) · `_stagePrefab`(풀 실패 폴백) · `_fixedMapSeed` | MapStagePool · MapStage_Duel · 0 |
| 카탈로그 | `_defenderCatalog` · `_stoneCatalog` | DefenderCatalog · DreamstoneCatalog |
| 규칙 콘텐츠 | `_hazards[3]` · `_extraBlockers[1]` · `_stackModifiers[4]` · `_imbueCaps` · `_movementTuning` · `_seasonRegistry` | Hazard_Fire/Poison/Ice · Hazard_Rock · Fire/Ice/Bleed/Fatigue · … |
| 씬 전용 | `_boardPlane` · `_tileSize` · `_seed` · `_beginOnStart` · `_maxTicksPerFrame` | 그대로 둔다 |

모드 SO 는 이미 `deck` · `plan` · `mapPool` · `gimmickPool` · `costConfig` · `deckRuleConfig` 를 든다 — 드라이버의 `_deck/_plan/_mapPool` 은 **모드가 비었을 때의 폴백**(`BattleDriver:339`).

## 변경 대상

1. **`Scripts/Data/BattleContent.cs`** (D1 — 추천안): `CreateAssetMenu("Wassup/Battle Content")`. 필드 = `defenderCatalog` · `stoneCatalog` · `cardCatalog` · `hazards[]` · `extraBlockers[]` · `stackModifiers[]` · `imbueCaps` · `movementTuning` · `seasonRegistry` · `bonus` · `activePool[]` · `activeCount`. 에셋 `Data/BattleContent.asset` 1장.
2. **`Scripts/Data/DefaultLoadout.cs`** (D2): `defenders[]` · `dreamstones[]` · `deck`(`DreamcatcherDeck` 재사용 — 참조 0 인 `DreamcatcherDeck_Default.asset` 을 지금 `_cards[12]` 로 채운다) · `activeCards[]`. 에셋 `Data/DefaultLoadout.asset`.
3. **`BattleDriver`**: 위 필드 23 → `[SerializeField] BattleContent _content; DefaultLoadout _loadout; MatchModeData _mode; MapStagePool _mapPool; MapStage _stagePrefab; int _fixedMapSeed;` 읽는 자리는 전부 `_content.x` · `_loadout.x` 로. `Mode`·`BonusAuthoring` 같은 공개 getter 는 서명 유지. `MatchEntryPlan.Defenders == null` → `_loadout.defenders` 폴백(지금 `_defenders` 와 같은 자리).
4. **`SeasonRuntime` 삭제**: `BattleDriver:324-325, 408` 이 `Bind` 직후 `Active` 를 읽는 것뿐 → `_content.seasonRegistry.activeSeason` 직접. `LiveDefinitionSmokeTests:78` 주석 갱신.
5. **`ModeSelection.Lobby` → `External`** · `FromLobby` → `FromExternal` · `MatchEntryPlan.FromLobby` → `FromOutside`(로비는 이 리포에 없다). `CoreScenePlayMenu:63` · `BattleDriver:316,373,416` · `CoreMatchEntryCarryTests:63`.
6. **씬**: 일회용 MenuItem 이 드라이버의 옛 필드 값을 읽어 두 SO 에 쓰고(`SaveAssetIfDirty`) 씬을 저장 → 옛 직렬화 줄은 필드 삭제로 고아가 되어 저장 때 사라진다. 순서: SO 생성 → 메뉴 실행(값 복사) → 필드 삭제 커밋.
7. **테스트**: `CoreSceneFixture` 는 드라이버 경유라 무변. `MatchEntryBuildTests` · `LiveDefinitionSmokeTests` 가 드라이버 필드를 리플렉션으로 읽으면 SO 로 바꾼다. 새 테스트 1: `BattleContent.asset` · `DefaultLoadout.asset` 의 필수 참조가 null 이 아니다(EditMode.Assets).
8. `MatchViewAssets`(뷰 프리팹 묶음)는 이미 씬 컴포넌트 참조 묶음이라 그대로.

## 완료 기준

- [x] `BattleDriver` 의 `[SerializeField]` 가 SO/프리팹 참조 5 + 씬 전용 6(`_boardPlane` · `_tileSize` · `_seed` · `_fixedMapSeed` · `_beginOnStart` · `_maxTicksPerFrame`) 뿐 · 씬의 BattleDriver 블록이 그만큼만 남는다
- [x] `rg "SeasonRuntime|FromLobby|\.Lobby\b" Assets/_Project` → 0
- [ ] 에디터 컴파일 0 · Play 한 판: 같은 편성 8 · 같은 덱 12(액티브 2 굴림 포함) · 같은 맵 풀
- [ ] 커밋 2(SO 타입 + 메뉴 · 씬 + 필드 삭제)

## 구현 결과 (2026-10-07) — `c1289bc00` + 씬 커밋

- `Scripts/Data/BattleContent.cs`(슬롯 14 + `ActiveMapTheme` + `runtimeMaterials`) · `DefaultLoadout.cs`(유닛 · 돌 · `DreamcatcherDeck` + `DeckCards`). 에셋 `Data/BattleContent.asset` · `Data/DefaultLoadout.asset` 은 씬의 값을 그대로 옮겨 손으로 썼다(GUID 48 동일). `DreamcatcherDeck_Default` 는 씬의 개발용 덱 12 로 채움(옛 10 장은 사라진 카드 포함).
- `BattleDriver`: 필드 23 → `_content` · `_loadout`(+ 폴백 `_deck/_plan/_mapPool/_stagePrefab/_fixedMapSeed` · 씬 전용 6). 둘 중 하나라도 비면 판을 짓지 않고 에러. `Content` · `Loadout` 공개 getter. `Awake` 가 `RuntimeMaterialFactory.Configure`(단위 4).
- `SeasonRuntime` 삭제 · `ModeSelection.Lobby → External` · `FromLobby → FromExternal` · `MatchEntryPlan.FromOutside` · `ResolveMode(external)`.
- 테스트: 씬 YAML 을 읽던 둘은 `_content/_loadout` guid 를 풀어 SO 를 읽는다. PlayMode 덱/편성 덮어쓰기 6곳은 `CoreSceneFixture.OverrideDeck/OverrideDefenders`(메모리 사본). 카탈로그는 `driver.Content`.
- 씬: 일회용 메뉴 `Wassup/Battle Content Finish/Wire Driver SOs (one-off)` 가 참조 2 를 꽂고 저장 — 옛 필드 줄은 저장 때 떨어진다. 메뉴 스크립트는 씬 커밋 뒤 삭제.
