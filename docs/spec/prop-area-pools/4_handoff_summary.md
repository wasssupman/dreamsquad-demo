# 4 — Handoff Summary

## Commit

- `a0ce63c` unit 0 — 데이터 모델 + 마이그레이션
- `b5ad11a` unit 1 — 근경 placer → playAreaProps
- `86911ea` unit 2 — 원경 placer → distantRingProps
- (unit 3 cleanup — 이 커밋)

## Implemented

- `MapThemeData` 가 근경/원경 프랍 풀을 두 개의 독립 `WeightedProp[]` 로 소유: `playAreaProps`, `distantRingProps`.
- `WeightedProp = { PropData prop; float weight=10 }`. weight = 룰렛 base. weight<=0 / prefab 없음 = 제외.
- 근경(`BackgroundPropPlacer`)은 `playAreaProps` 만, 원경(`TilemapMapView.InstantiateRingProps`)은 `distantRingProps` 만 순회 — 교차 참조 없음.
- 같은 PropData 를 양쪽에 다른 weight 로 등록 가능. 한쪽에만 넣으면 그 영역 전용 → 플레이 전용 프랍이 원경에 새던 문제 해소.
- 인스펙터에서 영역별 가용 목록 + 항목별 weight 를 직접 authoring.
- 제거: `MapThemeData.tileProps`, `PropData.placementWeight`/`excludeFromDistantRing`/`distantRingWeight`, `TilemapMapView.RingWeight`, 일회성 마이그레이션 스크립트.
- `BackgroundPropPlacerTests` 를 WeightedProp 기반으로 갱신 — 12/12 green.

## Key Files

- `Assets/_Project/Scripts/Data/MapThemeData.cs` — WeightedProp 정의 + 두 리스트 필드
- `Assets/_Project/Scripts/Data/BackgroundPropPlacer.cs` — 근경 placer (playAreaProps)
- `Assets/_Project/Scripts/Core/TilemapMapView.cs` — 원경 `InstantiateRingProps` (distantRingProps) + 근경 인스턴스화
- `Assets/_Project/Scripts/Core/MapView.cs` — 레거시 근경 인스턴스화
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs:737` — playAreaProps 가드
- `Assets/_Project/Tests/EditMode/BackgroundPropPlacerTests.cs`

## Verified

- compile 클린 (전 unit), dead code ref 0.
- `BackgroundPropPlacerTests` EditMode 12/12 green.
- Play 시각 검증(에디터 포커스) — `Assets/Screenshots/prop_area_pools_unit2_verify_1.png`(분리 증명: 버섯·꽃 플레이 영역 전용) / `unit3_verify_1.png`(필드 제거 후 회귀 없음).

## Notes

- 마이그레이션은 일회성으로 실행 후 삭제됨. forest/desert `.asset` 의 `tileProps`/`placementWeight` 등 YAML 은 orphan 으로 남지만 무해(직렬화 무시). 되돌리지 말 것.
- 근경 weight 의 유일 권위는 `WeightedProp.weight`. `placementWeight` 부활 금지.
- `decorProps` (MapThemeData) 는 이 spec 범위 밖 — future 예약 필드로 유지.
- 스크린샷 폴더 `Assets/Screenshots` 는 gitignore 스크래치 — 커밋 안 됨.

## Follow-up

`docs/spec/README.md` Follow-up Backlog 참조 (영역별 밀도/falloff 리스트 이관, 원경 카테고리 회피).
