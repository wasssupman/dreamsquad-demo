# 2. 8탭 병합 payload 빌더

## 목적

전 8탭(유닛 2탭 + DC 6탭)을 하나의 POST 바디(`{ "<탭명>": [rows], ... }`)로 병합한다. **기존 exporter 를 한 줄도 안 고친다** — 검증된 `ExportToFolder` 를 임시 폴더에 그대로 돌린 뒤 8개 JSON 파일을 다시 읽어 탭명 키로 합친다. `DcSheetExporter.ExportCombinedFile` 이 이미 쓰는 패턴(수집 로직 중복 0).

## 변경 대상

- 신규: `Assets/_Project/Editor/UnitStatImport/SheetPushPayload.cs`.
- **불변**: `UnitStatExporter.cs`, `DcSheetExporter.cs`(그대로 호출만).

## 구현

- `SheetPushPayload.BuildCombinedJson(defenderSheet, enemySheet, defenderFolder, enemyFolder, dcTabs, dcFolder, skillFolder) → string`:
  1. 임시 폴더 생성(`Path.GetTempPath()` + GUID).
  2. `UnitStatExporter.ExportToFolder(temp, defenderSheet, enemySheet, defenderFolder, enemyFolder)` — `{defenderSheet}.json`/`{enemySheet}.json` 산출.
  3. `DcSheetExporter.ExportToFolder(temp, dcTabs, dcFolder, skillFolder)` — DC 6탭 `{tab}.json` 산출.
  4. `[defenderSheet, enemySheet] + dcTabs` 순서로 각 `{temp}/{tab}.json` 을 `JArray.Parse` 해 `JObject[tab]` 에 담는다.
  5. `finally` 로 임시 폴더 삭제.
  6. `root.ToString(Formatting.Indented)` 반환.
- null 필드 생략(blank=keep)·enum=멤버명 등 직렬화 규약은 exporter 가 이미 보장(`NullValueHandling.Ignore`+`StringEnumConverter`). 빌더는 파일을 재조립만 하므로 규약을 재선언하지 않는다.
- `_note` 같은 `_` top-level 키는 넣지 않는다(순수 데이터 바디).

## 완료 기준

- [x] compile 성공(신규 파일). read_console 에러 0.
- [x] 기존 export 버튼("Export SO→JSON", "시트 페이로드", DC export) 동작 무변(exporter 미변경이라 구조적으로 보장 — `git` 상 exporter 파일 미변경).
- [x] 확인 완료 2026-07-22 · 커밋 `6aede4e9`. (실 push 왕복은 유닛 5.)
