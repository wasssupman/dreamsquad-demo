# 0 — InstantiateProp 추출

## 목적

`TilemapMapView.InstantiateBackgroundProps` 의 per-prop 인스턴스화 로직을 `InstantiateProp(...)` 로 추출한다. 임의의 **resolved `PropData`** 를 지정 위치에 배치할 단일 재사용 지점을 만드는 게 목적. **동작·시각 무변경** — 기존 배경 프랍 loop 는 추출된 메서드를 그대로 호출한다.

## 변경 대상

- `Assets/_Project/Scripts/Core/TilemapMapView.cs` (`InstantiateBackgroundProps`, 약 278–316)

## 구현

- 현재 loop body 의 per-prop 부분(centerX/Y 계산 → `Instantiate(prop.prefab, root)` → name → position/scale → `MapView.ApplyPropSorting` → `MapView.DisablePropDebugMarkers` → `AttachPropBlob` → `MapView.ApplyPropGlobalTint`)을 다음으로 추출:

  ```csharp
  private void InstantiateProp(PropData prop, PropPlacement placement,
                               BoardVisualPlan plan, MapThemeData theme, Transform root)
  ```

- `InstantiateBackgroundProps` 는 인덱스 해석(`theme.tileProps[placement.propIndex]`)과 null 가드만 남기고, loop 에서 `InstantiateProp(prop, placement, plan, theme, _backgroundPropsRoot)` 호출.
- **계약**: `InstantiateProp` 은 `placement.propIndex` 로 `tileProps` 를 재조회하지 않는다 — 넘겨받은 `prop` 을 그대로 쓴다(구조물 프랍은 tileProps 밖이라 이게 필수).
- View 전용. EntityManager/SystemAPI 무관. billboard 경로 그대로.

## 완료 기준

- compile 통과 (`read_console` 클린).
- Play → 게임뷰: 기존 배경 프랍의 위치·스케일·그림자·틴트가 **추출 전과 동일**(회귀 없음). 스크린샷 육안 확인.
