# 5. SO → JSON Export

## 목적

현재 SO 값을 계약 형태의 JSON 으로 추출하는 정식(C#, Unity 내) 도구를 만든다. 용도: (a) 시트 재시드 — unit 3 의 일회용 Python 스크립트 대체(재사용 금지 판정됨), (b) 추후 스프레드시트 POST API 확장 시 body 생성기 재사용.

이번 unit 범위는 **파일 저장까지**. POST 전송은 서버 측 엔드포인트가 생긴 뒤 별도 unit (후속 후보).

## 변경 대상

- `Assets/_Project/Editor/UnitStatImport/UnitStatExporter.cs` (신규) — SO → DTO 역방향 채움 + 직렬화
- `Assets/_Project/Editor/UnitStatImport/UnitStatFieldMapper.cs` — `ReadFieldsToDto(so, dto)` 역방향 리플렉션 추가 (이름 매칭, `id` 포함)
- `Assets/_Project/Editor/UnitStatImport/DefenderClassFlagsJsonConverter.cs` — `WriteJson` 구현 (flags → 콤마 문자열, `Everything`/`None` 특수값)
- `Assets/_Project/Editor/UnitStatImport/UnitStatImportWindow.cs` — "Export to JSON File" 버튼 (저장 경로 다이얼로그)
- `Assets/_Project/Tests/EditMode/UnitStatImport/UnitStatImportTests.cs` — 신규 테스트

## 구현

- **필드 범위 = import 서브셋과 동일** (계약 표의 컬럼만). DTO 를 공유하므로 컬럼 추가 시 DTO 필드 하나로 양방향이 함께 따라온다.
- **atk/heal 역투영**: unique output 의 magnitude 를 읽는다 (`AttackOutputStats` 의 카운트 로직 재사용). 0개 또는 2개+ 는 키 생략 — import 의 "빈 셀 = 유지" 와 대칭.
- **전체 스냅샷**: 각 유닛의 서브셋 필드를 항상 전부 채운다 (diff export 없음).
- **출력 형태**: **시트(탭)별 파일** — `{DefenderSheet}.json` / `{EnemySheet}.json` (윈도우의 시트명 필드 그대로, 기본 `Defenders.json`/`Enemies.json`). 각 파일은 행 객체 배열 `[ {...}, ... ]` — API `data` 필드·시트 탭과 1:1 대응. null 필드는 키 생략(빈 셀 대응), `targetClassMask` 는 콤마 문자열로 직렬화(셀 표기와 동일).
- 파일은 UTF-8, 들여쓰기. 저장 위치는 폴더 선택 다이얼로그 (repo 밖 기본 — 임의 산출물 커밋 방지). 행 순서는 id 오름차순 고정.

## 완료 기준

- [x] compile 오류 없음, 기존 스위트 회귀 없음 (2026-07-06, EditMode 518개 — 무관 상시 실패 1건 외 전부 통과)
- [x] 신규 테스트 8종: 역방향 매퍼, atk/heal 역투영(unique/모호 생략), flags WriteJson(부분/Everything/None), enum 멤버명 직렬화, null 키 생략, 왕복 대칭, 실 에셋 export 통합(개수/파싱)
- [x] Export 산출 JSON ↔ `3_seed_unit_stats.json` 25유닛 전 필드 동치 확인 (2026-07-06, 자동 대조). 첫 대조에서 enum 서수 직렬화 결함 발견 → `StringEnumConverter` 로 수정 후 동치
