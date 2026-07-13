# 8. 통합 export 버튼 — 시트 반영 한 파일

## 목적

기존 export 는 6탭을 **개별 파일**로 뱉어(SaveFolderPanel) 시트에 넣으려면 수동 병합이 필요했다. POST 엔드포인트가 없어 시트 반영은 어차피 수작업(챗봇/붙여넣기)이라는 전제 하에, **붙여넣기 1회로 전체 반영**되도록 6탭을 탭명 키 단일 JSON 으로 합치고 시트 챗봇용 프롬프트를 함께 출력하는 버튼을 추가한다.

## 판단 (skill vs script)

- **script(에디터 툴) 채택.** 자동화 가능 구간은 "SO → 붙여넣기 준비된 단일 JSON+프롬프트"뿐(시트 진입은 무조건 수작업). 이걸 네이티브 버튼으로 두면 Claude·MCP 없이 누구나 한 클릭, 게임 도구로 영속. Claude 스킬은 결국 MCP로 이 버튼을 누르는 간접층이라 더 취약 → 불채택.

## 변경 대상

- `Assets/_Project/Editor/UnitStatImport/DcSheetExporter.cs` — `ExportCombinedFile(outFilePath, tabNames, dcFolder, skillFolder)` + `BuildChatbotPrompt` 추가.
- `Assets/_Project/Editor/UnitStatImport/UnitStatImportWindow.cs` — "Export Dreamcatcher → 시트 페이로드 (1파일 + 프롬프트)" 버튼(SaveFilePanel).

## 구현

- **수집 로직 중복 없음**: 검증·커밋된 `ExportToFolder` 를 `Path.GetTempPath()` 하위 임시 폴더에 그대로 호출 → 6 파일을 `JArray.Parse` 로 읽어 탭명 키 `JObject` 로 병합(탭 순서 유지) → `outFilePath` 에 단일 JSON write. 임시 폴더는 `finally` 에서 삭제.
- 옆에 `dreamcatcher_sheet_prompt.md` sidecar 출력 — 업서트 규칙(키=id / (cardId,slot))·SoT 모드·enum/한글 원문 유지 지시 + JSON 임베드. 시트 챗봇에 이 파일 하나만 넘기면 됨.
- `_note` 키로 스냅샷 성격·SoT 모드 자기설명.

## 완료 기준

- [x] compile 0 error.
- [x] 버튼 산출 JSON 이 6탭(DcCards 29 / DcCardEffects 14 / DcMechanics 9 / DcAttackMods 1 / DcSkills 6 / DcConfig 2) 전량 포함, 탭명 키. 헤드리스 검증으로 **앞선 수동 병합본과 6탭 byte-identical** 확인.
- [x] 프롬프트 sidecar 가 JSON 임베드 + 업서트 규칙 포함.
- [x] 임시 폴더 자동 정리(finally), worktree 오염 0.

확인 2026-07-13 — 커밋 `<이 커밋>`. `ExportCombinedFile` 헤드리스 실행 결과가 `7_full_dreamcatcher_export.json` 과 동일.

## 사용법

Unity `Window/Wassup/Unit Stat Import` → **"Export Dreamcatcher → 시트 페이로드 (1파일 + 프롬프트)"** → 저장 위치 지정 → 나온 `*_prompt.md`(또는 `.json`)를 시트 챗봇에 붙여넣기. SO 를 고칠 때마다 이 버튼으로 시트를 되올린다(운영 규칙 3).
