# 5 — 새 시트 연결 (저작)

## 목적
unit 0 의 표 구조로 시트 export/import 를 새로 만든다. 기존 `DcSheetApplier`(카드 전용) 는 은퇴.

## 변경 대상
- `Editor/UnitStatImport/` · `Scripts/Data/StatImport/` — 표별 export/import(효과 · 소유 줄 · 탄 · 패턴 · 장판 · 소유자). 임포터 하나 · 표마다 id 조회.
- 로그인 자동 임포트 경로(`LoginAutoImport`) — 새 임포터로.
- 임포트 전 diff 표 로그(dry-run 없음 → 에셋 쓰기 전에 무엇이 바뀌는지 먼저).

## 완료 기준
- export → import 왕복 후 굽기 스냅샷 동치.
- 시트 쓰기는 사용자 승인 후 · 시트 확인은 curl 읽기 전용.
