# 4. API Envelope 적응 — 실 Swagger 계약 반영

## 목적

실 API(`dev-api-somnia.cashroyale.games`, demo 그룹) 계약이 unit 0 의 가정과 다른 점을 임포터에 반영하고, 실 엔드포인트 왕복 검증(README 후속 후보)을 완료한다.

확인된 실 계약 (2026-07-06, `/v3/api-docs/demo` 직독 + 오류 프로브):

- `GET /demo/google/sheet/{sheetName}` — 탭 하나당 호출 1회. 인증 없음.
- 응답: `{ "success": bool, "data": [ {헤더: 값} ], "errorDetail": { "errorCode", "code", "errorMessage", "detailMessage" } }` — 실패 시 HTTP 500 + `success:false`.
- `data` 는 flat 행 배열 (기존 가정 `{defenders:[], enemies:[]}` 최상위 구조는 폐기).
- `targetClassMask` 셀은 스칼라 문자열로 내려옴 (배열 아님) — unit 3 콤마 규약.

## 변경 대상

- `Assets/_Project/Editor/UnitStatImport/UnitStatImportDto.cs` — envelope DTO 추가 (`SheetApiResponse<TRow>` 또는 defender/enemy 각각), `UnitStatImportPayload` 는 두 응답을 합치는 내부 컨테이너로 유지
- `Assets/_Project/Editor/UnitStatImport/UnitStatImportWindow.cs` — Base URL + 시트명 2필드(기본 `Defenders`/`Enemies`, EditorPrefs 유지) UI, GET 2회 순차 호출 후 ApplyPayload 1회, 실패 시 `errorDetail.errorMessage/detailMessage` 를 결과 로그에 표시
- `Assets/_Project/Editor/UnitStatImport/DefenderClassFlagsJsonConverter.cs` — 배열에 더해 **콤마 구분 문자열 토큰 수용** (case-insensitive, `Everything`/`None` 혼용 거부 규칙 동일)
- `Assets/_Project/Tests/EditMode/UnitStatImport/UnitStatImportTests.cs` — 신규 테스트

## 구현

- envelope 역직렬화 → `success` false 또는 HTTP 실패 시 해당 시트 전체 중단 + errorDetail 로그. 한쪽 시트만 성공해도 성공한 쪽은 적용한다 (부분 갱신 철학 유지).
- 숫자 셀이 문자열(`"500"`)로 내려와도 Json.NET 기본 강제변환으로 흡수됨 — 테스트로 고정.
- 두 시트에서 같은 슬러그가 와도 기존 `defender:`/`enemy:` 네임스페이스 dedup 이 그대로 동작.

## 완료 기준

- [ ] compile 오류 없음, 기존 EditMode 스위트 회귀 없음
- [ ] 신규 테스트: envelope 파싱(success/data), errorDetail 파싱, mask 콤마 문자열(부분/Everything/None/혼용 거부/대소문자), 숫자 문자열 강제변환
- [ ] **실 API 왕복**: 시드 입력된 실 시트를 `Defenders`/`Enemies` 로 GET → import → 결과 로그 `unmatched 0`, projected/skipped 카운트가 예상치(projected 18 = def 11 + enemy 7, skipped 7)와 일치, 값 스팟체크 3유닛. ※ git diff 0 은 판정 기준이 아님 (SaveAssetIfDirty 가 YAML 누락 키를 기본값 라인으로 추가 기록할 수 있음)
