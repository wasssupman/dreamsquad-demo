# 2 — 에디터: Import 창 Preset 섹션 (import + seed export)

## 목적

`Window/Wassup/Unit Stat Import` 창에 **Preset** 섹션을 추가한다. Import = 시트 → `SquadPresetCollection.asset` 재구성 + 저장. Export = 현 프리셋 → 시트 시드용 JSON.

## 변경 대상

- `Assets/_Project/Editor/UnitStatImport/UnitStatImportWindow.cs` (편집 — Preset 섹션)
- `Assets/_Project/Editor/UnitStatImport/PresetSheetExporter.cs` (신규 — SO→JSON)

## 구현

**상수** (창에 추가): `PresetTab = "Presets"` (EditorPrefs 로 오버라이드 가능하게 하되 기본 고정). 폴더 재사용 — `DefenderFolder`("Assets/_Project/Data/Defenders"), `DcFolder`("Assets/_Project/Data/Dreamcatcher"). 컬렉션은 `AssetDatabase.FindAssets("t:SquadPresetCollection")` 로 로드(단일 전제, 복수면 경고).

**Import 버튼**:
1. `SheetFetcher.Fetch(SheetEnvelopeParser.BuildSheetUrl(baseUrl, PresetTab), ...)` (기존 `_requestInFlight` 패턴 재사용).
2. `SheetEnvelopeParser.ParseSheetLogged<PresetDto>(body, transportError, PresetTab, log)`.
3. 해석기 인덱스: `UnitAssetScan.Enumerate<DefenderUnitData>(DefenderFolder)` → `UnitStatApplier.BuildIndex(..., so=>so.id)` → dict. 카드도 `<DreamcatcherCard>(DcFolder)` 동일. `Func` = `id => dict.TryGetValue(id, out var v) ? v : null`.
4. `PresetSheetApplier.Apply(rows, resolveUnit, resolveCard, SquadSave.SlotCount, collection, log)`.
5. 변경(true) 시 `EditorUtility.SetDirty(collection)` + `AssetDatabase.SaveAssets()`.
6. onDone 은 apply 예외에도 발화(기존 `RunDcImport` try/finally 규칙 준수) → `_requestInFlight` 고착 방지.

**Export 버튼** (`PresetSheetExporter.ExportToFile`):
- 컬렉션 로드 → 각 `SquadPreset` → `PresetDto{ presetName, squad = string.Join(",", units.Where(non-null).Select(u=>u.id)), dreamcatcher = join(cards...) }`.
- `PresetDto[]` 를 JSON 배열로 `EditorUtility.SaveFilePanel` 경로에 기록. (기존 `UnitStatExporter` 의 행-배열 JSON 형식과 동형; 챗봇 프롬프트는 생략 — 프리셋은 2~수행이라 붙여넣기 단순.)
- 목적: 현 프리셋(추천 A/B)을 시트 초기 시드로 뽑아 id 손전사 제거.

**주의**: 이 창은 `sheet-export-push` 도 Push 버튼/Script URL 을 추가할 파일 — 먼저 착지하는 쪽 기준으로 hunk 격리(같은 세션 아니면 커밋 청결 확인). *(조율 사항 — 코드 주석엔 남기지 않음.)*

## 완료 기준

- compile green(콘솔 CS 0).
- 창에 Preset 섹션(Import / Export 버튼) 표시.
- Import: `Presets` 탭 seed 후 실행 → `SquadPresetCollection.asset` 이 시트대로 재구성 + `git diff` 로 확인. 미해결 id 는 로그에 unmatched 로 노출.
- Export: 버튼 → JSON 파일 생성, 행이 `presetName/squad/dreamcatcher` 형식.
- (실 왕복 e2e 는 unit 4.)
