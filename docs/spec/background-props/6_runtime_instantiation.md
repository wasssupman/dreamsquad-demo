# Runtime Instantiation

**작업 구분**: 6 / Runtime Integration

## 목적

footprint placement 결과를 실제 scene object 로 생성한다. 생성 책임은 `MapView` 확장 또는 별도 `BackgroundPropSpawner` 중 하나로 둘 수 있다.

## 권장 구조

초기 구현은 별도 컴포넌트를 권장한다.

```text
BattleBridge
  -> GeneratedMap 생성
  -> BackgroundPropPlacer.Generate(map, theme, seed)
  -> MapView.Initialize(map, tileSize)
  -> BackgroundPropSpawner.Spawn(placements, theme, tileSize)
```

이유:

- `MapView` 는 이미 tile mesh/obstacle/goal 표시 책임이 많다.
- 배경 프랍 배치는 별도 검증/교체/비활성화가 쉬워야 한다.
- Decor Prop 외곽 배치까지 들어오면 MapView 와 책임이 더 멀어진다.

## BackgroundPropSpawner

SerializeField:

- `Transform root`
- `float tileSize`

public API:

```csharp
public void Clear();
public void Spawn(IReadOnlyList<PropPlacement> placements, MapThemeData theme, float tileSize);
```

동작:

1. 기존 root 하위 프랍 제거.
2. placement 순회.
3. `theme.tileProps[placement.propIndex]` 확인.
4. `PropData.prefab` null 이면 warning 1회 또는 skip.
5. footprint 중심 world position 계산.
6. prefab instantiate.
7. instance 이름은 `{prop.name}_{x}_{y}`.

## Decor Props

맵 외곽 장식용 프랍은 같은 `PropData.prefab` 을 사용하되, tile placement 와 분리한다.

초기 방식:

- 디자이너가 scene 또는 theme prefab 에 직접 배치.
- 또는 `MapThemeData.decorProps` 는 데이터만 보관하고 자동 배치는 후속.

후속 방식:

- map bounds 바깥 anchor ring 을 만들고 seeded random 배치.
- camera framing 에 따라 보이는 외곽만 배치.
- tile occupancy 를 사용하지 않는다.

## 완료 기준

- generated placement 수만큼 prefab instance 생성.
- `Clear()` 후 중복 instance 없음.
- `PropBillboard` 가 runtime 에서 `PropData` 값을 반영.
- prefab null 은 skip 되고 전체 생성은 계속 진행.
- Play smoke 에서 생성 맵 배경 타일 위에 프랍 표시.
