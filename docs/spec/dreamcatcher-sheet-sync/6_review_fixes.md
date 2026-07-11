# 6. 리뷰 반영 — 3관점 리뷰 (아키텍트/코드/프로세스) 결함 수정

## 목적

2026-07-11 방법론 전체 리뷰(효율/확장성, 3 병렬 에이전트)에서 확정된 결함을 해소한다. 확정 finding: H1(음수 slot 크래시), H2(DC import 예외 시 창 고착), 3b(컬럼 rename 무음 실패), M1(재배열+빈셀 시맨틱 미문서), M2(fetch timeout 부재), L1(export 버튼 과잉 게이트), 아키텍트 #3(SoT 모드 암묵지).

## 변경 대상

- `Assets/_Project/Scripts/Data/StatImport/DcSheetApplier.cs` — H1: `GroupByCard` 가 음수 slot 을 null 과 동일하게 카드 poison (mechanics 경로와 대칭 복원)
- `Assets/_Project/Editor/UnitStatImport/UnitStatImportWindow.cs` — H2: `RunDcImport` 콜백 try/catch 로 onDone 발화 보장 · L1: export 버튼을 baseUrl 게이트에서 분리 · SoT 모드 로그 1줄
- `Assets/_Project/Scripts/Data/StatImport/SheetFetcher.cs` — M2: `request.timeout=30` + 빈 url 배열 가드
- `Assets/_Project/Scripts/Data/StatImport/SheetEnvelopeParser.cs` — 3b: DTO 에 없는 헤더(`_` 제외) 탭별 리포트
- `Assets/_Project/Tests/EditMode/UnitStatImport/DcSheetImportTests.cs` — 신규 8케이스 (음수/빈 slot, M1 재배열 동작 고정, attackMods 축소 리포트, mechanics 중복, 미지 cardId, 빈 payload, 미인식 헤더)
- 계약 문서: `0_json_schema_contract.md` (음수 slot 규정, 재배열 규칙, 미인식 헤더 리포트) · `README.md` (운영 규칙 4항, 후속 후보 갱신)

## 구현

리뷰 판정 그대로 — 새 시맨틱 도입 없음. M1 은 동작 변경 없이 현 시맨틱을 계약 문서화 + 테스트로 고정. 백로그 이관: dry-run diff [M, 최우선], 탭 매핑 테이블 [S], Rebuild 병합 [M], git-dirty 검사 [S] (README 후속 후보).

## 완료 기준

- [x] compile 0 error
- [x] EditMode 73/73 (신규 8케이스 포함), 기존 스위트 그린 유지
- [x] H1 재현 테스트가 수정 전 크래시 경로를 커버 (`RebuildEffects_NegativeSlot_SkipsCardWithoutThrowing`)
- [x] 미인식 헤더가 로그에 등장하고 `_` 컬럼은 제외됨 (`ParseSheetLogged_UnknownHeader_IsReportedAndUnderscoreIsNot`)

확인 2026-07-11 — EditMode 73/73, 커밋 `0fff6a9d`.
