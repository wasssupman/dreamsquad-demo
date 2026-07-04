# 0 — 드림스톤 데이터 모델

## 목적

드림스톤 정의 SO + 카탈로그 + 등급 캡 데이터 계약(validator)을 만든다. 이후 작업 단위(저장/UI/반입)가 전부 이 타입을 참조한다.

## 변경 대상

- new `Assets/_Project/Scripts/Data/Dreamstone/DreamstoneData.cs`
- new `Assets/_Project/Scripts/Data/Dreamstone/DreamstoneCatalog.cs`
- new `Assets/_Project/Data/Dreamstones/*.asset` (스톤 16종 + 카탈로그 1)
- new `Assets/_Project/Tests/EditMode/DreamstoneCatalogTests.cs`

## 구현

- `enum DreamstoneGrade { Common, Rare, Epic, Unique }`
- `DreamstoneData : ScriptableObject` — `string id`, `string displayName`, `DreamstoneGrade grade`, `CardEffect effect` (`Wassup.Data.CardEffect` 재사용 — kind + percent, 스톤당 1개). `CreateAssetMenu` 는 `DreamcatcherCard` 패턴 미러.
- `DreamstoneCatalog : ScriptableObject` — `DreamstoneData[] stones` + `ById(string)` (`DefenderCatalog` 미러).
- 테스트 에셋 16종: 등급 4 × 스탯 4 (`CardBuffKind` 4종). 수치 = 등급 캡 ÷ 4:
  - Common 2% / Rare 3% / Epic 5% / Unique 7.5%
  - id 규칙: `stone_{stat}_{grade}` (예: `stone_atk_unique`)
- **validator 테스트 (EditMode)**: 카탈로그 로드 →
  - (a) id 비어있음/중복 없음, stones 에 null 없음
  - (b) `effect.percent > 0`
  - (c) `effect.percent ≤ 등급 캡 ÷ 4` — 등급 캡 표 (Common 8 / Rare 12 / Epic 20 / Unique 30)
- 등급 캡 표는 validator 상수로 둔다. 런타임 소비자가 없는 설계 계약 값이라 SO 로 만들지 않는다 (런타임 표시가 필요해지면 그때 SO 승격 — 후속 후보).

## 완료 기준

- compile 클린
- EditMode `DreamstoneCatalogTests` 통과: validator (a)(b)(c) + `ById` 조회

> 완료 확인 2026-07-04 — Unity compile clean, `DreamstoneCatalogTests` validator 조건 PASS. EditMode 전체 재실행은 기존 `ObstaclePlacerTests.Place_PreservesWalkAndMinimumPlaceRatio` 실패 1건만 재발.
