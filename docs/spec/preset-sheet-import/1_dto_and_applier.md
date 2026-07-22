# 1 — PresetDto + PresetSheetApplier (순수 코어)

## 목적

시트 행 ↔ 프리셋 SO 참조를 매핑하는 **순수 코어**. csv 분해 → id 해석(호출처 주입) → `collection.presets` 재구성. 아키텍처 무의존이라 EditMode 단위 테스트 대상(CLAUDE.md 제약 10).

## 변경 대상

- `Assets/_Project/Scripts/Data/PresetImport/PresetDto.cs` (신규)
- `Assets/_Project/Scripts/Data/PresetImport/PresetSheetApplier.cs` (신규)
- `Assets/_Project/Tests/EditMode/PresetImport/PresetSheetApplierTests.cs` (신규)

## 구현

**PresetDto** (`Wassup.Data.PresetImport`) — public 필드, 필드명 = 시트 헤더(기존 DTO 규약):
```csharp
public class PresetDto { public string presetName; public string squad; public string dreamcatcher; }
```
`squad`/`dreamcatcher` 는 `,` 구분 id 원문. 빈 셀은 파서(`ParseSheetLogged`)가 바인딩 전 제거 → null 도착 가능.

**PresetSheetApplier.Apply** — 순수 static. 해석기는 `Func` 로 받아 에디터/런타임 공용:
```csharp
public static bool Apply(
    IReadOnlyList<PresetDto> rows,
    Func<string, DefenderUnitData> resolveUnit,
    Func<string, DreamcatcherCard> resolveCard,
    int maxUnits,
    SquadPresetCollection collection,
    StringBuilder log)
```
- `rows == null` → no-op, `false` 반환(가드; 기존 리스트 보존). `rows.Count == 0` 도 no-op 로 둘지는 계약 2 상 "빈 배열 = 명시적 비움" 이지만, 안전을 위해 **빈 배열도 no-op** 로 처리(리스트 삭제는 authoring 실수일 확률이 큼 — 로그로 고지). *(구현 시 이 한 줄만 주의: rows.Count==0 → false + "0 rows, 기존 유지" 로그.)*
- 각 행 → `SquadPreset`:
  - `presetName` = dto.presetName
  - `units` = `Split(dto.squad)` → 각 id `resolveUnit(id)`(미해결 null) → 첫 `maxUnits` 개. null 슬롯 허용(순서 보존). 초과분·미해결 카운트 리포트.
  - `cards` = `Split(dto.dreamcatcher)` → `resolveCard(id)` → **non-null 만**. 미해결 스킵 리포트.
- `collection.presets = built`(새 List)로 재구성.
- 로그 1행 요약: `rows N, presets M, units matched/unmatched/overflow, cards matched/unmatched`.
- `Split` = `,` split → trim → 빈 항목 제거 (private helper).

**의존**: `SheetFetcher`/`SheetEnvelopeParser` 는 **참조 안 함**(호출처가 rows 를 이미 파싱해 전달). applier 는 SO 타입 + `Func` 만 안다 → 순수·테스트 용이.

## 완료 기준

- compile green.
- EditMode 통과: (a) 정상 2행 재구성, (b) 미해결 unit → null 슬롯 + 순서 보존, (c) 미해결 card → 스킵, (d) squad >maxUnits → 클램프, (e) rows=null / 빈 배열 → collection 불변 + false, (f) csv 공백/빈 항목 trim.
- 테스트는 `Assets/_Project/Tests/EditMode/` 하위(asmdef 안). 해석기는 in-test 딕셔너리 `Func`.
- ✅ 완료 2026-07-22 · commit `e66be309` — EditMode 8/8 (TDD red→green), 컴파일 그린.
