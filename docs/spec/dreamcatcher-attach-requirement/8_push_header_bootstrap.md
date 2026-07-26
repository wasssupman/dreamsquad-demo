# 8 — Push 신규 컬럼 부트스트랩

## 목적

제한 카드가 0장이어도 첫 `Push to Sheet`에서 `DcCards` 오른쪽에
`attachType` / `attachValue` 두 컬럼이 자동 생성되게 한다. 일반 카드 export의
blank 규칙과 기존 배포 Apps Script는 유지한다.

## 변경 대상

- `Assets/_Project/Editor/UnitStatImport/SheetPushPayload.cs`
- `Assets/_Project/Tests/EditMode/UnitStatImport/DcSheetAttachRequireExportTests.cs`
- `docs/spec/dreamcatcher-attach-requirement/README.md`
- `docs/spec/dreamcatcher-attach-requirement/2_sheet_sync.md`
- `docs/spec/dreamcatcher-attach-requirement/6_handoff_summary.md`

## 구현

1. `SheetPushPayload`가 DC 6탭을 병합한 뒤 `DcCards` 배열 끝에 Push 전용
   **헤더 시드 행**을 하나 추가한다.
   - `id` 없음
   - `attachType: ""`
   - `attachValue: ""`
2. 기존 Apps Script는 헤더 계산 시 모든 행의 키를 먼저 읽으므로 두 컬럼을 오른쪽에
   추가한다. 이후 업서트 단계에서는 `id`가 없는 행을 건너뛰므로 실제 카드 행은
   생성·갱신되지 않는다.
3. 시드 행은 `SheetPushPayload`에만 넣는다. `DcSheetExporter.ExportToFolder`와
   Dreamcatcher 단독 JSON/챗봇 payload는 그대로 유지해, 제한 없는 카드에
   `None` 노이즈나 가짜 데이터 행을 노출하지 않는다.
4. 기존 배포 Apps Script 계약을 그대로 사용한다. 서버 재배포나 시트 수동 헤더 추가는
   필요 없다.

## 완료 기준

- Unity compile 에러 0.
- EditMode: Push payload의 `DcCards`에 키 없는 헤더 시드가 정확히 1개 있고,
  두 attach 키가 빈 문자열로 존재한다.
- EditMode: 실제 카드 행 수와 id 집합은 exporter 결과와 동일하며 시드가 카드로
  오인되지 않는다.
- 사용자가 `Push to Sheet` 실행 후 `DcCards` 오른쪽에 두 컬럼이 생기고, 카드 행
  추가 수에는 시드가 포함되지 않는 것을 확인한다.

자동 검증 2026-07-26 — Unity compile 에러 0 ·
`DcSheetAttachRequireExportTests` 2/2 pass(기존 blank export + 신규 Push 헤더 시드).
사용자 완료 승인 2026-07-26 · 커밋 `(이 커밋)`.
라이브 Push는 외부 시트 데이터를 변경하므로 이 커밋에서는 실행하지 않았고,
최초 운영 Push의 비차단 실측 항목으로 `9_handoff_summary.md`에 남긴다.
