# 7 — desc 시트 왕복 (import/export)

## 목적

`desc`를 Defenders 시트와 왕복시킨다 — 체력·displayName 등과 **완전 동일**한 리플렉션 매핑 경로. DTO 필드 1개 추가로 흡수(임포터/익스포터 무변경). Enemies 는 범위 밖.

## 변경 대상

- `Assets/_Project/Scripts/Data/StatImport/UnitStatImportDto.cs` — `DefenderStatDto`에 `public string desc;`(displayName 동형).
- `Assets/_Project/Tests/EditMode/UnitStatImport/UnitStatImportTests.cs` — desc 왕복 테스트.

## 구현

- `DefenderStatDto.desc` (문자열, nullable 아님) → `ApplyNonNullFields`(import)가 non-null 시 SO.desc 로 복사, null(빈 셀/생략) 시 유지. `ReadFieldsToDto`(export)가 SO.desc → DTO.desc. 매퍼/윈도우/익스포터 코드 **무변경**(계약: "컬럼 추가 = DTO 필드 1개").
- 시트: Defenders 시트에 헤더 `desc` 컬럼 추가는 시트 소유자 몫. export JSON 이 그 값을 만들어준다(`Defenders.json` 의 각 행 `desc`).
- `_descAuto` 파생컬럼·특수 익스포트 없음(체력과 동일하게).

## 완료 기준

- [x] 컴파일 클린. `UnitStatImportTests` 44/44(기존 40 + desc 4).
- [x] import: `{id, desc}` → SO.desc 갱신 / desc 생략 → 기존 유지.
- [x] export: SO.desc → `ReadFieldsToDto` DTO.desc / JSON 왕복 보존.
- [x] 기존 export 통합 테스트 회귀 없음(desc 흡수, 매퍼/윈도우/익스포터 코드 무변경).

> 구현 2026-07-18 · 커밋 대기.
