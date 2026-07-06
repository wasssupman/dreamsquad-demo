# Unit Stat Spreadsheet Schema

상태: **Unit 0~2 구현 완료 (2026-07-02) · Unit 3~5 spec 작성 (2026-07-06, 실 API 계약 확인 반영) — 구현 대기**. atk/heal 투영·레거시 제거는 → `docs/spec/unit-stat-projection/` (완료).

## 목표

기획파트가 Defender/Enemy 유닛 스탯을 Unity Inspector 대신 스프레드시트에서 관리할 수 있도록, 스프레드시트 → REST API → JSON → Unity SO(`DefenderUnitData`/`AttackUnitData`) 왕복 파이프라인의 Unity 측을 구현한다. 1차 스코프는 기본 스탯(체력/공격력/공속/이속 등 계약 표의 밸런스 스칼라)만 — 도트뎀/힐 확장·해저드 수치는 후속 후보로 대기.

## 작업 단위 목록

| 번호 | 구분 | 문서 | 목적 |
|---|---|---|---|
| 0 | 계약 (docs only) | `0_json_schema_contract.md` | 유닛별 필드 서브셋 + 공통 컨벤션 확정 (최상위 구조는 unit 4 로 개정) |
| 1 | 구현 | `1_dto_mapper_and_importer.md` | DTO/부분갱신 매퍼/flags 컨버터/`UnitStatImportWindow` + EditMode 테스트 |
| 2 | 구현 | `2_importer_robustness_hotfix.md` | 리뷰 결함 5건 해소 (SaveAssetIfDirty, 도메인 리로드, 중복 id 등) |
| 3 | 데이터+docs | `3_seed_json_and_sheet_guide.md` | 시드 JSON(`3_seed_unit_stats.json`) 보존 + 시트 입력 규약 |
| 4 | 구현 | `4_api_envelope_adaptation.md` | 실 API envelope/2-call/errorDetail/mask 콤마 문자열 반영 + 실 왕복 검증 |
| 5 | 구현 | `5_so_to_json_export.md` | SO→JSON export (파일 저장, 추후 POST body 생성기) |

## Feature-wide 계약

- **실 API** (unit 4 에서 확정): `GET https://dev-api-somnia.cashroyale.games/demo/google/sheet/{sheetName}`, 인증 없음. 탭 하나당 호출 1회 (`Defenders` / `Enemies`). 응답 envelope `{ success, data: [행 객체], errorDetail }`. unit 0 의 `{defenders:[], enemies:[]}` 최상위 구조 가정은 폐기 — 유닛별 필드 표와 셀 컨벤션은 유효.
- **범위**: 순수 밸런스 수치만. 자산 참조·스킬 메커니즘 파라미터(hazard/knockback/onPlace 등)는 Inspector 관리 유지.
- **매칭키**: `id` 슬러그 문자열. 갱신만 수행 (upsert 없음, 미매칭 무시, 신규 asset 생성 없음).
- **Enum 표기**: C# 멤버명 문자열, case-insensitive 수용. 미지 멤버명은 임포트 실패.
- **`targetClassMask`**: 시트 셀은 콤마 구분 문자열 (`Everything` / `None` / `Ranger,Guardian`). 특수값과 개별 클래스명 혼용 거부. (JSON 배열 표기는 unit 1 하위호환으로 유지)
- **빈 셀 = 키 생략 = 기존 SO 값 유지** (부분 갱신). `_` 접두 시트 컬럼은 계약 밖 (파생/메모용, 임포터가 무시).
- **DTO 필드명 = SO 필드명 = 시트 헤더** — 컬럼 추가는 DTO 필드 1개 추가로 흡수 (import/export 공유).
- **레거시 caveat**: `attackDamage` 컬럼은 폐기 (`atk` 로 대체, 수신 시 경고 후 무시). 런타임 데미지 SoT 는 `AttackOutput[] outputs`.

## 스키마 v2

시트 `atk`/`heal` → outputs 투영 및 `attackDamage` 폐기는 **`docs/spec/unit-stat-projection/`** 에서 정의 (완료). v2 delta 의 SoT 는 그쪽 `0_projection_contract.md`.

## 후속 후보

- **스프레드시트 POST API 전송** [M] · unit 5 의 export body 를 실 POST 엔드포인트로 전송. 서버 측 엔드포인트 확보 대기.
- **Import dry-run 프리뷰** [S] · 적용 전 old→new 변경 목록 표시. 밸런싱 반복 신뢰성 + no-op 왕복 검증 명확화.
- **Hazards 탭** [M] · 캐스터 데미지(`HazardSO` param1/lifetime/radius 등) 시트 노출. `HazardSO.id` 신설 필요.
- **2nd output(도트/디버프/스택) 시트 노출** [M] · ApplyStat/ApplyStack magnitude·duration 투영. (구 "AttackOutput[] outputs 반영" 항목 흡수)
- **ObstaclePlacerTests 무관 실패** [S] · `Place_PreservesWalkAndMinimumPlaceRatio` (expected >=36, was 31) — 기존 항목 유지, 별도 확인 필요.
- ~~실 엔드포인트 왕복 검증~~ → unit 4 완료 기준으로 승격.
