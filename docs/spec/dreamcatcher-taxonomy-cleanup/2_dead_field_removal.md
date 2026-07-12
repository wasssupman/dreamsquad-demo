# 2 — placementWarmupSec 잔재 제거

## 목적

구 Squad warmup 잔재 필드 `placementWarmupSec` 를 제거한다. 런타임 reader 는 이미 없고(combat-action-lock 에서 placement-aura → Sleep 로 승격되며 은퇴), sheet import 스키마에만 남아 round-trip 되고 있다. warmup 개념 자체가 은퇴했으므로 필드·sheet 컬럼·테스트 흔적을 걷어낸다.

## 유지(제거 안 함) — 근거

- `category`/`CardCategory`: 제거 대상 아님. `DreamcatcherDeckBuilderView.IsSubconscious` 가 무의식 프레임 색 결정에 **살아있는 소비처**로 사용. (DreamcatcherCard 의 "RETIRED — no consumer" 주석은 오기 — unit 0 범위 아니면 이 unit 에서 주석만 정정.)

## 변경 대상

- `Assets/_Project/Scripts/Data/Dreamcatcher/DreamcatcherCard.cs`
  - `public float placementWarmupSec;` 필드 + RETIRED 주석 블록 삭제.
  - `category` 필드의 "no consumer" 오기 주석 정정(살아있는 소비처 명시).
- `Assets/_Project/Scripts/Data/StatImport/DcSheetImportDto.cs`
  - `DcCardDto.placementWarmupSec` 필드 삭제(리플렉션 매퍼가 이름으로 round-trip → sheet 컬럼 자동 제거).
- `Assets/_Project/Tests/EditMode/UnitStatImport/DcSheetImportTests.cs`
  - `Deserialize_CardDto_…`: `placementWarmupSec` 파싱 단언 제거(JSON 에서도 해당 키 제거).
  - `ApplyCards_UpdatesFieldsAndKeepsOmitted`: `so.placementWarmupSec` 세팅·단언 제거. "생략 컬럼 보존"은 남는 다른 필드(`axis`)로 이미 커버되므로 테스트 의도 유지.

## 구현

1. SO·DTO 에서 필드 제거 → 컴파일 에러가 sheet/테스트 잔존 참조를 가리킴.
2. 테스트에서 placementWarmupSec 흔적 제거, "생략 보존" 의도는 다른 필드로 유지.
3. `category` 주석 정정.

## 완료 기준

- [x] `placementWarmupSec` 심볼이 코드베이스에 없다 (grep 0건, 주석 제외).
- [x] 4개 어셈블리 `dotnet build` 오류 0개.
- [x] `DcSheetImportTests` 논리 불변(생략-보존 단언을 `description` 필드로 유지, partial-update 의미 그대로).

확인: 2026-07-12 — dotnet build 컴파일 검증 (테스트 실행은 미실시).
