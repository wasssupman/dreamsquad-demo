# 7. CostConfig 탭 — 코스트 경제 SO 시트 왕복

## 목적

전투 코스트 경제(`CostConfig`: 시작/최대 코스트, 초당 생산, 배치 페이즈 길이)를 **신규 `CostConfig` 탭**으로 push 하고, 시트에서 조정한 값을 다시 import 한다. `DcConfig` 탭(= config SO 를 `id` 키 행으로 주고받는 flat 탭)과 같은 방식이다.

## 변경 대상

- `Assets/_Project/Scripts/Data/CostConfig.cs` — `id` 필드 추가(행 키)
- `Assets/_Project/Data/Config/DefaultCostConfig.asset` — `id: cost_default` 백필
- `Assets/_Project/Scripts/Data/StatImport/CostConfigDto.cs` — **신규**. export/import 공용 행 타입
- `Assets/_Project/Scripts/Data/StatImport/CostConfigSheetApplier.cs` — **신규**. 에디터/런타임 공용 apply 코어
- `Assets/_Project/Scripts/Core/CostConfigRuntimeRefresher.cs` — **신규**. dev 버튼용 `IRuntimeRefresher`
- `Assets/_Project/Editor/UnitStatImport/CostConfigSheetExporter.cs` — **신규**. SO → `{탭명}.json`
- `Assets/_Project/Editor/UnitStatImport/SheetPushPayload.cs` — 병합 payload 에 탭 추가
- `Assets/_Project/Editor/UnitStatImport/UnitStatImportWindow.cs` — 탭명 필드(EditorPrefs) + Import/Export 버튼 + Push 포함
- `Assets/_Project/Tests/EditMode/UnitStatImport/CostConfigSheetTests.cs` — **신규**
- `docs/spec/sheet-export-push/apps-script/Code.gs` — `KEY_CONFIG` 에 `CostConfig: ['id']`
- `Assets/_Project/Scenes/OutgameScene.unity` — dev 트레이 `IMPORT COST` 버튼 + refresher 배선

## 구현

**행 키**: `id`. `CostConfig` 에는 원래 id 가 없어 추가한다 — `DeckRuleConfig`/`AwakeningConfig` 가 `dreamcatcher-sheet-sync` unit 2 에서 같은 이유로 했던 것과 동일. Unity 는 필드명으로 역직렬화하므로 기존 에셋의 튜닝값은 보존되고, `cost_default` 를 백필한다. id 가 비면 서버는 "키 결측 행"으로 스킵하고 클라이언트 applier 는 매칭 실패로 로그만 남긴다.

**공용 타입**: `CostConfigDto` 의 nullable 필드가 blank=keep 을 양방향으로 구현한다 — export 는 null 필드를 파일에서 생략하고(`NullValueHandling.Ignore`), import 는 null 필드를 건너뛴다(`UnitStatFieldMapper.ApplyNonNullFields`). 필드명은 SO 와 1:1 이라 reflection 이 이름으로 읽고/쓴다.

**apply 코어 공유**: `CostConfigSheetApplier.Apply(rows, byId, onApplied, log)` 를 에디터 import(AssetDatabase 인덱스 + `SaveAssetIfDirty`)와 런타임 refresher(인스펙터 참조 1개 + in-memory)가 함께 쓴다. 중복 id 행은 첫 행만 적용, 미매칭 id 는 로그만 — `DcSheetApplier` 의 flat 탭과 같은 규칙.

**반영 시점**: `GameManager` 는 battle-scoped 라 Awake 에서 `CostRuntime.Configure` 를 다시 호출한다. 따라서 로비 dev 버튼으로 갱신한 값은 **다음 전투부터** 적용된다(형제 refresher 와 동일).

**dev 버튼**: `CostConfigRuntimeRefresher` 는 `IRuntimeRefresher` 4번째 구현체. OutgameScene 의 `UnitStatRefresher` 호스트에 붙고 `AllRuntimeRefresher.refresherSources` 에도 들어가, 전용 `IMPORT COST` 버튼과 기존 `IMPORT ALL`·로그인 자동 import 양쪽에서 구동된다(`PresetSheetRuntimeRefresher` 와 같은 편입 방식).

**서버**: `KEY_CONFIG` 에 한 줄. 탭이 없으면 `upsertTab` 이 `insertSheet` 로 만든다(첫 push 가 곧 탭 생성).

## 완료 기준

- [x] 컴파일 통과 · EditMode 전체 통과(2026-07-23: 1256개 중 실패 0, 스킵 2는 기존 Ignored. 신규 `CostConfigSheetTests` 6개가 `Wassup.Tests.EditMode` 에 등록된 것까지 확인)
- [x] Export 산출물이 `[{"id":"cost_default","startingCost":10,"maxCost":10,"regenPerSec":0.35,"placementPhaseDuration":30.0}]` 형태
- [x] `DefaultCostConfig.asset` 의 기존 4개 값이 id 추가 후에도 그대로(10/10/0.35/30)
- [x] push payload 조립에 탭이 실림 — 10탭 각 행수 정상(`CostConfig 1`), 기존 9탭 회귀 없음
- [ ] Push 후 시트에 `CostConfig` 탭이 생기고 1행 업서트 (**Apps Script 재배포 선행 필요**)
- [ ] 로비 dev 트레이에 `IMPORT COST` 버튼이 뜨고, 눌러 시트값이 반영된 뒤 시작한 전투의 코스트가 달라짐

## 후속 후보

- **`BattleConfig`/`ScoreRules` 등 나머지 전역 튜닝 SO** · 같은 flat-config 탭 패턴으로 확장 가능. 요청 시.
- **flat 탭 apply 의 공통화** · `DcSheetApplier.ApplyFlat` 과 `CostConfigSheetApplier` 는 같은 규칙(id 매칭·중복 스킵·미매칭 로그)을 각자 구현한다. 3번째 flat 탭이 생기면 generic 헬퍼로 추출할 것. **드리프트가 이미 시작됐다**: `CostConfigSheetApplier` 는 `byId`/`so` 의 null 을 방어하지만 `ApplyFlat` 은 하지 않는다(더 강한 쪽이라 버그는 아니다). 추출 시 어느 쪽 규칙을 정본으로 삼을지부터 정할 것.
