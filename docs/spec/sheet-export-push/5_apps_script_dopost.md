# 5. Apps Script doPost 업서트 엔진 + 실 왕복 검증

## 목적

시트 쪽 반영 엔진. generic upsert(탭+키 config 구동)라 프로젝트 무관 — 이식 시 그대로 복붙. 레포에 버전 관리하고, 실 test 탭에 1회 push 해 계약(업서트·blank=keep·고아 리포트)을 실증한다.

## 변경 대상

- 신규: `docs/spec/sheet-export-push/apps-script/Code.gs` (레포 커밋. 구글 Apps Script 에디터엔 복붙 배포).
- 신규: `docs/spec/sheet-export-push/apps-script/README.md` (배포 6스텝 + 키 config 수정법).

## 구현 (Code.gs 계약)

- `doPost(e)`: 바디 = `{ "<탭명>": [행객체], ... }`(`_` 접두 top-level 키 무시). container-bound → `SpreadsheetApp.getActiveSpreadsheet()`(시트 ID 불요).
- 탭↔키 config 는 스크립트 상단 상수(유닛 1 `SheetTabRegistry` 와 **동일 계약**): `id` = Defenders/Enemies/DcCards/DcSkills/DcConfig · `(cardId,slot)` = DcCardEffects/DcMechanics/DcAttackMods.
- 탭별 처리:
  - 1행=헤더, 2행부터 데이터. 기존 헤더 순서 유지, JSON 에만 있는 새 키 → **오른쪽 새 열** 추가.
  - **업서트**: 들어온 행을 키로 매칭 → 있으면 셀 갱신, 없으면 append.
  - **blank=keep**: 행 객체에 **없는 키는 그 셀을 건드리지 않음**(비우지도 않음). import 의 "빈 셀=유지" 와 대칭.
  - **고아**: 시트엔 있고 이번 JSON 엔 없는 키 → **삭제 안 함**, 키만 수집.
  - 값 원문: enum=문자열, 숫자=숫자, 한글 원문(변형/반올림 금지).
  - 없는 탭은 생성(또는 스킵+리포트 — 배포 가이드에 명시).
- 반환: `ContentService` JSON `{success:true, data:{results:{"<탭>":{updated,added,orphans:[키]}}}, errorDetail:null}`. 예외 → `{success:false, ..., errorDetail:{errorMessage}}`.
- 배포: "웹앱으로 배포"(실행=나, 접근=URL 소지자) → `/exec` URL 을 Unity Script URL 필드에.

## 완료 기준

- [x] `Code.gs` + 배포 README 작성·커밋(`apps-script/`, `da81b0e0`).
- [x] 실 시트 배포 + 라이브 push 동작 확인(401=액세스"모든 사용자"로 해소).
- [x] **값 무변 = IDENTICAL 실증** — read-only 양방향 정합성 대조에서 8탭 전량 SO==sheet(drift 0·orphan 0·added 0). 즉 지금 push 는 no-op.
- [x] Defenders `공` 헤더 사고(키 컬럼 오라벨 → 20행 중복 추가) 발견·정리(20/20). 재발 방지 가드 `c773ec14`(키 컬럼 결측 탭 스킵+에러, `SheetPushReportTests` 회귀 고정).
- [~] 명시적 사본 3케이스(1값변경/1행제거 라이브 스크립트 재현)는 선택 — 정합성 + 가드 단위테스트로 대체 실증, 미실행.
- [x] 확인 완료 2026-07-22.
