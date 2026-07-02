# 1 — 근경 placer 를 playAreaProps 로 이관

## 목적

근경 배치 경로 전체를 `tileProps` → `playAreaProps` 로 바꾼다. 룰렛 base weight 를 `PropData.placementWeight` 대신 `WeightedProp.weight` 에서 읽는다. `propIndex` 는 `playAreaProps` 인덱스가 된다.

## 변경 대상

- `Assets/_Project/Scripts/Data/BackgroundPropPlacer.cs`
- `Assets/_Project/Scripts/Core/TilemapMapView.cs` (`InstantiateBackgroundProps`)
- `Assets/_Project/Scripts/Core/MapView.cs` (레거시 `InstantiateBackgroundProps`)
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` (`:737` 가드)
- `Assets/_Project/Tests/EditMode/BackgroundPropPlacerTests.cs`

## 구현

### BackgroundPropPlacer

`theme.tileProps[i]` (PropData) 접근을 모두 `theme.playAreaProps[i]` (WeightedProp) 로 이관:

- `Generate` null 가드(`:12`) → `theme.playAreaProps`.
- prop 조회(`:37`, `:199`, `:380`) → `theme.playAreaProps[i].prop`.
- 배열 길이(`:196`, `:197`, `:210`, `:378`) → `theme.playAreaProps.Length`.
- `TrySelectProp` base weight(`:206`): `math.max(0, prop.placementWeight)` → `math.max(0f, entry.weight)` (entry = `theme.playAreaProps[i]`).
- `IsEligible`(`:324`)의 `prop.placementWeight <= 0` 게이트 제거 — weight 게이트는 룰렛에서 `entry.weight` 로 처리(0 이면 자연 배제). `IsEligible` 은 prefab/region/anchor/proximity 만 판정하도록 시그니처 유지.
- `ViolatesSameCategory`(`:372`, `:380`) 의 `theme.tileProps` null 가드·재조회 → `theme.playAreaProps` / `.prop`.

### 인스턴스화 재조회

- `TilemapMapView.InstantiateBackgroundProps`(`:292`, `:303`, `:304`): `theme.tileProps` → `theme.playAreaProps`, prop = `theme.playAreaProps[propIndex].prop`.
- `MapView.InstantiateBackgroundProps`(`:773`, `:783`, `:786`): 동일 이관.

### BattleBridge 가드

`:737` `theme.tileProps != null && theme.tileProps.Length > 0` → `theme.playAreaProps`.

### 테스트

`BackgroundPropPlacerTests.cs` 의 `theme.tileProps = new[]{...}` 및 `prop.placementWeight = N` 을 `WeightedProp` 리스트로 변환. 헬퍼 추가:

```csharp
private static void SetPlayArea(MapThemeData theme, params (PropData p, float w)[] entries)
    => theme.playAreaProps = entries.Select(e => new WeightedProp { prop = e.p, weight = e.w }).ToArray();
```

weight 를 쓰던 테스트(`Generate_AvoidsRecentlyUsedProp`: 1000/1, `Generate_PrefersSmallProps`: 1/1000)는 `placementWeight` 대신 entry.weight 로 지정. 나머지는 기본 weight(10) 로.

## 완료 기준

- compile 성공.
- `run_tests` EditMode `BackgroundPropPlacerTests` 전 케이스 green.
- Play 시 근경 프랍 배치 육안 동일(회귀 없음). 원경은 아직 `tileProps` 사용(unit 2 대상)이라 unit 1 시점엔 정상.

확인: 2026-07-02 · `b5ad11a` — compile 클린, `BackgroundPropPlacerTests` 12/12 green (weight 의존 2개 포함).
