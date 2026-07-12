# 0 — CardBinding 제거

## 목적

`CardBinding {Axis, Unit}` 를 제거해 택소노미 이중 필드 세금을 없앤다. "이 카드가 host-only 냐 축-집합이냐"는 이제 `CardType` 하나에서 파생한다(`CardType.Unit` = host / 그 외 = axis). `binding` 은 `type` 과 완전히 중복(Squad⟺Axis, Unit⟺Unit)이었고, 그 중복을 감시하던 `DcSheetApplier` 의 일관성 경고도 함께 제거한다.

## 변경 대상

- `Assets/_Project/Scripts/Data/Dreamcatcher/DreamcatcherCard.cs`
  - `enum CardBinding` 삭제, `public CardBinding binding;` 필드 삭제, 관련 주석 갱신(mechanics 주석의 "binding=Unit" → "type=Unit").
- `Assets/_Project/Scripts/Bridge/BattleBridge.Dreamcatcher.cs`
  - `ApplyDreamcatcherCardToUnit` 가드: `card.binding != CardBinding.Unit` → `card.type != CardType.Unit` (type 이 authoritative 필드 — 더 정확한 방어).
- `Assets/_Project/Scripts/Data/StatImport/DcSheetImportDto.cs`
  - `DcCardDto.binding` 필드 삭제 (리플렉션 매퍼가 이름으로 round-trip → 자동으로 sheet 컬럼에서 빠짐).
- `Assets/_Project/Scripts/Data/StatImport/DcSheetApplier.cs`
  - `WarnTypeBindingMismatch` 메서드 삭제, `ApplyFlat(payload?.cards, …, postApply)` 인자를 `null` 로.
- `Assets/_Project/Tests/EditMode/UnitStatImport/DcSheetImportTests.cs`
  - `Deserialize_CardDto_…`: `binding` null 단언을 다른 생략 nullable(`description`) 단언으로 교체.
  - `ApplyCards_TypeBindingMismatch_WarnsInLog` 테스트 삭제(경고 자체가 사라짐).
- `Assets/_Project/Tests/PlayMode/PlacementAuraTest.cs`
  - `MakeAuraCard`: `c.binding = CardBinding.Unit;` 줄 삭제(`c.type = CardType.Unit;` 유지).

## 구현

1. 정의 계층에서 enum·필드 제거 → 컴파일 에러가 소비처를 정확히 가리킴.
2. 런타임 가드를 `type` 기반으로 전환. `ApplyDreamcatcherCardToUnit` 는 이미 `CommitUnit`(type==Unit 확인 후)에서만 호출되므로 동작 동일, 방어만 authoritative 필드로 이동.
3. sheet DTO·applier·테스트 정리.

## 완료 기준

- [x] `CardBinding` 이 코드베이스 어디에도 없다 (grep 0건, 주석 제외).
- [x] 4개 어셈블리 `dotnet build` 오류 0개 (Runtime·EditMode·PlayMode·Editor.UnitStatImport).
- [ ] Unity 테스트 실행: 미실시 (Unity 세션 unavailable — 복구 후 확인 권장).

확인: 2026-07-12 — dotnet build 컴파일 검증 (테스트 실행은 미실시).
