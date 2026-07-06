# 4. API Envelope 적응 — 실 Swagger 계약 반영

## 목적

실 API(`dev-api-somnia.cashroyale.games`, demo 그룹) 계약이 unit 0 의 가정과 다른 점을 임포터에 반영하고, 실 엔드포인트 왕복 검증(README 후속 후보)을 완료한다.

확인된 실 계약 (2026-07-06, `/v3/api-docs/demo` 직독 + 오류 프로브):

- `GET /demo/google/sheet/{sheetName}` — 탭 하나당 호출 1회. 인증 없음.
- 응답: `{ "success": bool, "data": [ {헤더: 값} ], "errorDetail": { "errorCode", "code", "errorMessage", "detailMessage" } }` — 실패 시 HTTP 500 + `success:false`.
- `data` 는 flat 행 배열 (기존 가정 `{defenders:[], enemies:[]}` 최상위 구조는 폐기).
- `targetClassMask` 셀은 스칼라 문자열로 내려옴 (배열 아님) — unit 3 콤마 규약.

## 변경 대상

- `Assets/_Project/Editor/UnitStatImport/UnitStatImportWindow.cs` — Base URL + 시트명 2필드(기본 `Defenders`/`Enemies`, EditorPrefs 유지) UI, GET 2회 순차 호출 후 ApplyPayload 1회, envelope 파싱(`ParseSheetRows<T>`, JObject 기반 — 별도 envelope DTO 클래스 없음), 실패 시 `errorDetail` 을 결과 로그에 표시
- `Assets/_Project/Editor/UnitStatImport/DefenderClassFlagsJsonConverter.cs` — 배열에 더해 **콤마 구분 문자열 토큰 수용** (case-insensitive, `Everything`/`None` 혼용 거부 규칙 동일)
- `Assets/_Project/Tests/EditMode/UnitStatImport/UnitStatImportTests.cs` — 신규 테스트
- (`UnitStatImportDto.cs` 무변경 — 행 DTO 는 기존 그대로 재사용)

## 구현

- envelope 역직렬화 → `success` false 또는 HTTP 실패 시 해당 시트 전체 중단 + errorDetail 로그. 한쪽 시트만 성공해도 성공한 쪽은 적용한다 (부분 갱신 철학 유지).
- **빈 문자열 셀 = 키 생략**: DTO 바인딩 전에 `""`/공백 문자열 값을 행에서 제거 — "빈 셀 = 기존 값 유지" 계약을 API 가 빈 문자열로 내려줘도 보장 (float?/enum? 가 `""` 에서 깨지는 것도 방지).
- 숫자 셀이 문자열(`"500"`)로 내려와도 Json.NET 기본 강제변환으로 흡수됨 — 테스트로 고정.
- 두 시트에서 같은 슬러그가 와도 기존 `defender:`/`enemy:` 네임스페이스 dedup 이 그대로 동작.

## 완료 기준

- [x] compile 오류 없음 (2026-07-06, unityMCP `read_console` 0 errors)
- [x] 신규 테스트 12종 + 기존 스위트 회귀 없음 (2026-07-06, EditMode 510개 중 실패 1 = 알려진 무관 상시 실패 `ObstaclePlacerTests.Place_PreservesWalkAndMinimumPlaceRatio`)
- [x] **실 API 왕복** (2026-07-06): `Defenders`/`Enemies` GET → import → `Matched 25, unmatched 0, fields applied 318, projected 19, skipped 0`. 시트 데이터는 시드와 25유닛 전 필드 동치(사전 대조). SO 값 변경 0 — asset diff 는 레거시 `attackDamage` 잔존 라인 제거 + 누락 키 기본값 명시뿐 (재직렬화 정규화).
  - 카운트 해석: projected 19 = def atk 11 + heal 1 + enemy atk 7. 빈 atk 셀은 키 생략 → no-op (skip 아님; 초안의 "skipped 7" 예상은 오산이었음).
  - ~~잔여: Enemies 탭 헤더 2개~~ → 정정 완료 (2026-07-06). 재실행 결과 `fields applied 336` 확인 + 시트발 attackRange 변경 5건이 대상 asset 5개에만 정확 반영 (`2f6dcd53`).
