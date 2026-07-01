# Unit Stat Spreadsheet Schema

상태: **스키마 설계 완료 (2026-07-02) — 구현 대기**

## 목표

기획파트가 Defender/Enemy 유닛 스탯을 Unity Inspector 대신 스프레드시트에서 관리할 수 있도록, 스프레드시트 → REST API → JSON 변환 이후 Unity SO(`DefenderUnitData`/`AttackUnitData`)로 들어오는 **JSON 계약**을 정의한다.

REST API 호출 및 스프레드시트→JSON 변환 파이프라인은 별도로 준비되어 있다고 가정한다. 이 spec의 범위는 **JSON 스키마 형태 설계까지**이며, 실제 Unity Editor 임포터 구현과 `AttackUnitData.id` 필드 추가는 포함하지 않는다 (후속 유닛으로 이관).

## 작업 단위 목록

| 번호 | 구분 | 문서 | 목적 |
|---|---|---|---|
| 0 | 계약 (docs only) | `0_json_schema_contract.md` | Defender/Enemy stat JSON 스키마 + 공통 컨벤션 확정 |

## Feature-wide 계약

- **JSON 최상위 구조**: `{ "defenders": [...], "enemies": [...] }` — 스프레드시트 탭 2개(Defenders/Enemies)에 1:1 대응하는 두 배열. 유닛 타입 통합 배열(`units[]` + type 판별자)이나 base/type 중첩 구조는 채택하지 않음 (sparse null 방지, 스프레드시트 행 → flat JSON object 변환과 자연스럽게 대응).
- **범위**: 순수 밸런스 수치만. Mesh/Material/Spine/VFX prefab 등 자산 참조와 `hazardCast`/`onPlaceEffect`/`knockback`/`onPlacePush`/`targetAllies` 등 스킬 메커니즘 파라미터는 제외 — 엔지니어가 Inspector에서 계속 직접 관리.
- **매칭키**: `id` 문자열. Defender는 기존 `DefenderUnitData.id` 그대로 사용. Enemy는 `AttackUnitData`에 `id` 필드가 없으므로 **신규 추가 필요** (후속 후보).
- **Enum 표기**: C# enum 멤버명과 동일한 문자열(대소문자 유지). 서수가 아닌 이름 매칭이라 enum 재정렬에 안전.
- **비트마스크(`targetClassMask`)**: enum명 문자열 배열로 표기. `[]`=None, `["Everything"]`=전체 허용.
- **임포트 정책**: `id` 매칭 기반 갱신만 수행 (upsert 없음 — 미매칭 `id`는 무시, 신규 `.asset` 자동 생성 안 함). 빈 셀(JSON에 키 없음)은 기존 SO 값 유지하는 **부분 갱신**.
- **버전 필드**: 1차 스키마엔 미포함 (컨슈머 단일, YAGNI). 필요해지면 후속 후보로 승격.
- **레거시 caveat**: `attackDamage`는 두 SO 모두 레거시 스칼라 필드. 런타임 데미지의 실제 source of truth는 `AttackOutput[] outputs`이며, 이번 스키마 범위 밖.

## 후속 후보

- **AttackUnitData.id 필드 추가** [S] · Defender와 동일 패턴(stable persistence key)으로 Enemy SO에 신규 필드 추가. 기존 9종 Enemy asset에 값 채우기 필요.
- **Unity Editor 임포터 구현** [M] · JSON → SO 필드 매핑 + 부분 갱신 로직 + 미매칭 id 경고 UI. `Assets/_Project/Editor/` 대상.
- **AttackOutput[] outputs 스프레드시트 반영** [M] · 현재 범위 밖(멀티 히트/DoT/스플래시 등 메커니즘). 별도 spec에서 논의.
- **targetClassMask 검증 로직** [S] · `"Everything"`과 개별 클래스명 혼용 방지 등 importer-side validation.
