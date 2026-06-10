# 4 — Handoff Summary (match-seed-unification)

## Commit

- `31b9f08` 0 MatchSeed 정적 유틸 + EditMode 6 테스트
- `5d4e2b4` 1 GameManager 가 matchSeed 소유·주입
- `4bbd59f` 2+3 BattleBridge 가 맵·웨이브를 matchSeed 에서 파생 (동일 파일이라 단일 커밋)

## Implemented

- **단일 매치 시드**: `GameManager` 가 매치당 matchSeed 1개를 소유(`debugFixedMatchSeed` 0=랜덤/≠0=고정),
  `EnsureMatchSeed()` 가 `Start()` 최상단(PrepareDraftMap 이전)에서 `battleBridge.SetMatchSeed()` 로 주입.
- **파생**: `Core/MatchSeed.cs` 정적 유틸 — `DeriveMapSeed`/`DeriveWaveSeed`/`DeriveVisualSeed` 가 salt 분리
  32-bit 믹스로 **결정론·decorrelated**. `GenerateRandom()` 만 비결정론(진입점 1회).
- **맵**: `BuildMapForBattle` 가 `DeriveMapSeed(_matchSeed)`, visualSeed 가 `DeriveVisualSeed(_matchSeed)`.
- **웨이브**: `TryInitializeGeneratedWaves` 가 `DeriveWaveSeed(_matchSeed)` 를 `WavePatternGenerator.Generate(deck, seed)`
  오버로드로 주입.
- **SO 노브 강등**: `mapSettings.EffectiveSeed/defaultSeed`(호출처 0), `deck.waveSeed/ResolveWaveSeed`(레거시
  `Generate(deck)` 전용) 라이브 경로에서 손 뗌. deprecated 주석만, 필드는 직렬화 호환 위해 유지.

## Key Files

- `Assets/_Project/Scripts/Core/MatchSeed.cs` (신규 — 파생 유틸)
- `Assets/_Project/Scripts/Core/GameManager.cs` (`debugFixedMatchSeed`, `MatchSeed`, `EnsureMatchSeed`)
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` (`_matchSeed`, `SetMatchSeed`, BuildMapForBattle, visualSeed, TryInitializeGeneratedWaves)
- `Assets/_Project/Scripts/Data/WavePatternGenerator.cs` (`Generate(deck, seedOverride)` 오버로드)
- `Assets/_Project/Scripts/Data/{MapGenerationSettings,AttackDeck}.cs` (deprecated 주석)
- `Assets/_Project/Tests/EditMode/MatchSeedTests.cs`

## Verified

- EditMode **315 total / 313 passed / 0 failed / 2 skipped**(skip 2개 기존 Ignored, 무관).
- Play(고정 matchSeed=999, reflection): `GameManager.MatchSeed=999` → `BattleBridge._matchSeed=999` →
  `_generatedMap.seed=251418039 == DeriveMapSeed(999)` 정확 일치(주입→소비 통합 체인).
- execute_code 실덱(WaveA): 같은 matchSeed → 두 번 생성 12웨이브 plan **완전 동일**(wave0 Swift x8 + Runner x6),
  matchSeed 12345 → **다른 구성**. mapSeed≠waveSeed≠visualSeed(decorrelation).
- 컴파일 green, 콘솔 에러 0. 검증용 `debugFixedMatchSeed` 는 0 으로 복원(씬 미저장, 순변화 0).

## Notes

- **결정론 함수 vs 진입점**: `Derive*` 는 순수(테스트로 보장). `GenerateRandom()` 만 시간/Unity RNG 의존 —
  매치 진입 1회만 호출한다. 다른 곳에서 호출 금지.
- **동명 주의**: `GameManager.MatchSeed`(int 프로퍼티) 와 `Wassup.Core.MatchSeed`(타입) 동명. 타입 호출은
  정규화 경로(`Wassup.Core.MatchSeed.*`)로 작성해야 모호성 없음.
- **재현 고정은 한 곳**: `GameManager.debugFixedMatchSeed`. SO 의 defaultSeed/waveSeed 로 고정하려 하지 말 것
  (라이브 경로 미사용).
- **블라스트 반경 최소**: 생성기 `int seed` 시그니처 불변. 변경은 시드 *출처* + 주입뿐.

## Follow-up

- **드래프트 시드 통합** — 아직 `DraftController.GenerateSeed()` 독립. matchSeed 파생으로 묶으면 매치 전체 재현.
- **비동기 토너먼트 시드 공유** — GameManager 소유 구조가 토대. 서버/상대 matchSeed 주입 배선은 별도 spec.
- **정식 로그 스키마에 matchSeed** — 현재 `Debug.Log` 만. BattleLogSchema 반영 후속.
- **SO 시드 필드 제거** — 지금은 deprecated 주석만. 에셋/필드 정리는 후속.
- **콘솔 matchSeed 로그 캡처** — Play 중 `[GameManager] matchSeed=` 로그가 MCP read_console 필터에 안 잡혔음
  (필드 reflection 으로 주입은 확정). 캡처 타이밍 이슈로 보이며 기능과 무관.
