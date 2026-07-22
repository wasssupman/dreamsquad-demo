# Sheet Export Push — Handoff Summary

> feature 종료 2026-07-22. 다음 작업자용 지도 — 최신 계약은 README/번호 문서 우선.

## Commit
- `ad2028f8` docs — 스펙 초안(README + units 0~5)
- `e17ab435` feat unit 0 — `Wassup.SheetSync` asmdef(자체 envelope + POST)
- `6aede4e9` feat units 2~4 — 8탭 payload 빌더 + push 클라이언트 + 에디터 버튼
- `898974e5` docs — 유닛 0~4 완료 기록
- `da81b0e0` feat unit 5 — Apps Script `Code.gs` + 배포 가이드
- `c773ec14` fix — 키 컬럼 결측 탭 스킵+에러(실사고 방어)
- `0896e050` test — SheetEnvelope 7케이스

## Implemented
- Unity SO → 구글 시트 자동 push — 에디터 `Window/Wassup/Unit Stat Import` → "Push to Sheet", 전 8탭.
- 이식 가능한 게임 무의존 `Wassup.SheetSync` asmdef: `SheetEnvelope`(자체 봉투 파서) + `SheetHttp.Post`.
- `SheetPushPayload`: 검증된 exporter 를 임시폴더에 돌려 8탭 재읽기·병합(**exporter 미변경**).
- `SheetPushClient`: POST + 응답 요약(탭별 updated/added/고아) + 탭 스킵 경고. 비파괴.
- Apps Script `Code.gs`: generic 업서트 엔진(KEY_CONFIG 구동) — blank=keep, 고아 리포트만, 헤더 순서 유지 + 새 열 우측.
- 키 컬럼 결측 방어: 기존 행 있는데 키 컬럼 없으면 그 탭 스킵+에러(중복 append 금지).

## Key Files
- 코어: `Assets/_Project/Scripts/SheetSync/{SheetEnvelope,SheetHttp}.cs`
- 에디터: `Assets/_Project/Editor/UnitStatImport/{SheetPushPayload,SheetPushClient}.cs` + `UnitStatImportWindow.cs`(Push 섹션)
- 서버: `docs/spec/sheet-export-push/apps-script/{Code.gs,README.md}`
- 테스트: `Assets/_Project/Tests/EditMode/UnitStatImport/{SheetPushReportTests,SheetEnvelopeTests}.cs`

## Verified
- 컴파일 클린. EditMode 1229/1231 통과(0 실패, skip 2=기존 `[Ignore]`, 무관). 신규 12케이스(봉투 7 + 응답 5).
- 라이브 push 동작(401=액세스"모든 사용자"로 해소). read-only 양방향 정합성 8탭 전량 SO==sheet(drift 0).
- Defenders `공` 헤더 사고 정리(37행 중복 → 20/20).

## Notes (되돌리면 안 됨)
- **import 불변**: `SheetFetcher`/`SheetEnvelopeParser`/`Core/Api/ApiEnvelope` 안 건드림(무위험 결정). SheetSync 는 자체 봉투 — `ApiEnvelope` 참조 금지(Firebase/Tournament 공유 인프라라 끌면 모듈이 게임에 묶임).
- **비파괴**: 업서트 + 고아 리포트만(삭제 없음), blank=keep. **clear-and-insert 로 바꾸지 말 것**(디자이너 행/열 파괴 = 거부된 "완전 미러").
- **키 컬럼 함정**: 시트 각 탭 1행 헤더 키 컬럼이 `id`/`cardId`/`slot` 인지 확인. 아니면 전량 unmatched(Defenders `공` 사고). 가드가 이제 중복 대신 스킵.
- **URL=secret**: Apps Script `/exec` 는 EditorPrefs 로컬만(미커밋). Workspace 계정은 익명 웹앱 차단 가능 → 개인 Gmail.
- **프록시 캐시**: import 는 `dev-api-somnia` 프록시로 읽음 → push 직후 stale 가능(별개 계층). 시트가 진실.
- Apps Script 재배포: 코드 수정 후 "배포 관리 → 편집 → 새 버전"(저장만으론 `/exec` 안 바뀜).

## Follow-up
- import → SheetSync 완전 이관(양방향 단일 코어, GET/apply 승격). 현재 import 는 레거시 유지.
- 런타임 pusher(카탈로그→POST, dev 빌드) · SoT=시트 전환(별도 spec) · push dry-run 프리뷰 · 고아 반자동 정리.
- (선택 미실행) 사본 시트 라이브 3케이스 스크립트 재현.
- (별개 spec) `preset-sheet-import` — Presets 탭, list-SoT + id→SO 참조. 이 8탭 keyed-upsert 모델 밖.
