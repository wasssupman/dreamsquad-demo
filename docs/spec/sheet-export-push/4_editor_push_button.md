# 4. 에디터 Push 버튼

## 목적

`UnitStatImportWindow` 에 push UX 를 붙인다. 버튼 한 번 → 8탭 수집(유닛 2) → POST(유닛 3) → 결과 로그. 공용 시트에 쓰는 파괴 인접 동작이라 확인 다이얼로그로 게이트.

## 변경 대상

- 수정: `Assets/_Project/Editor/UnitStatImport/UnitStatImportWindow.cs`.

## 구현

- **Script URL 필드**: `EditorPrefs` 키 `Wassup.UnitStatImport.ScriptUrl`(baseUrl 패턴 동일, 기본값 빈 문자열). **커밋 금지 secret** — 안내 라벨 1줄.
- **"Push to Sheet" 버튼** (Export 섹션 하단):
  - `_requestInFlight` + URL 공백 시 비활성(기존 disable 패턴 재사용).
  - 클릭 → `EditorUtility.DisplayDialog` 확인("전 8탭을 시트에 업서트합니다. 고아 행은 삭제하지 않고 리포트만. 계속?").
  - 확인 시 유닛 2 병합 payload 조립 → `SheetPushClient.Push`(유닛 3) → 콜백에서 `_statusLog` 갱신 + `Repaint`.
  - `onDone` 는 apply 예외에도 반드시 발화(기존 `RunDcImport` H2 규칙과 동일 — `_requestInFlight` 안 물림).
- 결과는 기존 Result TextArea 에 표시(탭별 updated/added + 고아 목록).
- import 버튼/export 버튼은 무변. push 는 독립 추가.

## 완료 기준

- [ ] 창에 URL 필드 + Push 버튼 노출, URL 없으면 비활성.
- [ ] (유닛 5 배포 후) 실 test 탭에 push → Result 로그에 탭별 카운트·고아 표시.
- [ ] `_requestInFlight` 가 성공/실패/예외 모두에서 해제됨(스티킹 없음).
- [ ] compile + 확인 일자·커밋 해시 기록.
