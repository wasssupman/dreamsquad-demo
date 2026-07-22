# 0 — Presets 탭 스키마 계약 (docs only)

## 목적

`Presets` 시트 탭의 컬럼·시맨틱을 확정한다. 이후 유닛(DTO/applier/에디터/런타임)이 이 계약 하나를 참조한다.

## 변경 대상

- 이 문서만 (docs). 코드 변경 없음.

## 구현 (계약 확정)

**탭명**: `Presets` (contract-fixed). API 는 기존과 동일 — `GET {base}/Presets`, envelope `{success, data:[row], errorDetail}`.

**컬럼 3개** (헤더 = DTO 필드명):

| 헤더 | 타입 | 의미 |
|---|---|---|
| `presetName` | string | 목록 아이템에 표시. 빈 값이면 뷰가 "프리셋" 폴백 |
| `squad` | string | `,` 구분 `DefenderUnitData.id` 목록. 순서 = 스쿼드 슬롯 순서. 상한 = `maxUnits`(=`SquadSave.SlotCount`=7) |
| `dreamcatcher` | string | `,` 구분 `DreamcatcherCard.id` 목록. 상한 = 라이브 deckSize(현 10) |

**행 = 프리셋 1개. 행 순서 = `presets` 리스트 순서.** id/slot 컬럼 없음(위치 기반).

**csv 규칙**: `,` split → 각 항목 trim → 빈 항목 drop. (빈 셀은 파서가 바인딩 전 제거하므로 `squad`=null → 빈 스쿼드로 취급.)

**위치 기반 list-SoT 재구성**:
- 탭 전체가 `presets` 리스트의 SoT. 파싱된 행이 있으면 리스트를 그 행들로 **통째 교체**(재구성).
- **가드**: fetch 실패/비 JSON/빈 응답으로 rows=null 이면 **no-op**(기존 리스트 보존). 리스트를 비우는 유일한 방법은 "빈 행 배열"이 아니라 명시적 authoring 이다.

**id 해석 + 미해결 처리** (계약 3 상세):
- unit id → `DefenderUnitData`, card id → `DreamcatcherCard`. 해석기는 호출처 주입(에디터=AssetDatabase 인덱스 / 런타임=카탈로그 `ById`).
- 미해결 unit id → 그 **슬롯 null**(빈슬롯; 순서 보존) + unmatched 리포트.
- 미해결 card id → **스킵**(리포트). 카드는 슬롯 없는 리스트라 홀 없이 유효 카드만.
- `squad` 항목 수 > `maxUnits` → 초과분 drop + 리포트.
- 카드 수는 하드 캡 없음(계약 7: v1 검증 없음). 로그로 개수만 남긴다.

**컨벤션 승계**: `_` 접두 컬럼(예: `_note`)은 계약 밖 — 파서가 무시(정보성). 헤더가 계약과 다르면 파서가 "headers not in contract" 경고.

## 완료 기준

- 컬럼·시맨틱·미해결 규칙이 위 표/문장으로 확정되어 unit 1~4 가 참조 가능.
- docs 커밋.
