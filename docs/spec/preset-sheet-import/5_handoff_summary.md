# 5 — Handoff Summary

## Commit

- `e66be309` feat unit 1 — PresetDto + PresetSheetApplier 순수 코어 + EditMode
- `2b094631` docs — 스펙 초안 README + units 0~4 (+ sheet-export-push 경계 메모)
- `06eb866d` feat unit 2 — 에디터 Import 창 Preset 섹션 + seed export
- `8a922165` feat unit 3 — 런타임 refresher + 페이지 재빌드
- `0ae9a2c7` feat unit 4 — OutgameScene 런타임 refresher 배선
- `fed036fa` feat unit 6 — 프리셋 push (list-replace 모드; Apps Script + payload 확장)
- (docs 스탬프: `34744718`·`4c1e1153` 등)

## Implemented

- 시트 `Presets` 탭(컬럼 `presetName`/`squad`/`dreamcatcher`, `,` 구분 id) → `SquadPresetCollection` 임포트 매퍼.
- `PresetSheetApplier`: csv 분해 → resolver(Func) 로 id→SO 해석 → `presets` **통째 재구성**(시트=list-SoT). 미해결 유닛=null 슬롯(순서 보존)/카드=스킵, maxUnits 초과 drop, rows null/빈 → **no-op**(리스트 보존).
- 에디터: `Window/Wassup/Unit Stat Import` 에 **Preset 섹션** — Import(1탭 fetch→apply→save) + Export(SO→시트 시드 JSON).
- 런타임: `PresetSheetRuntimeRefresher`(IRuntimeRefresher 3번째) — 로그인 후 `Presets` fetch → 카탈로그 `ById` 해석 → collection **in-memory** 갱신(재시작 원복). `AllRuntimeRefresher.refresherSources` 에 등록.
- `PresetPageController` 매 OnEnable 재빌드(clear→build)로 런타임 refresh 반영.
- `SquadPresetCollection.asset` **포맷 무변경** — 임포터는 인스펙터 대체 authoring 경로.
- **프리셋 push**(unit 6): "Push to Sheet" 가 Presets 를 **list-replace**(전체 교체)로 반영. `replaceTab` 이 탭 없으면 자동 생성 → 수동 시드 불필요. keyed 8탭 업서트는 불변.

## Key Files

- `Assets/_Project/Scripts/Data/PresetImport/PresetDto.cs` · `PresetSheetApplier.cs` (순수 코어)
- `Assets/_Project/Tests/EditMode/PresetImport/PresetSheetApplierTests.cs` (8 케이스)
- `Assets/_Project/Editor/UnitStatImport/PresetSheetExporter.cs` · `PresetCollectionAsset.cs` · `UnitStatImportWindow.cs`(Preset 섹션)
- `Assets/_Project/Scripts/Core/PresetSheetRuntimeRefresher.cs`
- `Assets/_Project/Scripts/UI/Outgame/PresetPageController.cs`(재빌드)
- 씬: `Assets/_Project/Scenes/OutgameScene.unity`(`UnitStatRefresher` GO)

## Verified

- 컴파일 그린(콘솔 CS 0). EditMode `PresetSheetApplierTests` 8/8(TDD red→green).
- Export 실호출: 추천 A/B 2개 → seed JSON(squad 7 + dreamcatcher 10 id csv).
- Import round-trip(에디터·런타임 양쪽): export 바디 되먹임 → units 14/0/0·cards 20/0, `SquadPresetCollection.asset` **byte-identical**(무손실, 실 asset 무변경).
- 씬 배선 diff 18줄(배선 only, WIP 베이킹 0).

## Notes

- 읽기 transport = **레거시 `SheetFetcher`/`SheetEnvelopeParser`**(영구). `Wassup.SheetSync` 는 POST 전용이라 import 가 안 씀.
- 프리셋은 **keyed-upsert 모델 밖**(계약 6) — sheet-export-push 의 Push(8탭 keyed)로는 못 넣음(`KEY_CONFIG` 에 `Presets` 없어 "unknown tab" throw). seed 는 export JSON 붙여넣기.
- ⚠️ **신규 `.cs` stuck 함정**: `PresetSheetExporter.cs` 가 AssetDatabase 엔트리 stuck 으로 어셈블리 소스셋 누락 → CS0103(파일·meta 정상인데도). force reimport/RequestScriptCompilation 무효, **삭제→refresh→재생성**으로만 해결. 진단=`CompilationPipeline.GetAssemblies().sourceFiles`. (`docs/reference/lessons/` 승격 후보.)
- 되돌리지 말 것: rows null/빈 = no-op(리스트 보존), 미해결 유닛=null 슬롯(순서), export 는 null 슬롯 drop(csv 미표현), 페이지 재빌드 Destroy(렌더 전 처리라 겹침 안 보임).

## Follow-up

- **남은 유일 작업 = Code.gs 재배포 + 라이브 왕복**: (1) Apps Script 재배포("배포 관리→편집→새 버전", 저장만으론 `/exec` 안 바뀜) → (2) "Push to Sheet"(Presets 탭 자동 생성+채움) → (3) 에디터 Import Preset diff + 로그인 자동 import 로그 + 프리셋 페이지 반영 확인.
- README 후속 후보: import dry-run diff, 적용됨 하이라이트, 카드수≠deckSize 경고 강화.
