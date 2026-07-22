# 1. Adapter 등록 테이블 (8탭 계약 한곳에)

## 목적

8탭의 {탭명·업서트 키·DTO 타입·수집 소스}를 **한곳에 선언**한다. 지금 이 지식은 exporter/importer/window 에 흩어져 위치기반(`r[0..5]`)으로 암묵돼 있다(`dreamcatcher-sheet-sync` 후속 후보 "탭 배선 매핑 테이블화"와 동일 문제). push payload 빌더(유닛 2)와 응답 해석이 이 테이블 하나만 읽으면 되게 한다. 이식 시 "새 프로젝트의 adapter" = 이 테이블 재작성.

## 변경 대상

- 신규: `Assets/_Project/Editor/UnitStatImport/SheetTabRegistry.cs` (에디터 어셈블리 — push 는 에디터 전용, AssetDatabase scan 소스에 묶임).

## 구현

- `SheetTabRegistry` — 8개 엔트리의 정적 배열. 각 엔트리:
  - `tabName` (예: `"Defenders"`, `"DcCardEffects"`) — 시트 탭명 = payload JSON 키.
  - `keyKind` — `Id` | `CardIdSlot` (Apps Script 와 공유하는 키 계약. `id` = Defenders/Enemies/DcCards/DcSkills/DcConfig · `(cardId,slot)` = DcCardEffects/DcMechanics/DcAttackMods).
  - 수집 델리게이트 — 그 탭의 행 배열(DTO 리스트)을 반환. 유닛 2 가 채운다(기존 `UnitStatExporter.ToDto`/`DcSheetExporter` 행빌더 재사용).
- 탭명 상수는 기존 `UnitStatImportWindow` 의 default(`"Defenders"`/`"Enemies"`/`DefaultDcSheets`)와 **동일 문자열**을 단일 출처로 참조(중복 리터럴 금지).
- **주의**: 이 테이블은 "재사용할 기존 시스템을 가리키는 길찾기"다. 키 계약은 이미 import applier 에 존재하므로 **새 규칙을 만들지 말고** 기존 계약을 한 뷰로 모으기만 한다.

## 완료 기준

- [ ] 8 엔트리 전부 등록, 탭명 리터럴 단일 출처.
- [ ] compile 성공.
- [ ] (유닛 2 에서 소비되므로) 이 유닛 단독 테스트는 생략 — 유닛 2 의 payload 빌더 테스트가 커버.
