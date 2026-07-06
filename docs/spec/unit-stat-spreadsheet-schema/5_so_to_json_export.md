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
- **출력 형태**: `{ "defenders": [...], "enemies": [...] }` 단일 파일 — 사람이 시트에 옮기는 용도의 통합 뷰. POST 확장 시 시트별 분할은 그 unit 에서 결정.
- 파일은 UTF-8, 들여쓰기 2. repo 밖 경로 저장 기본 (임의 산출물 커밋 방지).

## 완료 기준

- [ ] compile 오류 없음, 기존 스위트 회귀 없음
- [ ] 신규 테스트: **왕복 대칭** — 계약 JSON 을 import 로 SO(테스트용 인스턴스)에 적용 → export → 원본 payload 와 동치. flags WriteJson (부분/Everything/None), atk 역투영(unique/0개/2개+ 생략)
- [ ] 수동: Export 실행 → 산출 JSON 이 `3_seed_unit_stats.json` 과 값 동치 (시드 검증의 상호 확인 겸함)
