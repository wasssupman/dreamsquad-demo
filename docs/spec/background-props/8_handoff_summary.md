# Background Props — Handoff Summary

**작성일**: 2026-04-22  
**상태**: v1 1차 구현 완료. 기본 프리팹 prototype 위에 theme 이미지 매칭, footprint placement, runtime instantiate 연결까지 추가했다. Play smoke 는 아직 미확인.

## Prototype Scope

- `PropData` ScriptableObject 초안 (`Wassup/PropData` 메뉴).
- `PropBillboard` 기본 런타임 컴포넌트.
- `PropDataEditor` Inspector 의 `Generate Billboard Prefab` 버튼 prototype.
- 샘플 기본 프리팹: `prop_prototype_1_1.asset` + 동명 prefab.

## Implemented In V1 Pass

- `MapThemeData.tileProps / decorProps` 연동.
- `Data/Theme/{themeName}` SO 와 `Art/Theme/{themeName}` PNG 매칭.
- `BackgroundPropPlacer.Generate` 로 배경 타일 영역 순회 + 후보 프랍 필터 + seeded random placement.
- `PropPlacement` record.
- 1x1 외 2x1, 1x2, 2x2 footprint occupancy 검증 테스트.
- `MapView.InstantiateBackgroundProps` 를 통한 runtime instantiate 연결.
- `BattleBridge.BuildMapForBattle` 에서 tileProps 가 있으면 background props 경로 사용, 없으면 기존 obstacle prefab 경로 유지.

## Not Implemented Yet

- 디자이너용 batch generator / footprint gizmo / naming validation.
- Play mode smoke 확인.

## Key Files

- `Assets/_Project/Scripts/Data/PropData.cs`
- `Assets/_Project/Scripts/Data/PropPlacement.cs`
- `Assets/_Project/Scripts/Data/BackgroundPropPlacer.cs`
- `Assets/_Project/Scripts/Data/MapThemeData.cs`
- `Assets/_Project/Scripts/Core/MapView.cs`
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs`
- `Assets/_Project/Scripts/Presentation/PropBillboard.cs`
- `Assets/_Project/Editor/PropDataEditor.cs`
- `Assets/_Project/Tests/EditMode/BackgroundPropPlacerTests.cs`
- `Assets/_Project/Data/Props/prop_prototype_1_1.asset`
- `Assets/_Project/Prefabs/Props/prop_prototype_1_1.prefab`
- `docs/spec/background-props/`

## Next Step

다음 구현은 Play smoke 와 디자이너 도구 보강이다.

권장 순서:

1. 실제 theme asset 에 `tileProps` 를 채우고 Play mode 에서 생성 맵 위 자동 배치 확인.
2. `prop_{name}_{x}_{y}` filename validation 추가.
3. Theme 폴더 batch generator 추가.
4. footprint gizmo 추가.
