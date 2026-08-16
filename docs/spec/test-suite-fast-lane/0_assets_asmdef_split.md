# 0 — 에셋 검증 테스트 asmdef 분리 (fast lane 확보)

## 목적

실제 프로젝트 에셋(SO·프리팹·맵)을 로드하는 EditMode 테스트를 별도 어셈블리
`Wassup.Tests.EditMode.Assets` 로 분리한다. 남는 `Wassup.Tests.EditMode` 는
**시트·에셋·콘텐츠 편집으로 깨질 수 없는 고속 코어 lane** 이 된다.
`run_tests` 에서 동작이 확인된 유일한 필터가 `assembly_names` 이므로(lessons 01),
어셈블리가 곧 실행 입도다.

## 변경 대상

- 신설: `Assets/_Project/Tests/EditModeAssets/` + `Wassup.Tests.EditMode.Assets.asmdef`
  (references 는 EditMode asmdef 사본 + `Wassup.Tests.EditMode` 추가)
- **통이동 18파일** (파일 존재 이유가 실에셋 검증 — git mv, .meta 동반):
  AuthoredTargetMask · DcApplicabilityMatrix · DcAttachRequirementWiring ·
  DirectionalVolleyIntegration · DragonBreathAuthoring · DreamcatcherCardName ·
  DreamcatcherCatalogSync · DreamstoneCatalog · LoadingRunnerRig ·
  MapDocumentPoolDevEntries · MultiGoalPoolSeparation · ParticleCurveModeConsistency ·
  SlimeSplitAuthoring · WaveConceptAuthoring · WaveKillBudgetPin · WaveSpawnLeadIn ·
  WhirlpotAuthoring 각 Tests + UnitStatImport/UnitRosterInvariantTests
- **메서드 추출 8파일** (본체는 합성 픽스처 로직 — 실에셋 메서드만 EditModeAssets 의
  주제별 새 파일로 이동, 본체는 코어 잔류):
  | 원본 | 추출 대상 |
  |---|---|
  | EnemyTierBakeTests | `EveryCatalogEnemy_HasValidSplitChain` · `LiveBossAssets_AreTaggedBoss` |
  | UnitStatImport/UnitStatImportTests | `ExportToFolder_RealAssets_WritesParseableRowFiles` |
  | UnitKitSummaryTests | `CatalogDescriptions_UseThreeFixedSections` |
  | DreamcatcherCardTextTests | 실카탈로그 스캔 2개 (44장 count pin 포함, :340 · :558) |
  | PlacementLayerTests | `DefenderCatalog_ExistingUnits…AntiAir…` · `EnemyCatalog_SkimmerIs…` |
  | WaveConceptBossTests | 라이브 덱 절 전체 (`MapDecks` 헬퍼 + 테스트 5개, :221~) |
  | Profile/ProfileStoreDefaultDeckTests | 실덱/카탈로그 로드 메서드 (:238) |
  | MapGrid/WaypointPathAuthoringTests | 실맵 로드 메서드 (:320 · :401) |
- 갱신: `docs/reference/lessons/01-unity-mcp-operation.md` §run_tests — 처방을
  두 lane 체계로 (같은 커밋에 포함해 옛 관용구 오판 방지).

## 구현

1. git mv (파일+.meta 쌍) → `EditModeAssets/`. 하위 폴더는 평탄화, 네임스페이스는 유지.
2. asmdef 작성. 추출 메서드는 새 파일로 옮기고 필요한 헬퍼는 함께 복사(공유 강제 금지).
3. `refresh_unity scope=all` → 컴파일 0 에러 확인 → 두 어셈블리 각각 `run_tests`.
4. lessons 01 처방 갱신 후 전체를 **한 커밋**으로.

## 완료 기준

- [x] 컴파일 클린 (read_console 에러 0 — InternalsVisibleTo 2건 추가로 해결)
- [x] 코어 lane: 실에셋 로드(AssetDatabase.Load/FindAssets) grep 0건, **2,233개 / 실패 0 / 26초**
- [x] 두 lane 테스트 수 합계 = 분리 전 2,394 (2,233 + 155 + DepthParallax 6 — 누락·중복 0)
- [x] Assets lane **155개 / 10초**, 실패 = 기존 기지 실패(MultiGoal 4건)만 — 분리로 새 빨강 없음
- [x] lessons 01 이 두 lane 실행법을 안내 (ObstaclePlacer 상시 실패 기록은 실측 통과로 해소 표기)

2026-08-16 구현 + 기계 검증 완료 (EditMode 실측 위 수치).
