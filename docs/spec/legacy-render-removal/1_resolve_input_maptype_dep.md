# 1. 입력/배치의 MapView 타입 의존 해소

## 목적

입력/배치 계층이 `MapView` **타입**을 물고 있어 unit 2 삭제가 막힌다. 실측(2026-07-03) 결과 이 의존은 전부 **dead reference** — hover/reject 피드백은 이미 `BattleBridge.SetPlacementHover/FlashTileReject` 단일 경로(tilemap-view-backend unit 3)로 통일돼 있고, `mapView` 필드는 선언/대입만 있고 읽기 0건. 따라서 "중립 인터페이스 전환"이 아니라 **dead 필드 삭제**로 해소한다.

## 변경 대상

- `Assets/_Project/Scripts/Core/PlacementInput.cs:22` — `[SerializeField] MapView mapView` 미사용 필드 삭제
- `Assets/_Project/Scripts/UI/DefenderDragPlacementController.cs:15,33,36` — `mapView` 필드(쓰기만 존재) + `Configure(..., MapView view, ...)` 파라미터 삭제
- `Assets/_Project/Scripts/UI/DefenderSelector.cs:198` — `Configure` 호출에서 `bridge.MapView` 인자 제거
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs:1289` — `public MapView MapView` 프로퍼티 삭제 (유일 소비자 = DefenderSelector:198)

## 구현

1. 위 4파일에서 해당 선언/인자만 기계적으로 삭제. 로직 변경 0.
2. `rg "MapView" Assets/_Project/Scripts/{Core/PlacementInput.cs,UI/}` 로 잔여 타입 참조 0건 확인 (주석 제외).

**주의**:
- SerializeField 제거로 `BattleScene.unity` YAML 에 stale 참조가 남지만 무해(Unity 가 무시). **씬 저장 금지** — 씬이 사용자 WIP 로 dirty 상태(메모리: SaveScene 은 미저장 WIP 까지 베이크).
- `BattleBridge.mapView` 필드 자체(82행)는 unit 2 에서 제거. 여기서는 공개 프로퍼티만.

## 완료 기준

- [ ] compile 통과 (에러 0)
- [ ] `PlacementInput/DefenderSelector/DefenderDragPlacementController` 에 `MapView` 타입 참조 0건
- [ ] Play 검증: D&D 배치 — 드래그 hover 하이라이트 / 유효 배치 / 무효 셀 reject 플래시 동작 동일
