# Unit Stat Spreadsheet Schema

상태: **Unit 0~1 구현 완료 (2026-07-02) — 실 엔드포인트 왕복 검증 대기**

## 목표

기획파트가 Defender/Enemy 유닛 스탯을 Unity Inspector 대신 스프레드시트에서 관리할 수 있도록, 스프레드시트 → REST API → JSON 변환 이후 Unity SO(`DefenderUnitData`/`AttackUnitData`)로 들어오는 **JSON 계약**을 정의한다.

REST API 호출 및 스프레드시트→JSON 변환 파이프라인(Swagger 기반)은 이미 구현되어 있다고 가정한다. Unity 쪽 JSON 소비(DTO/매퍼/임포터)는 Unit 1에서 구현 완료. 스프레드시트 컬럼 구성은 향후 바뀔 수 있다는 전제로, 필드 추가/삭제가 최소 diff로 흡수되도록 설계했다 (자세한 내용은 Unit 1 문서 참고).

## 작업 단위 목록

| 번호 | 구분 | 문서 | 목적 |
|---|---|---|---|
| 0 | 계약 (docs only) | `0_json_schema_contract.md` | Defender/Enemy stat JSON 스키마 + 공통 컨벤션 확정 |
| 1 | 구현 | `1_dto_mapper_and_importer.md` | `AttackUnitData.id` 추가, DTO/부분갱신 매퍼/`targetClassMask` 컨버터/`UnitStatImportWindow` 구현 + EditMode 테스트 |

## Feature-wide 계약

- **JSON 최상위 구조**: `{ "defenders": [...], "enemies": [...] }` — 스프레드시트 탭 2개(Defenders/Enemies)에 1:1 대응하는 두 배열. 유닛 타입 통합 배열(`units[]` + type 판별자)이나 base/type 중첩 구조는 채택하지 않음 (sparse null 방지, 스프레드시트 행 → flat JSON object 변환과 자연스럽게 대응).
- **범위**: 순수 밸런스 수치만. Mesh/Material/Spine/VFX prefab 등 자산 참조와 `hazardCast`/`onPlaceEffect`/`knockback`/`onPlacePush`/`targetAllies` 등 스킬 메커니즘 파라미터는 제외 — 엔지니어가 Inspector에서 계속 직접 관리.
- **매칭키**: `id` 문자열. Defender는 기존 `DefenderUnitData.id` 그대로 사용. Enemy는 Unit 1에서 `AttackUnitData.id` 신규 추가 + 기존 9종 asset 값 채움 완료.
- **Enum 표기**: C# enum 멤버명 문자열(서수 아님 — enum 재정렬에 안전). 표기 권장은 멤버명 그대로, 수용은 case-insensitive. 미지 멤버명은 임포트 실패.
- **비트마스크(`targetClassMask`)**: enum명 문자열 배열로 표기. `[]`=None, `["Everything"]`=전체 허용. `"Everything"`과 개별 클래스명 혼용은 거부.
- **임포트 정책**: `id` 매칭 기반 갱신만 수행 (upsert 없음 — 미매칭 `id`는 무시, 신규 `.asset` 자동 생성 안 함). 빈 셀(JSON에 키 없음)은 기존 SO 값 유지하는 **부분 갱신**.
- **버전 필드**: 1차 스키마엔 미포함 (컨슈머 단일, YAGNI). 필요해지면 후속 후보로 승격.
- **레거시 caveat**: `attackDamage`는 두 SO 모두 레거시 스칼라 필드. 런타임 데미지의 실제 source of truth는 `AttackOutput[] outputs`이며, 이번 스키마 범위 밖.

## 스키마 v2

시트 `atk`/`heal` → outputs 투영 및 `attackDamage` 컬럼 폐기(deprecation shim)는 **`docs/spec/unit-stat-projection/`** 에서 정의한다. v2 delta의 SoT는 그쪽 `0_projection_contract.md`.

## 후속 후보

- **실 엔드포인트 왕복 검증** [S] · `Window/Wassup/Unit Stat Import`에서 실제 Swagger REST URL로 1회 import 확인 (이번 세션은 URL 미제공으로 미검증).
- **AttackOutput[] outputs 스프레드시트 반영** [M] · 단순 magnitude 투영은 unit-stat-projection 에서 진행. 멀티 히트/항목 구성 등 배열 전체 노출은 여전히 범위 밖.
- **ObstaclePlacerTests 무관 실패** [S] · `Place_PreservesWalkAndMinimumPlaceRatio` (expected >=36, was 31) — 이번 변경과 무관, 별도 확인 필요.
