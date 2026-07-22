# 6 — 프리셋 push (list-replace 모드)

## 목적

Export Preset(SO→JSON 파일 수동 붙여넣기) 대신 **버튼 한 번으로 Presets 탭을 시트에 반영**한다.
프리셋은 list-SoT(시트가 리스트 전체의 미러)라, sheet-export-push 의 keyed-upsert(8탭)와 다른
**list-replace 모드**로 Apps Script 엔진을 확장한다. keyed 탭의 비파괴 업서트는 **한 줄도 안 건드린다**.

## 배경 (sheet-export-push 경계 승계)

- sheet-export-push 는 클라이언트 **registry 를 안 만듦**(unit 1) — payload 빌더가 탭명 키로 병합, 키는 서버 관심사. 확장 = 빌더에 탭 넘기기 + Apps Script config.
- sheet-export-push handoff 명시: keyed 8탭은 *"clear-and-insert 로 바꾸지 말 것(거부된 완전 미러)"*, 그리고 *"Presets = list-SoT, 이 8탭 keyed 모델 밖(별개 spec)"*. → 프리셋은 **별개 모드**로 붙이는 게 그 스펙의 의도.

## 변경 대상

- `docs/spec/sheet-export-push/apps-script/Code.gs` — `LIST_REPLACE_TABS` + `replaceTab` + 라우팅 (⚠️ **재배포 필요**)
- `Assets/_Project/Editor/UnitStatImport/PresetSheetExporter.cs` — `ExportToFolder` 추가(payload 빌더용)
- `Assets/_Project/Editor/UnitStatImport/SheetPushPayload.cs` — `presetTab` 파라미터 + 9번째 탭 병합
- `Assets/_Project/Editor/UnitStatImport/UnitStatImportWindow.cs` — StartPush 에 `_presetTab` 전달 + 라벨/다이얼로그/게이트
- `Assets/_Project/Editor/UnitStatImport/SheetPushClient.cs` — `replaced` 리포트 브랜치
- `Assets/_Project/Tests/EditMode/UnitStatImport/SheetPushReportTests.cs` — `replaced` 케이스

## 구현

**Apps Script (`Code.gs`)** — keyed 경로 불변, 병렬 모드 추가:
- `var LIST_REPLACE_TABS = { Presets: true };`
- `doPost` 라우팅: `LIST_REPLACE_TABS[tab] ? replaceTab(...) : upsertTab(...)`.
- `replaceTab(ss, tab, rows)`: 헤더=행 키 등장순(`_` 접두 제외) → `clearContents()` → header+rows 재작성. 키·고아 개념 없음(Unity 리스트 정확 미러 = 삭제/재정렬 반영). 반환 `{replaced: N}`.
- **가드**: `rows.length === 0` → clear 안 하고 `{replaced:0, error:"..."}` 스킵(전체 비우기 사고 방지 — keyed 탭 `공` 헤더 가드와 같은 정신). 의도적 비우기는 수동.

**`PresetSheetExporter.ExportToFolder(folder, tabName)`**: 기존 `ExportToFile` 과 행 빌드 공유(`BuildRows`). `{folder}/{tabName}.json` 기록(UnitStat/DcSheetExporter 패턴). **컬렉션 없으면 throw**(빈 파일로 시트 비우는 사고 방지).

**`SheetPushPayload.BuildCombinedJson(..., presetTab)`**: 8탭 뒤 `PresetSheetExporter.ExportToFolder(temp, presetTab)` + `AddTab(root, temp, presetTab)`.

**`UnitStatImportWindow`**: push 버튼 게이트에 `_presetTab` 비어있음 추가, StartPush 가 `_presetTab` 전달, 라벨/다이얼로그에 "Presets 전체 교체" 명시.

**`SheetPushClient.BuildReport`**: 탭 error 체크 뒤 `replaced >= 0` 이면 `"{tab}: replaced N (list-SoT 전체 교체)"` 출력.

## 완료 기준

- compile green. `SheetPushReportTests` `replaced` 케이스 포함 통과.
- payload 빌더가 `Presets` 탭 포함(execute_code 로 body 확인).
- Code.gs 문법 검증 + 배포 README 에 list-replace 모드 한 줄.
- ⚠️ **라이브 push 왕복은 사용자 재배포 후**(에디터 코드로는 재배포 불가).
- ✅ 구현 완료 2026-07-22 — 컴파일 그린, `SheetPushReportTests` 6/6(`replaced` 포함), payload 빌더 9탭(Presets 2행) 확인. Code.gs list-replace 모드 작성(keyed 8탭 불변). **남은 것: Code.gs 재배포 + 라이브 push 왕복**.
