# 6. Handoff Summary

## Commit

- `5a11f584` docs(spec): unit 3~5 spec — 실 API envelope 계약 반영 + 시드 JSON 보존
- `b9cc03ac` feat(import): 실 시트 API envelope 적응 (unit 4)
- `9e18c8ca` feat(export): SO→JSON 시트별 export (unit 5)
- `4d0d277a` chore(data): 실 시트 import 왕복 검증 — SO 재직렬화 정규화
- `2f6dcd53` balance(enemy): 원거리 적 attackRange 하향 — 시트 경유 첫 밸런스 반영
- (unit 0~2 는 2026-07-02 이전 세션 커밋)

## Implemented

- 실 API 계약 가동: `GET https://dev-api-somnia.cashroyale.games/demo/google/sheet/{sheetName}` (인증 없음), 탭당 1콜, `{success, data:[행], errorDetail}` envelope
- `Window/Wassup/Unit Stat Import`: Base URL + 시트명 2필드, Import(2-call→id 매칭 부분갱신), Export(시트별 `{SheetName}.json`, id 오름차순)
- 빈 문자열 셀 = 키 생략(기존 값 유지), 숫자 문자열 강제변환, enum 대소문자 무관
- `targetClassMask` 콤마 문자열 양방향 (`Everything`/`None` 혼용 거부, unit 1 배열 하위호환)
- export 는 enum 멤버명 직렬화(StringEnumConverter) + null 키 생략 + atk/heal unique 역투영
- 구글 시트 실 왕복 2회 검증: ① 시드 no-op (`Matched 25, unmatched 0, projected 19, fields 336`) ② 시트에서 attackRange 5건 변경 → SO 정확 반영

## Key Files

- `Assets/_Project/Editor/UnitStatImport/UnitStatImportWindow.cs` — 창/fetch/envelope 파싱/ApplyPayload
- `Assets/_Project/Editor/UnitStatImport/UnitStatExporter.cs` — export (ToDto/ToRowsJson/ExportToFolder)
- `Assets/_Project/Editor/UnitStatImport/UnitStatFieldMapper.cs` — 양방향 리플렉션 매퍼
- `Assets/_Project/Editor/UnitStatImport/DefenderClassFlagsJsonConverter.cs` — mask 양방향
- `Assets/_Project/Tests/EditMode/UnitStatImport/UnitStatImportTests.cs` — 37 테스트
- `docs/spec/unit-stat-spreadsheet-schema/3_seed_unit_stats.json` — 최초 시드 스냅샷 (역사 기록; 이후 SoT 는 시트)

## Verified

- EditMode 518개 통과 — 무관 상시 실패 1건(`ObstaclePlacerTests.Place_PreservesWalkAndMinimumPlaceRatio`) 제외
- compile 0 error (unityMCP `read_console`)
- 실 시트 왕복: 시드 no-op 확인 후, 의도 변경 5건이 대상 asset 5개에만 반영됨을 diff 로 확인

## Notes

- **시트가 밸런스 컬럼의 SoT** — SO 를 직접 고치면 다음 import 때 시트 값으로 덮인다. SO 쪽 변경은 Export 로 시트에 되올리는 흐름(수동) 유지.
- 시트 헤더 = 계약 키 (초기 `name`/`type` 오기 → `displayName`/`enemyClass` 정정 완료). 새 컬럼 추가는 DTO 필드 1개 추가로 양방향 흡수.
- `execute_code`(unityMCP)는 이 환경에서 고장(mono 커맨드라인 길이) — 에디터 내 일회 실행이 필요하면 임시 `[MenuItem]` 스크립트 + `execute_menu_item` 패턴 사용 후 삭제.
- 왕복 판정은 import 로그 카운트 + 값 diff 로. git diff 0 아님 (재직렬화 정규화는 `4d0d277a` 로 일단락).

## Follow-up

- README "후속 후보" 참조: POST API 전송(엔드포인트 대기), import dry-run 프리뷰 [S], Hazards 탭 [M], 2nd output(도트/디버프) 노출 [M]
- 기획 편의: 시트에 `_dps` 등 파생 수식 컬럼 추가 가능 (`_` 접두 = 계약 밖, 임포터 무시)
